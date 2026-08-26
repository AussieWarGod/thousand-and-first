# Diversity, technology trees, and the body-modification lab

Date: 2026-08-22.
Lane: **design ideation — no code, no branch, no tracked edit outside this file.**
Requested by: the author's ruling of 2026-08-22 (`a0bbbb7`, the heart ruling's third breath):

> "lets think about diversity and customisation, different buildings, different plots, different
> benefits, different technology tree's and technology tree options for different creeds,
> genotypes, races, and other qud native functionality"

plus the two endorsed-in-principle items opened in the same breath: the **body-modification lab**
(late-game exotic building; augment the player with parts from other creatures) and **exotic
buildings that introduce new mechanics, new ways to play, and new endgame content**.

## Provenance and evidence standard

Same rigour as `_notes/VANILLA-PRODUCTION-TRUTH.md` and `_notes/EVOLVING-HEART-RESEARCH.md`.
Ground truth:

- **`D/`** — a local decompile of the pinned build 2.0.211.51 assembly.
- **`B/`** — the licensed installation's shipped game-data root.
- **`T/`** — this repository root.
- **`W/`** — that contributor's installed Steam Workshop tree for app 333640.
- **`M/`** — that contributor's local Caves of Qud Mods root.

Every load-bearing claim cites `file:line` or an exact blueprint/attribute name. Anything not read
directly is marked **INFERRED** or **UNVERIFIED**. Runtime behaviour is never asserted from source
alone — `_notes/README.md`'s standing rule, restated after the C2 correction in `IDEA-INBOX.md`.

**Binding constraints this document is written under**, and which it does not get to relax:

| Constraint | Source |
|---|---|
| **The mesh condition** — every lane is a RENDERING of model state through surfaces that already exist. No parallel machinery. A lane that cannot be built as a rendering returns as a design question before code. | Addendum 13, `BUILDING-CATALOGUE-BRIEF.md` |
| **(type × size) set binding** — the plot is typed at staking; the set key is (`Category` × `Plot`); an over-constrained or empty set REFUSES BY NAME. | Addendum 2 |
| **Derive before authoring** | Addendum 4, Addendum 11 method note |
| **Time is labour, never maturation**; every rate is time × labour × infrastructure | Addendum 8, VISION |
| **"This mod has no research tree and does not want one — a tree is a second job, and the founder already has one."** | `T/Growth/KingdomZoningRules.cs:9-10`, verbatim |
| **Not a second job / not a management screen / not a numbers panel**; bonus for engaging, never penalty for abstaining | `VISION.md` |
| Megastructure cardinality: one per city by default; the door opens only with proof | heart ruling, 2026-08-22 |
| Protection law: nothing player-placed is ever consumed, moved, or destroyed without explicit say-so | `VISION.md`, STANDARDS |

---

## 0. The verdicts, up front

