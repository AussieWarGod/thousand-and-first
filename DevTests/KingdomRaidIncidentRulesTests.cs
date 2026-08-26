#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomRaidIncidentRulesTests
	{
		[Test]
		public void ExactAuthoredSourceMintsOneStableGrievanceAndOneStableIncident()
		{
			KingdomRaidLedger ledger = new KingdomRaidLedger();
			KingdomLifecycleOperation warning = Warning("city-a", "source-a", 10L, 6);
			Assert.AreEqual(KingdomRaidIncidentRules.GrievanceId("source-a"), warning.ObjectId);
			Assert.AreEqual(KingdomRaidIncidentRules.IncidentId(warning.ObjectId),
				warning.ObjectMarker);
			Assert.IsTrue(KingdomRaidIncidentRules.TryApply(ledger, warning, out ledger));
			Assert.AreEqual(1, ledger.Grievances.Count);
			Assert.AreEqual(1, ledger.Incidents.Count);
			Assert.AreEqual(warning.ObjectMarker, ledger.ActiveIncidentId);
			Assert.AreEqual("specific authored evidence", ledger.Incidents[0].CauseSnapshot);
			Assert.AreEqual("Snapjaws", ledger.Incidents[0].AttackerFactionId);
			Assert.AreEqual("zone-a", ledger.Incidents[0].TargetZoneId);
			Assert.AreEqual(KingdomRaidIncidentState.Rumored, ledger.Incidents[0].State);
			Assert.AreEqual(0L, ledger.Incidents[0].DeliveredTick);
			Assert.AreEqual(0L, ledger.Incidents[0].DueTick);
			Assert.AreEqual(100L, ledger.Incidents[0].DemandLeadTicks);
			Assert.AreEqual(KingdomRaidChannelState.AwaitingDelivery,
				ledger.Incidents[0].ChannelState);
			Assert.AreEqual("stores", ledger.Incidents[0].ObjectiveCode);
			Assert.AreEqual(24, ledger.Incidents[0].MaximumPlunder);
			Assert.AreEqual("salt-road scouts", ledger.Incidents[0].ReachRule);
			Assert.IsFalse(KingdomRaidIncidentRules.TryApply(ledger, warning, out _));
			Assert.IsTrue(KingdomRaidIncidentRules.SourceConsumed(ledger, "source-a"));
		}

		[Test]
		public void RumorPhysicalDeliveryAcknowledgementLossAndDeadlineStayDistinct()
		{
			KingdomRaidLedger ledger = Apply(new KingdomRaidLedger(),
				Warning("city-a", "channel-source", 10L, 6));
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(ledger);
			Assert.AreEqual(KingdomRaidIncidentState.Rumored, incident.State);
			Assert.AreEqual(0L, incident.DueTick);

			KingdomLifecycleOperation first = Delivery(incident, 11L);
			ledger = Apply(ledger, first);
			incident = KingdomRaidIncidentRules.Active(ledger);
			Assert.AreEqual(KingdomRaidIncidentState.Rumored, incident.State);
			Assert.AreEqual(KingdomRaidChannelState.Issued, incident.ChannelState);
			Assert.AreEqual(0L, incident.DueTick);

			ledger = Apply(ledger, Acknowledgement(incident, 12L, 112L));
			incident = KingdomRaidIncidentRules.Active(ledger);
			Assert.AreEqual(KingdomRaidIncidentState.Warned, incident.State);
			Assert.AreEqual(112L, incident.DueTick);

			KingdomLifecycleOperation lost = Response(incident,
				KingdomLifecycleAction.RaidLoseChannel, 20L);
			lost.Origin = incident.DemandObjectId;
			ledger = Apply(ledger, lost);
			incident = KingdomRaidIncidentRules.Active(ledger);
			Assert.AreEqual(KingdomRaidChannelState.RedeliveryQueued, incident.ChannelState);
			Assert.AreEqual(0L, incident.DueTick);
			Assert.AreEqual(92L, incident.RemainingLeadTicks);

			ledger = Apply(ledger, Delivery(incident, 30L));
			incident = KingdomRaidIncidentRules.Active(ledger);
			Assert.AreEqual(2, incident.ChannelRevision);
			ledger = Apply(ledger, Acknowledgement(incident, 31L, 123L));
			incident = KingdomRaidIncidentRules.Active(ledger);
			KingdomLifecycleOperation deadline = Response(incident,
				KingdomLifecycleAction.RaidDeadline, 123L);
			ledger = Apply(ledger, deadline);
			incident = KingdomRaidIncidentRules.Active(ledger);
			Assert.AreEqual(KingdomRaidIncidentState.ConfrontationReady, incident.State);
			Assert.AreEqual(0L, incident.DueTick);
			Assert.AreEqual(KingdomRaidResponse.None, incident.Response);

			Assert.IsTrue(KingdomRaidIncidentRules.CanPublish(ledger,
				Response(incident, KingdomLifecycleAction.RaidTalkDown, 124L)));
			Assert.IsTrue(KingdomRaidIncidentRules.CanPublish(ledger,
				Response(incident, KingdomLifecycleAction.RaidFight, 124L)));
			KingdomLifecycleOperation order = Response(incident,
				KingdomLifecycleAction.RaidFortifyOrder, 124L);
			Assert.IsTrue(KingdomRaidIncidentRules.CanPublish(ledger, order));
		}

		[Test]
		public void MultipleAuthoredSourcesQueueWithoutLossAndPromoteDeterministically()
		{
			KingdomRaidLedger ledger = Apply(new KingdomRaidLedger(),
				Warning("city-a", "source-a", 10L, 6));
			ledger = Apply(ledger, Warning("city-a", "source-b", 20L, 6));
			Assert.AreEqual(2, ledger.Grievances.Count);
			Assert.AreEqual(2, ledger.Incidents.Count);
			KingdomRaidIncident first = KingdomRaidIncidentRules.Active(ledger);
			KingdomRaidIncident second = ledger.Incidents[1];
			Assert.AreEqual(KingdomRaidIncidentState.Queued, second.State);
			KingdomLifecycleOperation cancel = Response(first,
				KingdomLifecycleAction.RaidCancel, 40L);
			cancel.Kind = (int)KingdomRaidResolution.SourceInvalid;
			ledger = Apply(ledger, cancel);
			Assert.AreEqual(KingdomRaidIncidentState.Cancelled, ledger.Incidents[0].State);
			Assert.AreEqual(second.Id, ledger.ActiveIncidentId);
			Assert.AreEqual(KingdomRaidIncidentState.Rumored, ledger.Incidents[1].State);
			Assert.AreEqual(0L, ledger.Incidents[1].DueTick,
				"promotion carries rumor authority only; delivery has not been acknowledged");
			Assert.AreEqual(100L, ledger.Incidents[1].DemandLeadTicks);
			Assert.AreEqual(KingdomRaidChannelState.AwaitingDelivery,
				ledger.Incidents[1].ChannelState);
			Assert.IsTrue(KingdomRaidIncidentRules.ValidLedger(ledger));
		}

		[Test]
		public void TributeTalkFightAndFortifyHaveExplicitAtomicDomainResults()
		{
			KingdomRaidLedger tribute = Warned(new KingdomRaidLedger(),
				Warning("city-a", "tribute-source", 10L, 6));
			KingdomLifecycleOperation unpaid = Response(
				KingdomRaidIncidentRules.Active(tribute), KingdomLifecycleAction.RaidTribute, 20L);
			unpaid.WaterRequested = 6;
			Assert.IsFalse(KingdomRaidIncidentRules.TryApply(tribute, unpaid, out _));
			unpaid.WaterProved = 6;
			tribute = Apply(tribute, unpaid);
			Assert.AreEqual(KingdomRaidResolution.TributePaid,
				tribute.Incidents[0].Resolution);
			Assert.IsNull(tribute.ActiveIncidentId);

			KingdomRaidLedger talk = Warned(new KingdomRaidLedger(),
				Warning("city-a", "talk-source", 30L, 6));
			KingdomLifecycleOperation envoy = Response(
				KingdomRaidIncidentRules.Active(talk), KingdomLifecycleAction.RaidTalkDown, 40L);
			talk = Apply(talk, envoy);
			Assert.IsTrue(talk.Incidents[0].TalkObligation);
			Assert.IsTrue(KingdomRaidIncidentRules.HasTalkObligation(talk, "Snapjaws"));
			talk = Apply(talk, Warning("city-a", "later-provocation", 50L, 12));
			Assert.AreEqual(talk.Incidents[1].Id,
				talk.Incidents[0].TalkObligationDischargedBy);
			Assert.IsFalse(KingdomRaidIncidentRules.HasTalkObligation(talk, "Snapjaws"));

			KingdomRaidLedger fight = Warned(new KingdomRaidLedger(),
				Warning("city-a", "fight-source", 60L, 6));
			fight = Apply(fight, Response(KingdomRaidIncidentRules.Active(fight),
				KingdomLifecycleAction.RaidFight, 70L));
			Assert.AreEqual(KingdomRaidIncidentState.FightCommitted,
				KingdomRaidIncidentRules.Active(fight).State);
			Assert.AreEqual(KingdomRaidResponse.Fight,
				KingdomRaidIncidentRules.Active(fight).Response);

			KingdomRaidLedger fortify = Warned(new KingdomRaidLedger(),
				Warning("city-a", "fortify-source", 80L, 6));
			KingdomLifecycleOperation muster = Response(
				KingdomRaidIncidentRules.Active(fortify), KingdomLifecycleAction.RaidFortify, 90L);
			muster.Detail = "R1;101=2[7,9];202=1[]";
			muster.Defence = 3;
			fortify = Apply(fortify, muster);
			Assert.AreEqual(KingdomRaidIncidentState.Fortified,
				KingdomRaidIncidentRules.Active(fortify).State);
			Assert.AreEqual("R1;101=2[7,9];202=1[]",
				KingdomRaidIncidentRules.Active(fortify).DefenceCommitment);
			Assert.AreEqual(3, KingdomRaidIncidentRules.Active(fortify).DefenceEstimate);
			Assert.AreEqual(1,
				KingdomRaidIncidentRules.Active(fortify).DefenceReservationVersion);
			Assert.AreEqual(2,
				KingdomRaidIncidentRules.Active(fortify).DefenceReservations.Count);
		}

		[Test]
		public void FortifyReservationIsCanonicalAndCrewCannotServeTwoWorks()
		{
			List<KingdomRaidDefenceReservation> rows = new List<KingdomRaidDefenceReservation>
			{
				new KingdomRaidDefenceReservation
				{
					WorkId = 202, FrozenScore = 1,
					CrewSemanticIds = new List<int>()
				},
				new KingdomRaidDefenceReservation
				{
					WorkId = 101, FrozenScore = 2,
					CrewSemanticIds = new List<int> { 9, 7 }
				}
			};
			Assert.IsTrue(KingdomRaidIncidentRules.TryEncodeDefenceReservations(rows,
				out string commitment, out int total));
			Assert.AreEqual("R1;101=2[7,9];202=1[]", commitment);
			Assert.AreEqual(3, total);
			Assert.IsTrue(KingdomRaidIncidentRules.TryDecodeDefenceReservations(commitment,
				out List<KingdomRaidDefenceReservation> decoded, out total));
			Assert.AreEqual(2, decoded.Count);

			rows[0].CrewSemanticIds.Add(7);
			Assert.IsFalse(KingdomRaidIncidentRules.TryEncodeDefenceReservations(rows,
				out _, out _), "one resident row cannot reserve two defensive jobs");
			Assert.IsFalse(KingdomRaidIncidentRules.TryDecodeDefenceReservations(
				"R1;101=2[09,7];202=1[]", out _, out _),
				"alternate integer spelling cannot change the frozen plan hash on reload");
		}

		[Test]
		public void AttackCannotRecordPlunderUntilSeparateObjectiveResolutionProof()
		{
			KingdomRaidLedger ledger = Warned(new KingdomRaidLedger(),
				Warning("city-a", "fight-source", 10L, 6));
			ledger = Apply(ledger, Response(KingdomRaidIncidentRules.Active(ledger),
				KingdomLifecycleAction.RaidFight, 20L));
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(ledger);
			KingdomLifecycleOperation attack = Response(incident,
				KingdomLifecycleAction.RaidAttack, 30L);
			attack.Origin = "exact-store-id";
			attack.ArrivalText = "stores";
			attack.Target = 4;
			attack.Count = 5;
			attack.Defence = 0;
			attack.PartySize = 2;
			attack.Spawned = 2;
			attack.PlunderRequested = 24;
			attack.PlunderProved = 6;
			Assert.IsFalse(KingdomRaidIncidentRules.TryApply(ledger, attack, out _),
				"an attack operation cannot pre-credit plunder before objective contact");
			attack.PlunderProved = 0;
			ledger = Apply(ledger, attack);
			incident = KingdomRaidIncidentRules.Active(ledger);
			Assert.AreEqual(KingdomRaidIncidentState.Active, incident.State);
			Assert.AreEqual(0, incident.PlunderProved);
			Assert.AreEqual("exact-store-id", incident.ObjectiveObjectId);
			Assert.AreEqual(4, incident.ObjectiveX);
			Assert.AreEqual(5, incident.ObjectiveY);

			KingdomLifecycleOperation resolve = Response(incident,
				KingdomLifecycleAction.RaidResolve, 40L);
			resolve.Kind = (int)KingdomRaidResolution.StoresPlundered;
			resolve.Target = 3;
			ledger = Apply(ledger, resolve);
			Assert.AreEqual(3, ledger.Incidents[0].PlunderProved);
			Assert.AreEqual(KingdomRaidResolution.StoresPlundered,
				ledger.Incidents[0].Resolution);
			KingdomRaidIncident recovery = ledger.Incidents[0];
			Assert.AreEqual(KingdomRaidRecoveryState.Offered, recovery.RecoveryState);
			Assert.AreEqual(KingdomRaidIncidentRules.RecoveryQuestId(recovery.Id),
				recovery.RecoveryQuestId);
			KingdomLifecycleOperation accept = Response(recovery,
				KingdomLifecycleAction.RaidRecoveryAccept, 41L);
			accept.Origin = recovery.RecoveryQuestId;
			accept.ObjectMarker = recovery.RecoveryStepId;
			ledger = Apply(ledger, accept);
			recovery = ledger.Incidents[0];
			KingdomLifecycleOperation ready = Response(recovery,
				KingdomLifecycleAction.RaidRecoveryReady, 42L);
			ready.Origin = recovery.AttackOperationId;
			ledger = Apply(ledger, ready);
			Assert.AreEqual(KingdomRaidRecoveryState.Ready,
				ledger.Incidents[0].RecoveryState);
			ledger = Apply(ledger, Response(ledger.Incidents[0],
				KingdomLifecycleAction.RaidRecoveryResolve, 43L));
			Assert.AreEqual(KingdomRaidRecoveryState.Resolved,
				ledger.Incidents[0].RecoveryState);
		}

		[Test]
		public void RaidLedgerV1AndV2FixturesMigrateAndV3OrFutureRoundTripsExactly()
		{
			KingdomRaidLedger warned = Warned(new KingdomRaidLedger(),
				Warning("city-a", "frozen-v1", 10L, 6));
			byte[] v1 = KingdomLifecycleWireCodec.WriteRaidLedgerV1Fixture(warned);
			Assert.AreEqual(734, v1.Length, "PIN_RAID_V1_LENGTH");
			Assert.AreEqual("702aaa4e997ef4b96fbc1c4d1989de7a3d62dd99bb46eb4e89d81c82aea35d03",
				Sha256(v1), "PIN_RAID_V1_SHA256");
			KingdomRaidLedger migrated = KingdomLifecycleWireCodec.ReadRaidLedgerFixture(v1);
			Assert.AreEqual(KingdomRaidLedger.CurrentVersion, migrated.Version);
			Assert.AreEqual(KingdomRaidIncidentState.Warned, migrated.Incidents[0].State);
			Assert.AreEqual(KingdomRaidChannelState.RedeliveryQueued,
				migrated.Incidents[0].ChannelState);
			Assert.AreEqual(0L, migrated.Incidents[0].DueTick);

			KingdomRaidLedger oldFortify = Warned(new KingdomRaidLedger(),
				Warning("city-a", "frozen-v2", 20L, 6));
			KingdomLifecycleOperation oldMuster = Response(
				KingdomRaidIncidentRules.Active(oldFortify),
				KingdomLifecycleAction.RaidFortify, 30L);
			oldMuster.Detail = "R1;101=3[7]";
			oldMuster.Defence = 3;
			oldFortify = Apply(oldFortify, oldMuster);
			byte[] v2 = KingdomLifecycleWireCodec.WriteRaidLedgerV2Fixture(oldFortify);
			Assert.AreEqual("da982eef9f8c70fcfb677cb33e5c084b549e7c670de55974bf5d050fd2c47a60",
				Sha256(v2), "PIN_RAID_V2_SHA256");
			KingdomRaidLedger reopened = KingdomLifecycleWireCodec.ReadRaidLedgerFixture(v2);
			Assert.AreEqual(KingdomRaidLedger.CurrentVersion, reopened.Version);
			Assert.AreEqual(KingdomRaidIncidentState.ConfrontationReady,
				reopened.Incidents[0].State);
			Assert.AreEqual(KingdomRaidResponse.None, reopened.Incidents[0].Response);
			Assert.AreEqual(0, reopened.Incidents[0].DefenceReservationVersion);
			Assert.AreEqual(0, reopened.Incidents[0].DefenceReservations.Count);
			StringAssert.Contains("older muster", reopened.Incidents[0].LastNotice);

			byte[] current = KingdomLifecycleWireCodec.WriteRaidLedgerFixture(warned);
			CollectionAssert.AreEqual(current, KingdomLifecycleWireCodec.WriteRaidLedgerFixture(
				KingdomLifecycleWireCodec.ReadRaidLedgerFixture(current)));

			KingdomRaidLedger future = new KingdomRaidLedger
			{
				Version = KingdomRaidLedger.CurrentVersion + 1,
				OpaqueFuturePayload = new byte[] { 4, 8, 15, 16, 23, 42 }
			};
			byte[] opaque = KingdomLifecycleWireCodec.WriteRaidLedgerFixture(future);
			KingdomRaidLedger retained = KingdomLifecycleWireCodec.ReadRaidLedgerFixture(opaque);
			Assert.AreEqual(future.Version, retained.Version);
			CollectionAssert.AreEqual(future.OpaqueFuturePayload, retained.OpaqueFuturePayload);
			CollectionAssert.AreEqual(opaque,
				KingdomLifecycleWireCodec.WriteRaidLedgerFixture(retained));
			Assert.IsNull(KingdomRaidIncidentRules.Active(retained));
		}

		[Test]
		public void LedgerRejectsSemanticEvidenceAndObjectiveTampering()
		{
			KingdomRaidLedger original = Apply(new KingdomRaidLedger(),
				Warning("city-a", "tamper-source", 10L, 6));

			KingdomRaidLedger changedCause = KingdomRaidIncidentRules.Copy(original);
			changedCause.Incidents[0].CauseSnapshot = "different evidence";
			Assert.IsFalse(KingdomRaidIncidentRules.ValidLedger(changedCause));

			KingdomRaidLedger changedClock = KingdomRaidIncidentRules.Copy(original);
			changedClock.Incidents[0].DueTick = 1L;
			Assert.IsFalse(KingdomRaidIncidentRules.ValidLedger(changedClock));

			KingdomRaidLedger inventedObjective = KingdomRaidIncidentRules.Copy(original);
			inventedObjective.Incidents[0].ObjectiveObjectId = "store-never-reached";
			inventedObjective.Incidents[0].ObjectiveX = 1;
			inventedObjective.Incidents[0].ObjectiveY = 1;
			Assert.IsFalse(KingdomRaidIncidentRules.ValidLedger(inventedObjective));

			KingdomLifecycleOperation prematureAttack = Response(
				KingdomRaidIncidentRules.Active(original), KingdomLifecycleAction.RaidAttack, 20L);
			prematureAttack.Origin = "exact-store-id";
			prematureAttack.ArrivalText = "stores";
			prematureAttack.Target = 1;
			prematureAttack.Count = 1;
			prematureAttack.PartySize = 1;
			prematureAttack.Spawned = 1;
			prematureAttack.PlunderRequested = 6;
			Assert.IsFalse(KingdomRaidIncidentRules.TryApply(original, prematureAttack, out _),
				"a warning cannot become a physical attack without an explicit fight or fortify answer");
		}

		[Test]
		public void LegacyStandingDerivedEvidenceIsArchivedRawWithoutMintingCause()
		{
			KingdomRaidLedger ledger = new KingdomRaidLedger();
			KingdomLifecycleOperation legacy = new KingdomLifecycleOperation
			{
				Lane = KingdomLifecycleLane.Raid, Action = KingdomLifecycleAction.RaidCancel,
				Kind = (int)KingdomRaidResolution.LegacyWarningDispersed,
				Target = 1, Count = 3, Faction = "Snapjaws", DepartTick = 400L,
				Origin = "350"
			};
			ledger = Apply(ledger, legacy);
			Assert.IsTrue(ledger.LegacyEvidenceArchived);
			Assert.AreEqual(1, ledger.LegacyRaidState);
			Assert.AreEqual("Snapjaws", ledger.LegacyFaction);
			Assert.AreEqual(400L, ledger.LegacyDueTick);
			Assert.AreEqual(350L, ledger.LegacyLastTick);
			Assert.AreEqual(3, ledger.LegacyTimesDeferred);
			Assert.AreEqual(0, ledger.Grievances.Count);
			Assert.AreEqual(0, ledger.Incidents.Count);
			Assert.IsNull(ledger.ActiveIncidentId);
			Assert.IsFalse(KingdomRaidIncidentRules.TryApply(ledger, legacy, out _));
		}

		[Test]
		public void ArchiveV2ColdLoadKeepsRawLegacyFieldsAndCreatesNoGrievance()
		{
			KingdomSettlement old = Settlement("city-old");
			old.LifecycleBook.FormatVersion = KingdomLifecycleRules.PreviousLifecycleFormatVersion;
			old.RaidState = 1;
			old.RaidFactionName = "Snapjaws";
			old.RaidDueTick = 400L;
			old.LastRaidTick = 350L;
			old.RaidTimesDeferred = 3;
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodePreviousV2ForTests(old,
				out byte[] v2, out string failure), failure);
			Assert.AreEqual(39098, v2.Length);
			Assert.AreEqual(
				"a0a5c95781e8893e4a7c1d6ade09ef05dbb6eaa395919a8b012f3e57fa9c987c",
				Sha256(v2));
			Assert.AreEqual(KingdomArchivedSettlementCodec.PreviousVersion,
				BitConverter.ToInt32(v2, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v2,
				out KingdomSettlement loaded, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion,
				loaded.LifecycleBook.FormatVersion);
			Assert.IsTrue(KingdomRaidIncidentRules.ValidLedger(loaded.LifecycleBook.RaidLedger));
			Assert.AreEqual(0, loaded.LifecycleBook.RaidLedger.Grievances.Count);
			Assert.AreEqual(0, loaded.LifecycleBook.RaidLedger.Incidents.Count);
			Assert.AreEqual(1, loaded.RaidState);
			Assert.AreEqual("Snapjaws", loaded.RaidFactionName);
			Assert.AreEqual(400L, loaded.RaidDueTick);
			Assert.AreEqual(350L, loaded.LastRaidTick);
			Assert.AreEqual(3, loaded.RaidTimesDeferred);
		}

		[Test]
		public void ArchiveV3RoundTripRetainsOneIncidentAndRejectsDuplicateEvidence()
		{
			KingdomSettlement source = Settlement("city-current");
			KingdomLifecycleOperation warning = Warning("city-current", "archive-source", 10L, 6);
			source.LifecycleBook.RaidLedger = Apply(source.LifecycleBook.RaidLedger, warning);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(source,
				out byte[] payload, out string failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CurrentVersion,
				BitConverter.ToInt32(payload, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(payload,
				out KingdomSettlement loaded, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(1, loaded.LifecycleBook.RaidLedger.Grievances.Count);
			Assert.AreEqual(1, loaded.LifecycleBook.RaidLedger.Incidents.Count);
			Assert.AreEqual(warning.ObjectMarker,
				loaded.LifecycleBook.RaidLedger.ActiveIncidentId);
			Assert.IsFalse(KingdomRaidIncidentRules.TryApply(
				loaded.LifecycleBook.RaidLedger, warning, out _),
				"archive-to-live transition cannot mint the consumed source again");

			KingdomRaidLedger other = KingdomRaidIncidentRules.Copy(
				loaded.LifecycleBook.RaidLedger);
			loaded.LifecycleBook.RaidLedger.Grievances.Add(other.Grievances[0]);
			loaded.LifecycleBook.RaidLedger.Incidents.Add(other.Incidents[0]);
			Assert.IsFalse(KingdomRaidIncidentRules.ValidLedger(loaded.LifecycleBook.RaidLedger));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeUncheckedCurrentForTests(
				loaded, out byte[] malformed, out failure), failure);
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryDecode(malformed,
				out KingdomSettlement refused, out future, out failure));
			Assert.IsNull(refused);
			StringAssert.Contains("raid evidence", failure);
		}

		private static KingdomSettlement Settlement(string id)
		{
			KingdomSettlement value = new KingdomSettlement();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(value.LifecycleBook,
				id, false, null, new List<string>()));
			return value;
		}

		private static KingdomLifecycleOperation Warning(string settlement, string source,
			long tick, int stake)
		{
			KingdomLifecycleOperation op = new KingdomLifecycleOperation
			{
				Lane = KingdomLifecycleLane.Raid, Action = KingdomLifecycleAction.RaidWarning,
				SettlementId = settlement, ZoneId = "zone-a", Origin = source,
				ObjectName = "authored act", Faction = "Snapjaws",
				DisplayFaction = "salt-road scouts", Creed = "explicit-slight",
				Detail = "specific authored evidence", ArrivalText = "zone-source",
				Target = 1, Count = 2, CreatedTick = tick, DepartTick = tick + 100L,
				PlunderRequested = stake, Kind = 24, Blueprint = "snapjaw-foragers"
			};
			op.ObjectId = KingdomRaidIncidentRules.GrievanceId(source);
			op.ObjectMarker = KingdomRaidIncidentRules.IncidentId(op.ObjectId);
			return op;
		}

		private static KingdomLifecycleOperation Response(KingdomRaidIncident incident,
			KingdomLifecycleAction action, long tick)
		{
			return new KingdomLifecycleOperation
			{
				Id = KingdomLifecycleRules.ChildId(incident.Id, "test-response-" + (byte)action, 0),
				Lane = KingdomLifecycleLane.Raid, Action = action,
				SettlementId = incident.SettlementId, ZoneId = incident.TargetZoneId,
				ObjectId = incident.Id, Faction = incident.AttackerFactionId,
				CreatedTick = tick
			};
		}

		private static KingdomLifecycleOperation Delivery(KingdomRaidIncident incident,
			long tick)
		{
			KingdomLifecycleOperation delivery = Response(incident,
				KingdomLifecycleAction.RaidDeliverDemand, tick);
			delivery.Origin = incident.DemandChannelId;
			delivery.Target = incident.ChannelRevision + 1;
			delivery.ObjectMarker = KingdomRaidIncidentRules.DemandObjectId(
				incident.DemandChannelId, delivery.Target);
			delivery.Count = 1;
			delivery.Blueprint = "r_KingdomSnapjawRaidDemand";
			return delivery;
		}

		private static KingdomLifecycleOperation Acknowledgement(
			KingdomRaidIncident incident, long tick, long due)
		{
			KingdomLifecycleOperation acknowledgement = Response(incident,
				KingdomLifecycleAction.RaidAcknowledgeDemand, tick);
			acknowledgement.Origin = incident.DemandObjectId;
			acknowledgement.DepartTick = due;
			return acknowledgement;
		}

		private static KingdomRaidLedger Warned(KingdomRaidLedger before,
			KingdomLifecycleOperation warning)
		{
			KingdomRaidLedger ledger = Apply(before, warning);
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(ledger);
			ledger = Apply(ledger, Delivery(incident, warning.CreatedTick + 1L));
			incident = KingdomRaidIncidentRules.Active(ledger);
			return Apply(ledger, Acknowledgement(incident, warning.CreatedTick + 2L,
				warning.CreatedTick + 102L));
		}

		private static KingdomRaidLedger Apply(KingdomRaidLedger before,
			KingdomLifecycleOperation operation)
		{
			Assert.IsTrue(KingdomRaidIncidentRules.TryApply(before, operation,
				out KingdomRaidLedger after));
			Assert.IsTrue(KingdomRaidIncidentRules.ValidLedger(after));
			return after;
		}

		private static string Sha256(byte[] bytes)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] digest = sha.ComputeHash(bytes);
				StringBuilder text = new StringBuilder(digest.Length * 2);
				for (int i = 0; i < digest.Length; i++)
					text.Append(digest[i].ToString("x2"));
				return text.ToString();
			}
		}
	}
}
#endif
