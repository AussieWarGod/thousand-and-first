# Kingdom Quickstart

Kingdom Quickstart is an optional new-game mode for testing or learning The Thousand and First. It does not alter Classic or Kingdom starts.

## Start a world

1. Before creating the world, set **The Thousand and First: place a benefit-free camp guide...** to Yes or No. The choice is read during world creation; changing it later does not add or remove the guide.
1. Before creating the world, set **The Thousand and First: found new Kingdom Quickstart worlds with four settlers already on the roll** to Yes or No. This choice is also read during world creation, and it is frozen there: turning it on later never gives an existing world founders, and turning it off later never takes a cohort away from one that has them.
2. Choose **New Game**, then **Kingdom Quickstart**.
3. Build a character normally.
4. Choose one reviewed holding camp:
   - **Reedwake** — salt marsh.
   - **Riftside** — desert canyon.
   - **Saltwake** — salt dunes.
5. Enter the world. After placement, the ordinary founding transaction creates the first heart and city identity, two tent-row lots are staked west of the supply column, and — with the founders option on — four founding citizens are placed on the approach.

Each successful camp physically contains 24 drams of fresh water in dedicated casks, 12 style-appropriate meals in a larder, and a chest containing 1 mud, 3 brush, and 4 timber. These are finite objects and items. They grant no hidden production and replenish only through ordinary settlement work.

The optional named camp guide explains this opening inventory and answers five fixed questions: how the place was founded and what ground is held, how anything gets built, water and the stores, whether anyone will come, and petitions and raiders. Every answer returns to the opening, and the guide says plainly that he is not on the roll and is not counted, that hands come off the roll, and that nobody new stays unless a roof stands with room left in it. He never states how many people the settlement currently counts, so the guide stays accurate however a camp is seeded. The guide is passive and immobile, carries no stock, awards no experience, provides no labour, staffing, support, or defence, and is not a citizen.

## The four founders

With the founders option on, a new world is founded with four settlers already standing on the approach east of the supply column: a hand, a drifter, a tinker and a physicker, named in one culture per camp. They are enrolled as citizens under a `Founding` reason that only this bootstrap can emit — the ordinary founding rite still enrols nobody — and they are on the roll at turn 1, so the settlement has labour before the first traveller is due.

- **They arrive together or not at all.** Their bodies and every piece of gear they carry are raised inside one custody scope, so a failure anywhere takes all four and all their gear back off the map and leaves the world exactly as it was; the next load retries from a clean slate.
- **Nothing is ever raised twice.** Each founder wears a reservation minted from the camp's own frozen ground, so a wake that still owes a cohort reads the ground before it makes anything: it adopts four that are already standing, raises four only where none are, and refuses — telling you, and never granting again — if it finds a party that is neither.
- **A refused cohort costs you nothing else.** Every store is granted and verified before a founder is raised, so a refusal leaves the casks, larder, chest and guide exactly as they are and says so once.
- **Each founder is counted into the settlement's origin tally exactly once, by adding one.** That tally is shared with ordinary arrivals, so five people who were already here plus four founders is nine. Each founder carries a durable, identity-bound record of whether this settlement has counted them, written before anything changes and completed only after both the label and the tally have been measured.
- **If an interruption lands between those two writes, the world says so and stops.** This is a deliberate safety policy, not a promise of fully automatic recovery: a pair of writes across a person and a shared total cannot always be told apart afterwards, and an unrelated arrival can leave the total reading exactly what a finished count would have left. Rather than guess, that founder's accounting is declared unresolved, in the open, once, forever. Note what that does *not* say: it does not say the tally is short. An interruption after the increment keeps it, and nothing left behind can tell that case from one before it — so the tally is left exactly as it stands and named unresolved rather than counted again. The founder is named, enrolled and on your roll, and the settlement stays playable.
- **They sleep rough at first.** A home is a bed only once its roof stands, and the two tent rows take about a day and a half. Keep the plots clear and check construction in the charter. Unhoused citizens receive a six-day warning window. If housing remains unavailable, voluntary departures stop at two citizens, leaving a workforce for recovery; this does not protect citizens from death.
- **They drink.** Four settlers at a camp drink 4 drams a day, so the 24 drams you start with last six days. Four is below the five living residents the water ladder wants, so the camp stage does not change on their account; the first arrival is what moves it.
- **Founders lengthen the base arrival interval.** The base interval is 3600 ticks plus 600 for every settler already living there: 6000 ticks rather than 3600 with four founders. This is not a promise that the first guest appears at an exact turn; the live cadence, district, and settlement policies govern the actual deadline.
- **A short party is said once.** If a named founder cannot be found when the irreversible half runs — only reachable through a crash mid-seed — the world says so once, keeps whoever did arrive, and never retries. It never quietly stands at one, two or three founders without telling you.

