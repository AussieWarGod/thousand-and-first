# Decision ledger

Every significant call, why it was made, and what would reverse it. Commit messages carry the
detail; this is the index. Where a decision was later **overturned**, that is recorded rather
than edited away.

**Authority note (2026-08-25).** This ledger is chronological, not a shortcut around later
rulings. Direct author corrections and later approved addenda in
`BUILDING-CATALOGUE-BRIEF.md` supersede an older entry here. An older decision that has changed
must say **OVERTURNED** beside the original text; silently treating it as current is a defect.

---

## Framing

**The city is a character you have a relationship with, not a process you administer — NARROWED
2026-09-01.**
Drawn from what separates the survivors (Suikoden, Kenshi, Dwarf Fortress, Terraria) from the
graveyard (Kingmaker, Fallout 4, Hearthfire). Everything else is downstream.
*Reverses if:* playtesting shows players want a management screen after all. Unlikely; the
evidence base is twenty games deep.

**Correction:** the survivor/graveyard ranking and “twenty games deep” causal-confidence claim are
superseded. Those games serve different audiences and are design quarries, not one natural
experiment. The supported decision is narrower: an opt-in Qud cast-and-place should visibly answer
adventure through people, places, and physical function while leaving scheduling and hidden
management pressure out. Current authority: `../VISION.md` and
`RESEARCH-ALIGNMENT-AUDIT-2026-09-01.md`.

**Renewal, not reclamation — RECONCILED 2026-09-01.** The Templar already own "reclaim and
purify", so a player city tells the other story — Abram sowing watervine, Resheph's harborage.
Later generalised: the system never judges, the *world* does, so any covenant (including a Girsh
necropolis) is playable and priced by the faction web.

**Correction:** Abram's renewal is tragic and contested; Resheph's harborage is poetic and
cosmopolitan cosmic-port resonance, not sanctuary proof. The retained law is plural, priced,
contestable covenants and situated histories, not an uncomplicated canonical renewal story.

**Two ledgers, not one.** Kingdom standing is separate from personal reputation, coupled by a
spillover that dampens as the city gains identity (50% at Camp → 10% at City).
*Why:* dynasty requires kingdom relationships to outlive a character; one ledger double-counts;
and "loved founder, hated ministry" is where half the stories live.

## Architecture

**Lazy catch-up, never background simulation.** The engine's own idiom (`ZoneRepair`), and
forced anyway: only one zone is ever active. Confirmed by the power investigation.

**Witnessed-only accounting — OVERTURNED by Addenda 8 and 10(a).** The original rule accrued
consequences only while the player was present and capped them at three days. Current authority
uses world-time, pushes one warning at the brink wherever the founder is, names the arrest action,
then permits the warned consequence to fire in absence after its fair world-day window. Ordinary
production and consumption likewise reckon full elapsed world-time. Awareness controls warning
and telling; visit cadence is never the simulation clock.

**One survey per due active-seat reconciliation.** Was ~20 full-zone scans; now
`KingdomSurvey` binds one maintained classifying index shared by city, growth, trade, construction,
petitions, upgrades, raids, wear, lab, offices, reach, guests, faith, roads, crops, networks,
visual state, residents, and transient jobs. Physical commits observe additions/removals into that
same index. Exact-cell/object reproof is allowed; a second whole-zone helper scan is not. Reports
and explicit recovery outside the bound pass may take their own survey. Static repair is tracked in
`CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md`; dense native instrumentation remains the closing
evidence. Forced by the performance audit and Foundation's bounded-wake law.

**Errors never escape into the engine.** Every entry point wrapped in `Guard`. Vindicated
immediately: the shopkeeper NRE would otherwise have silently killed trade and raids forever.

**Everything data-driven.** Registries load through `DataManager.YieldXMLStreamsWithRoot`, so
third-party mods extend with no code. A hardcoded catalog is a defect by our own standard.

