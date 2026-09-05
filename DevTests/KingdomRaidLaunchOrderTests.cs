#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Pins the publish-before-mint reorder of LaunchRaid (05.cs): (i) source pins on
	/// the LaunchRaid-&gt;ResumeOpen slice (05.cs/09.cs need the XRL engine assembly and are not
	/// compiled into either test project, so these read the file as text, exactly like
	/// KingdomRaidRuntimeSourceTests.cs); (ii) a compiled production-rule protocol exercise that
	/// calls the same real, compiled rule methods LaunchRaid uses (PrepareOperation,
	/// RaidRuntimeAdapter.PrepareProjection/PrepareLeases, TryPublish, AdvancePhase) but does NOT
	/// execute LaunchRaid or ResumeOpen itself; runtime-adapter evidence against the live engine
	/// is root-owned native testing, not reproduced here.</summary>
	[TestFixture]
	public class KingdomRaidLaunchOrderTests
	{
		private const int PartySize = 3;

		private static string LaunchSlice()
		{
			string source = TestMain.ReadRepositoryText(
				Path.Combine("Raids", "KingdomRaids.05.AttackLaunchAndResume.cs"));
			int at = source.IndexOf("private static void LaunchRaid(", StringComparison.Ordinal);
			Assert.GreaterOrEqual(at, 0, "LaunchRaid");
			int until = source.IndexOf("private static void ResumeOpen(", at, StringComparison.Ordinal);
			Assert.Greater(until, at, "ResumeOpen");
			return source.Substring(at, until - at);
		}

		[Test]
		public void LaunchRaidMintsNoBodiesAndPlacesNothing()
		{
			string launch = LaunchSlice();
			StringAssert.DoesNotContain("GameObject.Create(", launch);
			StringAssert.DoesNotContain(".AddObject(", launch);
			StringAssert.DoesNotContain(".ID =", launch);
			StringAssert.DoesNotContain("PrepareRaiderBody(", launch);
			StringAssert.DoesNotContain("ActivateRaiderBody(", launch);
			StringAssert.DoesNotContain("BeginProjection(", launch);
			StringAssert.DoesNotContain("CommitProjection(", launch);
			StringAssert.DoesNotContain("GameObjectFactory", launch);
			StringAssert.DoesNotContain("CreateObject(", launch);
			StringAssert.Contains("PrepareProjection(", launch);
			StringAssert.Contains("TryPublish(", launch);
			StringAssert.Contains("AdvancePhase(", launch);
			StringAssert.Contains("ResumeOpen(system, zone)", launch);
		}

		[Test]
		public void PublicationOrderIsProjectThenPublishThenAdvanceThenResume()
		{
			string launch = LaunchSlice();
			int lastProjection = launch.LastIndexOf("PrepareProjection(", StringComparison.Ordinal);
			int publish = launch.IndexOf("TryPublish(", StringComparison.Ordinal);
			int advance = launch.IndexOf("AdvancePhase(", StringComparison.Ordinal);
			int resume = launch.IndexOf("ResumeOpen(system, zone)", StringComparison.Ordinal);
			Assert.Greater(lastProjection, 0, "PrepareProjection");
			Assert.Greater(publish, 0, "TryPublish");
			Assert.Greater(advance, 0, "AdvancePhase");
			Assert.Greater(resume, 0, "ResumeOpen(system, zone)");
			Assert.Greater(publish, lastProjection,
				"the last PrepareProjection must precede publication");
			Assert.Greater(advance, publish, "publication must precede the phase advance");
			Assert.Greater(resume, advance, "the phase advance must precede ResumeOpen");
		}

		[Test]
		public void ProjectionBlueprintComesFromTheFrozenRosterNotALiveBody()
		{
			string launch = LaunchSlice();
			Assert.IsFalse(launch.Contains("bodies["),
				"no live body array may survive in LaunchRaid");
			int blueprint = launch.IndexOf("KingdomRaidProfiles.Blueprint(profile, frozenStage",
				StringComparison.Ordinal);
			Assert.Greater(blueprint, 0, "Blueprint(profile, frozenStage");
			int loopStart = launch.LastIndexOf("for (int i = 0; i < party; i++)", blueprint,
				StringComparison.Ordinal);
			int nextProjection = launch.IndexOf("PrepareProjection(", blueprint,
				StringComparison.Ordinal);
			Assert.Greater(loopStart, 0, "the projection-preparation loop");
			Assert.Less(loopStart, nextProjection,
				"the projection-preparation loop must precede PrepareProjection(");
			Assert.Greater(nextProjection, blueprint,
				"the frozen roster blueprint must feed PrepareProjection, not a bodies[] read");
			StringAssert.Contains("op, i, objectId, blueprint,", launch,
				"PrepareProjection's LaunchRaid call must pass op, i, objectId, blueprint in this exact order");
		}

		// ---- (ii) compiled production-rule protocol exercise: the real, compiled rule calls
		// only; this does not execute LaunchRaid or ResumeOpen ----

		private static KingdomLifecycleBook Book(string settlementId)
		{
			KingdomLifecycleBook book = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(book, settlementId, false,
				null, new List<string>()));
			return book;
		}

		private static KingdomLifecycleOperation LedgerOp(KingdomRaidIncident incident,
			KingdomLifecycleAction action, long tick)
		{
			return new KingdomLifecycleOperation
			{
				Id = KingdomLifecycleRules.ChildId(incident.Id, "launch-order-" + (byte)action, 0),
				Lane = KingdomLifecycleLane.Raid, Action = action,
				SettlementId = incident.SettlementId, ZoneId = incident.TargetZoneId,
				ObjectId = incident.Id, Faction = incident.AttackerFactionId,
				CreatedTick = tick
			};
		}

		private static KingdomRaidLedger Apply(KingdomRaidLedger before,
			KingdomLifecycleOperation operation)
		{
			Assert.IsTrue(KingdomRaidIncidentRules.TryApply(before, operation,
				out KingdomRaidLedger after), operation.Action.ToString());
			return after;
		}

		/// <summary>Warning -&gt; delivery -&gt; acknowledgement -&gt; committed-to-fight, the same
		/// sequence KingdomLifecycleRulesTests.SeedRaidPlan uses to reach a publishable
		/// RaidAttack; only the direct FightCommitted stamp is a test-only shortcut (matching
		/// that existing precedent) for a state the real answer-flow also reaches.</summary>
		private static KingdomRaidIncident SeedFightCommittedIncident(KingdomLifecycleBook book)
		{
			KingdomLifecycleOperation warning = new KingdomLifecycleOperation
			{
				Lane = KingdomLifecycleLane.Raid, Action = KingdomLifecycleAction.RaidWarning,
				SettlementId = book.SettlementId, ZoneId = "zone-a", Origin = "launch-order-source",
				ObjectName = "authored act", Faction = "Snapjaws",
				DisplayFaction = "salt-road scouts", Creed = "explicit-slight",
				Detail = "specific authored evidence", ArrivalText = "zone-source",
				Target = 1, Count = 2, CreatedTick = 10L, DepartTick = 110L,
				PlunderRequested = 4, Kind = 24, Blueprint = "snapjaw-foragers"
			};
			warning.ObjectId = KingdomRaidIncidentRules.GrievanceId(warning.Origin);
			warning.ObjectMarker = KingdomRaidIncidentRules.IncidentId(warning.ObjectId);
			book.RaidLedger = Apply(book.RaidLedger, warning);
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(book.RaidLedger);

			KingdomLifecycleOperation delivery = LedgerOp(incident,
				KingdomLifecycleAction.RaidDeliverDemand, 11L);
			delivery.Origin = incident.DemandChannelId;
			delivery.Target = incident.ChannelRevision + 1;
			delivery.ObjectMarker = KingdomRaidIncidentRules.DemandObjectId(
				incident.DemandChannelId, delivery.Target);
			delivery.Count = 1;
			delivery.Blueprint = "r_KingdomSnapjawRaidDemand";
			book.RaidLedger = Apply(book.RaidLedger, delivery);
			incident = KingdomRaidIncidentRules.Active(book.RaidLedger);

			KingdomLifecycleOperation acknowledge = LedgerOp(incident,
				KingdomLifecycleAction.RaidAcknowledgeDemand, 12L);
			acknowledge.Origin = incident.DemandObjectId;
			acknowledge.DepartTick = 112L;
			book.RaidLedger = Apply(book.RaidLedger, acknowledge);
			incident = KingdomRaidIncidentRules.Active(book.RaidLedger);

			incident.State = KingdomRaidIncidentState.FightCommitted;
			incident.Response = KingdomRaidResponse.Fight;
			Assert.IsTrue(KingdomRaidIncidentRules.ValidLedger(book.RaidLedger));
			return incident;
		}

		/// <summary>A compiled production-rule protocol exercise: builds and publishes a
		/// RaidAttack operation using exactly the rule calls LaunchRaid now uses (PrepareOperation,
		/// RaidRuntimeAdapter.PrepareProjection, RaidRuntimeAdapter.PrepareLeases, TryPublish,
		/// AdvancePhase), assembled by hand. This does NOT execute LaunchRaid or ResumeOpen;
		/// runtime-adapter evidence against the live engine is root-owned native testing.
		/// </summary>
		private static KingdomLifecycleOperation PublishFrozenAttack(KingdomLifecycleBook book,
			KingdomRaidIncident incident, int partySize, out string blueprint)
		{
			blueprint = "Snapjaw";
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Raid, KingdomLifecycleAction.RaidAttack, 20L);
			Assert.NotNull(op, "PrepareOperation");
			op.ZoneId = incident.TargetZoneId;
			op.ObjectId = incident.Id;
			op.Faction = incident.AttackerFactionId;
			op.Origin = "test-store";
			op.ArrivalText = "stores";
			op.Target = 1;
			op.Count = 1;
			op.Defence = 0;
			op.PartySize = partySize;
			op.PlunderRequested = 1;
			op.EffectState = KingdomLifecyclePhysicalState.Prepared;
			for (int i = 0; i < partySize; i++)
			{
				KingdomLifecycleProjection projection = KingdomLifecycleRules.RaidRuntimeAdapter
					.PrepareProjection(book, op, i, KingdomLifecycleRules.ChildId(op.Id, "raider", i),
						blueprint, op.ZoneId, i, 0);
				Assert.NotNull(projection, "PrepareProjection " + i);
			}
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, "chronicle", "ledger", "message",
				"deed", "guestbook");
			Assert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(book, op),
				"PrepareLeases");
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op), "TryPublish");
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 20L), "AdvancePhase");
			return op;
		}

		// The real AllProjectionsProved lives in XRL-coupled Raids/09.cs and is root-owned
		// native evidence; it is not re-implemented here. The per-projection
		// Assert.AreEqual(Prepared, projection.State) loop below and the Assert.AreEqual(0,
		// op.Spawned) assertion are the proof for this engine-free slice.

		[Test]
		public void PublishedRaidAttackFreezesNProjectionsBeforeAnyMint()
		{
			KingdomLifecycleBook book = Book("city-launch-order-freeze");
			KingdomRaidIncident incident = SeedFightCommittedIncident(book);
			string blueprint;
			KingdomLifecycleOperation op = PublishFrozenAttack(book, incident, PartySize,
				out blueprint);

			Assert.AreSame(op, book.Raid);
			Assert.AreEqual(KingdomLifecyclePhase.ProjectionIntent, op.Phase);
			Assert.AreEqual(PartySize, op.Projections.Count);
			Assert.AreEqual(0, op.Spawned);
			// The operation's Phase is ProjectionIntent, but no projection has been through
			// BeginProjection yet -- each one is still exactly as PrepareProjection left it
			// (State == Prepared). ResumeAttackProjections (09.cs) is what later drives
			// Prepared -> Intent -> Proved per raider; see
			// ProjectionCommitRulesIncrementSpawnedForExactEvidence below.
			for (int i = 0; i < PartySize; i++)
			{
				KingdomLifecycleProjection projection = op.Projections[i];
				Assert.AreEqual(KingdomLifecyclePhysicalState.Prepared, projection.State);
				Assert.AreEqual(blueprint, projection.Blueprint);
				Assert.AreEqual(KingdomLifecycleRules.ChildId(op.Id, "raider", i),
					projection.ObjectId);
			}
		}

		[Test]
		public void ProjectionCommitRulesIncrementSpawnedForExactEvidence()
		{
			KingdomLifecycleBook book = Book("city-launch-order-resume");
			KingdomRaidIncident incident = SeedFightCommittedIncident(book);
			string blueprint;
			KingdomLifecycleOperation op = PublishFrozenAttack(book, incident, PartySize,
				out blueprint);

			// The rewind half (BeginProjection -> ResetAbsentProjectionIntent on zero ids/markers)
			// is already covered by KingdomLifecycleRulesTests.
			// RaidProjectionIntentRetriesOnlyAfterExactAbsenceProof; only the CommitProjection
			// half is new coverage here.
			KingdomLifecycleProjection exact = op.Projections[1];
			Assert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.BeginProjection(
				book, op, exact, 0, 0));
			int spawnedBefore = op.Spawned;
			Assert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(
				book, op, exact, 1, 1, exact.Blueprint, exact.ZoneId, exact.X, exact.Y));
			Assert.AreEqual(KingdomLifecyclePhysicalState.Proved, exact.State);
			Assert.AreEqual(spawnedBefore + exact.Count, op.Spawned,
				"one id and one marker must commit exactly once");
		}
	}
}
#endif