| Question | Verdict |
|---|---|
| **Do we author per-creed / per-genotype building sets?** | **NO — and the arithmetic is not close.** 33 admissible creeds × 40 (Category × Plot) sets = 1,320 sets against a catalogue of 63 designs that already leaves **15 of 40 base sets empty**. Variation is FILTERS and SHADES over one catalogue. |
| **Does the mod already have a diversity axis?** | **Yes, five of them, and one is entirely unexercised.** `Styles` (5 declared, site-derived) is honoured by the code and **every one of the 63 designs says `Styles="all"`** — the machinery ships, the data never uses it. |
| **Is "genotype" even the right word for settlers?** | **Not strictly.** `GetGenotype()` is `GetPropertyOrTag("Genotype")` (`D/XRL/World/GameObject.cs:10550`) and is set for the player, not for ordinary creatures. The settler-side axis that answers for everything, including modded creatures, is **`GetSpecies()`** (`:14290`, 98 distinct values shipped) and **`GetCulture()`** (`:14304`, 33 distinct, falling back to Species). Use those. |
| **Does the mod have a technology tree today?** | **It has a technology LADDER and a hidden DAG, and no tree.** `TechLevel` is 5 rungs derived from disks-taught + machines-certified (`T/Growth/KingdomZoningRules.cs:12-24, 210-232`). The DAG is real but scattered across `MinStage`/`MinTech`/`Knowledge`/`Districts`/`MinZones`/`Refines` and is **never rendered anywhere**. |
| **Can a tech tree exist without violating the no-research-tree ruling?** | **Yes, on one condition: it must be a MAP, never a SPEND.** The ruling forbids a screen you pay into. It does not forbid showing the founder the dependency graph their own catalogue already contains. |
| **Where does branching come from, honestly?** | **`B/Factions.xml`'s own `<waterritual>` table.** 43 factions ship one, and they grant *different things*: Barathrumites grant `Skill="Tinkering" Blueprints="1d3"`; Templar grant `ItemBlueprint="CyberneticsCreditWedge"`; Seekers grant `RandomMentalMutation="50"`; Oozes grant `Mutation="Slime Glands"`. **Vanilla already authored the divergent tech tree, per creed.** We render it; we do not invent it. |
| **Does vanilla ever gate a grant on genotype?** | **Yes, with a negation operator, in shipped data.** `Chavvah`'s water ritual carries `RecipeGenotype="!True Kin"` (`B/Factions.xml`, Chavvah entry). Genotype-shaded benefits are a vanilla idiom, not our invention. |
| **Is the body-modification lab novel machinery?** | **Substantially, no — the author has already built the hard half twice over.** `M/CreatureControl/Trophic Absorption/` (1,021 lines of C#) is a working, shipping implementation of "digest a creature, gain its whitelisted parts, switch between absorbed variants." The lab is that mechanic **relocated from a mutation to a building**, with cost, consent, and creed friction added. |
| **Is the lab's UI a new invention?** | **No.** `M/CreatureControl/Control Menu/ControlMenu.cs` (472 lines) already is the idiom: one ability, rows per behaviour, variant-switch submenus, an exclusion manager, a guise picker. |
| **How many genuinely-new exotic buildings survive the mesh condition?** | **Four of eight — and only two of those are recommended now.** Two are new machinery that earns its place (the lab, the deep delve), two are new machinery deferred to unbuilt prerequisites (the stasis vault, the assenting moot), and **four are pure renderings**. Renderings are the majority, which is the correct ratio and the brief's own stated preference. |

---

## 1. The diversity thesis

### 1.1 Three sentences

**Variation is a filter and a shade over one catalogue, never a second catalogue.** The mod already
ships five diversity axes — city `Style`, creed, origin, the derived QoL profile, and `TechLevel` —
of which the style axis is fully implemented in code and used by exactly zero of the 63 designs;
the honest first move is to *exercise what exists* before adding an axis. **A genuinely separate
(type × size) set earns its place only where the ground itself is different** — underground, sky/
hosted, and arcology interior — because those are the only three cases where the *plot*, not the
builder, has changed, and Addendum 2's set binding is a property of plots.

### 1.2 What already varies — the five shipped axes

| Axis | Values | How it derives | Where it bites | Exercised? |
|---|---|---|---|---|
| **City `Style`** | `common`, `verdant`, `fungal`, `gyre`, `eater` (`T/KingdomBuildings.xml:83-87`) | `KingdomRules.StyleForSite(blueprint, region, zLevel)` at founding (`T/Core/KingdomFounding.cs:60,181,259`) | design eligibility (`KingdomRules.StyleAllows`, `T/Growth/KingdomCommission.cs:48`, `KingdomSocket.cs:719`), upgrade eligibility (`KingdomUpgrade.cs:502`), wall material (`KingdomMaterialRules.cs:897-931`), wall blueprint (`KingdomPlotRules.cs:1592`), crop species and crop days (`KingdomCropRules.cs:305,337,400`) | **NO.** All 63 designs are `Styles="all"`. Skins vary; designs do not. |
| **Creed** | any vanilla faction passing `CanBeCreed` — **33 of 83 admitted, 50 rejected** (`T/Core/KingdomCreed.cs:74-85`, documented outcome at `:67-71`) | the realm's own standings ledger, fed by the founder's reputation spillover (`T/Core/KingdomSystem.cs:1516-1524`), trade, raids, declaration | cohabitation, dissent/secession, conversion, shrine, water rite, shared meal | Partly — behavioural, never catalogue-facing |
| **Origin** | 6 places: the salt marshes, the desert canyons, the hills, the flower fields, the rust wells, the banana grove (`T/Core/KingdomRules.cs:858`) | assigned on settler arrival (`T/Growth/KingdomGrowth.cs:768`), counted live in `OriginCounts`, and **leaves with the people** (`T/Growth/KingdomZoning.cs:96-102`) | one knowledge key each; `Knowledge="origin:the salt marshes"` gates exactly one design today (`T/KingdomBuildings.xml:241`) | Barely — 1 of 63 |
| **QoL profile** | 6 tags (`taf:charge`, `taf:openwater`, `taf:damp`, `taf:dark`, `taf:sky`, `taf:quiet`), open set | `KingdomQolRules.Derive(ResidentTruth)` off vanilla body truth — `Robot`, `Aquatic`, `Fungal`, `Photosynthetic`, `Inorganic`, `HasStomach` (`T/Core/KingdomQolRules.cs:394-424`; reads at `T/Core/KingdomQol.cs:145-152`) | who may move in, who may share a roof | 9 of 63 designs declare `Provides` |
| **`TechLevel`** | 5 rungs: hands, salvage, workshop, foundry, arclight (`T/Growth/KingdomZoningRules.cs:12-24`) | disks taught (1 pt) + machines certified (2 pts); origins deliberately 0 (`:211-226`) | `MinTech` gate on designs | 7 of 63; **`arclight` gates nothing shipped** |

**The finding that should govern the whole wave:** the mod's diversity problem is not a missing
system. It is **five systems with almost no data attached to them.** `Styles` is a complete,
tested, load-validated eligibility mechanism (`KingdomCatalogueRules.Validate` even warns on styles
no design uses, `T/Growth/KingdomCatalogueRules.cs:607-668`) that no design has ever used to mean
anything.

### 1.3 The arithmetic that kills authored sets

Addendum 2 binds a building plan to exactly one (`Category` × `Plot`) pair and requires a
load-time validator: *"every commissionable (type × size) reachable at any stage has ≥1 design, or
the gap is named at load."*

The catalogue today, counted (`T/KingdomBuildings.xml`, 63 `<building>` records):

| Category | S | M | L | XL | single-cell | total |
|---|--:|--:|--:|--:|--:|--:|
| storage | 6 | 4 | 2 | 2 | – | 14 |
| housing | 5 | 3 | 2 | 1 | – | 11 |
| craft | 2 | 8 | **0** | **0** | – | 10 |
| food | 2 | 3 | 1 | 1 | – | 7 |
| civic | 3 | 2 | 1 | **0** | – | 6 |
| defense | **0** | **0** | 1 | **0** | 4 | 5 |
| power | 3 | 1 | **0** | **0** | – | 4 |
| faith | 2 | **0** | 1 | **0** | – | 3 |
| knowledge | 1 | 1 | **0** | **0** | – | 2 |
| memorial | 1 | **0** | **0** | **0** | – | 1 |

**25 of 40 (Category × Plot) sets are filled. Fifteen are empty right now** — the Addendum 2
validator, once written, names fifteen gaps on the first load.

Now multiply by any authored variation axis:

| Scheme | Sets to fill | Designs needed at ≥1 each | Reality check |
|---|--:|--:|--:|
| Base, today | 40 | 40 | 63 designs, **15 gaps** |
| × 5 city styles | 200 | 200 | 3.2× the entire existing catalogue, to reach *minimum* |
| × 33 creeds | 1,320 | 1,320 | 21× the catalogue |
| × 33 creeds × 5 styles | 6,600 | 6,600 | not a design, a data-entry career |
| × 98 species | 3,920 | 3,920 | and the set is open — mods add more |

And the sets are **open on every axis**, including by vanilla itself: `B/Factions.xml` ships 83
factions and `B/ChiliadFactions.xml` adds **33 more** (hidden until the endgame — Sludges, Jellies,
Snailfolk, Foxen, Batfolk, Nephilim, the Quetzal Council), for **116 shipped faction definitions**,
and both files are picked up by the same `GetXMLFilesWithRoot("Factions")` sweep. Mods extend it
further (four faction-expansion mods are installed on this machine alone — Cats and Dogs, Tortoises,
and two CYF expansions);
`Genotypes.xml` is extended by mods (Playable Slime ships two genotypes and two subtype classes in
plain XML, `W/3388408799/Genotypes.xml`, `Subtypes.xml`); `Species` has 98 shipped values and no
closed list. **Any design that enumerates is wrong on arrival and wronger every patch.**

### 1.4 Derive-don't-author, applied: FILTER, SHADE, SET

Three mechanisms, in strict order of preference. The first two are cheap and compose with
everything shipped. The third is expensive and is rationed to three cases.

#### (a) FILTER — the same catalogue, differently reachable

A design declares *what it wants of the city*, and the city's identity answers. This is the
existing `ZoneGate` mechanism (`T/Growth/KingdomZoningRules.cs:57-92`) with its vocabulary widened,
and it needs **no new gate type** — `Knowledge` already accepts arbitrary `kind:name` keys, and
`Knows` already matches an unqualified requirement against any kind (`:436-468`).

Proposed new knowledge kinds, all riding the existing encoder (`ComposeKey`, `:349-365`) and all
inert if nothing supplies them:

| Kind | Meaning | Derived from | Leaves when? |
|---|---|---|---|
| `origin:` | a people's homeland trade | *shipped* — `OriginCounts` | when the last of them leaves (already true) |
| `creed:` | a creed's own practice | `System.CreedCounts`, live like origins | when the believers go |
| `species:` | a body-plan's craft | live count over `GetSpecies()` on residents | when they go |
| `culture:` | a people's craft | live count over `GetCulture()` (`D/…/GameObject.cs:14304`) | when they go |
| `rite:` | a technique learned at a water ritual | the founder's own `<waterritual>` grants, mirrored | **never** — a thing you learned is learned |
| `machine:` | *shipped* — a certified salvage | *shipped* | never (already one-way, `T/Growth/KingdomZoning.cs:173-183`) |
| `disk:` | *shipped* — a taught recipe | *shipped* | never |
| `pattern:` | *shipped by convention* — a foreign design | additive pattern-book knowledge frozen and settled by its CharterDelivery receipt (`Trade/KingdomTradePatternRules.cs`, produced by `Experience/KingdomCeremony.FreezePatternBook`) | never |

The distinction between kinds that **leave with the people** and kinds that **stay** is the whole
of the design's honesty, and it is already the shipped rule, stated in the source:

> *"a trade the settlement holds only because somebody from that country lives here should leave
> with them"* — `T/Growth/KingdomZoning.cs:99-102`

So a fungal city can raise the spore-cellar because fungal people live there; if they all leave,
the standing cellar keeps working (nothing regresses silently — the raising law) but no *new* one
can be commissioned. That is diversity that walks around, which is the pillar.

**Cost:** one `Roster()` extension, three new live-count sources, and catalogue attributes. No new
gate type, no new refusal verdict, no new UI.

#### (b) SHADE — the same building, differently worth it

The catalogue's `Carries` numbers are equilibrium contributions, not flows. A shade is a *modifier
on Carries, on staffing, or on cost*, keyed to who is doing the work or who is living there —
never a new record.

Four shading surfaces already exist and are the whole of what this needs:

1. **`Provides` ↔ `Needs`/`Prefers`** (`T/Core/KingdomQolRules.cs:518-548, 570-607`) — already
   derives from body truth, already shades equilibrium up through `TasteShade`, already never
   penalises when unmet (`:602-605`). A photosynthetic settler in a `taf:sky` house is *already*
   a shade; nothing new is required for "different benefits by species".
2. **Crew capability** (Addendum 7) — derived from settler stats, never assigned. A city of
   Strength-heavy people quarries faster; a city of Intelligence-heavy people runs certified
   machines better. This is the honest "different benefits by genotype" and it needs no genotype
   check at all: it reads the attributes the genotype already set.
3. **Notable tastes** (co-opted idea, Addendum table) — a settling notable states one or two
   tastes in prose; met tastes shade equilibrium up; the penalty half is already rejected.
4. **Style-preferred materials** (`T/Growth/KingdomMaterialRules.cs:897-931`) — already shipped,
   already varies the wall material by style.

**The rule that keeps shading from becoming a numbers panel:** a shade is always **≤ ±1 band** and
is always **stated in prose before it is stated in a number**. Vanilla's own idiom is the
`<extrainfo>` line on a genotype (`+850 reputation with Oozes`), not a stat block.

#### (c) SET — a genuinely separate (type × size) family

Reserved for the three cases where **the plot is a different kind of ground**, which is exactly
what Addendum 2 binds on. Named in full in §1.5. Everything else is (a) or (b).

### 1.5 The few separate sets that earn their place

Three, and the case for each is that the *ground*, not the *builder*, differs — so the set binding
is doing its actual job rather than being abused as a variation mechanism.

| Set | Why the ground differs | Already anticipated by | Size of the ask |
|---|---|---|---|
| **`Strata="deep"` — the carved catalogue** | No weather (no sailvane, condenser, catchment); enclosure is free because *the rock is the wall*; clearing yields stone and costs dearly; fungal crops replace surface crops; natural chokepoints change defence entirely. Roof state is always `Carved`. | Brief §Terrain, verbatim: *"Different catalogue subset by stratum"*; `RefusedStratum` verdict already ships (`T/Growth/KingdomZoningRules.cs:47`), and `StratumAccepts` already refuses sky-needing designs underground (`:738`) | **Medium.** The gate half ships. The design half is ~10-14 new records. Note the source's own honesty: the "deep-only" half is *deliberately not faked* today (`:698-701`) — this is the wave that makes it real. |
| **`Strata="sky"` — the hosted catalogue** | The plot's ground is another building's roof. Footprint law reads the host's roof; the host's lifecycle owns the child's; wear on the host condemns the child; the host is the conduit. | BACKLOG addition 2026-08-22 (strata-filtered plots + the sky), with the author's own open question: *"whether deep/sky get their own building SETS … or filtered subsets of the surface sets"* | **Large**, and it is the plot-nesting work, not a catalogue work. |
| **`Strata="arcology"` — the interior catalogue** | Sealed hydroponics, vertical lodging at surface-impossible densities, gallery commons, scrubbers, funicular market — none of which is legal on open ground, and all of which the shell's own infrastructure (freight shaft, riser taps) makes possible. | "Arcology-only sets (Addendum 2 extension)" — already named in the arcology ideation | **Large**, waits on the megastructure wave. |

**The answer to the author's own open question**, offered for ruling: **separate sets for deep and
arcology; a filtered subset for sky.** Deep and arcology change what a building *can physically
be* (no sky, free walls, sealed volume); sky changes only *where it stands*, and a rooftop garden
is a garden. The three-way split keeps two expensive sets instead of three.

**What does NOT earn a separate set, and the reason each time:** creed (belief does not change
masonry — it changes *which* masonry you are allowed to commission, which is a filter); species
(a body plan changes who can live there and who can work it, which is `Needs`/`Prefers` and crew
capability, both shades); genotype (see §1.6); origin (a homeland is a technique, which is a
knowledge key).

### 1.6 Genotype, race, and the word problem

The mandate says *"creeds, genotypes, races"*. Two of those three need translating before they can
be built, and the translation is where the design gets honest.

**Genotype is a chargen concept, not a creature concept.** `GenotypeEntry`
(`D/XRL/GenotypeEntry.cs:11-77`) is rich — `Species`, `IsMutant`, `IsTrueKin`,
`CyberneticsLicensePoints`, `AllowedMutationCategories`, `BodyObject`, `Reputations`, `Skills`,
`MutationPoints`, `StatPoints` — but vanilla ships exactly **two** genotypes (`B/Genotypes.xml`:
Mutated Human, True Kin), and `GetGenotype()` on an arbitrary creature is just
`GetPropertyOrTag("Genotype")` (`D/…/GameObject.cs:10550`), which is null for ordinary NPCs.

So genotype answers reliably for exactly one creature in the game: **the player.** Which makes it
the right key for Addendum 13's lane 2 (*"the city reacts to what you ARE"*) and the wrong key for
anything about settlers.

For settlers, the derive keys that answer for everything, modded creatures included:

| What you mean | Read | Values shipped | Fallback behaviour |
|---|---|---|---|
| "race" | `GetSpecies()` (`D/…/GameObject.cs:14290`) | 98 distinct `Species` tags across `B/ObjectBlueprints/*.xml` | falls back to the stripped short display name — **never null** |
| "people" | `GetCulture()` (`:14304`) | 33 distinct `Culture` tags (Barathrumite, Templar, Hindren, Mopango, Naphtaali, Issachari, Goatfolk, Ooze, Robot, Sightless Way, Eater, Star, …) | falls back to `GetSpecies()` — **never null** |
| "is a mutant / is true kin" | `IsMutant()` / `IsTrueKin()` (`:10560-10568`) | **event-checked** (`IsTrueKinEvent.Check`, `IsMutantEvent.Check`) — so a mod's genotype answers correctly for free | event default |
| "body plan" | the anatomy's `Category` | 19 categories across 78 anatomies in `B/Bodies.xml`: Plant 34, Mechanical 33, Wooden 32, Metal 29, Stone 22, Fungal 12, Plastic 11, Animal 11, Cloth 9, Protoplasmic 7, Cybernetic 7, Bone 7, Arthropod 7, Glass 4, Leather 3, Jelly 2, Crystal 2, Mollusk 1, Light 1 | — |
| "can take chrome" | `CyberneticsLicensePoints > 0` → `supportsCybernetics` (`D/XRL/GenotypeEntry.cs:89`, `SubtypeEntry.cs:64`) | True Kin `2`; Mutated Human absent | — |
| "can take mutations" | `AllowedMutationCategoriesList` → `supportsMutations` (`GenotypeEntry.cs:77-104`) | Mutated Human `*`; True Kin `""` | — |

**`GetCulture()` is the single best find in this section.** It is a vanilla, mod-extensible,
never-null answer to "what people is this", it already names 33 peoples including every one this
mod would want to build around, and it costs one method call. Where the mandate says "races", the
build should read `GetCulture()`.

**And vanilla already gates a grant on genotype, with a negation operator, in shipped data:**
`Chavvah`'s water ritual carries `RecipeGenotype="!True Kin"` (`B/Factions.xml`, Chavvah entry).
Genotype-conditional benefits are not a thing we would be introducing; they are a thing we would be
matching.

### 1.7 Different plots, different benefits — what falls out for free

The mandate asks for varied plots and benefits. Under the thesis, most of that is already paid for:

- **Different plots by ground** — the three stratum sets (§1.5), plus the existing `Open`/`Sky`
  flags and the `Roof` states (`Open`/`Soft`/`Walled`/`Carved`).
- **Different plots by city** — `Styles` filtering, finally exercised: an `eater` city's civic set
  is not a `verdant` city's civic set, using the mechanism that already ships.
- **Different benefits by who lives there** — `Provides`/`Needs`/`Prefers`, already derived from
  body truth, already never punitive.
- **Different benefits by who works there** — crew capability from settler attributes (Addendum 7),
  reach by size × tier (Addendum 6), office holders' quality derived from who the settler is.
- **Different benefits by belief** — reach-scoped lifts (Addendum 6) mean the temple quarter is
  genuinely different ground from the tanners', with no code knowing the word "quarter".

The honest new asks are: **exercise `Styles`** (data work, no code), **widen the knowledge-key
vocabulary** (small code, no new gate type), and **build the deep set** (the biggest single
diversity win available, and already promised by the brief).

---

## 2. Technology trees

### 2.1 Three sentences

**The mod already has a technology DAG; what it lacks is a rendering of it and any reason for two
cities to walk different paths through it.** The Qud-native shape is research as **artifacts and
people** — a disk taught, a machine dragged home and certified, a savant lodged, a rite performed
with a faction who then teaches you their technique — which is exactly what `Roster()` already
encodes and exactly what `B/Factions.xml`'s 43 `<waterritual>` entries already differentiate per
creed. **The tree becomes a MAP the founder reads, never a screen the founder spends into**, and
its branches diverge because the *acquisition* is scarce and place-bound, never because a node
costs points somebody else spent elsewhere.

### 2.2 The doctrine problem, stated before it is dodged

The source says this, and it is not ambiguous:

> *"Derived, never authored and never set: it is a readout of what the keepers have been taught and
> what they have certified fit for the grid … so it rises by playing rather than by spending
> anything on a research screen. **This mod has no research tree and does not want one — a tree is
> a second job, and the founder already has one.**"*
> — `T/Growth/KingdomZoningRules.cs:5-11`

And `_notes/EVOLVING-HEART-RESEARCH.md:970-974` already rejected a research tree by name, calling
any UI that *looks* like a tech tree a pillar violation "regardless of what it is called".

So the mandate ("different technology tree's and technology tree options") and the standing ruling
are in tension, and the tension must be resolved on the ruling's own terms rather than by
re-litigating it. The resolution:

| What the ruling forbids | What it does not forbid |
|---|---|
| A screen the founder pays into | Showing the founder the dependency graph their catalogue already contains |
| Abstract points banked and spent | Concrete things — a disk, a machine, a person — held and counted |
| A second job: a queue to manage, a thing that idles if unattended | A **map that renders state**, which is a rendering under Addendum 13's mesh condition |
| Branch choices that are opportunity costs on a budget | Branches that diverge because *the world put the knowledge somewhere*, and you went there or you did not |

**The one-line test to apply to every proposal in this section:** *can the founder do anything at
this surface except look?* If yes, it is a research screen and it is refused. Every acquisition
happens **out in the world** — at a ruin, at a merchant, at a water ritual, at a lodging — and the
map only ever tells you what you have and what it opens.

### 2.3 The tree that already exists, and is invisible

Every `<building>` record can declare six gates. Chase them and you have a DAG:

```
                                    [play the game]
                                          |
              +---------------------------+---------------------------+
              |                           |                           |
        disk taught (+1)          machine certified (+2)      people arrive (+0 tech,
              |                           |                    but +1 knowledge key)
              +-------------+-------------+                           |
                            |                                         |
                     TechLevel rungs                          origin:/creed:/culture:
                 hands→salvage→workshop→foundry→arclight               |
                            |                                         |
        +-------------------+--------------------+                    |
        |                   |                    |                    |
   MinTech gate        Knowledge gate       MinStage gate        Districts gate
        |                   |                    |                    |
        +-------------------+---+----------------+--------------------+
                                |
                          a design becomes commissionable
                                |
                    Refines (yards) → materials for the NEXT tier
                                |
                        L needs a yard; XL needs a yard headed by its notable
```

Three facts about this DAG as shipped:

1. **It is real and it is enforced.** `ZoningVerdict` refuses in a fixed, deliberate order —
   `RefusedUnlearned → RefusedTechLevel → RefusedTerritory → RefusedStratum → RefusedDistrict`
   (`T/Growth/KingdomZoningRules.cs:26-49`) — and every refusal already carries 7b prose naming
   what would fix it (`ZoningJudgement.Detail`/`.Note`, `:96-116`).
2. **It is almost entirely unexercised.** 7 of 63 designs declare `MinTech`; 4 declare `Knowledge`;
   exactly **one** is a certification gate (`Knowledge="machine:Solar Still"` on the condensery,
   `T/KingdomBuildings.xml:328`). `TechLevel.Arclight` is reachable at 14 points and **gates
   nothing that ships**.
3. **It is never drawn.** The only surface is a one-line readout —
   `"Craft: {{C|workshop}}  {{K|(2 more toward foundry)}}"` (`T/Growth/KingdomZoning.cs:198-212`) —
   and a list under "What the keepers know" (`:355-460`). A founder cannot see that certifying the
   solar still is what stands between them and a condensing hall until they try to build one.

**The single cheapest large win in this whole document:** *the tree is already built; nobody has
ever seen it.*

### 2.4 The shape: research as artifacts and people

Five acquisition channels, each of which is a thing that happens **in the world**, and each of
which already has machinery or a near neighbour in the mod. Note that no channel is a spend.

| # | Channel | The verb | Roster key minted | Shipped? |
|---|---|---|---|---|
| 1 | **Teach from a disk** | you carry a data disk to the keepers; it is read and handed back | `disk:<design>` | **Yes** — `T/Growth/KingdomZoning.cs:520-545`. Note the deliberate divergence from vanilla: *"The disk is not consumed"* (`:516-519`) |
| 2 | **Certify salvage** | you drag a machine home, pay water and hands, and the keepers pass it fit for the grid | `machine:<blueprint>` | **Yes** — `T/Core/KingdomCharterPart.cs:1067-1130`, `T/Growth/KingdomZoning.cs:173-183`; one-way by design |
| 3 | **Take in a people** | a settler of an origin/culture/species joins and brings their trade | `origin:` / `culture:` / `species:` | **Half** — origins ship; culture/species are the §1.4(a) extension. **Leaves with them.** |
| 4 | **Host a savant** | a named notable is lodged in housing that satisfies them, and teaches while they stay | `savant:<name>` | **Near neighbour** — "Guests at the gate" and the legendary-trader-wants-a-fine-house rule are both already in the brief; this is the same lodging gate with a knowledge key attached |
| 5 | **Learn a creed's technique** | the founder performs the water ritual with a faction; what that faction teaches becomes a thing the city can build | `rite:<faction>` | **Near neighbour** — lane 1 ("Water ritual with citizens") is scheduled W5; this reads the *founder's own* vanilla water-ritual grants |

Channels 1, 2 and 5 are permanent (a thing learned is learned). Channels 3 and 4 are **live** —
they hold only while the people hold — which is what makes a city's technology a portrait of who
lives in it rather than a checklist it has finished.

**Why this is Qud-native and a points tree is not.** Qud's own progression is exactly this shape:
Joppa → Argyve → Grit Gate is a power chain gated on *standing with places*; data disks are found
objects; the Becoming Nook is a place you travel to; a water ritual is a social act that grants a
skill. The C1 correction in `IDEA-INBOX.md` already established that a city may *make* things; what
it may never do is *demand the player's labour at a screen*. Artifacts-and-people demands travel,
which is the game.

### 2.5 Divergence: why an esper city cannot research what a chrome city can

This is the part the mandate specifically asks for, and vanilla has already written the table.

**`B/Factions.xml` ships 43 `<waterritual>` entries, and they grant materially different things:**

| Grant kind | Attribute | Factions granting it | What a city could do with it |
|---|---|---|---|
| A skill | `Skill` (32 uses) | most | the technique itself |
| Tinkering + **blueprints** | `Skill="Tinkering" Blueprints="1d3"` | **Barathrumites**; `Skill="Tinkering_Repair" Blueprints="1-2"` — **Daughters** | the craft/foundry branch |
| **Cybernetics credit** | `ItemBlueprint="CyberneticsCreditWedge"` | **Templar** (`Items="1"`), **Robots** (`Items="0-1"`), **Naphtaali** (alt-branch, if the partner is a Robot) | the chrome branch |
| **A mutation** | `Mutation="Slime Glands" MutationCost="100"` | **Oozes** | the flesh branch |
| **A random mental mutation** | `RandomMentalMutation="50"` | **Seekers** | the esper branch |
| Skill points | `SkillPointAmount="200"` | **Newly Sentient Beings** | — |
| A liquid | `Liquid` | **Entropic** (`warmstatic`), **Girsh** (`cloning`), **Mamon** (`blood`) | exotic inputs |
| A recipe | `Recipe` (8 uses) | Joppa, Kyakukya, Ezra, Hindren, Mopango, Barathrumites, YdFreehold, Chavvah | the favoured-dish lane, already in flight |
| Fungal infection | `FungusInfect="30"` | **Fungi** | the fungal branch |
| An oath | `HermitOath="100"` | **Hermits** | — |
| A **genotype-conditional** recipe | `RecipeGenotype="!True Kin"` | **Chavvah** | proof that genotype-gated grants are vanilla's own idiom |

**That is the tech tree's branch table, shipped, authored by the game's own designers, per creed,
and extended for free by every faction any mod adds.** We do not write it. We read it.

The divergence rule, stated once:

> **A city's technology options are the union of what its people brought, what its founder learned
> at other people's fires, and what somebody carried home from a ruin. Two cities differ because
> their founders went to different places and took in different people — never because a budget
> was spent one way instead of another.**

Which gives, concretely and without inventing a single faction:

- A **Seekers** city (esper): `rite:Seekers` opens mental-discipline works — a meditation hall,
  a glimmer-warded moot. It has **no** `rite:Templar` and so cannot raise the implant clinic, and
  the Seekers' own standing makes Templar arrivals impossible anyway (`CreedWeight` returns 0 for
  a disliking faction and a non-positive weight is *skipped*, `T/Core/KingdomCreedRules.cs:275-278`
  — a faction that dislikes the realm sends **nobody**).
