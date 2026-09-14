# Higher-heart cold-load acceptance

Work for #159, #160, #212–#216 and draft PR #207. This remains part of the
Beta objective; it does not replace same-city multi-map acceptance in #211 or the
land-use direction in [CITY-GROWTH-BALANCE.md](CITY-GROWTH-BALANCE.md).

## Current scope

The existing camp snapshot and observer pin rung two, its fire, its paid tent job,
21 brush units and one extra timber for a later job. That evidence cannot establish
the identity, physical support or persistence of a later heart.

`KingdomCampHeartChainSnapshot` and its codec define a separate bounded witness for
completed rungs three and four. A separate `camp-heart-chain-save` persona now runs the
whole paid chain and saves after rung-four next-day recovery. Its observer captures the
real completed heart, physical support, original anchors and paid receipts. Continue
dispatch and the loaded next-job continuation are implemented but await engine compilation
and native execution; do not launch an acceptance run until both source and consumer are
validated and sealed together. No new native save
has been executed. Codec tests validate the evidence format, not persistence.
Rung five and interrupted paid handovers still require their own coverage; refusing
them in this test record is not a gameplay restriction or completion of those tasks.

The record binds game, realm, city and local map; the heart, original basin, original
store and retained unpaid track by exact identity and coordinates; the displaced
resident and paid heart job; population, physical water/food and the game clock.
Separate digests bind job receipts, the resident census, support works and original
store custody. No population limit is imposed by the codec.

The save route retains four canonical fact files (`camp-heart-chain-*-facts.txt`) beside
the external snapshot. Facts have length-prefixed fields, explicit nulls, sorted unique
row identities, strict UTF-8 and bounded aggregate size. They include complete encoded
per-city paid jobs, exact resident rows and physical bodies, city work rows, verified
authored roots and their layouts, the disclosed legacy producers, all four authenticated
stakes, water mixtures and larder/stockpile contents. Anonymous food stacks retain their
observed inventory position and raw count; observation never assigns them an identity.

Before the first higher construction wait, the save variant requests an ordinary fire
quote outside the future court footprint, checking its two-drams/one-timber bill and no
change to paid jobs, original store custody or civic water. It quotes again before saving,
then explicitly adds one next-job timber to the original store. The whole snapshot must
match again after actual Primary serialization before the save receipt is published.
Completion reads share one local survey and prove disposal; no speedup is claimed yet.

## Required native integration

1. Implemented, unexecuted: an exact save variant of the paid chain. Its save verb follows successful
   ordinary completion, recovery and custody checks. Retain the existing unsaved
   chain and rung-two save regression. Validate the persona before game preparation.
2. Implemented, unexecuted: capture the actual completed heart and its physical objects, without repairing,
   relocating, enrolling or minting anything during observation. Define canonical,
   bounded digests for per-city paid receipts, resident rows and bodies, and physical
   support works. Include the original survey stakes. Retain the underlying rows in
   evidence so a digest mismatch can be diagnosed.
3. Implemented, unexecuted: preflight the next ordinary paid job before the long save run. Any synthetic
   material supplied for that job must be disclosed and included in the saved custody
   census. Do not replay the 50-resident setup or backdate any completion after loading.
4. Source route implemented, unexecuted: write the witness to durable game state and external snapshot, save the actual
   primary, and retain exact save/metadata/cache hashes. Verify clock and custody
   across serialization. The host must stop the owned source game before importing
   the sealed primary into a fresh, separately sealed profile.
5. Implemented, unexecuted: dispatch this prefix explicitly through the existing Continue barrier and raw-reader
   witness. Unknown/malformed higher-heart records must refuse, with no fallback to a
   new game, rung-two observer or alternate save. Reconstruct and compare the entire
   witness before `AfterGameLoaded` callbacks. Never populate missing data from it.
6. Implemented, unexecuted: after normal activation, verify all physical layouts, founding recovery, exact paid
   receipts, resident identity/citizenship, support and original stock. Re-arm input
   isolation for this dedicated loaded game; its current owner flag is nonserialized.
   Preserve ordinary input in other games and restore popup ownership on every exit.
7. Implemented, unexecuted: perform the next paid action through its ordinary quote, commission and settlement
   turns. Prove one debit, exact completion, preserved prior jobs and original custody,
   support over subsequent days, and no replay of the source script or setup.
8. Host format/import/journal/fact checks implemented and tested. Integrate the existing
   native driver and archive for the optional save persona, retaining preactivation,
   activation, next-action and completion witnesses, strict logs, full source/profile
   bindings and exact owned shutdown. Tests reject absent, duplicate, refused and
   wrong-identity evidence. A parser pass or a
   `SCRIPT-COMPLETE` row alone is insufficient.

The host importer binds all four source fact files to the saved digests, copies their exact
bytes into the fresh Local profile, seals them and reproves source custody. The load observer
retains separate preactivation, activated and completed facts. Before any new action, all facts
must match exactly. After ordinary work, resident movement and resource use are allowed while
original resident identities, anchors, stakes, prior jobs and 21 original brush units remain
required; the saved timber must pay the new fire job. The oracle keeps actual wait overshoot.
Run `Tools/dev-check.sh tools 'camp_heart_chain_*test.py'` and the existing
`scenario_load_profile_test.py` selection for these host contracts.

Current compile prerequisite: the installed Hearthpyre updated to 2.2.4 and the canonical
gate refuses its exact 2.2.3 reference check (#217). Preserve the failed gate; restore authentic
pinned source or review new-version support before claiming engine compilation. Do not weaken
the version/hash check. This does not close any native persistence scenario below.

## Remaining scenarios

- Save/load at completed higher rungs, followed by another ordinary action.
- Save/load during a paid Outstanding handover after transient obstruction, preserving
  the same job, physical identities, custody and no second debit.
- Resident movement after payment; protected founder/foreign bodies; no safe destination;
  successful later recovery and preservation of any moved post.
- Rung-five arcology progression and persistence (#160/#144).
- Multi-claimed-map travel, unload/reconciliation, construction, support disruptions and
  real cold-load continuation on both maps (#211).

Keep [STATUS.md](STATUS.md) authoritative for actual executed evidence. Isolated preparation
must not change the checkout or profile of an already running native scenario.