**One canonical building map; pose is runtime authority.** Architecture is authored north-facing.
Heart or road frontage resolves north/east/south/west before payment, the preview names that
facing, and the frozen receipt rotates map, footprint, walls, doors, fixtures, anchors, yards,
entrances, and routes together. Four routine directional maps would multiply drift and review
cost without adding architecture. Separate maps are allowed only when direction changes the
design itself; an asymmetric sprite may instead need a posed asset. *User-confirmed 2026-09-01.*

## Balance

**Threats key off provocation, never prosperity.** RimWorld's wealth-scaling teaches players
to fear their own growth; Kenshi's model is legible and negotiable.
*This overturned our own earlier "prosperity scales threat" line.*

**Threat must cost more than its remedy.** Tribute 6 drams vs. raids plundering up to 24.
Originally inverted (12-dram tribute vs. free loot) — raids were strictly profitable.

**Stage gates on built capacity, not stored water**, because stored water can be lent and
withdrawn; capacity must be built.

**Consumption scales with elapsed time, not event cadence.** Upkeep was charged per arrival
interval, which grows with population — a City checked in monthly and thirst evaporated.

**Failure keeps a loyal floor, but stage ratcheting was OVERTURNED by Addendum 8.** Emigration
still stops at a loyal core of two and Camp remains the absolute stage floor. Above Camp, supported
level and stage may subside toward what standing infrastructure, supply, labour, and social shade
can sustain. Subsidence leaves owned ruins and never erases the title; it is not silent regression.

## Player experience

**Invitation, never obligation.** Per-module toggles; a player who never founds anything sees
one rumour and one ruin.

**Absence is world-time, not visit cadence — REFINED by Addenda 8, 10, 11 and 12.** Missed work is
reckoned from durable world ticks and told through bounded receipts/homecoming summaries; it is
never recomputed from how often the founder visits. Gains crystallise when due. Warned brinks may
resolve in absence after their fair window. Module-specific caps bound stored evidence or physical
rendering, not the elapsed simulation itself. The homecoming digest remains a gift-opening, not an
inspection or a backlog replay.

**Growth must name its cause.** Arrivals cite the deed that drew them. Uncaused arrivals are
what "feels drip-fed".

**Settlers are people.** Named through the engine's own generator, listed on a roll with origin
and arrival date. *User direction, and correct.*

**Homes and hands gate everything.** Beds cap population; works need crew. *User direction.*
Answers "why would I build anything" on the first visit.

**Partial manning scales output; some works are all-or-nothing.** *User correction to a binary
model I had shipped* — a half-crewed crank still turns, but there is no half a shopkeeper.

**Petitions, not a quest board.** Generated from real state, spoken by a named settler, always
declinable, one at a time, silent when the settlement is content.

**Petitions remain a custom, city-carried lifecycle, not vanilla journal quests.** Closed by the
engine-truth audit in `CODEX-ENGINE-TRUTH-BATCH-1-ANSWERS.md` Q6 and
`QUEST-HANDLING-RESEARCH.md`: vanilla starts force a blocking modal, expose no abandon verb, and
route failure through global finished-quest state and completion gates. The petition book instead
freezes the real requester's body and words, survives succession with its city, and keeps decline,
option pause, expiry, and completion under its own receipts.

## Content

**Salvage must be commissioned into the works.** *User's idea, better than the rule it
replaced.* A hauled-home reactor is inert until surveyed, anchored and certified — cheaper than
building new, never free, and gated on the Ascent tier you are competent to certify.
*Replaced:* a flat "the budget only counts what you built", which would have made dragging a
reactor home pointless rather than a triumph with a price.

**Power: one source of truth, two time-scales.** Machines are the truth; the civic budget is a
projection of them. Links are commissioned per connection, never per cable.

**Vertical settlements are stacked zones** (how Grit Gate and the Tomb are built). Within a
zone there is no stacking, so a palace is a footprint repeated across levels.

## Process