- A **Templar** city (chrome): `rite:Templar` mints credit wedges through the water ritual, which is
  the license economy; it can raise the becoming-annexe. Its creed's own feelings toward
  mutant-associated factions do the fault-line work with no table of ours.
- A **Barathrumite** city (craft): `rite:Barathrumites` grants Tinkering plus **1d3 blueprints**,
  which is the disk channel arriving as a gift instead of a purchase — the foundry branch's
  natural home.
- A **Fungi/Ooze** city (flesh): `rite:Oozes` grants a mutation; `rite:Fungi` grants infection.
  The fungal style already has its own crop (`T/Growth/KingdomCropRules.cs:305`) and its own
  `taf:damp`/`taf:dark` QoL shape.

**And the fault lines are already correct**, because creed hostility reads
`Faction.GetFeelingTowardsFaction` and takes the worse of the two directions
(`T/Core/KingdomCreedRules.cs:341-368`). A city cannot hold `rite:Templar` and `rite:Seekers`
comfortably, and nothing of ours has to say so.

### 2.6 Three worked branches

Written as they would appear in the catalogue and the roster. Numbers are illustrative and must go
through `_notes/balance-sim.py` Q6 before they mean anything.

---

#### Branch A — THE FOUNDRY (the craft spine; available to any city, cheapest to a Barathrumite one)

The existing spine, made legible and given its missing top.

| Rung | Gate | Design | Acquisition that opens it |
|---|---|---|---|
| 1 | *(none)* | knapping yard, mud-brick pit | — |
| 2 | `MinTech="salvage"` | **sawyer's yard** (`Refines="shapedtimber"`) | 2 points: any two disks, or one certified machine |
| 3 | `MinTech="workshop"` | **mason's yard** (`Refines="shapedstone"`), charging post | 5 points |
| 4 | `MinTech="foundry"` + `Knowledge="machine:*Furnace"` | **smelter** (`Refines="workedmetal"`), condensing hall | 9 points **and** a furnace certified — you had to find one |
| 5 | `MinTech="arclight"` + `Knowledge="rite:Barathrumites\|disk:*"` | **the arclight forge** (XL, `Refines="alloy"`, exotic-consuming) | 14 points **and** either Barathrumite friendship or four taught disks |

**What the creed changes:** a Barathrumite city gets `Blueprints="1d3"` free from every water
ritual, so its disk count climbs by *being friends* rather than by shopping — reaching rung 5 by a
different road, not a shorter one. A Daughters city gets `Blueprints="1-2"` plus
`Skill="Tinkering_Repair"`, which is the **repair** half — the natural fit for the wear/mending
economy of Addendum 10(b), and a genuinely different play emphasis at the same rung.

**What it gates onward:** `arclight` is currently a rung with nothing behind it. This branch is the
reason it exists: the arcology's own construction, the lab's top rungs, and the megastructure
class all hang here.

---

#### Branch B — THE FLESH (the lab's spine; forbidden outright to some creeds)

| Rung | Gate | Design | Acquisition |
|---|---|---|---|
| 1 | `MinTech="salvage"` | **butcher's slab / bone yard** — the S work that makes a carcass into parts | — |
| 2 | `Knowledge="culture:Ooze\|culture:Fungal\|rite:Oozes"` + `MinTech="workshop"` | **the vat-house** — the M preservation work; the whole preservation chain (§3.5) starts here | either ooze/fungal people live here, or you shared water with the Oozes |
| 3 | `MinTech="foundry"` + `Knowledge="savant:*"` + a standing L craft district | **the grafting hall** — the lab proper, L | a savant is lodged; see §3 |
| 4 | `MinTech="arclight"` + `Knowledge="rite:Girsh\|machine:*Regeneration Tank"` | **the chimeric theatre** — XL, the named-procedure rung | Girsh friendship (their ritual pays in `Liquid="cloning"`) or a regeneration tank certified |

**What the creed changes:** a **Templar** city may not raise any of it. That is not a rule we
write — it is the fault-line ceiling (Addendum 4d) doing its job: Templar and the flesh-creeds sit
at named −100, and a Templar-creed city's residents refuse to live beside the people who staff a
vat-house. **The refusal is emergent, and the 7b sentence writes itself.**

**What it gates onward:** §3's rung ladder is exactly this branch's rungs 3 and 4.

---

#### Branch C — THE CHOIR (the mental/esper spine; a genuinely different way to play)

The branch that answers "an esper city researches what a chrome city cannot", and the one with the
least existing machinery — so the one to scrutinise hardest.

