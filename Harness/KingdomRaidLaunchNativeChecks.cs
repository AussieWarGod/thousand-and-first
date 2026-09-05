using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Native raid-launch assertions. Every Check below runs AFTER the engine dispatch
	/// that produced the evidence has returned: the probe records inside the factory callback and
	/// never throws there, because the factory swallows callback exceptions (design rev2 sec 4,
	/// decompiled GameObjectFactory.cs:1163-1169). No teardown beyond disarming the probe.</summary>
	internal static class KingdomRaidLaunchNativeChecks
	{
		internal const string CaseA = "actual-launch-mint-order";
		internal const string CaseB1 = "second-mint-replacement-quarantine";
		/// <summary>1-based creation index the B1 probe substitutes at.</summary>
		internal const int SubstituteAtSequence = 2;
		/// <summary>A harmless, deliberately UNPROBED, different blueprint, so the substitution
		/// fires no nested observation. Create precedent:
		/// Harness/KingdomQuickstartBootstrap.NativeCreators.cs:113.</summary>
		internal const string SubstituteBlueprint = "Chest";
		/// <summary>The exact fixed fault the mint loop quarantines with when the created body's
		/// blueprint is not the frozen one (Raids/KingdomRaids.09.*.cs:76-82).</summary>
		internal const string QuarantineFault =
			"raid projection body or frozen entry cell could not be recreated";

		internal static string Run(XRLGame Game, Zone Zone, bool Quarantine, out bool Ok)
		{
			int passed = 0;
			long now = Game.TimeTicks;
			string current = Quarantine ? CaseB1 : CaseA;
			StringBuilder results = new StringBuilder();
			KingdomRaidLaunchNativeFixture fixture = null;
			KingdomLifecycleOperation op = null;
			r_TAF_RaidMintObservation[] observations = null;
			bool ok = false;
			string report = null;
			try
			{
				// Installed-eligibility gate FIRST: one refused band drops its whole profile, so
				// every shipped key must resolve before any Snapjaws-only assertion can hide it.
				int loaded;
				string missing;
				results.Append('\n').Append(
					KingdomRaidLaunchNativeProvider.ProfilesLoaded(out loaded, out missing));
				Check(loaded == KingdomRaidLaunchNativeProvider.ShippedFactions.Length,
					"shipped raid profiles refused at load, missing: " + missing);
				string failure;
				Check(KingdomRaidLaunchNativeFixture.TryCreate(Zone, out fixture, out failure), failure);
				KingdomLifecycleBook book = fixture.System.LifecycleBook;
				Check(r_TAF_RaidMintProbe.Armed && ReferenceEquals(r_TAF_RaidMintProbe.Book, book),
					"the probe is not armed on the founded lifecycle book");
				Check(r_TAF_RaidMintProbe.Snapshot().Length == 0,
					"the probe observed a creation before the real activation call");
				// ---- the only engine dispatch in this verb; nothing below asserts inside it ----
				fixture.Activate();
				r_TAF_RaidMintObservation[] first = r_TAF_RaidMintProbe.Snapshot();
				if (Quarantine) fixture.Activate();
				r_TAF_RaidMintProbe.Armed = false;
				observations = r_TAF_RaidMintProbe.Snapshot();
				// ---- assertions, outside dispatch ----
				Check(observations.Length >= 1, "the real activation minted no observed raider");
				Check(r_TAF_RaidMintProbe.Retained == observations.Length,
					"retained " + r_TAF_RaidMintProbe.Retained + " ledger " + observations.Length + " diverged");
				op = observations[0].Raid;
				Check(op != null && op.Action == KingdomLifecycleAction.RaidAttack
					&& op.Projections.Count >= 2, "no real raid attack with a frozen party was opened");
				Check(op.PartySize >= 2, "the launcher resolved fewer than two actual raiders");
				Check(ReferenceEquals(book.Raid, op),
					"the observed operation is not the book's own raid authority");
				Check(!Quarantine || observations.Length == first.Length,
					"a second real activation minted again after quarantine");
				if (Quarantine) KingdomRaidLaunchNativeQuarantineChecks.Verify(fixture, op, observations);
				else VerifyHappyPath(fixture, op, observations);
				passed++;
				results.Append('\n').Append(current).Append("=PASS");
			}
			catch (Exception error)
			{
				results.Append('\n').Append(current).Append("=FAIL ")
					.Append(KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message));
			}
			finally
			{
				// Disarm and NOTHING else. The probe ledger, its Book reference, every body,
				// substitute and abandoned original outlive this run by design; only the verb's
				// entry may reset the probe, and only over a provably empty ledger.
				r_TAF_RaidMintProbe.Armed = false;
				// Evidence and the report must survive a fault in either one: the record is the
				// point of the run even when the verdict itself cannot be computed cleanly.
				try { Evidence(results, fixture, op, observations); }
				catch (Exception error)
				{
					results.Append("\nreport-fault=").Append(KingdomScenarioRules.Bounded(
						error.GetType().Name + ": " + error.Message));
				}
				ok = passed == KingdomRaidLaunchNativeProvider.ExpectedCases;
				try { report = Report(Game, now, Quarantine, passed, ok, fixture); }
				catch (Exception error)
				{
					report = "report-fault=" + KingdomScenarioRules.Bounded(
						error.GetType().Name + ": " + error.Message);
				}
			}
			Ok = ok;
			return report + results;
		}

		internal static void Check(bool Condition, string Detail)
		{
			if (!Condition) throw new InvalidOperationException(Detail ?? "native raid launch refused");
		}

		/// <summary>One mint's contract: the probe saw the same published operation, in
		/// ProjectionIntent, with every prior projection already Proved, and the blueprint the
		/// factory was asked for is exactly the frozen one for this ordinal.</summary>
		internal static void VerifyMint(r_TAF_RaidMintObservation O, KingdomLifecycleOperation Op,
			int K)
		{
			string at = " at mint " + K;
			Check(O.Fault == null, "the probe recorded a callback fault" + at + ": " + O.Fault);
			Check(O.Sequence == K + 1, "observation sequence is not contiguous" + at);
			Check(ReferenceEquals(O.Raid, Op)
				&& string.Equals(O.OperationId, Op.Id, StringComparison.Ordinal),
				"the observed book.Raid is not the published operation" + at);
			Check(O.Phase == KingdomLifecyclePhase.ProjectionIntent,
				"the operation was not in ProjectionIntent" + at);
			Check(O.MintIndex == K && O.ProvedBefore == K,
				"the earlier projections were not already Proved" + at);
			Check(O.MintState == KingdomLifecyclePhysicalState.Prepared,
				"the minting projection was not Prepared" + at);
			Check(KingdomRaidLaunchNativeFixture.Probed(O.RequestedBlueprint),
				"an unprobed blueprint was requested" + at + ": " + (O.RequestedBlueprint ?? "(null)"));
			Check(string.Equals(O.MintBlueprint, O.RequestedBlueprint, StringComparison.Ordinal),
				"the requested blueprint differs from the frozen projection blueprint" + at);
			Check(string.Equals(O.MintObjectId,
				KingdomLifecycleRules.ChildId(Op.Id, "raider", K), StringComparison.Ordinal),
				"the frozen projection identity is not this ordinal's raider child id" + at);
			VerifyPriors(O, Op, K);
		}

		/// <summary>Every prior actor as the callback itself SAW it: the placement is read off each
		/// retained body's own zone, cell and marker part at that instant, never inferred from the
		/// projection's Proved bookkeeping, which is the model's claim rather than the world.</summary>
		private static void VerifyPriors(r_TAF_RaidMintObservation O, KingdomLifecycleOperation Op,
			int K)
		{
			Check(O.Priors != null && O.Priors.Length == K,
				"the callback did not snapshot one prior actor per earlier mint at mint " + K);
			for (int j = 0; j < K; j++)
			{
				r_TAF_RaidMintPriorActor prior = O.Priors[j];
				string at = " for prior raider " + j + " at mint " + K;
				Check(prior != null && prior.Sequence == j + 1 && prior.Body != null
					&& GameObject.Validate(prior.Body),
					"the prior raider is not a retained live body" + at);
				Check(string.Equals(prior.ObjectId, Op.Projections[j].ObjectId,
					StringComparison.Ordinal),
					"the prior raider did not carry its frozen identity at callback time" + at
						+ ": " + (prior.ObjectId ?? "(null)"));
				Check(string.Equals(prior.ZoneId, Op.ZoneId, StringComparison.Ordinal)
					&& prior.CellX.HasValue && prior.CellY.HasValue,
					"the prior raider was not standing at a cell in the target zone" + at);
				Check(prior.MarkerParts == 1 && string.Equals(prior.MarkerOperationId, Op.Id,
					StringComparison.Ordinal),
					"the prior raider did not carry exactly one marker for this operation" + at);
			}
		}

		/// <summary>One proved raider: unique identity and marker in the zone, live, at a cell, with
		/// exactly one objective part bound to this operation (Raids/KingdomRaids.09.*.cs:111-125).
		/// </summary>
		internal static void VerifyBody(KingdomRaidLaunchNativeFixture Fixture,
			KingdomLifecycleOperation Op, KingdomLifecycleProjection Projection, int Index)
		{
			int ids, markers;
			GameObject body = r_TAF_RaidMintSnapshots.Census(Fixture.Zone, Projection,
				out ids, out markers);
			Check(ids == 1 && markers == 1,
				"raider " + Index + " is not exactly one identity and one marker in the zone");
			Check(GameObject.Validate(body) && body.IsAlive && body.CurrentCell != null
				&& ReferenceEquals(body.CurrentZone, Fixture.Zone),
				"raider " + Index + " is not a live body standing in the zone");
			Check(string.Equals(body.Blueprint, Projection.Blueprint, StringComparison.Ordinal)
				&& string.Equals(body.IDIfAssigned, Projection.ObjectId, StringComparison.Ordinal),
				"raider " + Index + " lost its frozen blueprint or identity");
			Check(body.GetIntProperty("KingdomRaider") == 1
				&& string.Equals(body.GetStringProperty(KingdomRaids.ProjectionMarkerProperty),
					Projection.Marker, StringComparison.Ordinal),
				"raider " + Index + " lost its projection marker property");
			r_KingdomRaiderObjective objective = body.GetPart<r_KingdomRaiderObjective>();
			Check(objective != null && r_TAF_RaidMintSnapshots.MarkerParts(body) == 1
				&& string.Equals(objective.OperationId, Op.Id, StringComparison.Ordinal),
				"raider " + Index + " does not carry exactly one objective for this operation");
		}

		private static void VerifyHappyPath(KingdomRaidLaunchNativeFixture Fixture,
			KingdomLifecycleOperation Op, r_TAF_RaidMintObservation[] Observations)
		{
			Check(Observations.Length == Op.PartySize,
				"the probe did not observe exactly one creation per actual raider");
			for (int k = 0; k < Observations.Length; k++)
			{
				VerifyMint(Observations[k], Op, k);
				Check(Observations[k].Substitute == null,
					"the happy path substituted a body at mint " + k);
			}
			Check(Op.Phase != KingdomLifecyclePhase.Quarantined && Op.Fault == null,
				"a clean launch quarantined its raid authority: " + (Op.Fault ?? "(none)"));
			Check(Op.Projections.Count == Op.PartySize && Op.Spawned == Op.PartySize,
				"the settled operation did not spawn exactly its frozen party");
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Op.Projections.Count; i++)
			{
				KingdomLifecycleProjection projection = Op.Projections[i];
				Check(projection.State == KingdomLifecyclePhysicalState.Proved,
					"projection " + i + " is not Proved after a clean launch");
				Check(ids.Add(projection.ObjectId), "two projections claim one body identity");
				VerifyBody(Fixture, Op, projection, i);
			}
		}

		/// <summary>Per-mint and per-placement evidence rows. Emitted for a failed case too: the
		/// point of the run is the record, not only the verdict.</summary>
		private static void Evidence(StringBuilder Results, KingdomRaidLaunchNativeFixture Fixture,
			KingdomLifecycleOperation Op, r_TAF_RaidMintObservation[] Observations)
		{
			Results.Append("\nprobe entries=").Append(r_TAF_RaidMintProbe.Retained)
				.Append(" ledger=").Append(Observations == null ? -1 : Observations.Length);
			if (Observations != null)
				for (int k = 0; k < Observations.Length; k++)
				{
					r_TAF_RaidMintObservation o = Observations[k];
					Results.Append("\nmint#").Append(o.Sequence)
						.Append(" bp=").Append(o.RequestedBlueprint ?? "-")
						.Append(" frozen=").Append(o.MintBlueprint ?? "-")
						.Append(" op=").Append(o.OperationId ?? "-")
						.Append(" phase=").Append(o.Phase)
						.Append(" proj=").Append(o.ProjectionStates)
						.Append(" oid=").Append(o.MintObjectId ?? "-")
						.Append(" child=").Append(Op == null ? "-"
							: KingdomLifecycleRules.ChildId(Op.Id, "raider", k))
						.Append(" sub=").Append(o.SubstituteBlueprint ?? "-")
						.Append(" subid=").Append(o.Substitute == null ? "-"
							: (o.Substitute.IDIfAssigned ?? "(null)"))
						.Append(" priors=").Append(r_TAF_RaidMintSnapshots.Describe(o.Priors))
						.Append(" fault=").Append(o.Fault == null ? "-"
							: KingdomScenarioRules.Bounded(o.Fault));
				}
			if (Op == null) return;
			Results.Append("\nop=").Append(Op.Id).Append(" phase=").Append(Op.Phase)
				.Append(" spawned=").Append(Op.Spawned).Append('/').Append(Op.PartySize)
				.Append(" fault=").Append(Op.Fault ?? "-");
			if (Fixture == null) return;
			for (int i = 0; i < Op.Projections.Count; i++)
			{
				KingdomLifecycleProjection projection = Op.Projections[i];
				int ids, markers;
				GameObject body = r_TAF_RaidMintSnapshots.Census(Fixture.Zone, projection,
					out ids, out markers);
				Cell cell = body == null ? null : body.CurrentCell;
				Results.Append("\nplace#").Append(i).Append(" state=").Append(projection.State)
					.Append(" frozen=").Append(projection.X).Append(',').Append(projection.Y)
					.Append(" actual=").Append(cell == null ? "-" : cell.X + "," + cell.Y)
					.Append(" delta=").Append(cell == null ? "-"
						: (cell.X - projection.X) + "," + (cell.Y - projection.Y))
					.Append(" ids=").Append(ids).Append(" markers=").Append(markers);
			}
		}

		/// <summary>Retention state is read fresh from the book here, never from a local set only
		/// once every assertion upstream has already passed, so a failed run still reports the
		/// true retention state (Fixture.cs ~14-22,112-125,190-191 are the fixture literals the
		/// synthetic-seed clause below is derived from, not duplicated as a second copy).</summary>
		private static string Report(XRLGame Game, long Now, bool Quarantine, int Passed, bool Ok,
			KingdomRaidLaunchNativeFixture Fixture)
		{
			KingdomLifecycleOperation raid = Fixture == null || Fixture.System == null
				|| Fixture.System.LifecycleBook == null ? null : Fixture.System.LifecycleBook.Raid;
			return "native-raid-launch cases=" + KingdomRaidLaunchNativeProvider.ExpectedCases
				+ " passed=" + Passed + " failed=" + (Ok ? 0 : 1)
				+ "; case=" + (Quarantine ? "b1-different-blueprint-replacement" : "a-happy-path")
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested"
				+ "; synthetic-seed=store:" + KingdomRaidLaunchNativeFixture.StoreBlueprint + "@"
				+ KingdomRaidLaunchNativeFixture.StoredDrams + "dr+KingdomStores=1,incident:"
				+ KingdomRaidIncidentState.FightCommitted + "/" + KingdomRaidResponse.Fight
				+ "@stage=" + KingdomRaidLaunchNativeFixture.FrozenStage + ",party="
				+ KingdomRaidLaunchNativeFixture.PartySize
				+ "; b2-swallowed-throw=unimplemented; same-blueprint-custody=open"
				+ "; raid-open=" + (raid == null ? "false" : "true:" + raid.Phase)
				+ "; scene-preserved=true; substitute-custody=unproved"
				+ "; world-clock=" + (Game.TimeTicks == Now ? "unchanged" : "changed");
		}
	}
}
