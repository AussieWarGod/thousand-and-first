# End-state cities: flesh, chrome, shell — and the capital

**Status:** research return for Addendum 19 (Q6 + Q7 interaction) and the Capital ruling
(`BUILDING-CATALOGUE-BRIEF.md`, tail). Research-first, no code. Options + recommendation for the
author's final ruling.

> **Current v1 supersession, 2026-08-27.** The live realm cap is two cities. Any larger-purpose
> mesh below is research, not current scope. The controlling v1 model is the reciprocal compatible
> pair graph in `BUILDING-CATALOGUE-BRIEF.md` and `../VISION.md`: Deep-Bore, Great Foundry,
> Granary-Colossus, theatre, and becoming annexe; first-work ordinary bootstrap, second-work exact
> input, and exact return cargo before first-work activation.

**Method:** praise-first, evidence-cited. Every load-bearing claim carries a `file:line`, an exact
blueprint/attribute name, or a URL. Anything not read directly is marked **INFERRED** or
**UNVERIFIED**. Runtime behaviour is never asserted from source alone (`_notes/README.md` standing
rule, restated after the C2 correction).

**What changed mid-research.** The author ruled while this was in flight:

> "Maybe the capital is special and can have a couple of extra megastructures that are capital
> specific, but other cities are restricted to 1 megastructure, and it generally serves a purpose
> (like a big drill, a huge foundry, or something)."

That collapses the original three-way question. The kingdom-of-specialised-cities shape is
**confirmed**, not proposed. What remains open, and what this document answers:

1. the **megastructure-as-purpose portfolio** — which colossi are city-defining, which are
   capital-only, and where flesh and chrome land in it (§4);
2. **what makes a city the capital** (§5);
3. the **lock-in question, re-scoped to per-city** — the kingdom composes purposes, so no path is
   denied across the realm, only in a given place (§3, §6);
4. the **chrome annexe's fiction** — "a reason in the world, not a checkbox" (§2, §7).

---

## 0. The verdicts, up front