**Hand-verify agent findings.** Forced by the workflow that reported "nothing confirmed" when
in fact every verifier had died. Findings were real.

**Public repo, MIT.** *User decision.* Matches the platform design and the community's norms.

**Vanilla idiom governs implementation; library discipline governs contracts.** Resolves the
contradiction between "no doc-comment banners" (vanilla style) and enterprise documentation.

## Inheritance: what a settlement becomes when you are gone

**The settlement outlives the founder, and what it becomes is the last thing the founder
does.** When a run ends, the settlement is sealed into a record and can appear in the next
playthrough in an inherited state. This is the mod's answer to permadeath: not a stash that
survives, but a *place* that survives, in whatever condition your last life left it.

**The ladder is how much settlement there was to lose, tested against one hard generation.**
Four inherited states:

| State | Reads as | Reached by |
|---|---|---|
| Held | Largely intact site and a chronicle of a strong polity; no living successor is reconstructed | A strong seal: a town or city with people, walls, and stores when sealed |
| Faded | Thinner surviving fabric and a chronicle of decline; no living successor is reconstructed | A middling seal, or a large settlement that was withering when sealed |
| Abandoned | Everything standing, nobody home, doors open, cistern dry | A weak seal, or any seal sealed with nobody left in it |
| Ruins | Streets still legible under the collapse, something else living there | Little there to begin with, or a hard interregnum on a weak seal |

Seven representative archetypes, each swept across all 100 draws. This is a **sample chosen to
span the range**, not a survey of the whole reachable space: enough to show the ladder uses all
four rungs rather than collapsing into two, not enough to claim more than that.

| Archetype | Reachable now? | Seal | Outcomes across the interregnum |
|---|---|---|---|
| dying camp | yes | 2 | Ruins 100% |
| small camp | yes | 9 | Ruins 100% |
| steading behind a palisade | yes | 31 | Abandoned 43%, Ruins 57% |
| walled village | yes | 65 | Held 28%, Faded 49%, Abandoned 23% |
| walled town, full cisterns | yes | 90 | Held 90%, Faded 10% |
| withered town | yes | 45 | Faded 28%, Abandoned 49%, Ruins 23% |
| great city | **no — theoretical** | 100 | Held 100% |

**City was not reachable under the flat forty-work cap — OVERTURNED before public release.** The
original proof remains useful: forty one-bed bunks could not reach population 50, still less carry
1024 storage. The building pass now budgets plots by stage (`MaxBuildingsForStage`: 40 / 70 / 110 /
160 / 220 per zone), bunks carry four beds, and the city model prices all 880 possible work rows
across four City zones. `MaxBuildings` survives only as an `[Obsolete(..., true)]` binary adapter;
it is not live authority. Reachability now depends on the staged resource/crew/ground gates, not
the disproved flat ceiling. This correction does not claim human playtest evidence for reaching
City; it removes the arithmetic impossibility and the forty-row simulation truncation.

The draw matters most in the middle and not at all at the ends, which is the intent: a walled
town is rarely lost to bad luck, a dying camp is never saved by good luck, and everything
between is a genuine story.

**There is no clock. — OVERTURNED 2026-08-19, user ruling.** The first version of this keyed
the ladder to days since the founder last stood in the settlement. That is now wrong on
purpose:

- a living save never languishes because the player has not visited;
- visits reset no hidden clock;
- wall-clock time is never the default authority;
- death or deliberate retirement **seals** one immutable condition;
- a later save applies exactly one fictional intergenerational transition to it.

This is only the inheritance clock ruling. Current-run food, water, work, wear, and subsidence
still advance through ordinary world time whether or not the founder visits; no separate
inheritance/languish clock reads visit cadence.

*Why it had to go:* I flagged in the original entry that a languish clock was the mechanic most
likely to breed babysitting, and that if anyone ever detoured home to reset a timer the
constants were wrong. The ruling removes the timer instead of tuning it, which resolves the
concern rather than managing it. A settlement is now judged on **how well it was left**, never
on how long ago.

