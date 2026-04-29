# FlatRedBall2 — Todo

**No speculative items.** Every entry must be either (a) ready to work on now or (b) ready to discuss now. "Eventually," "someday," "maybe if it comes up" do not belong here — they're noise that buries real work and never gets revisited. If an idea is interesting but not actionable, let it surface again organically when a real use case appears; don't pre-emptively log it.

Open work only. When an item ships, delete it — don't leave a "landed" breadcrumb. Design decisions and historical context that outlive a TODO belong in skill files, XML docs, or commit messages, not here.

## Native AOT
**Priority: Eventual** — `<IsAotCompatible>true</IsAotCompatible>` is on `FlatRedBall2.csproj`; AOT analyzers run at build time. Two warning sites remain (12 warnings across 2 TFMs).

### XmlSerializer in animation loading (IL2026 + IL3050)
`AnimationChainListSave.FromFile()` and `AdobeAnimateAtlasImporter.FromFile()` use `XmlSerializer` to load `.achx` and Adobe Animate atlas XML files. `XmlSerializer` relies on runtime codegen — fundamentally AOT-incompatible.

Options:
- **A) Manual XML parsing** — `XDocument` with hand-written mapping. AOT-safe, keeps XML format, more code to maintain.
- **B) .NET 9+ XML source gen** — `XmlSerializer` gained source-gen support in .NET 9. Works on net10.0 but not net8.0 (KNI). May need `#if` or dropping net8.0 XML animation support.

**Full validation (phase 2):** `<PublishAot>true</PublishAot>` on a sample executable, `dotnet publish -c Release -r win-x64`, then exercise tilemap load and animation loading at runtime.

## Documentation Site
**Priority: Soon** — Stand up a public docs site for FlatRedBall2. Today all guidance lives in skill files (AI-facing, in-repo) and inline XML docs; a human-facing site is the missing third leg.

Open questions:
- **Content sourcing.** Large portions of the skill files are high-quality prose that could seed the docs (e.g. "entities and factories," "collision relationships," "content-boundary"). Tension: skills are AI-optimized (terse, bullet-heavy), docs are human-optimized (more narrative, more examples). Do we fork the content, cross-reference, or generate one from the other?
