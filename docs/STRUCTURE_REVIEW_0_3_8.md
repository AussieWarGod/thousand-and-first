# Alpha 0.3.8 hotfix production delta review

Claude automated structural review (0.3.8 hotfix builder), 2026-10-04, under the standing author
authorization for automated structural review. This establishes structural continuity only; it
is not native, delivery or release acceptance.

Inventory `cdb361018c945f31416ea81252d453ca0043f17c1bb726408e740deae03cdc16`: 3,108 production
sources, 440,963 physical lines, 1,450 direct XRL imports, zero files at or above 300 lines.
Every staged production C# path and byte was compared with the reviewed 0.3.7 inventory
`91973648605848aa1939d38f366ed6e578cd4cc07ef542bd6f6a03b76575c935` (public 0.3.7 at
`de8317db`): 3,102 are byte-identical, two are added and four changed, none removed. Comparison
record SHA-256: `0710f000bea54bd3699260c6101b9fc66e5a32c65afb200a41b0122ca2e6d80e`.

| Changed responsibility                                 | Boundary review                                                                                                                                                                                                                                                                                                                                                                          |
| ------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Experience/KingdomLifecycleWireCodec.LifecycleWrite.cs | `WriteLifecycle` chooses the frame before any byte is written: a book admitted by `DormantLifecycleWireExact` goes to the existing growth-free lifecycle v5 frame, every other book to the current frame with the unchanged strict growth gate. `WriteLifecycleCore`, its guards and every reader are unchanged. The doc comment records the permanent dependency on the v5 read branch. |
| Experience/KingdomLifecycleDormantWireRules.cs (new)   | Pure rules partial of `KingdomLifecycleRules`: the dormant admission predicate and a constructor-default raid ledger check covering every ledger field. It reads only and composes the existing pristine and canonical-quarantine predicates. Split out because the common validation partial is at the line cap.                                                                        |
| Experience/KingdomLifecycleWireCodec.GrowthEnvelope.cs | Both refusal messages keep their exact existing text as a prefix and append the failing check. No verdict, branch or catch changes.                                                                                                                                                                                                                                                      |
| Experience/KingdomGrowthEnvelopeRefusalRules.cs (new)  | Pure diagnostic partial, reached only after `GrowthEnvelopeWritable` refused. It mirrors the gate order by calling the existing predicates and returns one ASCII token. Split out because the migration rules partial is at the line cap.                                                                                                                                                |
| Core/KingdomReleaseInfo.cs                             | Runtime receipt identity follows manifest 0.3.8; no state or branching change.                                                                                                                                                                                                                                                                                                           |
| Polity/KingdomPolityIncidentState.cs                   | The `KingdomPolityOptions.FutureCauseFloorTick` initializer is now `long.MaxValue`, the value normalization already installed, so a fresh polity ledger is canonical before its first save. Every decoder still reads the options from the wire. No field, codec or validator change; byte-identical to dev (PR #250).                                                                   |

No serialized field, schema, save format or protocol is added or changed: the writer emits a
historical frame every shipped reader since v0.3.0 already parses, and founded books keep their
exact bytes. The polity initializer changes only the in-memory default of an existing field, and
every decoder still reads the options from the wire, so saved ledgers decode unchanged. The four
lifecycle files are byte-identical to dev PR #277 and the polity file to dev. The harness verb,
cold-load route and personas are developer harness, outside the staged package. No structural
exception is needed. Native unfounded save, unfounded cold load and founded regressions remain
owed and are tracked in docs/STATUS.md.