**The seal is one bounded number.** `SealedVigour` reduces stage, population, defence, and
stored water to 0–100, capped per term. It is a summary and not a save: no items, no charge, no
liquids, no object state. Every term only ever *adds*, and a monotonicity test enforces it —
the first implementation measured water as *days of supply*, which divided stores by population
and meant a founder could raise their own inheritance by letting settlers die before the end.
That is exactly the class of exploit the gate exists to catch, and it caught it.

**One draw of fortune, bounded and resolved at promotion.** `InterregnumRoll` is a pure hash of
**immutable legacy data only** — lineage, origin, generation, revision. Never the target world's
seed, the calendar, system time, or any stream the player can reroll. *I had this wrong first
time:* I wrote that the legacy should be combined with the new world's seed, which would have
handed back precisely the reroll it claimed to prevent — regenerate the world, draw again. The
fate is fixed when the legacy is promoted, not when it is placed, so it arrives in every world
the same way and retrying generation reproduces it byte for byte.

The swing is capped at 40 vigour points, moving the outcome a band or two. Under the **original
uncapped** draw — the roll subtracted whole — one bad interregnum could take a settlement sealed
at perfect vigour all the way down to Abandoned, which made the years between lives the author
of the story and the founder's work irrelevant. That is why it is capped: with the current
40-point swing, a seal of 100 comes through Held on every draw there is.

**The chronicle survives every state, including Ruins.** There is always a findable, readable
artifact — the founding book, a scratched cask, a stone — carrying the chronicle and the roll
of settlers into the next life. This is the payoff and the whole point: a stranger walks into a
dead town and reads who lived there, who arrived from the salt marshes, what they built, and
the name of the founder who never came back. Losing the settlement must never lose the story.
*Reverses if:* nothing. This is load-bearing. A state that erases the chronicle is a defect.

It crosses as a **namespaced apocryphal echo**, not as history the world vouches for. The
settlement enters the new world under its own identity in that world's records; it never
imports the old faction registry key and never touches vanilla `PlayerCult`. What a later
character finds is a local account of a place, which is also the honest fiction: Qud is full of
records that disagree with each other.

**The people are not the old roll walking around.** The named roll crosses as *history* and must
never be respawned as the same creatures. The author reopened the positive living-echo direction on
2026-08-27: a successor, namesake, claimant, or envoy is a fresh current-run person derived only
from bounded institutional facts and an explicit lore role. Exact old creature/object continuation
is `REJECTED`, including replay of possibly modded creature graphs. `VISION.md` owns this canonical
disposition, `V1-POLITY-SCOPE.md` expands its safety/evidence boundary, and
`docs/V1-UNDEFERRAL.md` owns active closure; together they supersede generic “current
v0,” “later-version,” and positive deferral wording elsewhere.

**No item inheritance, ever, and say so up front.** Layout, name, chronicle, condition, founder
cairn, and bounded faction identity facts needed by the history carry. No living successor does.
Loose loot, stored equipment, charge, and water do not. A settlement that returned your
stash would turn permadeath into a bank and every player into a hoarder who dies on purpose. The
founding book states this plainly so it is never a surprise discovered too late. Inherited works
are a real benefit, so their restored condition and any starting civic supplies must be capped —
otherwise "no items" is circumvented by banking water or leaving stocked machines behind.

**Restoration should be worth the walk — an intention, not yet a promise.** The bones being
already there is what makes your own ruin the most personal entry in the special-sites
restoration pillar, and the new character wants a reason to go that is not purely sentiment.
But *how much* cheaper is unsettled and deliberately unpromised: inherited works are a real
benefit, and an uncapped discount is how "no item inheritance" gets circumvented by the back
door. The exact concession is a balance question for after the seat reconstruction works at all.

