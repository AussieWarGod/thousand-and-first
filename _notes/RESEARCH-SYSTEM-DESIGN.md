# The research system, designed

> **Mandate.** `_notes/BUILDING-CATALOGUE-BRIEF.md` Addendum 14 (2026-08-22), the author's answer
> to ideation Q1: *"The tech system is a REAL TIERED RESEARCH SYSTEM, not a read-only map — but in
> vanilla's own learning idioms: 'similar feel to the way blueprints/data disks/psychometry
> work.'"* With it: the visibility law, sources varying by creed/genotype/race, quest locks, tiers
> gated by researcher INTELLIGENCE, reach into buildings/worker efficiency/citizen stat caps,
> wild high-stat recruits with expectations, and a modder-extensible surface for research features
> AND research requirements.

## Provenance and evidence standard

Two kinds of claim appear below and they are marked differently.

- **`D/…`** — a local decompile of the pinned Caves of Qud 2.0.211.51 assembly, cited
  `file:line`. Its filesystem location is contributor-specific. Every
  vanilla mechanic asserted here was read there. Where a claim is *inference* from the code rather
  than a quotation, it says so.
- **`T/…`** — this repository root, cited `file:line`.

Design proposals carry no citation because they are not claims about the world. Numbers are
illustrative until `_notes/balance-sim.py` has run them.

Prior groundwork this document builds on and, where necessary, overrules:
`_notes/DIVERSITY-AND-TECH-TREES.md` §2 (the invisible DAG; the artifacts-and-people acquisition
shape; the three worked branches), `_notes/BUILDING-CATALOGUE-BRIEF.md` Addenda 8/10/11/12(i)/13,
`_notes/LIVING-CITY-ARCHITECTURE.md` §0.0 and §6.6, `_notes/EVOLVING-HEART-RESEARCH.md` §4.

---

## 0. The verdicts, up front

1. **Nothing in the shipped gate vocabulary changes.** A research node's whole effect is that it
   *mints a roster key* — the same `disk:`/`machine:`/`origin:` strings `KingdomZoningRules.Knows`
   already matches (`T/Growth/KingdomZoningRules.cs:443-471`). All 63 catalogue designs, and every
   third-party design ever written against `Knowledge=`, keep working untouched. **The research
   system is a new source of keys, not a parallel gate system.** This is the single most important
   structural decision in the document.
2. **Research is a scaffold for an idea.** The lab does not fill a meter and does not mint a
   currency. It is a work that accrues *labour ticks against one named subject*, on exactly the
   machinery `r_KingdomScaffold` already uses to raise a building
   (`T/Growth/KingdomScaffold.cs:19-33`): banked once, charged per elapsed stretch at the crew's
   own effectiveness, **idle time spent and never banked**. An unstaffed lab produces nothing and
   says so once (Addendum 8 clause 2; STANDARDS 7b).
3. **The map stays a reading surface. The one pressable thing is a building.** `KingdomTechMap`'s
   no-press rule (`T/Core/KingdomTechMapRules.cs:50-56`) survives intact. Setting the current
   subject happens *at the lab, in the world*, which is where every other decision in this mod is
   made. This is how Addendum 14 is honoured without re-opening the "second job" wound.
4. **The visibility law is vanilla's own.** Qud already models "the player has learned a fact" as a
   revealable journal entry, and already hides unknown tinker recipes by simply not listing them.
   We copy the idiom rather than invent one. See §6.
5. **Two ladders, both already substrated.** `TechLevel` (craft: what the keepers can build at) is
   unchanged — disks and certifications, nothing else (`T/Growth/KingdomZoningRules.cs:216-230`),
   which preserves DIVERSITY §5 R3. **Research TIER is a second, orthogonal ladder gated on the
   best researcher's Intelligence**, read off real settlers by the shipped crew machinery
   (`T/Growth/KingdomCrewRules.cs:36-60`, `KingdomCrews.CapabilityOf`). A node may demand both.
6. **The three worked branches survive contact**, re-aimed: foundry keeps the material spine, flesh
   gains the citizen-stat lane, and choir gains *teaching* — which is the honest carrier for both
   worker efficiency and the citizen Intelligence cap, and therefore the loop that makes research
   feed research. A shared 3-node trunk is added because the visibility law needs a place to start.
7. **`KingdomTechMap.Locked` as shipped violates the visibility law and must change.** It
   enumerates every locked design by name with its missing gates, and tails with a count over the
   *whole* locked set (`T/Core/KingdomTechMap.cs:137-190`). Under Addendum 14 both the rows and the
   count must be computed over the DISCOVERED set only. This is a behavioural regression to the
   founder's convenience and it is the correct one.
8. **Wild high-stat recruits mint nothing.** v1 recruits creatures the world already made. The
   design is discovery + terms + the existing QoL/lodging refusal law, not a spawner.
9. **`rite:` is a SEED, never a ceiling** (Addendum 18). A rite key REVEALS its branch and BEGINS
   its head node with a head-start fraction; it can never finish a node and can never skip a tier,
   because the tier is checked against the CITY's researchers and not the founder. A `rite:` token
   in `TaughtBy` is a load-time schema error, not a convention. §5.5b.
10. **The identity tuple is a first-class input** (Addendum 17): `culture:` for what a people
   KNOWS and `species:` for what a body IS, as two distinct roster kinds beside `creed:` and
   `genotype:`. It enters at four existing readers — node prerequisites, worker efficiency, crew
   capability, QoL/anatomy — and at no fifth. It may make a node reachable, cheaper, or faster;
   it may **never** make a tier skippable. §7.
11. **`Statistic.Max` must never be written for a citizen cap.** `_Max` is a static dictionary of
   boxed ints keyed by stat NAME (`D/XRL/World/Statistic.cs:142-196`) — one write changes the
   ceiling for every creature in Qud, the player included. The citizen ceiling is ours, enforced
   in our own training code, touching vanilla only through `BaseValue`. §8.3, RR8.

---

## 1. What Addendum 14 changes, stated before anything is built on it

Three shipped statements are in tension with the mandate, and the tension must be closed on the
record rather than walked past.

| Standing text | What Addendum 14 does to it |
|---|---|
| *"This mod has no research tree and does not want one — a tree is a second job, and the founder already has one."* (`T/Growth/KingdomZoningRules.cs:9-10`, a doc comment on shipped code) | **Superseded in letter.** The author was asked the question directly (DIVERSITY §6 Q1) and answered "a real tiered research system". The doc comment must be rewritten in the same commit that lands the first node, or the source lies about the design. |
| DIVERSITY §5 **R2** — "a research SCREEN, in any costume. Refused." | **Superseded in letter, retained in reason.** The reason was *"a queue, a budget, a percentage, or a timer"*. §5 below discharges all four: no currency to allocate, one subject at a time, prose distance instead of a percentage, and labour rather than a clock. |
| `EVOLVING-HEART-RESEARCH.md` §4.1 — "a research tree gating the heart tiers … a pillar violation regardless of how good the ladder is." | **Narrowed, not superseded.** The heart's own rungs stay gated on `workshop`/`foundry`/`arclight` as that document ruled (`EVOLVING-HEART-RESEARCH.md:814`). Research nodes never gate the heart directly; they gate the *works* whose raising moves the heart. |
| DIVERSITY §6 **Q4** — "may we read `GetCulture()`?" and **Q5** — "is `rite:` legitimate?" | **Answered** by Addenda 17 and 18 respectively, mid-draft. Both accessors, as two knowledge kinds (§7); `rite:` legitimate as **seed, not ceiling** (§5.5b). |

**What survives untouched from DIVERSITY §2, and is the substrate here:**

- The DAG is real, enforced, and ordered — `RefusedUnlearned → RefusedTechLevel → RefusedTerritory
  → RefusedStratum → RefusedDistrict` (`T/Growth/KingdomZoningRules.cs:26-49`) — and every refusal
  already carries the 7b sentence naming what would fix it.
- Acquisition is **artifacts and people**: disks taught (`T/Growth/KingdomZoning.cs:520-545`, and
  note the deliberate divergence from vanilla — *"The disk is not consumed"*, `:516-519`), machines
  certified (`T/Growth/KingdomZoning.cs:173-186`), peoples arriving (`:104-124`), savants lodged
  (`T/Experience/KingdomGuestRules.cs`), rites learned.
- The per-creed divergence table is **vanilla's, already written**: 43 `<waterritual>` entries in
  `B/Factions.xml` grant materially different things — Barathrumites grant `Blueprints="1d3"`,
  Templar grant a cybernetics credit wedge, Oozes grant a mutation, Seekers grant a random mental
  mutation, Chavvah grant a **genotype-conditional** recipe (`RecipeGenotype="!True Kin"`). We read
  it; we do not write it.

**What changes.** DIVERSITY §2 concluded *"the tree becomes a MAP the founder reads, never a screen
the founder spends into"*, and §2.8 rendering 2 explicitly proposed **naming the nearest locked
things and why**. Addendum 14's visibility law forbids exactly that. The map survives; rendering 2
inverts (§6).

---

## 2. The vanilla substrate, read

Everything in this section was read in the decompile. Paths are relative to
`/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/` (`D/`) and the sibling data
directory `/home/r/coq/qud_helper/game_base/Base/` (`B/`) — note the game XML is **not** under the
decompile tree, which is worth recording because two prior notes assumed it was.

### 2.1 Tinkering: what a recipe is, and where "known" lives

`TinkerData` (`D/XRL/World/Tinkering/TinkerData.cs:14-51`) is a flat record —
`DisplayName`, `Blueprint`, `Category`, `Type` (`"Build"`/`"Mod"`), `int Tier`, `Cost` (a bit
string like `"0345"`), `Ingredient`. `PartName` is computed, not stored (`:56-66`).

The master list is derived, never authored as a list: `TinkerData.TinkerRecipes` (`:194-228`) walks
every `GameObjectBlueprint` through `TinkerItem.LoadBlueprint` (`D/XRL/World/Parts/TinkerItem.cs:201-237`)
and every `ModEntry` in `B/Mods.xml`. A blueprint qualifies only if it has a `TinkerItem` part with
`CanBuild="true"` and lacks the `BaseObject`/`NoDataDisk` tags. **`Tier` defaults to the highest
bit-level in the cost string** and is overridable by an XML `BuildTier` parameter (`:222-231`).

Known recipes are a **process-global mutable static list** — not a part, not a player field:

```csharp
// D/XRL/World/Tinkering/TinkerData.cs:39-43
[GameBasedStaticCache(true, false, ClearInstance = true)]
public static List<TinkerData> _TinkerRecipes = new List<TinkerData>();
[GameBasedStaticCache(true, false)]
public static List<TinkerData> KnownRecipes = new List<TinkerData>();
```

serialised through `TinkerItem.SaveGlobals`/`LoadGlobals`
(`D/XRL/World/Parts/TinkerItem.cs:239-258`). Membership tests are
`TinkerData.RecipeKnown(data)` (`:369`, by `Blueprint` string) and `data.Known()` (`:293`).

**There is no learn event.** Verified by exhaustive grep: no `LearnRecipeEvent`,
`CanLearnRecipeEvent`, `AfterLearnEvent`, `IsBlueprintKnown`. Every one of the seven learn sites
mutates the list with a bare `.Add()` — `DataDisk.cs:229`, `Psychometry.cs:207`,
`Disassembly.cs:383/387`, `WaterRitualTinkeringRecipe.cs:76`, `CyberneticsSchemasoft.cs:70`,
`TinkerData.cs:355/365`, `Wishing.cs:1971`. **Design consequence: we must not build our knowledge
ledger on top of `KnownRecipes`.** Our roster is our own store (it already is —
`The.Game.GetStringGameState` at `T/Growth/KingdomZoning.cs:545-553`) and we read vanilla's list
only to answer "does the founder personally know this", never to hold city state.

### 2.2 The data disk: the learn verb and its exact gate

`D/XRL/World/Parts/DataDisk.cs`. Blueprints in `B/ObjectBlueprints/Items.xml:10273-10312+`:
`DataDisk`, `BuildDataDisk`, `ModDataDisk`, `TinkerTier1DataDisk` … `TinkerTier6DataDisk`.

The action is offered only when the *disk itself* is understood (`:187-196`), and the learn path
has exactly two refusals:

```csharp
// D/XRL/World/Parts/DataDisk.cs:204-211
if (TinkerData.RecipeKnown(Data))
{ E.Actor.Fail("You already know that recipe!"); }
else if (!E.Actor.HasSkill(GetRequiredSkill()))
{ E.Actor.Fail("You don't have the required skill: " + GetRequiredSkillHumanReadable() + "!"); }
```

```csharp
// D/XRL/World/Parts/DataDisk.cs:437-448
public static string GetRequiredSkill(int Tier)
{
    if (Tier <= 3) return "Tinkering_Tinker1";
    if (Tier <= 6) return "Tinkering_Tinker2";
    return "Tinkering_Tinker3";
}
```

