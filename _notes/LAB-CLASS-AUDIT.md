# LAB-CLASS-AUDIT — the D2 safe-part-class wishlist

**Status: DRAFT for author review. Not a catalogue change; nothing here ships until the author
picks.** Commissioned by Addendum 22 D2 (`_notes/BUILDING-CATALOGUE-BRIEF.md:1169-1171`, work item
`:1184`): *"orchestrator to pull a reasonable wishlist of safe part classes (spore puffer among the
candidates), audited class-by-class."* Rider: "i can add later based on player feedback."

**Ground rules honoured** (DIVERSITY-AND-TECH-TREES.md §3.4, `_notes/DIVERSITY-AND-TECH-TREES.md:845-906`):

- The ~23 already-shipped records are out of scope: Class I riders (`:896`) and Class II
  defences/utility (`:897`).
- D1's blocklist is sustained and **not relitigated** (`BUILDING-CATALOGUE-BRIEF.md:1166-1168`):
  every self-replication/duplication part, `Invisibility`, `WallWalker`, `Metamorphosis`,
  `OldElectricalGeneration` (`DIVERSITY-AND-TECH-TREES.md:901-906`).
- Hard rule 4 (`:881-882`): no acquisition randomness. Effect-level saves and rolls are fine — the
  shipped riders all roll saves.
- Grant mechanism assumed throughout: snapshot before butchering + `IPart.DeepCopy(player)`
  (`:888`), so **the source instance's field values are the granted numbers** ("your sting is its
  sting", `:650-652`).

**Citation shorthand:** `D/` = `/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/`,
`B/` = `/home/r/coq/qud_helper/game_base/Base/`. All part classes are
`D/XRL/World/Parts/<Class>.cs` unless noted; all creature lines are
`B/ObjectBlueprints/Creatures.xml`.