**The MVP inherits one seat zone.** Not a district, not a region, and not several generations
layered on one map. Multi-district geometry waits until settlement state and cross-zone
transfer are proven, and the footprint has to be validated against bounds, connection cells,
stairs, terrain, reachability, and whatever the new world already placed there before a single
object goes down. The stacked-civilisations fantasy is right for Qud and wrong for a first
slice; it is a later feature, and calling it MVP was overreach.

**A founder's cairn names your dead character and how they died,** drawn from the actual death.
That much is cheap and lands hard, and it is the piece of the layering fantasy worth having
first.

**Abandoned is intact and derelict, never damaged — CORRECTED 2026-08-19.** The first
implementation mapped `Abandoned` onto the engine's `Ruiner` at level 10, which detonates
explosions across the zone. That directly contradicted this section's own promise that
everything is still standing, and Codex caught it. Ruination is now ours: `StandingPercent`
leaves 25–60% of structures up on a purpose-built deterministic transform over a fresh
reconstruction canvas. `Ruiner` is never called against a live or authored zone — it would
damage whatever else the new world had already placed there.

The floor exists because a ruin has to stay readable as a *place*. Recognising where you once
lived is the entire payload of inheriting a ruin; undifferentiated rubble delivers none of it.

*Engine seams and the v0 transaction are implemented; native evidence remains.* Cross-run storage
uses `DataManager.SyncedPath`; terminal sealing, promotion, reservation, worldgen application,
durability proof, and release are separate monotonic receipts. Joppa's supported extension point
uses the remaining-site allocator, and authored spatial snapshots reconstruct through the same
architecture stamper while excluding items, liquids, and charge. `INHERITANCE-SEAMS.md` remains
the engine evidence and safety contract. Current cold-save, interruption, subscribed-install, and
human spatial-fidelity protocols still gate release; implementation is not a signed receipt.

## Known-wrong things we corrected in our own standards

- `SerializationVersion` as a blanket rule — **inert in this engine** when nothing consumes it.
  That much was right. **The replacement was also wrong, and this entry is the correction of a
  correction.** `[FieldSaveVersion(N)]` is *not* a mod schema mechanism: it is read only by
  `SerializationReader.ReadTypeFields` — the positional reflection path — and compared against
  `FileVersion`, the *engine's* save-file version, which a mod neither controls nor can advance.
  `ReadNamedFields` ignores the attribute entirely. A mod versioning its own state has exactly
  one real option, which is what `KingdomSystem` now does: `WantFieldReflection = false` plus a
  custom `Write`/`Read` carrying a magic marker, a schema version, and named fields.
  *Filed by Codex; verified independently at source before rewriting.*
- "Prosperity scales threat" — replaced by provocation-keyed threat.
- Binary staffing — replaced by per-type manning.
- Ability class `"Kingdom"` — invented; vanilla has ~9 real categories, now uses `Skills`.

## Architectural style does not cause dissent — ruled 2026-08-29

A city style is evidence about ground, material culture, technology, and the people who built
there. It is not an ideology. Two cities in one realm may therefore carry different architecture
without accumulating an invisible friction score or opening a fourth brink. Creed, explicit civic
decisions, directional relationships, and witnessed incidents already own caused conflict. If a
future event makes architectural difference matter, that event must name its cause and enter one
of those authorities; mere style mismatch never mints hostility.

*Reverses if:* the author asks for a specific visible dispute whose choices and resolution cannot
be represented by the existing caused-conflict lanes. It does not reverse for a desire to make a
meter move.

## Plot geometry and authored transformation — ruled 2026-08-31

**Every plot tier uses one even-axis law:** S `6x4`, M `8x6`, L `12x10`, XL `20x18`.
The consistent centre is the seam between two cells, so doors, aisles, monuments, and paired
fixtures can straddle one axis in every cardinal pose. `20x18` is the largest lawful rectangular
XL: the documented minimum mature-zone mix occupies 936 of the 957 plot cells available inside
an ordinary 80x25 zone; `20x20` would occupy 976 and violate that budget. The interrupted
`25x17` experiment is superseded.

