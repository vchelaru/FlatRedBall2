# Phase 8 — Factories / Spawning

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Partly implemented — spawning by name works; pooling and partitioning deferred, see §9. |
| **Depends on** | Phase 6 (abstract elements get no factory; entity instances exist) |
| **Blocks** | Phase 9 (relationships take entity lists), Phase 10 (TMX spawns entities) |
| **Suggested branch** | `804-phase-8-factories` |

---

## 1. The problem

Glue entities that other entities spawn are marked `CreatedByOtherEntities`, and Glue generates a
`<Entity>Factory` for each. Screens wire their entity lists to those factories, so
`PlayerBallFactory.CreateNew()` puts the new ball into `PlayerBallList` automatically.

FRB2 has `Factory<T>` and it is a good fit — **except for one structural problem that has no
workaround at the current API**: FRB2 keys factories on the CLR type, and every loaded entity is the
same CLR type. See G80. **Resolve D80 before writing any code.**

---

## 2. Scope

### In scope

1. Recognise `CreatedByOtherEntities` and create a factory per Glue entity type.
2. Wire entity lists to factories via `AssociateWithFactory`.
3. `PooledByFactory` → `Factory<T>.EnablePooling()`.
4. Refuse a factory for an abstract entity.

### Out of scope

- `ListsToAddTo` as a general mechanism — FRB2 factories own exactly one list (G82).
- `NewInstancesCreatedThisScreen`, `PoolCount`, `FactoryManager.GetCreationReport` — diagnostics
  with no FRB2 equivalent.
- `Factory.Initialize(contentManagerName)` — content scope is Phase 4's concern.
- The public spawn API (`Factory<GlueEntity>.Create(string)`) → Phase 14.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | Factories exist per entity type | Beefball's four spawnable entities get four factories. | §6.2 |
| F2 | Lists are wired | A spawned puck lands in `PuckList`. | §6.3 |
| F3 | Pooling matches the author's choice | A pooled entity recycles rather than allocating. | §6.4 |
| F4 | Abstract entities get none | An entity with an unfilled slot cannot be spawned. | §6.5 |

---

## 4. Proposed resolution

### When a factory exists

`CodeWriter.cs:357`:

```csharp
if (entitySave.CreatedByOtherEntities && !entitySave.IsAbstract)
    FactoryElementCodeGenerator.GenerateAndAddFactoryToProjectClass(entitySave);
```

Both conditions. `IsAbstract` is computed from `SetByDerived` NamedObjects (Phase 6 G63).

### Which lists wire to which factory

`GetEntitiesToInstantiateFactoriesFor` (`FactoryElementCodeGenerator.cs:114-148`):

```csharp
nos => !nos.InstantiatedByBase
    && nos.SourceType == SourceType.FlatRedBallType
    && nos.IsList
    && !nos.IsDisabled
    && !string.IsNullOrEmpty(nos.SourceClassGenericType)
    && nos.SourceClassGenericType.StartsWith("Entities\\" or "Entities/")
```

Then the list's element type **plus every entity recursively derived from it**, filtered to
`CreatedByOtherEntities && !IsAbstract`. Wiring is gated on the list's `AssociateWithFactory`.

Note the dependencies: `IsList` (computed — Phase 2 G24), `SourceClassGenericType` (missing — Phase 6
G67), `IsDisabled` (missing — Phase 6 §6.0), `InstantiatedByBase` (Phase 6). **This phase cannot
start before Phase 6's model audit lands.**

### Mapping

| FRB1 | FRB2 |
|---|---|
| `<T>Factory.Self` singleton | a `Factory<T>` instance owned by the screen |
| `Initialize(contentManagerName)` | constructor takes the `Screen` |
| `CreateNew(x, y, z)` | `Create(configure)` |
| `AddList(list)` | — factory owns `Instances` (G82) |
| `PooledByFactory` | `EnablePooling()` |
| pre-allocate 20 | `Prewarm(20)` |
| `MakeUnused(obj, callDestroy)` | `Destroy(instance)` — no separate return-to-pool |
| `SortAxis` | `PartitionAxis` — but sorted per frame, not at insert |
| `Destroy()` | `DestroyAll()` |

---

## 5. Gotchas

### G80 — One CLR type means one factory per screen · **Blocker, resolve before coding**

`FlatRedBallService.RegisterFactory<T>` is `_factories[typeof(T)] = factory` (`:722`) — an
**assignment, not an `Add`**. Every loaded entity is `GlueEntity`. Therefore:

- exactly **one** `Factory<GlueEntity>` can exist per screen, and a second silently overwrites the
  first;
- `GetFactory<GlueEntity>()` cannot mean "the Player factory";
- `Instances` mixes every Glue entity type together, so a Phase 9 relationship taking
  `IReadOnlyList<A>` receives Players *and* Doors;
- `EnablePooling()` pops from one type-blind free list (`Factory.cs:304`), so
  **`Create("Entities\\Player")` can hand back a recycled `Door`**;