| Rung | Gate | Design | Acquisition |
|---|---|---|---|
| 1 | *(none)* | **the moot stone** — S memorial, `Carries="spirit:1"` | — |
| 2 | `Knowledge="rite:Seekers\|rite:Chavvah\|culture:Sightless Way"` | **the fasting cell** — M, `Provides="taf:quiet"`, staffed by one | shared water with the Seekers, or Sightless Way people live here |
| 3 | `MinTech="workshop"` + `MinZones=3` | **the listening hall** — L, reach `zone` | — |
| 4 | `MinTech="arclight"` + `Knowledge="rite:Chavvah"` + Moon Stair adjacency | **the assenting moot** — XL; the glimmer-ward machinery (`IDEA-008/011/013`), whose strength scales with how many minds assent | the map, not an unlock — `PsychicBiome` gates on `DescendsFrom("TerrainMoonStair")` |

**What the creed changes:** the whole branch is creed-acquired. A city with no mental creed reaches
rung 1 and stops, and that is fine — it built a foundry instead.

**Honest flag on Branch C:** rungs 3 and 4 lean on the glimmer-ward ideas (`IDEA-008`, `011`, `013`)
which are `SHAPED` but **unbuilt**, and on `COVERAGE.md` A7 (Moon Stair), also unbuilt. Rungs 1-2
are cheap and shippable now; 3-4 are a wave of their own and should not be promised in the same
breath as A and B. Vanilla note: `PsychicBiome.MutateGameObject` opens
`if (!Object.IsMutant()) return Object;` — the Moon Stair grants mental mutations **to mutants
only**, so the True Kin half of this branch is empty at exactly that location, which is a genuine
asymmetry to design around rather than paper over.

---

### 2.7 What the tree gates, end to end

| Thing gated | Gate today | Gate proposed | Note |
|---|---|---|---|
| The condensery-class producers | `MinTech="foundry"` + `machine:Solar Still` | unchanged — it is the **model** the rest should copy | The only certification gate in the file, and it is exactly right |
| The **arcology** | nothing (unbuilt) | `MinTech="arclight"` + Branch A rung 5 + the full material chain at scale | The heart ruling's top rung; megastructure cardinality holds |
| The **lab** | nothing (unbuilt) | Branch B rungs 3-4 | §3 |
| **`TechLevel` itself** | disks(1) + certifications(2) | **unchanged** | Do not add point sources. The temptation to make rites and savants worth points must be refused — see §5, rejection R3 |
| Heart rungs | proposed in the heart research | `workshop`/`foundry`/`arclight` per rung | *"No new gate type is needed"* — `EVOLVING-HEART-RESEARCH.md:814` |

### 2.8 The legibility surface — a map, not a screen

The existing "What the keepers know" screen (`T/Growth/KingdomZoning.cs:340-460`) becomes the map.
Three changes, all rendering, none interactive:

1. **Group the roster by what it OPENED.** Today it lists keys. Instead, for each key, name the
   designs it unlocked: *"Solar Still, certified — the condensing hall now stands within reach."*
   Pure derivation from the gate registry the mod already fills (`T/Growth/KingdomZoning.cs:68-94`).
2. **Name the nearest locked things and why.** For every design the settlement cannot yet raise,
   the existing `ZoningJudgement.Detail` already says what is missing. Sort by *how close*, show
   the top few. This is 7b applied forward instead of only at the moment of refusal.
3. **Name the roads not taken, by creed.** *"The keepers know nothing of the Barathrumites' way."*
   This is where the branching becomes visible without a single node being clickable.

**The hard rule, restated:** nothing on this screen can be pressed. It has no queue, no budget, no
percentage, and no timer. It is the city book's technology chapter — which is exactly the surface
Addendum 13 says a lane must ride.


---

## 3. The lab, designed

### 3.0 Grounding — the three real mods, read end to end

The author's mid-flight direction was to *"actually look into the absorption mechanic, and creature
controls additions to it."* All three relevant mods are installed on this machine with full readable
C# source, and all three were read directly. Reception below is from the Steam Workshop
comment/discussion tabs and is marked **second-hand**; everything about mechanism is from source.

| | **Playable Slime 1.0** | **Creature Control** | **Playable Golem** |
|---|---|---|---|
| Where | `W/3388408799/` | `M/CreatureControl/` (dev copy) | `W/3270006554/` |
| Workshop | `3388408799` | **`3350013464`** | `3270006554` |
| Author | Symbiode | **AussieWarGod / `{{G|r}}` — this project's author** | John Snail |
| Version / updated | `0.2.5`, last updated **31 Mar 2025** | `0.4.0`, last updated **17 Aug 2026** | `1.1.0.3` |
| Reach (second-hand) | 1,249 subs, 46 ratings, **4 stars**, 59 comments | 176 subs, 31 favourites, "not enough ratings" | 102 comments |
| Size | 25 files, **4,992 lines** (incl. ~1,400 lines of abandoned `LegacyClasses/`) | 20 files, **2,880 lines** | 3 files, **1,320 lines** |
| Harmony? | **No** — pure `IScribedPart`/`BaseMutation` + XML | **Yes**, throughout | partial |

**The manifest states the integration in one sentence:** *"With Playable Slime installed, digesting
creatures grants their chords and special parts (Trophic Absorption; configurable in Options)."*

#### (a) Playable Slime — the absorption mod