**Growth is authored transformation, not universal accretion.** This applies to the civic heart
and to every other building and reserved plot. Each transition chooses the form its purpose needs:

- **additive** keeps all standing fabric and adds within unused space;
- **additive-expand** keeps all standing fabric and adds an authored wing, court, yard, or service
  band in a proved larger envelope;
- **renovate** may rebuild walls, floors, rooms, circulation, and stateless furnishings inside the
  reserved lot;
- **renovate-expand** rebuilds where useful while also growing into a proved larger envelope;
- **replacement** is an explicit strike-and-fresh functional conversion, never an accidental
  consequence of two maps differing.

The behavior root, LotId, paid receipt, immutable founding basin, non-empty containers, liquids,
resident/custody state, names, wear, player additions, and third-party state remain protected.
An authored handover may relocate a protected fixture while preserving its exact object identity;
otherwise occupied or ambiguous work refuses before debit. Static scenery is not protected merely
because an earlier plan placed it. A larger reserved lot may support a fuller later plan; actual
lot-envelope growth additionally proves adjacent ground, lanes, entrances, and both rectangular
poses before commitment. Generated larger-lot plans therefore cannot universally be the source
map plus padding.

**This is the final named pre-Alpha geometry break.** No public release or tag exists, and the
standing pre-release policy explicitly waives development-save compatibility. Pre-redesign saves
are refused clearly rather than silently interpreting old 5x4/12x9/20x14 receipts under the new
law. The geometry/version boundary freezes before public Alpha testing.

**The hosted arcology is not an XL lot.** Its surface heart/gateway may occupy the XL reservation,
but the megastructure itself spans one complete parasang's 3x3 local-zone grid on each of three
storeys: 27 ordinary zones under one unique surface-root authority. The former
single-interior/single-lot implementation is a stub and cannot satisfy the feature by enlargement
or decoration. `_notes/ARCOLOGY-ZONE-TOPOLOGY.md` is the implementation and acceptance contract.

**Quickstart stock is physical.** A pre-founded start may provide water-holding and resource-
holding Camp shelters, filled vessels, food, and materials. Holding is not production: these
buildings never mint water or grant an indefinite stock aura. The optional advisor explains the
same production rules ordinary play uses.

*Reverses if:* playtesting demonstrates the exact tier dimensions cannot produce readable Qud
architecture or the mature-zone mix is itself deliberately changed. The transformation and
physical-state laws do not reverse merely to simplify a generator.

## Creed visual idioms and Mechanimist scope — ruled 2026-09-01

**Creed distinction uses physical function before new art or new currency.** Joppa, Kyakukya,
snapjaw, and farmers' stores are separate usable empty containers with native basket, fired-jar,
red woven-cache, and timber-bin silhouettes. Generated campuses receive matching sealed direct-
`Furniture` markers, never copied containers. Goatfolk expansion extends its witness line with
canvas pennons instead of multiplying the authored ritual horn posts. Gyre's connected native
bone-wall screen is vowed bone and chitin over a paid fieldstone spine; this does not invent a
bone commodity or rewrite the existing material bill. Chavvah's school uses native crystalline-
trunk wall fabric as a real grown cell.

**Mechanimists receive no early small proxy.** Their one creed work remains the L,
Town/workshop-gated reliquary. Machine curation needs the processional case/relic room and its
material/standing decision; a decorative S shrine would duplicate the Six Day Stilt's existing
pantheon, dilute the covenant, and violate the one bespoke work per admitted creed census.

*Reverses if:* native capture shows one borrowed silhouette communicates the wrong interaction,
or playtest shows Mechanimists need an earlier behavior-bearing decision that cannot be supplied
by the existing covenant path. It does not reverse to add decorative variety alone.

## Creed is an affiliation umbrella, not universal theology — ruled 2026-09-01

