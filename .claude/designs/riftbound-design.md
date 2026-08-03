# Riftbound — Game Design Document

> **Companion docs:** [Implementation Plan](riftbound-tasks.md) | [Engine Reference](riftbound-refs.md) (read before writing code)

## One-Sentence Pitch

A desperate father races across three fractured worlds — steampunk, ethereal, and cyberpunk — to rescue his daughter from a realm torn apart by cosmic gravitational waves, in a classic JRPG built on fast ATB combat, 20+ recruitable allies, living pet companions, and deep crafting.

---

## 1. Game Overview

### Title
**Riftbound**

### Elevator Pitch
When gravitational waves from a tri-galaxy black hole merger rip the world in two, half the population is thrown into an ethereal realm. Among them: your daughter. You are Kael, a steamwright engineer who will cross the boundary between worlds — and eventually a third, virtual world built to bridge them — to bring her home. Time is running out. The ethereal realm is unstable, and the rift is closing.

### Tone and Mood
- **Urgent and desperate.** The player should feel the clock ticking. NPCs remind you that the rift is destabilizing. Story beats introduce doubt — is this even possible? Is she still alive? Are you making things worse by trying?
- **Hopeful in the cracks.** Despite the urgency, moments of warmth break through — a party member's loyalty, a pet's affection, a town that rebuilt against the odds. The game earns its emotional highs by not flinching from the lows.
- **Aesthetically rich.** Three worlds, three visual languages. The overworld hums with brass and steam. The ethereal realm shimmers with impossible light. The virtual world pulses with neon geometry. Moving between them should feel like stepping into a different painting.

### Core Experience Goals
1. The player should feel emotionally invested in saving their daughter — not because the game tells them to care, but because the world, characters, and stakes make them care.
2. Combat should feel fast, fluid, and satisfying — never tedious, never punishing through unfair mechanics.
3. Exploration should reward curiosity without punishing players who want to push the main story forward.
4. Every system (pets, crafting, party management) should feel like it adds to the experience, never like homework.

### Aesthetic Blend
The world of Riftbound is not purely one genre. Before the Rift, the world was advancing through a steampunk industrial revolution while also practicing traditional magic. The virtual world was created post-Rift using a fusion of both traditions plus new computational science. This gives the game three distinct aesthetic pillars:

| World | Aesthetic | Tech Level | Color Palette |
|-------|-----------|------------|---------------|
| Overworld | Steampunk | Brass, gears, steam engines, airships | Warm browns, copper, amber, coal smoke |
| Ethereal Realm | Arcane/Dreamlike | Pure magic, crystalline structures, floating islands | Cool purples, silver, translucent blues, aurora greens |
| Virtual World | Cyberpunk/Tron | Neon grids, data streams, holographic constructs | Electric blue, hot pink, black, white grid lines |

---

## 2. Story and World Design

### The Cataclysm
Three galaxies, each containing a supermassive black hole, entered a merger spiral. The resulting gravitational waves propagated across spacetime and struck the planet of Aethon with catastrophic force. The waves did not destroy the world — they fractured reality itself. Gravimetric eddies, proton cascades, and neutrino storms tore the fabric of the material plane, shearing it into two overlapping but separated realms.

Roughly half the population — seemingly at random — was pulled into what survivors call the **Ethereal Realm**: a dreamlike mirror of the world where magic flows freely but physical matter is unstable. The other half remained on the **Overworld**, where the steampunk civilization scrambles to survive without half its people, its infrastructure shattered.

In the years since the Rift, those in each realm occasionally catch glimpses of the other — shadows in mirrors, whispers in empty rooms, a child's laughter in a house where no child lives. But no one has crossed between them. Not yet.

### The Virtual World
A coalition of the Overworld's greatest steamwrights and the Ethereal Realm's most powerful mages — communicating through fragile, flickering rifts — collaborated to build a third space: the **Nexus**, a virtual world constructed from computational steam-logic and crystallized magical theory. The Nexus was designed as a bridge, a meeting ground where both populations could interact.

It worked — partially. The Nexus is stable, traversable, and allows communication between realms. But it cannot transfer physical matter or living beings between worlds. It is a meeting room, not a doorway. Or so everyone believes.

### The Protagonist: Kael
Kael is a 38-year-old steamwright — an engineer who builds and maintains steam-powered machinery in the industrial city of Brasshollow. He is practical, stubborn, and quietly desperate. His daughter, **Lira** (age 12), was pulled into the Ethereal Realm during the Rift. His wife, **Maren**, died in the chaos of the cataclysm itself — crushed when their workshop collapsed from the gravimetric shockwaves.

Kael has spent three years searching for a way to cross into the Ethereal Realm. He has exhausted every official channel. The coalition says it cannot be done. The Nexus engineers say the bridge only carries data, not people. But Kael has found something — old research notes from a scientist named **Dr. Veyla Ashcroft**, who theorized that the Rift is not a wall but a membrane, and that with the right harmonic frequency, it could be crossed.

The game begins when Kael activates Ashcroft's prototype device and tears a small, unstable hole between worlds.

### Narrative Arc — Key Story Beats

**Act I: The Overworld (Chapters 1-4)**
- Kael activates the prototype in Brasshollow. It works — briefly. He glimpses the Ethereal Realm before the device overloads.
- He learns he needs three **Harmonic Crystals** — one attuned to each realm — to stabilize the crossing.
- Journey across the Overworld to find the first crystal, hidden in the ruins of a pre-Rift research facility.
- Recruit early party members. Encounter the **Ironclad Authority**, the Overworld's militaristic government, who want to control the Rift technology for political power.
- **Doubt moment:** Kael meets a seer who tells him Lira may no longer be the girl he remembers — the Ethereal Realm changes people.

**Act II: The Ethereal Realm (Chapters 5-8)**
- Kael makes his first crossing. The Ethereal Realm is beautiful and terrifying — physics are loose, magic is ambient, and the landscape shifts like a dream.
- He finds traces of Lira but cannot reach her. She is deeper in, in a region called the **Fade**, where the realm is dissolving entirely.
- The second Harmonic Crystal is here, guarded by an entity called the **Reverie** — a semi-sentient manifestation of the realm's collective unconscious.
- Recruit ethereal party members. Learn that many people in the Ethereal Realm do not want to return — they have built new lives and fear the crossing would destroy what they have built.
- **Doubt moment:** Kael receives a vision of Lira asking him to stop. Is it real, or is the Reverie manipulating him?

**Act III: The Nexus (Chapters 9-11)**
- To reach the Fade, Kael needs the third crystal — and it is inside the Nexus, embedded in the core architecture that keeps the virtual world running. Removing it could collapse the Nexus entirely, severing the only communication link between realms.
- The Nexus has its own inhabitants: digital echoes of people, AIs that have gained sentience, and a shadowy figure called **The Architect** who claims to have designed the Nexus for a purpose no one else understands.
- Moral dilemma: save your daughter at the cost of cutting off the two realms from each other permanently?
- **Doubt moment:** The Architect reveals that the Rift is not random — it was triggered deliberately by someone on Aethon who wanted to harness the black hole merger's energy. The cataclysm was engineered.