**The tier gate IS the skill gate. There is no Intelligence check at learn time.** Learning also
succeeds regardless of INT — see §2.3. And the disk is destroyed (`:230`), which is exactly the
divergence our own teaching path already documents and deliberately rejects
(`T/Growth/KingdomZoning.cs:516-519`).

One more precedent worth naming: **a data disk hides its own contents from an unskilled reader.**

```csharp
// D/XRL/World/Parts/DataDisk.cs:120
if (E.AsIfKnown || (E.Understood() && The.Player != null && (The.Player.HasSkill("Tinkering") || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech))))
```

Without Tinkering or tech Scanning it reads as "data disk" and nothing more. That is vanilla
gating *the description of a knowledge source* on the reader's competence — directly reusable for
treatises (§5.5).

### 2.3 Where Intelligence actually gates, in vanilla

`B/Skills.xml:241-252` is the entire Tinkering tree. The attribute minima:

| Class | Name | SP | INT minimum |
|---|---|---|---|
| `Tinkering_GadgetInspector` | Gadget Inspector | 0 | 15 |
| `Tinkering_Disassemble` | Disassemble | 100 | 15 |
| `Tinkering_Repair` | Repair | 100 | 17 |
| `Tinkering_ReverseEngineer` | Reverse Engineer | 100 | **25** |
| `Tinkering_Tinker1` | Tinker I | 100 | **19** |
| `Tinkering_Tinker2` | Tinker II | 200 | **23** |
| `Tinkering_Tinker3` | Tinker III | 300 | **29** |

Enforced at *purchase*, against `BaseStat`, not the boosted value:

```csharp
// D/XRL/World/Skills/PowerEntryRequirement.cs:26
if (Object.BaseStat(Attributes[i]) < Minimums[i])
```

**This is the exact shape Addendum 14 asks for, and it is worth stating plainly: vanilla's tiered
learning system gates the TIER on a hard Intelligence threshold, and then never checks
Intelligence again at the moment of learning.** Our tier ladder (§4.0) is the same shape: hard at
the boundary, and above it Intelligence only buys speed. Note also that vanilla reads `BaseStat`,
which means a temporary boost cannot buy a tier — a discipline we should copy so a
willpower-potion economy cannot appear around research.

There is no `EffectiveTinkerLevel` and no `GetTinkerTier` in the assembly. The one place INT feeds
tinkering arithmetic is identification:

```csharp
// D/XRL/World/Parts/Skill/Tinkering.cs:70-83
if (who.HasSkill("Tinkering_GadgetInspector") && !who.HasPart<Dystechnia>())
{
    num += 3;
    if (who.HasSkill("Tinkering_Tinker2")) num += 2;
    if (who.HasSkill("Tinkering_Tinker3")) num++;
    int bonus = num * (who.StatMod("Intelligence") * 3) / 100;
    num += GetTinkeringBonusEvent.GetFor(who, null, "Inspect", num, bonus);
}
```

`base × (IntMod × 3) / 100` — a **percentage of a base**, not a flat add. Our tier bonus (§5.3)
follows the same shape for the same reason: it scales the work rather than replacing it.

### 2.4 Identification: the four-layer hiding law, and the epistemic ladder

`D/XRL/World/Parts/Examiner.cs` is the richest precedent in the game for the visibility law, and
it is *not* the tinkering screen. Three states:

```csharp
// D/XRL/World/Parts/Examiner.cs:22-28
public const int EPISTEMIC_STATUS_UNINITIALIZED = -1;
public const int EPISTEMIC_STATUS_UNKNOWN = 0;
public const int EPISTEMIC_STATUS_PARTIAL = 1;
public const int EPISTEMIC_STATUS_KNOWN = 2;
```

`Understanding` is **not** on `Physics` — it is a global blueprint→int table
(`Examiner.UnderstandingTable`, `:68-70`), proxied per-object (`:169-179`), and **monotonic**:
`SetUnderstanding` only ever raises (`:235-256`). Status derives from understanding vs complexity:

```csharp
// D/XRL/World/Parts/Examiner.cs:622-645
public static int GetAppropriateEpistemicStatus(int Understanding, int Complexity, GameObject Object = null)
{
    if (Understanding >= Complexity) return 2;
    ...
    if (Understanding > 0) return 1;
    if (GameObject.Validate(ref Object) && Object.HasProperty("PartiallyUnderstood")) return 1;
    return 0;
}
```

Unknown things are hidden **four ways at once**, and this is the pattern to copy:

1. **Name** — `GetDisplayNameEvent` swaps in a *decoy blueprint's* name (`:341-348`,
   `GetActiveSample` `:1071-1079`). The decoys are real blueprints in `B/ObjectBlueprints/Data.xml:27-56`:
   `BaseUnknown` (`DisplayName="weird artifact"`, `RenderString="*"`), `UnknownOddTrinket`,
   `UnknownStrangeTubes`, `UnknownBizarreContraption`, `UnknownStrangeFurniture`. **The `*` is
   literally an authored decoy object, not a formatting rule.**
2. **Description** — `D/XRL/World/Parts/Description.cs:81-105` swaps in the decoy's short text.
3. **Tile and colour** — `EpistemicDisguise` applied in `SetEpistemicStatus` (`:661-678`).
4. **Actions** — every inventory action but `Examine` is suppressed:

```csharp
// D/XRL/World/Parts/Examiner.cs:389-395
if (!ParentObject.Understood(this))
{
    E.AddAction("Examine", "examine", "Examine", null, 'x');
    return false;
}
```
plus `CanSmartUseEvent` → false (`:503-510`), category forced to `"Artifacts"` (`:494-501`), and
value cut to 10%/20% (`:359-370`).

The examine roll itself: `E.Actor.Stat("Intelligence") - Difficulty` (`:413`), plus
`GetTinkeringBonusEvent("Inspect")` (`:420`), rolled through `Stat.RollResult` (`:456`), with
`result > Understanding` producing a *partial* (`:480`) — the "you think it's probably a variety of
X" copy at `:848`.

**This is a knowledge system with a middle state, and it is the one the mandate's visibility law
wants.** Three states, not two: unknown, glimpsed, held.

### 2.5 Psychometry: the exact read

`D/XRL/World/Parts/Mutation/Psychometry.cs`, `B/Mutations.xml:82`
(`Cost="3" MaxSelected="1" Exclusions="Dystechnia"`).

```csharp
// D/XRL/World/Parts/Mutation/Psychometry.cs:249-262
public static int GetIdentifiableComplexity(int Level) { return 4 + Level / 2; }
public static int GetLearnableComplexity(int Level)    { return 2 + (Level - 1) / 2; }
```

**Corrections to the brief's assumptions, on the record:**

- **There is no Ego gate.** `Stat("Ego")` does not appear in the file. The gate is the mutation
  **Level** through those two formulas, plus `Complexity > 0` for the action to be offered at all.
- **There is no cooldown.** `Mutate` adds the ability with no cooldown argument (`:290-298`); the
  defensive cooldown checks in `Activate` (`:323-360`) never fire from this path.
- Learning by psychometry **costs no energy** — `UseEnergy` is only on the identify branch
  (`:173`).
- It is **reality-distortion based** (`:19`), so a normality field vetoes it (`:346-357`).

The two verbs are separate and beautifully chosen, and they are the model for §6's two states:

```csharp
// D/XRL/World/Parts/Mutation/Psychometry.cs:110, 140
E.AddAction("Psychometry", "read history with Psychometry", ...);        // unidentified → identify
E.AddAction("Psychometry", "read early history with Psychometry", ...);  // identified   → learn to build
```

The learn branch requires the base `Tinkering` skill but **not** the Tinker I/II/III tier skill
(`:113`, `:189-207`), and per-blueprint repetition is blocked by `LearnedBlueprints` (`:21, 195`)
even if the recipe is later unlearned.

**What we take:** the *shape* — touching a real artifact converts to knowledge, gated on a
capability the toucher has, with complexity deciding how far the read gets. **What we do not
take:** a free, repeatable, city-wide verb. §5.5's `machine:` arm advances a node by half and
reveals it; it does not complete it.

### 2.6 Reverse Engineer: the chance, and a shipped documentation bug