Preserve `Creed` and `KingdomCreed` on public/save/catalogue surfaces, but resolve behavior through
mergeable typed kinds: community, people, polity, order, doctrine, cult. Only doctrine/cult and an
explicitly opted-in order may use belief, conversion, consecration, shrine pull, or theological
pressure. Other kinds use covenant/allegiance/adoption and retain every architecture, arrival,
declaration, history, and dissent role. Unknown third-party keys fail closed to neutral affiliation
without save migration. Installed-source mapping and disputed rulings are frozen in
`_notes/CREED-KIND-EVIDENCE.md`.

*Reverses if:* updated primary Qud text changes one curated identity, or a third-party author ships
an explicit valid semantic row. It does not reverse by inferring from faction grammar or worship
attitudes.

## Open decisions

- **Resolved 2026-08-29:** keep the trimmed canonical `VISION.md` public. Contributors need the
  product laws and bounded direction; its header separates intent from schedule and links current
  evidence. Private research, comparative criticism, raw agent/session material, and local paths
  remain outside the public vision.
- Dynasty persistence is implemented through seal/promotion/reservation/application receipts;
  current native succession, cold-save, and subscribed-install evidence remains unsigned. Treat
  this as a release gate, not an unbuilt design question.
- Whether to keep the WSL clone or work directly on the Windows path over `/mnt/c`.

## Larger starter-housing lots show use without inventing an estate — ruled 2026-09-01

S/M/L/XL names the reserved plot, not the building. The compact tent, hut, mud-hut, and recovered-
block bills are invariant by reservation size and pay for only their exact 3x2–5x4 shelter,
fixtures, and natural ground treatment. Generated larger lots must not multiply rooms, beds,
storage, hearths, walls, or households to fill space the player did not buy.

They also must not imply a fully developed estate through repeated formal path grids. Their
larger-lot grammar is one exact frontage route plus a restrained family-specific court: canvas
dooryard, timber return court, mud drying apron, or angular salvage-block corner. At XL the visible
natural path treatment stays within 25–35 cells; the rest remains honest future yard. Optional
yard trades arise through their own gameplay and receipts. A genuinely occupied XL estate needs a
new paid successor or the existing strike-and-commission route into house, terrace, manor, or
court; decoration cannot substitute for progression authority.

*Reverses if:* playtest establishes a new paid size-specific starter tier and its exact material,
labour, fixture, and transition contract. It does not reverse because empty future ground is less
spectacular than fabricated construction.

## v1.0 is the public Alpha feedback release — ruled 2026-08-31

The terminal target for this work is a **public, playable v1.0 Alpha**, not an indefinitely
polished private candidate. Its purpose is to put the complete intended game in players' hands,
learn from real play, and make outside contribution practical. The version remains `0.2.0` while
the tree is structurally in motion; the frozen public candidate becomes numeric `1.0.0`, with the
Workshop title and description saying **Alpha** plainly.

Alpha does not mean a knowingly incoherent vertical slice. Before publication, the complete
founding -> building -> provisioning -> growth -> threat/relationship -> return/inheritance loop
must be understandable and playable; every shipped building must be structurally valid,
functionally legible, era/material coherent, reachable, and free of known release-blocking visual
faults; install, save/cold-load, removal, and bounded catch-up must work; and the repository must
give a new contributor one documented, reproducible path from checkout to a tested change.

Alpha also does not require every subjective embellishment to be final. Once automated/static
coverage, representative native galleries, the production persona matrix, a fresh end-to-end
playtest, packaging, and subscribed-copy smoke are green, non-blocking aesthetic alternatives and
balance refinements become public feedback work. A valid but ugly, misleading, inaccessible, or
functionally nonsensical plan remains blocking; an arguable preference between two already-good
forms does not.

The checked-in developer scenario/persona harness is the production conveyor, not optional demo
tooling. It must exercise deterministic positive and refusal paths through the real transactions,
produce sealed machine-readable journals, and run from the frozen candidate. Static unit tests do
not replace it; the harness does not replace native human appearance or Steam-install evidence.