- `IsSolidGrid` (`:144`) assumes one uniform cell size across the factory;
- `PartitionAxis` (`:189`) sorts a heterogeneous list.

DoorsDemo needs two factories; Beefball needs four. **This is not a corner case — it blocks the
phase's primary story.** D80.

### G81 — `AssociateWithFactory` defaults `false` when absent, and FRB1 says so deliberately

It is bag-backed (`NamedObjectSave.cs:686`), and `SetDefaults()` sets it true (`:877`) but the
**constructor deliberately does not** — with an explicit comment at `:856-858`: *"do not set this to
true: AssociateWithFactory. This will result in accumulation of values."*

So `Properties.GetValue<bool>` → `false` when absent, and that is correct. **Do not "fix" this by
defaulting to true** — it is the one bool in this family that genuinely defaults false, and Phase
1's G3 instinct points the wrong way here.

In practice the key is always written: 270 occurrences repo-wide, **every one `true`**.

Its use is version-gated: only consulted when `FileVersion >= ListsHaveAssociateWithFactoryBool`
(3); below that every entity list is eligible unconditionally
(`FactoryElementCodeGenerator.cs:248-256`).

### G82 — `ListsToAddTo` is a many-lists mechanism; FRB2 has one implicit list

FRB1's factory holds `static List<IList> ListsToAddTo` and adds each new instance to **every**
registered list (`:331-349`). `AddList<T>` accepts any `IList<T>` where `T` is the **root base
entity** (`:198-214`), so `ZombieFactory.AddList(enemyList)` is legal.

FRB2's `Factory<T>` owns `Instances` and that is the only list.

For the common case — one list per entity type — these are equivalent. They diverge when a project
registers a polymorphic list, which is exactly what an entity-inheritance project does.