| Question | Verdict |
|---|---|
| Is mutant-chrome a lore violation in the Qud community? | **No — and the evidence is not close.** At least four published Workshop genotype mods do it; the community's own framing is "semi lore friendly, mostly balanced *excuse*"; the recurring objection is **balance, never lore**. §1. |
| Does vanilla itself cross the boundary? | **Yes, three separate ways, all in shipped data or source.** A mutant NPC described in chrome prose (`Psyberneticist`); three factions that hand mutants cybernetic credit at the water ritual with no genotype check; and an **overridable `IsTrueKinEvent`** whose only purpose is to let something other than birth decide the answer. §2. |
| Is "chrome is for the pure" a defensible in-world position? | **It is the position of the game's genocidal antagonists.** The Putus Templar sit at **-700** base reputation to mutated humans, second-lowest in the game. A mutant taking chrome refutes the Templar; it does not violate Qud. §2.4. |
| Should the flesh theatre and the chrome annexe be megastructures? | **Yes — both, under the confirmed one-per-city rule.** They are the two halves of one doctrine (what a city does to bodies), and making them the city's purpose is exactly what the capital ruling asks megastructures to be. §4. |
| Does the kingdom need a separate city to support both ways? | **Yes, and that is the feature, not the tax** — provided the three named conditions in §6.4 hold. §6. |
| What makes a city the capital? | **The crown — a building the founder raises, movable by raising it elsewhere.** Civ IV/V's Palace rule is the only designation rule players actually praise, and every complaint in the corpus is about *who chooses*, never about the capital being special. **It must NOT be called `Seat`** — that name is already taken in our own code for the currently-loaded city. §5. |
| Is one-per-city safe as a rule? | **Yes — uniqueness limits are the accepted kind.** Zero complaints found about Anno's one-Palace-per-save, Civ VI's one Government Plaza, Endless Legend's one-city-per-region or Songs of Syx's one city. What players resent is a cap on **expansion** (how many cities/islands they may hold), which 12(j) already declines to impose. §3.6. |
| What is the real risk, then? | **Not lock-in. Two other things:** the **commute** between specialised cities (answer: ship the mirror-gate first, §4.4) and the **saturation ceiling** — one-per-city is a finite checklist that completes (answer: make the megastructures consume each other's output, §4.6). §7.3. |

---

## 1. The sentiment map — Qud community on the chrome/genotype boundary

### 1.1 Headline

**The boundary is defended as a BALANCE line, not a LORE line.** Every objection found in the wild
is about power ("best of both worlds is too strong"), and every lore discussion found is *curious*
rather than *offended* — players ask "why does that NPC have a robot arm?", get faction-lore
answers, and move on. The camp that would call mutant-chrome a lore violation did not surface in
any thread read for this document. Originally a negative finding about Steam, the wiki, and
Workshop tabs only; **the Reddit gap has since been closed and the finding held** — seven
r/cavesofqud threads, ~90 comments, zero lore objections, and the community's own top-voted
explanation of the boundary is the registry reading (§1.7).

### 1.2 Thread-cluster A — "mutants with cybernetics?" (the lore question, asked directly)

The only found thread that asks the boundary question head-on
([Steam, General Discussions](https://steamcommunity.com/app/333640/discussions/0/7130962134695094195/)).
User *flemingluiz* asks why some Mechanimist and gutsmonger tiles have cybernetics like bionic arms
"despite being mutants", finding it strange.

What the thread does **not** contain is instructive:
- no accusation of lore-breaking;
- no "that shouldn't exist";
- instead, two *in-fiction* reconciliations offered immediately —
  *Glass Zebra* proposes Mechanimists are "closest to True Kin genetically" and could craft crude
  prosthetics, and separately maps Mechanimist **factional schism** (Stiltgrounds sacrifices
  artifacts and kills robots; the Bethesda Susa faction works with shovelers; Jacobo's faction shows
  no opposition to technology use);
- *I blame Earthshaker* raises the doctrinal tension — "cast off the chrome and throw it down" —
  then adds the deflationary read: **"On a more practical gameplay note, it's probably just a bug."**

**Reading:** the community's instinct when it meets mutant-chrome is to *explain it in-world*, and
its fallback is "bug", not "heresy". A mod that supplies a real in-world reason is pushing on an
open door. No consensus was reached in the thread — the question is live and unclosed, which is
exactly the room a fiction can occupy.

### 1.3 Thread-cluster B — the mods themselves (revealed preference)

**At least four published Steam Workshop mods grant mutants cybernetics.** This is the strongest
sentiment evidence in the whole document, because it is behaviour rather than opinion.

| Mod | What it does | Signal |
|---|---|---|
| [Mechanimist Genotype](https://steamcommunity.com/sharedfiles/filedetails/?id=2613407133) (`2613407133`) | Cybernetic genotype, nine subtypes, cybernetics at chargen | **1,729 subscribers, 272 favourites, 6,347 unique visitors, 79 ratings at ~4 stars, 130 comments.** Also on [Nexus](https://www.nexusmods.com/cavesofqud/mods/14) |
| [Mutated Kin](https://steamcommunity.com/sharedfiles/filedetails/?id=2908962625) (`2908962625`) | "a new genotype called Mutated Kin, which is able to use both mutations and cybernetics" | **Installed in the author's own game** — `/mnt/f/SteamLibrary/steamapps/workshop/content/333640/2908962625/` |
| [Hybrid: Cybernetic Mutants](https://steamcommunity.com/sharedfiles/filedetails/?id=3076962310) (`3076962310`) | "Can choose a starting cybernetic — Can use Becoming Nooks" | Explicitly opens the Nook to mutants |
| [Cyber Mutant - Unstable Genome](https://steamcommunity.com/sharedfiles/filedetails/?id=2424913452) (`2424913452`) | "Cyber Mutant's qualify as True Kin and have mutations but do NOT have the ability to Rebuke Robots" | Pays a named price for the crossing |

Adjacent, on the **flesh** side: [Limbsmith](https://steamcommunity.com/sharedfiles/filedetails/?id=2645492028)
(`2645492028`, BadMojo, Nov 2021, last updated Jun 2024) — "adds a wish allowing comprehensive
control of your limbs. Balance your chimeric sprawl, sprout a second back, become a plant." The
chimeric-theatre fantasy is *also* already a shipping community appetite.

**The community's own words for the fantasy:**
- Mechanimist Genotype describes itself as "**a semi lore friendly, mostly balanced excuse to have
  access to both cybernetics and mutations**". The word *excuse* is the tell: modders know the
  fantasy is wanted and know it needs a reason. **We are being handed the exact brief the author
  already stated — "a reason in the world, not a checkbox."**
- Mutated Kin is praised as "**a perfect low-key implementation of a 'both sides' genotype**"
  providing "mutant stats with cybernetics unlocked."
- Mechanimist Genotype's own lore hook is the psyberneticist: "**that psyberneticist tile has a
  robot arm**".

**Reception pattern across all four:** the comment tabs are dominated by **technical** complaints
(XML errors for `RandomGrossLiquid`/`ooze` blueprints, invalid faction references `Playerhater`/
`Duto Ko`/`Vanta Knight`, conflicts with Jademouth / Chiropterans / Bestiary Expanded, an unfindable
quest NPC "Xolotl") and **balance** commentary. Praise is for the lore content and the hard early
game. **No lore objection to mutant-chrome was found in any comment tab read.**

### 1.4 Thread-cluster C — the balance camp (the real objection)

This is where the heat is, and it is heat we should take seriously because it maps onto the mod's
own R4.

- Mutated Kin's **own author** writes the warning into the store page: "It is balanced around the
  default mutant genotype, **as such it may prove to be a bit strong later in a run.**"
- The general "both sides" framing is contested on power-curve grounds: some players argue it is not
  worth giving up mutations for cybernetics, since True Kin "start out strong but have to work twice
  as hard as a mutant to stay competitive."
- The vanilla genotype debate itself is a live power-curve argument
  ([Steam](https://steamcommunity.com/app/333640/discussions/0/4334231305371600921)): True Kin's
  early game is "such a slog trying to find the right parts"; a defender notes mutants "get to shoot
  mind lasers at baboons" while True Kin grind stats; another prefers mutant identity outright —
  "Being a bizarre mutant … and making it work anyways feels like the real game."

**Reading:** the objection to mutant-chrome is *"that character is too strong"*, and it is answered
by **cost**, not by refusal. This is the same conclusion R4 already reached from the Playable Slime
receipt, arrived at independently from a different mod family. It is now doubly evidenced.

### 1.5 Thread-cluster D — the accidental-crossing thread, and its transferable lesson

The most operationally useful thread found
([Steam](https://steamcommunity.com/app/333640/discussions/0/3728449612308825510)) concerns
**Cybernetic Rejection Syndrome (CRS)**.

- CRS is **dormant base-game code** activated by the developer's own Sifrah mod. As one commenter
  puts it: "**Sifrah has code in the base game that is just not active.**"
- Players running **Limbsmith and Mutant Kin — "which allow mutants to use cybernetics"** — hit CRS
  debuffs **that persisted after the implants were removed**.
- The player verdict, verbatim and worth pinning to the wall:
  > "**a permanent debuff on arguably the most important stats in the game from a reversible action
  > is a serious kick in the balls.**"

**This is the single most transferable sentiment finding in the document.** It is a *precise*
statement of the failure mode the becoming annexe must avoid: the cost of chrome-in-a-mutant-body
must be **paid at the door, visibly, and be reversible in kind**. A hidden permanent penalty
attached to a reversible act is the thing players actually hate here — and it also violates
Addendum 8 (rates are time × labour × infrastructure) and the "bonus for engaging, never penalty for
abstaining" pillar. See rejection **X3** in §9.

### 1.6 Thread-cluster E — the purity camp, and who holds it in-fiction

The purity position exists in Qud and is **authored as villainy**.

- [Putus Templar](https://wiki.cavesofqud.com/wiki/Putus_Templar): "one of the two main antagonistic
  factions … on a crusade to reclaim Qud from a supposed 'mutant threat'." Base reputation to
  mutated humans is **-700, the second lowest in the game after the Girsh's -800.**
- Community framing of the faction is explicit that "their goals are eugenics and genocide, and
  their means are extermination and subjugation."
- Shipped data agrees on both sides of the mirror: `Genotypes.xml:17` gives Mutated Human
  `<extrainfo>-600 reputation with {{C|the Putus Templar}}</extrainfo>`; `Genotypes.xml:33` gives
  True Kin `<reputation With="Templar" Value="600" />`.
- And the Templar are the **only** creature family in shipped data that carries cybernetics:
  `Creatures.xml:6762, 6785, 6817, 6848` (`CyberneticsHasRandomImplants`), `:15179`
  (`CyberneticsHasImplants Implants="PenetratingRadar@head"` on a blueprint tagged
  `<tag Name="Genotype" Value="True Kin" />`).

**Reading, and it is the fiction's spine:** *in vanilla Qud, chrome is the sacrament of the people
who want mutants dead.* Granting chrome to a mutant is therefore not a transgression against Qud's
fiction — **it is a transgression against the Templar**, which the game already frames as the right
side of that argument. The annexe does not need to apologise. It needs to be *dangerous*.

### 1.7 The Reddit read — §8's largest gap, closed (2026-08-22)

The gap stated in §8 — "the finding 'no lore objection to mutant-chrome was found' is a finding
about Steam, the wiki, and Workshop comment tabs, not about the whole community" — is now closed.
Route: the author's Reddit MCP server (Arctic Shift archive; scores may lag live Reddit). Seven
r/cavesofqud threads read in full, ~90 comments, spanning 2020–2025. **The headline survives
contact with Reddit, and comes back stronger than it went in.** Zero lore objections. The camp
§1.1 could not rule out does not exist in the found record.

**R-A. The direct question, asked head-on, answered as *policy*.**
["why can't mutants use cybernetics?"](https://www.reddit.com/r/cavesofqud/comments/xwet30/why_cant_mutants_use_cybernetics/)
(Oct 2022, 0.93 ratio) is the Reddit twin of §1.2's Steam thread, and its answer stack is the
single best sentiment result in either corpus:

- Top answer, **42 points** — twice the thread's own score:
  > "**Mutants are not aristocrats.** The Eaters constructed their becoming nooks to detect and
  > reject mutant genomes."
  ([comment](https://www.reddit.com/r/cavesofqud/comments/xwet30/_/ir60hgn/)) — *aristocrats*: a
  **caste** word, not a species word. The refusal belongs to the machine and the dead polity that
  built it.
- Second, 23 points: "Maybe **this tells you something about the people who built the becoming
  nooks**" ([comment](https://www.reddit.com/r/cavesofqud/comments/xwet30/_/ir6022c/)) — the
  boundary read as *characterisation of the Eaters*, not as physics.
- Third, 14 points: "I've worked on printers. **I think machines can be racist.**"
  ([comment](https://www.reddit.com/r/cavesofqud/comments/xwet30/_/ir6wpvd/)).
- The lone biological reading in the thread (6 points) is per-mutation — regeneration would push
  the implant out — and the thread *ends in upvoted mod recommendations* for crossing.

The community's own top-voted explanation of the boundary **is F1**: an authorization check by a
machine enforcing a dead aristocracy's enrollment policy. §2.3's `IsTrueKinEvent` reading did not
have to be argued to Reddit — Reddit already holds it.

**R-B. The registry vocabulary, volunteered unprompted.**
["lore question"](https://www.reddit.com/r/cavesofqud/comments/10qq1b6/lore_question/) (Feb 2023,
1.0 ratio) asks the F1 question verbatim — *"how do the implant machines differentiate between me
and a true kin?"* — and the answers supply the fiction's whole vocabulary:

- 17 points: "…a **specific marker gene that says 'this person is an aristocrat'**"
  ([comment](https://www.reddit.com/r/cavesofqud/comments/10qq1b6/_/j6s53oh/)) — the record,
  written into the body as a credential.
- 7 points: the machines "check for **'NET-terminal gene'**… like in Blame!"
  ([comment](https://www.reddit.com/r/cavesofqud/comments/10qq1b6/_/j6relkv/)) — the community's
  spontaneous reference is the *strongest fictional precedent for record-over-blood in the genre*:
  in Blame! the city's machines still check a credential humanity has lost, and the whole tragedy
  is enrollment, not biology.
- Another commenter cites the nook's own greeting — "**WELCOME ARISTOCRAT**" — as caste
  recognition ([comment](https://www.reddit.com/r/cavesofqud/comments/10qq1b6/_/j6xufvo/)).
- A third *invents the forgery fiction unprompted*, proposing a mod mutation that is "**a gene
  sequence to trip up the nook's filters**"
  ([comment](https://www.reddit.com/r/cavesofqud/comments/10qq1b6/_/j6srtux/)).
- The thread's one essentialist — mutants "no longer even in the same genus" (11 points) — closes
  by hoping the vanilla workaround is *never patched*: "That doesn't sound terribly fun, so I hope
  they don't" ([comment](https://www.reddit.com/r/cavesofqud/comments/10qq1b6/_/j6rzb2z/)). Even
  the purity reading votes against enforcement.

**R-C. The theme, read by the room.** In
[the Eaters thread](https://www.reddit.com/r/cavesofqud/comments/tjczm8/question_about_true_kin_and_the_eaters/)
(Mar 2022): "I think a big part of the point of this game is **blurring the boundary between
human/non-human. Are genetic mutations really that different from cybernetic enhancements?**"
(9 points, [comment](https://www.reddit.com/r/cavesofqud/comments/tjczm8/_/i1jkdsh/)). The
thread's top answer (22 points) says True Kin "are **(or are believed to be)** the descendants" of
the aristocratic class ([comment](https://www.reddit.com/r/cavesofqud/comments/tjczm8/_/i1ji3jk/))
— record-scepticism in passing — and a third notes the Eaters themselves were "very
trans-humanist… changing their form at will"
([comment](https://www.reddit.com/r/cavesofqud/comments/tjczm8/_/i1jip72/)): the purity doctrine
is a *later invention*, not ancestral practice.

**R-D. A finding Steam never surfaced: the boundary is already one-way, and the community knows
it.** From [the True Kin balance thread](https://www.reddit.com/r/cavesofqud/comments/1hay731/is_it_me_or_true_kin_feels_kinda_underwhelming/)
(Dec 2024, 70 comments):

- "**Truekin can get mutant abilities, there is no way for mutants to get implant abilities.**"
  ([comment](https://www.reddit.com/r/cavesofqud/comments/1hay731/_/m1dkmtc/))
- "There are ways to get mutations as a True Kin, but Mutants are permanently locked out of
  cybernetics. **That's enough for me to always play them** [True Kin]."
  ([comment](https://www.reddit.com/r/cavesofqud/comments/1hay731/_/m1cl2k8/))
- "…you can cheese Gamma Moths to get mutations as Truekin to end up with **the best of both
  worlds**" ([comment](https://www.reddit.com/r/cavesofqud/comments/1hay731/_/m1d0trp/)).

Vanilla's wall has a door in it and the door swings one way: gamma moths and brain brine carry
mutations *into* True Kin bodies; nothing carries chrome the other way. **The becoming annexe does
not breach a sacred boundary — it symmetrises an asymmetry vanilla already tolerates.** The same
quotes carry the caution: per the second voice, chrome-exclusivity is *the* remaining reason to
pick True Kin at all, so R4's cost-not-refusal answer is not optional politeness — it is what
keeps the last exclusive worth having. One rung of the annexe priced too cheap deletes a genotype.

**R-E. The routes players already walk, and who built them.** Every crossing thread converges on
the same three routes: Dominate a Templar (vanilla, body-swap —
[thread](https://www.reddit.com/r/cavesofqud/comments/ujd35h/mutant_with_cybernetics/), 0.96
ratio, all how-to, no objection); precognition + gamma moths (vanilla, TK-side); and the Sifrah
mod's nook-hacking. On that last: §1.5 already identified Sifrah as **the developer's own mod**,
and in the Domination thread its author *personally recommends it as the mutant-chrome route* —
"my mod Sifrah… makes it possible (though difficult) for mutants to hack becoming nooks and
install cybernetics" (top comment, 12 points,
[comment](https://www.reddit.com/r/cavesofqud/comments/ujd35h/_/i7inm35/)). §8's unclosed question
— whether Freehold considers the boundary sacred — now has a revealed-preference answer, if not a
stated one: **a Freehold developer ships and recommends the door.** The frame is hacking the
*authorization* — the body never objects, only the ledger does — with the crossing priced
("cybernetics rejection syndrome"). F1's fiction and R4's cost, already live in the wild, from the
developer's own hand.

One negative verified while reading: a commenter's "Genetic Resonance Tonic — makes you an
aristocrat for 10 turns" is **not vanilla** — no such blueprint in the 2.0.211.51 decompile; it is
the Equity Tonic mod's item. Recorded so nobody mistakes it for a vanilla route. (Note its idiom
anyway: *makes you an aristocrat* — enrollment language again, even in a mod's design.)

---

## 2. The chrome annexe's fiction — found in shipped data, not invented

The author's requirement is "a reason in the world, not a checkbox." Four independent reasons were
found in vanilla itself. Any one of them would carry the building; together they make the annexe
feel *overdue* rather than *added*.

### 2.1 Vanilla already ships a mutant rebuilt with machinery — and calls it a psyberneticist

`B/ObjectBlueprints/Creatures.xml:5404-5420`:

```xml
<object Name="Psyberneticist" Inherits="BaseSightless">
  <part Name="Brain" Hostile="false" Factions="Mechanimists-100" />
  <part Name="RandomMutations" Mental="1d3" MentalLevel="1d4" Physical="0" PhysicalLevel="0" />
  <part Name="Description" Short="=pronouns.Possessive= tendons were wound over ragwheels and
    sheathed in fiberglass. A bio-clock metronome causes =pronouns.possessive= muscles to spasm and
    eyes to snap at regular intervals, leveling out the intrusive Song, and recentering the
    concentration of =pronouns.possessive= mutant mind." />
  <tag Name="Culture" Value="Mechanimist" />
</object>
```

Its parent `BaseSightless` (`Creatures.xml:971-983`) carries
`<tag Name="Genotype" Value="Mutated Human" />` at `:981` and `<tag Name="Culture" Value="Sightless Way" />`
at `:980`. So the psyberneticist is, in shipped data, **a Mutated Human of the Sightless Way who
took Mechanimist culture and had their body rebuilt with machinery** — ragwheels, fiberglass, a
bio-clock metronome — *in service of the mutant mind*.

Five of them stand guaranteed in the **Temple of the Rock in Bethesda Susa**
([wiki](https://wiki.cavesofqud.com/wiki/Psyberneticist)); more appear as random encounters through
most of Bethesda Susa. Their tile has the robot arm the modders keep pointing at.

**The critical detail: the blueprint carries no `CyberneticsHasImplants` and no
`CyberneticsBaseItem`.** Vanilla wrote the *fiction* of a mutant remade by machinery and left the
*mechanics* unbuilt. **The becoming annexe builds the mechanics for a fiction Freehold already
wrote.** That is the strongest possible answer to "a reason in the world": the reason is a named
NPC order that already exists, in a named temple, in a named city.

### 2.2 Vanilla hands mutants the currency and locks the door

Three shipped factions grant `CyberneticsCreditWedge` at the water ritual **with no genotype check
whatsoever** (`B/Factions.xml`):

| Faction | Grant |
|---|---|
| **Robots** (`~:630-634`) | `Skill="Discipline_IronMind" Items="0-1" ItemBlueprint="CyberneticsCreditWedge"` |
| **Naphtaali** (`~:953-957`) | `AltBehaviorPart="Robot" AltItems="1d3-2" AltItemBlueprint="CyberneticsCreditWedge" AltSkill="Customs_Tactful"` |
| **Putus Templar** (`~:1263-1266`) | `Items="1" ItemBlueprint="CyberneticsCreditWedge"` |

The wedge itself is genotype-blind — `D/XRL/World/Parts/CyberneticsCreditWedge.cs` is a bare credit
counter (`public int Credits;` plus display handlers, nothing else). The gate lives entirely at the
terminal: `D/XRL/UI/CyberneticsTerminal.cs:481-487`

```csharp
public bool IsAuthorized(GameObject Object)
{
    if (HackActive) { return true; }
    return Object?.IsTrueKin() ?? false;
}
```

**A mutant founder who shares water with the Putus Templar walks away holding a credit wedge they
can never spend.** Vanilla ships the key and locks the door, and has done for years. The annexe is
where the wedges finally mean something — which means the building's *reward* is already sitting in
players' inventories before the building exists. That is a gift.

Note also `Chavvah`'s `RecipeGenotype="!True Kin"` (`Factions.xml:~1816`) — vanilla's own idiom for
**genotype-negated grants**, already cited in `DIVERSITY-AND-TECH-TREES.md`. Genotype-shaded
benefits are the game's grammar, not our invention.

### 2.3 Freehold shipped an extension point whose only purpose is to let something other than birth decide

This is the decisive technical finding, and it changes the standing C2 posture.

`D/XRL/World/GameObject.cs:10560` — `IsTrueKin()` does not read a tag. It dispatches:

```csharp
public bool IsTrueKin() { return IsTrueKinEvent.Check(this); }
```

And `D/XRL/World/IsTrueKinEvent.cs` `Check(...)` starts from the genotype flag
(`Object?.genotypeEntry?.IsTrueKin == true`) and then **fires two overridable events** — the legacy
`Event.New("IsTrueKin")` with a settable `IsTrueKin` flag, and the pooled `IsTrueKinEvent` — each of
which may **rewrite the answer** before it is returned.

**Freehold built a sanctioned door for exactly this question.** A part on the player can answer
`IsTrueKinEvent` and change the verdict, with no genotype rewrite, no data hack, and — crucially for
`ECOSYSTEM-COMPAT-AUDIT.md`'s posture — **no Harmony patch of anyone**.

Two further facts sit alongside it:

- `B/Genotypes.xml:4` — the **Mutated Human** genotype's own
  `CharacterBuilderModules="XRL.CharacterBuilds.Qud.QudCasteModule,XRL.CharacterBuilds.Qud.QudCyberneticsModule"`
  already names the **cybernetics** builder module, and `:20` gives **True Kin** the **mutations**
  module. Freehold crossed the module lists on purpose. *(**INFERRED** that this is meaningful
  rather than an inert shared list — the modules very likely no-op at zero license points. Not
  runtime-verified. Cited as corroboration only, never as the load-bearing claim.)*
- `D/XRL/UI/CyberneticsTerminal.cs:71` — the slot budget is
  `Subject.GetIntProperty("CyberneticsLicenses")`, an **int property on the object**. Not a genotype
  lookup. Granting a mutant licenses is a property write.

**Revision to C2, stated plainly so it is not smuggled.** The C2 correction withdrew the claim that
vanilla lets a non-True-Kin use the Nook, and ruled that a city granting cybernetic eligibility to a
mutant "is a genuinely new thing and must be argued on its merits." Two things now qualify that:

1. `IsAuthorized` returns `true` unconditionally when `HackActive` (`:483`), and
   `GetAuthorizedSubjects` (`:615`) admits **any allied creature** that passes `IsAuthorized`. On a
   plain source read that means **a hacked Becoming Nook authorizes a mutant.** This is
   **SOURCE-PROVEN, RUNTIME-UNVERIFIED** and must be playtested before anyone leans on it. It does
   not make the annexe redundant; it makes the annexe *the legitimate version of a thing players
   already do with a crowbar*.
2. Whether or not (1) survives playtest, `IsTrueKinEvent` means the boundary is **adjudicated, not
   architectural**. The merits argument is now easy: we are not forcing a door, we are using the
   hinge Freehold installed.

### 2.4 Vanilla's chrome religion is *already* a mutant religion, and it is about debt

The Mechanimists — "mainly comprised of mutant humanoids"
([wiki](https://wiki.cavesofqud.com/wiki/Mechanimists)) — have a liturgy of chrome, in `B/Books.xml`:

- `:117` — "Praise Dagon, the rhetorical, who speaks truth to chrome."
- `:165` — "Unburden yourself from the weight of your **chrome guilt**."
- `:170` — "Repay that **debt**, lightseeker! **Offer your chrome to Shekhinah!**"
- `:171` — "Cleanse it of your guilt, your burdens. **Throw it down the Sacred Well.**"
- `:184` — "In the name of Dagon, speak chrome's prayer and unholster your lightrods!"
- `:582` — the "Monastery of the Illustrious **Heart of Chrome**"

(The Steam poster's half-remembered "cast off the chrome and throw it down" is this Sacred Well
liturgy. Verified against shipped text; the exact phrase does not appear, the doctrine does.)

**Chrome in Qud is not a purchase. It is a debt you carry and may be asked to give back.** That is a
gift to Addendum 8's cost doctrine and to the creed system: the annexe is not a shop.

### 2.5 The four fiction candidates, ranked

Any of these satisfies "a reason in the world". They are not mutually exclusive; the strongest
design uses **F1 as the building's identity and F2 as its reward hook.**

| # | The fiction | Rests on | Strength |
|---|---|---|---|
| **F1 — The registry** | True Kin is a **matter of record**, not of blood. The Eaters' arcologies kept rolls; the Nook refuses a mutant because the mutant is not *on the rolls*, not because the body rejects the chrome. The annexe is the city keeping its own rolls — the first polity of the post-injunction age asserting the right to say who is enrolled. | `IsTrueKinEvent` being an overridable adjudication (§2.3); `IsAuthorized` being an authorization check, not a biology check | **Strongest.** Turns a genotype violation into a *sovereignty* claim — which is this mod's entire thesis. Also the cleanest code. |
| **F2 — The unspendable wedge** | Your founder has been carrying Templar credit since the water ritual. The annexe is where it finally spends. | `Factions.xml` Robots/Naphtaali/Templar grants + terminal gate (§2.2) | **Best reward hook.** The payoff pre-exists the building. Pairs with F1 rather than competing. |
| **F3 — The psyberneticist chair** | The annexe is staffed by a lodged **psyberneticist** out of Bethesda Susa — tendons wound over ragwheels — who does to your citizens what was done to them. The Sightless Way method, the Mechanimist culture, the mutant mind kept central. | `Creatures.xml:5404-5420`, `:971-983` (§2.1) | **Best flavour, and it staffs the building** — it reuses the lodged-savant machinery the theatre already needs (Addendum 4b), and gives the annexe a *named notable* per Addendum 6. |
| **F4 — The debt** | Chrome is borrowed from Shekhinah. The annexe's price is a standing obligation, and the Mechanimists will come to collect. | `Books.xml:165,170,171` (§2.4) | **Best creed friction**, weakest as a standalone charter. Use as the annexe's `Refuses`/petition layer, not its reason for existing. |

**Recommended fiction: F1 + F2, staffed by F3, with F4 as the creed friction.** The one-line pitch:
*the Becoming Nook asks whether you are on the Eaters' rolls; the annexe is your city writing its
own.*

---

## 3. The lock-in question, re-scoped to per-city

The capital ruling changes what we must defend. We are **not** denying the player a path across the
run. The kingdom composes purposes; only a *place* is committed. The sentiment evidence for that
shape is markedly better than for run-level exclusivity — and the failure modes are different ones.

### 3.1 Headline

**Per-site exclusivity with realm-level composition sits in the well-liked zone.** Almost all
"curse" sentiment attaches to *run-level* denial ("I must replay to see the other half"). Almost
none attaches to *site-level* denial where the realm still gets everything. The complaint register
shifts from **injustice** to **logistics** — a far cheaper complaint to hold, and one we can design
against directly.

The counter-worry — "if the realm gets everything anyway, the choice stops mattering" — was
**searched for specifically and not found** in the strategy-game corpus. The nearest thing is Soren
Johnson's critique of tile-assignment as "clumsy, fiddly, and unnecessary" where "it's usually
obvious which tiles are optimal"
([Old World designer notes](http://www.designer-notes.com/old-world-designer-notes-6-citizens-and-specialists/)) —
which is aimed at **fake** choices (a dominated option dressed as a decision), not at per-site
exclusivity. **This is absence-of-evidence, and it is stated as such.**

### 3.2 The praise, and the closest structural cousins

**Civ VI districts are our exact shape** — limited slots per city, so each city becomes a thing,
while the empire gets everything. Lead producer Dennis Shirk, on the record:

> "This forces players to make specialization decisions, because they can't have every District in
> every city."
> "You now have to think long and hard about which city is your best science city, which is your
> main commercial hub, and which is the cultural center of the empire."
> — [Game Developer](https://www.gamedeveloper.com/design/designing-i-civilization-vi-i-s-distinctive-districts-system)

And the geography link, which we must copy: "your city specialisation will count based on where you
place that city: mountains mean you're going to be faith heavy or science heavy; if you're near a
coast it'll be a harbour or commercial hub"
([PCGamesN](https://www.pcgamesn.com/civilization-vi/civilization-6s-new-world-order-hes-always-a-dick-but-now-he-might-be-a-dick-somewhere-else)).

**But the player outcome contradicts the designer's intent, and this correction is load-bearing.**
Civ VI's cap is population-gated — one district per three population — so it *delays* rather than
*denies*. Players report that it produced the friction and none of the identity:

> "you want mostly the same districts in every city. all the small bonuses adds up and that how you
> get going in this game" — *fuzion2100*, [Steam](https://steamcommunity.com/app/289070/discussions/0/3378285310310013020/)
> "There is not so much to gain by specialising. You might as well build as much as you can."
> — *ezzlar*, [CivFanatics](https://forums.civfanatics.com/threads/a-district-for-every-city.634423/)
> "your cities don't feel unique like they were in Civ 4. You don't build a campus in your
> specialized science city like I was hoping" — *Arms Longfellow*,
> [CivFanatics](https://forums.civfanatics.com/threads/city-specialization-is-not-where-it-needs-to-be-how-to-fix-this.615409/)

**DELAY IS NOT DENIAL.** This is the sharpest single lesson available to us, and it cuts *against*
any soft version of the cap: if a flesh-city can eventually also raise the annexe, we get Civ VI —
all the friction, none of the identity. It is direct evidence **for** Design B's hard cardinality and
**against** Design C's coefficients.

The counterfactual confirms it. Civ VII loosened the constraint and players complained immediately:
"good cities don't specialize - they just have everything"; "This has felt like a major step
backwards compared to the puzzle of putting a VI city together"
([CivFanatics](https://forums.civfanatics.com/threads/city-building-a-bit-reapititive.695611/)).
So Civ VI's *constraint* was wanted; its *softness* was the failure.

**Anno 1800 is the closest cousin and is not controversial.** "most people have 1 major island in
which everything is supplied, the rest act as 'production islands' specialized in a few goods"
([Steam](https://steamcommunity.com/app/916440/discussions/0/5715697731005455872/)); another player
moves "iron/coal/steel off my main island to lessen pollution there"
([Steam](https://steamcommunity.com/app/916440/discussions/0/1651044588034767632/)). The reason is
**diegetic** — pollution, fertility, what the ground holds. The island's ground justifies its
destiny.

**Satisfactory's most-circulated build essay argues the case positively:** "Factories which produce
many things rarely do those things efficiently. They devote more space to routing parts around
pieces of the factory which do not use those parts… They are difficult to observe and balance"
([aphyr](https://aphyr.com/posts/351-a-satisfactory-way-of-building)). Specialise the sites, then
invest in the network — and the network is the thing players build cathedrals to.

### 3.3 The curse — the named failure modes, all avoidable

**(1) THE COMMUTE.** The clearest evidence is Fallout 4, and the decisive datapoint is that *the
same player* reports both sides:

> "it's becoming a chore to travel from settlement to settlement to keep them all happy, safe,
> prosperous and fed"
> …and, from the same user…
> "walking between settlements is not a waste of time since the map is packed with stuff to do that
> if u did not walk between settlements you would never know of"
> — [Steam](https://steamcommunity.com/app/377160/discussions/0/492378265890123054/)

**The complaint is never distance. It is repetition of a journey whose content is exhausted.**

Anno's answer is the single most transferable finding in the comparables: **Ubisoft shipped the
Commuter Pier.** "unlock the T4 commuter pier as soon as possible, then your Old World will become
one giant linked network"; "The commuter pier makes it easier than it sounds"
([Steam](https://steamcommunity.com/app/916440/discussions/0/1642044369669962743/)). **Anno's island
specialisation is beloved because Ubisoft kept the specialisation and refunded the tax.**

**(2) UNBOUNDED SITE COUNT × PER-SITE UPKEEP.** Stellaris is the cautionary case:

> "my ironman with 50+ planets and just noticing how insanely poor the ai is at handling sectors im
> now giving up trying to finish that game, its just mental the amount of time i spend on managing
> all those planets" — [Steam](https://steamcommunity.com/app/281990/discussions/0/2958292387824652627/)

The enjoyable version of the same mechanic is *one decision at a threshold*: "I just leave them
until the population grows enough to open all slots and then I choose a specialization for the
planet." Anno at ~10 islands with automated routes is loved; Stellaris at 50 planets with manual
attention is quit. **12(j) says we are free to scale; this says the GAMEPLAY cap should be chosen
for exactly this reason** — and 12(j) already reserves that call as gameplay, never viability.

**(3) THE CAP THAT READS AS ARBITRARY — with an important qualification (see §3.6).** Anno's one-trade-union-per-island cap produces the
sharpest split found anywhere, and the discriminator is precise.

- **Praise (constraint generates interior decisions)** — *altarius*: "which items do i put into the
  trade union, where do i put it and how do i arrange my industry within its circle?… it breaks open
  all these usual arrangements of farms and buildings."
- **Curse (constraint forces a known ugly workaround)** — *Strongbeard*: "You have to do like
  everyone… end up with 4 Trade Unions and screw your beautiful city design." *Caz*: "**It's this
  limitation that is keeping 1800 from being a great Anno.**"
- — all [Steam](https://steamcommunity.com/app/916440/discussions/0/1678064284152141903/); mods
  exist purely to remove the cap.

**Same cap, opposite verdicts, decided by whether the constraint generates interesting interior
decisions or a known-correct workaround you repeat.** Direct transfer: *one megastructure per city
will be loved if choosing which, and siting it within the city, is a live problem. It will be
resented if the answer is always obvious and the rest is bookkeeping.*

### 3.4 Run-level exclusivity — the weaker case, and why it does not bind us

- **Frostpunk 1**: the Faith/Order Purpose fork is genuinely irreversible, and the expected "I had
  to replay 20 hours" grief **was searched for across three threads and not found**. The threads are
  about *balance*, not betrayal. Critical praise names the mechanism: "forcing you to make decisions
  that actually matter and then holding you accountable for them"
  ([Metacritic user review](https://www.metacritic.com/game/frostpunk/user-reviews/)). Likely
  because the game is short and the fork is early — **exclusivity is uncontroversial when the price
  of the counterfactual is low.** Our per-city scheme drives that price to near zero: the
  counterfactual is one city over.
- **Frostpunk 2** is where lock-in *did* draw fire, and the complaint is not "I can't have both" but
  "**I never really chose**" — "The game is mostly on rails and our choices are limited"; players
  saving before the first vote and finding that "no matter what they did, they ended up with
  Venturers and Menders". **Exclusivity you didn't author is the thing players hate.**
- **New Vegas** complaints cluster on *accidental* lock-in — "I accidentally set myself to align
  with the Legion for the Yes-man route, rather than the NCR route I wanted"
  ([Steam](https://steamcommunity.com/app/22380/discussions/0/3104564981101515905/)). Nobody
  complains about *choosing* the Legion. **Flag the commitment loudly at the moment of commitment**
  — which is the brink's existing job, and Addendum 4's "consent before cost".
- **RimWorld Ideology**: the objection is *incoherence*, not exclusivity — "I want to eat corn, not
  mushrooms!" — and the accepted defence is "**you can choose not to be a tunneler who prefers
  mushrooms. You voluntarily choose this restriction**"
  ([Steam](https://steamcommunity.com/app/294100/discussions/0/3042731710351609698/)).

### 3.5 Our audience is the least FOMO-prone population available

Qud is replay-native and the genotype fork is discussed **entirely as playstyle and balance, never
as content denial**. Praise: "My personal favorite part of True Kin is scrounging for new
cybernetics"; "Mutants can make a build based on their starting mutations, truekin have to build off
of what they find". Curse — and note it is about power, never access: "They still can't really
compete with mutants"
([Steam](https://steamcommunity.com/app/333640/discussions/0/4628108035612476994/),
[Steam](https://steamcommunity.com/app/333640/discussions/0/3071999401488500371/)).

**The single most telling datapoint found:** a years-deep player states flatly, "i actually never
played true kin before (and ive played this game for years.)" — neutrally, with no sense of loss.
The reporting agent's fetch of that thread noted explicitly that *no post mentions True Kin locking
players out of content areas*.

**Implication:** our risk is not that players resent exclusivity. It is that they resent
**imbalance** (one megastructure strictly better) or **tedium** (the commute). Those are the two
failure modes Qud players actually voice, and both are ours to design against.

### 3.6 The finding that most supports the ruling: uniqueness limits are accepted, EXPANSION limits are resented

This is the cleanest positive result in the comparables and it maps directly onto the capital ruling.

**Accepted, with essentially no complaint found:**
- Anno 1800's **Palace — one per save**. Searched specifically; the recurring Palace complaint is
  that it is *too strong* ("a plain +18k income just from the fact of it's existence",
  [Steam](https://steamcommunity.com/app/916440/discussions/0/3364775232089855027/)), not that it is
  limited.
- Civ VI's one Government Plaza; Songs of Syx's one playable city (dev **Vanir**: multiple towns
  "would be a nightmare to implement sensibly",
  [Steam](https://steamcommunity.com/app/1162750/discussions/2/604147808980774081/)).
- Endless Legend's **one city per region**, defended by players *on identity grounds*: "I highly am
  against the idea of more cities per region. This is one major feature which distinquish EL from
  other games… where you have to decide if the city will be good in science production or in Dust
  production or industry" — *MezzoMax*,
  [Steam](https://steamcommunity.com/app/289130/discussions/0/613937943072783478).
- CK3's **one duchy building per duchy** — the entire community discourse is "which one should I
  pick", guide-worthy and contested, never "why can't I build them all".

**Resented, loudly:**
- Anno's **Influence cap on island count**: "I really don't like it… I like having freedom when
  settling the islands" — *banan1996.1996*
  ([Anno Union](https://www.anno-union.com/pretty-influential/)).
- Manor Lords' **per-region tech-point budget that resets**: "That feels punishing instead of
  rewarding… Atm I'm more likely to restart then to bother expanding" — *Ketraar*
  ([Steam](https://steamcommunity.com/app/1363080/discussions/3/4355620138225604413/)).

**Read:** *one megastructure per city* is a uniqueness limit — the accepted kind. It would only
become the resented kind if it were paired with a cap on how many **cities** the founder may hold.
12(j) already rules city count free to scale; **this is the evidence that it should stay that way.**

### 3.7 The strongest anti-enforcement voice wanted MORE specialisation, not less

Worth quoting because it dissolves the apparent enforced/emergent dichotomy. Manor Lords' *Ketraar*
is the loudest critic of enforced region specialisation in the corpus, and says:

> "having regions be special is fine, they already have different fertility, resources… **I would
> appreciate it if there would be even more contrast.** I just feel that it takes the wind of my
> enthusiasm having to grind for points again."
> "No one suggested regions should not be specialised, in fact I suggested they should be even more
> so." — [Steam](https://steamcommunity.com/app/1363080/discussions/3/4355620138225604413/)

**Nobody in the corpus argues against specialised cities. They argue against being *told*.** The
transferable rule: let the *ground* do the telling. Ostriv proves the floor — with zero enforcement,
pure travel-time gradient is enough: "The Stones are kind of far from the town I built, so it might
be better to build a small village closer to the rocks"
([Steam](https://steamcommunity.com/app/773790/discussions/0/5966762350812987539/)), and the replies
are enthusiastic build advice, not complaints.

### 3.8 Two further failure modes the comparables name, both avoidable

**(4) ISOLATION INSTEAD OF INTERDEPENDENCE.** Manor Lords' enforced specialisation produced cities
that don't need each other: "a new region is like an entirely separate town with no actual connection
to the first one" — *HellDuke*
([Steam](https://steamcommunity.com/app/1363080/discussions/3/4355620303681223907/)); "I do not like
it… switching between settlements is just tedious i find" — *Candid*
([Steam](https://steamcommunity.com/app/1363080/discussions/3/564786150742606839/)).
**A flesh-city and a chrome-city that never trade are two save files in a trenchcoat.** The mod
already has the answer shipped — trade charters, caravans, the water manifest — and should require
the megastructures to *consume* one another's output.

**(5) THE SATURATION CEILING.** The predicted endgame of one-per-city, stated by a Songs of Syx
player 25-30 regions deep:

> "I feel like there's definitely a point where you kinda run out of stuff to build. **You already
> have one city devoted to every resource, and you're kinda at peak efficiency.** Not sure how to
> mitigate it."
> …and his own proposed cure: "Say maybe a city could make furniture, or metal, but in order to do
> so they'd need to consume resources from you unless they produce it locally. Could add some more
> interesting cities even in the late game." — *Khan*,
> [Steam](https://steamcommunity.com/app/1162750/discussions/0/6352962692740066072/)

**One-per-city is a finite checklist, and a finite checklist completes.** The named cure — from the
player himself, and independently from a Humankind reviewer asking for "later infrastructures to
unlock more interesting adjacencies instead of piling up bonuses" — is **dependency between cities,
not exclusivity within them.** Carried into §7.3 as the strongest argument against the
recommendation.

### 3.9 Make the city legible at a glance — the cheapest win in the corpus

The only unambiguous "my cities felt memorable" quote found anywhere is about *vision*, not economy:

> "I like the fact that at a glance I can tell the difference between my Chinese Civ VI cities and
> my Norwegian ones. Stuffing the entire city back into a single tile destroys that effect."
> — *Boris Gudenuf*, [CivFanatics](https://forums.civfanatics.com/threads/civ-vii-districts.675799/)

This is cheaper than any economic tuning and appears to do more work. **We have the machinery and it
is unexercised**: `Styles` (five declared, site-derived, honoured by the code, and every one of the
63 designs still says `Styles="all"` — `DIVERSITY-AND-TECH-TREES.md` R9). A megastructure that shades
its city's style is the answer to R9's "do a data pass first" *and* to the legibility problem, in one
move.

### 3.10 One caution the comparables cannot cover

The reporting agent flagged it honestly and it must be carried forward: **every good precedent found
is ECONOMIC specialisation** (Anno, X4, Civ, Satisfactory). Nobody has shipped *ideologically*
committed sister settlements in one save. Our flesh-city / chrome-city split is partly economic
(what the city can build) and partly identity (what the city believes bodies are for). **The
economic sentiment above is an analogy, not a precedent, for the identity half.** That argues for
keeping the commitment attached to a BUILDING with a function — which is exactly what the capital
ruling already demands.

---

## 4. The megastructure-as-purpose portfolio

The ruling: *ordinary cities get ONE megastructure, and it serves a purpose.* That is a strong
design constraint and it does most of the work for us — it converts "cardinality" (a scarcity rule
people argue about) into "identity" (a thing people name their city after).

**Design rule proposed, from the Anno trade-union split (§3.3(3)):** a megastructure qualifies only
if it (a) has a **verb** the city could not otherwise perform, (b) has **preconditions the ground
supplies** so siting is a judgement, and (c) leaves the city **legible by name** — you should be
able to say "that's my foundry-city" and have it mean something mechanical.

### 4.1 The portfolio

Aligned with the eight exotics already scored in `DIVERSITY-AND-TECH-TREES.md` §4, plus the author's
own two examples.

| Megastructure | Purpose (the verb) | Ground precondition — the thing that makes siting a judgement | Tier | Status |
|---|---|---|---|---|
| **The delve-drill** (author's "big drill") | *build downward* — opens the carved catalogue and the stratum axis | deep strata reachable beneath the seat; rock the shaft can hold | **City** | Rides exotic **#2, the deep delve** — already the recommended first build |
| **The great foundry** (author's "huge foundry") | *refine at scale* — the material chain's top rung, realm-supplying | ore-bearing ground, a standing mason's yard + smelter, water for quenching | **City** | New, but pure ladder-extension of the existing `MinTech="foundry"` chain |
| **The chimeric theatre** (flesh) | *augment a body from what was carried home* — Class-III limbs and the named procedures | butcherable biome density; `taf:damp`/`taf:offal`; a lodged savant who will actually stay | **City** | Promoted from XL (`DIVERSITY` §3.3 rung 3) — see §4.2 |
| **The becoming annexe** (chrome) | *enrol a mutant on the rolls* — cybernetic eligibility, licensed | ruin-adjacency / arclight infrastructure; a psyberneticist to lodge; Mechanimist or Templar reach | **City** | New, ruled in by Addendum 19 — see §4.2 |
| **The granary-colossus** | *feed the realm* — the food/water model at realm scale | fertile, watered ground (the author's own "a city that became its granary") | **City** | Author-named in the cardinality ruling; unscheduled |
| **The arcology** (shell) | *become a city inside a building* — hosted plots, freight-shaft pantry, riser-tap backbone | a city that already reached the full parasang; `MinTech="arclight"`; Branch A rung 5 | **CAPITAL** | The heart ladder's top rung — see §4.3 |
| **The mirror-gate hub** | *the realm becomes one place* — every city one step from the seat | the capital only; power on the 12(g) network at both ends | **CAPITAL** | Rides exotic **#3** — and it is the load-bearing one, see §4.4 |
| **The great archive** | *the realm's knowledge is one body* — renders the DAG, holds every city's keys | the capital; the chronicle and the press | **CAPITAL**, and conditional | See the hard warning in §4.5 |

### 4.2 Flesh and chrome: BOTH become megastructures, and they are one doctrine

**Recommendation: promote the chimeric theatre from XL to megastructure, and make the becoming annexe
its opposite number at the same tier.** This overturns `DIVERSITY-AND-TECH-TREES.md` **R10** and
answers **Q6** in the affirmative — deliberately, with the burden of proof discharged here.

The case, against the cardinality ruling's own standard ("never as a build-more-wonders lane"):

1. **They are the only two buildings in the portfolio that change the PLAYER, not the city.** Every
   other colossus changes what the settlement produces. These change what your body is. That is a
   different kind of thing and it deserves the tier that says so.
2. **They are a matched pair, not a list.** Flesh and chrome are the two answers to one question —
   *what may a body be made of?* — and Qud has already authored that question as its central
   genotype fork. A city that answers it is a city with a creed. This is the "temple-city" case the
   author's own ruling left the door open for, arriving with the fiction already written (§2).
3. **The one-per-city rule makes them affordable.** As XL-ordinary buildings, a single city
   eventually holds both, and the answer to "what may a body be made of?" becomes "yes". As
   megastructures, the answer costs a city, and the kingdom's shape becomes its author's argument.
4. **It gives the flesh/chrome balance problem a structural brake.** §1.4's balance camp is the real
   objection to mutant-chrome, and R4 is the mod's own standing answer. Megastructure cardinality
   adds a second, cheaper brake: **you cannot stack both augmentation ladders in one place**, so the
   power curve is paced by founding and holding a second city rather than by patience alone.
5. **It satisfies the ground-precondition rule.** Flesh wants biome density and offal; chrome wants
   ruins and arclight. Those are *different maps*. Siting is a genuine judgement — which is exactly
   what Civ VI's designers named as the thing that makes specialisation work, and what CivFanatics'
   sceptics said was missing when terrain didn't differentiate, and which Civ VI's soft cap failed
   to deliver (§3.2).

**The one thing this must not become:** a requirement. Per §3.5 and the Kenshi one-town players,
a meaningful slice of the audience will play one city forever. Both buildings must be *optional
prestige*, never a win condition — which the "bonus for engaging, never penalty for abstaining"
pillar already mandates.

### 4.3 Is the arcology the capital's crown, or any city's?

**Recommendation: the arcology is CAPITAL-ONLY.** Three reasons:

- The heart ladder already makes it singular *within* a city (`EVOLVING-HEART-RESEARCH.md`: basin →
  waterstone → moot → court → arcology). If every city can reach it, every city ends the same, and
  the portfolio above collapses back into sameness — the exact failure the specialisation literature
  warns about.
- The arcology's own fiction is the True Kin arcology: **isolated, singular, the seat of a people.**
  A realm of six arcologies is not Qud.
- It gives the capital something to *be*, rather than merely something to *have more of*. A capital
  defined by "it has three megastructures" is a counting rule; a capital defined by "it is the one
  that became an arcology" is a place.

**Consequence to rule on:** ordinary cities then top out at the **court** rung (heart rung 4). That
is a real loss and should be checked against the heart ruling before it is adopted — flagged here,
not decided.

### 4.4 The mirror-gate is our Commuter Pier, and it is the highest-priority item in this document

§3.3(1) is unambiguous: **Anno's island specialisation is beloved because Ubisoft shipped the thing
that deletes the commute.** Fallout 4 is the same design without that thing, and its players call it
"a chore to travel from settlement to settlement".

The mod already has the answer designed and costed: exotic **#3, the mirror-gate** — a
`TeleporterPair` wrapper with a real power draw on the 12(g) network, already assessed as "a
**RENDERING**" and "the cheapest large reward in the document".

**Therefore: the mirror-gate is a PREREQUISITE of the specialised-cities endgame, not a companion to
it.** Shipping flesh-city and chrome-city before the gate is shipping Fallout 4's settlement system
and hoping. Shipping the gate first turns "I have to go to my other city" from a commute into a
step. This is the strongest single actionable finding in the comparables and it costs us almost
nothing, because the work is already scheduled.

### 4.5 The research works — proceed with extreme care, or not at all

The author's list floated "research-works". **`DIVERSITY-AND-TECH-TREES.md` R2 refuses a research
screen "in any costume"**, citing the mod's own verbatim ruling at
`T/Growth/KingdomZoningRules.cs:9-10`, and R3 refuses making people worth tech points.

A capital "great archive" is admissible **only** as §2's MAP — it renders the DAG the catalogue
already contains, unified across the realm's cities, and **nothing on it can be pressed.** If any
version of it has a queue, a budget, a percentage, or a timer, it is R2 being smuggled back in
wearing a colossus. **Recommendation: defer it.** It is the weakest item in the portfolio and the
one most likely to violate a standing ruling.

### 4.6 The portfolio must be INTERDEPENDENT, or it is a checklist

§3.8's two failure modes bite here specifically, and the fix is a design requirement rather than a
caution.

- **Manor Lords' warning**: enforced specialisation produced cities that never needed each other —
  "a new region is like an entirely separate town with no actual connection to the first one".
- **Songs of Syx's warning**: "You already have one city devoted to every resource, and you're kinda
  at peak efficiency." The checklist completes and the kingdom stops having decisions.

**Requirement: every megastructure in §4.1 must consume something another city's megastructure
produces.** The great foundry wants the delve-drill's ore. The chimeric theatre wants the
granary-colossus's preserved stock and the foundry's worked metal. The becoming annexe wants
arclight-grade parts only the foundry can make. The arcology wants all of it. Then the kingdom is a
system rather than a set, the trade charters and caravans that already ship become load-bearing, and
the saturation ceiling moves out of reach because *scaling* a specialisation is still a live problem
after *choosing* it is settled.

This costs nothing new: it is `Materials`/`Refines`/`Provides` on records we are already writing, and
it is the mesh condition being satisfied rather than strained.

### 4.7 The megastructure should SHOW

Per §3.9 and R9: give each megastructure a `Styles` consequence, so the flesh-city and the
chrome-city look different from the road. It is the cheapest identity win available, it discharges
R9's "exercise the unused axis with data first", and it is the only mechanism in the whole comparables
corpus that players spontaneously named as making cities memorable.

---

## 5. What makes a city the capital

### 5.1 First, a naming collision that must be caught before it ships

The author's ruling says "structures only **the seat of the kingdom** may raise." But `Seat` is
**already taken in this codebase, with a different meaning**:

> "Everything above this line describes the seat — **the settlement the founder is currently in** —
> and every consumer reads those fields directly. The other city waits here, and the two are
> exchanged by `TrySeat` when the founder walks into its ground."
> — `Core/KingdomSystem.z03.State.Realm.cs:88`

`Seat` is the **hot/cold swap role**, not a political capital: it moves every time the founder walks
between cities (`KingdomSystem.TrySeat`, `:935`). `FOUNDATION-CONTRACT.md:53-60` reinforces it —
"One seat owns physical truth… Seat retargeting is never automatic", and the realm's manifest and
carry-haul are deliberately addressed **by settlement name rather than by seat/Away role, because
those roles exchange on `TrySeat`.**

**Therefore the capital cannot be "the seat", and must not be called it.** If the capital were the
seat, every city would be the capital in turn, simply by the founder standing in it, and the
capital-only megastructures would flicker. Recommended vocabulary: **the crown** (or **the high
court**), never `Seat`.

### 5.2 The four designation rules, and what players say about each

| Rule | Who does it | Sentiment |
|---|---|---|
| **(a) The founding city is the capital, permanently** | **Civ VI** — you cannot move your capital; it relocates only if the original is captured | Uncontroversial, but also **unloved**: no praise found for it as a *feature*. It is a fact, not a decision. |
| **(b) The capital is wherever a specific unique BUILDING stands** | **Civ IV/V** — the Palace; moving the capital means building the Palace elsewhere | **The only rule with actual praise attached.** Players report enjoying it "for both the roleplay aspect and free city bonuses", with some "moving every age transition"; noted benefits include relieving crowding in the ancient-era capital so other cities can host wonders. ([CivFanatics](https://forums.civfanatics.com/threads/do-you-ever-choose-to-move-capitals-and-why.695469/), [CivFanatics](https://forums.civfanatics.com/threads/move-capital.542504/), [Apolyton](https://apolyton.net/forum/civilization-iv/civilization-iv-general/168835-capital-should-it-ever-be-moved)) |
| **(c) An explicit designation verb with a cost/cooldown** | CK3/EU4-style realm-capital moves | Not found in strength; the mechanic exists but generates little discussion either way. |
| **(d) The largest / highest-developed city, automatically** | — | **Rejected here on principle**: an automatic capital is not a decision, and it would make the capital's extra megastructures a reward for a number going up. That is a numbers panel wearing a crown. |

**The corroborating evidence is strong and one-directional: the complaint is always about WHO
CHOOSES, never about the capital being special.**

- **Praise for the capital-as-reward**: "It was always fun to build your palace as 'reward' for
  growing a large civ" — *acluewithout*,
  [CivFanatics](https://forums.civfanatics.com/threads/the-capital-buff-the-palace.637492/); "I kind
  of enjoy the roleplay aspect of having my new centre be in a new place" — *UWHabs*,
  [CivFanatics](https://forums.civfanatics.com/threads/do-you-ever-choose-to-move-capitals-and-why.695469/).
- **Removing the move drew complaints**: "I'm very disappointed that the development team took away
  the ability to change where your seat of government (palace) is" — *Trav'ling Canuck*, same thread.
- **Letting the GAME curate the candidates drew rage** (Civ VII): "i had a specific city in mind to
  switch too and then it decides to give me the option of some random ass town" — *dfwsh*; "Why
  exactly is there not an option to just chose any Settlement you wish?" — *TheGhostEnthusiast*
  ([CivFanatics](https://forums.civfanatics.com/threads/any-mod-to-pick-which-city-becomes-the-capital-on-the-next-age.695442/),
  [CivFanatics](https://forums.civfanatics.com/threads/so-about-changing-capitals.696646/)).
- **The opposite complaint also exists** — capitals that aren't special *enough*: "One thing I find a
  bit underwhelming is that your capital is often just another city" — *Archon_Wing*, same thread.
  Which is a point in favour of the author's ruling that the capital gets extra megastructures.
- **CK3's shape is the one to copy for the move verb**: a costed/limited free choice *plus* an
  always-available free move back to the canonical seat. Note the revealed preference against a hard
  lock — several mods exist purely to delete CK3's once-per-lifetime restriction. **Recommendation:
  the crown is movable, priced in labour and world-days, and never once-per-run.**
- **Emergent capitals happen for free.** Anno players voluntarily abandon their starting island for
  the bigger one with no designation verb involved: "I wound up just concentrating on Crown Falls as
  my capital city. With that large corner island it's almost a no-brainer" — *Kmmacman*,
  [Steam](https://steamcommunity.com/app/916440/discussions/0/2958293022030273344/). Expect founders
  to want to move the crown once they find better ground; give them the verb rather than making them
  restart.

### 5.3 Recommendation: rule (b), in Qud's own morphology — **the crown is a building**

**The capital is the city where the founder raised the crown.** Concretely, the heart ladder gains
its final civic rung — a **high court** raised on the heart's own plot, above the court rung — and
the city holding it is the capital for as long as it holds it. Moving the capital means raising the
crown somewhere else: a real project in materials, labour and world-days, announced, brink-shaped,
and reversible only by doing it again.

**Why this and not the alternatives:**

1. **It is the only rule players praise** (§5.2b), and the praised part is exactly what this mod
   wants: roleplay *and* a real strategic consequence.
2. **It is a rendering, not new machinery** (mesh condition). The heart ladder already ships rungs
   1-4 with a growing plot, the ghost survey and the yielding mark; the relocation verb is already
   designed for ring calls. A crown rung is one more rung on a ladder that exists.
3. **It does not collide with `Seat`** (§5.1) and needs no new realm-level authority — the crown is
   a property of a *settlement record*, which the roster wave already generalises.
4. **It makes the capital a decision the founder can be wrong about.** A capital chosen early on a
   thin site, then outgrown, is a story. An automatic capital is not.
5. **It composes with §4.3**: the arcology is capital-only, and the crown is what the arcology grows
   out of. The capital is *the city whose heart went all the way*.

**The alternative worth naming, if the author prefers less ceremony:** rule (a) — the founding city
is the capital, forever. It is free, it is Civ VI's choice, and it makes the founding decision heavy
in a way this mod likes. Its cost is that a bad first site is a permanent bad capital, with no verb
to answer it — and the mod's own protection law and brink doctrine are built around *never* leaving
the founder without a say.

### 5.4 Does capital-only content feel like a reward or like arbitrary privilege?

The honest answer from the comparables is that it depends on **whether the capital was chosen**.
Civ IV/V's movable Palace generates positive sentiment because the player *sited* the capital and
can *resite* it. Civ VI's fixed capital generates neither praise nor complaint — it is inert. The
failure case that would generate real resentment is (d): capital privileges attached to a city the
game picked by counting population.

Under rule (b), capital-only megastructures read as **the reward for a project the founder
undertook**, which is the same logic that makes every other megastructure legible. That is the
recommendation's strongest defence.


### 5.5 The shape the capital should take — Anno's Palace and its Local Departments

The closest working precedent for "one grand capital structure plus ordinary cities that still feel
connected to it" is Anno 1800's Palace, and it is worth copying almost directly. The dev framing:

> local departments are "essentially outposts of the palace that you can construct **once per** Old
> World or Cape Trelawney **island** to select a single policy"… "to bring everything full circle and
> ensure that future palace builders will have to make some tough choices, the radius of these
> effects increases with each additional palace module constructed."
> — [Anno Union, DevBlog: Seat of Power](https://www.anno-union.com/devblog-seat-of-power/)

Player reception describes exactly the pattern we want: "I always wait to build the palace on the
large residential island of Crown Falls… **You can then build an annex on any of the other islands
that may benefit from it**" — *kmmacman*,
[Steam](https://steamcommunity.com/app/916440/discussions/0/3129415856233150367/).

**Transfer:** the capital's megastructures should each project a **cheap, one-per-city satellite**
into the ordinary cities — an outpost that carries a slice of the capital structure's function and
costs a small plot, not a colossus. This does three things at once: it makes the capital *felt*
everywhere rather than hoarded, it gives ordinary cities a reason to care about the capital's
projects, and it keeps the satellite low-attention, which §5.6 says is mandatory.

**Two warnings that come with it:**

1. **Capital clutter.** From Stellaris, verbatim: "A pretty bad idea to also make them capital only.
   This means that eventually, your capital planet will be **cluttered with empire unique
   buildings**" ([Steam](https://steamcommunity.com/app/281990/discussions/0/1620599015903650048/)).
   Give capital-only megastructures their **own footprint** — the arcology already spans zones, which
   solves it — so they never crowd out the capital's ordinary economy.
2. **Gravity.** One-per-realm content drifts to the capital by default because the capital is the
   best producer: "my games have tended to end up with nearly all the Wonders I build being located
   in the capital city" — *Trigan Emperor*,
   [CivFanatics](https://forums.civfanatics.com/threads/decentralizing-wonder-building-from-capitals.654043/).
   And the naive fix backfires — a "gamey prohibition" is itself resented (*Boris Gudenuf*, same
   thread). Our one-per-city rule already prevents hoarding structurally, which is the elegant
   version of that fix; the thing to watch is that the capital does not *also* become the only city
   worth siting a megastructure in.

### 5.6 Satellite cities must be LOW-ATTENTION by design

Two independent precedents point the same way and it is a hard requirement, not a nicety.

- **Frostpunk 2 scaled from one city to city-plus-colonies and lost the settlement's identity**:
  "The once-charismatic city now feels impersonal and anonymous, stripping away the charm that made
  the first game"; "Individual characters seem insignificant, lost in a faceless crowd"
  ([Metacritic](https://www.metacritic.com/game/frostpunk-2/user-reviews/)). 11 bit's mitigation is
  exactly our capital ruling: "**None of these new factions will have their own Council Hall** as New
  London remains the center for political decisions"
  ([PCGamesN](https://www.pcgamesn.com/frostpunk-2/colony)).
- **RimWorld's verdict on second colonies is near-unanimous**: "Honestly find it too much work";
  "starting a new base early on is a pain in the ass, one base is hard enough"; "One colony is all I
  need" ([Steam](https://steamcommunity.com/app/294100/discussions/0/4368004299238676243/),
  [Steam](https://steamcommunity.com/app/294100/discussions/0/1693785669857132612/)). The pattern
  players converged on unprompted: "The trick to keeping it manageable is to keep it very small
  though and ship out the resources as you get them" — *Astasia*.

**Transfer:** a specialised city must be *runnable without visiting it often*. The mod is already
built for this — the world keeps time whether the founder is in it or not, works run on days and
labour rather than visits, and a dormant city resolves its whole absence on the next seating
(`Core/KingdomSystem.z02.State.City.cs:152`, `Itineraries`). **That existing design is what makes Design B viable where it
made RimWorld's multi-colony play a chore.** It should be stated as a load-bearing reason, not
assumed.

---

## 6. The three candidate designs, fully argued

The capital ruling fixes the *frame* (one megastructure per ordinary city, serving a purpose;
capital gets extras) but does **not** fix which buildings are megastructures. All three candidates
therefore remain live inside the ruling. They are ordered weakest-to-strongest as this document
reads them.

### 6.1 Design A — both XL-ordinary; one city can hold all

Flesh theatre and becoming annexe are **XL plots** (as `DIVERSITY-AND-TECH-TREES.md` §3.3 currently
assumes for rung 3). A city's single megastructure is something else — drill, foundry, granary. A
mature city eventually holds theatre **and** annexe **and** its megastructure.

**For.**
- Cheapest by far: it is the design already written. R10 stands, Q6 answers "no", nothing is
  re-costed, and the theatre's four rungs ship as scoped.
- Single-city players (a real and content constituency — Kenshi's "i'm a one town guy") lose
  nothing. Maximum accessibility.
- No new lock-in surface at all, so none of §3's failure modes can bite.
- Keeps augmentation on the **building ladder** (improvements climb within a plot), which is the
  catalogue's own native progression idiom.

**Against.**
- **It makes the answer to Qud's central question "yes".** The genotype fork — the thing Qud's own
  design is built around — becomes a checklist a sufficiently patient founder completes. That is
  precisely the "have-everything-eventually" shape the design literature calls a non-decision:
  Compton's third requirement is that a choice must be non-obvious, and "build both" is obvious.
- It wastes the strongest fiction available (§2). A registry that decides who is enrolled is a
  *sovereign act*; making it one more XL plot among nine spends the fiction on furniture.
- It leaves the balance objection (§1.4) resting entirely on per-procedure cost — one brake, where
  §4.2(4) offers a second for free.
- It gives the endgame no shape. The portfolio in §4 exists to make cities *different*; excluding
  the two most identity-laden buildings from it is self-defeating.

### 6.2 Design B — both megastructures; kingdom of specialised cities

Theatre and annexe are each a city's **one** megastructure. A founder who wants both must hold two
cities. The capital adds the arcology and the mirror-gate hub.

**For.**
- **The evidence is strongest here.** §3.1: per-site exclusivity with realm-level composition is the
  well-liked shape; the "it hollows the choice" objection was searched for and not found; our
  audience is the least FOMO-prone available (§3.5).
- It is what the capital ruling asks for, read plainly: the megastructure IS the city's functional
  identity, and "the flesh-city" / "the chrome-city" are the two most nameable identities the mod
  will ever have.
- It gives siting a real judgement, because the two want different ground (§4.2(5)) — the condition
  Civ VI's designers named and Civ VI's soft cap then failed to deliver (§3.2).
- **The cap is the accepted kind.** §3.6: uniqueness limits draw essentially no complaint across
  Anno, Civ VI, Endless Legend, Songs of Syx and CK3; only expansion caps do, and we impose none.
- It prices the augmentation power curve in **cities**, which is the mod's own currency, rather than
  in patience.
- The fiction lands hardest: a city that enrols mutants on the Eaters' rolls is a political act, and
  a *second* city that instead grafts girshworm to bone is a different politics. The kingdom becomes
  an argument about bodies. That is Qud.

**Against.**
- **The commute is now load-bearing** (§3.3(1)). Without the mirror-gate this design is Fallout 4's
  settlement system, and the sentiment on that is unambiguous.
- It raises the floor: a founder must reach a **second city** before the second augmentation path
  exists at all. That is a large gate on content the C1 correction says the mod owes players.
- It risks the Anno trade-union split (§3.3(3)) if the assignment is obvious — if "the big one gets
  chrome" is always right, the cap is bookkeeping.
- Single-city players are cut off from half the endgame. Mitigated only if both remain optional
  prestige, never required.
- The identity half has **no precedent** (§3.10) — every comparable is economic specialisation.

### 6.3 Design C — the hybrid: XL buildings, one END-STATE IDENTITY per city

The buildings stay **XL-ordinary** (any city may raise theatre and annexe). What is one-per-city is a
declared **end-state identity** — a *dedication* the city takes once, at the top of its ladder,
which **empowers** one path without denying the other's building. A flesh-dedicated city runs
Class-III procedures at full effect, holds the named procedures, and lodges the notable; the same
city may still hold a becoming annexe, but it operates at the lower rungs only. Chrome-dedicated
inverts it.

**For.**
- Keeps content accessible (Design A's virtue) while making the choice matter (Design B's virtue).
- Nothing is *denied*, so the FOMO surface is nil, and the single-city player still reaches both
  ladders — just not both summits.
- The dedication is a natural home for the creed system, the fault-line ceiling, and standing —
  machinery that already ships.
- Degrades gracefully: a city can hold the "wrong" building and still get value from it.

**Against — and this is decisive.**
- **"Delay is not denial" kills it.** §3.2's Civ VI evidence is that a *soft* differentiator produces
  all the friction and none of the identity — "you want mostly the same districts in every city";
  "There is not so much to gain by specialising". A dedication that merely reduces the other path's
  effectiveness is the soft version, and this is the closest available evidence of how it lands.
- **It is a numbers panel in a robe.** "Full effect vs lower rungs" is a multiplier, and Addendum 4's
  pillar guard is explicit — *placement constraints, never meters* — the same ground on which R7
  refused a revulsion meter. A dedication that scales output is exactly the shape that was already
  rejected once.
- It **violates the capital ruling as written**: the ruling says the megastructure is the city's
  purpose. Design C makes the *dedication* the purpose and the megastructure something else, which
  is a second identity axis stacked on an unexercised first — the exact error R9 names.
- It adds a new concept ("dedication") that is not a rendering of existing model state, which is the
  **mesh condition** talking. Design B adds no new concept at all: it changes one attribute on two
  building records.
- Players cannot easily forecast what "empowered" means before committing — failing Compton's
  predictability requirement more badly than B does.

### 6.4 The three conditions any design must satisfy

From the distilled conditions across §3, the three that actually bind us:

1. **The ground must justify the specialty.** Flesh and chrome need *different* preconditions, or
   siting is a coin-flip and the specialisation is decorative. (Civ VI's terrain link; CivFanatics'
   sceptic answered by "you *manufacture* the specialty".)
2. **The connection must be cheap.** The mirror-gate ships first (§4.4). Non-negotiable.
3. **The commitment must be flagged before it is made.** The brink already does this; the New Vegas
   evidence is that *accidental* lock-in is the only lock-in players actually resent.

A fourth, softer: **keep the realm legible** — name cities by their megastructure and surface a
realm-level roster, or you get X4's "which factory is engines and which is turrets".

---

## 7. Recommendation

### 7.1 The ruling this document asks for

**Adopt Design B, with the mirror-gate as a hard prerequisite.**

- The **chimeric theatre** and the **becoming annexe** are both promoted to **megastructures** under
  one-per-city cardinality. `DIVERSITY-AND-TECH-TREES.md` **R10 is overturned** and **Q6 is answered
  yes**, deliberately and on the record.
- Each is a city's **purpose** in the capital ruling's sense: the flesh-city and the chrome-city.
- The **arcology** and the **mirror-gate hub** are **capital-only**; a "great archive" is deferred
  under R2 (§4.5).
- The **mirror-gate ships before or with** the first specialised city. This is the load-bearing
  condition, not a nicety.
- Both remain **optional prestige**. Neither is a win condition; the one-city founder loses summits,
  never the game.
- **§4.6 is a condition of the ruling, not a refinement of it**: every megastructure must consume
  something another city's megastructure produces. Without it, §7.3's saturation objection stands.
- **§4.7**: each megastructure shades its city's `Styles`, so the kingdom is legible from the road.
- **The city count stays uncapped** (12(j)). §3.6's evidence is that uniqueness limits are accepted
  and expansion limits are resented; pairing one-per-city with a cap on cities would import the
  resented half.

### 7.2 The single strongest piece of evidence FOR

**Anno 1800's Commuter Pier, read together with Fallout 4's settlement commute.** It is one finding
in two halves, and it is the only comparable that isolates the variable.

Anno and Fallout 4 both ask the player to keep several specialised sites. Anno's players describe
the arrangement as simply *how you play* — "most people have 1 major island in which everything is
supplied, the rest act as 'production islands' specialized in a few goods" — and recommend the
Commuter Pier as the thing that makes it work: "then your Old World will become one giant linked
network"; "The commuter pier makes it easier than it sounds"
([Steam](https://steamcommunity.com/app/916440/discussions/0/5715697731005455872/),
[Steam](https://steamcommunity.com/app/916440/discussions/0/1642044369669962743/)).

Fallout 4 has the same specialisation and no pier, and its players say: "it's becoming a chore to
travel from settlement to settlement"
([Steam](https://steamcommunity.com/app/377160/discussions/0/492378265890123054/)).

**Two games, same structure, opposite reception, one differing variable — the cost of the
connection.** That is as close to a controlled experiment as sentiment research gets, and it tells
us both that Design B is safe *and* exactly what makes it safe. And the mod already has the pier
designed, costed, and scored as a rendering (exotic #3).

### 7.3 The single strongest piece of evidence AGAINST

**The saturation ceiling — Songs of Syx, from a player deep enough to have hit it.**

> "I feel like there's definitely a point where you kinda run out of stuff to build. **You already
> have one city devoted to every resource, and you're kinda at peak efficiency.** Not sure how to
> mitigate it." — *Khan*,
> [Steam](https://steamcommunity.com/app/1162750/discussions/0/6352962692740066072/)

This is the sharpest argument against Design B because it does not attack the choice — it attacks
what happens *after* the choice. **One megastructure per city turns the endgame into a finite
checklist.** Found six cities, assign six purposes, and the kingdom has no decisions left. Design A,
for all its faults, does not have this problem: there is always another XL plot.

It is independently corroborated from a different game and a different direction — a Humankind
reviewer asking for "later infrastructures to unlock more interesting adjacencies instead of piling
up bonuses, to shake up the late game city building a bit" (*Elhoim*,
[CivFanatics](https://forums.civfanatics.com/threads/my-review-of-humankind.672454/)).

**The mitigation is named by both of them and it is §4.6: dependency, not exclusivity.** Khan's own
cure — "a city could make furniture, or metal, but in order to do so they'd need to consume resources
from you unless they produce it locally" — is exactly the requirement §4.6 places on the portfolio.
**If §4.6 is not built, this objection stands and Design B degrades into a checklist.** That makes
§4.6 a condition of the recommendation, not a refinement of it.

**Secondary against — the cap that reads as arbitrary.** Anno's one-trade-union-per-island cap
splits the community hard: *altarius* praises it because it "breaks open all these usual
arrangements"; *Caz* says "**It's this limitation that is keeping 1800 from being a great Anno**"
([Steam](https://steamcommunity.com/app/916440/discussions/0/1678064284152141903/)). §3.6 qualifies
this — the resented half of Anno's design is the *Influence budget on expansion*, not uniqueness per
se — but the lesson survives: **a one-per-city rule is read as arbitrary whenever the correct
assignment is obvious.** Mitigation is §6.4(1): give flesh and chrome genuinely different ground.

**Tertiary against, stated plainly:** §3.10's gap. Every precedent found is *economic*
specialisation; nobody has shipped ideologically committed sister settlements in one save. Design B
is the best-evidenced option available, not a proven one.

### 7.4 How the chrome annexe earns its fiction, per design

The author's requirement — "a reason in the world, not a checkbox" — is met differently by each.

| Design | The fiction available | Verdict |
|---|---|---|
| **A — XL-ordinary** | F2 (the unspendable wedge) works fine; F1 (the registry) does not. A registry that any city can raise, alongside everything else, is a *service*, not a claim of sovereignty. The building says "we do implants here" — which is a checkbox with a roof. | **Weakest.** The fiction survives but is spent cheaply. |
| **B — megastructure** | **F1 + F2, staffed by F3, with F4 as friction** (§2.5). The city that raises the annexe is the city that decided it may write the rolls. It cost the city its other destiny to say so. The psyberneticist who lodges there did it to themselves first. The Mechanimists will come about the debt. | **Strongest.** The building's *cost* is what makes the claim credible — a registry nobody paid for is not a registry. |
| **C — hybrid** | F1 is available but muffled: the city holds the annexe either way and the dedication only changes how well it works. "You may be enrolled, but only partially" is not a fiction, it is a coefficient. | **Weak, and worse than A** — A at least has a clean small fiction. |

**The line to put in the building's description, under Design B:**
*The Becoming Nook asks whether you are on the Eaters' rolls. This is where your city keeps its own.*

### 7.5 Open questions this document does not decide

1. **Does the arcology being capital-only cap ordinary cities at the court rung?** §4.3 recommends
   yes; the heart ruling should be re-read before it is adopted.
2. **Does the annexe grant eligibility EVERYWHERE, or only at home?** Strong recommendation for
   **only at home** — it costs nothing to implement (our building owns its own authorization),
   it keeps the chrome-city a *place you return to* rather than a switch you flip, and it avoids
   touching genotype at all. But it is a real design call with a real feel cost and it is the
   author's.
3. **Does the theatre being a megastructure change its four-rung ladder?** Rungs 0-2 (slab,
   vat-house, grafting hall) should almost certainly remain ordinary plots; only rung 3 becomes the
   colossus. Same for the annexe. Unresolved.
4. **What happens to a city that loses its megastructure's staffing?** The standing rule is *shell
   is history, function is labour* — an abandoned annexe stands dark and the enrolment lapses. What
   happens to already-installed implants when the rolls go unkept is a genuine fiction opportunity
   and a genuine cruelty risk. See rejection **X3**.

---

## 8. What I could not find — stated plainly

Several of these are load-bearing, and the recommendation in §7 should be read with them in view.

**On the Qud community cluster:**

- ~~**No Reddit content at all.**~~ **CLOSED 2026-08-22** via the author's Reddit MCP server —
  see §1.7. Seven r/cavesofqud threads read in full (~90 comments, 2020–2025); the "no lore
  objection" finding held, the registry fiction (F1) gained community-sourced receipts, and two
  new findings surfaced (the one-way permeability of the vanilla boundary; the Sifrah
  revealed-preference answer to the Freehold question below). Honest residue: the archive route
  (Arctic Shift) may lag live scores; one lore-rich thread
  ([what True Kin exist](https://www.reddit.com/r/cavesofqud/comments/1i5ee12/aside_from_templar_and_the_player_what_true_kin/),
  43 comments) failed retrieval twice and remains unread; r/cavesofqud beyond these seven threads
  remains unswept.
- **The official Discord's public spillover** was not reachable. Not attempted beyond search.
- **Verbatim comment tabs for three of the four mutant-chrome mods.** Steam rate-limited
  (`"You've made too many requests recently"`) partway through, so `Hybrid: Cybernetic Mutants`,
  `Cyber Mutant - Unstable Genome` and `Limbsmith` comment tabs are represented by search-engine
  summaries and the Mechanimist Genotype tab, not by direct reads. Nexus Mods returned 403.
- **Creature Control has no comment tab to read** — it is a local mod at
  `M/CreatureControl/`, not published to the Workshop (its own source still carries the
  `// TEMP-DIAG (remove before Steam upload)` marker noted in `DIVERSITY-AND-TECH-TREES.md` Q10).
  Playable Slime's tab was read: the sentiment there is the R4 receipt, and it is about power, not
  lore.
- **Any Freehold statement on *why* two genotypes exist.** Devlogs, press kit, the GDC 2019 procgen
  talk and Roguelike Radio ep. 45 were searched; no accessible transcript. The design rationale for
  Qud's own permanent fork remains undocumented in reachable sources — which matters, because it is
  the one thing that could have settled whether Freehold considers the boundary sacred. *Partial
  answer found on Reddit (§1.7 R-E): no stated rationale, but a revealed preference — the
  developer's own Sifrah mod opens the boundary via nook-hacking, and its author recommends it for
  exactly that purpose. The boundary's keeper sells keys.*
- **No "chrome pilgrim" exists.** The brief asked about one; searched and not found. The nearest
  real things are the **Mechanimist pilgrim** (an ordinary faction creature) and the chrome liturgy
  in `B/Books.xml` (§2.4). Recorded so nobody hunts for it again.

**On the comparables:**

- **Paradox's official forums** were behind a Cloudflare browser-validation wall, costing the best
  Stellaris planet-specialisation threads.
- **Sentiment on parallel RimWorld colonies running different ideoligions** — the exact analogue of
  our flesh-city/chrome-city — was not found. Only a mechanical note that multi-settlement play
  degrades (one leader and one guide per ideoligion). RimWorld does not cleanly support the pattern.
- **The Frostpunk "I had to replay 20 hours to see the other half" complaint** was hunted across
  three threads and is not there. Probably genuine absence (short game, early fork), but unproven.
- **Any evidence that the multi-site compromise hollows out the choice.** Searched for directly;
  not found. Absence of evidence.
- **Any precedent for IDEOLOGICALLY committed sister settlements in one save.** Every good
  comparable is *economic* specialisation (Anno, X4, Civ VI, Satisfactory, Kenshi). This is the most
  important gap in the document: **the identity half of our design is extrapolated from economic
  precedent, not evidenced directly.** §7.3 carries it as the secondary argument against.
- **Songs of Syx** was reached and is one of the most useful sources in the document (§3.8, §7.3),
  but its *race/religion capital* angle specifically did not surface — the usable material is
  per-city resource specialisation and player-built race districts.
- **Paradox's own forums** ("Validating browser…" wall) and the **Steam hubs for Frostpunk 2 and
  CK3** (age-gated content interlock) were unreachable. Frostpunk 2 sentiment here comes entirely
  from Metacritic user reviews; Stellaris and CK3 forum material is search-snippet paraphrase.
- **Ludeon's official forums** returned 403; all RimWorld evidence is Steam and Metacritic.
- **No GDC talk or dev postmortem** on settlement specialisation, Anno island design, or the Against
  the Storm blueprint draft was located, despite repeated search.
- **No Firaxis statement** on *why* Civ VI's district count is population-gated — which matters,
  because §3.2's "delay is not denial" reading is inferred from player outcome, not from stated
  intent.

**Two "do not cite" warnings**, recorded so they are not laundered into a later document:

- **Civ VI has no capital-only wonders.** Its analogue is one-per-empire (the Government Plaza). Do
  not cite Civ VI as precedent for capital-exclusive structures.
- **Against the Storm is weaker precedent than it looks.** Its praise is always phrased as
  *adaptation to RNG* ("every run feels like a fresh puzzle"), never as *settlement identity*. It
  supports "denial can be fun"; it does not support "specialised settlements are memorable".
- Two lines surfaced only in search snippets and could not be verified on a fetched page — an
  Against the Storm critic line about "disposable settlements", and a Manor Lords line about
  specialisation making each town "interesting, unique, valuable and important". **Neither is used
  above and neither should be quoted.**

**On the engine:**

- **The hacked-Becoming-Nook path (§2.3(1)) is source-read only.** `IsAuthorized` returning true
  under `HackActive`, and `GetAuthorizedSubjects` admitting any allied creature that passes it, are
  both read from the decompile at `D/XRL/UI/CyberneticsTerminal.cs:481-487, 605-620`. **Not
  playtested.** Rejection X10 forbids leaning on it until it is.
- **Whether `QudCyberneticsModule` on the Mutated Human genotype does anything at zero license
  points** is untested; §2.3 marks it INFERRED and uses it only as corroboration.
- **A megastructure tier does not exist in the catalogue today.** `KingdomBuildings.xml` tops out at
  `Plot="XL"` (5 designs) and `MinTech="foundry"`; `arclight` appears in no shipped design. Promoting
  the theatre and the annexe to megastructures is therefore **new vocabulary plus a new tier**, not a
  relabel — which is R10's own point, arriving now as a cost estimate rather than a refusal.

---

## 9. Loud rejections

Stated as loudly as the house pattern asks, so nobody re-derives them.

**X1 — Flipping the player's genotype to True Kin. Refused.**
The crude precedent exists and is honest about itself: `Mutated Kin`
(`.../workshop/content/333640/2908962625/Genotypes.xml`) ships `IsTrueKin="true" IsMutant="true"`
plus `CyberneticsLicensePoints="2"`, and `Cyber Mutant` says outright that its mutants "qualify as
True Kin" and lose Rebuke Robot for it. Both are **chargen** genotypes; ours is a **mid-run
building**. Flipping the genotype mid-run would silently rewrite Templar reputation semantics,
`Rebuke Robot`, arcology-of-origin resistances, and every mod reading `GetGenotype()`. Use the
**`IsTrueKinEvent` handler** (§2.3) — narrow, scoped, reversible, and the door Freehold installed.

**X2 — Granting eligibility as a permanent invisible player flag. Refused.**
That is the checkbox the author named. Eligibility is a **licence issued by a place**, recorded in
the chronicle, revocable if the annexe goes dark. "Everything is remembered, twice."

**X3 — Cybernetic Rejection Syndrome, or any hidden permanent penalty for a reversible act.
Refused, with the receipt.**
`GetCyberneticRejectionSyndromeChance` (`D/XRL/World/Parts/CyberneticsBaseItem.cs:220-235`) computes
a mutant-specific rejection chance and then unconditionally `return 0;` at `:234`. It is dormant
Sifrah code. When community mods let mutants take chrome, that code fires, and the player verdict is
on the record: "**a permanent debuff on arguably the most important stats in the game from a
reversible action is a serious kick in the balls**"
([Steam](https://steamcommunity.com/app/333640/discussions/0/3728449612308825510)). It also breaks
Addendum 8 clause 2 (rates are time × labour × infrastructure, never time alone) and the
penalty-for-abstaining pillar. **Cost is paid at the door, visibly, in water, materials, bits,
staff-days and standing.** If chrome in a mutant body must hurt, it hurts *as fiction and creed
friction*, never as a silent stat tax.

**X4 — A "wonders menu" of megastructures. Refused, restating the cardinality ruling.**
The portfolio in §4 is a set of destinies, not a build list. If any version of this ends with the
capital raising five colossi because it can afford them, the ruling has been inverted. The capital
gets **a couple** of capital-specific structures, each with a purpose no ordinary city can serve.

**X5 — A dedication/identity multiplier per city. Refused (Design C).**
Addendum 4's pillar guard: *placement constraints, never meters*. R7 already refused a revulsion
meter on this ground. "Flesh-dedicated cities run Class-III at full effect, others at reduced" is
the same rejected shape with a new name.

**X6 — Making the specialised cities MANDATORY. Refused.**
Kenshi's one-town players ("i'm a one town guy", "Only one, normally i roleplay as part of the Shek
Kingdom") are a real constituency, and the mod's own pillar is *bonus for engaging, never penalty for
abstaining*. A founder with one city must reach a complete, satisfying end-state.

**X7 — A capital that is merely "the biggest city". Refused.** See §5.

**X8 — Shipping specialised cities before the mirror-gate. Refused as sequencing.**
§4.4 and §7.2. This is the one finding in the document with a controlled comparison behind it.

**X9 — Treating the Mechanimists as simple chrome-lovers. Refused as lore.**
Their liturgy is *renunciation* — "Unburden yourself from the weight of your chrome guilt", "Offer
your chrome to Shekhinah", "Throw it down the Sacred Well" (`B/Books.xml:165,170,171`). Chrome is a
**debt**, not a blessing. An annexe framed as a Mechanimist gift-shop misreads the faction; an annexe
framed as a place that takes on debt reads it correctly.

**X10 — Asserting the hacked-Nook precedent without playtest. Refused until verified.**
§2.3(1) is SOURCE-PROVEN, RUNTIME-UNVERIFIED. `_notes/README.md`'s standing rule holds, and the C2
correction exists precisely because a precedent claim was once made too fast. It goes in TESTING.md,
not into an argument.