## What happens next

Settlers are not recruited by hand. They arrive on a clock, and roofs, water and hands decide whether they stay.

- The next traveller is due 3600 ticks after founding plus 600 for every settler already living there — three in-game days from an empty roll — and the settlement pass that publishes an arrival runs only while you are standing on claimed ground.
- When the correspondence opens, one message says so once. The durable half is the Charter header's **Next need** line, which keeps naming the unanswered guest across saves until you answer it.
- The route is: **Charter** → **Read the first guest's correspondence** → **Admit this person through Growth** → interact with the guest → **speak with the first guest** → **Welcome as citizen**. Deferring costs nothing and has no expiry.
- With no roof standing, **Next need** names the settler's tent (3 drams, 2 brush); the opening chest's 3 brush pays it. (The building catalogue writes that material as `canvas`; every player-facing surface calls it brush.) The two tent-row lots staked at founding rise on their own in about a day and a half, so that line stands only until the first row does. A hosted guest can remain without a roof or deadline, but hosting does not add a citizen or a worker. Finish a home before attempting citizenship.
- Turning the settlers-arrive Mods option off stops arrivals entirely; the next-need line then stops promising a settler.

Two tent rows are staked at founding, granted free: the stores above are unchanged. They are staked, not standing. Each lot is receiptless, so it keeps the same calendar clock the first heart uses and advances only at day boundaries you spend on this claimed ground, and only while the Mods option that enables settlement simulation is on — switch that off and the rows stop rising. The rule that prices a raising makes each row 1,700 ticks of work — 1,200 for the design and 500 for the walls it puts up — against 1,200 ticks to the day, so expect them standing about a day and a half in, not by nightfall, and expect no settler to build them. The two rows carry three beds each, six in all, so the first arrivals are not turned away for want of room. Until a roof stands nobody joins, so these two lots are the opening the mode did not have before. Nothing else is commissioned for you, and nothing commissioned rises while the population is zero. A staked lot is labelled from the shared building catalogue and reads on screen as `plot: staked tent-row (canvas, and a wall against the wind)`.

## Safety and compatibility

- A world whose receipt predates the tent-row shelter obligation keeps its original opening: no tent-row lots are retroactively staked for that receipt. Older worlds whose receipts already carry the shelter obligation retain it; they are not treated as pre-shelter worlds merely because they predate founders.
- A world founded before founders existed never gains them. Its receipt decodes with no cohort, re-encodes byte for byte as it was written, and reports itself finished on every load, zone activation and end-turn wake. A world founded with the founders option off is written the same way and is indistinguishable from it.
- The selected parasang is reserved before dynamic villages, lairs, or encounters claim it. Only the heart apron, supply approach, the two shelter lots, and the two-cell approach each lot's door opens onto are prepared; the rest of the wilderness remains intact. Nearby danger is still possible.
- Creatures, loose items, and liquid-bearing objects on required cells are relocated when safe. Stairs or an unsafe preparation result stop the bootstrap.
- Kingdom Quickstart never offers legacy realm inheritance in the same world. Use another supported mode to test inheritance.
- The bootstrap stores a checksummed, phase-by-phase receipt containing the exact physical object identities. Each cask, larder, chest, and included guide is completely prepared off-map, receives a profile/ground/role-bound reservation mark, and then enters the zone in one visible placement. A save or callback cut can therefore leave only no object or one exact, fully prepared object; load, zone-activation, and bounded end-turn wakes adopt that object before advancing the receipt and never place a second one.
- Once a grant phase is receipted, later recovery proves its object identity, dedicated role, position, and non-producing shape. It does not demand the opening water, meal, or material quantities again: using those finite provisions is normal play, not corruption and not authority to replenish them.
- A malformed receipt, mismatched profile or zone, unavailable founding authority, unsafe site, or failed physical measurement stops further grants. It does not synthesize replacement resources.
- The conversation on a guide is built once, when the world is created. A Quickstart world created before the guide learned its topics keeps the single opening line for good; nothing restamps an existing guide, and no verification demands the new shape.
- Do not treat changing the advisor option after world creation as a retroactive toggle. Quickstart
  registers one serializable player-system wake for load, zone activation, and bounded end-turn
  recovery. Its only mutable member is explicitly non-serialized; the checksummed game-state
  receipt above remains the sole durable authority. Quickstart adds no custom player part.

This alpha flow does not promise a combat-free start, staffed production, a finished tent on the first night, custom Quickstart art, or compatibility with saves created before the mode existed. If zoning or the authored-ground preflight refuses a shelter lot, the bootstrap stops there and says so rather than promising a roof it did not stake; because the lots are staked before the stores are granted, that refusal also means no casks, larder, materials chest or advisor for that world.