*Reverses if:* the author explicitly changes the public version or release channel. It does not
reverse merely because additional post-Alpha ideas are discovered.

## Building benefits are physically embodied — ruled 2026-09-01

**A catalogue declaration is a cap and contract, never the source of a building benefit.** A
house shelters people because it contains usable beds; a workshop supports craft because its
actual benches or machines work; learning, order, spirit, luxury, charge, storage, and other
benefits likewise come from present furniture, equipment, structure, contents, or technology.
Food and water retain their stronger existing physical flows and custody rather than becoming
passive provider auras.

**Providers attach to things; designations decide where they count.** The core contract is one
typed, bounded provider protocol plus one typed, bounded building-designation protocol. A
designation names an exact stable cell set, accepted benefit kinds, and per-kind caps. A valid
provider physically inside that designation contributes up to its declared amount only when its
own spatial and operational conditions hold. The effective result is the lesser of valid physical
supply and the designation's cap. Extra furniture remains ordinary usable furniture but grants no
extra settlement lift. A provider belongs to at most one designation; overlap or uncertain custody
fails closed and is explained by inspection rather than counted twice.

**The whole designated place may count, subject to what the object actually needs.** Beds require
covered habitable building cells. Outdoor benches, markets, shrines, or yard apparatus may opt into
appropriate yard cells. Containers are judged by real contents and custody. Machines may require
power, condition, access, staffing, or their existing native operation. Merely sharing a plot does
not turn a broken, packed, carried, unreachable, unpowered, or wrongly sited object into civic
capacity.

**Authored and player-made buildings use the same law.** Shipped plot architecture supplies one
designation source from its frozen lot/claim/footprint authority. A TAF-adopted room must persist
the exact measured cells the founder approved; a centred catalogue rectangle is not a room.
Exact Hearthpyre 2.2.3 supplies the same normalized source from its stable `Home` identity and
enumerated cells through the optional bridge. Furniture may be placed by the authored stamper, the
player, Hearthpyre, or another compatible mod; provenance does not grant or deny the benefit.
Blueprint-name allowlists and one-off building arithmetic are not authorities.

**Foreign extension faults are row-local.** A known malformed or ambiguous exact footprint becomes
refused evidence on only those cells; healthy siblings and other providers survive. Provider-wide,
registration, unknown-cell, and deterministic budget faults are bounded and quarantined rather
than becoming global civic state. An already bound row pauses only when its own provider/evidence
fails or known foreign ground now intersects it; an unrelated extension fault cannot erase it.
Roster count above 512, or the sum of otherwise row-bounded cell-array counts above 65,536, is a
provider-wide protocol fault checked before exact-cell enumeration; null, empty, and individually
over-row cell arrays remain row-local and do not erase healthy siblings.
For Hearthpyre specifically, sector identity/list-snapshot/roster churn remains provider-wide.
The snapshot unions `Sector.Homes` with every globally indexed Home backlinking that sector, so an
unlisted backlink is refused evidence rather than an omission. One Home's global-key, backlink,
duplicate-custody, cell, or overlap defect does not erase unrelated Homes. Exact cells are refused
when they can be proved, and no cell is invented when membership cannot be proved. Both Hearth
proof passes share one 1,048,576-entry registry/backlink/cell work budget; exhaustion faults the
provider atomically, preventing individually bounded reverse registries from multiplying into an
unbounded nested scan.

Loaded ground is evaluated from live physical state and exposes accepted, active, capped, missing,
and ineligible providers to the player. Unloaded ground keeps only its bounded dated last-observed
result; this feature never loads a remote zone or simulates an unwitnessed object. Moving,
destroying, breaking, emptying, or disabling a provider changes the next live observation without
silently rewriting its building designation.

*Reverses if:* playtesting changes a named cap, spatial scope, or operational condition. The
physical-source, exact-designation, single-assignment, inspectability, and shared-adapter laws do
not reverse merely to simplify authored maps or preserve an old catalogue total.
