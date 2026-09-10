# Economic travel developer fixture

Layered on the continuity-only travel seam. Run `beta-economic-present` and
`beta-economic-away` with the same explicit `TAF_PERSONA_SEED` via `Tools/run-personas.sh`.
Compare retained journals with `Tools/compare-travel-personas.py`. No native result is
claimed by authoring these recipes or by portable tests/compilation.

The exact sealed recipe observes a real local growth pause, then a real master pause.
Before either pause it requests a 1200-turn warm-up intended to allow ordinary daily reconciliation
to initialize the home row and growth health. Native execution must prove admission; the oracle still refuses unhealthy or uninitialized
authority; the fixture never writes health or schedule fields to manufacture admission.
After disabling local growth, it spends another 1200 turns so the ordinary daily semantic
pass can publish the local pause. A one-turn wait does not guarantee that reconciliation.
Master disable is requested only after the actual local pause receipt is present; no clock
or pause flag is stamped by the fixture. The following master observation still waits one turn.
The semantic-pass guard distinguishes a completed, published durable receipt from unfinished
work using the production receipt law. A retained `SemanticPassActive` flag alone is not proof
that execution is still inside a pass. Incomplete or unpublished receipts still refuse.
It restores growth configuration while master remains disabled, walks normally to an
unclaimed parasang, waits 1200 game turns, and walks home. Home must no longer be cached
before the return begins; there is no forced eviction or teleport fallback. The present
comparator stays home with the same pause/wait protocol. Master resumes at first home entry,
not after the additional walk back to the original cell.

An independent arithmetic oracle checks the actual master wake before and after publication:
`paused = priorPaused + resume - min(localPauseStart, masterDisabledAt)`.
For this healthy modern unleased fixture, arrival restarts at `resume + arrivalInterval`,
the epoch and resume token increment once, and daily work/clock deadlines become
`resume + TicksPerDay`. Effective work time, health/subsidence checkpoints and arrival
ordinal are preserved. Only this observed reset may rebase the continuity observer.
Open operations, earned arrival debt, unhealthy growth or unsupported authority refuse;
this is not a general proof of every leased-work recovery path.

The fixture physically populates 244 water vessels and eight larders with exactly one unit
of room each. It then explicitly seeds 244 units of controlled water model debt, plus eight
units of controlled LEGACY food model debt, while master is off. Existing debt is never
overwritten. Partial setup is retained and cannot retry. Objects are placed only in empty
cells away from the travel row; no existing object is cleared or moved.

**Water and food diverge from here, by production law.** Food-rate minting is retired
(`Simulation/City/KingdomCity.z05.Reify.cs:41-61`; `docs/STATUS.md`'s "Food and water are
separate physical flows" ruling): any owed food a zone carries into reification is retired to
zero — the pantry is preserved exactly as its currently-spendable physical level, never paid
onto a container — before container catch-up ever measures a food demand. Only the 244 water
units are real catch-up demand. So each of the **244 water containers** must receive its own
unit through normal physical catch-up, retain its ID, and finish full; the **eight larders'
seeded debt must retire to zero while their physical food is CONSERVED exactly** — same body
identities, same exact holder, same raw count, both before AND after the pause, proved by the
raw census seam (`KingdomMaterials.RawCensusCountOf`,
`Growth/KingdomMaterials.RawObservation.cs:48`) rather than the ordinary, dispatching
`GameObject.Count`. No food is minted, deleted, or relocated by this fixture or by the
reification it exercises.

This is a **synthetic physical-capacity fixture**, not proof that 220 civic works were
lawfully commissioned or that an ordinary player produced the debt. It exercises 244 water
container moves (732 thirds) — not 252 moves/756 thirds, because the eight larders never
generate catch-up demand — whose ideal minimum is 31 budgeted turns. The
production admission ceiling remains **at most 39 turns from first home entry**; the recipe
also requests a 39-turn advance after returning to the starting cell. Its completion may be
observed later at an action opportunity or render yield; this does not extend the physical-observation-based
physical deadline. It does not assert
that a measured drain must take exactly 39 turns. The 60-resident component of the
312-unit/936-third worst case is **not exercised**. Journal fields preserve those limits:
`synthetic-fixture=true`, `stress-residents=0`, `ordinary-acceptance=false`.
The physical observer refuses any population, resident rows, or citizen-marked bodies rather
than inferring zero body demand from a custody-only survey. This is a container-only proof.

Host checks reject incomplete/mixed economic witnesses, double-counted pause arithmetic,
wrong deadlines, partial container counts, repeated resume, missing/out-of-order setup,
and continuity-only evidence offered for an economic persona. Journals are developer
evidence, not an authentication mechanism or release/Beta authorization.