**Method:** full census of `<part Name=...>` usage across `B/ObjectBlueprints/Creatures.xml`
(1,107 blueprints' worth), every candidate class read in the decompile for (a) determinism,
(b) blocklist adjacency, (c) player-hostile code assumptions, (d) economy exposure. 36 classes
read end-to-end or skimmed to the load-bearing branch; every verdict cites the line that decides it.

---

## 1. The spore puffer — the author's named candidate. VERDICT: ADMIT, with three findings.

### What actually carries the ability

The puffer's power is **not a plain IPart — it is a mutation class**, `SporePuffer : BaseMutation`
(`D/XRL/World/Parts/Mutation/SporePuffer.cs:10`). Carried by the brooding puff family:
`FungusPuffer` base + `FungusPuffer1-4` (gold/azure/rose/jade), all via
`<mutation Name="SporePuffer" PuffObject="N"/>` (`B/ObjectBlueprints/Creatures.xml:6042-6079`).
Since `BaseMutation` extends `IPart`, both §3.4 verbs can carry it; recommendation below.

### Mechanics, from the code

- **Passive, involuntary puff.** On `BeginTakeActionEvent`, if any **adjacent, non-allied,
  brain-bearing** creature is present and the 20+1d6-turn cooldown is up, it fills **all 8 adjacent
  cells** with the puff gas (`SporePuffer.cs:55-87`; the ally test is
  `!ParentObject.Brain.IsAlliedTowards(item2)` at `:69`).
- **Which infection it puffs is deterministic per world seed.** `PuffObject="0".."3"` resolves once
  via `Stat.ReseedFrom("PufferType")` + a seeded shuffle of the four spore gases
  (Luminous/Puff/Wax/Mumbles) (`:44-49`, lists at `:22-26`). A snapshot taken at kill time carries
  the already-resolved gas name; even an unresolved digit re-resolves identically on the same seed.
  **Hard rule 4 is satisfied**: you get exactly the puff the creature you killed had.
- **The bearer is safe from their own gas** — `Gas.Creator = ParentObject` (`:84`) and
  `GasFungalSpores` exempts the creator (`D/XRL/World/Parts/GasFungalSpores.cs:50,70`).
- **Hidden defensive rider: fungal-infection immunity.** `SporePuffer` registers `"ApplySpores"`
  and returns false (`SporePuffer.cs:107,114-117`); that event is fired live at infection time
  (`GasFungalSpores.cs:107`, `D/XRL/World/Effects/FungalSporeInfection.cs:322`). A grafted puffer
  gland also makes you unpuffable. Worth printing on the slate.
- **Hostiles pathfind around you**: non-allies weight adjacent cells at 97
  (`SporePuffer.cs:96-103`) — a soft melee deterrent, free.
- **Player-hostility check: clean.** The `IsPlayer()` branches (`:50-53`, `:89-92`) exist to
  charge NPCs a full action; the player-bearer path is the *cheaper* one (no action cost). The
  `VillageInit` recolour branch guards on `DescendsFrom("FungusPuffer")` (`:118`) — inert on a
  player. No brain/AI assumptions. Level scaling is a no-op (`:129-132`), so the mutation-verb's
  level-1-3 cap (`DIVERSITY:890`) costs nothing.

### The three findings the record must carry

1. **It puffs at neutrals, and the gas does not spare your city.** The trigger is "adjacent and
   not allied" (`:69`) — a resident who is merely neutral trips it, and once the gas exists it
   infects anyone but the creator who fails the checks (`GasFungalSpores.cs:103-107`), allies
   included. Walking your own market can seed fungal infections in your own people. **I recommend
   shipping it anyway** — it is honest, it is Qud, and §3.6's friction doctrine wants exactly this
   — but the slate must say so in words. *Question for the author: is "your body puffs on its own
   judgment" the product, or do you want a taf-side leash (e.g. suppress while in an owned city)?
   The leash is new machinery; the naked graft is zero new machinery.*
2. **The source drops no corpse.** `FungusPuffer` ships `CorpseChance="0"`
   (`B/ObjectBlueprints/Creatures.xml:6045`), so the §3.5 chain (kill → butcher → preserve) has
   nothing to butcher. The record needs one of: (a) our mod adds a `Butcherable`/corpse override to
   the puffer family (one XML line, catalogue-adjacent data we already own the pattern for), or
   (b) a harvest-from-remains happening. I recommend (a).
3. **Killing one costs Fungi standing.** The brooding puff is non-hostile, faction `Fungi-100`
   (`:6044`). The vat-house's own knowledge gate is fungal culture (`DIVERSITY:818`). The lab's
   most fungal procedure is sourced by killing the Fungi's own — that is creed friction §3.6 gets
   for free, and it should be *said*, not patched.

### Proposed record

```xml
<procedure Key="sporegills" DisplayName="the brooding gills"
           Class="II" Grants="SporePuffer"
           Slots="Body"
           Source="part"
           MinRung="2"
           Cost="30" Bits="002" StaffDays="8"
           Preserved="1"
           Creeds="-Templar" />
```

- **`Source="part"`, not `"mutation"`, deliberately.** DeepCopy carries `PuffObject` and
  `ColorString` for free; the mutation verb re-instantiates by name and would puff *generic*
  spores — the exact defect Creature Control had to hand-patch (`SyncPufferIdentity`,
  `DIVERSITY:678-682`). The part verb makes that bug structurally impossible.
- **Class II rung 2**: it is a defence/utility-with-teeth in the same band as `GasImmunity` — not
  a limb, not a named. Slot `Body` (gills in the flank reads right; no anatomy contention with
  Back/Face named procedures).
- Colour variant = which brooding puff you brought home. Four sources, four records' worth of
  variety, one record.

---

## 2. The wishlist — ten records, ranked

Ranking is by (strength of fit × cleanliness of code × source quality). Each entry: effect, source,
verdict grounds, record sketch fields (all are `Source="part"`, `Preserved="1"`), balance note,
creed note. Slot lists are proposals in `BodyPart.Type` vocabulary (`B/Bodies.xml:5-23`).

**A cross-cutting implementation finding first — the attach point.** Melee combat fires
`"AttackerHit"` on the attacker and `"WeaponHit"` **only on the weapon object**
(`D/XRL/World/Parts/Combat.cs:1146-1155, 1178-1188`). A rider that registers only weapon events is
**inert if DeepCopied onto the player's torso** — it must land on a natural-weapon
`DefaultBehavior` object instead. This is not hypothetical: **five of the nine already-shipped
Class I riders are weapon-event-only** (`BleedingOnHit`, `StunOnHit`, `DischargeOnHit`,
`TemperatureOnHit` register only weapon events; `StoneOnHit` only `"WeaponDealDamage"` — verified
by registration grep across all nine). The lab's grant verb needs a per-record
`Attach="body|weapon"` bit (or infer it from the class), and the shipped ladder needs the same
sweep. Candidates below are annotated **[body]** / **[weapon]**.

| # | Class | Source creature(s) | Effect | Class/Rung | Attach |
|---|---|---|---|---|---|
| 1 | `SporePuffer` | brooding puffs (`:6042-6079`) | spore-gas aura + spore immunity | II / 2 | body |
| 2 | `StickOnHit` | honey/sap/wax/tar soup sludges | melee applies Stuck, saveable | I / 2 | body |
| 3 | `SapOnPenetration` | stat saps, Life Sap, Enigma Snail | permanent stat drain on penetrating natural hits | I / 2 | body |
| 4 | `SapChargeOnHit` | Juice Sap (`:5262`) | drains electrical charge from what you strike | I / 2 | body |
| 5 | `LifeDrainOnHit` | bloody/oily/unctuous soup sludges | melee applies LifeDrain toward you | I / 3 | weapon |
| 6 | `ReflectDamage` | Quartz Baboon (`:6364`), Mirror Bug (`:8207`) | reflects % of incoming damage | II / 2-3 | body |
| 7 | `GiantHands` | Troll King (`:5666`) | Hand/Hands/Missile-Weapon gear needs one fewer slot | II / 3 | body |
| 8 | `Swarmer` | snapjaws, dogs, hyrkhounds (`:1696` ff.) | +hit/+pen per fellow swarmer on your target | II / 2 | body |
| 9 | `DrunkOnHit` | lush/spiced soup sludges (`:10068,10072`) | melee force-feeds 4 doses of wine | I / 2 | weapon |
| 10 | `TemperatureVenting` | the Girsh nephilim, only (`:11345`) | auto-vents body temperature past ±thresholds | II / 3 | body |

### 2.1 `StickOnHit` — "the tarry grip"

- Applies `Stuck` (`Duration`, `SaveTarget=15`, named save) on hit
  (`D/XRL/World/Parts/StickOnHit.cs:26-45`); registers **`"AttackerHit"` and `"WeaponHit"`**
  (`:19-23`) — works from the body. Exactly the shape of the shipped nine: chance, save, duration,
  self-describing rules text (`:53-67`).
- Sources: the soup sludge's liquid-themed pseudopods — `Honeyed/Sugary/Waxen/Tarry Pseudopod`
  carry it with per-liquid saves (`B/ObjectBlueprints/Creatures.xml:10027-10040`); the pseudopod
  is built and equipped by `SoupSludge` per body liquid (`D/XRL/World/Parts/SoupSludge.cs:187-223`).
  **Snapshot caveat**: the part lives on the pseudopod weapon, not the creature — the butcher-time
  snapshot must walk equipped natural weapons, not just `PartsList`. That sweep is needed for the
  shipped ladder anyway (see attach-point finding).
- Sketch: `Key="tarrygrip" Class="I" Grants="StickOnHit" Slots="Arm,Hand,Tail" MinRung="2"
  Cost="20" Bits="002" StaffDays="6" Creeds="-Templar"`.
- Balance: peer of the shipped riders; Stuck is strong crowd control but saveable and vanilla-tuned.

### 2.2 `SapOnPenetration` — "the sap's kiss"

- On a penetrating **natural-weapon** hit (`NaturalOnly=true`, `:66-67`), **permanently** drains
  the defender's named stat: `GetStat(Stat).BaseValue -= num`
  (`D/XRL/World/Parts/SapOnPenetration.cs:88-90`). Registers `"AttackerHit"` +
  `"AttackerAfterDamage"` (`:47-51`) — creature-level, body-graft clean.
- Sources are a family with the stat baked in per creature: Life Sap drains Hitpoints (`:7346`),
  Enigma Snail drains Willpower (`:7926`), the Stat Sap bats (`BaseStatSap`, `:8338`; Strength Sap
  `:8359`, Agility/Intelligence/Ego per variant). **"Which sap you bring home names the stat"** —
  the DeepCopy doctrine at its best, deterministic, zero new fields.
- Sketch: `Key="sapskiss" Class="I" Grants="SapOnPenetration" Slots="Face,Hand" MinRung="2"
  Cost="25" Bits="002" StaffDays="6" Creeds="-Templar"`.
- Balance: permanent drain vs NPCs that die anyway is flavour, not power (1-2/hit); natural-only
  keeps it synergising with Class III limbs rather than every carbide hammer. The nasty edge —
  draining a companion — is the player's own swing.

### 2.3 `SapChargeOnHit` — "the galvanic leech"

- Drains `2d6`-scaled electrical charge from the defender on hit, saveable
  (`D/XRL/World/Parts/SapChargeOnHit.cs:13-49`); registers `"AttackerHit"` + `"WeaponHit"` —
  body-graft clean. `ForceCharge=true` arcs the stolen charge into your own storage if you have
  any, and harmlessly into the air if you don't (`:41-47` fields) — no robot assumption.
- Source: Juice Sap, a bat (`B/ObjectBlueprints/Creatures.xml:5262`), creature-level part;
  Ixlthyxl's copy rides its bite.
- Sketch: `Key="galvanicleech" Class="I" Grants="SapChargeOnHit" Slots="Hand,Face" MinRung="2"
  Cost="25" Bits="0023" StaffDays="6" Creeds="-Mechanimists"`.
- Balance: the anti-robot rider the ladder currently lacks; against flesh it does nothing, which
  is honest rationing. Mechanimist creed friction writes itself.

### 2.4 `LifeDrainOnHit` — "the leech pseudopod"

- Applies the `LifeDrain` effect **toward the attacker** on hit
  (`D/XRL/World/Parts/LifeDrainOnHit.cs:52-58`) — a true vampiric rider, `RealityDistortionBased`
  so normality shuts it off (honest counterplay). **Registers only `"WeaponHit"` (`:31-35`) —
  [weapon] attach**, pairs naturally with a Class III limb graft.
- Sources: the blood-family soup sludges — `Bloody/Oily/Inky/Unctuous Pseudopod`
  (`B/ObjectBlueprints/Creatures.xml:10064` for the 15-20 bloody instance).
- Sketch: `Key="leechpseudopod" Class="I" Grants="LifeDrainOnHit" Slots="Arm,Hand" MinRung="3"
  Cost="45" Bits="0023" StaffDays="10" Creeds="-Templar,-Mechanimists"`.
- Balance: 15-20 drain at Chance=100 is the strongest rider on this list — hence **rung 3**, the
  only Class I here priced above the hall. If that still reads hot, the honest alternative is
  dropping it, not nerfing the copied fields.

### 2.5 `ReflectDamage` — "the quartz hide" (with one sharp edge)

- Reflects `ReflectPercentage` of incoming damage to the attacker
  (`D/XRL/World/Parts/ReflectDamage.cs:33-76`); `IActivePart` with no `ChargeUse`, so it works
  unpowered on flesh; loop-guarded by the `"reflected"` damage attribute (`:34`). Was already
  `[Standard]` in the precedent whitelist (`DIVERSITY:641`) — its omission from the §3.4 ladder
  looks like triage, not verdict.
- **The edge**: shipped sources are Quartz Baboon at `ReflectPercentage="5"`
  (`B/ObjectBlueprints/Creatures.xml:6364`) and Mirror Bug at **`"100"`** (`:8207`). Under
  your-sting-is-its-sting, a Mirror Bug graft is permanent full thorns — degenerate. *Question for
  the author: (a) admit the class and accept that the record lists the baboon and the bug at
  different rungs (5% at rung 2; 100% at rung 3 priced like a named), (b) add one optional schema
  field (`ClampFields="ReflectPercentage:15"`) and spend the doctrine exception here, or (c) admit
  only via the baboon and let the Mirror Bug stay a monster?* My order of preference: (c), (a), (b).
- Sketch (option c): `Key="quartzhide" Class="II" Grants="ReflectDamage" Slots="Body" MinRung="2"
  Cost="30" Bits="002" StaffDays="8"`.

### 2.6 `GiantHands` — "the troll-king's grip"

- Equipment on Hand/Hands/Missile Weapon slots requires **one fewer slot**
  (`D/XRL/World/Parts/GiantHands.cs:11,28-36` — `E.Decreases++` on `GetSlotsRequiredEvent`);
  `IActivePart`, `WorksOnSelf`, no charge. The implant re-equip housekeeping (`:38-48`) only runs
  on implant events; a part-grant takes effect on next equip — cosmetic wrinkle, worth one line in
  the grant code.
- Source: **Troll King only** (`B/ObjectBlueprints/Creatures.xml:5666`) — a rare heroic troll.
  One creature, one graft, a hunt: reads exactly like the §3.7 doctrine but is honestly ordinary
  (no new machinery), so it belongs in the catalogue, not the named list.
- Sketch: `Key="trollkingsgrip" Class="II" Grants="GiantHands" Slots="Hands" MinRung="3"
  Cost="60" Bits="0023" StaffDays="12"`.
- Balance: wielding two-handed gear one-handed is real power for True Kin builds; rung 3 and the
  scarcity of Troll Kings price it.

### 2.7 `Swarmer` — "the pack's tooth"

- +1 hit / +1 penetration per **other hostile swarmer** adjacent to your target
  (`D/XRL/World/Parts/Swarmer.cs:42-52`, bonus math `:96-140`). Deterministic, no AI assumption —
  the one quirk is the fallback `ParentObject.Target ?? ThePlayer` (`:98`), which for a
  player-bearer degenerates to self and yields zero, not a crash.
- Sources everywhere: Dog (`:1696`), snapjaw scavengers/warlords (warlords are Swarm Alphas,
  `ExtraBonus="2"`, `:2548,2564`), hyrkhounds, Naphtaali base.
- **The honest caveat: solo it does nothing.** The bonus counts *other* creatures carrying
  `Swarmer` who are hostile to your target — i.e. it is a **follower-army graft**. In a mod whose
  whole spine is peoples and cities, "the founder fights as one of the pack" is a strong sentence;
  for a lone adventurer it is a dead slot, and the slate should say so.
- Sketch: `Key="packstooth" Class="II" Grants="Swarmer" Slots="Head,Face" MinRung="2"
  Cost="20" Bits="002" StaffDays="5"`.
- *Question: is beast-army synergy a product you want the lab selling? If your recruited
  snapjaws/dogs count (they carry the part), this is the cheapest "kingdom-shaped" graft on the
  board.*

### 2.8 `DrunkOnHit` — "the vintner's fang"

- On hit, force-feeds the defender four doses of wine
  (`D/XRL/World/Parts/DrunkOnHit.cs:24-31`) — real confusion-adjacent debuff through the liquid
  system, pure vanilla machinery. **[weapon] attach** (registers only `"WeaponHit"`, `:12-16`).
- Sources: `Lush Pseudopod` / `Spiced Pseudopod` — wine and oddly-spiced soup sludges
  (`B/ObjectBlueprints/Creatures.xml:10068,10072`).
- **Vanilla bug worth knowing**: the part calls `GetSpecialEffectChanceEvent.GetFor(...)` and
  **discards the result**, then rolls raw `Chance.in100()` (`:27-29`) — effect-chance modifiers
  don't apply to it. Not our bug; does not block; will confuse anyone reading logs.
- Sketch: `Key="vintnersfang" Class="I" Grants="DrunkOnHit" Slots="Hand,Face" MinRung="2"
  Cost="20" Bits="002" StaffDays="5" Creeds="-Templar"`.
- Balance: a joke that is also a debuff; the most Qud sentence on this list.

### 2.9 `TemperatureVenting` — "the nephal's furnace" (question-shaped)

- When body temperature passes ±thresholds for `Warmup` turns, vents it back toward 25 and
  releases steam/cryo gas around you (`D/XRL/World/Parts/TemperatureVenting.cs:33-46,50-95`).
  Real anti-burn/anti-freeze utility with an honest cost: the vented gas is **ownerless** (no
  `Gas.Creator` set, `:88-93` region) and will scald whoever stands next to you, allies included.
- **Source: `BaseNephal` — every carrier is a Girsh nephal**
  (`B/ObjectBlueprints/Creatures.xml:11345`). That is the same shelf as the Cold Regard
  (`DIVERSITY:996`), which is gated `rite:Girsh`.
- *Question for the author: do you want an ordinary catalogue record whose only lawful source is a
  nephal? If yes, it must carry the same gate as the Cold Regard (`MinRung="3"` +
  `Knowledge="rite:Girsh"`) or it quietly cheapens that named procedure's shelf. If that feels
  wrong, strike it — it is the most cuttable entry here.*
- Sketch: `Key="nephalsfurnace" Class="II" Grants="TemperatureVenting" Slots="Body" MinRung="3"
  Cost="70" Bits="0034" StaffDays="14"` + `rite:Girsh` gate.

---

## 3. Loud rejections — 32 classes considered and refused

Format: **class — verdict grounds (cite).** Grouped by disqualifier.

### Player-gated or mechanically inert on a player bearer

1. **`EatMemoriesOnHit`** — the effect is hard-gated on the *defender* being the player:
   `if (defender != null && defender.IsPlayer())` (`D/XRL/World/Parts/EatMemoriesOnHit.cs:24-25`).
   Grafted onto the player it can never fire. Its presence in the precedent `[Standard]` list is a
   Trophic oversight, and §3.4's ladder was right to drop it.
2. **`ThirstOnHit`** — thematically irresistible in this mod (the Salty Pseudopod parches you!)
   and mechanically hollow: every creature has a `Stomach` (base `Creature` blueprint,
   `B/ObjectBlueprints/Creatures.xml:66`) and handles `"AddWater"`
   (`D/XRL/World/Parts/Stomach.cs:470-498`), **but only the player ever suffers thirst** — the
   per-turn water decrement is inside `if (ParentObject.IsPlayer())` (`:241-289`), and an NPC at
   zero water gets a companion status line, not a debuff (`:202-206`); at worst it wastes a turn
   auto-sipping (`:330-339`). Weaponized thirst against NPCs would be new machinery — the second
   job the vision forbids. **Refused loudly, with regret.**
3. **`QuenchOnHit`** — force-hydrates the defender (`D/XRL/World/Parts/QuenchOnHit.cs:29`);
   inverse of the above, same inertness, and where it did work it would *help* the target.
4. **`HealOnHit`** — heals the **defender** (`D/XRL/World/Parts/HealOnHit.cs:28-30`). A graft that
   heals your enemies. (Sludge pseudopod flavour: convalessence.)
5. **`NoBreak`** — immunity to the `Broken` effect (`D/XRL/World/Parts/NoBreak.cs:19-33`);
   item-scope. Player bodies don't take `Broken`; a body graft grants nothing.
6. **`Springy`** — contributes weight-scaled springiness to `GetSpringinessEvent`
   (`D/XRL/World/Parts/Springy.cs:19-29`); trampoline substrate physics, no coherent player
   benefit.
7. **`VisualDistortion`** — cosmetic render splat on a real-time clock
   (`D/XRL/World/Parts/VisualDistortion.cs:25-31`). No mechanics at all.

### Bearer-hostile in effect (the graft punishes its owner)

8. **`BlinkOnDamage`** — unconditional random teleport on **every** non-Unavoidable damage event
   (`D/XRL/World/Parts/BlinkOnDamage.cs:15-20`), no chance field, no toggle. The player loses all
   positional agency the moment anything scratches them. Source: Mercurial, a robot.
9. **`Slumberling`** — forces `Asleep(9999)` on spawn and re-hibernates 10%/turn
   (`D/XRL/World/Parts/Slumberling.cs:33-42,50-68`). A narcolepsy graft.
10. **`Consumer`** — eats objects in the destination cell as you move, suppressing corpse drops
    (`D/XRL/World/Parts/Consumer.cs:31-44`). You would destroy your own loot by walking.
11. **`RealityStabilization`** — projects normality (`D/XRL/World/Parts/RealityStabilization.cs`,
    `WorksOnSelf`, `:207-208`): it would strangle the bearer's **own** teleportation, phasing, and
    every reality-distortion mutation at the origin cell. An esper-hostile self-curse sold as a
    defence. (Also in the precedent `[Standard]` list; Trophic's consumers are slimes, not espers.)

### Ecosystem-hostile (breaks the city, the neighbours, or the aggro system)

12. **`Calming`** — pacifies any AI that decides to attack you, once per creature, permanently
    (`D/XRL/World/Parts/Calming.cs:39-48`), and pins faction feeling at 50 (`:17-24`). An aggro
    nullifier; trivializes the entire hostility system. Sources: Asphodel, the Bethesda baetyl —
    authored quest furniture, not a graftable organ.
13. **`ConfuseOnSight`** — every turn, **every non-faction combatant in the zone** with LOS to you
    saves or is Confused (`D/XRL/World/Parts/ConfuseOnSight.cs:23-40`). The founder would confuse
    their own residents by existing. Sources: Qas and Qon (`B/ObjectBlueprints/Creatures.xml`
    Nephal blocks) — nephal boundary material anyway.
14. **`Lovely`** — everyone who *looks at you* becomes Lovesick for ~3,000 turns
    (`D/XRL/World/Parts/Lovely.cs:19-26`; shipped carrier: the apple farmer's daughter, `:1573`).
    An indiscriminate mind-affecting aura pointed at your own city.
15. **`LeavesTrail`** — slime-puddle litter every step (`D/XRL/World/Parts/LeavesTrail.cs:8-28`);
    marginal tactical value, permanent mess; even vanilla auto-disables it in villages
    (`VillageDeactivate`, `:23`) — which is the engine telling us what it thinks of trails in
    towns.

### Blocklist-adjacent (duplication/replication family — D1 controls)

16. **`Spawner`** — spawns creatures; duplication by another name. D1's "all self-replication
    parts" covers it in spirit; its `[Standard]` placement in the precedent is for slime-flavour
    consumers, not a costed lab.
17. **`Breeder`** — same family, same verdict.
18. **`Impaler`** — the urchin trap-part; its `ClusterSize` branch **creates copies of the
    bearer's blueprint on cell entry** (`D/XRL/World/Parts/Impaler.cs:80-103` —
    `GameObject.Create(ParentObject.Blueprint)`). DeepCopy a shipped `ClusterSize>1` instance onto
    the player and the player photocopies themselves as they walk. Also a stationary-trap part
    with no bite for a mobile bearer (`ObjectEnteredCellEvent`, `:107` — nothing enters your
    cell).
19. **`CooldownOnStep`** — same trap shape, same self-cluster replication branch
    (`D/XRL/World/Parts/CooldownOnStep.cs:44-63`), **plus** a live NRE for any bearer without a
    `Hidden` part: `Hidden part = ...GetPart<Hidden>(); if (!NeedsToBeHidden || !part.Found)`
    (`:73-75`) dereferences null on a player. Player-hostile code assumption, proven at the line.
20. **`CrossFlameOnStep`** — trap-part (flame cross on step, `D/XRL/World/Parts/CrossFlameOnStep.cs`),
    fire hazard to everything adjacent including your own people; same stationary assumptions.
21. **`CreateObjectOnHit`** — parameterized object spawning on hit with a decrementing,
    never-refilled `Charges` (`D/XRL/World/Parts/CreateObjectOnHit.cs:12-17,26-40`): dead weight
    after N hits, spawn-adjacent, and its one shipped carrier is a robot missile launcher.

### Charge/chrome province (inert on unpowered flesh; C2/Addendum-19 territory, not the lab's)

22. **`GroundOnHit`** — `IPoweredPart`, `ChargeUse=100` (`D/XRL/World/Parts/GroundOnHit.cs:26-31`);
    without charge `IsReady` fails and the rider never fires. Sole source: the Naser Cannon, a
    robot natural missile weapon. Anti-air is lovely; this class needs a power system we do not
    graft.
23. **`PointDefense`** — `IPoweredPart` + requires a slaved `WeaponSystem`/equipment to shoot with
    (`D/XRL/World/Parts/PointDefense.cs:15-37,233`). Turret chrome.
24. **`ForceEmitter`** — `IPoweredPart`, `ChargeUse=500` (`D/XRL/World/Parts/ForceEmitter.cs:45`).
    Force-bubble chrome.
25. **`FabricateFromSelf`** — `IPoweredPart`, `ChargeUse=1000`
    (`D/XRL/World/Parts/FabricateFromSelf.cs:76`), and an item-fabrication economy hole
    parameterized by blueprint — a modded creature with
    `FabricateFromSelf FabricateBlueprint="<anything>"` becomes a mint the day it ships (hard
    rule 1 makes the whitelist a contract for third parties too).
26. **`LiquidProducer`** — free liquid forever (`D/XRL/World/Parts/LiquidProducer.cs:11-25`,
    `ChargeUse=0` so it *would* run on flesh). In a mod whose spine is water-as-covenant, a
    liquid-from-nothing graft is an economy attack surface **and** it cheapens the Weeping Graft,
    §3.7's crown piece, which already owns "you weep liquid" as a named, once-ever procedure
    (`DIVERSITY:994`). The named list eats this class.

### Boundary powers (one-ruling-each material, not ordinary records)

27. **`MentalShield`** — total immunity to the mental damage school, all seven mental effects, and
    telepathy/empathy reception (`D/XRL/World/Parts/MentalShield.cs:47-66,85-99,113-120`). Two
    reasons it cannot be an ordinary record: (a) it is a **boundary power** — permanent immunity to
    an entire school is the same size as Invisibility, and D1 says boundary powers arrive only as
    named procedures; (b) its carriers are **base blueprints** — `BaseOoze`
    (`B/ObjectBlueprints/Creatures.xml:380`), `Robot` (`:683`), `MutatedPlant`, `MutatedFungus` —
    so sources are everywhere and the record would be a commodity. Blocking telepathy also blocks
    quest content delivered by it. *If the author wants "the mind of brass" it should be a fifth
    named procedure argued on its own — which §3.7 explicitly declined to invent.*
28. **`HologramMaterial` / `HolographicIvory`** — you become light; near-total damage immunity.
    Same size as Invisibility. Blocklist-adjacent in spirit; refused.

### On-death triggers (fire when the bearer dies — nothing to sell)

29. **`LiquidBurst`** — sprays liquid on `BeforeDeathRemoval`
    (`D/XRL/World/Parts/LiquidBurst.cs:21-27`). Also `DischargeOnDeath`, `BurstOnDeath`,
    `AcidCorpseExplosion` — same family, same reason: the player pays a season and a fortune for a
    firework at their own funeral.

### Sourceless, chaotic, or the numbers say no

30. **`CleaveOnHit`** — the cleanest rider code on the board
    (`D/XRL/World/Parts/CleaveOnHit.cs:65-80`, rides `Axe_Cleave.PerformCleave`) and **no lawful
    source**: every shipped carrier is a Templar mech *vehicle* or its `MachinedEdge` natural
    weapon (`B/ObjectBlueprints/Creatures.xml:9244,9268` — census count 3, all vehicle-side).
    Vehicles are not butcherable creatures; §3.5's chain has nothing to bite. *Question-shaped
    escape: if you want it, the honest route is an authored source creature, not a salvage verb —
    a salvage verb is a new preservation chain.*
31. **`MutateOnHit`** — defender saves or gains the `Mutating` effect and eventually a **random
    mutation** (`D/XRL/World/Parts/MutateOnHit.cs:36-42`). Against NPCs this can *buff the enemy*
    at random; the Gamma Moth's chaos is its own point, not a product. Effect-RNG that changes the
    fight's shape, not just its numbers — against the spirit of hard rule 4 even though the letter
    only covers acquisition.
32. **`BoomOnHit`** — `LiquidNeutronFlux.Explode` on hit (`D/XRL/World/Parts/BoomOnHit.cs:24-27`)
    — a neutron-flux explosion, the single largest damage event in the game, adjacent to the
    bearer. Source: the Neutronic Pseudopod. No.

**Categorically skipped without individual writeups:** all `AI*` behavior parts (AI-only by
construction), species-identity parts (`Raycat`, `Plastronoid`, `MoltingBasilisk`, `TrollKing`,
`BoneWorm`, `GreaterVoider`, ...), spawner/social scaffolding (`HasGuards`, `HasSlaves`,
`CherubimSpawner`, `ConvertSpawner`, `PariahSpawner`, `Nest`, ...), vehicle parts, and pure
render/material markers (`Metal`, `WallColor`, `RandomColors`, ...).

**Borderline, parked (not refused, not wishlisted):**

- **`MonochromePoisonOnDamage`** (Adiyy, natural-gear-gated,
  `D/XRL/World/Parts/MonochromePoisonOnDamage.cs:36`) — applies `MonochromeOnset`; the disease's
  bite is largely player-experiential (vision), and I could not establish a meaningful AI-side
  penalty. Park until someone verifies what Monochrome does to an NPC.
- **`ElementalDamage`** (natural-weapon part, e.g. `Gyrohumor_Pseudopod` `Damage="1d8+20"`
  Attributes="Heat", `B/ObjectBlueprints/Creatures.xml:6039-6041`) — "your fist burns" is a fine
  rider but it is weapon-attach, parameterized by source at wildly varying numbers, and overlaps
  the shipped `TemperatureOnHit`. Revisit only if Class III limbs want default-weapon riders.
- **`Pettable`** — flavour-only; a lab that charges staff-days cannot sell a pat on the head,
  but if a zero-cost flavour tier ever exists, it is the first entry.

---

## 4. What I could not verify

1. **How Trophic Absorption makes weapon-event-only riders fire for its absorbers.** The
   attach-point split (§2 preamble) is proven in the base game (`Combat.cs:1146-1188`), but I did
   not re-open `M/CreatureControl/` to see whether `TrophicRepertoire` re-homes such parts onto
   natural weapons or simply eats the inertness. Whoever builds the grant verb should check,
   because the answer decides whether the **shipped** ladder's `BleedingOnHit`/`StunOnHit`/
   `DischargeOnHit`/`TemperatureOnHit`/`StoneOnHit` records work at all as body grafts.
2. **NPC thirst side channels.** I verified the `Stomach` decrement and consequence paths are
   player-only in `Stomach.cs`; I did not exhaustively rule out some other system reading NPC
   `Water` for a penalty. The ThirstOnHit rejection is robust to being slightly wrong here (the
   effect would still be one debuff nobody can see).
3. **Corpse/butcherability of every wishlist source.** Verified: brooding puffs drop nothing
   (`CorpseChance="0"`, finding §1.2); trolls drop Troll Corpse. Not individually verified: soup
   sludge, stat sap, Quartz Baboon, Mirror Bug, Juice Sap corpse yields. The §3.5 chain should be
   spot-checked per record at build time.
4. **`Monochrome`'s AI-side effect** (borderline item above).
5. **Whether recruited/beguiled snapjaws retain `Swarmer` in follower state** (they should — the
   part is on the blueprint — but I did not trace follower conversion for part stripping). Affects
   the `packstooth` pitch.

---

## 5. One-paragraph summary for the author

Ten records survive the audit: the brooding gills (your spore puffer — admit, with a no-corpse fix,
a puffs-at-neutrals warning, and a free spore-immunity rider the slate should brag about), four
sludge-sourced riders (tar grip, leech pseudopod, vintner's fang — plus the sap's kiss and galvanic
leech from the sap bats), two defensive hides (quartz reflect — pick your answer to the Mirror
Bug's 100% — and the nephal furnace, which only works if you accept a nephal-sourced ordinary
record), and two utility grafts (the troll-king's grip, the pack's tooth). Thirty-two classes were
refused with line-cited reasons — the loudest: `EatMemoriesOnHit` literally cannot fire for a
player attacker, weaponized thirst is mechanically hollow because only the player ever thirsts,
`MentalShield` is a boundary power wearing a part's clothes, and both urchin trap-parts would
photocopy the player as they walk. One structural finding affects the *already-shipped* ladder:
five of the nine Class I riders register only weapon events and are inert as body grafts — the
grant verb needs an attach-point bit before any rider record ships.