The skill class is a 25-line stub that only adds `E.Add("scholarship", 2)`
(`D/XRL/World/Parts/Skill/Tinkering_ReverseEngineer.cs`). All logic is in
`D/XRL/World/Tinkering/Disassembly.cs`. Candidates are gathered **only from unknown recipes**
(`:162-205`) — one Build recipe (the item's own blueprint) plus every unknown Mod whose `PartName`
the object actually has.

```csharp
// D/XRL/World/Tinkering/Disassembly.cs:206-217
int chance = 0;
int num2 = 0;
if (tinkerData != null || (list != null && list.Count > 0))
{
    chance = 15;
    num2 = GetTinkeringBonusEvent.GetFor(player, Object, "ReverseEngineer", chance, num2, ref Interrupt);
    chance += num2;
}
```

**Base 15%, rolled ONCE for the whole batch** (`:341-345`) — build recipe and all mods together.
`B/Skills.xml:244` advertises *"25% chance … you also have a 15% chance per mod"*. **The shipped
description does not match the shipped code.** Recorded here because it is exactly the class of
divergence our own STANDARDS 7b prose is supposed to never have, and because anyone balancing
against the description would be balancing against fiction.

### 2.7 Books that teach

| Part | File | What reading grants |
|---|---|---|
| `Book` | `D/XRL/World/Parts/Book.cs` | nothing mechanical; text from `B/Books.xml` by `ID`; read-once via `The.Game.GetStringGameState("AlreadyRead_" + ID)` (`:65-70`); fires `AfterReadBookEvent` (`:54`) |
| `Cookbook` | `D/XRL/World/Parts/Cookbook.cs` | **per page turned** — `readPage[]` gates `CookingGameState.LearnRecipe(recipes[p])` (`:110-122`) |
| `TrainingBook` | `D/XRL/World/Parts/TrainingBook.cs` | **the only part that grants a SKILL or a permanent stat by reading** — `E.Actor.GetStat(Attribute).BaseValue++` (`:114`) and `E.Actor.AddSkill(value.Class, ParentObject, "TrainingBook")` (`:145`); read-once via a **per-actor int property** `"HasReadBook_" + BookID` (`:167-175`) |
| `MarkovBook` | `D/XRL/World/Parts/MarkovBook.cs` | procedural flavour only |

There is **no `SkillBook` and no `Tome` class**; `TrainingBook` is it. Sheba Hagadias
(`D/XRL/World/Parts/MechanimistLibrarian.cs`, forced `Intelligence >= 27` at `:19-22`) *takes*
books through `LibrarianGiveBook` (`D/XRL/World/Conversations/Parts/LibrarianGiveBook.cs:28-55`);
**she teaches nothing.** The library is a turn-in, not a research source — worth knowing before
anyone designs "our own Sheba".

`Cookbook.readPage[]` is the cleanest in-codebase example of *partial knowledge from a single
source*, and it is the direct model for §5.5's treatises: a treatise with three chapters is three
node-fragments, not one.

### 2.8 How vanilla hides unknown recipes — the precedent, blunt

**Vanilla never renders an unknown recipe. There is no greyed-out row anywhere in either
tinkering UI.** Both screens iterate `TinkerData.KnownRecipes` and never touch
`TinkerData.TinkerRecipes`:

```csharp
// D/XRL/UI/TinkeringScreen.cs:50-60
foreach (TinkerData knownRecipe in TinkerData.KnownRecipes)
{
    if (knownRecipe.Type == "Build") { list2.Add(knownRecipe); }
    else if (knownRecipe.Type == "Mod") { ModRecipes.Add(knownRecipe); }
}
```
```csharp
// D/Qud/UI/TinkeringStatusScreen.cs:144-154
foreach (TinkerData knownRecipe in TinkerData.KnownRecipes) { ... }
```

The entire "you have no idea what exists" affordance is four literal strings and one placeholder
row: *"You don't have the Tinkering skill."* / *"You don't have any item schematics."* /
*"You don't have any modification schematics."* / *"You don't have any moddable items."*
(`D/XRL/UI/TinkeringScreen.cs:108, 113, 180, 269, 274`) and `"~<none>"`
(`D/Qud/UI/TinkeringStatusScreen.cs:394-402`).

Vanilla *does* show a known-but-currently-unusable recipe (`DummyTinkerScreenObject`,
`TinkeringScreen.cs:152`). It never shows an unknown one. **Addendum 14's visibility law is not a
new idea we are imposing on Qud; it is Qud's own rule, and the shipped `KingdomTechMap.Locked` is
the deviation.**

### 2.9 The knowledge ledger vanilla actually persists: journal `Revealed`

Cooking diverges from tinkering and is the better model. `CookingGameState`
(`D/XRL/World/Skills/Cooking/CookingGameState.cs:10-13`) is a proper `[GameStateSingleton]`, and
`LearnRecipe` delegates storage to the **journal**:

```csharp
// D/XRL/World/Skills/Cooking/CookingGameState.cs:81-85
public static CookingRecipe LearnRecipe(CookingRecipe newRecipe, GameObject Chef = null)
{
    JournalAPI.AddRecipeNote(newRecipe, Chef);
    return newRecipe;
}
```

And the journal entry is the primitive we want:

```csharp
// D/Qud/API/IBaseJournalEntry.cs:11-31
public class IBaseJournalEntry : IComposite
{
    public string ID;
    public string History = "";
    public string Text;
    public string LearnedFrom;
    public int Weight = 100;
    public bool Revealed;
    public bool Tradable = true;
    public List<string> Attributes = new List<string>();
```
```csharp
// D/Qud/API/IBaseJournalEntry.cs:167-191
public virtual void Forget(bool fast = false)
{ if (Revealed && Forgettable()) { Revealed = false; SecretVisibilityChangedEvent.Send(this); Updated(); } }

public virtual void Reveal(string LearnedFrom = null, bool Silent = false)
{ if (!Revealed) { this.LearnedFrom = LearnedFrom; Revealed = true; SecretVisibilityChangedEvent.Send(this); Updated(); } }
```

Read API: `JournalAPI.HasNote(id)` (`D/Qud/API/JournalAPI.cs:183-187`),
`HasUnrevealedNote(id)` (`:189-199`), `TryRevealNote(id, LearnedFrom)` (`:173-181`),
`GetUnrevealedNotes(filter)` (`:1218`). Mod hook:
`SecretVisibilityChangedEvent` (`D/XRL/World/SecretVisibilityChangedEvent.cs:5-27`), fired from
both `Reveal` and `Forget`.

Three things this gives us for free and we should take all three:

1. **`Revealed` is exactly the discovery bit** — a fact that exists in the ledger but is not yet
   known to the player. Our node states map onto it one-for-one.
2. **`LearnedFrom` is the provenance string**, and vanilla stamps it with the faction that taught
   you: `observation.AppendHistory(" {{K|-learned from " + faction.GetFormattedName() + "}}")`
   (`D/XRL/World/Conversations/ConversationDelegates.cs:1198-1213`). Our chronicle line writes
   itself.
3. **Secrets are TRADABLE at water rituals** (`Tradable`, `:27-31`;
   `D/XRL/World/Conversations/Parts/IWaterRitualSecretPart.cs:186-201`,
   `D/XRL/World/Faction.cs:1533-1548`) — which means "buy a research lead from a faction you have
   standing with" is not an invention, it is an existing verb.

**Design ruling that falls out:** a discovered-but-unheld node should be a real
`JournalObservation` with `Revealed=false` until discovery, `Revealed=true` after. That puts the
founder's research leads in the same book as every other thing they have learned about the world,
costs us no new UI, and gets the water-ritual secret economy for nothing. It also means the
visibility law is enforced by vanilla's own accessor rather than by our discipline.

### 2.10 Quest state: the clean read

`The.Game.HasFinishedQuest(id)` (`D/XRL/XRLGame.cs:1206-1209`),
`HasUnfinishedQuest(id)` (`:1224-1231`), `HasQuest(id)` (`:1164-1175`),
`TryGetQuest(id, out q)` (`:1155-1162`), `Quest.IsStepFinished(stepId)`
(`D/XRL/World/Quest.cs:570-577`), `GetQuestFinishTime(id)` (`:619-622`, `-1` when never).

Quest IDs are strings and, in vanilla, **equal the quest name** — `QuestLoader.cs:63-67` falls back
to `Name`, and `B/Quests.xml` never writes an `ID`. So a lock reads
`Quest="What's Eating the Watervine?"`.

Three traps, all avoided by the §5.7 rule:

- `HasFinishedQuestStep(q, s)` returns **true for any step id** once the whole quest is finished,
  including ids that do not exist (`D/XRL/XRLGame.cs:1211-1216`).
- `ForceFinishQuest` inserts a blank `new Quest()` with a null `ID` and null `StepsByID`
  (`:636`), so anything dereferencing `.StepsByID` on a force-finished quest throws.
- A finished quest stays in **both** `Quests` and `FinishedQuests` (`:661-670`).

The event is `QuestFinishedEvent` (`D/XRL/World/QuestFinishedEvent.cs:5-6`,
`[GameEvent(Cascade = 15, Cache = Cache.Pool)]`), sent from `D/XRL/XRLGame.cs:740` **after** all
state is consistent. There is no `AfterQuestCompleteEvent` and no failure event. Subscribe from an
`IGameSystem` — `Registrar.Register(QuestFinishedEvent.ID)` + `override HandleEvent(QuestFinishedEvent E)`
— exactly as `D/XRL/WanderSystem.cs:49-55, 77-84` does.

Conversation gating is the shipped idiom for "this dialogue exists only if you did that":
`IfFinishedQuest` ×78, `IfHaveActiveQuest` ×123, `IfFinishedQuestStep="Quest~Step"` ×48 in
`B/Conversations.xml`, all implemented at
`D/XRL/World/Conversations/ConversationDelegates.cs:202-231`, with `IfNot…` inverses
**auto-synthesised** (`D/XRL/World/Conversations/ConversationDelegate.cs:20-26`). Predicates AND
together and an unknown key silently passes (`IConversationElement.cs:340-392`) — a fail-open
default we must *not* copy for research gates.

### 2.11 Achievements are not save state

`AchievementManager.State` is written to `Achievements.json` at `DataManager.SyncedPath(...)`
(`D/AchievementManager.cs:95-105`) — **account-wide, across characters.** Nothing in this design
may read or write them as research state.

---

## 3. The system: nodes laid over the DAG that already ships

### 3.1 One sentence

A **research node** is an authored record with prerequisites expressed in the roster vocabulary the
mod already matches on, a tier expressed as an Intelligence threshold, and exactly one effect —
*mint these roster keys* — so that the catalogue's `Knowledge=` gates, all 63 of them and every
third party's, are satisfied by research without a single line of the gate machinery changing.

### 3.2 The record

```xml
<research>
  <node Key="cruciblesteel"
        DisplayName="crucible steel"
        Branch="foundry"
        Tier="3"
        Requires="node:kilnheat,machine:*Furnace"
        MinTech="foundry"
        Grants="node:cruciblesteel"
        Effort="14"
        Reveals="node:pressurevessels,node:powerlattice"
        TaughtBy="disk:Smelter,book:r_TreatiseOnHeat"
        Forbidden=""
        Quest=""
        Effect="" />
</research>
```

| Attribute | Meaning | Substrate it reuses |
|---|---|---|
| `Key` | registry identity; merge-by-key like every other data lane | `KingdomLoader` / MODDING.md's data lane |
| `Branch` | which spine it hangs on; free string, so a mod adds a fourth | — |
| `Tier` | 1-4; each tier names an Intelligence threshold (§5.3) | `KingdomCrewRules.KindIntelligence` (`T/Growth/KingdomCrewRules.cs:36-37`) |
| `Requires` | comma list of roster tokens, ALL required | `KingdomZoningRules.Knows` / `MissingKnowledge` (`:443-493`) — unchanged |
| `MinTech` | craft rung the settlement must have reached to attempt it | `ZoneGate.MinTech` parse path (`:623-678`) — same parser |
| `Grants` | roster keys minted on completion. Usually `node:<Key>`; may mint more | `KingdomZoning.Learn` (`T/Growth/KingdomZoning.cs:138-162`) |
| `Effort` | staff-days of thinking a fully-crewed lab takes | `r_KingdomScaffold.RemainingTicks` idiom |
| `Reveals` | nodes made VISIBLE on completion (§6) | new |
| `TaughtBy` | tokens that grant this node OUTRIGHT, no lab time — a disk taught, a book read | the disk and book channels |
| `SeededBy` | tokens that REVEAL the node and START it with a head-start fraction, never finish it — every `rite:` token lives here and nowhere else (Addendum 18) | §5.5b |
| `Forbidden` | creed / culture / species / genotype tokens that make this node **invisible and unreachable**, told to nobody | `KingdomCreedRules` feeling reads + the identity tuple (§7) |
| `Quest` | a vanilla quest (and optional step) that must be finished before this node exists at all | §2.5's quest API |
| `Effect` | named non-key effects: `efficiency:5`, `statcap:Intelligence:1`, `recruitreveal:1` | §8 |

**`Requires` and `Grants` speak one vocabulary and it is the shipped one.** `node:` joins `disk:`,
`machine:`, `origin:` — and, under Addendum 17, the two identity kinds `culture:` and `species:`
alongside `creed:`, `savant:` and `rite:` — as a roster *kind* — `KingdomZoningRules.KindOf`/`NameOf` split on `:` and nothing else
(`T/Growth/KingdomZoningRules.cs:353-397`). Consequence: **a modder's building gates on a modder's
research node with zero C# — one `Knowledge="node:theirthing"` attribute in their own
`KingdomBuildings.xml` merge.** That is the whole "research requirements extensible by other
modders" requirement, discharged by the data lane that already ships.

### 3.3 What a node is worth in points: nothing

`TechPointsPerOrigin = 0` exists because counting people *"would turn that readout into a
population count"* (`T/Growth/KingdomZoningRules.cs:216-225`). The same argument kills points for
nodes, and harder: a research system that raised `TechLevel` would make the craft rung a readout of
the research system rather than of what was found in the world. **`PointsForKind("node")` returns
0.** Craft rises by disks and certifications, exactly as it does today. DIVERSITY §5 R3 stands.

---

## 4. The v1 tree

**Twenty-one nodes: a three-node trunk and three six-node spines.** Tiers 1-4. The three spines are
DIVERSITY §2.6's foundry / flesh / choir, re-aimed where the mandate's reach demands it. The trunk
is new and it exists for one reason: **the visibility law needs a place to start.** Before the
first node, the founder has no map at all.

Notation: `Requires` are roster tokens (ALL required, `|` inside one token means OR, matching
`KingdomZoningRules.Knows`'s existing token grammar, `T/Growth/KingdomZoningRules.cs:443-471`).
`Effort` is staff-days at full crew. Int is the tier threshold on the *best* researcher.

### 4.0 Tier ladder

| Tier | Int on the best researcher | Fiction | Vanilla parallel |
|---|---|---|---|
| 1 | 10 | anyone literate | no Tinker skill needed |
| 2 | 14 | a trained hand | Tinker I |
| 3 | 18 | a specialist | Tinker II |
| 4 | 22 | a mind the world made once | Tinker III |

**Hard at the tier boundary, soft inside it.** A tier-3 node cannot be worked at all without an
Int-18 researcher in the city — that is the Tinker I/II/III idiom, a hard gate. Above the
threshold, more Intelligence makes the work FASTER, through
`KingdomCrewRules.CapabilityEffectiveness` unchanged (`T/Growth/KingdomCrewRules.cs:208-224`), which
already floors at `MinCapabilityEffectiveness = 25` and never stalls a work to zero. The floor does
not apply across a tier boundary; a tier you cannot reach is not slow, it is shut.

**Int 22 is deliberately above what settlers roll.** Tier 4 is reachable only through a lodged
savant, a wild recruit (§8.4), or the founder's own works — which is precisely what makes §8.4
matter and what stops the `schooling`→Int-cap→higher-tier loop from running away (§8.3).

### 4.1 The trunk

| # | Key | Tier | Requires | Grants / Effect | Reveals |
|---|---|---|---|---|---|
| T1 | **note-keeping** | 1 | *(none — the only node visible at founding)* | `node:notes`; opens **the bookshelf** (S, `Category="knowledge"`, ships) and **the keepers' map itself** | `node:measuredwork`, `node:apprenticeship`, and the three spine roots |
| T2 | **measured work** | 1 | `node:notes` | `efficiency:5` — the first rung of the named worker-efficiency lane (§8.2) | `node:schooling` |
| T3 | **apprenticeship** | 2 | `node:notes` + a standing housing work | `statcap:any:1`; opens **the practice yard** (S) | `node:schooling`, `node:physic` |

**T1 is the design's keystone.** The map is not a screen you are given; it is a thing the keepers
begin to draw. Until `note-keeping` is held, `KingdomTechMap.Draw` reports one sentence — *"Nobody
here writes anything down."* — and nothing else. Every "how does the founder find the tree?"
question resolves to: the same way they find anything, by doing something in the world.

### 4.2 Branch A — THE FOUNDRY (materials, works, power)

| # | Key | Tier | Requires | Grants / Effect | Effort |
|---|---|---|---|---|---|
| A1 | **salvage sense** | 1 | `node:notes` | `node:salvagesense`; certification's water/hands cost halved | 6 |
| A2 | **kiln heat** | 2 | `node:salvagesense`, MinTech `salvage` | `node:kiln` — the **mason's yard** and **lime kiln** gate on it; `efficiency:5` on refining works | 10 |
| A3 | **crucible steel** | 3 | `node:kiln`, `machine:*Furnace`, MinTech `foundry` | `node:cruciblesteel` — opens the **smelter** (`Refines="workedmetal"`) | 14 |
| A4 | **pressure vessels** | 3 | `node:cruciblesteel` | `node:pressure` — the **condensing hall**'s second gate, alongside its shipped `Knowledge="machine:Solar Still"` (`T/KingdomBuildings.xml:328`), which is left exactly as authored | 14 |
| A5 | **power lattice** | 3 | `node:cruciblesteel`, `machine:*Capacitor\|machine:*Solar Panel` | `node:lattice` — conduits and the charging-post upgrade (12(g) networks) | 16 |
| A6 | **arclight discipline** | 4 | `node:lattice`, `node:pressure`, MinTech `arclight`, `rite:Barathrumites\|disk:×4` | `node:arclight` — the **arclight forge** (XL, `Refines="alloy"`), and the arcology's own gate | 30 |

**What A3 makes true and nothing else in the mod currently does:** you had to *find a furnace,
drag it home, and certify it* before your keepers could be taught to smelt. The certification
channel already ships (`T/Growth/KingdomZoning.cs:173-186`) and gates exactly one design today.
This branch is why it exists.

**Creed variance:** a Barathrumite city gets `Blueprints="1d3"` free from every water ritual
(`B/Factions.xml`), so its disk count — and therefore A6's `disk:×4` arm — climbs by *being
friends*. A Daughters city gets `Skill="Tinkering_Repair"` plus `Blueprints="1-2"`: the repair half,
which is the natural fit for Addendum 10(b)'s wear/mending economy. Same rung, different road.

### 4.3 Branch B — THE FLESH (bodies, medicine, the lab, citizen stats)

| # | Key | Tier | Requires | Grants / Effect | Effort |
|---|---|---|---|---|---|
| B1 | **butchery** | 1 | `node:notes` | `node:butchery` — the **butcher's slab** (S) | 6 |
| B2 | **the vat** | 2 | `node:butchery`, `culture:Ooze\|culture:Fungal\|rite:Oozes`. **`Forbidden="creed:Templar"`** | `node:vat` — the **vat-house** (M) and the preservation chain | 12 |
| B3 | **physic** | 2 | `node:butchery`, `node:apprenticeship` | `node:physic`; `statcap:Toughness:1`; opens **the infirmary** (M) | 12 |
| B4 | **the long draught** | 3 | `node:physic`, `node:vat` | `statcap:Strength:1`, `statcap:Agility:1` | 18 |
| B5 | **the graft** | 3 | `node:vat`, `savant:*`, MinTech `foundry`, a standing L craft district | `node:graft` — the **grafting hall** (L): the lab proper, DIVERSITY §3's rung 3 | 22 |
| B6 | **chimerism** | 4 | `node:graft`, `rite:Girsh\|machine:*Regeneration Tank`, MinTech `arclight` | `node:chimerism` — the **chimeric theatre** (XL) and the named procedures | 30 |

**B2 is the visibility law's hard case and the reason it is written down.** A Templar-creed city
must never see that `the vat` exists — not greyed out, not counted in a tail, not named in a
"roads not taken" line. §6.3 specifies how.

### 4.4 Branch C — THE CHOIR (mind, teaching, the city's ear)

| # | Key | Tier | Requires | Grants / Effect | Effort |
|---|---|---|---|---|---|
| C1 | **the moot** | 1 | `node:notes` | `node:moot` — the **moot stone** (S memorial, `Carries="spirit:1"`) | 6 |
| C2 | **the census** | 2 | `node:notes`, `node:moot` | `recruitreveal:1` — the city starts hearing of people worth sending for (§8.4) | 10 |
| C3 | **schooling** | 2 | `node:moot`, `node:apprenticeship` | `efficiency:10`; `statcap:Intelligence:1`; opens **the school** (M) | 14 |
| C4 | **the quiet cell** | 2 | `node:moot`, `rite:Seekers\|rite:Chavvah\|culture:Sightless Way`. **`Forbidden="creed:Templar,creed:Mechanimists"`** *(illustrative — the real list derives from the fault-line ceiling, not from an author's table)* | `node:quiet` — the **fasting cell** (M, `Provides="taf:quiet"`) | 12 |
| C5 | **the listening hall** | 3 | `node:quiet`, MinZones 3, MinTech `workshop` | `node:listening` — the **listening hall** (L, reach `zone`) | 18 |
| C6 | **assent** | 4 | `node:listening`, `rite:Chavvah`, Moon Stair adjacency | `node:assent` — the **assenting moot** (XL); the glimmer-ward machinery | 30 |

**Honest flag carried forward from DIVERSITY §2.6:** C5 and C6 lean on `IDEA-008/011/013` and
`COVERAGE.md` A7, all unbuilt. C1-C4 are cheap and shippable in the first research wave; C5-C6 are
a wave of their own and must not be promised in the same breath.

**The vanilla asymmetry stands and is not papered over:** `PsychicBiome.MutateGameObject` opens
`if (!Object.IsMutant()) return Object;` — the Moon Stair grants mental mutations to mutants only,
so a True Kin city's C6 arm is empty at exactly that location.

### 4.5 The five example nodes, with their gates in full

1. **note-keeping** — trunk, tier 1 (Int 10), `Requires=""`, `Effort=4`. The only node visible in a
   new realm. Grants `node:notes`; reveals five children; turns the keepers' map on.
2. **crucible steel** — foundry, tier 3 (Int 18), `Requires="node:kiln,machine:*Furnace"`,
   `MinTech="foundry"`, `Effort=14`. Three different kinds of gate on one node: a research
   prerequisite, a *certified object you had to find in the world*, and a craft rung.
3. **the vat** — flesh, tier 2 (Int 14),
   `Requires="node:butchery,culture:Ooze|culture:Fungal|rite:Oozes"`,
   `Forbidden="creed:Templar"`, `Effort=12`. The creed-forbidden case: invisible, uncounted,
   unmentioned, to a Templar city.
4. **schooling** — choir, tier 2 (Int 14), `Requires="node:moot,node:apprenticeship"`,
   `Effect="efficiency:10,statcap:Intelligence:1"`, `Effort=14`. The feedback node: research that
   raises the ceiling on the stat that gates research. Capped hard at §8.3.
5. **the census** — choir, tier 2 (Int 14), `Requires="node:notes,node:moot"`,
   `Effect="recruitreveal:1"`, `Effort=10`. The wild-recruit carrier: it does not spawn anyone; it
   makes the guestbook start carrying word of people (§8.4).

---

## 5. Research production — the lab

### 5.1 The loop, in three sentences

The laboratory is a work like any other: while it is **staffed and supplied** it charges elapsed
world-time against the ONE subject the founder set, at the crew's own effectiveness scaled by how
far the best researcher's Intelligence clears the node's tier threshold — and an unstaffed lab
accrues nothing, says so once, and unsays it when the crew is whole (Addendum 8 clause 2; STANDARDS
7b). When the accrual meets the node's `Effort`, the node is **held**: its `Grants` mint roster
keys through `KingdomZoning.Learn` unchanged, its `Reveals` become visible, the chronicle records
it, and the founder is told at the next awareness moment (Addendum 8 clause 3 / Addendum 10(a)).
**Nothing is banked** — there is no research currency, so there is nothing to allocate, no queue to
manage, and no second job; there is exactly one decision per completed node, taken at a building
that stands on the ground.

### 5.2 It is `r_KingdomScaffold`, for an idea

This is not an analogy; it is the implementation. `r_KingdomScaffold` already does the whole thing
(`T/Growth/KingdomScaffold.cs:19-33, 112, 143-152`):

- authored duration banked once into `RemainingTicks` on first look;
- every elapsed stretch buys labour ticks at `KingdomRules.RaisingEffectiveness(FreeHands)`
  (`T/Core/KingdomRules.cs:1068-1071`);
- *"Idle time is SPENT, never banked: `LastWorkedTick` advances whether or not anyone stood here"*
  — a settlement that emptied out and refilled does not get the empty months back as a burst;
- a shortfall line said once and unsaid when the crew is whole
  (`KingdomRules.RaisingShortfallLine`, `:1079-1088`).

`r_KingdomInquiry` is the same part with three differences: it holds a node key instead of a target
blueprint; its hands-wanted comes from the lab's own `CrewNeeds="intelligence:N"`; and on
completion it calls `KingdomResearch.Complete(system, key)` instead of raising a building.

### 5.3 The rate

```
inquiryTicksPerElapsedTick
    = crewEff/100                       // headcount ∧ capability, KingdomCrewRules.CombinedEffectiveness
    × wearEff/100                       // the lab is a work; damage degrades function (Addendum 10(b))
    × tierBonus(bestInt, tierThreshold) // 100 at threshold, +5 per point over, capped 150
    × labTier                           // scriptorium 1.0, laboratory 1.5, arclight annexe 2.0
```

with a hard precondition `bestInt >= tierThreshold` — below it the subject cannot be SET, not merely
worked slowly (§4.0). `crewEff` at zero makes the whole product zero: an idle lab produces nothing,
by arithmetic rather than by a special case.

**Supplied, not only staffed.** Addendum 11's grounded-production law binds: the lab consumes real
inputs — ink and vellum from the shipped `vellumpress` yard trade, and for tier-3+ subjects a
material draw against the chain (`shapedstone`/`workedmetal`). A subject whose inputs are missing
stalls exactly like a scaffold whose stores are dry, and says which input.

### 5.4 Shelving is remembered, not punished

Switching subjects keeps the abandoned node's accrued ticks. This is *memory*, not a queue: nothing
progresses except the current subject, and the founder cannot arrange or prioritise anything. Cap
the remembered set at 8 rows (§10); the ninth shelving drops the least-advanced, named once.

### 5.5 The other ways a node is held or begun — no lab time at all

`TaughtBy` completes a node outright; `SeededBy` only reveals and begins one. Each arm is a vanilla
learning idiom, and each already has mod machinery or a near neighbour:

| Arm | The verb | Machinery |
|---|---|---|
| `disk:<name>` | you carry a data disk to the keepers and it is read and handed back | ships — `T/Growth/KingdomZoning.cs:520-545`; the disk is deliberately **not consumed** (`:516-519`) |
| `book:<blueprint>` | a treatise found in a ruin or bought from a merchant, read at the scriptorium | `Books.xml` + the vanilla book-reading surface; new items, one population-table merge per faction |
| `machine:<blueprint>` | certifying the machine teaches half of it — certification **advances** the node by `Effort/2` and reveals it, rather than completing it | ships — `RecordCertification` (`T/Growth/KingdomZoning.cs:173-186`) |
| `rite:<faction>` | **`SeededBy` only, never `TaughtBy`** — you shared water with them, and what they taught you *starts* the branch your city must still walk (Addendum 18; §5.5b) | `B/Factions.xml`'s 43 `<waterritual>` grants, read not written |
| `savant:<name>` | a lodged notable teaches while they stay — and **the node is held only while they stay** | `T/Experience/KingdomGuestRules.cs` + the lodging gate |

The `machine:` arm is the psychometry-shaped one: holding the artifact is *most* of the answer but
not all of it, and the rest is the keepers' own work.

### 5.5b The seed/completion split — `rite:` ruled (Addendum 18)

> *"`rite:` is LEGITIMATE … But it is **SEED, NOT CEILING**: rite-knowledge STARTS branches the
> city's own people must still complete — the founder opens the door; the city walks through. A
> rite key alone never finishes a node."* — Addendum 18

**What a rite key grants, on the day the founder shares water:**

1. **Discovery.** Every node in the seeded set becomes VISIBLE (§6) — the founder now knows the
   thing exists, because they were told it by someone who does it. This is a discovery source in
   exactly Addendum 14's sense.
2. **A begun subject.** The head node of the seeded set is credited with a head-start of
   `SeedFraction` × `Effort` (proposal: **25%**, one number, authored per node, capped so no
   stacking of rites can exceed 50% on any single node).
3. **A prerequisite satisfied.** Where a node's `Requires` names `rite:<faction>`, the key
   satisfies that token, exactly as it does today for any roster key. That is the *permission* to
   walk the branch; it is not progress along it.

**What a rite key can never grant, ever:**

- **A finished node.** No `rite:` token may appear in `TaughtBy`. This is a *validated* rule at
  load, refused loudly by file and key like every other schema error — not a convention.
- **A tier skip.** The Intelligence threshold is checked against the CITY's researchers, never
  against the founder. A founder with Int 30 and every rite in Qud still cannot advance a tier-4
  node in a city of Int-12 settlers. This is the clause that keeps the founder from being the
  research system.
- **More than one head node per rite.** A rite seeds its branch's entrance, not its whole spine —
  otherwise "share water widely" becomes the optimal research strategy and the lab becomes
  decoration.
- **A key that outlives the fiction.** `rite:` is permanent (a thing learned is learned), unlike
  `culture:`/`species:`/`savant:`, which are live off the people who carry them.

**How the map renders a seeded-but-incomplete branch.** This is its own prose register, and it is
the sentence the author's ruling asks for:

> *"The founder remembers the shape of it. Nobody in {{C|Sotham's Rest}} can build it yet."*

and, for the begun head node, the same prose-distance idiom `KingdomTechMapRules.Reach` already
uses (`T/Core/KingdomTechMapRules.cs:152-162`) — *"begun"* sits between *"one thing away"* and
*"within reach"*, with no percentage attached. The founder's own memory is named as the source, in
the `LearnedFrom` field vanilla already carries on a revealed secret
(`D/Qud/API/IBaseJournalEntry.cs:19`, stamped with the faction's formatted name at
`D/XRL/World/Conversations/ConversationDelegates.cs:1198-1213`): *"— remembered from the
Barathrumites"*.

**Why this is the right ruling and not a nerf.** Under the rejected reading, a founder who had done
the rounds of Qud's factions would arrive at a new settlement holding half a tech tree, and the
lab — the thing Addendum 14 actually asked for — would have nothing to do. Under this reading the
founder's history is *the reason a city can walk a road at all*, and the city's people are the
reason it gets to the end. The founding myth stays mechanical without becoming the whole mechanic.

### 5.6 Per-creed, per-culture, per-species variation lives in the SOURCES, not in the tree

One tree, differently reachable. Five dials, all of them vanilla's or already ours. The identity
tuple that drives them is Addendum 17's and is specified in full at §7.

1. **Rite grants** — `B/Factions.xml` decides what each creed teaches. Barathrumites → the foundry
   spine; Oozes/Fungi → the flesh spine; Seekers/Chavvah → the choir spine; Templar → chrome, and
   *not* the flesh spine at all.
2. **Books and disks by population table** — a treatise on heat is common in Barathrumite wares and
   absent from Templar ones. This is a `PopulationTables.xml` merge, no code
   (`T/PopulationTables.xml` already merges into `Tier1Wares`/`Tier2Wares` this way).
3. **Knowledge keys from peoples** — `culture:`/`species:` as node prerequisites, live off
   `System.OriginCounts` (`T/Growth/KingdomZoning.cs:104-124`), so they **come and go with the
   people**. A city's flesh branch closes when the last ooze leaves.
4. **Genotype conditions** — vanilla's own idiom is `RecipeGenotype="!True Kin"` on Chavvah's water
   ritual. A node may carry `Forbidden="genotype:True Kin"` in exactly that spelling.
5. **The identity tuple on the people themselves** (Addendum 17) — `culture:` for what a people
   KNOWS and `species:` for what a body IS, as two distinct knowledge kinds, read live off the
   settlers. A city of Mopango knows Mopango things; a city of oozes can staff a vat-house. §7.

**The fault lines need no table of ours.** Creed hostility already reads
`Faction.GetFeelingTowardsFaction` and takes the worse direction
(`T/Core/KingdomCreedRules.cs:341-368`); a faction that dislikes the realm sends nobody
(`:275-278`). A Templar city cannot staff a vat-house because nobody who would staff it will live
there. `Forbidden` is the *explicit* lock for the cases where emergence is not enough; it should
stay short.

### 5.7 Quest locks

`Quest="<QuestID>"` or `Quest="<QuestID>~<StepID>"`. A node whose quest is unfinished **does not
exist**: not hidden-but-counted, not revealed-and-locked — absent from the registry's answer to
every question the map asks. Read through `The.Game.HasFinishedQuest(id)`
(`D/XRL/XRLGame.cs:1206-1209`) or, for a step,
`The.Game.TryGetQuest(id, out var q) && q.IsStepFinished(stepId)` (`D/XRL/XRLGame.cs:1155-1162`,
`D/XRL/World/Quest.cs:570-577`) — deliberately **not** `HasFinishedQuestStep`, which returns true
for any step id once the whole quest is finished (`D/XRL/XRLGame.cs:1211-1216`) including step ids
that do not exist.

Cache the answer and refresh it on `QuestFinishedEvent` (`D/XRL/World/QuestFinishedEvent.cs:5-6`,
sent from `D/XRL/XRLGame.cs:740` **after** all quest state is consistent), registered from an
`IGameSystem` exactly as `WanderSystem` does (`D/XRL/WanderSystem.cs:49-55, 77-84`;
`D/XRL/IGameSystem.cs:2562, 2890`). There is no per-turn quest polling anywhere in this design.

---

## 6. The visibility law, implemented

> *"You SEE what you have unlocked; you do NOT see what you haven't; you ESPECIALLY cannot see what
> you CAN'T unlock. Discovery reveals the tree (psychometry-like), never a spoiler screen."*
> — Addendum 14

### 6.1 Four states, three of which the founder can tell apart

| State | The founder sees | Vanilla parallel |
|---|---|---|
| **HELD** | the node, what it opened, in the map's first chapter | a known recipe on the tinkering screen |
| **VISIBLE** (discovered, unheld) | the node's name, what it wants, and prose distance — no number | `Examiner` status 1: *"you think it's probably a variety of X"* |
| **HIDDEN** (exists, undiscovered) | **nothing.** Not a row, not a count, not a silhouette | `Examiner` status 0 — vanilla shows a decoy, not a blank |
| **FORBIDDEN** (creed/culture/species/genotype/quest-locked out) | **nothing, and nothing that differs from HIDDEN in any observable way** | no vanilla parallel; this is the law's hard clause |

The distinction between HIDDEN and FORBIDDEN must be invisible **by construction**, not by care.
See §6.3.

### 6.2 Where the state lives: vanilla's own revealed-secret bit

Per §2.9, each node is registered once as a `JournalObservation` with a stable ID
(`taf:node:<key>`), `Revealed = false`, and `Tradable` left at its default so the water-ritual
secret economy applies. Then:

- **Discovery** = `JournalAPI.TryRevealNote("taf:node:" + key, LearnedFrom: <source>)`
  (`D/Qud/API/JournalAPI.cs:173-181`), which fires `SecretVisibilityChangedEvent`
  (`D/XRL/World/SecretVisibilityChangedEvent.cs:5-27`) for free.
- **"Is it discovered?"** = `JournalAPI.HasNote(id)` (`:183-187`) — an O(1) `StringMap` lookup, not
  a scan.
- **Provenance for the prose** = the `LearnedFrom` field (`D/Qud/API/IBaseJournalEntry.cs:19`),
  stamped exactly as vanilla stamps a faction-taught observation
  (`D/XRL/World/Conversations/ConversationDelegates.cs:1198-1213`).

**Held** stays in our own roster (`node:<key>` in the game-state string,
`T/Growth/KingdomZoning.cs:545-553`), because held-ness is a property of the CITY and revealed-ness
is a property of the founder's knowledge of the world. That split is not fussiness — it is what
lets a founder who has walked three realms carry their leads to a new settlement while the new
settlement still has to do its own work (which is Addendum 18's ruling, generalised).

### 6.3 How a node becomes visible — six discovery sources, and one closed door

1. **Adjacency.** Holding a node reveals the nodes its `Reveals` names. This is the spine: the tree
   unrolls in front of the founder as they walk it, and there is no reachable dead end.
2. **A fragment in hand.** Carrying a data disk, a treatise, or a certified machine whose token
   appears in a node's `TaughtBy`/`SeededBy`/`Requires` reveals that node — *before* it is taught.
   Vanilla precedent: a data disk you cannot yet learn from still tells you what it is, provided
   you have the Tinkering skill (`D/XRL/World/Parts/DataDisk.cs:120`). Ours: you must be able to
   read it, which means holding it and having a keeper who could.
3. **A rumor at the gate.** The guestbook already mints notable guests carrying `HookKind.Ruin` /
   `Machine` / `Debt` with authored prose (`T/Experience/KingdomGuestRules.cs`). A fourth kind,
   `Lead`, reveals one HIDDEN node in the city's admissible set and says why:
   *"a still that has not been lit in a generation, and might yet be lit again"* is already written.
4. **A savant's hint.** A lodged notable reveals the nodes in their specialty for as long as they
   stay — and, unlike a held node, **the reveal is withdrawn when they leave** and the map says so.
5. **A rite remembered.** Addendum 18: a `rite:` key reveals its seeded set and begins the head
   node. §5.5b.
6. **A quest finished.** `QuestFinishedEvent` fires the re-evaluation; a node whose `Quest` is now
   satisfied moves from *does not exist* to HIDDEN, and from HIDDEN to VISIBLE by any of 1-5.

**The closed door:** nothing reveals a FORBIDDEN node. `Reveals` is filtered through admissibility
before it is applied, so a Templar city that completes `butchery` is offered `physic` and is never
offered `the vat` — and is never told that a thing was filtered.

### 6.4 The map, rewritten

Three chapters, replacing `KingdomTechMap`'s current three.

1. **What they know, and what it opened.** Unchanged — `KingdomTechMap.Opened`
   (`T/Core/KingdomTechMap.cs:82-101`) already does exactly this and is already law-compliant.
2. **What they are working out.** NEW. The current subject with its prose distance
   (`KingdomTechMapRules.Reach`, `T/Core/KingdomTechMapRules.cs:152-162`), its shortfall line if
   the lab is short-crewed or short-supplied, and the *shelved* subjects by name.
3. **What they have heard of.** REPLACES `KingdomTechMap.Locked`
   (`T/Core/KingdomTechMap.cs:137-190`). Same rendering — name, prose distance, what is in the way
   — but the row set is **the VISIBLE-and-unheld nodes only**, and the tail count
   (`"And N further off"`, `:181-184`) counts **only those rows**. Today it iterates
   `KingdomData.Buildings` in full and counts every locked design in the catalogue; that is the
   deviation, and it is the one line of shipped behaviour this design deletes.

The "roads not taken" chapter (`KingdomTechMapRules.RoadsNotTaken`) survives and gets *more*
important: it names **kinds of learning this city has never done** — *"The keepers know nothing of
the Barathrumites' way"* — which is honest about a whole road without naming a single node behind
it. That is the law's escape valve: the founder can tell there is more world, and cannot tell what
is in it.

**And the map itself is gated on `node:notes`** (§4.1 T1). Before the first node there is one
sentence and no chapters.

### 6.5 The leak audit — every place a hidden node could show through

A visibility law is only as good as its worst surface. Each of these is a real leak and each has a
named fix:

| Leak | Fix |
|---|---|
| The commission menu listing a design whose gate the founder cannot meet, with a `GateNote` naming the missing key (`T/Growth/KingdomZoning.cs:276-297`) | A design gated on `node:X` where `X` is HIDDEN is **absent from the menu**, not greyed. Vanilla's precedent is total omission (§2.8). |
| The refusal prose naming an unlearned key (`ZoningJudgement.Detail`, `RefusedUnlearned`) | Unreachable once the design is absent from the menu. Any other path to `Judge` returns a generic *"nobody here knows how"* for a HIDDEN gate. |
| The tail count *"and N further off"* | Counted over VISIBLE rows only. |
| The `open` count *"N designs are already open"* | Safe — it counts achievable designs, which are by definition held. |
| The chronicle / ledger / `KingdomWord` mentioning a filtered node | The telling budget draws from the same admissible set; a filtered node has no notice to emit. |
| A happening or petition asking for a forbidden building | `IKingdomAskSource` results are filtered through admissibility before publication (§9). |
| A **modder's** building gated on a forbidden node | Same filter, same code path — the filter lives in `KingdomZoning.Permits`, not in the catalogue. |
| The debug wishes (`T/Debug/KingdomWishes.cs`) | Exempt and must stay exempt; they are the only honest way to test the law. Gate behind the existing dev-log option. |
| A save-file reader | Out of scope. The law is a UI law, not a cryptographic one, and pretending otherwise would cost real work for no player-visible gain. **Stated so nobody re-derives it.** |

### 6.6 Two sentences, for the report

*State lives as a vanilla `JournalObservation.Revealed` bit per node (discovery) plus a `node:` key
in the city roster (held), so the law is enforced by the accessor rather than by our discipline —
and every surface that could enumerate nodes iterates the VISIBLE set, never the registry.*
*Forbidden nodes are removed from the admissible set before any reveal, count, refusal, menu row,
or notice is computed, so "cannot unlock" and "have not discovered" are the same absence of a row
rather than two different renderings.*

---

## 7. Identity as a first-class input (Addendum 17)

> *"Both accessors, following vanilla's own split: `GetCulture()` (33 peoples, story-shaped) for
> tech/creed/building divergence — what a people KNOWS; `GetSpecies()` (98, granular) for
> body-shaped things — QoL, the lab's anatomy gates. Two knowledge kinds. Extended, not replaced;
> API-friendly. … The full identity tuple feeds capability and knowledge … applied where a lane
> already reads identity, never as a new parallel system."* — Addendum 17

### 7.1 The tuple, and where each element already has a reader

`Identity = (Culture, Creed, Species, Genotype, Attributes, Skills)` — six facts about a person,
five of them already read somewhere in the mod or the engine.

| Element | Accessor | Kind minted | Already read by |
|---|---|---|---|
| Culture | `GetCulture()` | `culture:<name>` | new (Addendum 17); replaces DIVERSITY §2.4's speculative key |
| Creed | the settlement's creed machinery | `creed:<faction>` | `T/Core/KingdomCreedRules.cs` (weights, fault lines, hostility) |
| Species | `GetSpecies()` | `species:<name>` | new; the natural home for the lab's anatomy gates |
| Genotype | vanilla genotype | `genotype:<name>` | vanilla's own `RecipeGenotype="!True Kin"` idiom |
| Attributes | `Statistic` | — (not a key; a threshold) | `T/Growth/KingdomCrews.cs` capability reads |
| Skills | `HasSkill` | — (not a key; a threshold) | new, small |

**Two knowledge kinds, not one.** Culture answers *what these people know*; species answers *what
these bodies are*. A Templar ooze is a coherent thing and the two keys must be able to disagree.

### 7.2 Where identity enters, and where it must not

Addendum 17's mesh condition is the binding constraint: **applied where a lane already reads
identity, never as a new parallel system.** Four lanes qualify, and no fifth is proposed.

1. **Node prerequisites (§3.2 `Requires` / `Forbidden`).** `culture:`/`species:`/`creed:`/
   `genotype:` are roster tokens matched by `KingdomZoningRules.Knows` unchanged. Live off
   `System.OriginCounts`-style tallies, so they come and go with the people who carry them — which
   is what makes a city's technology *a portrait of who lives in it*.
2. **Worker efficiency (§8.2).** A culture's affinity for a work kind is a named multiplier on the
   *same* lane node effects feed, not a second lane.
3. **Crew capability (`KingdomCrewRules`).** Today capability is `Strength`/`Intelligence` plus the
   one derived fact `Tireless` (`T/Growth/KingdomCrewRules.cs:14-19`). Addendum 17 adds identity to
   the same `SettlerCapability` struct — one field, read by the same `RankCandidates`, changing no
   caller.
4. **QoL / anatomy.** Species already belongs here by the addendum's own split; the QoL vocabulary
   (`KingdomQolRules.Derive(ResidentTruth)`, `T/Core/KingdomQolRules.cs:394`) is the existing
   reader and needs a species field, not a new system.

**Where identity must NOT enter:** the tier ladder. Tiers are Intelligence and nothing else
(Addendum 14 is explicit). A culture may make a node *reachable*, *cheaper*, or *faster*; it may
never make a tier *skippable*. Stated because it is the obvious next step and it is wrong.

### 7.3 Affinity: the one new number, and its shape

```
identityAffinity(person, workKind)  ->  percent, default 100, clamped [70, 130]
```

Derived, not authored per pair: an affinity table maps `(culture|species) -> work-kind weights`,
seeded from what the game already says about each people (vanilla's own `xtagTextFragments`
`Activity` / `ValuedOre` / `SacredThing` strings on creature blueprints —
`B/ObjectBlueprints/Creatures.xml:2424` and its kin — are a shipped, per-culture statement of what
a people does, read by vanilla's own quest generators at
`D/XRL/World/ZoneBuilders/VillageDynamicQuestContext.cs:228-235`). **Derive before authoring: read
those strings first; author only what the survey leaves empty.**

The clamp is narrow on purpose. A ±30% band makes a culture *feel* like something without making
any city composition unplayable, and it keeps the "high-stat recruit" (§8.4) the dominant lever
rather than a demographic min-max.

### 7.4 Extension points (12(i), per Addendum 17's closing clause)

Every identity hook is an extension point, so a modded culture or species composes with no change
to our code:

- **Data lane, zero C#:** a modder's node declares `Requires="culture:TheirPeople"`; a modder's
  building declares `Knowledge="node:theirthing"`. Both already work through merge-by-key.
- **Behaviour lane:** `IKingdomIdentitySource` (§9) answers `Affinity(identity, workKind)` and
  `Keys(identity)` for vocabularies we do not know. Registered by attribute discovery, version
  checked, budget-isolated, exactly like `IKingdomAskSource`.
- **Never an enum.** Culture and species are strings on both sides. `KingdomZoningRules` already
  treats an unrecognised roster kind as *somebody else's vocabulary — logged, not refused*
  (STANDARDS 9), and that rule is what makes a third-party culture work on day one.

### 7.5 Composition with Addendum 16's creed-gate stack

Addendum 16 rules that creed-buildings gate on five things, the fifth being *"the technology
requirement (Addendum 14's keys when the research system lands)"*. That is this system, and the
composition is already correct: the creed-building's `Knowledge=` attribute names a `node:` key,
the node's own `Requires` names `creed:`/`culture:`, and the builder-alignment and creed-amount
gates are Addendum 16's own machinery sitting beside — not inside — the research system. **Nothing
here needs to know what a creed-building is.**

Addendum 16's *creed history* fact (a settler's previously-held creeds, newly recorded) also joins
the tuple naturally: it is another `creed:` token, minted from history rather than from the
present, and it should be a distinct kind (`creedwas:`) so a node can ask for either.

### 7.6 Composition with Addendum 15's strata

Addendum 15: *"deep research unlocks deep designs; the visibility law applies — a surface city
never sees the deep tree it hasn't touched."* This falls out with no new machinery: the deep set's
nodes are HIDDEN until a discovery source fires, and the only discovery sources that can fire are
ones that require being down there (a fragment from a deep ruin, a rumor from someone who was). A
fourth spine — **THE DEEP** — is the natural home, and it is deliberately **not** in the v1 tree
(§4): it lands with the WR-3 strata wave, not with the first research wave.

---

## 8. The reach — "deeply ingrained, touches everything"

Addendum 14 names four reaches. Two exist, two are new machinery, and the two new ones must be
designed at their honest minimum.

### 8.1 What buildings you know how to build — EXISTS, unchanged

Already answered in §3.1: a node mints a roster key; a design's `Knowledge=` gate matches it. Zero
churn on the 63 shipped designs, zero churn on third-party ones, zero new gate machinery.

The one action item is **data**: counted in `T/KingdomBuildings.xml`, **9 designs declare
`MinTech`** (`:313, 328, 423, 432, 440, 466, 578, 588, 713`) and **4 declare `Knowledge`**
(`:241` `origin:the salt marshes`; `:328` `machine:Solar Still`; `:758` and `:764`
`pattern:` tokens) — of 63 designs. Exactly one is a certification gate, and it is the model the
rest should copy. `TechLevel.Arclight` is reachable at 14 points and **gates nothing that ships**.
The tree is worth nothing until the catalogue actually asks for it: that data pass is the cheapest
half of this whole wave and should land first, ahead of any node machinery.

### 8.2 Worker efficiency — a NAMED lane, composed by multiplication

Today a work's output is `min(headcountEff, capabilityEff)` (`KingdomCrewRules.CombinedEffectiveness`,
`T/Growth/KingdomCrewRules.cs:226-229`) then scaled by wear (`KingdomWearRules.WorkEffectiveness`,
consumed at `T/Growth/KingdomWear.cs:230-238`). **Method is a third factor, never folded into the
first two:**

```
output = base
       × min(headcountEff, capabilityEff) / 100     // crew — unchanged
       × wearEff / 100                              // condition — unchanged
       × method / 100                               // NEW: 100 + Σ node efficiency grants, cap 150
       × identityAffinity / 100                     // NEW: §7.3, clamp [70,130]
```

Three properties this shape buys, all of them required:

- **Idle produces nothing, by arithmetic.** `min(0, x) = 0` and zero times anything is zero. No
  research grant can ever make an unstaffed work produce (Addendum 8 clause 2).
- **Method never papers over a broken building.** Wear stays its own factor, so Addendum 10(b)'s
  "damage degrades every work's function" is untouched.
- **The cap is on the lane, not the sum of grants.** `efficiency:` grants across the whole tree may
  total more than 50; `method` still clamps at 150. That is what stops the tree's tail from being
  a linear damage multiplier.

`method` is realm-wide (the keepers write to each other); `identityAffinity` is per-person and
enters through `SettlerCapability` (§7.2). One is knowledge, the other is who is holding the tool.

### 8.3 Citizen stat caps — NEW machinery, at its honest minimum

**A hard finding that constrains this entirely.** `Statistic.Max` is a *static, shared-by-name*
`IntBox`:

```csharp
// D/XRL/World/Statistic.cs:142-144, 176-196
public static Dictionary<string, IntBox> _Max = new Dictionary<string, IntBox>();
...
public int Max
{
    get { if (_MaxValue != null || _Max.TryGetValue(Name, out _MaxValue)) return _MaxValue.i; return 30; }
    set { if (_MaxValue == null && !_Max.TryGetValue(Name, out _MaxValue)) _Max[Name] = (_MaxValue = new IntBox(value)); else _MaxValue.i = value; }
}
```

**Writing `citizen.GetStat("Strength").Max` would change the Strength ceiling for every creature in
the game, including the player.** This is a loaded gun and it is the reason "citizen stat cap" must
be *our* ceiling, enforced by *our* training code, and never vanilla's `Max`. Recorded loudly.

Vanilla's other relevant facts:

- `BaseValue` is never clamped anywhere; all 82 `BaseValue +=` sites write unbounded
  (`D/XRL/World/Statistic.cs:222-238`).
- Attributes register `Min="1" Max="9000"` on the root `Creature` blueprint
  (`B/ObjectBlueprints/Creatures.xml:39-44`) — not a meaningful cap.
- The 24-point genotype cap (`B/Genotypes.xml:5-10, 21-26`) is **character-creation UI only** and
  is never consulted in game (`D/XRL/CharacterBuilds/Qud/UI/AttributeDataElement.cs:68-74`).
- **NPCs already level in vanilla**, and it is not obscure: every `IsPlayerLed()` brain within ten
  tiles of the player receives the same XP award (`D/XRL/World/Parts/Experience.cs:75-91`), runs
  `Leveler.LevelUp`, and gains **+1 to all six attributes on every sixth level**
  (`D/XRL/World/Parts/Leveler.cs:217-229, 261-277`). There is even a shipped option for the
  messages (`B/Options.xml:153`, `OptionDisplayLedLevelUp`).
- The data-driven permanent-stat precedent is `StatOnEat` with `Stats="Ego:-1"` (Humble Pie,
  `B/ObjectBlueprints/Foods.xml:625`) and the reusable `GameObjectAttributeUnit` with matching
  apply/remove (`D/XRL/World/Units/GameObjectAttributeUnit.cs:29-64`).

**The design — practice, not a training screen.**

1. **The ceiling is the city's, and it is per stat kind.**
   `ceiling(person, stat) = person.BaseStat(stat) at the moment they joined + cityHeadroom(stat)`,
   where `cityHeadroom` is the sum of held nodes' `statcap:<Stat>:<n>` effects, hard-capped at
   **+3 per stat and +6 in total** across all stats. A citizen never exceeds what they walked in
   with plus what the city taught them.
2. **Progress is practice, and practice is labour.** On the settlement pass, a resident assigned to
   a work whose `CrewNeeds` names stat kind K accrues *practice-days* in K. At
   `PracticeDaysPerPoint` (proposal: 30 world-days at full crew effectiveness) they gain +1
   `BaseValue` in K, up to their ceiling. **Idle citizens gain nothing** — the accrual multiplies
   by the same `crewEff` the work's output does, so a work that produced nothing also taught
   nothing (Addendum 8 clause 2, applied to people).
3. **Where the state lives.** Practice-days are two `ushort`s per resident row on the city model
   (Strength, Intelligence — the two kinds `KingdomCrewRules` reads today; extensible by kind
   later). The `BaseValue` write happens on **materialisation**, not in a dormant zone — Addendum
   8 clause 3's crystallise-at-awareness, and the only shape that respects "no touching objects in
   unloaded zones".
4. **It is announced, once, as a happening.** *"Ghali has grown strong at the quarry."* This is the
   whole player-facing surface. There is no training UI, no assignment screen, and no per-citizen
   panel — those would be the second job the mod refuses.
5. **Toughness and Intelligence have vanilla side effects and we take them.** `Leveler` already
   retroactively adjusts Hitpoints when Toughness changes and SP when Intelligence changes
   (`D/XRL/World/Parts/Leveler.cs:39-85`); writing `BaseValue` fires `NotifyChange`
   (`D/XRL/World/Statistic.cs:222-238`) and the existing handlers do the rest. **We write one
   number and vanilla keeps itself consistent.**

**What is deliberately NOT built in v1:** a practice yard that trains without work (a building
whose only output is a number is a numbers panel with a roof); stat *loss* from idleness (Addendum
8 clause 1's "time never mints unchosen debts"); and any path by which a citizen exceeds their
ceiling.

### 8.4 Wild high-stat recruits — discovery, terms, and the QoL law doing the work

Addendum 14: *"High-stat citizens are findable in the wild — recruitable, but with high
expectations: living standards, jobs they want to do (composes with QoL/lodging/creed law)."*

**Where they come from: nowhere new.** Vanilla already makes them, three ways, and all three are
readable:

- **Heroes.** `HeroMaker.MakeHero` boosts each attribute by `BoostStat(HeroXxxBoost)` — +25% of
  BaseValue per point — doubles Hitpoints and multiplies Level by 1.5
  (`D/XRL/World/HeroMaker.cs:52-100`). Templates are ordinary blueprints in
  `B/ObjectBlueprints/Data.xml:725-1038` — `SpecialVillagerHeroTemplate_Tinker` gives
  `HeroIntBoost=2`, `_Warden` gives Str/Tou/Agi 3 and `HeroMinLevel=20`, `_Bears` gives
  `HeroIntBoost=6` (+150% Intelligence). **There is no `HeroTemplates.xml`** — recorded because two
  prior notes assumed one.
- **Lair owners**, boosted a *second* time on top of hero-ing, with HP floored at `50 × ZoneTier`
  and Level at `ZoneTier × 5` (`D/XRL/World/WorldBuilders/JoppaWorldBuilder.cs:2685-2778`).
- **Legendaries**, which are `GivesRep` + a proper name; eligibility is negative-filtered at
  `D/Qud/API/EncountersAPI.cs:688-706`.

Crucially, `MakeHero` adds `GivesRep` unless `HeroNoWaterRitual` is set
(`D/XRL/World/HeroMaker.cs:314-319`) — **so a hero is already someone you can share water with.**
The recruitment verb has a social prerequisite the game already models.

**The four beats.**

1. **Discovery** — `node:census` (§4.4 C2) turns on a new guest hook kind, `Lead`, and the
   guestbook starts carrying word of *people*: a name, a place, and what they are good at. Nothing
   is minted; the world already contains them. The founder travels or does not.
2. **The ask** — the recruitment offer is a conversation at the person, not a purchase. It is
   gated on standing (their faction's feeling toward the realm, which
   `T/Core/KingdomCreedRules.cs:341-368` already computes) and on the city meeting their terms.
3. **The terms** — read off their own profile, not authored per recruit:
   - a `QolProfile` with real `Needs`/`Refuses` and a **Closeness floor** (`Roomed` or better for a
     legendary), through `KingdomQolRules.Refine` and `KingdomLodgingRules` unchanged;
   - a **work of their kind** standing and staffable — their `CrewNeeds` kind must match a work the
     city actually has;
   - a **creed the city can hold** — the fault-line ceiling (Addendum 4d) refuses the rest with no
     rule of ours.
   Unmet terms produce the 7b sentence naming exactly one missing thing, and they stay where they
   are. *"She will not leave the Stilt for a place with no forge."*
4. **Joining, and leaving** — joining is vanilla's own village-membership rewrite:
   `Brain.Allegiance.Clear(); Allegiance.Add(cityFaction, 100); Hostile = false; Calm = true;`
   (`D/XRL/World/ZoneBuilders/VillageBase.cs:221-238`) — the exact code the engine uses to make
   someone a villager. **Leaving needs no new machinery at all**: a high-stat recruit is a resident
   with a strict `QolProfile`, and the shipped lodging/brink law already makes an unsatisfied
   resident warn, wait its window, and go (Addendum 10(a)). That is the whole "high expectations"
   mechanic, and it costs one field.

**Why they matter, mechanically:** tier 4 wants Int 22 and settlers do not roll it (§4.0). A
`SpecialVillagerHeroTemplate_Tinker`-class recruit, or a lodged savant, is how a city reaches the
top of its own tree. **The research system's endgame is a person, not a building** — which is
DIVERSITY §2.4's artifacts-and-people thesis arriving exactly where it should.

**Loudly rejected here:** minting bespoke high-stat creatures for recruitment. The world already
made them, they are already interesting, and a spawner would turn a journey into a lottery.

---

## 9. The API — research features AND research requirements (12(i))

Addendum 14 asks for both halves, and they land in the two lanes the mod already publishes.

### 9.1 The data lane covers most of it, with no C# at all

| A modder wants to… | They write |
|---|---|
| add a research node | one `<node>` in their own `Research.xml`, merged by key |
| gate their building on their node | `Knowledge="node:theirkey"` on their `<building>` |
| gate their building on OUR node | `Knowledge="node:cruciblesteel"` — a published string |
| gate their node on our node, or ours on a rite/culture | `Requires="node:kiln,culture:Mopango"` |
| forbid their node to a creed | `Forbidden="creed:Templar"` |
| lock their node behind their quest | `Quest="Their Quest Name"` |
| have their book teach a node | `TaughtBy="book:their_treatise"` + a `PopulationTables.xml` merge |

**This is the single most important API statement in the document:** because a node's whole effect
is minting a roster key, and because `Knowledge=` already exists and is already public, *a third
party gets research integration by writing XML*. STANDARDS 6's extensibility law is satisfied
without a line of behaviour-lane code.

### 9.2 The behaviour lane — three contracts, `KingdomApiRules.Version` 1 → 2

Following `Api/KingdomApiContracts.cs` exactly: marker attribute `[KingdomExtension]`, discovery by
`ModManager.GetInstancesWithAttribute<T>`, `IKingdomExtension.ApiVersion` checked at registration,
refused **by mod name** on drift (`Api/KingdomApiRules.cs:121-171`).

```csharp
/// Declares research nodes at load. Called once, after the data lane has merged.
public interface IKingdomResearchSource : IKingdomExtension
{
    KingdomResearchNode[] Nodes();
}

/// Answers a requirement token in a namespace we do not own.
/// Returns the 7b sentence when unmet, so a refusal always names what would fix it.
public interface IKingdomResearchRequirement : IKingdomExtension
{
    /// The token prefix this source answers, e.g. "theirmod:". Collisions refuse by mod name.
    string Namespace { get; }
    bool Met(KingdomCityReading City, string Token, out string Missing);
}

/// Answers identity questions for vocabularies we do not know (Addendum 17).
public interface IKingdomIdentitySource : IKingdomExtension
{
    /// Extra roster keys this identity mints, e.g. a modded culture's own trades.
    string[] Keys(KingdomIdentityReading Identity);
    /// 100 = no opinion. Clamped to [70,130] by us, never by them.
    int Affinity(KingdomIdentityReading Identity, string WorkKind);
}
```

Under the same five invariants as every other extension (`LIVING-CITY-ARCHITECTURE` §6.6): draws
through `IKingdomDraws` only, frozen reading in / frozen result out through the executor seam,
budget and error isolation per call (**a throwing requirement source refuses its own token and
never the city's pass**), telling through our surfaces under the shared budget, and rows counted
against the memory ceiling by mod name.

Two rules specific to research:

- **`Met` is fail-CLOSED.** An unanswered or throwing token is UNMET, and the node stays hidden.
  This is the deliberate opposite of vanilla's conversation predicates, which fail open
  (`D/XRL/World/Conversations/IConversationElement.cs:381-391`, `Default = true`). A fail-open
  research gate is a visibility-law breach, so ours fails closed and says whose token it was.
- **`Nodes()` cannot mint a `Grants` key in another mod's namespace.** A node may *require*
  anything; it may only *grant* `node:<its own key>` plus keys it declares ownership of. Otherwise
  a third party can unlock our arclight forge by fiat.

### 9.3 Version discipline

`KingdomApiRules.Version` goes 1 → 2; `MinSupportedVersion` **stays 1**. `Judge` refuses only
`DeclaredVersion > Version` and `< MinSupportedVersion` (`Api/KingdomApiRules.cs:131-141`), so every
existing v1 extension keeps loading and only the new contracts need a recompile. Adding contracts
is additive; the version bump exists so a v2 extension cannot silently load against a v1 copy of
the mod. `RefusalLine` already writes the sentence (`:152-171`).

`docs/API.md` gains the `<research>` schema, the three interfaces, the published roster kinds, and
— per Addendum 9 — supersession markers rather than removals when any of it changes.

---

## 10. Performance, determinism, and save

The performance constitution has real headroom but not much: the model composes **53.6 KiB**
against a **56 KiB** warn rung and a **64 KiB** ceiling
(`T/Simulation/City/KingdomBudgetRules.cs:229-278`). Research must be measured in bytes, not
kilobytes.

### 10.1 The memory formula addition

| Row | Bytes | Count | Total |
|---|---|---|---|
| `ResearchHeaderBytes` (current subject key + accrued ticks + last-worked tick) | 48 | 1 per city | 96 |
| `ResearchShelfRowBytes` (shelved node key hash + accrued ticks) | 12 | ≤ 8 per city | 192 |
| `PracticeBytes` (2 × `ushort` per resident, §8.3) | 4 | 135 per city | 1,080 |
| **Total at `CitiesPerRealm = 2`** | | | **≈ 1.34 KiB** |

Against 53.6 KiB composed, that is **+2.5%**, landing at ≈ 54.9 KiB — still under the 56 KiB warn
rung. Held/discovered node state costs **zero** model bytes: held-ness is roster keys in the
existing game-state string, and discovered-ness is journal entries vanilla already pays for.
The registry itself is static and shared across cities — not per-city state, not counted per city.

Add the rows to `KingdomCityMemoryRules` (`T/Simulation/City/KingdomCityMemoryRules.cs:31-97`) so
the existing memory test measures them; a lane the receipt cannot see is a lane with no owner.

### 10.2 No per-turn cost, anywhere

- Research accrual runs on the **settlement pass / breakpoint**, never on the heartbeat and never
  per turn. It is one multiply and one compare against the current subject.
- Quest locks are **event-driven** (`QuestFinishedEvent`), cached, never polled (§5.7).
- Discovery checks are O(1) `StringMap` lookups (`JournalAPI.HasNote`,
  `D/Qud/API/JournalAPI.cs:183-187`) — deliberately **not** the O(n) `HasObservation` scan at
  `:1096-1106`.
- The map is drawn only when opened. Its cost is one pass over the registry (≤ 64 nodes v1) and one
  over the catalogue — the same shape `KingdomTechMap.Locked` already runs.
- **Fragment-in-hand discovery (§6.3 case 2) is the one thing that could be per-turn and must not
  be.** Scan the founder's inventory on the same cadence the disk-teaching screen already uses —
  when the founder is at the seat, and on the settlement pass — never on pickup.

### 10.3 Determinism

Nothing in the loop draws. Accrual is arithmetic over elapsed ticks; ordering is the registry's
authored order; ties break on key, ascending. Where a discovery source must pick *which* node a
rumor reveals, it draws through the kernel's own stream with a `SemanticEventKey`
(`T/Simulation/Kernel/CounterRandom.cs`), so the same city on the same seed reveals the same node
after a reload. `PlannerMaxDraws = 0` is the precedent for lanes that must not draw at all
(`T/Simulation/City/KingdomBudgetRules.cs:243`); research accrual is one of them, and its test
should assert zero draws.

### 10.4 Save

Three additions, all in shapes the mod already persists:

1. **Held nodes** — `node:<key>` entries appended to the existing roster string
   (`The.Game.SetStringGameState`, `T/Growth/KingdomZoning.cs:545-553`). No new key, no new format.
2. **Discovered nodes** — `JournalObservation` entries, persisted by `JournalAPI.Save`
   (`D/Qud/API/JournalAPI.cs:112-133`, called from `D/XRL/XRLGame.cs:2329`). We register them once
   per game and never write them again.
3. **Subject + shelf + practice** — fields on the city state, inside the existing serialization
   version bump. Addendum 9 waives migration pre-release; the bump stays clean and deliberate.

**One real save hazard, named.** `TinkerData.KnownRecipes` is a mutable global with no invariant,
no event, and inconsistent dedup — `Known()` compares by `Blueprint` string while `LearnBlueprint`
compares by reference (`D/XRL/World/Tinkering/TinkerData.cs:293, 350-369`), and five of the seven
learn sites have no dedup guard at all. **We never write to it and never store city state in it.**
Our only read is "does the founder personally know this disk", for prose.

### 10.5 Node-key identity

Node keys are **strings in the save, never ordinals**, for the same reason
`KingdomExtensionVerdict`'s values are appended and never reordered
(`T/Api/KingdomApiRules.cs:8-14`): a mod adding nodes must not renumber ours. The in-memory registry
may index by ordinal for speed; the boundary is always the key. A save referencing a node whose mod
has been removed keeps the key, reports it once as *unknown to this build*, and does not lose it —
the same shape STANDARDS 9 already requires for an unrecognised roster kind.

---

## 11. Loud rejections

Stated as loudly as the brief asks, so nobody re-derives them.

**RR1 — A research POINT CURRENCY. Refused.** Nothing accrues except progress on the one named
subject; there is no pool, so there is nothing to allocate and no optimal allocation. The moment
research becomes fungible it becomes a budget, and DIVERSITY §5 R2's *reason* — which Addendum 14
did not overturn — kills it.

**RR2 — A research QUEUE, or parallel subjects. Refused.** One subject per realm; extra labs add
throughput, not lanes. A queue is a schedule, a schedule is a thing to optimise, and optimising a
schedule is the second job.

**RR3 — Greyed-out unknown nodes, silhouettes, "???" rows, or a total count. Refused.** Vanilla's
own precedent is total omission (§2.8): both tinkering UIs iterate `KnownRecipes` and never render
an unknown recipe at all. Anything that lets the founder *count* what they cannot see is the
visibility law broken by arithmetic.

**RR4 — A percentage, a progress bar, or an ETA. Refused.** Distance is prose, through the
`KingdomTechMapRules.Reach` idiom the map already uses. *"Begun"*, *"one thing away"*, *"within
reach"*. A number invites planning against a timer, which Addendum 8 spent a whole ruling avoiding.

**RR5 — Research that advances in an unstaffed or unsupplied lab. Refused.** Addendum 8 clause 2 is
not negotiable, and `r_KingdomScaffold` already has the shape: idle time is spent, never banked.

**RR6 — `rite:` completing a node. Refused, by Addendum 18 and by load-time validation.** A `rite:`
token in `TaughtBy` is a schema error refused by file and key, not a convention someone can forget.

**RR7 — Research raising `TechLevel`. Refused.** `TechPointsPerOrigin = 0` already establishes that
the craft rung is what the settlement LEARNED and CERTIFIED, not a readout of anything else
(`T/Growth/KingdomZoningRules.cs:216-225`). `PointsForKind("node")` returns 0. DIVERSITY §5 R3
stands unamended.

**RR8 — Writing `Statistic.Max` for a citizen cap. Refused, and it is a landmine.** `_Max` is a
**static dictionary of boxed ints keyed by stat NAME**
(`D/XRL/World/Statistic.cs:142-196`) — one write changes the ceiling for every creature in Qud,
including the player. The citizen cap is ours, enforced in our training code, and touches vanilla
only through `BaseValue`.

**RR9 — Minting bespoke high-stat creatures to recruit. Refused.** `HeroMaker` and the lair-owner
path already populate the world with them (`D/XRL/World/HeroMaker.cs:52-100`,
`D/XRL/World/WorldBuilders/JoppaWorldBuilder.cs:2685-2778`). A spawner turns a journey into a
lottery, and the journey is the game.

**RR10 — A training building whose only output is a number.** Refused. Citizens improve by doing
the work the city already needs done. A practice yard that trains without producing is a numbers
panel with a roof.

**RR11 — Stat DECAY from idleness. Refused.** Addendum 8 clause 1: *time never mints unchosen
debts.* A citizen who stops quarrying stops getting stronger; they do not get weaker.

**RR12 — Building the knowledge ledger on `TinkerData.KnownRecipes`. Refused.** It is a mutable
global with no event, no accessor, no invariant, and inconsistent dedup across its seven writers
(§2.1, §10.4). We read it for prose and never for state.

**RR13 — A fail-open requirement token.** Refused. Vanilla conversation predicates default to true
on an unknown key (`D/XRL/World/Conversations/IConversationElement.cs:381-391`); copying that would
mean a typo'd or unloaded requirement silently *reveals* a node. Ours fails closed and names the
namespace.

**RR14 — Reading or writing achievements as research state. Refused.** `AchievementManager` writes
`Achievements.json` at `DataManager.SyncedPath` — account-wide across every character
(`D/AchievementManager.cs:95-105`).

**RR15 — Balancing anything against `B/Skills.xml`'s prose.** Refused. Reverse Engineer's
description advertises *"25% … 15% per mod"*; the code rolls a single 15% for the whole batch
(`D/XRL/World/Tinkering/Disassembly.cs:206-217, 341-345`). Read the code.

---

## 12. Open questions for the author

Ordered by how much downstream work each unblocks.

**Q1 — Is research per-REALM or per-CITY?** The roster is realm-global today
(`The.Game.GetStringGameState`, `T/Growth/KingdomZoning.cs:545-553`) and Addendum 12(j) schedules a
settlement-record roster for a later wave. §5 assumes **one current subject per realm, all labs
pushing it, knowledge shared realm-wide** — because that keeps "no queue" true and makes a second
city meaningful. The alternative (per-city trees) is more interesting and doubles the surface. This
answer sizes the whole wave.

**Q2 — Is 21 nodes the right size for v1, and are these the right four reaches?** §4 proposes
3 trunk + 6 + 6 + 6. Fewer is thinner; more is more balance surface. Related: should **THE DEEP**
(Addendum 15) be a fourth spine in v1, or wait for the strata wave as §7.6 recommends?

**Q3 — How hard is the Intelligence tier ladder?** §4.0 proposes 10/14/18/22 with tier 4 reachable
only through a savant or a wild recruit. That makes §8.4 load-bearing. If tier 4 should be
reachable by a well-schooled ordinary city, the ladder softens and the recruit becomes flavour.

**Q4 — Does `schooling` (C3) get to raise the Intelligence cap at all?** It is the elegant loop —
research raising the ceiling on the stat that gates research — and it is also the one place a
runaway could hide. §8.3 caps city headroom at +3 per stat / +6 total. Confirm the cap, or cut the
node's Intelligence grant entirely and let the ladder be climbed only by *who you recruit*.

**Q5 — Can the founder set the subject remotely, or only at the lab?** §0.3 rules "at the building,
in the world", which is what keeps the map a reading surface. It also means a founder three
parasangs away cannot redirect their keepers. That may be exactly right, or it may be an annoyance
the charter surface should absorb.

**Q6 — What does a shelved subject cost?** §5.4 remembers accrued work at zero cost, capped at 8
rows. The alternative — losing part-done work on a switch — is more dramatic and invites
save-scumming. Confirm "remembered, free".

**Q7 — Does a `savant:`-revealed node stay revealed after the savant leaves?** §6.3 case 4 says the
reveal is withdrawn (knowledge that walked out the door). That is strong fiction and a strong
feel-bad. The alternative is that discovery is permanent once made and only *completion* is live.

**Q8 — How wide is the identity affinity band?** §7.3 proposes ±30% for culture/species affinity on
worker efficiency. Wider makes demographics decisive; narrower makes them decorative.

**Q9 — Which existing screen hosts "what they are working out"?** §6.4 chapter 2 could live on the
keepers' map, on the city book's technology chapter, or in the charter menu beside the existing
`KingdomTechMap.Draw` entry (`docs/API.md:1153`, entry `8`). One place, chosen once.

**Q10 — Housekeeping, carried forward:** the shipped debug line at
`D/Qud/UI/TinkeringStatusScreen.cs:143` (`TinkerData.KnownRecipes.Where(d => d.Blueprint == "HandENuke").ToList();`,
result discarded) and the Skills.xml/Disassembly.cs description mismatch (§2.6) are *vanilla* bugs,
not ours — noted so nobody balances against either.

---

## Appendix — what was read

**Decompile** (`/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/`):
`XRL/World/Tinkering/TinkerData.cs`, `Disassembly.cs`, `ItemModding.cs`;
`XRL/World/Parts/TinkerItem.cs`, `DataDisk.cs`, `Examiner.cs`, `Description.cs`, `Book.cs`,
`Cookbook.cs`, `TrainingBook.cs`, `MarkovBook.cs`, `MechanimistLibrarian.cs`, `StatOnEat.cs`,
`Nectar_Tonic_Applicator.cs`, `StatBoostMedication.cs`, `Leveler.cs`, `Experience.cs`, `Brain.cs`,
`IBondedCompanion.cs`, `CompanionCapacity.cs`, `Uplift.cs`, `GivesRep.cs`;
`XRL/World/Parts/Mutation/Psychometry.cs`; `XRL/World/Parts/Skill/Tinkering.cs`,
`Tinkering_Tinker1-3.cs`, `Tinkering_ReverseEngineer.cs`, `Tinkering_Disassemble.cs`,
`Tinkering_GadgetInspector.cs`; `XRL/World/Skills/PowerEntry.cs`, `PowerEntryRequirement.cs`;
`XRL/World/Skills/Cooking/CookingGameState.cs`; `XRL/World/Statistic.cs`, `StatShifter.cs`,
`GameObject.cs`, `GameObjectFactory.cs`, `GameObjectBlueprint.cs`, `HeroMaker.cs`, `Quest.cs`,
`QuestStep.cs`, `QuestManager.cs`, `QuestLoader.cs`, `QuestFinishedEvent.cs`,
`QuestStepFinishedEvent.cs`, `SecretVisibilityChangedEvent.cs`, `GetTinkeringBonusEvent.cs`;
`XRL/World/AI/AllegianceSet.cs`, `IOpinion.cs`, `OpinionMap.cs`;
`XRL/World/Effects/Proselytized.cs`, `Beguiled.cs`, `Rebuked.cs`, `BoostStatistic.cs`;
`XRL/World/Units/GameObjectAttributeUnit.cs`;
`XRL/World/ZoneBuilders/VillageBase.cs`, `Village.cs`;
`XRL/World/WorldBuilders/JoppaWorldBuilder.cs`;
`XRL/World/Conversations/IConversationElement.cs`, `ConversationDelegates.cs`,
`ConversationDelegate.cs`, `Expression.cs`, `QuestHandler.cs`, `WaterRitualTinkeringRecipe.cs`,
`IWaterRitualSecretPart.cs`, `LibrarianGiveBook.cs`;
`XRL/XRLGame.cs`, `XRL/The.cs`, `XRL/IGameSystem.cs`, `XRL/WanderSystem.cs`,
`XRL/Collections/StringMap.cs`, `XRL/Rules/Stat.cs`, `AchievementManager.cs`;
`XRL/UI/TinkeringScreen.cs`, `Qud/UI/TinkeringStatusScreen.cs`, `Qud/UI/QuestsStatusScreen.cs`;
`Qud/API/JournalAPI.cs`, `IBaseJournalEntry.cs`, `JournalMapNote.cs`, `EncountersAPI.cs`.

**Game data** (`/home/r/coq/qud_helper/game_base/Base/` — **not** under the decompile tree):
`Skills.xml`, `Mutations.xml`, `Genotypes.xml`, `Options.xml`, `Quests.xml`, `Conversations.xml`,
`Factions.xml`, `Mods.xml`, `ObjectBlueprints/Creatures.xml`, `Items.xml`, `Data.xml`, `Foods.xml`.

**This mod** (`/home/r/work/thousand-and-first/`): `Growth/KingdomZoning.cs`,
`KingdomZoningRules.cs`, `KingdomScaffold.cs`, `KingdomCrewRules.cs`, `KingdomCrews.cs`,
`KingdomWear.cs`, `KingdomYardRules.cs`, `KingdomAdoptRules.cs`, `KingdomLodgingRules.cs`;
`Core/KingdomTechMap.cs`, `KingdomTechMapRules.cs`, `KingdomQolRules.cs`, `KingdomRules.cs`;
`Experience/KingdomGuestRules.cs`; `Api/KingdomApiContracts.cs`, `KingdomApiRules.cs`,
`KingdomCityReading.cs`; `Simulation/City/KingdomBudgetRules.cs`, `KingdomCityMemoryRules.cs`;
`KingdomBuildings.xml`, `PopulationTables.xml`, `Books.xml`, `Options.xml`, `STANDARDS.md`,
`MODDING.md`, `docs/API.md`.

**Notes:** `_notes/BUILDING-CATALOGUE-BRIEF.md` (Addenda 8-18), `_notes/DIVERSITY-AND-TECH-TREES.md`
§§1-2, 5-6, `_notes/LIVING-CITY-ARCHITECTURE.md` §6.6, `_notes/EVOLVING-HEART-RESEARCH.md` §4,
`_notes/RECIPE-PROJECTION-AUDIT.md` §§3-5.