There is no custom "absorb" command. The genotype grants vanilla `Engulfing`; the mod hooks the
engine's `EndTurnEngulfing` event in `Sym_ConsumeScribed`. Each turn a creature is engulfed it takes
enzyme damage; on its death five gain paths fire — body (anatomy for Mimicry), stats (only where the
eaten creature's base exceeds yours), MP, mutations, SP.

The mutation path (`W/3388408799/Sym_ConsumeScribed.cs:200-258`) walks the target's
`Mutations.MutationList`, filters against a hardcoded ~50-entry `excludedMutations` deny-list
(`:37`) and `mutation.CompatibleWith(this.ParentObject)`, then
`parentMutations.AddMutation(mutation.Name, variant, 1)`. **It re-instantiates by NAME, so the
source creature's field state is lost** — the exact hole Creature Control later patches.

A second axis, `Sym_EvolveScribed` (877 lines), catalyses liquids: standing in an open pool
(`LiquidVolume` where `MaxVolume == -1`) consumes 500 drams and permanently maps that liquid through
a 26-entry `PSEUDOPODS` table to effect scripts — `pod:` swaps a pseudopod's `DefaultBehavior` to a
weapon blueprint, `stat:` bumps a stat, `part:` attaches a real `XRL.World.Parts` (`DisarmOnHit`,
`LifeDrainOnHit`, `CloneOnHit`). Limbs are grown with
`Body.AddPartAt(Base:"Pseudopod", InsertAfter:"Hand", OrInsertBefore:"Missile Weapon", ...)` and shed
with `Body.RemovePart`.

**The reception is the single most important design input in this section**, and the author of that
mod reached the same conclusion independently in `FutureIdeas.txt`:

- **Overpowered is the dominant complaint** (second-hand): *"totally overpowered due to how easy
  they acquire mutation/stats"*; *"grossly overpowered by level 6"*. It drove a whole second
  "Balanced" genotype.
- **Engulf degeneracy** — one-turn cheese kills; requests for a cooldown. `FutureIdeas.txt`:
  *"Toughness currently provides both HP and DPS, all the slime needs to fight further. That's not a
  good position to be in. it makes all fighting styles but Spamming statpoints into only toughness
  and using nothing but engulf for attacks unneccessary."*
- **Compat drift** — multiple 2026 reports that the genotype no longer appears in chargen.
- **The prompt is not player-gated** — non-slime players get mutation pop-ups when an NPC slime eats
  something.
- Its own to-do file records that **mental mutations are near-unobtainable** because few creatures
  carry them, *"Esper-archetypes especially are hit very hard by this"* — which independently
  corroborates §2.6's Branch C asymmetry flag.

**Verdict on the shape:** a free, repeatable, unbounded absorb verb ate that mod's combat design and
its own author says so. **Every cost the lab imposes exists because of this.**

#### (b) Creature Control's Trophic Absorption — the direct precedent

`M/CreatureControl/Trophic Absorption/` is 1,021 lines: `TrophicRepertoire.cs` 484,
`Patch_TrophicAbsorption.cs` 448, `Patch_SlimeMutationPrompt.cs` 89. Two Harmony patches, both
**soft dependencies by construction** — the target is resolved by string name and `Prepare()` returns
false when absent:

```csharp
private static MethodBase Target => AccessTools.Method("XRL.World.Parts.Sym_ConsumeScribed:HandleEngulfingEvent");
private static bool Prepare() => Target != null;
```

The load-bearing techniques, each of which the lab inherits:

1. **Snapshot-before-death.** The original destroys the target, so *"nothing useful can be read from
   the target afterward"* (`:20-27`). The Prefix captures blueprint, display name, a copy of
   `PartsList`, `HasPart<NephalProperties>()`, and the whole `Render` identity into `__state`; the
   Postfix detects consumption via `!GameObject.Validate(ref check)` — the engine's own liveness
   check — and works entirely from the snapshot.
2. **A whitelist by PART CLASS, in plain text, split by risk.** `AbsorbableParts.txt` (63 lines):
   `[Standard]` (~40 entries — attack riders `PoisonOnHit`/`BleedingOnHit`/`ConfuseOnHit`/`StunOnHit`/
   `DischargeOnHit`/`TemperatureOnHit`/`RustOnHit`/`DiseaseOnHit`/`StoneOnHit`/`DrunkOnHit`/
   `EatMemoriesOnHit`/`MutateOnHit`; defences `ReflectProjectiles`/`ReflectDamage`/`RefractLight`/
   `NoKnockdown`/`GasImmunity`/`ImmuneToSleepGas`/`ImmuneToConfusionGas`/`EffectResistance`/
   `SaveModifier`; utility `MovespeedInLiquid`/`CarryBonus`/`Drill`/`DiggingTool`/`FungalVision`/
   `LiquidProducer`/`FabricateFromSelf`/`Rummager`; always-on `Spawner`/`Consumer`/`LeavesTrail`/
   `Breeder`/`ActiveLightSource`/`RealityStabilization`; flavour `Pettable`/`Lovely`) and `[Spicy]`
   (11 entries, off by default, **each with its own Options checkbox and a written risk rationale** —
   `Reconstitution`, `SplitOnDeath`, `Cloneling`, `Mimic`, `MimicProperties`, `Engulfing`,
   `EngulfingDamage`, `FugueOnStep`, `StunningForceOnJump`, `Twinner`, `Triner`). The file's own
   header: they *"are experimental and can break balance, saves, or quest sequences."*
3. **`IPart.DeepCopy(absorber)`** is the whole grant mechanism (`:225`) — the source's field values
   carry over, so *your* sting is *its* sting, at its numbers. This is the fix for Playable Slime's
   by-name re-instantiation.
4. **One live instance per part type, always** — *"duplicates double-fire events and escape
   GetPart-based toggles"* (`:172-174`). Repeats are recorded as **variants**, never stacked.
5. **`TrophicRepertoire`** — a `[Serializable] IPart` of parallel primitive lists (`TypeNames`,
   `SourceNames`, `FieldBlobs`, `Active`, `Excluded`, plus cosmetic guise lists), field values
   flattened to name/value pairs joined by two private separator characters. `Activate(index)`
   rebuilds the part from stored fields via `Activator.CreateInstance` + reflection, explicitly
   *"mirror[ing] the engine's own GamePartBlueprint approach: instantiate by type, set fields from
   strings."*
6. **Custom versioned serialization with a magic header** (`FormatMagic = 0x51D3C0DE`,
   `SerializationVersion = 2`, an explicit branch per released version, a documented one-time-reset
   migration). The comment states the lesson plainly: *"the engine's default reflection
   serialization is positional, so any field-layout change between mod versions would silently drop
   this part — and with it the player's entire absorbed collection."*
7. **Three-way consent, every time**: *"Incorporate it." / "Not this time." / "Never absorb X."* —
   the third writes to a permanent `Excluded` list. **NPCs absorb silently; only the player is
   asked.** (Patch B additionally replaces Playable Slime's raw
   `Popup.AskString("...y/n or 'x'...")` with a hotkeyed `Popup.PickOption` and gates it to the
   player, which incidentally fixes the reported NPC-prompt bug — and adds variant CHOICE where the
   original rolled randomly.)
8. **Silent dedupe of identical variants** — `IsKnownVariant` compares type **and** field blob, so
   field-less parts never prompt twice: *"Only genuinely new field states are worth the player's
   attention."*
9. **Vanilla registries reused, not rebuilt** — `NephalProperties.Chords` is the game's own
   blueprint-to-chord-type dictionary, instantiated with
   `Activator.CreateInstance(chordType, nonPublic: true)` (`:118-131`).
10. **A real defect handled by hand.** `SyncPufferIdentity` (`:250-300`) exists solely because
    Playable Slime grants mutations as fresh instances by name; an absorbed spore puffer would puff
    *generic* spores. The fix reflects `PuffObject` and `ColorString` off the snapshot onto any
    still-default instance. **This is the class of bug the lab will hit repeatedly, and the reason
    §3.9's estimate is not small.**

**The UI already exists in the idiom the mandate names.** `Control Menu/ControlMenu.cs` (472 lines)
is **one** activated ability opening **one** menu of rows. Its `Row` struct is the whole vocabulary:
`Label`, `Command`, `VariantType` (*"short part/mutation type name with absorbed variants to switch
between; null when only toggling applies"*), `IsPassive`, `IsExclusions` (*"sentinel row that opens
the never-absorb exclusion manager"*), `IsGuise` (the cosmetic picker — appearance only, explicitly
contrasted in a comment with Playable Slime's Mimicry, which *"swaps the body plan — not ideal for a
slime with heaps of pods"*). The class doc states the constraint: *"One slot on the abilities bar no
matter how much has been absorbed."*

It also carries two hard-won pieces of engineering the lab gets for free: a **static**
duplicate-open guard (*"open-commands can be delivered multiple times per press … the engine
dispatches string events to every registered handler"*, plus an explicit close-recorded flag because
*"a sentinel like int.MinValue makes the first tick-delta wrap negative and swallows every open
forever"*), and a ~150 ms key-bleed forgiveness so the opening keypress does not instantly cancel.

**One live finding, surfaced because it matters and is cheap to fix:** `ControlMenu.cs` still
contains an active block marked `// TEMP-DIAG (remove before Steam upload)` that calls
`UnityEngine.Debug.Log` on every menu open. It shipped in the 17 Aug 2026 update. Not this
document's business, but the author should know.

#### (c) Playable Golem — the reference implementation of the picker

`W/3270006554/` (1,320 lines) is the third precedent and the most directly useful for §3.8, because
**it reuses the vanilla golem system wholesale**: `using XRL.World.Quests.GolemQuest;`,
`GolemMaterialSelection<string,string>.Units[liquid](liquid)`,
`GolemMaterialSelection<JournalAccomplishment,MuralCategory>`, `GolemBodySelection.GetBodyBySpecies()`,
`GameObjectUnit`. Its `Pick<T>` is commented `// GolemMaterialSelection.Pick` — a deliberate
re-implementation. If the lab is built in that idiom, this is the working reference.

It also absorbs on a budget rather than on kills: **one of four things every 12 MP gained or spent** —
3 drams of liquid (catalyst), an adjacent creature (atzmus), a chronology entry (incantation), or an
inventory item under 5 lbs (hamsa). That is a rationing model worth noting beside ours.

**Its complaints (second-hand) are a warning about anatomy changes**, not about the picker: golems
are Gigantic, so most gear cannot be equipped and several endgame objects — including the Spindle
ascension — cannot be entered. **Changing a player's body can lock them out of content that has
nothing to do with your mod.** §3.8's removal verb exists partly for this.

#### (d) The vanilla golem quest — the picker's canon

`D/XRL/World/Quests/GolemQuest/` is ~3,500 lines across 11 files.

- Seven slots keyed exactly `Body`, `Catalyst`, `Atzmus`, `Armament`, `Incantation`, `Hamsa`,
  `Power` (`GolemQuestSystem.cs:14-44`), plus a `Soup` quest step satisfied by 20 drams of
  primordial soup. Pax Klanq frames them as *"the body as a model, the catalyst to charge the
  sanguine fluid, the atzmus as deistic direction, the armament for protection, and the hamsa for
  personality."*
- **There is no dedicated screen class.** The UI is two levels of `Popup.PickOption`:
  `GolemQuestMound.DisplayOptions` (`:134-184`) lists slots with `{{red|[X]}}` / `{{green|[check]}}`
  marks and a `<make a selection>` sentinel; picking one calls `GolemMaterialSelection.Pick`
  (`:80-143`), which paginates candidates at 250 per page with per-entry icons and effect lines.
- Effects are shown **before** commitment: the first prefixed `{{rules|--}}`, alternatives prefixed
  `{{rules|OR}}` (`GolemMaterialSelection.cs:145-180`), because **only one listed effect is applied,
  at random** (`:209`).
- **The Atzmus slot is exactly our verb.** It qualifies on a *severed limb* — an object carrying
  `DismemberedProperties` or the property `LimbSourceGameObjectBlueprint`
  (`GolemAtzmusSelection.cs:33-44`) — resolves the source creature, and grants one of its non-defect
  mutations, or `+5` to the source's highest attribute if it has none (`:129-180`). Blocklist at
  `:21`: `NightVision, DarkVision, Invisibility, OldElectricalGeneration, WallWalker, Metamorphosis`.
- Materials are **consumed and conflict-checked**: `Consumed = 1` triggers `SplitStack` +
  `Obliterate`, and `GetFirstConflict` forbids one object satisfying two consuming slots
  (`GolemGameObjectSelection.cs:98-128`).
- The build takes **three days** (`B/Conversations.xml:5292-5307`, `TimeDays="3"`), during which the
  mound answers *"The creature is being shaped. It will be finished ..."*.
- Every applied effect is **inscribed into the object's own description** — `GameObjectUnit` carries
  `CanInscribe()` and `GetDescription(bool Inscription)`, and `Apply` appends to a `RulesDescription`
  part; the golem's description is templated with `=atzmus.creature=`, `=body.features=`,
  `=catalyst=`, `=hamsa=` (`GolemQuestSelection.ProcessDescription:134-176`).

**Community sentiment on that picker (second-hand), and it is a precise, actionable finding:**
searches turned up **no** complaints about the picker being tedious to navigate. The complaint is
**consequence-legibility**. Players treat the Atzmus as a lottery — *"There are actually not many
powerful atzmus available, and there is a chance that it just doesn't do anything"* — and an
external guide exists specifically because the payoff of each choice is opaque at the point of
choosing. **Playable Golem's picker answers this by printing each `GameObjectUnit.GetDescription()`
under the option name. So should ours, and ours has the easier job because ours is not random.**

### 3.1 What we co-opt, what we do differently

| Thing | Precedent | Ours |
|---|---|---|
| Grant by **part class**, whitelisted, risk-tiered | `AbsorbableParts.txt` `[Standard]`/`[Spicy]` | **same split, shipped as `KingdomProcedures.xml`** so it is mergeable third-party data like the rest of the catalogue |
| Copy mechanism | `IPart.DeepCopy(absorber)` | same — never `AddMutation` by name where a part will do |
| Grant a **limb** | `BodyPart.AddPartAt(..., Manager: key)`; `Body.RemovePartsByManager(key)`; precedents `CyberneticsGraftedMirrorArm`, `HelpingHands`, `Waldopack`, `SoupSludge` | same, with `Manager = "TAF::Lab::<key>"` so every graft is reversible in one call |
| Provenance | `DismemberedProperties{SourceID, SourceBlueprint, SourceGenotype, SourceBlood, BodyPart}`, written by `Body.Dismember` (`D/.../Body.cs:2557`) | same — **`SourceGenotype` is already recorded, and that is the chronicle's line** |
| Anatomy-slot gating | `CyberneticsBaseItem{Slots="Arm,Hands" Cost="5"}` + `BodyPart.CanReceiveCyberneticImplant()` (`Category == ANIMAL && !Extrinsic`) | **same two-field model**, but the category test is per-procedure rather than hardcoded to ANIMAL |
| Multi-slot commit UI | golem's two-level `Popup.PickOption`; Playable Golem's re-implementation | same |
| Effect legibility | Playable Golem prints `GetDescription()` per option | **same, and mandatory** — it is the one documented complaint about the vanilla idiom |
| Duration | golem's flat 3 days | **labour, not a timer** (Addendum 8) — staff-days worked by real crew |
| **Cost** | absorption is free and repeatable | **the whole difference.** Water, materials, bits, a preserved part, staff-days, and standing |
| **Consent** | three-way prompt each time | same prompt; but the *commitment* is a staked commission, not a click |
| **Randomness** | golem applies ONE listed effect at random | **rejected.** A procedure that cost a season and a fortune may not roll dice. You get what the slate said. |
| **Repeatability** | absorb every kill, forever | **rationed by anatomy slots and by rung** — the ceiling is your body, not your patience |
| Serialization | `TrophicRepertoire`'s magic-header versioned format | **inherit verbatim, from version 1** |
| Where the record lives | on the player's body (`IPart`) | **on the player AND in the city's chronicle.** "Everything is remembered, twice" |
| Compat posture | Playable Slime hard-broke on a Qud update; Creature Control soft-resolves by name | **soft-resolve everything**, and never Harmony-patch another mod as a dependency |

**The difference in one sentence:** *Playable Slime asks "what did you kill?"; the lab asks "what did
you bring home, who did you find to do it, and what did your city give up to let them."*

### 3.2 What the lab is, in fiction

A **grafting hall** — a walled L plot in the craft district, staffed by a lodged savant and two
hands, standing over a vat-house that keeps what you carry in. It does not sell augmentation. It
performs it: once, slowly, at a price the city feels, on a body it can see.

It is not a Becoming Nook, and the distinction matters. The Nook is True Kin infrastructure that
already exists in the world, gated on `IsTrueKin()` (`D/XRL/UI/CyberneticsTerminal.cs:481-487`),
priced in license points, and reached by travel. The lab is *meat*, and its gate is the opposite
one. **A city may have both, and a founder who wants both must hold two creeds at once and pay the
fault-line price for it.**

**On the standing question this reopens:** `IDEA-INBOX.md`'s C2 correction withdrew the claim that
vanilla lets a non-True-Kin use the Nook, and ruled that *"a city granting cybernetic eligibility to
a mutant is a genuinely new thing and must be argued on its merits."* **This document does not argue
it.** The lab grants flesh, not chrome, and deliberately leaves the chrome question where C2 left
it. If the author wants the chrome half, it is a separate ruling (§6, Q7).

Worth recording for whoever picks that up: `GetCyberneticRejectionSyndromeChance`
(`D/XRL/World/Parts/CyberneticsBaseItem.cs:220-235`) computes a value and then unconditionally
`return 0;` at `:234` — **the mutant-rejection effect is dead code in this build.** That is a fact
about the substrate, not permission to use it.

### 3.3 The rung ladder

Four rungs. Each is a **building tier climbing within one plot** (improvements climb within a plot;
sizes compete across plots), except rung 3, which competes for XL ground.

**RUNG 0 — the butcher's slab.** *(S, craft, `MinTech="salvage"`)*
Not the lab: the prerequisite. The work that turns what you drag home into *parts*. Rides vanilla
`Butcherable` (gated on `CookingAndGathering_Butchery`) and `Corpse`.
- **Player gets:** parts as real items, and a reason to carry corpses home.
- **Cost:** trivial — `Materials="timber:6,stone:4"`, 2 staff.

**RUNG 1 — the vat-house.** *(M, craft, `MinTech="workshop"`, `Knowledge="culture:Ooze|culture:Fungal|rite:Oozes|rite:Fungi"`)*
The preservation chain (§3.5). Nothing is grafted here; things are *kept*.
- **Player gets:** a preserved part is a **permanent, storable, tradeable item** — immediately
  useful even if the lab is never built. That is the honest "bonus for engaging, never a penalty for
  abstaining".
- **Cost:** `Cost="30"`, `Materials="stone:20,shapedstone:6,scrap:10"`, `Bits="00"`, 3 staff,
  `Provides="taf:damp,taf:offal"`.
- **Creed friction begins here** (§3.6).

**RUNG 2 — the grafting hall.** *(L, craft, `MinTech="foundry"`, `Knowledge="savant:*"`, needs a standing mason's yard)*
The lab proper. **Class-I and Class-II procedures.**
- **Player gets:** riders and defences — the `[Standard]` half. A sting that poisons. A hide that
  reflects. Immunity to the gas that has been killing you.
- **Cost:** `Cost="90"`, `Materials="stone:60,shapedstone:20,workedmetal:8"`, `Bits="0023"`,
  5 staff **including one lodged savant** — and the savant leaves if their housing stops satisfying
  them, through the lodging machinery that already ships (Addendum 4b).
- **Per-procedure cost on top:** the preserved part, water, bits, and staff-days of real labour.

**RUNG 3 — the chimeric theatre.** *(XL, craft, `MinTech="arclight"`, `Knowledge="rite:Girsh|machine:*Regeneration Tank"`, headed by its notable per Addendum 6)*
**Class-III procedures: limbs.** Where the anatomy actually changes.
- **Player gets:** a new body part grafted at a named slot, with its natural weapon or its utility —
  and the named procedures (§3.7).
- **Cost:** the full XL ladder — `Exotics`, a headed office, a mason's yard *and* a smelter, and a
  season of world-days.
- **Cardinality:** rung 3 is XL but is **not** a megastructure and does not contend with the arcology
  under the one-per-city rule. If the author wants it to, that is an explicit ruling (§6, Q6).

### 3.4 Procedures — part-class, anatomy-slot gated

A procedure is a **catalogue record** in the same idiom as a building, shipped in
`KingdomProcedures.xml`, mergeable by key. The schema deliberately mirrors `CyberneticsBaseItem`'s
two load-bearing fields:

```xml
<procedure Key="poisonsting" DisplayName="the envenomed sting"
           Class="I" Grants="PoisonOnHit"
           Slots="Tail,Arm,Hand"              <!-- BodyPart.Type list; exactly CyberneticsBaseItem.Slots -->
           SlotCategories="Animal,Arthropod"  <!-- BodyPartCategory names; omit for any live category -->
           Source="part"                      <!-- part | limb | mutation -->
           MinRung="2"
           Cost="20" Bits="002" StaffDays="6"
           Preserved="1"
           Creeds="-Templar,-Mechanimists" />
```

**Four hard rules:**

1. **`Grants` names a PART CLASS — never a creature, never "a creature's power".** This is the
   mandate's own instruction and it is also what makes the system extensible: a modded creature
   carrying `PoisonOnHit` is a valid source for `poisonsting` the day that mod ships, with no entry
   of ours. The whitelist is the contract, exactly as `AbsorbableParts.txt` is.
2. **`Slots` is checked against the PLAYER'S OWN ANATOMY, by `BodyPart.Type`.** A player with no
   `Tail` and no free `Arm` is refused **by name** per 7b: *"there is nowhere on you to put it."*
   This is the rationing mechanism, and it is why the lab cannot become a shopping list — the 19
   base part types (`B/Bodies.xml:5-23`) and their 153 variants are a finite budget that the
   player's genotype and prior grafts have already partly spent.
3. **`SlotCategories` gates on `BodyPartCategory`** (23 constants, `D/.../BodyPartCategory.cs:8-52`:
   ANIMAL, ARTHROPOD, PLANT, FUNGAL, PROTOPLASMIC, CYBERNETIC, MECHANICAL, METAL, WOODEN, STONE,
   GLASS, LEATHER, BONE, CHITIN, PLASTIC, CLOTH, PSIONIC, EXTRADIMENSIONAL, MOLLUSK, JELLY, CRYSTAL,
   LIGHT, LIQUID). Vanilla's own precedent is `CanReceiveCyberneticImplant()` refusing anything not
   ANIMAL. Ours is per-procedure — **which is how a True Kin, a robot player, and a slime player each
   get a different legal procedure set for free, derived, with no genotype list anywhere in the
   code.**
4. **No procedure is ever random.** Golem randomness is rejected by name (§3.1). The one exception
   is a named procedure that is *confessedly* a gamble and is priced as one (§3.7).

**Three source kinds, three verbs:**

| `Source` | What you must bring | Read path | Write path |
|---|---|---|---|
| `part` | a preserved part from a creature carrying that `IPart` | the snapshot idiom — read `PartsList` **before** butchering, stamp the class list onto the preserved item | `IPart.DeepCopy(player)` — Trophic Absorption's own mechanism |
| `limb` | a preserved severed limb (`DismemberedProperties` present) | `Body.Dismember` already writes full provenance including `SourceGenotype` | `BodyPart.AddPartAt(type, Manager: "TAF::Lab::<key>", ...)`, precedent `CyberneticsGraftedMirrorArm` |
| `mutation` | a preserved gland or organ from a mutation-bearing creature | Atzmus's own path — `Mutations.MutationList`, non-defect, blocklisted | `Mutations.AddMutation(class, level)` — **capped at level 1-3, never the source's level** |

**Class ladder, mapped to the whitelist's own risk split:**

| Class | Rung | What | Source list |
|---|---|---|---|
| **I — riders** | 2 | `PoisonOnHit`, `BleedingOnHit`, `ConfuseOnHit`, `StunOnHit`, `DischargeOnHit`, `TemperatureOnHit`, `RustOnHit`, `DiseaseOnHit`, `StoneOnHit` | `[Standard]` attack-rider block |
| **II — defences and utility** | 2 | `ReflectProjectiles`, `RefractLight`, `NoKnockdown`, `GasImmunity`, `ImmuneToSleepGas`, `ImmuneToConfusionGas`, `EffectResistance`, `SaveModifier`, `MovespeedInLiquid`, `CarryBonus`, `Drill`, `DiggingTool`, `FungalVision`, `Rummager` | `[Standard]` defence + utility blocks |
| **III — limbs** | 3 | a new `BodyPart` at a named slot, with its default natural weapon | golem `GameObjectBodyPartUnit` |
| **IV — named** | 3 | §3.7. One each, forever. | authored |

**Refused outright, and staying refused** — the `[Spicy]` list is a warning, not a menu:
`Reconstitution`, `SplitOnDeath`, `Cloneling`, `Mimic`, `MimicProperties`, `Engulfing`,
`EngulfingDamage`, `FugueOnStep`, `StunningForceOnJump`, `Twinner`, `Triner`, and every
self-replication or duplication part. `_notes/COVERAGE.md`'s cross-cutting question 3 already names
cloning as a dominant-exploit vector. Also permanently blocklisted, following the golem's own list:
`Invisibility`, `WallWalker`, `Metamorphosis`, `OldElectricalGeneration`.

### 3.5 The preservation chain

**The finding that shapes this section: vanilla has no rot.** No `Decay`, no `Rotting`, no spoilage
timer — verified by exhaustive grep across `D/XRL/` and `B/`, independently reconfirming
`VANILLA-PRODUCTION-TRUTH.md`'s section 0 verdict. `PreservableItem` is a two-field marker with no
behaviour at all (`D/.../PreservableItem.cs:8-10`).

**So we do not invent rot.** A decay timer would violate Addendum 8 clause 2 (rates are time x labour
x infrastructure, never time alone) and would be exactly the second job the vision forbids. The chain
is instead **a labour gate, in vanilla's own idiom**:

```
   kill it, out in the world
        |
        v
   BUTCHER it   (rung 0, or vanilla Butcherable + CookingAndGathering_Butchery)
        |   -> a raw part item, stamped at creation with the source's blueprint,
        |      its IPart class list, and - for limbs - DismemberedProperties
        v
   CARRY it home   -- and here is the only "spoilage" in the design, and it is vanilla's own:
        |           Temporary.CarryOver is invoked on dismemberment (D/.../Body.cs:2596),
        |           so a limb from a temporary or summoned creature is ITSELF temporary.
        |           You cannot harvest a conjured thing. Vanilla already said no.
        v
   PRESERVE it at the vat-house   (rung 1 - a staffed work, real labour, real world-days)
        |   -> the raw part is obliterated; N preserved parts are issued, on exactly
        |      Campfire.Preserve()'s own arithmetic: Result x Number x Count
        |      (D/.../Campfire.cs:539-565)
        v
   a PRESERVED PART: permanent, storable in a dedicated larder, tradeable,
   and the sole legal input to any procedure
        |
        v
   COMMISSION the procedure at the lab   (rung 2 or 3)
```

**Why a raw part cannot go straight to the lab.** Not because it rots — because **the lab refuses
it, by name**: *"the hall will not open a body for a thing that was not kept."* The gate is the
vat-house's existence and its labour, which is the honest cost the author asked for. It also makes
the vat-house worth building on its own — preserved parts are a trade good — rather than being pure
tax on the lab.

**Quantities.** `PreservableItem`'s shipped numbers are the calibration: raw bear meat gives 5
jerky, a dawnglider tail 10 cured, a psychal gland 5 paste. So **3-8 preserved parts per carcass** is
vanilla-shaped, and a Class-III limb procedure consuming a whole creature's yield reads correctly:
*one creature, one limb.*

### 3.6 Creed friction as a feature

The mandate calls friction a feature. **Nothing new is written; three shipped systems are pointed at
the lab.**

1. **`Refuses` tags (Addendum 4).** The vat-house and hall declare `Provides="taf:damp,taf:offal"`;
   a resident who `Refuses="taf:offal"` will not live in the quarter. This one is **authored**, not
   derived — correctly, because `Derive()` deliberately never produces a `Refuses`
   (`T/Core/KingdomQolRules.cs:383-389`): revulsion is a belief, not a body plan.
2. **The fault-line ceiling (Addendum 4d).** Templar and the flesh-creeds sit at named -100 in
   `B/Factions.xml`. A Templar-creed city physically cannot house the people who would staff a
   vat-house, at any tier including Private. **The Templar city cannot build the lab, and no rule of
   ours says so** — the layout grammar partitions, the housing binds, the commission finds nobody to
   staff it, and 7b names it. That is the design working.
3. **Standing cost on use.** Each procedure carries `Creeds` in the same `-Faction` removal idiom
   the QoL vocabulary already uses. A Class-III graft in a city with Templar standing on the books
   costs standing, through the existing `AdjustStanding` path
   (`T/Core/KingdomSystem.cs:1516-1524`), the existing chronicle entry, and the existing outsider
   register drift.

**Three friction events worth authoring**, all as happenings on existing surfaces:

| Event | Trigger | Shape |
|---|---|---|
| **The hall is spoken against** | first Class-II procedure while a hostile-creed minority lives in the city | a petition, in the existing petitions machinery. Answering it is the founder's call; there is no correct answer |
| **The savant's price** | the lodged savant's creed differs from the city's | they ask for one thing — a shrine left unconsecrated, a neighbour rehoused. Rides notable tastes |
| **Somebody leaves** | a resident whose `Refuses` the hall now violates | the existing roof-brink path, warned per Addendum 10(a), arrestable |

**What must NOT happen:** no meter, no "revulsion score", no city-wide happiness number. Friction is
**placement constraints and named people**, exactly as Addendum 4's pillar guard says.

### 3.7 The exceptional named procedures

Four. Each is **once, ever, per character**; each is tied to a specific creature the world contains;
each is discovered out in the world rather than listed on a menu. They are the lab's answer to
"endgame content", and the reason it is not a shopping list.

| Name | Source | Slot | What the player gets | Gate |
|---|---|---|---|---|
| **The Weeping Graft** | a `waterLichen`, the giant water weep (`B/.../Creatures.xml:9935-9939` — a `LiquidLichen` carrying `<mutation Name="LiquidFont" Liquid="water"/>`) | `Back` or `Fungal Outcrop` | `LiquidFont` at minimum level: **you weep water.** In a mod whose spine is water-as-covenant, the most thematically loaded augmentation available | rung 3; the weep must be **found, killed, and carried** — it is a creature, killable and losable |
| **The Chimeric Confession** | any creature; the procedure is the *method*, not the source | random, per `AddChimericBodyPart` | one limb by the game's own chimera weighting (`Anatomies.GetRandomBodyPartType(..., UseChimeraWeight: true)`, `D/.../Mutations.cs:551-604`) — **and it is random, because this one is confessedly a gamble, and is priced as one** | rung 3; **refused to a player who already has `Chimera`** (they get this for free); `Esper` players refused by the morphotype exclusion |
| **The Cold Regard** | a Girsh nephal | `Face` | one nephal chord, via the game's own `NephalProperties.Chords` registry — the exact mechanism Trophic Absorption already uses (`Patch_TrophicAbsorption.cs:118-131`) | rung 3 + `rite:Girsh`; the Girsh water ritual pays in `Liquid="cloning"`, which is also this procedure's catalyst |
| **The Lantern Rib** | a `LuminousInfection`-bearing creature | `Icy Outcrop` — the vanilla precedent adds exactly this part type (`D/.../LuminousInfection.cs:47`) | a permanent light source grown from your own body, with the glow toggle Creature Control already ships | rung 2 |

**Why exactly four, and why these:** each already exists as working vanilla or working mod machinery;
none requires a new effect system; each is a thing in the world you must go and get; each reads
instantly as a Qud sentence. A fifth invented one would be a fifth thing to balance.

### 3.8 The UI

**One screen, two levels, both `Popup.PickOption`. No new screen class.** This is simultaneously the
golem's shape, Playable Golem's shape, and Creature Control's shape — which is not a coincidence; it
is Qud's house idiom.

**Level 1 — the slate** (opened at the hall, or from the Charter's works menu):

```
                    the grafting hall of Ashkelon
     savant: Nuntu, who was a bone-surgeon at Ezra    [lodged, content]
     preserved parts in the vat-house: 11

  [a]  your left arm          {{green|[check]}} the envenomed sting
                                {{rules|--}} attacks poison on hit, from the salthopper
  [b]  your face              {{red|[X]}} {{K|<nothing grafted>}}
  [c]  your back              {{red|[X]}} {{K|<nothing grafted>}}
  [d]  a tail                 {{K|there is nowhere on you to put one}}
  ---
  [p]  What the vat-house is keeping
  [x]  Procedures the hall will never perform
  [Backspace]  Commission a procedure
```

Straight from `GolemQuestMound.DisplayOptions` (`:134-184`): the `[X]`/`[check]` marks, the
`<make a selection>` sentinel, the `[Backspace]` commit item, `AllowEscape`.

**Level 2 — the candidates**, from `GolemMaterialSelection.Pick` (`:80-143`), Playable Golem's
re-implementation, and Trophic Absorption's prompt shape:

```
  Choose a procedure for your left arm

    the envenomed sting        [salthopper mandibles, preserved x3]
      {{rules|--}} your attacks poison on hit
      {{rules|--}} 20 drams, 002 in bits, six days of the hall's work
    the reflecting hide        [gel-crab carapace, preserved x5]
      {{rules|--}} reflects projectiles
    {{K|the drill-arm}}        {{K|- the hall knows it; nothing kept is a source}}
```

Inherited exactly:

- **effects shown before commitment**, `{{rules|--}}` prefixed — this is the fix for the one
  documented complaint about the vanilla idiom (§3.0d);
- **`{{rules|OR}}` is NOT used**, because we have no randomness to disclose (§3.1);
- 250-per-page pagination with explicit page items;
- per-entry icons, using the preserved-part item's own tile (so we skip vanilla's
  fake-a-liquid-icon hack entirely);
- `Popup.ShowFail` in `NO_REQUIRED`'s exact register — *"You have nothing that meets the requirement
  of the ..."* — when a slot has no legal candidate;
- the **three-way consent prompt** from Trophic Absorption: *"Have it done." / "Not now." /
  "Never offer this again."*, the third writing to a permanent exclusion list;
- **the static duplicate-open guard and the key-bleed debounce, copied from `ControlMenu.cs`.** Both
  were expensive to learn and are already solved.

And one thing that is ours, because the golem does not need it and we do:

- **Commissioning is not clicking.** Choosing a procedure **stakes** it, the way a building is
  staked. Crews work it over world-days; the hall says so; you can walk away and come home to it
  done. That is the whole mod's grammar, and the lab may not be the one place that breaks it.

**The removal verb.** Because every graft carries `Manager = "TAF::Lab::<key>"`,
`Body.RemovePartsByManager` undoes it in one call. Removal is offered, costs less than the graft,
returns nothing, and is chronicled. This is the consent story — **nothing the lab does to you is
permanent against your will** — and it is also the escape hatch for the Playable Golem failure mode
(§3.0c): if a graft locks you out of content, you can have it taken off.

### 3.9 Honest scope

Estimated against the real precedents, which is the point of §3.0.

| Piece | Reference | Estimate |
|---|---|---|
| Procedure registry, XML schema, load-time validator | mirrors `KingdomData` catalogue loading | **~400 lines**, low risk — the fourth registry of this exact shape |
| Slot judgment (anatomy read, `Slots`/`SlotCategories`, 7b refusals) | `CyberneticsScreenInstallLocation.cs:26-30` is five lines; ours adds prose | **~300 lines**, low risk |
| Grant/remove application (`part`/`limb`/`mutation`) | `GameObjectPartUnit`, `GameObjectBodyPartUnit`, `GameObjectMutationUnit` are 16-42 lines each | **~350 lines**, **medium risk** — the `SyncPufferIdentity` class of bug lives here |
| The lab record (what you have, from what, reversible) | `TrophicRepertoire.cs` is **484 lines**, and that is a mature v2 | **~500 lines**, **high risk** — versioned serialization; the author has been burned once already |
| Preservation chain (butcher stamp, vat-house work, preserved-part item) | `Campfire.Preserve` + `PreservableItem`, plus our staffed-work machinery | **~450 lines** plus blueprints, medium risk |
| The two-level UI | `ControlMenu.cs` **472 lines**; `DisplayOptions` + `Pick` about **200 lines**; Playable Golem's `Pick<T>` as reference | **~550 lines**, **medium-high risk** — but the two non-obvious hazards are already solved and can be copied |
| Creed friction (tags, petition, standing) | all existing surfaces | **~200 lines**, low risk |
| Catalogue entries, four named procedures, blueprints | data | **~600 lines XML** |
| Tests (STANDARDS: >80% on pure logic) | | **~1,200 lines** |
| **Total** | | **about 2,750 lines C#, 600 XML, 1,200 test — a full wave, not a feature** |

**Four named risks:**

1. **Serialization is what will hurt.** `TrophicRepertoire`'s own comment is the warning. Our record
   must ship with the magic-header versioned format **from version 1**. Addendum 9 waives save
   compatibility pre-release, which buys exactly one free mistake.
2. **Part-copy fidelity is not uniform.** `DeepCopy` carries field state; `AddMutation` by name does
   not. Trophic Absorption needed a bespoke fixup for one mutation's `PuffObject` field. Expect
   several; budget a per-procedure `Fixup` hook in the schema from day one.
3. **The balance failure mode is documented, by the same author, in the same genre.** Playable
   Slime's own to-do file records that a free repeatable absorb verb *"makes all fighting styles ...
   unnecessary"*, and its Workshop reception is dominated by "overpowered". The lab's defences are:
   anatomy slots are finite; procedures cost water, bits, staff-days and a preserved part; mutation
   grants are capped at level 1-3; Class III sits behind `arclight`. **The sub-adventuring invariant
   must be re-checked against a fully-grafted player specifically, in `balance-sim.py`, before this
   ships.**
4. **Changing a body can lock a player out of content that has nothing to do with us.** Playable
   Golem's dominant complaint is that Gigantic golems cannot equip most gear or enter the Spindle.
   Every Class-III procedure needs an explicit answer to *"what does this stop you doing?"*, stated
   in the option text before commitment, and the removal verb must be reachable without the lab
   (a field surgeon, a Nook, anything) in case the graft is what stranded you.

---

## 4. Exotic buildings portfolio

Eight candidates. Each is scored honestly against the mesh condition: a **rendering** builds nothing
parallel and is preferred; **new machinery** must earn its place by adding a verb nothing else can
add. **Two** of the eight are new machinery that earns it, **two** are new machinery deferred behind
unbuilt prerequisites, and **four** are renderings.

The pillar column names which of `VISION.md`'s pillars the building actually serves — not which one
it can be argued toward.

| # | Building | New verb | Pillar | Mesh verdict | Prerequisite wave |
|---|---|---|---|---|---|
| 1 | **The grafting hall + vat-house** | *augment your own body from what you brought home* | Your decisions walk around; the adventurer loop closes (lane 8) | **NEW MACHINERY — earns it.** Nothing in the mod grants a player capability, and the C1 correction ruled that a city with no content of its own is a lobby | Branch B; `arclight` for rung 3; QoL `Refuses` (Addendum 4); lodging (4b) |
| 2 | **The deep delve** | *build downward — a whole catalogue that surface ground cannot hold* | Hubris subsides (new ground to earn); Buildings are people | **NEW MACHINERY — earns it.** The stratum axis is the single largest diversity win available and the brief already promised it | WR-3 (strata-filtered plots); the carved set (§1.5) |
| 3 | **The mirror-gate** | *step from one of your cities to another* | Automation is bought — and what it buys is your attention back | **RENDERING.** `TeleporterPair` is a shipped `IPoweredPart` with `LocationKey`/`DestinationKey`/`Cooldown`; ours is a wrapper plus a power draw on the 12(g) network | Networks (12(g)); the roster wave (two cities) |
| 4 | **The reliquary** | *display what you took from the world, and be visited for it* | Everything is remembered, twice; Speak Qudish or stay silent | **RENDERING.** Rides sockets, the outsider register, and "pilgrims of the told story" — all already designed | Lane 6 (the city enters the world's story); the co-opted pilgrims idea |
| 5 | **The muster hall** | *your citizens leave with you* | Buildings are people; the adventurer loop closes | **RENDERING**, with one hard compat rule | Clever Girl compat (EC-13: never clear `PartyLeader`); the salvage-commission idea |
| 6 | **The press** | *your chronicle becomes a real book that circulates* | Everything is remembered, twice; History is contested text | **RENDERING.** `COVERAGE.md` A4 already names it | Chronicle; trade charters; A4's own open questions (persistence, save size) |
| 7 | **The stasis vault** | *leave a body somewhere safe* | Loss writes chronicles, not game-overs | **NEW MACHINERY — deferred, and narrowly.** `IDEA-018` calls it "the small good one" and it is, but see the blocker below | after the lab; needs its own ruling |
| 8 | **The assenting moot** | *a ward whose strength is how many minds consent* | Any covenant, honestly priced | **NEW MACHINERY — deferred.** Branch C rungs 3-4; leans entirely on unbuilt work | `IDEA-008/011/013`; `COVERAGE.md` A7 (Moon Stair) |

### The three worth building first, in order

**#2 — the deep delve, before anything else.** It is the only candidate that makes the *existing*
catalogue mean more rather than adding beside it. Every `Sky="yes"` design refusing to go underground
is already enforced (`StratumAccepts`, `T/Growth/KingdomZoningRules.cs:738`); every carved plot has
free enclosure because the rock is the wall; the fungal style already has its own crop and its own
`taf:damp`/`taf:dark` shape. It is diversity that costs one gate vocabulary and about a dozen
records, and it turns a one-axis city into a two-axis one.

**#3 — the mirror-gate, because it is the cheapest large reward in the document.** The mod's stated
progression spine is *"invest until it no longer needs your hands"* and *"what automation buys is
the player's attention back."* Nothing currently gives back travel time. `TeleporterPair` ships as an
`IPoweredPart` with `LocationKey`, `DestinationKey` and `Cooldown`; a gate at each end, a real charge
draw on the 12(g) network, a brownout that closes it — and the founder's two cities are one place.
It is a wrapper, it serves the named spine, and it is felt every single session.

**#1 — the lab, because the C1 correction demands content and this is the content.** But it is a
full wave (§3.9), and it should follow the two above rather than precede them.

### Notes on the rest

**#4, the reliquary.** The verb is *display*, and the machinery is sockets plus the outsider register.
A relic taken from a named place, staked in a plot, raises the register; the register already drives
pilgrims. The temptation to make it grant a bonus must be resisted — its whole point is that it is
worth building for the sentence in the chronicle. If it must carry something, `Carries="spirit:N"`
scaled by the relic's own tier, and nothing else.

**#5, the muster hall.** The strongest unexplored answer to "what does the player get" is
**companions**: real production, decisive in vanilla play, uniquely city-shaped, and barely touching
the sub-adventuring line (`IDEA-INBOX.md`'s own assessment). The hard rule from the compat audit is
non-negotiable: **citizenship and companionship are orthogonal; never clear `PartyLeader` merely
because a follower becomes a citizen** (EC-13, and the Hearthpyre conversion is a counter-example of
exactly this going wrong). A citizen who leaves with you should stop counting toward the city's
equilibrium while away, and come home into the same identity row — which the binding registry
already guarantees (one identity, at most one body).

**#6, the press.** A generated book object that circulates through the trade system is a lovely
rendering of "history is contested text" — the outsider register's version and the city's version
could both exist as physical books, and a caravan could carry the wrong one home. `COVERAGE.md` A4
flags the real risks (generated-book persistence, localization, save size); those are the gate, not
the concept.

**#7, the stasis vault — and the blocker that must be stated.** `IDEA-018`'s assessment holds:
`Domination` ships body-swap (*"while your own body lies dormant"*), `Effects/Stasis.cs` and three
stasis cybernetics ship stasis, and `Capabilities/Cloning.cs` ships
`CanBeCloned` / `GenerateClone(..., DuplicateGear, BecomesCompanion, ...)`
(`D/XRL/World/Capabilities/Cloning.cs:20,77`). What is missing is **safety**, and only a city could
supply it. But the same entry records the blocker: `CyberneticsTerminal.cs:615` requires
`!gameObject.IsInStasis()` for a valid subject, so **a vault cannot double as an operating theatre.**
The vault and the lab are two buildings, and a player cannot be worked on while stored. That is a
clean fiction and a clean rule, but it must be designed in rather than discovered.

**#8, the assenting moot.** The fiction is already right — the Coven shut the door *"by oon assent"*,
so ward strength scales with how many minds assent and every exemption granted weakens it;
exemptions are a budget, which is a genuinely novel and genuinely Qud-shaped mechanic. It also has a
shipped veto event with zero vanilla consumers (`AmbientRealityStabilized.Apply` aborts if a handler
returns false) to hang the exemption on. But it depends on `IDEA-008/011/013` and A7, all unbuilt,
and Branch C's rung-3/4 asymmetry (the Moon Stair grants mental mutations to mutants only). **Defer,
and do not promise it in the same breath as #1-#3.**

### What was considered and cut

- **A market hall that sets prices.** A price-setting screen is a numbers panel. The existing shop
  tiering plus the posted price already covers the intent.
- **A barracks that trains soldiers.** Per-unit management is the definitional second job.
- **A hospital that heals the player.** Vanilla already has healing everywhere; a building that
  duplicates a tonic adds nothing and costs a plot.
- **A second megastructure.** The cardinality ruling stands and the burden of proof is on the design;
  none of the eight clears it. The arcology remains the city's singular end-state.

---

## 5. Loud rejections

Stated as loudly as the brief asks for, so that nobody has to re-derive them.

**R1 — A per-creed, per-genotype, or per-species building CATALOGUE. Refused on arithmetic.**
40 (Category x Plot) sets, of which **15 are empty today**. Multiply by 33 admissible creeds and you
have 1,320 sets to fill, against a 63-design catalogue. Multiply by 98 species and it is 3,920. All
three sets are **open** — mods add factions, genotypes and species — so any enumeration is wrong on
arrival. Variation is FILTER and SHADE (§1.4). The three exceptions are ground-based, not
builder-based, and are named in §1.5.

**R2 — A research SCREEN, in any costume. Refused on the mod's own ruling.**
*"This mod has no research tree and does not want one — a tree is a second job, and the founder
already has one"* (`T/Growth/KingdomZoningRules.cs:9-10`), and
`EVOLVING-HEART-RESEARCH.md:970-974` already rejected it once by name. §2's tree is a **map**: it
renders the DAG the catalogue already contains and nothing on it can be pressed. If any proposal
downstream of this document has a queue, a budget, a percentage, or a timer, it is this rejection
being smuggled back in.

**R3 — Making rites, savants, cultures or species worth TECH POINTS. Refused, specifically.**
The temptation is obvious and the source already refused its sibling: `TechPointsPerOrigin = 0`,
because counting people *"would turn that readout into a population count"*
(`T/Growth/KingdomZoningRules.cs:220-225`). The same argument kills the rest. Rites, savants,
cultures and species mint **knowledge keys** (which open specific designs) and never **points**
(which raise the rung). Keep `TechLevel` exactly as it is: disks and certifications, nothing else.

**R4 — Absorption as a repeatable free verb. Refused, with the receipt.**
Playable Slime's own author wrote it down: a free scaling absorb verb *"makes all fighting styles
but Spamming statpoints into only toughness and using nothing but engulf for attacks unnecessary"*,
and the mod's Workshop reception is dominated by "overpowered". Every procedure in §3 costs water,
bits, staff-days, and a preserved part; anatomy slots are a finite budget; mutation grants are capped
at level 1-3; Class III sits behind `arclight`. **This is the single most important constraint in
section 3 and it is not negotiable on grounds of "it would be fun".**

**R5 — Randomness in a procedure that cost a season. Refused.**
The golem applies **one of its listed effects at random** (`GolemMaterialSelection.cs:209`), and the
documented community consequence is that players treat the Atzmus as a lottery and consult an
external guide because the payoff is opaque at the point of choosing. We inherit the *picker*, not
the *dice*. The one exception is named, priced as a gamble, and called The Chimeric Confession.

**R6 — Inventing rot to justify the preservation chain. Refused.**
Vanilla has no decay of any kind — verified twice. A spoilage timer would break Addendum 8 clause 2
(*rates are time x labour x infrastructure, never time alone*) and would be a chore in robes. The
preservation gate is **labour and a building**, not a clock. The only expiry in the design is
vanilla's own: `Temporary.CarryOver` on dismemberment means a summoned creature's limb is itself
temporary (`D/.../Body.cs:2596`).

**R7 — A "revulsion" or "acceptance" meter for creed friction. Refused.**
Addendum 4's pillar guard is explicit: *placement constraints, never meters*. Friction is `Refuses`
tags, the fault-line ceiling, named petitions, and standing — all of which already ship or are
already designed. A number would be a numbers panel and a penalty-for-abstaining at once.

**R8 — Granting cybernetic eligibility to mutants because "the lab could". Refused here, and left
where C2 left it.**
The C2 correction withdrew the precedent claim and ruled that this *"is a genuinely new thing and
must be argued on its merits."* This document does not argue it and does not sneak it in: the lab
grants flesh. Note also that `GetCyberneticRejectionSyndromeChance` unconditionally returns 0 in this
build (`D/.../CyberneticsBaseItem.cs:234`) — a fact about the substrate, not a permission.

**R9 — Splitting the catalogue by city `Style` before any design uses `Styles` at all. Refused as
premature.**
All 63 designs are `Styles="all"`. The style machinery is complete, tested, and load-validated. The
correct first move is **data**: give a handful of designs real style restrictions and see whether
five city styles feel different. Building a second axis on top of an unused first one is how a mod
gets two half-systems.

**R10 — A second megastructure to host the lab. Refused under the standing cardinality ruling.**
One megastructure per city; the arcology is the end-state; the door opens only with proof, and the
burden is on the design. The chimeric theatre is XL, which is a plot size, not a megastructure.

---

## 6. Open questions for the author

Ordered by how much downstream work each unblocks.

**Q1 — Does "technology tree" mean a MAP, or did you mean something with choices to make?**
Section 2 resolves the mandate against the standing no-research-tree ruling by making the tree a
**readable map with no interaction** — all divergence comes from where you went and who you took in.
If you actually want the founder to *choose* a branch at a surface, that is a different design and a
re-ruling of `KingdomZoningRules.cs:9-10`. Everything in §2 depends on this answer.

**Q2 — Deep, sky, and arcology: separate SETS or filtered subsets?**
You asked this yourself in the backlog. §1.5 recommends **separate sets for deep and arcology,
filtered subset for sky**, on the grounds that deep and arcology change what a building can
physically be while sky only changes where it stands. Confirm or overrule; the WR-3 wave sizing
depends on it.

**Q3 — Do we exercise `Styles` first?**
All 63 designs are `Styles="all"`. Do you want a data pass giving the five city styles real
restrictions (cheap, immediate, no code) before any new diversity axis is built? §5's R9 says yes.

**Q4 — Where the mandate says "races", may we read `GetCulture()`?**
It is vanilla, mod-extensible, never null, and already names 33 peoples (Barathrumite, Templar,
Hindren, Mopango, Naphtaali, Issachari, Goatfolk, Ooze, Robot, Sightless Way, Eater, Star, ...).
The alternative is `GetSpecies()` (98 values, more granular, less meaningful). Or both, as two
knowledge kinds.

**Q5 — Is `rite:` legitimate?** It reads the FOUNDER's own vanilla water-ritual grants and mints a
permanent city knowledge key from them. It is the cleanest source of per-creed tech divergence in
the whole document (§2.5) — and it is also the founder's personal history becoming the city's
capability, which may be exactly right or may be a step too far toward the founder-as-protagonist.

**Q6 — Is the chimeric theatre allowed to be XL, or does it contend with the arcology?**
§3.3 assumes XL-but-not-a-megastructure. If an endgame body-modification facility should be a
*specialised end-state megastructure* — one of the cases your cardinality ruling left the door open
for — that changes its cost, its cardinality, and whether a city can have both it and an arcology.

**Q7 — Does the lab ever touch chrome?**
§3.2 deliberately leaves the C2 question closed: the lab grants flesh only. A "becoming annexe" that
grants cybernetic eligibility to a mutant is the single most requested thing in this space and the
single least justified by precedent. Your call, on its merits, as C2 required.

**Q8 — How many named procedures, and are these the right four?**
§3.7 proposes The Weeping Graft, The Chimeric Confession, The Cold Regard, The Lantern Rib. Each maps
to real machinery and a real creature. More is more balance surface; fewer is less endgame.

**Q9 — Order of the three build-first exotics.**
§4 recommends deep delve, then mirror-gate, then the lab — on the grounds that the first makes the
existing catalogue mean more, the second is the cheapest large reward, and the third is a full wave.
The lab is the one you endorsed in principle, so if you want it first, say so and the other two
reschedule.

**Q10 — Housekeeping, not design:** `M/CreatureControl/Control Menu/ControlMenu.cs` still ships an
active block marked `// TEMP-DIAG (remove before Steam upload)` that logs on every menu open. It went
out in the 17 Aug 2026 update.

---

## Appendix — what was read

**Doctrine, end to end:** `_notes/BUILDING-CATALOGUE-BRIEF.md` (906 lines: the plot/materials/
lifecycle core and Addenda 1-13, megastructure cardinality, the heart ruling);
`_notes/VANILLA-PRODUCTION-TRUTH.md`; `VISION.md`; `_notes/IDEA-INBOX.md` (index, corrections C1/C2,
entries 010-021a, open rulings); `_notes/COVERAGE.md` (A1-A7, X1-X5);
`_notes/EVOLVING-HEART-RESEARCH.md` (TechLevel and research-tree findings);
`_notes/ECOSYSTEM-COMPAT-AUDIT.md` (the installed stack, Clever Girl and Creature Control rows).

**Mod source:** `T/Growth/KingdomZoningRules.cs`, `T/Growth/KingdomZoning.cs`,
`T/Core/KingdomCreedRules.cs`, `T/Core/KingdomCreed.cs`, `T/Core/KingdomQolRules.cs`,
`T/Core/KingdomQol.cs`, `T/Core/KingdomRules.cs`, `T/Core/KingdomData.cs`,
`T/Growth/KingdomCatalogueRules.cs`, `T/Growth/KingdomMaterialRules.cs`,
`T/Growth/KingdomCropRules.cs`, `T/KingdomBuildings.xml` (header and all 63 records).

**Decompile:** `D/XRL/GenotypeEntry.cs`, `GenotypeFactory.cs`, `SubtypeEntry.cs`,
`SubtypeFactory.cs`, `MutationCategory.cs`, `MutationEntry.cs`, `MutationFactory.cs`,
`XRL/World/Faction.cs`, `Factions.cs`, `XRL/World/GameObject.cs` (genotype/species/culture accessors),
`XRL/World/Anatomy/*` (`BodyPart.cs`, `Anatomies.cs`, `BodyPartCategory.cs`),
`XRL/World/Parts/Body.cs`, `Mutations.cs`, `DismemberedProperties.cs`, `PreservableItem.cs`,
`Campfire.cs`, `Butcherable.cs`, `Corpse.cs`, `CyberneticsBaseItem.cs`, `TeleporterPair.cs`,
`XRL/UI/CyberneticsTerminal.cs`, `CyberneticsScreenInstall*.cs`,
`XRL/World/Quests/GolemQuest/*` (all 11 files), `XRL/World/Units/*`,
`XRL/World/Capabilities/Cloning.cs`.

**Game data:** `B/Genotypes.xml`, `B/Subtypes.xml`, `B/Mutations.xml`, `B/HiddenMutations.xml`,
`B/Bodies.xml`, `B/Factions.xml` (83 factions, 43 water rituals, parsed), `B/ChiliadFactions.xml`,
`B/Quests.xml`, `B/Conversations.xml`, `B/ObjectBlueprints/*.xml` (Species/Culture tag census).

**Mods, full source:** `W/3388408799/` Playable Slime 1.0 (25 files, 4,992 lines, incl.
`FutureIdeas.txt`); `M/CreatureControl/` Creature Control 0.4.0 (20 files, 2,880 lines, incl.
`AbsorbableParts.txt`); `W/3270006554/` Playable Golem (3 files, 1,320 lines). Workshop reception for
all three is second-hand from comment and discussion tabs and is marked as such throughout.

**Not found, and stated so:** no GitHub repository for Playable Slime 1.0 or Creature Control; no
substantive Reddit discussion of either; **no community complaints about the golem picker being
tedious to navigate** — the documented complaint is consequence-legibility at the point of choosing,
which §3.8 answers directly.
