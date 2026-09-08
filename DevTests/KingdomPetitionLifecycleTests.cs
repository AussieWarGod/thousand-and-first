#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomPetitionLifecycleTests
	{
		[Test]
		public void LifecycleOrdinals_AreAppendOnlySaveValues()
		{
			ClassicAssert.AreEqual(0, (int)PetitionLifecycle.None);
			ClassicAssert.AreEqual(1, (int)PetitionLifecycle.Offered);
			ClassicAssert.AreEqual(2, (int)PetitionLifecycle.Accepted);
			ClassicAssert.AreEqual(3, (int)PetitionLifecycle.Declined);
			ClassicAssert.AreEqual(4, (int)PetitionLifecycle.Resolved);
			ClassicAssert.AreEqual(5, (int)PetitionLifecycle.Expired);
		}

		[Test]
		public void TransitionMatrix_IsExact()
		{
			foreach (PetitionLifecycle from in Enum.GetValues(typeof(PetitionLifecycle)))
			{
				foreach (PetitionLifecycle to in Enum.GetValues(typeof(PetitionLifecycle)))
				{
					bool expected = (to == PetitionLifecycle.Offered
						&& (from == PetitionLifecycle.None || KingdomPetitionRules.IsTerminal(from)))
						|| (from == PetitionLifecycle.Offered
							&& (to == PetitionLifecycle.Accepted || to == PetitionLifecycle.Declined
								|| to == PetitionLifecycle.Expired))
						|| (from == PetitionLifecycle.Accepted
							&& (to == PetitionLifecycle.Resolved || to == PetitionLifecycle.Expired));
					ClassicAssert.AreEqual(expected, KingdomPetitionRules.CanTransition(from, to),
						from + " -> " + to);
				}
			}
		}

		[Test]
		public void CalendarBuckets_MatchEveryQudMonthBoundaryIncludingUtYara()
		{
			long[] starts = new long[13]
			{
				0L, 36001L, 72001L, 108001L, 144001L, 180001L, 216001L,
				222001L, 258001L, 294001L, 330001L, 366001L, 402001L
			};
			for (int month = 0; month < starts.Length; month++)
			{
				ClassicAssert.AreEqual(month, KingdomPetitionRules.CanonicalMonthOrdinal(starts[month]));
				if (month > 0)
				{
					ClassicAssert.AreEqual(month - 1,
						KingdomPetitionRules.CanonicalMonthOrdinal(starts[month] - 1L));
				}
			}
			ClassicAssert.AreEqual(12L, KingdomPetitionRules.CanonicalMonthOrdinal(437999L));
			ClassicAssert.AreEqual(13L, KingdomPetitionRules.CanonicalMonthOrdinal(438000L));
			ClassicAssert.AreEqual(19L, KingdomPetitionRules.CanonicalMonthOrdinal(438000L + 216001L));
		}

		[Test]
		public void CalendarOrdinal_IsMonotoneAcrossTwoWholeYears()
		{
			long previous = -1L;
			for (long tick = 0L; tick <= KingdomPetitionRules.TicksPerYear * 2L; tick += 97L)
			{
				long current = KingdomPetitionRules.CanonicalMonthOrdinal(tick);
				ClassicAssert.GreaterOrEqual(current, previous);
				previous = current;
			}
		}

		[Test]
		public void OfferGate_AllowsAtMostOneOfferInEachCanonicalMonth()
		{
			long offered = KingdomPetitionRules.CanonicalMonthOrdinal(216001L);
			ClassicAssert.IsFalse(KingdomPetitionRules.CanOffer(216001L, offered, 0L,
				PetitionLifecycle.Declined, KingdomRules.PetitionKind.None));
			ClassicAssert.IsFalse(KingdomPetitionRules.CanOffer(222000L, offered, 0L,
				PetitionLifecycle.Expired, KingdomRules.PetitionKind.None));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanOffer(222001L, offered, 0L,
				PetitionLifecycle.Resolved, KingdomRules.PetitionKind.None));
		}

		[Test]
		public void OfferGate_UsesLegacyTickWhenNewMonthFieldIsAbsent()
		{
			ClassicAssert.IsFalse(KingdomPetitionRules.CanOffer(50000L, -1L, 40000L,
				PetitionLifecycle.None, KingdomRules.PetitionKind.None));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanOffer(72001L, -1L, 40000L,
				PetitionLifecycle.None, KingdomRules.PetitionKind.None));
		}

		[Test]
		public void ActivePetition_AlwaysBlocksAnotherOffer()
		{
			ClassicAssert.IsFalse(KingdomPetitionRules.CanOffer(999999L, -1L, 0L,
				PetitionLifecycle.Offered, KingdomRules.PetitionKind.Thirst));
			ClassicAssert.IsFalse(KingdomPetitionRules.CanOffer(999999L, -1L, 0L,
				PetitionLifecycle.Accepted, KingdomRules.PetitionKind.Thirst));
		}

		[Test]
		public void EvidenceCannotResolveBeforeAcceptance()
		{
			foreach (PetitionLifecycle state in Enum.GetValues(typeof(PetitionLifecycle)))
			{
				bool expected = state == PetitionLifecycle.Accepted;
				ClassicAssert.AreEqual(expected, KingdomPetitionRules.CanResolve(state,
					KingdomRules.PetitionKind.Thirst, 10, 999, 999, 0, 999, true), state.ToString());
			}
		}

		[Test]
		public void ShelterTarget_DoesNotMoveWhenPopulationLaterChanges()
		{
			int target = KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Shelter, 8);
			ClassicAssert.AreEqual(9, target);
			ClassicAssert.IsFalse(KingdomPetitionRules.IsMet(KingdomRules.PetitionKind.Shelter,
				target, 0, 8, 0, 0, false));
			ClassicAssert.IsTrue(KingdomPetitionRules.IsMet(KingdomRules.PetitionKind.Shelter,
				target, 0, 9, 0, 0, false));
		}

		[Test]
		public void EveryPetitionKindHasStableTargetSemantics()
		{
			ClassicAssert.Greater(KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Thirst, 8), 0);
			ClassicAssert.AreEqual(-100, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Peace, 8));
			ClassicAssert.AreEqual(0, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Craft, 8));
			ClassicAssert.AreEqual(1, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Memorial, 8));
			ClassicAssert.AreEqual(1, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Flesh, 8));
			ClassicAssert.AreEqual(1, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Chrome, 8));
		}

		[Test]
		public void Expiry_IsExactAndOverflowSafe()
		{
			ClassicAssert.IsFalse(KingdomPetitionRules.IsExpired(25000L, 1000L, 24000L));
			ClassicAssert.IsTrue(KingdomPetitionRules.IsExpired(25001L, 1000L, 24000L));
			ClassicAssert.IsFalse(KingdomPetitionRules.IsExpired(long.MaxValue, long.MaxValue - 10L, 10L));
			ClassicAssert.IsTrue(KingdomPetitionRules.IsExpired(long.MaxValue, long.MaxValue - 11L, 10L));
		}

		[Test]
		public void OriginMatch_IsStrictAndNullSafe()
		{
			ClassicAssert.IsTrue(KingdomPetitionRules.OriginMatches("taf:city:a", "taf:city:a"));
			ClassicAssert.IsFalse(KingdomPetitionRules.OriginMatches("taf:city:a", "taf:city:b"));
			ClassicAssert.IsFalse(KingdomPetitionRules.OriginMatches(null, "taf:city:a"));
			ClassicAssert.IsFalse(KingdomPetitionRules.OriginMatches("", ""));
		}

		[Test]
		public void LegacyActivePetition_MigratesToOfferedNeverAccepted()
		{
			ClassicAssert.AreEqual(PetitionLifecycle.Offered,
				KingdomPetitionRules.NormalizeLegacy(PetitionLifecycle.None,
					KingdomRules.PetitionKind.Thirst));
			ClassicAssert.AreNotEqual(PetitionLifecycle.Accepted,
				KingdomPetitionRules.NormalizeLegacy(PetitionLifecycle.Resolved,
					KingdomRules.PetitionKind.Flesh));
			ClassicAssert.AreEqual(PetitionLifecycle.Expired,
				KingdomPetitionRules.NormalizeLegacy(PetitionLifecycle.Accepted,
					KingdomRules.PetitionKind.None));
			ClassicAssert.AreEqual(PetitionLifecycle.Offered,
				KingdomPetitionRules.NormalizeLegacy((PetitionLifecycle)255,
					KingdomRules.PetitionKind.Thirst));
			ClassicAssert.AreEqual(PetitionLifecycle.None,
				KingdomPetitionRules.NormalizeLegacy((PetitionLifecycle)255,
					KingdomRules.PetitionKind.None));
		}

		[Test]
		public void CorruptTargets_AreRepairedOnlyWhereTheyCouldInventOrEraseTruth()
		{
			ClassicAssert.IsTrue(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Thirst, -1));
			ClassicAssert.IsTrue(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Shelter, 0));
			ClassicAssert.IsTrue(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Peace, 0));
			ClassicAssert.IsFalse(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Peace, -100));
			ClassicAssert.IsFalse(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Craft, 0));
			ClassicAssert.IsTrue(KingdomPetitionRules.TargetValid(KingdomRules.PetitionKind.Thirst, 1));
			ClassicAssert.IsTrue(KingdomPetitionRules.TargetValid(KingdomRules.PetitionKind.Shelter, 1));
			ClassicAssert.IsTrue(KingdomPetitionRules.TargetValid(KingdomRules.PetitionKind.Craft, 0));
			ClassicAssert.IsTrue(KingdomPetitionRules.TargetValid(KingdomRules.PetitionKind.Peace, -100));
			ClassicAssert.IsTrue(KingdomPetitionRules.TargetValid(KingdomRules.PetitionKind.Memorial, 1));
			ClassicAssert.IsFalse(KingdomPetitionRules.TargetValid(KingdomRules.PetitionKind.Craft, 1));
			ClassicAssert.IsFalse(KingdomPetitionRules.TargetValid(KingdomRules.PetitionKind.Peace, -99));
			ClassicAssert.IsFalse(KingdomPetitionRules.TargetValid(KingdomRules.PetitionKind.Memorial, 2));
		}

		[Test]
		public void LifecycleActionGraph_IsExactAndPauseResumeUsesRepeatedAccept()
		{
			KingdomLifecycleAction[] actions =
			{
				KingdomLifecycleAction.PetitionOffer,
				KingdomLifecycleAction.PetitionAccept,
				KingdomLifecycleAction.PetitionDecline,
				KingdomLifecycleAction.PetitionResolve,
				KingdomLifecycleAction.PetitionExpire
			};
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(KingdomLifecycleAction.None, actions[0]));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(actions[0], actions[1]));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(actions[0], actions[2]));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(actions[0], actions[4]));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(actions[1], actions[1]));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(actions[1], actions[3]));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(actions[1], actions[4]));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(actions[2], actions[0]));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(actions[3], actions[0]));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanFollow(actions[4], actions[0]));
			for (int i = 0; i < actions.Length; i++)
				for (int j = 0; j < actions.Length; j++)
				{
					bool listed = (i == 0 && (j == 1 || j == 2 || j == 4))
						|| (i == 1 && (j == 1 || j == 3 || j == 4))
						|| (i >= 2 && j == 0);
					ClassicAssert.AreEqual(listed, KingdomPetitionRules.CanFollow(actions[i], actions[j]),
						actions[i] + " -> " + actions[j]);
				}
		}

		[Test]
		public void DistrictInterval_IsRestoredAndOverflowSafe()
		{
			ClassicAssert.AreEqual(3600L, KingdomPetitionRules.ScaledInterval(3600L, 100));
			ClassicAssert.AreEqual(2700L, KingdomPetitionRules.ScaledInterval(3600L, 75));
			ClassicAssert.AreEqual(1L, KingdomPetitionRules.ScaledInterval(1L, 75));
			ClassicAssert.AreEqual(-1L, KingdomPetitionRules.ScaledInterval(long.MaxValue, 100));
			ClassicAssert.AreEqual(-1L, KingdomPetitionRules.ScaledInterval(3600L, 0));
			ClassicAssert.IsFalse(KingdomPetitionRules.CanOfferAt(3699L, 0L, 100L, 3600L));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanOfferAt(3700L, 0L, 100L, 3600L));
			ClassicAssert.IsFalse(KingdomPetitionRules.CanOfferAt(5000L, 2000L, 100L, 3600L));
			ClassicAssert.IsTrue(KingdomPetitionRules.CanOfferAt(5600L, 2000L, 100L, 3600L));
		}

		[Test]
		public void AcceptedClock_PausesAndResumesFromExactRemainingDuration()
		{
			ClassicAssert.AreEqual(600L, KingdomPetitionRules.PauseRemaining(400L, 1000L));
			ClassicAssert.AreEqual(1L, KingdomPetitionRules.PauseRemaining(1001L, 1000L));
			ClassicAssert.IsTrue(KingdomPetitionRules.TryResumeDeadline(9000L, 600L,
				out long deadline));
			ClassicAssert.AreEqual(9600L, deadline);
			ClassicAssert.IsFalse(KingdomPetitionRules.TryResumeDeadline(long.MaxValue, 1L,
				out deadline));
			ClassicAssert.IsFalse(KingdomPetitionRules.IsExpired(9600L, 9600L));
			ClassicAssert.IsTrue(KingdomPetitionRules.IsExpired(9601L, 9600L));
		}

		[Test]
		public void FrozenSnapshot_RequiresExactRequesterBodyOriginCauseTargetAndEvent()
		{
			KingdomLifecycleOperation offer = Snapshot(KingdomLifecycleAction.PetitionOffer);
			ClassicAssert.IsTrue(KingdomPetitionRules.FrozenSnapshotValid(offer));
			string[] required =
			{
				offer.ObjectId, offer.Blueprint, offer.ObjectName, offer.Origin,
				offer.ZoneId, offer.Detail, offer.ObjectMarker, offer.ArrivalText
			};
			for (int i = 0; i < required.Length; i++)
			{
				KingdomLifecycleOperation broken = CopySnapshot(offer);
				switch (i)
				{
				case 0: broken.ObjectId = null; break;
				case 1: broken.Blueprint = null; break;
				case 2: broken.ObjectName = null; break;
				case 3: broken.Origin = null; break;
				case 4: broken.ZoneId = null; break;
				case 5: broken.Detail = null; break;
				case 6: broken.ObjectMarker = null; break;
				default: broken.ArrivalText = null; break;
				}
				ClassicAssert.IsFalse(KingdomPetitionRules.FrozenSnapshotValid(broken), "field " + i);
			}
			KingdomLifecycleOperation foreign = CopySnapshot(offer);
			foreign.Origin = "city-b";
			ClassicAssert.IsFalse(KingdomPetitionRules.FrozenSnapshotValid(foreign));
			KingdomLifecycleOperation control = CopySnapshot(offer);
			control.ObjectMarker = "event\nforged";
			ClassicAssert.IsFalse(KingdomPetitionRules.FrozenSnapshotValid(control));
			ClassicAssert.IsFalse(KingdomPetitionRules.EventIdValid("event\tforged"));
			ClassicAssert.IsFalse(KingdomPetitionRules.EventIdValid("   "));
			ClassicAssert.IsFalse(KingdomPetitionRules.SnapshotTextValid("\ud800", 3, false));
			ClassicAssert.IsFalse(KingdomPetitionRules.SnapshotTextValid("abcd", 3, false));
		}

		[Test]
		public void FrozenSnapshot_CanChangeOnlyActionClockAndDeadline()
		{
			KingdomLifecycleOperation offer = Snapshot(KingdomLifecycleAction.PetitionOffer);
			KingdomLifecycleOperation accepted = CopySnapshot(offer);
			accepted.Action = KingdomLifecycleAction.PetitionAccept;
			accepted.Creed = KingdomPetitionRules.PausedClock;
			accepted.DepartTick = 600L;
			ClassicAssert.IsTrue(KingdomPetitionRules.SameFrozenSnapshot(offer, accepted));

			Action<KingdomLifecycleOperation>[] corruptions =
			{
				o => o.SettlementId = "city-b", o => o.ZoneId = "zone-b",
				o => o.ObjectId = "body-b", o => o.Blueprint = "OtherCitizen",
				o => o.ObjectName = "Bex", o => o.Faction = "Barathrumites",
				o => o.DisplayFaction = "the Barathrumites", o => o.Detail = "other cause",
				o => o.Kind = (int)KingdomRules.PetitionKind.Shelter, o => o.Target = 11,
				o => o.ObjectMarker = "event-b", o => o.ArrivalText = "91"
			};
			for (int i = 0; i < corruptions.Length; i++)
			{
				KingdomLifecycleOperation changed = CopySnapshot(accepted);
				corruptions[i](changed);
				ClassicAssert.IsFalse(KingdomPetitionRules.SameFrozenSnapshot(offer, changed),
					"semantic " + i);
			}
		}

		[Test]
		public void PetitionAdapter_DrivesEveryDurablePhaseAndRetainsTerminalAuthority()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation offer = Draft(book, KingdomLifecycleAction.PetitionOffer, 100L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, offer));
			ClassicAssert.AreSame(offer, book.Petition);
			ClassicAssert.AreEqual(2, offer.ResourceLeases.Count);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, offer,
				KingdomLifecyclePhase.DomainIntent, 101L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.PetitionRuntimeAdapter.ProveDomain(book, offer));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, offer,
				KingdomLifecyclePhase.DomainSettled, 102L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, offer,
				KingdomLifecyclePhase.Sinks, 103L));
			Deliver(book, offer, KingdomLifecycleSinkMask.Chronicle);
			Deliver(book, offer, KingdomLifecycleSinkMask.Ledger);
			Deliver(book, offer, KingdomLifecycleSinkMask.Message);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, offer,
				KingdomLifecyclePhase.ScheduleIntent, 104L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.PetitionRuntimeAdapter.ProveSchedule(book, offer));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, offer,
				KingdomLifecyclePhase.Terminal, 105L));
			ClassicAssert.AreSame(offer, book.Petition, "terminal petition remains the current state");
			ClassicAssert.AreEqual(PetitionLifecycle.Offered, KingdomPetitionRules.LifecycleOf(book.Petition));
		}

		[Test]
		public void PetitionOutboxRecovery_RetriesOnlyReceiptOwnedChronicle()
		{
			KingdomLifecycleBook book = Book("city-outbox");
			KingdomLifecycleOperation offer = Draft(book, KingdomLifecycleAction.PetitionOffer, 100L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, offer));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, offer,
				KingdomLifecyclePhase.DomainIntent, 101L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.PetitionRuntimeAdapter.ProveDomain(book, offer));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, offer,
				KingdomLifecyclePhase.DomainSettled, 102L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, offer,
				KingdomLifecyclePhase.Sinks, 103L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.PetitionRuntimeAdapter.BeginSink(book, offer,
				KingdomLifecycleSinkMask.Chronicle));
			ClassicAssert.IsTrue(KingdomLifecycleRules.PetitionRuntimeAdapter.BeginSink(book, offer,
				KingdomLifecycleSinkMask.Ledger));
			ClassicAssert.IsTrue(KingdomLifecycleRules.RecoverOutbox(book, offer));
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, offer.Outbox.ChronicleState);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Lost, offer.Outbox.LedgerState);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, offer.Outbox.MessageState);
		}

		[Test]
		public void PetitionWire_RoundTripsRetainedSnapshotAndReceiptsExactly()
		{
			KingdomLifecycleBook book = Book("city-wire-petition");
			KingdomLifecycleOperation offer = Draft(book, KingdomLifecycleAction.PetitionOffer, 100L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, offer));
			KingdomLifecycleBook loaded = RoundTrip(book);
			ClassicAssert.IsFalse(loaded.Quarantined);
			ClassicAssert.NotNull(loaded.Petition);
			ClassicAssert.IsTrue(KingdomPetitionRules.SameFrozenSnapshot(offer, loaded.Petition));
			ClassicAssert.AreEqual(offer.PlanHash, loaded.Petition.PlanHash);
			ClassicAssert.AreEqual(offer.Outbox.ChronicleReceiptId,
				loaded.Petition.Outbox.ChronicleReceiptId);
			ClassicAssert.AreEqual(2, loaded.Petition.ResourceLeases.Count);
		}

		[Test]
		public void MalformedPetition_IsQuarantinedWithoutClearingItsEvidence()
		{
			KingdomLifecycleBook book = Book("city-malformed-petition");
			KingdomLifecycleOperation offer = Draft(book, KingdomLifecycleAction.PetitionOffer, 100L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, offer));
			offer.ObjectMarker = "rewritten-event";
			KingdomLifecycleRules.Normalize(book);
			ClassicAssert.IsTrue(book.Quarantined);
			ClassicAssert.AreSame(offer, book.Petition);
			ClassicAssert.AreEqual("rewritten-event", book.Petition.ObjectMarker);
			ClassicAssert.AreEqual(KingdomLifecyclePhase.Quarantined, book.Petition.Phase);
		}

		private static KingdomLifecycleBook Book(string id = "city-a")
		{
			KingdomLifecycleBook book = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(book, id, false,
				null, new List<string>()));
			return book;
		}

		private static KingdomLifecycleOperation Draft(KingdomLifecycleBook book,
			KingdomLifecycleAction action, long tick)
		{
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Petition, action, tick);
			ClassicAssert.NotNull(op);
			KingdomLifecycleOperation snapshot = Snapshot(action, book.SettlementId, tick);
			op.ZoneId = snapshot.ZoneId;
			op.ObjectId = snapshot.ObjectId;
			op.Blueprint = snapshot.Blueprint;
			op.ObjectName = snapshot.ObjectName;
			op.Origin = snapshot.Origin;
			op.Faction = snapshot.Faction;
			op.DisplayFaction = snapshot.DisplayFaction;
			op.Detail = snapshot.Detail;
			op.Kind = snapshot.Kind;
			op.Target = snapshot.Target;
			op.ObjectMarker = snapshot.ObjectMarker;
			op.ArrivalText = snapshot.ArrivalText;
			op.DepartTick = snapshot.DepartTick;
			op.Creed = snapshot.Creed;
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, "chronicle", "ledger",
				"message", null, null);
			ClassicAssert.IsTrue(KingdomLifecycleRules.PetitionRuntimeAdapter.PrepareLeases(book, op));
			return op;
		}

		private static KingdomLifecycleOperation Snapshot(KingdomLifecycleAction action,
			string settlement = "city-a", long tick = 100L)
		{
			return new KingdomLifecycleOperation
			{
				Lane = KingdomLifecycleLane.Petition,
				Action = action,
				Phase = KingdomLifecyclePhase.Prepared,
				CreatedTick = tick,
				UpdatedTick = tick,
				SettlementId = settlement,
				ZoneId = "zone-a",
				ObjectId = "body-a",
				Blueprint = "KingdomCitizen",
				ObjectName = "Ada",
				Origin = settlement,
				Faction = "Joppa",
				DisplayFaction = "the people of Joppa",
				Detail = "the dry cistern",
				Kind = (int)KingdomRules.PetitionKind.Thirst,
				Target = 10,
				ObjectMarker = "petition-event-a",
				ArrivalText = "90",
				DepartTick = 1000L,
				Creed = KingdomPetitionRules.ActiveClock,
				WaterState = KingdomLifecyclePhysicalState.Skipped,
				RemovalState = KingdomLifecyclePhysicalState.Skipped,
				EffectState = KingdomLifecyclePhysicalState.Skipped
			};
		}

		private static KingdomLifecycleOperation CopySnapshot(KingdomLifecycleOperation source)
		{
			return new KingdomLifecycleOperation
			{
				Lane = source.Lane,
				Action = source.Action,
				Phase = source.Phase,
				CreatedTick = source.CreatedTick,
				UpdatedTick = source.UpdatedTick,
				SettlementId = source.SettlementId,
				ZoneId = source.ZoneId,
				ObjectId = source.ObjectId,
				Blueprint = source.Blueprint,
				ObjectName = source.ObjectName,
				Origin = source.Origin,
				Faction = source.Faction,
				DisplayFaction = source.DisplayFaction,
				Detail = source.Detail,
				Kind = source.Kind,
				Target = source.Target,
				ObjectMarker = source.ObjectMarker,
				ArrivalText = source.ArrivalText,
				DepartTick = source.DepartTick,
				Creed = source.Creed,
				WaterState = KingdomLifecyclePhysicalState.Skipped,
				RemovalState = KingdomLifecyclePhysicalState.Skipped,
				EffectState = KingdomLifecyclePhysicalState.Skipped
			};
		}

		private static void Deliver(KingdomLifecycleBook book, KingdomLifecycleOperation op,
			KingdomLifecycleSinkMask sink)
		{
			ClassicAssert.IsTrue(KingdomLifecycleRules.PetitionRuntimeAdapter.BeginSink(book, op, sink));
			ClassicAssert.IsTrue(KingdomLifecycleRules.PetitionRuntimeAdapter.CommitSink(book, op, sink));
		}

		private static KingdomLifecycleBook RoundTrip(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream,
					System.Text.Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteLifecycle(writer, book);
				stream.Position = 0;
				KingdomLifecycleBook loaded = new KingdomLifecycleBook();
				KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(stream), loaded);
				return loaded;
			}
		}
	}
}
#endif