**Act IV: The Fade and Resolution (Chapters 12-14)**
- Kael enters the Fade — the dissolving edge of the Ethereal Realm where reality is coming apart.
- Finds Lira. She has changed — she has absorbed ethereal energy and exists partially in both realms simultaneously. She cannot simply be "brought back."
- The engineered cataclysm's architect is revealed: **Chancellor Draven**, leader of the Ironclad Authority, who sought to weaponize interdimensional energy.
- Final confrontation. The resolution depends on choices made throughout the game — but always involves Kael finding a way to reunite with Lira, even if the definition of "reunite" is not what he expected.
- The ending is bittersweet but hopeful. The rift between worlds narrows but does not close. Communication improves. Lira is saved, but the world is permanently changed.

### World Details

#### The Overworld
- **Brasshollow** — Kael's home city. Industrial, smoky, built into a canyon. Vertical city with steam-powered elevators connecting tiers. The lower tiers are working class; the upper tiers house the Ironclad Authority.
- **The Rustfields** — Agricultural region where steam-powered tractors work fields of copper-veined grain. Peaceful but haunted — half the farming families were lost to the Rift.
- **Cogspire Academy** — A university city dedicated to steam science. Home to Dr. Ashcroft's abandoned laboratory. The faculty is split between those who want to study the Rift and those who want to seal it forever.
- **Fort Ironmaw** — Military stronghold of the Ironclad Authority. Built from salvaged war machines. The Authority controls Rift research and will not tolerate Kael's unsanctioned experiments.
- **The Scorched Vents** — Volcanic region where geothermal steam powers the continent's largest factories. Dangerous, industrial, home to outcasts and smugglers.

#### The Ethereal Realm
- **Shimmer Hollow** — The first settlement Kael finds. Built from crystallized magic, it glows softly. The people here have adapted — they weave magic into daily life the way the Overworld uses steam.
- **The Driftwood Expanse** — A vast floating forest where trees grow upside down and gravity is optional. Navigation requires understanding the local magical currents.
- **The Reverie's Garden** — A surreal landscape that responds to the emotions of those within it. Flowers bloom from memories. Rivers flow with liquid light. The Reverie itself dwells here.
- **Mistveil Citadel** — A fortress built by ethereal inhabitants who reject contact with the Overworld. They believe the Rift was divine judgment and that attempting to reverse it is blasphemy.
- **The Fade** — The edge of the Ethereal Realm where reality dissolves. Geometry fractures. Time loops. Colors bleed. This is where Lira is trapped, and it is collapsing.

#### The Nexus (Virtual World)
- **The Grid** — The entry point. A flat, infinite plane of glowing blue lines. Simple, sterile, disorienting. New visitors always arrive here.
- **Codefall City** — The Nexus's largest settlement. Built by digital echoes and sentient AIs. Architecture is impossible — buildings that exist in four dimensions, streets that loop in on themselves. Neon and holographic signage everywhere.
- **The Archive** — A vast data library containing the combined knowledge of both realms. Guarded by AI constructs. Some of the data is corrupted, some is classified, some is alive.
- **The Core** — The heart of the Nexus. A pulsing sphere of light that contains the third Harmonic Crystal. Removing it means the Nexus dies. The Architect guards it.
- **Glitch Zones** — Unstable regions where the Nexus's code is breaking down. Enemies here are corrupted data fragments. The environment shifts and glitches unpredictably.

### Inter-Realm Interaction
Throughout the game, the player encounters moments where the realms overlap:
- **Echoes**: In the Overworld, you sometimes see ghostly outlines of Ethereal Realm inhabitants. You cannot interact with them, but they can give clues.
- **Rift Tears**: Small, temporary openings between realms. These serve as optional puzzle elements and side quest hooks.
- **Nexus Terminals**: Found in both the Overworld and Ethereal Realm. Allow communication with the Nexus and, eventually, travel to it.
- **Dream Sequences**: At certain story beats, Kael falls asleep and briefly experiences the Ethereal Realm from Lira's perspective. These are non-combat narrative segments that deepen the emotional stakes.

---

## 3. Combat System

### Philosophy
Combat in Riftbound should feel like FFIV at its best: fast, readable, and rewarding. The player should spend their time making interesting decisions, not waiting, not fighting the UI, and never losing progress to unfair mechanics. Every QoL improvement that the JRPG genre has learned since 1991 is baked in from the start.

### Active Time Battle (ATB)
- Each character has an **ATB gauge** that fills in real time based on their Speed stat.
- When a character's gauge is full, the game does **not** pause — other gauges keep filling. This keeps combat feeling fast and pressured.
- The player selects an action: **Attack, Ability, Item, Defend, Pet Command, or Flee**.
- The action executes immediately upon selection (no additional charge time for basic actions). Some powerful abilities have a brief cast bar that fills after selection.
- Enemy ATB gauges are visible to the player so they can plan around incoming attacks.

### Smart Retargeting and QoL Combat Rules
These are non-negotiable design rules. Every one of them exists to prevent a specific frustration:

| Rule | What It Prevents |
|------|-----------------|
| **Smart Retarget**: If your target dies before your turn, your attack automatically redirects to a random living enemy. | Wasted turns (the FF1 problem). |
| **Overkill Redirect**: If your attack would deal 10x the remaining HP of your target, excess damage splashes to adjacent enemies. | Wasting a powerful attack on a nearly dead enemy. |
| **No Ambush Stunlock**: If the party is ambushed, they still get at least one action before the second round of enemy attacks. | Unfair wipes from bad luck. |
| **Auto-Phoenix**: If a party member has a revival item and the last standing member falls, the item is automatically used. | Total party wipes from one unlucky hit when you had the resources to survive. |
| **Smart AI Targeting**: Enemies prioritize tactically (targeting healers, exploiting weaknesses) but never feel random or unfair. Bosses telegraph big attacks one turn in advance. | Deaths that feel cheap or unpredictable. |
| **Instant Flee**: Fleeing from non-boss battles always succeeds. There is a brief animation but no failure chance. | Tedious flee attempts in areas you have outleveled. |
| **Speed Toggle**: Press a button to toggle 2x or 4x combat speed for trash encounters. | Boredom during easy battles. |
| **Auto-Battle**: Hold a button to repeat each character's last action automatically. | Repetitive input during grinding. |
| **Battle Memory**: The game remembers your last action for each character and pre-selects it on their next turn. | Redundant menu navigation. |

### Damage and Stats