**How we tackle it.** Map the single-list case directly; diagnose the multi-list and polymorphic
cases. Note `TmxCodeGenerator.cs:198` *temporarily swaps* `ListsToAddTo` so tile-spawned entities
land in an owning entity's list rather than the screen's (FRB1 issue #582) — Phase 10 will hit this.

### G83 — Pooling changes the entity, not just the factory

`PooledCodeGenerator.cs:12-18`: a poolable entity **implements `IPoolable`** and gains `Index` and
`Used` properties. Its `Destroy` returns itself to the pool before `base.Destroy()`
(`FactoryElementCodeGenerator.cs:36-53`), and it gains a `PostInitialize()` call (`:315-355`).

FRB2 keeps pool membership entirely inside `Factory<T>` (`Entity._isPooled`, `src/Entity.cs:292`),
which is a cleaner design and needs nothing on the entity.

**The observable difference that matters:** FRB2's pooled recycle runs `configure` on an entity whose
`CustomInitialize` will **not** re-run (`frb-skills/entities-and-factories/SKILL.md:107-111`). For a
`GlueEntity`, `CustomInitialize` *is* `BuildObjects()` — so a recycled entity keeps the children it
built the first time. That matches FRB1's pooling semantics, but it is load-bearing and needs a test
either way.

### G84 — Pooling is unexercised by every vendored fixture

`PooledByFactory: true` appears in exactly **14 files repo-wide, all in
`Tests/TestProjectDesktopNet6`** — zero samples. All six fixture entities have
`CreatedByOtherEntities: true` and **none** sets `PooledByFactory`.

So the fixtures exercise the non-pooled path only. FRB1's test project has the interesting
combinations by design: `DerivedPooledFromPooled`, `DerivedPooledFromNotPooled`,
`DrivedNotPooledFromBaseNotPooled` [sic].

**How we tackle it.** Vendor those three; do not claim pooling coverage without them.

### G85 — `IsPooled` does not exist

The issue lists `IsPooled` alongside `CreatedByOtherEntities` and `PooledByFactory`. **There is no
such member anywhere in FRB1** — the only near-match is a local variable
(`FactoryElementCodeGenerator.cs:319`). Do not go looking for it.

### G86 — Factory teardown reaches beyond the screen's own lists

`GenerateDestroy` (`:150-204`) destroys factories for every entity reachable from the screen's lists
**plus** every project entity with `CreatedByOtherEntities && !IsAbstract` that inherits from a
listed type. A screen holding a `PositionedObjectList<Enemy>` tears down `ZombieFactory` too.

FRB2's screen teardown already destroys and clears all registered factories
(`FlatRedBallService.ActivateScreen`), which covers this — but only because FRB2 tracks factories per
screen rather than statically. Worth a test so the equivalence is recorded rather than assumed.

---

## 6. Tasks

Test-first throughout. **Do §6.1 first — it is a design decision, not an implementation step.**

### 6.1 — Resolve the identity problem

- [ ] Decide D80 and record the reasoning here.
- [ ] Failing test: two Glue entity types in one screen get two independent factories.
- [ ] Failing test: pooling never returns an instance of a different Glue entity type (G80).

### 6.2 — Factory creation

- [ ] Failing test: an entity with `CreatedByOtherEntities` gets a factory.
- [ ] Failing test: one without does not.
- [ ] Failing test: an abstract entity gets none, even with `CreatedByOtherEntities` (§4).

### 6.3 — List wiring

- [ ] Failing test: Beefball's `PuckList` receives instances created by the Puck factory.
- [ ] Failing test: an absent `AssociateWithFactory` means **not** wired (G81).
- [ ] Failing test: a list whose `SourceClassGenericType` is not under `Entities\` is skipped.
- [ ] Failing test: a polymorphic list (element type has derived entities) warns (G82).
- [ ] Failing test: `InstantiatedByBase` lists are excluded.

### 6.4 — Pooling

- [ ] Failing test: `PooledByFactory` calls `EnablePooling()` and prewarms 20.
- [ ] Failing test: a recycled `GlueEntity` keeps its built children and does not rebuild (G83).
- [ ] Vendor the three pooled-inheritance entities (G84).

### 6.5 — Teardown

- [ ] Failing test: a screen transition destroys every factory it created (G86).

### 6.6 — Wrap-up

- [ ] XML docs; update this document and `plan/plan.md`.
- [ ] Record for Phase 9: relationships take `IReadOnlyList<A>`, which is whatever D80 produces.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D80 | How does one CLR type get many factories? | **Key the registry on `(Type, glueElementName)`.** Smallest change that fixes every symptom in G80 — the existing `Dictionary<Type, IFactory>` becomes `Dictionary<(Type, string?), IFactory>` with `null` for hand-written entities, so nothing outside the Glue path changes. Rejected: a `GlueSpawner` owning per-name sub-lists (leaves `Factory<T>`'s pooling and partitioning unusable), and runtime type emission (defeats the epic's no-codegen premise and is not trim-safe). |
| D81 | Map `SortAxis` onto `PartitionAxis`? | **Yes, with a recorded difference.** FRB1 inserts in sorted position at spawn; FRB2 insertion-sorts once per frame (`Factory.cs:218-237`). Same steady state, different transient. Note it; do not chase it. |
| D82 | Reproduce `MakeUnused(obj, callDestroy: false)`? | **No.** FRB2's `Destroy` branches internally on `_isPooled`, which is the same outcome by a cleaner route. The FRB1 flag exists to avoid an infinite loop in its own destroy chain — a problem FRB2 does not have. |
| D83 | Honour the `DefaultLayer` a factory carries? | **Defer.** FRB2's `Factory` takes its layer from the screen (`Factory.cs:277`). Layers are not modelled anywhere in this epic yet; revisit if a phase adds them. |

---

## 8. Definition of done

- [ ] `dotnet build` clean; `dotnet test` green.
- [ ] A real `PublishTrimmed` emits no IL warnings from `src/Glue` (Phase 2 G26).
- [ ] Beefball creates four distinct factories and a spawned puck lands only in `PuckList`.
- [ ] Pooling never crosses Glue entity types (G80) — the test that would have caught the bug.
- [ ] An abstract entity gets no factory.
- [ ] The three pooled-inheritance fixtures are vendored and passing (G84).
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.

---

## 9. What landed, and how D80 was actually resolved

**D80 recommended keying the engine's factory registry on `(Type, glueName)`.** That is not what
shipped, and the reason is worth recording.

`Factory<T>` is registered as `_factories[typeof(T)]`, and every loaded entity is `GlueEntity` — the
blocker G80 describes. But changing the engine's registry shape means touching core API that
hand-written games depend on, to serve the loader. The cheaper route turned out to be sufficient:
`Screen.Register(Entity)` already does all the wiring a spawned entity needs, so `GlueProject` owns
a per-Glue-name instance list and registers through that.

| | FRB1 factory | What shipped |
|---|---|---|
| Create by name | `PlayerFactory.CreateNew()` | `project.CreateEntity(@"Entities\Player", screen)` |
| The list to collide against | `ListsToAddTo` | `project.InstancesOf(@"Entities\Player")` |
| Pooling | `PooledByFactory` → free list | **not wired** |
| Spatial partitioning | `SortAxis` | **not wired** |

**What this costs:** pooling and partitioning are unavailable to loaded entities. No vendored fixture
sets `PooledByFactory` — 14 files repo-wide, all in FRB1's test project — so nothing exercised it
either way, and shipping it untested was the worse option. `AssociateWithFactory` is parsed and not
consulted, because with one instance list per name there is nothing to associate.

**What it buys:** nested entities instantiate. Beefball's `GameScreen` now spawns its players, puck
and goals from `PositionedObjectList` contents, which is what makes it look like the game rather
than an empty arena. That was blocked on nothing but the missing project context.

Revisit the registry change if a project actually needs pooling; the seam to change is
`FlatRedBallService._factories`, and G80 still describes it accurately.
