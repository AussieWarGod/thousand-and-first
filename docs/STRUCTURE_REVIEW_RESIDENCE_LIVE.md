# Live residence integration structural review

Automated delta review against `60f865e4f30770438c23f71e59be4848be31d42b`: all 3,119 production paths compared,
3,105 byte-identical, 11 changed, 3 added and none removed. Census: 441,537 physical lines, 1,457 direct XRL imports,
zero files at or above 300 lines. Inventory 447c50cfc41e98d6132695cbcf4c2a01b975093cd7f4eed99c12bdd52ec937f5-256: `447c50cfc41e98d6132695cbcf4c2a01b975093cd7f4eed99c12bdd52ec937f5`.
Full comparison and census: `home-map/residence-live-20260915-1/`.
The preceding [residence storage review](STRUCTURE_REVIEW_RESIDENCE.md) retains its model scope.

| Responsibility | Reviewed boundary |
| --- | --- |
| Resident engine adapter | Captures actual household facts, preserves map/plot through visiting check-in, reconciles roots only on observed source ground. Publishes existing city-row authority before projecting body properties. No second population ledger or forced map loading. |
| Household occupancy | Combines on-roll durable reservations with actual local bodies. Absent owners keep capacity and compatibility facts; unknown absent traits conservatively conflict. Exact reads do not enroll, publish or load remote maps. |
| Arrival observation | Present occupants preserve the historical v1 hash. Absent reservations have a separate domain and include exact resident identity and observed traits. Housing choice uses the same reservation collection. |
| Home consumers | Local room benefits and cohabitation use map-qualified identity. Reports identify a home in another district. Rehouse/departure use the shared authority-first writer; interrupted projection can retry. |
| Row clone | Optional replacement root ID preserves every other row field. No further saved-column or codec shape change. |

Source validation: 15,048 main / 5,934 portable cases, zero skips. Engine compile evidence is
recorded separately. Native replay remains pending. Exact usable sleeping-place allocation,
ordinary home/work travel, cold recovery and absent-owner condemnation chronology remain #230.
No all-building or Beta acceptance follows from this review; #229/#251 retain the full requirement.