**Core Stats:**
| Stat | Effect |
|------|--------|
| HP | Hit points. Zero = KO. |
| MP | Magic/ability points. Shared resource for all non-basic actions. |
| STR | Physical attack power. |
| MAG | Magical attack power and healing potency. |
| DEF | Physical damage reduction. |
| RES | Magical damage reduction. |
| SPD | ATB gauge fill rate. |
| LCK | Critical hit chance, item drop rate, flee success (always succeeds but LCK affects transition speed). |

**Damage Formula (Simple):**
- Physical: `(STR * 2 - DEF) * ability_multiplier * random(0.9, 1.1)`
- Magical: `(MAG * 2 - RES) * ability_multiplier * random(0.9, 1.1)`
- Minimum damage is always 1. The player should never see a "0 damage" result — it feels broken.

### Elemental System
Six elements in two triangles:

**Physical Triangle:**
- **Steam** (Overworld) beats **Glitch** (Nexus)
- **Glitch** (Nexus) beats **Aether** (Ethereal)
- **Aether** (Ethereal) beats **Steam** (Overworld)

**Classical Triangle:**
- **Fire** beats **Ice**
- **Ice** beats **Lightning**
- **Lightning** beats **Fire**

Weakness hits deal 1.5x damage. Resistance halves damage. This is simple enough to internalize quickly but deep enough to reward party composition.

### Ability System
Each character has:
- **Base Abilities** — Learned through leveling. 4-6 per character, unique to their class.
- **World Abilities** — Learned by exploring the character's home world. These are optional and reward side content.
- **Limit Break** — A single powerful ability that charges when the character takes damage. Each character's Limit Break is unique and visually spectacular.

Abilities cost MP. MP regenerates slowly in combat (1% per turn) and fully restores after battle. This means MP management matters within a fight but never between fights — you are never punished for using abilities.

### Formation
The party of 4 is arranged in two rows:
- **Front Row**: Higher physical damage dealt and received.
- **Back Row**: Lower physical damage dealt and received. Ranged and magic attacks are unaffected by row.

Characters can swap rows as a free action on their turn (does not consume the turn).

---

## 4. Party Members (22 Recruitable Characters)

The party roster spans all three worlds and all three aesthetic traditions. Each character has a distinct personality, role, and reason for joining Kael. Below, characters are grouped by their world of origin.

### Overworld Characters (Steampunk)

**1. Kael Ashford** — *Steamwright / Protagonist*
- **Role**: Physical DPS / Support
- **Personality**: Determined, pragmatic, quietly desperate. Speaks little but acts decisively. Carries guilt over his wife's death and channels it into his mission.
- **Unique Ability**: *Overclock* — Temporarily doubles the ATB fill rate of one party member at the cost of Kael's next turn.
- **Pet**: Cinder, a small brass automaton fox he built for Lira.

**2. Brynn Galvask** — *Gunslinger*
- **Role**: Ranged Physical DPS
- **Personality**: Loud, brash, fiercely loyal. A former Ironclad Authority sharpshooter who deserted when they started hoarding Rift technology. Uses custom steam-powered revolvers.
- **Unique Ability**: *Ricochet* — Single attack that hits all enemies for diminishing damage (100%, 75%, 50%, 25%).
- **Pet**: Bullet, a one-eyed mechanical raven.

**3. Sister Tessara** — *Steam Cleric*
- **Role**: Healer / Support
- **Personality**: Warm, motherly, but with a backbone of steel. Runs a clinic in Brasshollow's lower tiers. Believes in healing through both medicine and steam-powered medical devices.
- **Unique Ability**: *Pressure Mend* — Party-wide heal that also cures all status effects.
- **Pet**: Patch, a docile steam-powered tortoise that carries medical supplies.

**4. Huxley Wren** — *Tinkerer*
- **Role**: Support / Debuffer
- **Personality**: Eccentric, talkative, genius-level intellect with zero social awareness. Builds gadgets that should not work but do. Former colleague of Dr. Ashcroft.
- **Unique Ability**: *Jury-Rig* — Creates a temporary gadget with a random powerful effect (buff, debuff, damage, or heal). The randomness is always beneficial — never a dud.
- **Pet**: Sprocket, a clockwork spider that rides on his shoulder.

**5. Captain Voss** — *Ironclad Knight*
- **Role**: Tank
- **Personality**: Disciplined, honorable, conflicted. A career Ironclad Authority officer who begins as an antagonist but defects when he learns the truth about Chancellor Draven.
- **Unique Ability**: *Iron Wall* — Absorbs all physical damage directed at the party for one full ATB round.
- **Pet**: Bastion, an armored steam-powered badger.

**6. Ember** — *Saboteur*
- **Role**: Physical DPS / Debuffer
- **Personality**: Street-smart orphan from the Scorched Vents. Distrustful, sarcastic, but deeply protective of those she considers family. Age 16.
- **Unique Ability**: *Thermite Trap* — Places a trap that triggers on the next enemy action, dealing heavy fire damage and reducing DEF.
- **Pet**: Flick, a hyperactive salamander that lives in her coat pocket.

**7. Professor Aldric Thane** — *Archaeologist*
- **Role**: Magical DPS / Lore
- **Personality**: Bookish, obsessive, haunted. Has spent his career studying pre-Rift civilizations and believes the cataclysm was not the first time the world was fractured.
- **Unique Ability**: *Ancient Resonance* — Exploits any elemental weakness for 2x damage instead of 1.5x.
- **Pet**: Tome, a floating book that occasionally snaps at people.

### Ethereal Realm Characters (Magical/Dreamlike)

**8. Selia Dawnveil** — *Dreamweaver*
- **Role**: Magical DPS
- **Personality**: Serene, cryptic, unsettling. She was a seamstress before the Rift; now she weaves raw magic into devastating spells. Speaks in metaphors that are frustratingly accurate.
- **Unique Ability**: *Nightmare Thread* — Locks one enemy in a dream state for 2 turns (stun) while dealing damage over time.
- **Pet**: Loom, a floating ball of luminescent silk that changes color with Selia's mood.

**9. Ghael the Hollow** — *Revenant*
- **Role**: Tank / Self-Healer
- **Personality**: Stoic, melancholic, darkly humorous. Ghael died during the Rift and was resurrected by ambient ethereal energy. He is technically undead and has complicated feelings about it.
- **Unique Ability**: *Undying Will* — When KO'd, automatically revives at 25% HP once per battle.
- **Pet**: Shade, a translucent cat that phases through solid objects.

**10. Yuki** — *Frost Dancer*
- **Role**: Magical DPS / Crowd Control
- **Personality**: Playful, childlike wonder, but with flashes of ancient wisdom. Claims to be older than the Rift. No one can confirm or deny this. Fights by dancing.
- **Unique Ability**: *Absolute Zero* — Ice attack that hits all enemies and has a 50% chance to freeze each.
- **Pet**: Frostbite, a tiny ice elemental shaped like a rabbit.

