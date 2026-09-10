# Developer travel pair

`TAF_PERSONA_SEED='#123' Tools/run-personas.sh beta-travel-present beta-travel-away` prepares
separate sealed profiles with the same explicit seed for comparison.
The recipes found a first city at START=8.22@40,12, request a 1200-turn warm-up
for ordinary daily reconciliation, then wait 1200 turns, return, request a 39-turn drain,
yield one render frame, and check. Departure requires an initialized home row; no direct
check-in or clock stamp substitutes for the warm-up. Warm-up completion may be observed later
at an action opportunity; the actual elapsed count stays in the journal.

The away leg uses ordinary westward `GameObject.Move`, one bounded step per action opportunity,
until a different, nonclaimed surface parasang is reached. It returns east along the same route.
Blocked movement, unexpected coordinates, more than 240 steps each way, changed game/player/book
identity, or a home still cached at the return boundary explicitly refuse. No clearing, teleport,
zone eviction, forced loading, or direct turn-clock write substitutes for those steps.

`advance` counts game turns, not settlement passes. Travel overhead is reported separately;
the pair does not claim equal total elapsed time. Use
`python3 Tools/compare-travel-personas.py PRESENT-JOURNAL AWAY-JOURNAL` to compare matching
seed/home witnesses. This checks journal structure, not provenance of arbitrary supplied files.
The normal runner still owns strict log/MODERROR checks; no log exemptions are added.

The runtime observes shared reification spend (24 thirds/four heavy per turn), physical remaining
demand receipts (936 thirds maximum), model/semantic clock monotonicity, growth mirror equality,
and schedule deadline/ordinal continuity. A post-return physical-demand receipt must reach zero
within 39 turns after first home entry and remain settled at the final check. Three model resource
flags alone never prove physical drainage. Missing demand evidence refuses, even in a quiet camp.
The script requests a 39-turn drain wait; an action opportunity or real render yield may observe
its completion later (for example, 40 elapsed turns). That later observation does not extend the
physical deadline: the retained zero-demand receipt must still be within 39 turns of home entry.

Limits: this small first-city fixture is not a 252-container stress fixture. Schedule continuity
does not prove all economic pause effects; `pause-effects-proved=false` is mandatory. The route
is not general pathfinding. Native execution is required to establish whether engine cache
lifetime and the chosen terrain can express this first recipe. No native success, full Beta
acceptance, ordinary-game acceptance, save/reload continuation, or release approval is implied.
