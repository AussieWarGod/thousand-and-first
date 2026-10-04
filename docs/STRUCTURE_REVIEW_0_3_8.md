# Alpha 0.3.8 hotfix production delta review

Claude automated structural review (0.3.8 hotfix builder), 2026-10-04, under the standing author
authorization for automated structural review. This establishes structural continuity only; it
is not native, delivery or release acceptance.

Inventory `5a5462a6889b1843bc13a7930ae28cd5b0a8838fd1563e995222534575e38ed8`: 3,108 production
sources, 440,963 physical lines, 1,450 direct XRL imports, zero files at or above 300 lines.
Every staged production C# path and byte was compared with the reviewed 0.3.7 inventory
`91973648605848aa1939d38f366ed6e578cd4cc07ef542bd6f6a03b76575c935` (public 0.3.7 at
`de8317db`): 3,103 are byte-identical, two are added and three changed, none removed. Comparison
record SHA-256: `1c55b14f07c9a58d112852affb860bd88ef05be4e3e80edd23a930177adb26cf`.

| Changed responsibility                                 | Boundary review                                                                                                                                                                                                                                                                                                                                                                          |
| ------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Experience/KingdomLifecycleWireCodec.LifecycleWrite.cs | `WriteLifecycle` chooses the frame before any byte is written: a book admitted by `DormantLifecycleWireExact` goes to the existing growth-free lifecycle v5 frame, every other book to the current frame with the unchanged strict growth gate. `WriteLifecycleCore`, its guards and every reader are unchanged. The doc comment records the permanent dependency on the v5 read branch. |
| Experience/KingdomLifecycleDormantWireRules.cs (new)   | Pure rules partial of `KingdomLifecycleRules`: the dormant admission predicate and a constructor-default raid ledger check covering every ledger field. It reads only and composes the existing pristine and canonical-quarantine predicates. Split out because the common validation partial is at the line cap.                                                                        |
| Experience/KingdomLifecycleWireCodec.GrowthEnvelope.cs | Both refusal messages keep their exact existing text as a prefix and append the failing check. No verdict, branch or catch changes.                                                                                                                                                                                                                                                      |
| Experience/KingdomGrowthEnvelopeRefusalRules.cs (new)  | Pure diagnostic partial, reached only after `GrowthEnvelopeWritable` refused. It mirrors the gate order by calling the existing predicates and returns one ASCII token. Split out because the migration rules partial is at the line cap.                                                                                                                                                |
| Core/KingdomReleaseInfo.cs                             | Runtime receipt identity follows manifest 0.3.8; no state or branching change.                                                                                                                                                                                                                                                                                                           |

No serialized field, schema, save format or protocol is added or changed: the writer emits a
historical frame every shipped reader since v0.3.0 already parses, and founded books keep their
exact bytes. The four lifecycle files are byte-identical to dev PR #277. The harness verb,
cold-load route and personas are developer harness, outside the staged package. No structural
exception is needed. Native unfounded save, unfounded cold load and founded regressions remain
owed and are tracked in docs/STATUS.md.