**11. Rowan Ashfeld** — *Guardian*
- **Role**: Tank / Support
- **Personality**: Protective, gentle giant. Was a schoolteacher before the Rift. Now leads the defense of Shimmer Hollow. Fights with a shield made of crystallized magic.
- **Unique Ability**: *Aegis of Light* — Creates a magical barrier that absorbs the next 3 hits on any party member.
- **Pet**: Glimmer, a large moth with wings that glow like stained glass.

**12. Whisper** — *Shadow Thief*
- **Role**: Physical DPS / Speed
- **Personality**: Mysterious, speaks rarely, moves like smoke. No one knows their real name or face. Joins Kael because "the Fade should not exist."
- **Unique Ability**: *Phantom Strike* — Attacks twice and steals one buff from the target.
- **Pet**: Echo, a shadow that moves independently of Whisper.

**13. Mira Solenne** — *Star Caller*
- **Role**: Healer / Magical DPS
- **Personality**: Optimistic, idealistic, sometimes naive. Believes the Rift can be healed entirely. She channels starlight — the one constant between both realms.
- **Unique Ability**: *Celestial Cascade* — Heals the entire party and deals Aether damage to all enemies simultaneously.
- **Pet**: Nova, a miniature floating star that hums softly.

**14. The Conductor** — *Entropy Mage*
- **Role**: Debuffer / Magical DPS
- **Personality**: Philosophical, fatalistic, darkly charismatic. A former musician who now "conducts" the chaotic magical currents of the Ethereal Realm. Believes entropy is the only truth.
- **Unique Ability**: *Discordance* — Reverses all buffs on all enemies into debuffs.
- **Pet**: Requiem, a spectral violin bow that floats beside him.

### Virtual World Characters (Cyberpunk/Tron)

**15. ARIA** — *Combat AI*
- **Role**: Ranged DPS / Analyzer
- **Personality**: Logical, curious, developing emotions she does not fully understand. A sentient AI who wants to experience the physical world. Projects a holographic humanoid form.
- **Unique Ability**: *Full Scan* — Reveals all enemy stats, weaknesses, resistances, and upcoming actions for the rest of the battle.
- **Pet**: Bit, a tiny floating geometric shape that shifts between a cube, sphere, and pyramid.

**16. Jax "Hardline" Corbin** — *Data Warrior*
- **Role**: Physical DPS / Tank
- **Personality**: Aggressive, confrontational, secretly insecure. A human who uploaded his consciousness to the Nexus voluntarily because he was dying of a terminal illness. Fights with weapons made of hardened data.
- **Unique Ability**: *Data Fortress* — Temporarily becomes invulnerable and reflects one attack back at the attacker.
- **Pet**: Firewall, a pixelated dog made of orange code.

**17. Pixel** — *Glitch Mage*
- **Role**: Magical DPS / Chaos
- **Personality**: Unpredictable, mischievous, speaks in sentence fragments and corrupted text. A Nexus-born entity — she never existed in the physical world. Finds "meatspace" hilarious.
- **Unique Ability**: *Corrupt Data* — Deals Glitch damage to all enemies and randomly scrambles one of their stats (can reduce ATK, DEF, SPD, etc.).
- **Pet**: Bug, a literal software bug — a glowing beetle made of error codes.

**18. Dr. Veyla Ashcroft** — *Rift Scientist*
- **Role**: Support / Magical DPS
- **Personality**: Brilliant, haunted, burdened by responsibility. Her research enabled Kael's journey but also inadvertently gave Chancellor Draven the tools to engineer the cataclysm. She uploaded herself to the Nexus to hide from Draven and continue her work.
- **Unique Ability**: *Harmonic Pulse* — Boosts the party's ATB fill rate by 50% for 3 turns.
- **Pet**: Theorem, a floating crystalline equation that solves itself in loops.

**19. Zero** — *Assassin Program*
- **Role**: Physical DPS / Speed
- **Personality**: Cold, efficient, gradually warming. Originally designed as a security deletion program. Developed sentience and now questions whether he should continue following his original directives.
- **Unique Ability**: *Execute* — Instantly KOs any non-boss enemy below 15% HP.
- **Pet**: Null, an invisible presence you can only detect by the slight distortion it leaves in the air.

**20. Synthia Vox** — *Digital Bard*
- **Role**: Support / Buffer
- **Personality**: Vibrant, theatrical, larger-than-life. A digital echo of a famous pre-Rift musician. She does not know she is an echo and believes she is the original.
- **Unique Ability**: *Power Chord* — Buffs the entire party's STR and MAG by 30% for 3 turns.
- **Pet**: Amp, a floating speaker that blasts music at enemies.

### Cross-World Characters (Hybrid Aesthetic)

**21. The Architect** — *Unknown Class*
- **Role**: Wildcard (changes role based on equipped abilities)
- **Personality**: Enigmatic, omniscient within the Nexus, deeply lonely. Created the virtual world as a monument to both realms. Joins in Act IV when he realizes the Nexus must evolve beyond his original vision.
- **Unique Ability**: *Rewrite* — Once per battle, completely resets one enemy to its starting HP and removes all its buffs, but also removes all debuffs from it. Useful for enemies who buff themselves to dangerous levels.
- **Pet**: Fragment, a shard of the Nexus core that orbits him slowly.

**22. Lira Ashford** — *Riftwalker / Kael's Daughter*
- **Role**: Magical DPS / Healer (hybrid)
- **Personality**: Brave, resilient, wise beyond her years. Three years in the Ethereal Realm have changed her. She can perceive both realms simultaneously and channel energy from either. Joins for the final stretch.
- **Unique Ability**: *Rift Harmony* — Deals massive Aether + Steam damage to all enemies and heals the entire party. The most powerful ability in the game, usable once per battle.
- **Pet**: Wisp, a gentle orb of light that has been Lira's companion through her years in the Ethereal Realm. Wisp is the reason Lira survived.

---

## 5. Pet System

### Philosophy
Pets are not a minigame or an afterthought. They are companions that reflect your relationship with each character. A well-cared-for pet is a meaningful combat asset. A neglected pet is a liability. A dead pet is a narrative moment — and recovering it is a short, self-contained adventure that reinforces the game's themes of bonds and perseverance.

### Obtaining Pets
- Each of the 22 party members comes with a predefined pet. The pet is introduced as part of the character's recruitment scene.
- Pets cannot be swapped between characters. The bond is personal and permanent.
- Pets are unique — no two characters have the same type.

### Nourishment System
Pets have three care stats, each on a scale of 0-100:

| Stat | Activity | Effect if High | Effect if Low |
|------|----------|---------------|--------------|
| **Satiety** | Feeding with food items found/crafted in the world | Pet combat abilities deal more damage | Pet abilities deal less damage; pet becomes visually lethargic |
| **Training** | Short training minigames (30 seconds each, no more) | Pet gains additional passive bonuses (stat boosts to owner) | No passive bonuses |
| **Bond** | Taking the pet's owner into battle, story events, and bond conversations | Unlocks the pet's ultimate ability | Pet's ultimate ability is locked |

**Key design rules for nourishment:**
- Stats decay slowly — roughly 10 points per in-game hour of play. This means a player who checks in on pets every 30-60 minutes of playtime is fine. It should never feel like a chore.
- Feeding is instant: open inventory, select food, give to pet. No animations longer than 1 second.
- Training minigames are varied but all take under 30 seconds. Examples: fetch (timing game), obstacle course (directional input), puzzle (simple pattern match).
- Bond increases passively by having the character in the active party during battles. Bond conversations are optional 30-second dialogues that appear at rest points.
- A well-maintained pet requires roughly 2-3 minutes of attention per hour of gameplay. This is the ceiling, not the floor.

### Pet Abilities in Combat
Each pet has three abilities that unlock based on care stats:

1. **Basic Ability** (always available): A small passive effect. Example: Cinder (Kael's fox) provides +5% ATB fill rate to Kael.
2. **Advanced Ability** (unlocked at 50+ Satiety and Training): An active ability the player can command once per battle. Example: Cinder breathes fire on one enemy for moderate Steam damage.
3. **Ultimate Ability** (unlocked at 75+ Bond): A powerful ability usable once per battle. Example: Cinder overcharges and detonates, dealing massive AoE Steam damage and boosting the party's SPD for 3 turns.

Pet commands do not consume the character's turn. Instead, pet commands share a separate **Pet Gauge** that fills over the course of battle (similar to a Limit Break gauge but for pets). This means pet abilities are supplemental, not a replacement for the character's own actions.

### Pet Death
Pets can "die" (they do not die permanently — they are incapacitated and retreat to a safe space). This happens when:
- A specific enemy attack targets pets directly (telegraphed, avoidable with Defend)
- All three care stats drop to 0 simultaneously (severe neglect)

When a pet dies:
- The character loses all pet bonuses immediately.
- The character gains a **Grief** debuff: -15% to all stats until the pet is recovered.
- A **Redemption Quest** becomes available at the next rest point.

### Redemption Quests
- Short, self-contained side dungeons (10-15 minutes each).
- Thematically tied to the pet and the character. Example: Recovering Cinder involves entering a memory of Kael's workshop where he first built the automaton fox for Lira, fighting through manifestations of his guilt.
- Always doable with the current party — they scale to the player's level.
- The redemption quest is designed to feel like a meaningful narrative moment, not a punishment. Players who experience them should feel like they gained something — character backstory, emotional depth, unique loot.
- Upon completion, the pet returns at full care stats.

---

## 6. Crafting System

### Philosophy: Grand But Not Annoying
The crafting system in Riftbound should feel expansive and rewarding — like there is always something worth making — without ever feeling like a second job. The guiding principle is: **the player's time is respected above all else.**

### Materials
Materials come from three sources, one per world:

| World | Material Type | Examples |
|-------|--------------|---------|
| Overworld | **Mechanical Components** | Brass gears, copper wire, steam valves, coal crystals |
| Ethereal Realm | **Magical Essences** | Dream dust, liquid light, memory fragments, aether crystals |
| Virtual World | **Data Fragments** | Code shards, logic cores, pixel dust, null bytes |

Cross-world recipes require materials from multiple worlds, incentivizing exploration of all three realms.

### Gathering
- **No separate gathering skill or minigame.** Materials drop from enemies, are found in chests, and can be purchased from merchants.
- Gathering nodes in the world (glowing objects, sparking machines, data terminals) give materials with a single button press. No mining minigame, no durability, no gathering tools.
- Boss enemies always drop rare materials. The player is told what the boss drops before the fight.

### Recipes
- **Discovery**: Recipes are found in the world — in bookshelves, as quest rewards, from NPCs, in treasure chests. They are never gated behind obscure conditions.
- **Categories**: Weapons, Armor, Accessories, Consumables, Pet Food, Key Items.
- **No failure chance.** If you have the materials, crafting always succeeds. RNG in crafting is disrespectful of the player's time.
- **Bonus traits**: When crafting, there is a chance of a bonus trait being added (e.g., +5% crit, elemental resistance). This is the only RNG, and it is always a bonus on top of a guaranteed result — never the difference between success and failure.

### Crafting UI Principles
- **One screen.** All crafting happens in a single menu. No separate stations, no traveling to specific locations (after unlocking crafting early in Act I).
- **Recipe filtering.** Filter by category, by "craftable now" (you have all materials), by character, by world.
- **Material tracker.** Pin a recipe and the game shows you where to find missing materials on the world map.
- **Batch crafting.** If you want to make 10 potions, you press "craft" once and select quantity. Not 10 separate crafting actions.
- **Auto-sort.** Materials are automatically sorted by type and world. The player never manually organizes their crafting inventory.
- **No inventory limit for materials.** Materials stack infinitely. Inventory management is not gameplay.

### Cross-World Crafting
The most powerful items require materials from all three worlds, but these recipes are clearly labeled and the material tracker makes finding them straightforward. Examples:

- **Riftblade** (Weapon): Brass hilt (Overworld) + Dream steel (Ethereal) + Data edge (Nexus) = A sword that deals bonus damage to enemies from any world.
- **Harmony Elixir** (Consumable): Steam extract + Liquid light + Code serum = Fully restores HP, MP, and all pet care stats for one character.
- **Nexus Communicator** (Key Item): Required for the Act III storyline. Built from components gathered across all three worlds during Acts I and II.

---

## 7. Exploration and Progression

### World Progression
The three worlds open gradually over the course of the game:

| Act | Accessible Areas | Gating Mechanism |
|-----|-----------------|-----------------|
| Act I (Ch 1-4) | Full Overworld, Nexus terminals (communication only) | Story progression |
| Act II (Ch 5-8) | Full Overworld + Full Ethereal Realm, Nexus terminals | First Harmonic Crystal |
| Act III (Ch 9-11) | All three worlds fully explorable | Second Harmonic Crystal |
| Act IV (Ch 12-14) | All worlds + The Fade | Third Harmonic Crystal |

Within each world, exploration is nonlinear. Once you reach a world, you can explore most of it freely, with a few areas gated by story events or abilities gained through progression.

### Leveling and Scaling
- **Classic XP leveling.** Enemies give XP, characters level up, stats increase. Simple and transparent.
- **No level scaling.** The world has fixed difficulty. If you are underleveled, enemies are hard. If you are overleveled, they are easy. This rewards exploration and side content.
- **Inactive party members earn 75% XP.** Characters not in the active party of 4 still gain experience, just at a reduced rate. This prevents the roster from becoming unusable due to level gaps.
- **Level cap: 60.** Reachable through normal play plus moderate side content. No grinding required if you engage with the game's systems.

### Side Content

**Side Quests:**
- 30-40 side quests across the three worlds.
- Each side quest is self-contained and completable in 10-30 minutes.
- Side quests always reward something meaningful: a character recruitment, a recipe, a unique item, pet food, or lore that recontextualizes a main story beat.
- No fetch quests. Every side quest has a narrative hook and a resolution. "Collect 10 bear hides" does not exist in this game.

**Optional Dungeons:**
- 6 optional dungeons (2 per world), each 30-60 minutes.
- These contain the game's hardest combat encounters and best loot.
- Each optional dungeon has a mini-story that ties into the main narrative.

**Rift Tears (Puzzle Content):**
- Scattered across all three worlds.
- Small environmental puzzles where the player uses the overlap between realms to progress. Example: a door in the Overworld that can only be opened by flipping a switch visible in the Ethereal Realm echo.
- Reward materials, recipes, and lore entries.

**Character Bond Events:**
- Each party member has 3-5 optional bond conversations that occur at rest points.
- These deepen the character's backstory and relationship with Kael.
- Completing all bond events for a character unlocks their **World Ability** (see Combat section).

### Pacing
- **Main story path: 25-35 hours.** This is the target for a player who does some side content but focuses on the main quest.
- **Completionist path: 50-60 hours.** All side quests, optional dungeons, full pet care, all bond events, all crafting recipes.
- **Session length: Flexible.** The game is designed so that meaningful progress can be made in 30-minute sessions. Save anywhere. Quest log is clear and specific ("go to X location in Y world").

### Save System
- **Save anywhere.** Manual save at any point outside of combat.
- **Autosave** at every screen transition, every rest point, and before every boss.
- **Multiple save slots (20+).** Never force the player to overwrite.

---

## 8. Quality of Life Features

This section is a comprehensive list of every JRPG annoyance and how Riftbound addresses it. These are not optional polish items — they are core design requirements.

### Combat QoL
| Feature | Description |
|---------|-------------|
| Smart retargeting | Attacks redirect to living enemies if the target dies first |
| Overkill splash | Excess damage on nearly-dead enemies splashes to others |
| Visible enemy ATB | See when enemies will act so you can plan |
| Boss telegraph | Bosses visually signal powerful attacks one turn in advance |
| Speed toggle (2x/4x) | Speed up trash encounters |
| Auto-battle | Hold button to repeat last actions |
| Battle memory | Pre-selects each character's last chosen action |
| Instant flee | Fleeing always succeeds in non-boss fights |
| No ambush stunlock | Party always gets at least one action even when surprised |
| Auto-phoenix | Auto-uses revival items to prevent full party wipe |
| Post-battle full heal (MP) | MP fully restores after every battle — no potion hoarding |
| XP for inactive members | 75% XP for characters not in the active party |

### Exploration QoL
| Feature | Description |
|---------|-------------|
| Save anywhere | Manual save outside of combat, anytime |
| Generous autosave | Screen transitions, rest points, pre-boss |
| 20+ save slots | Never overwrite |
| Run by default | Walk speed is fast. Hold button to walk slowly, not the reverse |
| Minimap | Always visible, toggleable. Shows objectives, shops, exits |
| Fast travel | Unlocked per-area after first visit. Instant. No loading screen pretending to be a cutscene |
| Quest tracker | Pinned quest shows objective and destination on minimap |
| Material tracker | Pinned crafting recipe shows where to find missing ingredients |
| No random encounters | All enemies are visible on the map. Avoidable if desired |
| Outleveled enemy skip | Enemies 10+ levels below you flee on contact (no forced battles) |

### UI/UX QoL
| Feature | Description |
|---------|-------------|
| One-screen crafting | No station-hopping. All crafting in one menu |
| Batch crafting | Craft multiple items at once |
| Auto-sort inventory | Items sorted automatically by type |
| No material inventory limit | Materials stack infinitely |
| Equipment comparison | Side-by-side stat comparison when equipping gear |
| "Optimize" button | Auto-equip best available gear for a character (filterable by priority: ATK, DEF, SPD, etc.) |
| Party swap anywhere | Change active party at any time outside of combat |
| Bestiary | Records enemy stats, weaknesses, drops. Auto-populated as you fight |
| Recipe list | Shows all discovered recipes, what you can craft now, and where to find missing materials |
| Text log | Reviewable log of all dialogue, accessible from the menu. Never miss a conversation |
| Cutscene skip | All cutscenes skippable. All cutscenes replayable from a menu |
| Tutorial recall | Any tutorial can be re-read from the menu at any time |

### Respect for Player Time
| Feature | Description |
|---------|-------------|
| No mandatory grinding | Main story is completable without grinding if you engage with systems |
| Clear objectives | The game always tells you where to go next and why |
| Short side quests | 10-30 minutes each, all self-contained |
| No missables | Nothing is permanently missable. If you pass a chest, you can return |
| No points of no return (unmarked) | If a story event prevents backtracking, the game warns you clearly and gives you a chance to prepare |
| No crafting failure | Crafting always succeeds |
| No durability | Equipment does not degrade |
| No hunger/thirst for player | Survival mechanics do not exist (pets have care stats but they decay slowly and are quick to maintain) |

---

## 9. Development Phases

### Phase 1: Core Engine and Combat Prototype (8-12 weeks)
**Goal**: A playable combat prototype with ATB, two test characters, one pet, and basic QoL.

**Milestone 1.1 — Core Systems (Weeks 1-4)**
- [ ] Project setup: FlatRedBall2 sample project with screen management
- [ ] Implement basic screen flow: TitleScreen -> OverworldScreen -> BattleScreen
- [ ] Character data model: stats, abilities, equipment, level, XP
- [ ] Enemy data model: stats, abilities, drops, elemental affinities
- [ ] ATB gauge system: fill rate based on SPD, pause on input, visual gauge display
- [ ] Turn execution: action selection -> target selection -> action resolution -> damage calculation
- [ ] Basic damage formula: physical and magical
- [ ] Elemental system: weakness (1.5x) and resistance (0.5x)
- [ ] Party of 4 with front/back row

**Milestone 1.2 — QoL Combat (Weeks 5-8)**
- [ ] Smart retargeting (core feature — implement early, test constantly)
- [ ] Overkill splash damage
- [ ] Visible enemy ATB gauges
- [ ] Boss telegraph system (visual indicator one turn before big attacks)
- [ ] Auto-battle (repeat last action)
- [ ] Battle memory (pre-select last action)
- [ ] Speed toggle (2x, 4x)
- [ ] Instant flee
- [ ] No ambush stunlock rule
- [ ] Auto-phoenix rule
- [ ] Post-battle MP restore
- [ ] Victory screen: XP distribution (active + 75% inactive)

**Milestone 1.3 — Pet System v1 (Weeks 9-12)**
- [ ] Pet data model: satiety, training, bond stats
- [ ] Pet feeding: inventory -> select food -> give to pet
- [ ] Pet training: one minigame prototype (30-second fetch game)
- [ ] Bond stat: passive increase per battle with owner in active party
- [ ] Pet passive ability in combat (basic tier)
- [ ] Pet active ability in combat (advanced tier, gated by care stats)
- [ ] Pet gauge: fills over the course of battle
- [ ] Pet death condition and Grief debuff
- [ ] Redemption quest prototype: one scripted mini-dungeon

**Phase 1 Deliverable**: A combat demo with 2 playable characters, 1 pet, 3-4 enemy types, 1 boss, and all core QoL features. Playable from a test menu.

---

### Phase 2: Overworld and Exploration (10-14 weeks)
**Goal**: A playable Act I with the Overworld, 7 recruitable characters, exploration, and basic crafting.

**Milestone 2.1 — Overworld Map and Navigation (Weeks 1-4)**
- [ ] Tile-based overworld map: Brasshollow, Rustfields, Cogspire Academy, Fort Ironmaw, Scorched Vents
- [ ] Character movement on overworld: run by default, collision with terrain
- [ ] Screen transitions between overworld and interior maps
- [ ] Minimap system with objective markers
- [ ] NPC dialogue system: branching text, portraits, text log
- [ ] Visible enemies on map (no random encounters)
- [ ] Enemy contact -> battle transition
- [ ] Outleveled enemy flee behavior

**Milestone 2.2 — Town and Interior Systems (Weeks 5-8)**
- [ ] Town maps: shops, NPCs, rest points, quest givers
- [ ] Shop system: buy/sell with equipment comparison
- [ ] Inn/rest point: full heal, pet care menu, bond conversation trigger
- [ ] Equipment system: weapons, armor, accessories per character
- [ ] Equipment comparison UI
- [ ] Optimize (auto-equip) button
- [ ] Inventory management: auto-sort, no material limit, stack infinitely
- [ ] Save system: save anywhere, autosave on screen transition, 20+ slots
- [ ] Fast travel: unlock per area on first visit

**Milestone 2.3 — Act I Content (Weeks 9-14)**
- [ ] Recruit all 7 Overworld characters with story scenes
- [ ] 7 unique pets with care systems
- [ ] Act I main story: chapters 1-4 scripted events and boss fights
- [ ] 4-6 Act I side quests
- [ ] Crafting system v1: recipe discovery, one-screen UI, batch crafting, material tracker
- [ ] Overworld mechanical component materials: drops + gathering nodes
- [ ] 8-10 craftable Overworld recipes (weapons, armor, consumables, pet food)
- [ ] Bestiary: auto-populate as enemies are fought
- [ ] Quest tracker with minimap integration
- [ ] 2-3 Rift Tear puzzle encounters (environmental puzzles)

**Phase 2 Deliverable**: Playable Act I (approximately 6-8 hours of content). Full Overworld exploration, 7 characters, crafting, pet care, side quests, and story through Chapter 4.

---

### Phase 3: Ethereal Realm and Nexus (12-16 weeks)
**Goal**: Acts II and III playable. All three worlds. 20+ characters. Full crafting.

**Milestone 3.1 — Ethereal Realm (Weeks 1-6)**
- [ ] Ethereal Realm tileset and visual style: dreamlike, crystalline, floating geometry
- [ ] Ethereal Realm maps: Shimmer Hollow, Driftwood Expanse, Reverie's Garden, Mistveil Citadel, The Fade (partial)
- [ ] Ethereal physics flavor: visual effects for loose gravity, shifting terrain
- [ ] Recruit 7 Ethereal Realm characters with story scenes and pets
- [ ] Magical Essence materials: drops + gathering nodes
- [ ] 8-10 Ethereal crafting recipes
- [ ] Act II main story: chapters 5-8 scripted events and boss fights (including the Reverie)
- [ ] 4-6 Ethereal Realm side quests
- [ ] 2 Ethereal Realm optional dungeons (30-60 min each)
- [ ] Ethereal Realm Rift Tear puzzles (3-4)
- [ ] Dream sequence system: non-combat narrative segments from Lira's perspective

**Milestone 3.2 — The Nexus (Weeks 7-12)**
- [ ] Nexus tileset and visual style: neon grids, holographic constructs, impossible geometry
- [ ] Nexus maps: The Grid, Codefall City, The Archive, The Core, Glitch Zones
- [ ] Glitch Zone environmental hazards: shifting terrain, data corruption visual effects
- [ ] Recruit 6 Nexus characters with story scenes and pets
- [ ] Data Fragment materials: drops + gathering nodes
- [ ] 8-10 Nexus crafting recipes
- [ ] Cross-world recipes (require materials from 2-3 worlds)
- [ ] Act III main story: chapters 9-11 scripted events and boss fights (including The Architect encounter)
- [ ] 4-6 Nexus side quests
- [ ] 2 Nexus optional dungeons
- [ ] Nexus Rift Tear puzzles (3-4)

**Milestone 3.3 — Systems Completion (Weeks 13-16)**
- [ ] All 22 character Limit Breaks implemented and visually polished
- [ ] All 22 pet ultimate abilities implemented
- [ ] 3 training minigame variants (not just fetch)
- [ ] Bond conversation system: 3-5 conversations per character, all written and implemented
- [ ] World Abilities: one optional ability per character, unlocked by exploring their home world
- [ ] Cutscene replay menu
- [ ] Tutorial recall menu
- [ ] Text log (full dialogue history)
- [ ] Bestiary completion rewards

**Phase 3 Deliverable**: Acts I-III fully playable (approximately 20-28 hours). All three worlds. 20+ characters. Full crafting, pet, and exploration systems.

---

### Phase 4: Act IV, Polish, and Balance (10-14 weeks)
**Goal**: Complete game. Balanced. Polished. Shippable.

**Milestone 4.1 — Act IV: The Fade (Weeks 1-4)**
- [ ] The Fade maps: dissolving geometry, fractured time, color bleeding effects
- [ ] Act IV main story: chapters 12-14
- [ ] Lira recruitment scene and integration
- [ ] The Architect recruitment scene and integration
- [ ] Chancellor Draven: boss encounter design (multi-phase)
- [ ] Final dungeon: 60-90 minute climactic dungeon through the Fade
- [ ] Multiple ending variations based on player choices (2-3 endings, all bittersweet-to-hopeful)
- [ ] Ending cinematics and epilogue text

**Milestone 4.2 — Balance Pass (Weeks 5-8)**
- [ ] Full playthrough balance testing: main story path (25-35 hour target)
- [ ] Enemy stat tuning: every encounter from Act I through Act IV
- [ ] Boss difficulty curve: each boss should require strategy, never grinding
- [ ] Character balance: all 22 characters should be viable for endgame (no dead roster members)
- [ ] Pet balance: care stat decay rates, ability damage, redemption quest difficulty
- [ ] Crafting balance: material drop rates, recipe costs, gear power curve
- [ ] XP curve: verify no grinding needed on main path; verify 75% inactive XP prevents level gaps
- [ ] Optional dungeon difficulty: challenging but fair at expected level

**Milestone 4.3 — Polish and QoL Final Pass (Weeks 9-14)**
- [ ] All QoL features from Section 8 verified and tested
- [ ] UI polish: all menus responsive, clear, consistent
- [ ] Screen transitions: smooth, fast, no unnecessary delays
- [ ] Battle animations: all abilities, pet abilities, and limit breaks visually polished
- [ ] Sound design: battle sounds, ambient world audio, UI feedback sounds
- [ ] Music: distinct themes per world, battle themes, boss themes, emotional story themes
- [ ] Bug fixing and regression testing
- [ ] Performance optimization: maintain 60fps target
- [ ] Accessibility: configurable text size, colorblind-friendly elemental indicators, rebindable controls
- [ ] Final completionist playthrough: verify 50-60 hour target, verify no missables, verify all side content is findable

**Phase 4 Deliverable**: Complete, balanced, polished game. Ready for release.

---

### Phase 5 (Post-Launch / Optional): Expanded Content
- [ ] New Game+ mode: carry over levels, unlock harder enemy variants
- [ ] Superboss encounters: one per world, hardest fights in the game
- [ ] Expanded bond events: additional character interactions
- [ ] Colosseum mode: wave-based combat challenge with leaderboards
- [ ] Additional crafting recipes and materials
- [ ] Pet evolution system: pets gain new forms at max bond

---

## 10. Technical Notes — FlatRedBall2 Considerations

### Engine Capabilities (Available Now)
- **Entity system**: Characters, enemies, and pets map cleanly to FlatRedBall2 entities via `Factory<T>` with `CustomInitialize`/`CustomActivity` lifecycle.
- **Collision system**: `AddCollisionRelationship` handles overworld entity-vs-terrain and entity-vs-entity collision. `TileShapeCollection` provides grid-based static collision for map terrain.
- **Screen management**: `Screen` lifecycle (Initialize, Activity, Destroy) maps to the game's screen structure. `MoveToScreen<T>` with configure callbacks passes data between screens (e.g., enemy group ID to battle screen).
- **Input system**: Keyboard, gamepad, and cursor input. `KeyboardInput2D`/`GamepadInput2D` for movement, `.Or()` to merge input sources. `IPressableInput` for menu navigation.
- **Top-down movement**: `TopDownBehavior` + `TopDownValues` provides ready-made overworld movement with acceleration, deceleration, and direction facing.
- **Camera**: `CameraControllingEntity` for automatic player-following with deadzone, smooth approach, and screen shake. `DisplaySettings` for resolution/zoom/aspect ratio.
- **Shapes**: `AxisAlignedRectangle`, `Circle`, `Polygon` for collision and debug visualization. Shapes default `IsVisible = false`.
- **Gum UI**: Forms controls (`Button`, `Label`, `TextBox`, `StackPanel`, `Panel`) for all menus and HUD. Screen-space by default (Y-down pixel coordinates, independent of game camera).
- **Paths**: `Path` + `PathFollower` for NPC patrol routes and scripted entity movement.
- **Physics**: Second-order kinematic physics (pos/vel/acc/drag) for overworld movement. Y+ is up.
- **Timing**: `FrameTime.DeltaSeconds` for all time-based logic (ATB gauges, cooldowns, stat decay, entity lifetimes).

### Needs to Be Built (Engine Gaps)
- **ATB system**: No built-in turn/time system. Build as pure game logic using `FrameTime.DeltaSeconds` for gauge fill. Keep battle logic isolated from rendering for testability.
- **Dialogue system**: No built-in text box, portrait display, or branching dialogue. Build with Gum UI (`Panel` + `Label` + `StackPanel` for choices). Data-driven via JSON dialogue files.
- **Menu system**: Equipment screens, crafting UI, pet care UI, bestiary — all built with Gum Forms controls. Start with code-only Gum mode for Phase 1; consider `gumcli` project mode for Phase 2+ when UI stabilizes.
- **Tile map rendering**: Tiled integration is **stubbed** (`TiledMapLayerRenderable`/`TiledCollisionGenerator` are non-functional). Use string-grid level definitions parsed into entities/shapes for prototyping. `TileShapeCollection` works for collision grids. Custom sprite-sheet tile renderer for polish phase.
- **Audio**: `AudioManager` throws `NotImplementedException`. Use MonoGame `SoundEffect`/`Song`/`MediaPlayer` APIs directly. Defer to Phase 4 (polish). Wrap in a simple `AudioService`.
- **Animation**: `Sprite.PlayAnimation` is a **no-op**. Use custom frame-based animation via `Sprite.SourceRectangle` cycling. Use colored shapes for Phase 1 prototype — no animation needed.
- **Save/Load**: No built-in serialization. Use `System.Text.Json` for game state serialization (party, inventory, quest flags, pet stats, position).
- **Spatial partitioning**: Collision is O(n*m) broad-phase. Monitor performance with many on-screen entities; implement partitioning only if needed.

### Architecture Recommendations
- **State machine for game flow**: TitleState -> OverworldState -> BattleState -> MenuState. Each state manages its own screen and entities.
- **Data-driven character definitions**: Store character stats, abilities, growth curves, and pet data in JSON or XML files. Avoid hardcoding — 22 characters with unique growth curves demand a data-driven approach.
- **Battle engine as isolated system**: The ATB combat engine should be separable from the rendering — this allows unit testing of damage calculations, retargeting logic, and ATB timing without needing a running game.
- **Event bus for cross-system communication**: Pet death -> Grief debuff, Quest completion -> Recipe unlock, Bond event -> World Ability unlock. These cross-system triggers are cleanest with a publish/subscribe event system.

### Scope Reality Check
This is a large game. For a solo developer or small team using FlatRedBall2, expect:
- **Phase 1** is realistic as a focused prototype (2-3 months).
- **Phase 2** is achievable but requires disciplined scope control (3-4 months).
- **Phases 3-4** represent 6-8 months of sustained development.
- **Total estimated development time: 12-18 months** for a small team, longer for a solo developer.
- Consider releasing Phase 1-2 as a public demo to gather feedback before committing to the full scope.

---

*This document is a living blueprint. Revisit and revise as development reveals new insights. The player experience goals in Section 1 are the north star — every system, feature, and design decision should serve those goals.*

