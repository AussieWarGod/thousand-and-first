#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomLifecycleRulesTests
	{
		private static KingdomLifecycleBook Book(string id = "city-a")
		{
			KingdomLifecycleBook book = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(book, id, false,
				null, new List<string>()));
			return book;
		}

		[Test]
		public void ActionLaneTable_IsExplicitAndRejectsEveryWrongLane()
		{
			foreach (KingdomLifecycleAction action in Enum.GetValues(typeof(KingdomLifecycleAction)))
			{
				if (action == KingdomLifecycleAction.None) continue;
				int allowed = 0;
				foreach (KingdomLifecycleLane lane in Enum.GetValues(typeof(KingdomLifecycleLane)))
				{
					bool value = KingdomLifecycleRules.ActionAllowedInLane(action, lane);
					if (value) allowed++;
					KingdomLifecycleOperation draft = KingdomLifecycleRules.PrepareOperation(
						Book("city-" + (byte)action + "-" + (byte)lane), lane, action, 1L);
					ClassicAssert.AreEqual(value, draft != null, action + " / " + lane);
				}
				ClassicAssert.Greater(allowed, 0, action.ToString());
			}
		}

		[Test]
		public void EveryAction_HasLegalNonSkippingFsmAndCompletePlan()
		{
			long tick = 10L;
			foreach (KingdomLifecycleAction action in Enum.GetValues(typeof(KingdomLifecycleAction)))
			{
				if (action == KingdomLifecycleAction.None) continue;
				KingdomLifecycleLane lane = FirstLane(action);
				KingdomLifecycleBook book = Book("city-fsm-" + (byte)action);
				KingdomLifecycleOperation op = null;
				try
				{
					op = Build(book, lane, action, tick, tick);
					if (!KingdomLifecycleRules.TryPublish(book, op))
						Assert.Fail("publication failed");
					ClassicAssert.IsFalse(KingdomLifecycleRules.CanTransition(action,
						KingdomLifecyclePhase.Prepared, KingdomLifecyclePhase.Terminal));
					Settle(book, op, tick + 1L);
					ClassicAssert.AreEqual(KingdomLifecyclePhase.Terminal, op.Phase);
					ClassicAssert.IsTrue(KingdomLifecycleRules.Retire(book, op, tick + 100L));
				}
				catch (AssertionException ex)
				{
					Assert.Fail(action + " at " + (op == null ? "build" : op.Phase.ToString())
						+ ": " + ex.Message);
				}
				tick += 10L;
			}
		}

		[Test]
		public void LegalEdge_StillRefusesMissingPhysicalDomainSinkAndScheduleProof()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Projected, 3L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Projected, 3L), "object witness alone lacks projection lease proof");
			SettleProjectionLease(book, op, op.Projections[0]);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Projected, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.DomainIntent, 4L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.DomainSettled, 5L));
			SettleLease(book, op.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Population));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.DomainSettled, 5L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Sinks, 6L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 7L));
			Deliver(op.Outbox);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 7L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Terminal, 8L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(book,
				op, LifecycleScheduleWorld(book, op)));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Terminal, 8L));
		}

		[Test]
		public void PreparedPhysicalCall_RequiresExactBeforeAndNotAfter()
		{
			ClassicAssert.AreEqual(KingdomLifecycleMutationAction.InvokeOnce,
				KingdomLifecycleRules.MutationAction(KingdomLifecyclePhysicalState.Prepared, true, false));
			ClassicAssert.AreEqual(KingdomLifecycleMutationAction.Quarantine,
				KingdomLifecycleRules.MutationAction(KingdomLifecyclePhysicalState.Prepared, false, false));
			ClassicAssert.AreEqual(KingdomLifecycleMutationAction.Quarantine,
				KingdomLifecycleRules.MutationAction(KingdomLifecyclePhysicalState.Prepared, true, true));
			ClassicAssert.AreEqual(KingdomLifecycleMutationAction.ConfirmAfter,
				KingdomLifecycleRules.MutationAction(KingdomLifecyclePhysicalState.Intent, false, true));
			ClassicAssert.AreEqual(KingdomLifecycleMutationAction.Quarantine,
				KingdomLifecycleRules.MutationAction(KingdomLifecyclePhysicalState.Intent, true, false));
		}

		[Test]
		public void CrossLaneEqualScalar_CannotAliasOperationReceipt()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation guest = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			KingdomLifecycleOperation raid = Build(book, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			KingdomLifecycleResourceLease guestShared = guest.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Population);
			KingdomLifecycleResourceLease raidShared = raid.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Population);

			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, guest));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(book, raid), "persisted lease blocks overlap");
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, guest,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			SettleProjectionLease(book, guest, guest.Projections[0]);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, guest,
				KingdomLifecyclePhase.Projected, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, guest,
				KingdomLifecyclePhase.DomainIntent, 4L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.BeginLease(book, guestShared, guestShared.Before));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CommitLeaseWitness(book, guestShared, guestShared.After));
			ClassicAssert.AreEqual(KingdomLifecycleCasAction.Quarantine,
				KingdomLifecycleRules.LeaseAction(book, raidShared, guestShared.After),
				"same scalar after is not another op's proof");
		}

		[Test]
		public void LeaseMutation_RequiresExactPublishedMemberPhaseAndRowWitness()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			KingdomLifecycleResourceLease domain = op.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Raid);
			KingdomLifecycleResourceLease schedule = op.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Schedule);

			ClassicAssert.IsFalse(KingdomLifecycleRules.BeginLease(book, schedule, schedule.Before),
				"Prepared cannot debit the terminal schedule");
			ClassicAssert.AreEqual(KingdomLifecycleCasAction.Quarantine,
				KingdomLifecycleRules.LeaseAction(book, schedule, schedule.Before));
			ClassicAssert.IsFalse(KingdomLifecycleRules.CommitLeaseWitness(book, schedule, schedule.After));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.DomainIntent, 2L));
			KingdomLifecycleResourceLease forged = CopyLease(domain);
			ClassicAssert.IsFalse(KingdomLifecycleRules.BeginLease(book, forged, forged.Before),
				"equal fields are not exact lease membership");
			ClassicAssert.IsTrue(KingdomLifecycleRules.BeginLease(book, domain, domain.Before));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CommitLeaseWitness(book, domain, domain.After));

			KingdomLifecycleBook mismatched = Book("city-mismatch");
			KingdomLifecycleOperation bad = Build(mismatched, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(mismatched, bad));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(mismatched, bad,
				KingdomLifecyclePhase.DomainIntent, 2L));
			KingdomLifecycleResourceLease badDomain = bad.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Raid);
			badDomain.State = KingdomLifecycleLeaseState.Proved;
			KingdomLifecycleBook reloaded = RoundTrip(mismatched);
			ClassicAssert.IsTrue(reloaded.Quarantined,
				"a Proved enum without the exact revision/last-op witness owns no authority");
		}

		[Test]
		public void ProjectionReceipt_RequiresCallbackExactMarkerObjectBlueprintAndTopology()
		{
			KingdomLifecycleBook noCallbackBook = Book("city-projection-no-callback");
			KingdomLifecycleOperation noCallbackOp = Build(noCallbackBook,
				KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(noCallbackBook, noCallbackOp));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(noCallbackBook, noCallbackOp,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleProjection(
				noCallbackBook, noCallbackOp, noCallbackOp.Projections[0], new TrustedWorld()));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Intent,
				noCallbackOp.Projections[0].State);

			KingdomLifecycleBook wrongBlueprintBook = Book("city-projection-blueprint");
			KingdomLifecycleOperation wrongBlueprintOp = Build(wrongBlueprintBook,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Spawn, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(wrongBlueprintBook, wrongBlueprintOp));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(wrongBlueprintBook, wrongBlueprintOp,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			TrustedWorld wrongBlueprint = LifecycleProjectionWorld(wrongBlueprintOp.Projections[0]);
			wrongBlueprint.ProjectionBlueprintOverride = "ForeignBlueprint";
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleProjection(
				wrongBlueprintBook, wrongBlueprintOp, wrongBlueprintOp.Projections[0], wrongBlueprint));

			KingdomLifecycleBook book = Book("city-projection-happy");
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			SettleProjectionLease(book, op, op.Projections[0]);
		}

		[Test]
		public void FrozenPlanAndQuarantinedBook_CannotAdvanceOrRetire()
		{
			KingdomLifecycleBook stale = Book();
			KingdomLifecycleOperation op = Build(stale, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(stale, op));
			op.Detail = "post-publication rewrite";
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvancePhase(stale, op,
				KingdomLifecyclePhase.Sinks, 2L));

			KingdomLifecycleBook terminal = Book("city-terminal");
			op = Build(terminal, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(terminal, op));
			Settle(terminal, op, 2L);
			op.Detail = "receipt no longer matches";
			ClassicAssert.IsFalse(KingdomLifecycleRules.Retire(terminal, op, 100L));
			ClassicAssert.AreSame(op, terminal.PlainGuest);

			op.Detail = null;
			terminal.Quarantined = true;
			ClassicAssert.IsFalse(KingdomLifecycleRules.Retire(terminal, op, 100L));
		}

		[Test]
		public void DraftFailure_DoesNotConsumeCounterOrMintCompetingId()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			string canonical = op.Id;
			op.Id = KingdomLifecycleRules.ChildId(canonical, "forged", 0);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.AreEqual(1L, book.RaidNextSequence);
			ClassicAssert.IsNull(book.Raid);
			op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			ClassicAssert.AreEqual(canonical, op.Id);
		}

		[Test]
		public void RetirementRefusesUnresolvedQuarantinedAndValueClaims()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			op.Phase = KingdomLifecyclePhase.Terminal;
			ClassicAssert.IsFalse(KingdomLifecycleRules.Retire(book, op, 2L));
			ClassicAssert.AreSame(op, book.NotableGuest);
			ClassicAssert.IsTrue(KingdomLifecycleRules.Quarantine(op, "uncertain debit"));
			ClassicAssert.IsFalse(KingdomLifecycleRules.Retire(book, op, 3L));
			ClassicAssert.AreSame(op, book.NotableGuest, "full quarantined value evidence is retained");
			ClassicAssert.AreEqual(op.Id, book.Resources[0].ActiveOperationId);
		}

		[Test]
		public void OutOfOrderLaneRetirement_UsesPerLaneBarriers()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation slow = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, slow));
			for (int i = 0; i < 4; i++)
			{
				long warningSequence = i * 2L + 1L;
				long baseTick = 100L + i * 100L;
				KingdomLifecycleOperation fast = Build(book, KingdomLifecycleLane.Raid,
					KingdomLifecycleAction.RaidWarning, baseTick, warningSequence - 1L);
				ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, fast));
				Settle(book, fast, baseTick + 10L);
				ClassicAssert.IsTrue(KingdomLifecycleRules.Retire(book, fast, baseTick + 20L));
				fast = Build(book, KingdomLifecycleLane.Raid,
					KingdomLifecycleAction.RaidTalkDown, baseTick + 30L, warningSequence);
				ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, fast));
				Settle(book, fast, baseTick + 40L);
				ClassicAssert.IsTrue(KingdomLifecycleRules.Retire(book, fast, baseTick + 50L));
			}
			KingdomLifecycleRules.Normalize(book);
			ClassicAssert.IsFalse(book.Quarantined);
			ClassicAssert.AreSame(slow, book.PlainGuest);
			ClassicAssert.AreEqual(0L, book.PlainGuestRetiredThrough);
			ClassicAssert.AreEqual(8L, book.RaidRetiredThrough);
		}

		[Test]
		public void MoreThanSixtyFourTerminalCycles_KeepPermanentBarrier()
		{
			KingdomLifecycleBook book = Book();
			string first = null;
			for (int i = 0; i < 96; i++)
			{
				KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
					KingdomLifecycleAction.Passages, i + 1L, i);
				ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op), "publish " + i);
				if (i == 0) first = op.Id;
				Settle(book, op, i + 200L);
				ClassicAssert.IsTrue(KingdomLifecycleRules.Retire(book, op, i + 300L), "retire " + i);
			}
			ClassicAssert.AreEqual(96L, book.PlainGuestRetiredThrough);
			ClassicAssert.AreEqual(97L, book.PlainGuestNextSequence);
			ClassicAssert.AreEqual(KingdomLifecycleRules.MaxRecentProofs, book.RecentProofs.Count);
			ClassicAssert.IsFalse(book.RecentProofs.Exists(p => p.Id == first));
		}

		[Test]
		public void DuplicateCanonicalProof_IsQuarantinedWithoutTailRewrite()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			Settle(book, op, 2L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.Retire(book, op, 30L));
			book.RecentProofs.Add(book.RecentProofs[0]);
			KingdomLifecycleRules.Normalize(book);
			ClassicAssert.IsTrue(book.Quarantined);
			ClassicAssert.AreEqual(2, book.RecentProofs.Count, "raw duplicate evidence remains visible");
		}

		[Test]
		public void WaterAndStandingConservation_RejectOverflowAndAmbiguity()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.WaterConserved(op, false));
			op.WaterAmbiguous = 1;
			ClassicAssert.IsFalse(KingdomLifecycleRules.WaterConserved(op, false));
			op.WaterAmbiguous = 0;
			op.WaterOutstanding = 0;
			op.WaterLost = 1;
			ClassicAssert.IsTrue(KingdomLifecycleRules.WaterConserved(op, false),
				"explicit loss replaces outstanding water; it is not extra water");
			ClassicAssert.IsFalse(KingdomLifecycleRules.WaterConserved(op, true),
				"loss evidence cannot retire as a proved debit");
			op.WaterOutstanding = 1;
			ClassicAssert.IsFalse(KingdomLifecycleRules.WaterConserved(op, false),
				"lost plus outstanding cannot exceed the request");
			long ignored;
			ClassicAssert.IsFalse(KingdomLifecycleRules.CheckedAdd(long.MaxValue, 1L, out ignored));
			ClassicAssert.IsNull(KingdomLifecycleRules.PrepareLease(book, op,
				KingdomLifecycleResourceKind.Standing, "city-a", "faction-a", long.MaxValue, 1L));
		}

		[Test]
		public void CarryPerUnitIntentReload_CannotMintEscrowWithoutCallbackReceipt()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 3, 2);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			KingdomCarrySource source = op.Sources[0];
			TrustedWorld noCallback = CarrySourceWorld(source);
			noCallback.CarryRemovalCallback = null;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op,
				source, noCallback));

			KingdomCarryBook reloaded = RoundTrip(book);
			op = reloaded.Open; source = op.Sources[0];
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Intent, source.UnitState);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(reloaded, op,
				source, CarrySourceWorld(source)));
			ClassicAssert.AreEqual(0, source.Removed);
			ClassicAssert.AreEqual(0, op.EscrowMud);

			KingdomCarryBook happy = CarryBook();
			KingdomCarryOperation happyOp = BuildCarry(happy, 1L, 3, 2);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(happy, happyOp));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(happy, happyOp,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			ClassicAssert.IsTrue(ProveCarryUnit(happy, happyOp, happyOp.Sources[0]));
			ClassicAssert.IsTrue(ProveCarryUnit(happy, happyOp, happyOp.Sources[0]));
			ClassicAssert.AreEqual(2, happyOp.EscrowMud);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CarryConserved(happyOp));
		}

		[Test]
		public void CarryIdentityProjection_ForcesNoStackAndExactTopology()
		{
			KingdomCarryBook stackBook = CarryBook();
			KingdomCarryOperation stack = BuildCarry(stackBook, 1L, 3, 2);
			stack.Outputs[0].NoStack = false;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublishCarry(stackBook, stack));
			ClassicAssert.AreEqual(1L, stackBook.NextSequence);

			KingdomCarryBook topologyBook = CarryBook();
			KingdomCarryOperation topology = BuildCarry(topologyBook, 1L, 3, 2);
			topology.Outputs[0].Topology = KingdomLifecycleTopology.Inventory;
			topology.Outputs[0].X = 4;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublishCarry(topologyBook, topology));

			KingdomCarryBook collisionBook = CarryBook();
			KingdomCarryOperation collision = BuildCarry(collisionBook, 1L, 3, 2);
			collision.Outputs[0].ObjectId = collision.Sources[0].ObjectId;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublishCarry(collisionBook, collision),
				"a partial source survivor and output cannot share one global object id");

			KingdomCarryBook realmBook = CarryBook();
			KingdomCarryOperation realm = BuildCarry(realmBook, 1L, 3, 2);
			realm.SettlementIds.RemoveAt(0);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublishCarry(realmBook, realm),
				"carry plan freezes the full sorted realm settlement topology");
		}

		[Test]
		public void CarryMutation_RequiresOpenMemberFrozenPlanAndLegalPhase()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 3, 2);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			KingdomCarrySource source = op.Sources[0];
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op, source,
				CarrySourceWorld(source)),
				"Prepared has no physical removal authority");
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			TrustedWorld wrongBlueprint = CarrySourceWorld(source);
			wrongBlueprint.Rows[0].BlueprintValue = "ForeignBlueprint";
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op,
				source, wrongBlueprint), "wrong blueprint cannot remove or escrow");
			KingdomCarrySource forged = CopySource(source);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op, forged,
				CarrySourceWorld(forged)),
				"equal source fields are not exact source membership");
			string destination = op.DestinationSettlementName;
			op.DestinationSettlementName = "rewritten destination";
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op, source,
				CarrySourceWorld(source)));
			op.DestinationSettlementName = destination;
			ClassicAssert.IsTrue(ProveCarryUnit(book, op, source));
		}

		[Test]
		public void CarryConservationAndRetirement_RefuseNonzeroEscrow()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 2, 2);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			KingdomCarrySource source = op.Sources[0];
			while (source.Removed < source.PlannedCount)
				ClassicAssert.IsTrue(ProveCarryUnit(book, op, source));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			op.Phase = KingdomLifecyclePhase.Terminal;
			ClassicAssert.IsFalse(KingdomLifecycleRules.RetireCarry(book, op, 4L));
			ClassicAssert.AreSame(op, book.Open);
			op.EscrowMud++;
			ClassicAssert.IsFalse(KingdomLifecycleRules.CarryConserved(op));
		}

		[Test]
		public void CarryHappyPath_ConservesSourceOutputEscrowAndLoss()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 2, 2);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			while (op.Sources[0].Removed < op.Sources[0].PlannedCount)
				ClassicAssert.IsTrue(ProveCarryUnit(book, op, op.Sources[0]));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			SettleCarrySchedule(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 5L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(book, op,
				op.Outputs[0], OutputWorld(op.Outputs[0])));
			ClassicAssert.IsTrue(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], false));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Projected, 6L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Sinks, 7L));
			Deliver(op.Outbox);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Terminal, 8L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.RetireCarry(book, op, 9L));
			ClassicAssert.AreEqual(0, KingdomLifecycleRules.CarryEscrow(op));
			ClassicAssert.AreEqual(2, op.DeliveredMud);
		}

		[Test]
		public void CarryRoadLoss_RequiresSkippedOutputProofBeforeEscrowRelease()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 1, 1);
			op.LostOnRoad = true;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			ClassicAssert.IsTrue(ProveCarryUnit(book, op, op.Sources[0]));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			SettleCarrySchedule(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 5L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], true));
			ClassicAssert.AreEqual(1, op.EscrowMud, "failed release rolls back exactly");
			op.Outputs[0].State = KingdomLifecyclePhysicalState.Skipped;
			op.OutputIndex = 1;
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Projected, 6L), "skipped output cannot strand escrow");
			op.OutputIndex = 0;
			op.Outputs[0].State = KingdomLifecyclePhysicalState.Prepared;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarryRoadAbsence(book, op,
				op.Outputs[0], new TrustedWorld()));
			ClassicAssert.IsTrue(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], true));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Projected, 6L));
			ClassicAssert.AreEqual(1, op.LostMud);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CarryConserved(op));
		}

		[Test]
		public void ExactCarry_ArbitraryWholeObjectMovesSameReferenceAndWaitsForSafety()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildExactCarry(book, 1L, "OddBlueprint", 7);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			TrustedWorld sign = ExactSignWorld(op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveExactCarrySign(book, op, sign));
			ClassicAssert.AreEqual(1L, op.ManifestRevision);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			KingdomCarrySource source = op.Sources[0];
			TrustedWorld pickup = ExactSourceWorld(source);
			MoveExactOnCallback(pickup);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveExactCarryPickup(book, op,
				source, 1, "porter-one", "zone-a", pickup));
			ClassicAssert.AreEqual("source-exact", source.ObjectId);
			ClassicAssert.AreEqual(7, source.LoadedCount);
			ClassicAssert.AreEqual(7, KingdomLifecycleRules.CarryEscrow(op));
			ClassicAssert.AreEqual(-1, source.Material, "generic cargo is not material-converted");
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			SettleCarrySchedule(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 5L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.SetExactCarryDestinationSafety(
				book, op, true, 6L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveExactCarryDestination(
				book, op, source, op.Outputs[0], false, KingdomLifecycleTopology.Inventory,
				"destination-store", "zone-b", -1, -1, ExactSourceWorld(source)));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.SetExactCarryDestinationSafety(
				book, op, false, 7L));
			TrustedWorld destination = ExactSourceWorld(source);
			MoveExactOnCallback(destination);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveExactCarryDestination(
				book, op, source, op.Outputs[0], false, KingdomLifecycleTopology.Inventory,
				"destination-store", "zone-b", -1, -1, destination));
			ClassicAssert.AreEqual(7, source.DeliveredCount);
			ClassicAssert.AreEqual(0, KingdomLifecycleRules.CarryEscrow(op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Projected, 8L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Sinks, 9L));
			DeliverCarrySinks(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Terminal, 10L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.RetireCarry(book, op, 11L));
		}

		[Test]
		public void ExactCarry_CallbackCutsRecoverOnlyFrozenBeforeOrAfterTopology()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildExactCarry(book, 1L, "MixedStack", 4);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			TrustedWorld sign = ExactSignWorld(op);
			sign.CarrySignRemovalCallback = delegate(object reference, int count, string receipt)
			{
				sign.Rows.Clear();
				return null;
			};
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveExactCarrySign(book, op, sign));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Intent, op.SignReceiptState);
			book = RoundTrip(book); op = book.Open;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveExactCarrySign(book, op,
				new TrustedWorld()));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));

			KingdomCarrySource source = op.Sources[0];
			TrustedWorld pickup = ExactSourceWorld(source);
			pickup.CarryMoveCallback = delegate(object reference, int trip,
				KingdomLifecycleTopology topology, string owner, string zone,
				int x, int y, string receipt)
			{
				MoveObservation(pickup.Rows[0], topology, owner, zone, x, y);
				return null;
			};
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveExactCarryPickup(book, op,
				source, 1, "porter-one", "zone-a", pickup));
			book = RoundTrip(book); op = book.Open; source = op.Sources[0];
			TrustedWorld picked = ExactSourceWorld(source);
			MoveObservation(picked.Rows[0], source.PendingTopology, source.PendingOwnerId,
				source.PendingZoneId, source.PendingX, source.PendingY);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveExactCarryPickup(book, op,
				source, 1, "porter-one", "zone-a", picked));
			ClassicAssert.AreEqual(2L, op.ManifestRevision);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book));
		}

		[Test]
		public void ExactCarry_RefusesPartialDuplicateAndUnfrozenTripAuthority()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = KingdomLifecycleRules.PrepareExactCarry(book, 1L);
			ClassicAssert.IsNull(KingdomLifecycleRules.PrepareExactCarrySource(op, 0, "bad", "Item",
				KingdomLifecycleTopology.Inventory, "box", "zone-a", -1, -1, 0));
			KingdomCarryOperation partial = BuildExactCarry(book, 1L, "Item", 2);
			partial.Sources[0].PlannedCount = 1;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublishCarry(book, partial));

			KingdomCarryBook tripBook = CarryBook();
			KingdomCarryOperation trip = BuildExactCarry(tripBook, 1L, "Item", 2);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(tripBook, trip));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveExactCarrySign(tripBook,
				trip, ExactSignWorld(trip)));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(tripBook, trip,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveExactCarryPickup(tripBook,
				trip, trip.Sources[0], 2, "porter-two", "zone-a",
				ExactSourceWorld(trip.Sources[0])));
		}

		[Test]
		public void UndefinedOptionAndClockRegression_FailClosedWithoutRewrite()
		{
			KingdomLifecycleOptionState raw = (KingdomLifecycleOptionState)255;
			KingdomLifecycleOptionDecision invalid = KingdomLifecycleRules.ObserveOption(raw,
				10L, true, 11L, false);
			ClassicAssert.IsFalse(invalid.Valid);
			ClassicAssert.AreEqual(KingdomLifecycleOptionAction.Quarantine, invalid.Action);
			ClassicAssert.AreEqual(raw, invalid.State);
			ClassicAssert.IsFalse(KingdomLifecycleRules.ObserveOption(KingdomLifecycleOptionState.Enabled,
				10L, true, 9L, false).Valid);

			KingdomLifecycleBook book = Book();
			book.RaidOption = raw;
			KingdomLifecycleRules.Normalize(book);
			ClassicAssert.IsTrue(book.Quarantined);
			ClassicAssert.AreEqual(raw, book.RaidOption);
		}

		[Test]
		public void EnableRestamp_HasNoBacklogAndElapsedGateIsAtomic()
		{
			KingdomLifecycleOptionDecision enabled = KingdomLifecycleRules.ObserveOption(
				KingdomLifecycleOptionState.Disabled, 10L, true, 100L, false);
			ClassicAssert.IsTrue(enabled.Valid);
			ClassicAssert.AreEqual(KingdomLifecycleOptionAction.EnableAndRestamp, enabled.Action);
			ClassicAssert.AreEqual(100L, enabled.Tick);
			ClassicAssert.IsFalse(enabled.AllowNewWork);

			KingdomLifecycleOptionDecision steady = KingdomLifecycleRules.ObserveOption(
				KingdomLifecycleOptionState.Enabled, 100L, true, 101L, false);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanStartAfterOption(steady, 109L, 10L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanStartAfterOption(steady, 110L, 10L));
			KingdomLifecycleOptionDecision open = KingdomLifecycleRules.ObserveOption(
				KingdomLifecycleOptionState.Enabled, 100L, false, 110L, true);
			ClassicAssert.IsFalse(open.AllowNewWork);
			ClassicAssert.IsTrue(open.ReconcileOpenWork, "disable gates only new work");
		}

		[Test]
		public void GuestPassages_AllowBothLanesAndRetireAbsenceExactlyOnce()
		{
			foreach (KingdomLifecycleLane lane in new[]
			{
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleLane.NotableGuest
			})
			{
				KingdomLifecycleBook book = Book("city-passages-" + (byte)lane);
				KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book, lane,
					KingdomLifecycleAction.Passages, 100L);
				ClassicAssert.NotNull(op);
				op.Count = 4;
				op.DepartTick = 90L;
				op.Target = 1;
				op.ArrivalText = "95";
				ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.PrepareSchedule(book, op,
					"zone-a", 10L, 110L));
				op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, "dated absence", "ledger",
					null, null, lane == KingdomLifecycleLane.NotableGuest ? "guestbook" : null);
				ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
				KingdomLifecycleBook resumed = RoundTrip(book);
				KingdomLifecycleOperation live = lane == KingdomLifecycleLane.PlainGuest
					? resumed.PlainGuest : resumed.NotableGuest;
				ClassicAssert.AreEqual(4, live.Count);
				Settle(resumed, live, 101L);
				ClassicAssert.IsTrue(KingdomLifecycleRules.Retire(resumed, live, 200L));
				ClassicAssert.IsFalse(KingdomLifecycleRules.Retire(resumed, live, 201L));
			}
		}

		[Test]
		public void CausalPilgrimPlan_FreezesEveryCauseFieldAcrossReloadAndMalformedQuarantine()
		{
			KingdomLifecycleBook book = Book("city-causal");
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Spawn, 77L);
			op.ObjectName = "Nara-of-the-Third-Telling";
			op.Origin = "the road that heard the bronze gate open";
			op.Detail = "the bronze gate opened after three refusals";
			op.ArrivalText = "Rite Ground of Glass Reeds";
			op.Kind = 19;
			op.Creed = "causal-pilgrim";
			op.DepartTick = 177L;
			ClassicAssert.NotNull(KingdomLifecycleRules.GuestRuntimeAdapter.PrepareProjection(book, op,
				KingdomLifecycleRules.ChildId(op.Id, "guest", 0), "r_KingdomGuestPilgrim",
				"zone-a", 3, 4));
			ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.PrepareDomain(book, op, 0L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.PrepareSchedule(book, op,
				"zone-a", 0L, 177L));
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, null, null, null, null, null);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));

			KingdomLifecycleBook resumed = RoundTrip(book);
			KingdomLifecycleOperation exact = resumed.PlainGuest;
			ClassicAssert.AreEqual("Nara-of-the-Third-Telling", exact.ObjectName);
			ClassicAssert.AreEqual("the road that heard the bronze gate open", exact.Origin);
			ClassicAssert.AreEqual("the bronze gate opened after three refusals", exact.Detail);
			ClassicAssert.AreEqual("Rite Ground of Glass Reeds", exact.ArrivalText);
			ClassicAssert.AreEqual(19, exact.Kind);
			ClassicAssert.AreEqual(177L, exact.DepartTick);
			ClassicAssert.AreEqual("causal-pilgrim", exact.Creed);

			exact.Detail = "rewritten shared scalar";
			KingdomLifecycleBook malformed = RoundTrip(resumed);
			ClassicAssert.IsTrue(malformed.Quarantined);
			ClassicAssert.NotNull(malformed.PlainGuest);
			ClassicAssert.AreEqual("rewritten shared scalar", malformed.PlainGuest.Detail,
				"quarantine retains hostile evidence instead of clearing the causal carrier");
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(malformed));
		}

		[Test]
		public void GuestPhysicalCuts_RecoverProjectionRemovalWaterDomainAndSchedule()
		{
			KingdomLifecycleBook spawnBook = Book("city-cut-spawn");
			KingdomLifecycleOperation spawn = Build(spawnBook, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 0L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(spawnBook, spawn));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(spawnBook, spawn,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			TrustedWorld projectedThenInterrupted = LifecycleProjectionWorld(spawn.Projections[0]);
			projectedThenInterrupted.LifecycleProjectionCallback = delegate(
				KingdomLifecycleProjection value)
			{
				object reference = new object();
				projectedThenInterrupted.Rows.Add(OutputObservation(value, reference));
				return null;
			};
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleProjection(
				spawnBook, spawn, spawn.Projections[0], projectedThenInterrupted));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Intent, spawn.Projections[0].State);
			ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.RecoverProjectionIntent(
				spawnBook, spawn, spawn.Projections[0], true, false));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(spawnBook, spawn,
				KingdomLifecyclePhase.Projected, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(spawnBook, spawn,
				KingdomLifecyclePhase.DomainIntent, 4L));
			KingdomLifecycleResourceLease spawnDomain = spawn.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Population);
			ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.BeginDomain(spawnBook,
				spawn, spawnDomain.Before));
			spawnBook = RoundTrip(spawnBook);
			spawn = spawnBook.PlainGuest;
			ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.ProvePhysicalDomain(
				spawnBook, spawn), "domain intent resumes only because projection proof survived");

			KingdomLifecycleBook removalBook = Book("city-cut-removal");
			KingdomLifecycleOperation removal = Build(removalBook,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Depart, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(removalBook, removal));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(removalBook, removal,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			TrustedWorld removedThenInterrupted = LifecycleRemovalWorld(removal);
			removedThenInterrupted.LifecycleRemovalCallback = delegate(object reference,
				int count, string operationId)
			{
				removedThenInterrupted.Rows[0].CountValue = 0;
				return null;
			};
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleRemoval(
				removalBook, removal, removedThenInterrupted));
			ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.RecoverRemovalIntent(
				removalBook, removal, true));

			KingdomLifecycleBook waterBook = Book("city-cut-water");
			KingdomLifecycleOperation water = Build(waterBook,
				KingdomLifecycleLane.NotableGuest, KingdomLifecycleAction.Lodge, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(waterBook, water));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(waterBook, water,
				KingdomLifecyclePhase.WaterIntent, 2L));
			KingdomLifecycleWaterLeg leg = water.WaterLegs[0];
			KingdomLifecycleResourceLease waterLease = water.ResourceLeases.Find(l =>
				l.Key == leg.LeaseKey);
			TrustedWorld drainedThenInterrupted = WaterWorld(leg);
			drainedThenInterrupted.WaterCallback = delegate(object reference, int amount)
			{
				drainedThenInterrupted.Rows[0].ValueValue = leg.After;
				return null;
			};
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveWater(waterBook,
				waterLease, leg, drainedThenInterrupted));
			ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.RecoverWaterIntent(
				waterBook, water, waterLease, leg, leg.After));

			KingdomLifecycleBook scheduleBook = Book("city-cut-schedule");
			KingdomLifecycleOperation schedule = Build(scheduleBook,
				KingdomLifecycleLane.NotableGuest, KingdomLifecycleAction.Passages, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(scheduleBook, schedule));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(scheduleBook, schedule,
				KingdomLifecyclePhase.Sinks, 2L));
			Deliver(schedule.Outbox);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(scheduleBook, schedule,
				KingdomLifecyclePhase.ScheduleIntent, 3L));
			TrustedWorld scheduledThenInterrupted = LifecycleScheduleWorld(scheduleBook, schedule);
			scheduledThenInterrupted.ScheduleCallback = delegate(object reference, long after,
				string operationId)
			{
				scheduledThenInterrupted.Rows[0].ValueValue = after;
				scheduledThenInterrupted.Rows[0].RevisionValue++;
				scheduledThenInterrupted.Rows[0].LastOperationIdValue = operationId;
				return null;
			};
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(
				scheduleBook, schedule, scheduledThenInterrupted));
			ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.RecoverScheduleIntent(
				scheduleBook, schedule, schedule.DueAfter));
		}

		[Test]
		public void GuestSeatSwap_CannotResumeEqualOperationUnderAnotherSettlement()
		{
			KingdomLifecycleBook seated = Book("city-seat-a");
			KingdomLifecycleOperation op = Build(seated, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 0L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(seated, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(seated, op,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			SettleProjectionLease(seated, op, op.Projections[0]);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(seated, op,
				KingdomLifecyclePhase.Projected, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(seated, op,
				KingdomLifecyclePhase.DomainIntent, 4L));
			KingdomLifecycleBook otherSeat = Book("city-seat-b");
			ClassicAssert.IsFalse(KingdomLifecycleRules.GuestRuntimeAdapter.ProvePhysicalDomain(
				otherSeat, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.GuestRuntimeAdapter.ProvePhysicalDomain(
				seated, op));
		}

		[Test]
		public void LegendaryTraderLodge_FreezesFineHouseAndShopFunctionAcrossAbsenceReload()
		{
			KingdomLifecycleBook book = Book("city-legendary-lodge");
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 50L, 80L);
			op.ObjectId = "legendary-trader-body";
			op.ObjectMarker = "exact-vacant-fine-house-root";
			op.Blueprint = "r_KingdomNotableGuestTrader";
			op.ObjectName = "Issachar, Merchant of Seven Roads";
			op.Origin = "the salt road";
			op.Faction = "12 of Nivvun Ut, 1002 AR";
			op.DisplayFaction = "finehouse";
			op.Creed = "Dromad merchants";
			op.Kind = (int)KingdomGuestRules.HookKind.Machine;
			op.Target = 1;
			op.Count = 2;
			op.Defence = 9;
			op.PlunderRequested = 5;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.WaterIntent, 51L));

			KingdomLifecycleBook resumed = RoundTrip(book);
			KingdomLifecycleOperation exact = resumed.NotableGuest;
			ClassicAssert.AreEqual("exact-vacant-fine-house-root", exact.ObjectMarker);
			ClassicAssert.AreEqual("finehouse", exact.DisplayFaction);
			ClassicAssert.AreEqual(5, exact.PlunderRequested, "promised resident shop tier is frozen");
			ClassicAssert.AreEqual("legendary-trader-body", exact.ObjectId);
			ClassicAssert.AreEqual("Issachar, Merchant of Seven Roads", exact.ObjectName);
			ClassicAssert.AreEqual(KingdomLifecyclePhase.WaterIntent, exact.Phase,
				"absence and reload resume the open exact lodging transaction");
			Settle(resumed, exact, 60L);
			ClassicAssert.AreEqual(KingdomLifecyclePhase.Terminal, exact.Phase);
		}

		[Test]
		public void LodgeDeadResident_RequiresExactFrozenRowAndSurvivesEveryReleaseCut()
		{
			KingdomLifecycleBook book = Book("city-lodge-dead-row");
			KingdomLifecycleOperation op = ReadyLodgeDomain(book);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeResident(book, op, 17,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryBeginLodgeAbandon(book, op, 0, 17,
				null, null, null, 0L, null, 0, 0, 5L), "absence is not death proof");
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryBeginLodgeAbandon(book, op, 2, 17,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId, 2, 2, 5L),
				"duplicate resident coordinates are ambiguous");
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryBeginLodgeAbandon(book, op, 1, 17,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId, 0, 0, 5L),
				"a live row cannot be reinterpreted as Dead");
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryBeginLodgeAbandon(book, op, 1, 17,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId, 2, 2, 5L));

			book = RoundTrip(book); op = book.NotableGuest;
			ClassicAssert.AreEqual(KingdomLifecycleMutationAction.InvokeOnce,
				KingdomLifecycleRules.LodgeAbandonScheduleAction(book, op, op.DueBefore));
			ClassicAssert.IsTrue(KingdomLifecycleRules.BeginLodgeAbandonSchedule(book, op, op.DueBefore));
			book = RoundTrip(book); op = book.NotableGuest;
			ClassicAssert.AreEqual(KingdomLifecycleMutationAction.InvokeOnce,
				KingdomLifecycleRules.LodgeAbandonScheduleAction(book, op, op.DueBefore),
				"an Intent crash before the scalar mutation retries once");
			ClassicAssert.IsTrue(KingdomLifecycleRules.BeginLodgeAbandonSchedule(book, op, op.DueBefore));
			ClassicAssert.AreEqual(KingdomLifecycleMutationAction.ConfirmAfter,
				KingdomLifecycleRules.LodgeAbandonScheduleAction(book, op, op.DueAfter));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CommitLodgeAbandonSchedule(book, op, op.DueAfter));
			book = RoundTrip(book); op = book.NotableGuest;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryCommitLodgeAbandon(book, op, 6L));
			AssertAbandonedLodgeHasNoSuccessOrRefund(op);

			book = RoundTrip(book); op = book.NotableGuest;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryReleaseAbandonedLodge(book, op, 7L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.ExactLodgeRetirementProof(book,
				op.Id, op.PlanHash));
			ClassicAssert.IsTrue(op.ResourceLeases.TrueForAll(l => book.Resources.Find(r =>
				r.Key == l.Key).ActiveOperationId == null));
			book = RoundTrip(book); op = book.NotableGuest;
			ClassicAssert.AreEqual(KingdomLifecycleLodgeTerminalState.AuthorityReleased,
				op.LodgeTerminal.State, "release-before-marker survives a save cut");
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryRemoveReleasedLodge(book, op, 8L));
			ClassicAssert.IsNull(book.NotableGuest);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(RoundTrip(book)));
		}

		[Test]
		public void LodgeMarketSource_FreezesExactReceiptAndRejectsAliasOrLateRewrite()
		{
			KingdomLifecycleBook book = Book("city-lodge-market-source");
			KingdomLifecycleOperation op = ReadyLodgeDomain(book, market: true);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeResident(book, op, 17,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeNoLodgeMarketSource(book, op));
			string intent = "market-receipt:handoff:exact-source:" + op.ObjectId;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryFreezeLodgeMarketSource(book, op,
				op.ObjectId, 18, op.PlunderRequested, intent), "target cannot alias source");
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryFreezeLodgeMarketSource(book, op,
				"exact-source", 17, op.PlunderRequested, intent), "resident cannot alias target row");
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryFreezeLodgeMarketSource(book, op,
				"exact-source", 18, op.PlunderRequested + 1, intent), "tier is frozen by plan");
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeMarketSource(book, op,
				"exact-source", 18, op.PlunderRequested, intent));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeMarketSource(book, op,
				"exact-source", 18, op.PlunderRequested, intent), "exact replay is idempotent");
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryFreezeLodgeMarketSource(book, op,
				"other-source", 18, op.PlunderRequested, intent), "prepared identity is immutable");

			book = RoundTrip(book); op = book.NotableGuest;
			ClassicAssert.AreEqual(1, op.LodgeTerminal.MarketSourcePrepared);
			ClassicAssert.AreEqual("exact-source", op.LodgeTerminal.MarketSourceBodyObjectId);
			ClassicAssert.AreEqual(18, op.LodgeTerminal.MarketSourceResidentId);
			ClassicAssert.IsNotEmpty(op.LodgeTerminal.MarketSourceProofId);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryCommitLodgeMarketSource(book, op,
				"exact-source", 18, op.PlunderRequested, intent, false));
			ClassicAssert.AreEqual(KingdomLifecycleLodgeTerminalReceipt.MarketCommitted,
				op.LodgeTerminal.MarketSourcePrepared);
			book = RoundTrip(book); op = book.NotableGuest;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryCommitLodgeMarketSource(book, op,
				"exact-source", 18, op.PlunderRequested, intent, false),
				"committed checkpoint replay is idempotent");
			foreach (int forged in new[] { 0, 1, 3 })
			{
				KingdomLifecycleBook tampered = RoundTrip(book);
				tampered.NotableGuest.LodgeTerminal.MarketSourcePrepared = forged;
				ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(tampered),
					"market outcome phase " + forged + " cannot reuse committed proof");
			}
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryBeginLodgeAbandon(book, op, 1, 17,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId, 2, 2, 5L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryFreezeLodgeMarketSource(book, op,
				"other-source", 19, op.PlunderRequested, intent), "death proof seals source receipt");
		}

		[Test]
		public void LodgeMarketSource_DeadSourceOutcomeIsDistinctAndRoundTrips()
		{
			KingdomLifecycleBook book = Book("city-lodge-market-source-dead");
			KingdomLifecycleOperation op = ReadyLodgeDomain(book, market: true);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeResident(book, op, 17,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId));
			string intent = "market-receipt:handoff:dead-source:" + op.ObjectId;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeMarketSource(book, op,
				"dead-source", 18, op.PlunderRequested, intent));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryCommitLodgeMarketSource(book, op,
				"dead-source", 18, op.PlunderRequested, intent, true));
			book = RoundTrip(book); op = book.NotableGuest;
			ClassicAssert.AreEqual(KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead,
				op.LodgeTerminal.MarketSourcePrepared);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryCommitLodgeMarketSource(book, op,
				"dead-source", 18, op.PlunderRequested, intent, true));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryCommitLodgeMarketSource(book, op,
				"dead-source", 18, op.PlunderRequested, intent, false));
			foreach (int forged in new[] { 0, 1, 2 })
			{
				KingdomLifecycleBook tampered = RoundTrip(book);
				tampered.NotableGuest.LodgeTerminal.MarketSourcePrepared = forged;
				ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(tampered));
			}
		}

		[Test]
		public void LodgeBodyDeathCallback_IsOnlyPreEnrollmentTerminalEvidence()
		{
			KingdomLifecycleBook book = Book("city-lodge-body-death");
			KingdomLifecycleOperation op = ReadyLodgeDomain(book);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryBeginLodgeAbandon(book, op, 0, 0,
				null, null, null, 0L, null, 0, 0, 5L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryObserveLodgeBodyDeath(book, op,
				"wrong-body", op.Blueprint, op.ZoneId, 5L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryObserveLodgeBodyDeath(book, op,
				op.ObjectId, "wrong-blueprint", op.ZoneId, 5L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryObserveLodgeBodyDeath(book, op,
				op.ObjectId, op.Blueprint, op.ZoneId, 5L));
			book = RoundTrip(book); op = book.NotableGuest;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryBeginLodgeAbandon(book, op, 0, 0,
				null, null, null, 0L, null, 0, 0, 6L));
			SettleLodgeAbandonSchedule(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryCommitLodgeAbandon(book, op, 7L));
			KingdomLifecycleResourceLease roster = op.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Roster);
			ClassicAssert.AreEqual(KingdomLifecycleLeaseState.Skipped, roster.State);
			AssertAbandonedLodgeHasNoSuccessOrRefund(op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.Retire(book, op, 8L));
			ClassicAssert.IsNull(book.NotableGuest);
		}

		[Test]
		public void LodgeReleasedAuthority_PinsExactRetirementProofUntilMarkerAck()
		{
			KingdomLifecycleBook book = Book("city-lodge-proof-pin");
			KingdomLifecycleOperation lodge = ReadyLodgeDomain(book);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryObserveLodgeBodyDeath(book, lodge,
				lodge.ObjectId, lodge.Blueprint, lodge.ZoneId, 5L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryBeginLodgeAbandon(book, lodge, 0, 0,
				null, null, null, 0L, null, 0, 0, 6L));
			SettleLodgeAbandonSchedule(book, lodge);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryCommitLodgeAbandon(book, lodge, 7L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryReleaseAbandonedLodge(book, lodge, 8L));

			for (int i = 0; i < KingdomLifecycleRules.MaxRecentProofs + 8; i++)
			{
				long tick = 100L + i * 10L;
				KingdomLifecycleOperation passage = Build(book,
					KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Passages, tick, i);
				ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, passage), "publish " + i);
				Settle(book, passage, tick + 1L);
				ClassicAssert.IsTrue(KingdomLifecycleRules.Retire(book, passage, tick + 8L), "retire " + i);
			}
			ClassicAssert.AreEqual(KingdomLifecycleRules.MaxRecentProofs, book.RecentProofs.Count);
			ClassicAssert.IsTrue(KingdomLifecycleRules.ExactLodgeRetirementProof(book,
				lodge.Id, lodge.PlanHash));
			book = RoundTrip(book); lodge = book.NotableGuest;
			ClassicAssert.IsFalse(book.Quarantined);
			ClassicAssert.IsTrue(KingdomLifecycleRules.ExactLodgeRetirementProof(book,
				lodge.Id, lodge.PlanHash));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryRemoveReleasedLodge(book, lodge, 2000L));
		}

		[Test]
		public void LodgeDeadRow_AfterCommittedEnrollmentDoesNotApplyAnotherRosterDelta()
		{
			KingdomLifecycleBook book = Book("city-lodge-proved-row");
			KingdomLifecycleOperation op = ReadyLodgeDomain(book);
			KingdomLifecycleResourceLease roster = op.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Roster);
			SettleLease(book, roster);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeResident(book, op, 19,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId));
			long revision = book.Resources.Find(r => r.Key == roster.Key).Revision;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryBeginLodgeAbandon(book, op, 1, 19,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId, 2, 4, 5L));
			SettleLodgeAbandonSchedule(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryCommitLodgeAbandon(book, op, 6L));
			ClassicAssert.AreEqual(KingdomLifecycleLeaseState.Proved, roster.State);
			ClassicAssert.AreEqual(revision, book.Resources.Find(r => r.Key == roster.Key).Revision,
				"terminal recovery neither reapplies nor rolls back enrollment");
		}

		[Test]
		public void LodgeTerminalReceipt_V10RoundTripsV9AndV8RejectsTampering()
		{
			KingdomLifecycleBook legacy = Book("city-lodge-v8");
			ReadyLodgeDomain(legacy);
			byte[] v8;
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV8Fixture(new BinaryWriter(stream), legacy);
				v8 = stream.ToArray();
			}
			KingdomLifecycleBook loaded = new KingdomLifecycleBook();
			using (MemoryStream stream = new MemoryStream(v8, false))
				KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(stream), loaded);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, loaded.FormatVersion);
			ClassicAssert.IsFalse(loaded.Quarantined);
			ClassicAssert.IsNull(loaded.NotableGuest.LodgeTerminal);

			KingdomLifecycleBook v9Book = Book("city-lodge-v9");
			KingdomLifecycleOperation v9Op = ReadyLodgeDomain(v9Book);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeResident(v9Book, v9Op, 21,
				v9Op.ObjectName, v9Op.Origin, v9Op.Faction, 1L, v9Op.ZoneId));
			byte[] v9;
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV9Fixture(new BinaryWriter(stream), v9Book);
				v9 = stream.ToArray();
			}
			KingdomLifecycleBook v9Loaded = new KingdomLifecycleBook();
			using (MemoryStream stream = new MemoryStream(v9, false))
				KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(stream), v9Loaded);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, v9Loaded.FormatVersion);
			ClassicAssert.AreEqual(0, v9Loaded.NotableGuest.LodgeTerminal.MarketSourcePrepared);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(v9Loaded));
			KingdomLifecycleBook v9DeathBook = Book("city-lodge-v9-death");
			KingdomLifecycleOperation v9Death = ReadyLodgeDomain(v9DeathBook);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryObserveLodgeBodyDeath(v9DeathBook, v9Death,
				v9Death.ObjectId, v9Death.Blueprint, v9Death.ZoneId, 5L));
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV9Fixture(new BinaryWriter(stream), v9DeathBook);
				KingdomLifecycleBook upgraded = new KingdomLifecycleBook();
				using (MemoryStream input = new MemoryStream(stream.ToArray(), false))
					KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(input), upgraded);
				ClassicAssert.AreEqual(KingdomLifecycleLodgeTerminalState.BodyDeathProved,
					upgraded.NotableGuest.LodgeTerminal.State);
				ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(upgraded));
			}

			KingdomLifecycleBook current = Book("city-lodge-v9");
			KingdomLifecycleOperation op = ReadyLodgeDomain(current, market: true);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeResident(current, op, 23,
				op.ObjectName, op.Origin, op.Faction, 1L, op.ZoneId));
			using (MemoryStream stream = new MemoryStream())
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec
					.WriteLifecycleV8Fixture(new BinaryWriter(stream), current));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeLodgeMarketSource(current, op,
				"v10-market-source", 24, op.PlunderRequested,
				"market-receipt:handoff:v10-market-source:" + op.ObjectId));
			using (MemoryStream stream = new MemoryStream())
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec
					.WriteLifecycleV9Fixture(new BinaryWriter(stream), current));
			current = RoundTrip(current); op = current.NotableGuest;
			ClassicAssert.AreEqual(23, op.LodgeTerminal.ResidentId);
			op.LodgeTerminal.MarketSourceProofId = "sha256:forged";
			KingdomLifecycleBook poisoned = RoundTrip(current);
			ClassicAssert.IsTrue(poisoned.Quarantined);
			ClassicAssert.IsTrue(poisoned.NotableGuest != null
				&& poisoned.NotableGuest.Phase == KingdomLifecyclePhase.Quarantined);
		}

		[Test]
		public void ChronicleIntentRetriesByReceipt_MessageIntentBecomesLost()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Sinks, 2L));
			string receipt = op.Outbox.ChronicleReceiptId;
			op.Outbox.ChronicleState = KingdomLifecycleSinkState.Intent;
			op.Outbox.MessageState = KingdomLifecycleSinkState.Intent;
			ClassicAssert.IsTrue(KingdomLifecycleRules.RecoverOutbox(book, op));
			ClassicAssert.AreEqual(receipt, op.Outbox.ChronicleReceiptId);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, op.Outbox.ChronicleState);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Lost, op.Outbox.MessageState);
		}

		[Test]
		public void RequiredSinkTextAndChronicleReceipt_ArePlanAuthority()
		{
			KingdomLifecycleBook missing = Book();
			KingdomLifecycleOperation op = Build(missing, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			op.Outbox.Message = null;
			op.Outbox.MessageState = KingdomLifecycleSinkState.Skipped;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(missing, op));
			ClassicAssert.AreEqual(1L, missing.RaidNextSequence);

			KingdomLifecycleBook forged = Book();
			op = Build(forged, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			op.Outbox.ChronicleReceiptId = KingdomLifecycleRules.ChildId(op.Id, "message", 0);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(forged, op));

			KingdomLifecycleBook frozen = Book();
			op = Build(frozen, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			op.Outbox.DeedDisposition = KingdomLifecycleSinkDisposition.Skip;
			op.Outbox.DeedState = KingdomLifecycleSinkState.Skipped;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(frozen, op),
				"optional content disposition is frozen before mutation");
			op.Outbox.DeedDisposition = KingdomLifecycleSinkDisposition.Deliver;
			KingdomLifecycleRules.Normalize(frozen);
			ClassicAssert.IsTrue(frozen.Quarantined, "a later disposition rewrite changes plan authority");
		}

		[Test]
		public void BoundedCodec_RoundTripsAndRejectsFutureOrOversizedBeforeAllocation()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			KingdomLifecycleBook copy = RoundTrip(book);
			ClassicAssert.IsFalse(copy.WireRejected);
			ClassicAssert.AreEqual(op.Id, copy.Raid.Id);

			using (MemoryStream futureBytes = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(futureBytes, System.Text.Encoding.UTF8, true))
				{
					writer.Write(KingdomLifecycleWireCodec.LifecycleMagic);
					writer.Write(KingdomLifecycleRules.CurrentFormatVersion + 1);
				}
				futureBytes.Position = 0;
				KingdomLifecycleBook future = new KingdomLifecycleBook();
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec.ReadLifecycle(
					new BinaryReader(futureBytes), future));
				ClassicAssert.IsTrue(future.WireRejected);
				ClassicAssert.IsTrue(future.Quarantined);
			}

			using (MemoryStream malicious = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(malicious, System.Text.Encoding.UTF8, true))
					writer.Write(int.MaxValue);
				malicious.Position = 0;
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec.ReadString(
					new BinaryReader(malicious), KingdomLifecycleRules.MaxTextBytes));
			}

			byte[] noncanonical;
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteLifecycle(writer, Book("city-wire"));
				noncanonical = stream.ToArray();
			}
			noncanonical[8] = 2; // first boolean follows magic and version
			KingdomLifecycleBook poisoned = Book("still-live-before-read");
			using (MemoryStream stream = new MemoryStream(noncanonical))
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec.ReadLifecycle(
					new BinaryReader(stream), poisoned));
			ClassicAssert.IsTrue(poisoned.WireRejected);
			ClassicAssert.IsTrue(poisoned.Quarantined);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(poisoned));
		}

		[Test]
		public void LifecycleV6OpenRaidColdLoadRetainsRawPlanButOwnsNoAuthority()
		{
			KingdomLifecycleBook source = Book("city-v6-open-raid");
			KingdomLifecycleOperation warning = Build(source, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 10L, 0L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(source, warning));
			byte[] v6;
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV6Fixture(new BinaryWriter(stream), source);
				v6 = stream.ToArray();
			}
			ClassicAssert.AreEqual(KingdomLifecycleRules.PreviousLifecycleFormatVersion,
				BitConverter.ToInt32(v6, 4));
			KingdomLifecycleBook loaded = new KingdomLifecycleBook();
			using (MemoryStream stream = new MemoryStream(v6, false))
				KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(stream), loaded);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, loaded.FormatVersion);
			ClassicAssert.IsTrue(loaded.Quarantined);
			StringAssert.Contains("legacy raid authority", loaded.Fault);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(loaded));
			ClassicAssert.NotNull(loaded.Raid);
			ClassicAssert.AreEqual(warning.Id, loaded.Raid.Id);
			ClassicAssert.AreEqual(warning.Origin, loaded.Raid.Origin);
			ClassicAssert.AreEqual(warning.Detail, loaded.Raid.Detail);
			ClassicAssert.IsTrue(KingdomRaidIncidentRules.ValidLedger(loaded.RaidLedger));
			ClassicAssert.AreEqual(0, loaded.RaidLedger.Grievances.Count);
			ClassicAssert.AreEqual(0, loaded.RaidLedger.Incidents.Count);
		}

		[Test]
		public void LifecycleV6WireRejectsActionsAppendedByV7()
		{
			KingdomLifecycleBook source = Book("city-v6-appended-action");
			KingdomLifecycleOperation fight = Build(source, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidFight, 10L, 0L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(source, fight));
			using (MemoryStream fixture = new MemoryStream())
				Assert.Throws<InvalidDataException>(() =>
					KingdomLifecycleWireCodec.WriteLifecycleV6Fixture(
						new BinaryWriter(fixture), source));

			byte[] hostile;
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycle(new BinaryWriter(stream), source);
				hostile = stream.ToArray();
			}
			Buffer.BlockCopy(BitConverter.GetBytes(
				KingdomLifecycleRules.PreviousLifecycleFormatVersion), 0, hostile, 4, 4);
			KingdomLifecycleBook refused = new KingdomLifecycleBook();
			using (MemoryStream stream = new MemoryStream(hostile, false))
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec.ReadLifecycle(
					new BinaryReader(stream), refused));
			ClassicAssert.IsTrue(refused.WireRejected);
			ClassicAssert.IsTrue(refused.Quarantined);
		}

		[Test]
		public void RaidProjectionIntentRetriesOnlyAfterExactAbsenceProof()
		{
			KingdomLifecycleBook book = Book("city-raid-projection-retry");
			KingdomLifecycleOperation attack = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidAttack, 10L, 0L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, attack));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, attack,
				KingdomLifecyclePhase.ProjectionIntent, 11L));
			KingdomLifecycleProjection projection = attack.Projections[0];
			ClassicAssert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.BeginProjection(
				book, attack, projection, 0, 0));
			ClassicAssert.IsFalse(KingdomLifecycleRules.RaidRuntimeAdapter.ResetAbsentProjectionIntent(
				book, attack, projection, 1, 0));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Intent, projection.State);
			ClassicAssert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.ResetAbsentProjectionIntent(
				book, attack, projection, 0, 0));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Prepared, projection.State);
			KingdomLifecycleResourceLease lease = attack.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Projection);
			ClassicAssert.AreEqual(KingdomLifecycleLeaseState.Prepared, lease.State);
			ClassicAssert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.BeginProjection(
				book, attack, projection, 0, 0));
		}

		[Test]
		public void OverCapAuthority_IsNotTruncatedIntoWritableCommand()
		{
			KingdomLifecycleBook book = Book();
			for (int i = 0; i <= KingdomLifecycleRules.MaxResourceRows; i++)
				book.Resources.Add(new KingdomLifecycleResourceRevision());
			KingdomLifecycleRules.Normalize(book);
			ClassicAssert.IsTrue(book.Quarantined);
			ClassicAssert.AreEqual(KingdomLifecycleRules.MaxResourceRows + 1, book.Resources.Count);
			using (MemoryStream stream = new MemoryStream())
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec.WriteLifecycle(
					new BinaryWriter(stream), book));
		}

		[Test]
		public void BoundedResourceRegistry_RefusesNewWorkWithoutEvictingOldRows()
		{
			KingdomLifecycleBook book = Book();
			for (int i = 0; i < KingdomLifecycleRules.MaxResourceRows; i++)
			{
				string subject = "subject-" + i;
				book.Resources.Add(new KingdomLifecycleResourceRevision
				{
					Kind = KingdomLifecycleResourceKind.Standing,
					ScopeId = book.SettlementId,
					SubjectId = subject,
					Key = KingdomLifecycleRules.ResourceKey(KingdomLifecycleResourceKind.Standing,
						book.SettlementId, subject)
				});
			}
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.AreEqual(KingdomLifecycleRules.MaxResourceRows, book.Resources.Count);
			ClassicAssert.AreEqual(1L, book.PlainGuestNextSequence);
			ClassicAssert.IsNull(book.PlainGuest);
		}

		[Test]
		public void DuplicateRegistryAndCounterGap_OwnNoRuntimeAuthority()
		{
			KingdomLifecycleBook duplicate = Book();
			KingdomLifecycleResourceRevision row = new KingdomLifecycleResourceRevision
			{
				Kind = KingdomLifecycleResourceKind.Standing,
				ScopeId = duplicate.SettlementId,
				SubjectId = "faction-a",
				Key = KingdomLifecycleRules.ResourceKey(KingdomLifecycleResourceKind.Standing,
					duplicate.SettlementId, "faction-a")
			};
			duplicate.Resources.Add(row);
			duplicate.Resources.Add(row);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(duplicate));
			ClassicAssert.IsNull(KingdomLifecycleRules.PrepareOperation(duplicate,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Passages, 1L));

			KingdomLifecycleBook replay = Book("city-replay-row");
			KingdomLifecycleOperation replayOp = Build(replay, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			KingdomLifecycleResourceLease replayLease = replayOp.ResourceLeases[0];
			replay.Resources.Add(new KingdomLifecycleResourceRevision
			{
				Kind = replayLease.Kind,
				ScopeId = replayLease.ScopeId,
				SubjectId = replayLease.SubjectId,
				Key = replayLease.Key,
				Revision = replayLease.BeforeRevision,
				LastOperationId = replayOp.Id
			});
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(replay, replayOp),
				"a row already carrying this deterministic operation id cannot replay it");

			KingdomLifecycleBook gap = Book("city-gap");
			gap.PlainGuestNextSequence = 3L;
			KingdomLifecycleRules.Normalize(gap);
			ClassicAssert.IsTrue(gap.Quarantined, "unaccounted sequence consumption is not canonical replay state");
		}

		[Test]
		public void FuturePhase_RemainsRawAndOwnsNoAuthority()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			op.Phase = (KingdomLifecyclePhase)255;
			KingdomLifecycleRules.Normalize(book);
			ClassicAssert.IsTrue(book.Quarantined);
			ClassicAssert.AreEqual((KingdomLifecyclePhase)255, op.Phase);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(book));
		}

		[Test]
		public void ExactSettlementRoot_PreventsMultiCityRedirection()
		{
			KingdomLifecycleBook a = Book("city-a");
			KingdomLifecycleBook b = Book("city-b");
			KingdomLifecycleOperation one = Build(a, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			KingdomLifecycleOperation two = Build(b, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			ClassicAssert.AreNotEqual(one.Id, two.Id);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(b, one),
				"a self-canonical foreign operation is not authority for this book");
			ClassicAssert.AreEqual(1L, b.PlainGuestNextSequence);
			ClassicAssert.IsNull(b.PlainGuest);
			two.SettlementId = a.SettlementId;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(b, two));

			KingdomLifecycleBook migration = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(migration, "legacy-city", true,
				"legacy-source", new List<string> { "city-a", "city-b" }));
			KingdomLifecycleBook collision = new KingdomLifecycleBook();
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(collision, "city-a", true,
				"legacy-source", new List<string> { "city-a", "city-b" }));
		}

		[Test]
		public void CarryOutputCallbackReceipt_RequiresGlobalUniquenessSameReferenceAndFrozenTopology()
		{
			KingdomCarryBook duplicateBook = CarryBook();
			KingdomCarryOperation duplicateOp = BuildCarry(duplicateBook, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(duplicateBook, duplicateOp));
			ReadyCarryProjection(duplicateBook, duplicateOp);
			TrustedWorld duplicate = new TrustedWorld();
			duplicate.Rows.Add(OutputObservation(duplicateOp.Outputs[0], new object()));
			duplicate.Rows.Add(OutputObservation(duplicateOp.Outputs[0], new object()));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(duplicateBook,
				duplicateOp, duplicateOp.Outputs[0], duplicate));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Prepared,
				duplicateOp.Outputs[0].ReceiptState);

			KingdomCarryBook noCallbackBook = CarryBook();
			KingdomCarryOperation noCallbackOp = BuildCarry(noCallbackBook, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(noCallbackBook, noCallbackOp));
			ReadyCarryProjection(noCallbackBook, noCallbackOp);
			TrustedWorld noCallback = new TrustedWorld();
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(noCallbackBook,
				noCallbackOp, noCallbackOp.Outputs[0], noCallback));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Intent,
				noCallbackOp.Outputs[0].ReceiptState, "missing callback cannot mint proof");

			KingdomCarryBook wrongRefBook = CarryBook();
			KingdomCarryOperation wrongRefOp = BuildCarry(wrongRefBook, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(wrongRefBook, wrongRefOp));
			ReadyCarryProjection(wrongRefBook, wrongRefOp);
			TrustedWorld wrongRef = OutputWorld(wrongRefOp.Outputs[0]);
			wrongRef.OutputReturnOverride = new object();
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(wrongRefBook,
				wrongRefOp, wrongRefOp.Outputs[0], wrongRef));

			KingdomCarryBook mutatedBook = CarryBook();
			KingdomCarryOperation mutatedOp = BuildCarry(mutatedBook, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(mutatedBook, mutatedOp));
			ReadyCarryProjection(mutatedBook, mutatedOp);
			TrustedWorld mutated = new TrustedWorld();
			mutated.OutputCallback = delegate(KingdomLifecycleProjection value)
			{
				value.ZoneId = "callback-zone";
				object reference = new object();
				mutated.Rows.Add(OutputObservation(value, reference));
				return reference;
			};
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(mutatedBook,
				mutatedOp, mutatedOp.Outputs[0], mutated), "callback cannot rewrite frozen plan");

			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			ReadyCarryProjection(book, op);
			TrustedWorld happyWorld = OutputWorld(op.Outputs[0]);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(book, op,
				op.Outputs[0], happyWorld));
			ClassicAssert.AreEqual(2, happyWorld.ObservationCountReads,
				"bounded scan snapshots observation count once before and once after callback");
			ClassicAssert.IsTrue(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], false));
		}

		[Test]
		public void CarryScheduleIntent_RequiresExactMemberRevisionCasBeforeProjection()
		{
			KingdomCarryBook noCallbackBook = CarryBook();
			KingdomCarryOperation noCallbackOp = BuildCarry(noCallbackBook, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(noCallbackBook, noCallbackOp));
			RemoveCarrySources(noCallbackBook, noCallbackOp);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(noCallbackBook, noCallbackOp,
				KingdomLifecyclePhase.Removed, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(noCallbackBook, noCallbackOp,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			TrustedWorld noCallback = ScheduleWorld(noCallbackBook, noCallbackOp,
				noCallbackOp.ScheduleLease.Before, noCallbackOp.ScheduleLease.BeforeRevision, null);
			noCallback.ScheduleCallback = null;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(noCallbackBook,
				noCallbackOp, noCallback));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Intent,
				noCallbackOp.ScheduleReceiptState, "no callback cannot mint schedule proof");
			TrustedWorld unchangedNoCallback = ScheduleWorld(noCallbackBook, noCallbackOp,
				noCallbackOp.ScheduleLease.Before, noCallbackOp.ScheduleLease.BeforeRevision, null);
			unchangedNoCallback.ScheduleCallback = null;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(noCallbackBook,
				noCallbackOp, unchangedNoCallback),
				"an unchanged intent may retry, but still needs a real callback proof");

			KingdomCarryBook interruptedBook = CarryBook();
			KingdomCarryOperation interrupted = BuildCarry(interruptedBook, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(interruptedBook, interrupted));
			RemoveCarrySources(interruptedBook, interrupted);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(interruptedBook, interrupted,
				KingdomLifecyclePhase.Removed, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(interruptedBook, interrupted,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			TrustedWorld changedThenCut = ScheduleWorld(interruptedBook, interrupted,
				interrupted.ScheduleLease.Before, interrupted.ScheduleLease.BeforeRevision, null);
			changedThenCut.ScheduleCallback = delegate(object reference, long after,
				string operationId)
			{
				changedThenCut.Rows[0].ValueValue = after;
				changedThenCut.Rows[0].RevisionValue++;
				changedThenCut.Rows[0].LastOperationIdValue = operationId;
				return null;
			};
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(
				interruptedBook, interrupted, changedThenCut));
			interruptedBook = RoundTrip(interruptedBook);
			interrupted = interruptedBook.Open;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(
				interruptedBook, interrupted, ScheduleWorld(interruptedBook, interrupted,
					interrupted.ScheduleLease.After, interrupted.ScheduleLease.AfterRevision,
					interrupted.Id)), "exact post-state recovers without repeating the callback");

			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op,
				ScheduleWorld(book, op, op.ScheduleLease.Before,
					op.ScheduleLease.BeforeRevision, null)), "Prepared has no scheduling authority");
			RemoveCarrySources(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			TrustedWorld foreign = ScheduleWorld(book, op, op.ScheduleLease.Before,
				op.ScheduleLease.BeforeRevision, null);
			foreign.Rows[0].ZoneIdValue = "foreign-zone";
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op, foreign));
			TrustedWorld stale = ScheduleWorld(book, op, op.ScheduleLease.Before,
				op.ScheduleLease.BeforeRevision + 1L, null);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op, stale));
			TrustedWorld duplicate = ScheduleWorld(book, op, op.ScheduleLease.Before,
				op.ScheduleLease.BeforeRevision, null);
			duplicate.Rows.Add(duplicate.Rows[0]);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op, duplicate));
			TrustedWorld world = ScheduleWorld(book, op, op.ScheduleLease.Before,
				op.ScheduleLease.BeforeRevision, null);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op, world));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 5L));
		}

		[Test]
		public void DomainPlan_RejectsArbitraryLeaseAndDepartedValueClaim()
		{
			KingdomLifecycleBook departed = Book("city-departed");
			KingdomLifecycleOperation depart = Build(departed, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Depart, 1L, 10L);
			depart.DepartedCount = depart.Count;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(departed, depart),
				"Prepared cannot claim a departure before its exact domain CAS");

			KingdomLifecycleBook arbitrary = Book("city-arbitrary");
			KingdomLifecycleOperation spawn = Build(arbitrary, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			spawn.ResourceLeases.Add(KingdomLifecycleRules.PrepareLease(arbitrary, spawn,
				KingdomLifecycleResourceKind.Standing, arbitrary.SettlementId,
				arbitrary.SettlementId, 10L, 1L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(arbitrary, spawn),
				"an unrelated lease cannot substitute for or accompany the action table");

			KingdomLifecycleBook wrongDelta = Book("city-wrong-delta");
			spawn = Build(wrongDelta, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			KingdomLifecycleResourceLease domain = spawn.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Population);
			domain.Delta++;
			domain.After++;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublish(wrongDelta, spawn));
		}

		[Test]
		public void WaterCallbackReceipt_RequiresExactUniqueVesselCompositionAndReference()
		{
			KingdomLifecycleBook foreignBook = Book("city-water-foreign");
			KingdomLifecycleOperation foreignOp = Build(foreignBook, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(foreignBook, foreignOp));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(foreignBook, foreignOp,
				KingdomLifecyclePhase.WaterIntent, 2L));
			KingdomLifecycleWaterLeg foreignLeg = foreignOp.WaterLegs[0];
			KingdomLifecycleResourceLease foreignLease = foreignOp.ResourceLeases.Find(l =>
				l.Key == foreignLeg.LeaseKey);
			TrustedWorld foreign = WaterWorld(foreignLeg);
			foreign.Rows[0].ObjectIdValue = "foreign-vessel";
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveWater(foreignBook,
				foreignLease, foreignLeg, foreign));

			KingdomLifecycleBook noCallbackBook = Book("city-water-no-callback");
			KingdomLifecycleOperation noCallbackOp = Build(noCallbackBook,
				KingdomLifecycleLane.NotableGuest, KingdomLifecycleAction.Lodge, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(noCallbackBook, noCallbackOp));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(noCallbackBook, noCallbackOp,
				KingdomLifecyclePhase.WaterIntent, 2L));
			KingdomLifecycleWaterLeg noCallbackLeg = noCallbackOp.WaterLegs[0];
			KingdomLifecycleResourceLease noCallbackLease = noCallbackOp.ResourceLeases.Find(l =>
				l.Key == noCallbackLeg.LeaseKey);
			TrustedWorld noCallback = WaterWorld(noCallbackLeg);
			noCallback.DisableWaterCallback = true;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveWater(noCallbackBook,
				noCallbackLease, noCallbackLeg, noCallback));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Intent, noCallbackLeg.ReceiptState);

			KingdomLifecycleBook duplicateBook = Book("city-water-duplicate");
			KingdomLifecycleOperation duplicateOp = Build(duplicateBook,
				KingdomLifecycleLane.NotableGuest, KingdomLifecycleAction.Lodge, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(duplicateBook, duplicateOp));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(duplicateBook, duplicateOp,
				KingdomLifecyclePhase.WaterIntent, 2L));
			KingdomLifecycleWaterLeg duplicateLeg = duplicateOp.WaterLegs[0];
			KingdomLifecycleResourceLease duplicateLease = duplicateOp.ResourceLeases.Find(l =>
				l.Key == duplicateLeg.LeaseKey);
			TrustedWorld duplicate = WaterWorld(duplicateLeg);
			duplicate.Rows.Add(duplicate.Rows[0]);
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveWater(duplicateBook,
				duplicateLease, duplicateLeg, duplicate));

			KingdomLifecycleBook book = Book("city-water-happy");
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.WaterIntent, 2L));
			KingdomLifecycleWaterLeg leg = op.WaterLegs[0];
			KingdomLifecycleResourceLease lease = op.ResourceLeases.Find(l => l.Key == leg.LeaseKey);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveWater(book, lease, leg,
				WaterWorld(leg)));
			ClassicAssert.AreEqual(op.WaterRequested, op.WaterProved);
			ClassicAssert.AreEqual(0, op.WaterOutstanding);
		}

		[Test]
		public void DeliverDisposition_CannotRetireThroughSkippedState()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Sinks, 2L));
			op.Outbox.LedgerState = KingdomLifecycleSinkState.Skipped;
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 3L));
			KingdomLifecycleRules.Normalize(book);
			ClassicAssert.IsTrue(book.Quarantined);
		}

		[Test]
		public void CarryDeliverSinks_CannotPublishOrRetireAsSkipped()
		{
			KingdomCarryBook publicationBook = CarryBook();
			KingdomCarryOperation publication = BuildCarry(publicationBook, 1L, 1, 1);
			publication.Outbox.LedgerDisposition = KingdomLifecycleSinkDisposition.Skip;
			publication.Outbox.LedgerState = KingdomLifecycleSinkState.Skipped;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublishCarry(publicationBook, publication));

			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			ReadyCarryProjection(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(book, op,
				op.Outputs[0], OutputWorld(op.Outputs[0])));
			ClassicAssert.IsTrue(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], false));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Projected, 6L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Sinks, 7L));
			Deliver(op.Outbox);
			op.Outbox.MessageState = KingdomLifecycleSinkState.Skipped;
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Terminal, 8L));
			KingdomLifecycleRules.Normalize(book);
			ClassicAssert.IsTrue(book.Quarantined);
		}

		[Test]
		public void LifecycleScheduleAndRemoval_RequireExactZoneBlueprintAndCallback()
		{
			KingdomLifecycleBook scheduleBook = Book("city-lifecycle-schedule");
			KingdomLifecycleOperation schedule = Build(scheduleBook,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Passages, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(scheduleBook, schedule));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(scheduleBook, schedule,
				KingdomLifecyclePhase.Sinks, 2L));
			Deliver(schedule.Outbox);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(scheduleBook, schedule,
				KingdomLifecyclePhase.ScheduleIntent, 3L));
			TrustedWorld foreignZone = LifecycleScheduleWorld(scheduleBook, schedule);
			foreignZone.Rows[0].ZoneIdValue = "foreign-zone";
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(
				scheduleBook, schedule, foreignZone));
			TrustedWorld noScheduleCallback = LifecycleScheduleWorld(scheduleBook, schedule);
			noScheduleCallback.ScheduleCallback = null;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(
				scheduleBook, schedule, noScheduleCallback));

			KingdomLifecycleBook removalBook = Book("city-lifecycle-removal");
			KingdomLifecycleOperation removal = Build(removalBook,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Depart, 1L, 10L);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(removalBook, removal));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(removalBook, removal,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			TrustedWorld wrongBlueprint = LifecycleRemovalWorld(removal);
			wrongBlueprint.Rows[0].BlueprintValue = "ForeignCitizen";
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleRemoval(
				removalBook, removal, wrongBlueprint));
			TrustedWorld noRemovalCallback = LifecycleRemovalWorld(removal);
			noRemovalCallback.LifecycleRemovalCallback = null;
			ClassicAssert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleRemoval(
				removalBook, removal, noRemovalCallback));
			ClassicAssert.AreEqual(KingdomLifecyclePhysicalState.Intent, removal.RemovalState);
		}

		[Test]
		public void PublicRulesApi_CannotMintTrustedCallbackReceiptsFromLiterals()
		{
			string[] removed =
			{
				"BeginWaterLease", "ConfirmWaterLeaseAfterCallback",
				"PrepareCarryScheduleLease", "BeginCarrySchedule",
				"CommitCarryScheduleWitness", "BeginCarryOutput",
				"ConfirmCarryOutputAfterCallback", "SkipCarryOutputOnRoad",
				"BeginCarryUnit", "ConfirmCarryUnit", "ConfirmLeaseFromPhysicalMarker"
			};
			for (int i = 0; i < removed.Length; i++)
				ClassicAssert.IsNull(typeof(KingdomLifecycleRules).GetMethod(removed[i],
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
					removed[i]);
			ClassicAssert.IsFalse(typeof(KingdomLifecycleRules.TrustedAdapter).IsPublic);
			KingdomLifecycleBook book = Book("city-public-physical");
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Spawn, 1L);
			ClassicAssert.IsNull(KingdomLifecycleRules.PrepareLease(book, op,
				KingdomLifecycleResourceKind.Schedule, book.SettlementId, "schedule", 1L, 1L));
			ClassicAssert.IsNull(KingdomLifecycleRules.PrepareLease(book, op,
				KingdomLifecycleResourceKind.Object, "topology", "object", 1L, -1L));
		}

		[Test]
		public void IdentityBinding_RequiresPristineStateExactMigrationKeyAndFullCarrySet()
		{
			KingdomLifecycleBook dirty = new KingdomLifecycleBook { PlainGuestNextSequence = 2L };
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(dirty, "city-a", false,
				null, null));
			ClassicAssert.IsNull(dirty.SettlementId);
			KingdomLifecycleBook migration = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(migration, "city-a", true,
				"migration-a", new List<string>()));
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(migration, "city-a", true,
				"migration-b", new List<string>()));
			KingdomLifecycleBook preseeded = new KingdomLifecycleBook { SettlementId = "city-a" };
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(preseeded, "city-a", false,
				null, new List<string>()), "preseeded id has no durable binding receipt");
			KingdomLifecycleRules.Normalize(preseeded);
			ClassicAssert.IsTrue(preseeded.Quarantined);
			KingdomLifecycleBook established = Book("city-established");
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(established,
				"city-established", false, null, null), "established binding still needs a scan");
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(established,
				"city-established", false, null, new List<string> { "city-established" }));
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(established,
				"city-established", false, null, new List<string>()));

			KingdomCarryBook carry = new KingdomCarryBook();
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindCarryIdentity(carry, "realm-a",
				new List<string> { "city-a", "city-a" }, false, null));
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindCarryIdentity(carry, "realm-a",
				new List<string> { "city-b", "city-a" }, false, null));
			CollectionAssert.AreEqual(new List<string> { "city-a", "city-b" }, carry.SettlementIds);
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindCarryIdentity(carry, "realm-a",
				new List<string> { "city-a" }, false, null));
			KingdomCarryBook preseededCarry = new KingdomCarryBook
			{
				RealmId = "realm-a", SettlementIds = new List<string> { "city-a", "city-b" }
			};
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindCarryIdentity(preseededCarry, "realm-a",
				new List<string> { "city-a", "city-b" }, false, null));
			KingdomLifecycleRules.Normalize(preseededCarry);
			ClassicAssert.IsTrue(preseededCarry.Quarantined);
			KingdomCarryOperation op = BuildCarry(carry, 1L, 1, 1);
			op.DestinationSettlementId = "foreign-city";
			ClassicAssert.IsFalse(KingdomLifecycleRules.TryPublishCarry(carry, op));
		}

		[Test]
		public void FirstFoundingCarryBinding_PublishesOneAtomicIdentityReceipt()
		{
			KingdomCarryBook book = new KingdomCarryBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindCarryIdentity(book, "realm-first",
				new List<string> { "city-first" }, false, null));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book));
			ClassicAssert.AreEqual("realm-first", book.RealmId);
			CollectionAssert.AreEqual(new[] { "city-first" }, book.SettlementIds);
			ClassicAssert.IsTrue(book.IdentityBound);
			ClassicAssert.IsNotEmpty(book.IdentityProof);

			KingdomCarryBook preseeded = new KingdomCarryBook { RealmId = "realm-first" };
			KingdomLifecycleRules.Normalize(preseeded);
			ClassicAssert.IsTrue(preseeded.Quarantined,
				"a realm id without its atomic identity receipt owns no authority");
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(preseeded));
		}

		[Test]
		public void CarryIdentityExpansion_IsCanonicalMonotoneRetryStableAndWireStable()
		{
			KingdomCarryBook book = new KingdomCarryBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindCarryIdentity(book, "realm-expand",
				new List<string> { "city-b" }, false, null));
			List<string> singleton = book.SettlementIds;
			string singletonProof = book.IdentityProof;
			byte[] singletonWire = CarryBytes(book);
			string failure;

			ClassicAssert.IsTrue(KingdomLifecycleRules.CanExpandCarryIdentity(book, "realm-expand",
				new List<string> { "city-b", "city-a" }, out failure), failure);
			ClassicAssert.AreSame(singleton, book.SettlementIds);
			ClassicAssert.AreEqual(singletonProof, book.IdentityProof);
			CollectionAssert.AreEqual(singletonWire, CarryBytes(book));
			ClassicAssert.IsTrue(KingdomLifecycleRules.ExpandCarryIdentity(book, "realm-expand",
				new List<string> { "city-b", "city-a" }, out failure), failure);
			ClassicAssert.AreNotSame(singleton, book.SettlementIds);
			ClassicAssert.AreNotEqual(singletonProof, book.IdentityProof);
			CollectionAssert.AreEqual(new[] { "city-a", "city-b" }, book.SettlementIds);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book));

			List<string> expanded = book.SettlementIds;
			string expandedProof = book.IdentityProof;
			byte[] expandedWire = CarryBytes(book);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanExpandCarryIdentity(book, "realm-expand",
				new List<string> { "city-b", "city-a" }, out failure), failure);
			ClassicAssert.IsTrue(KingdomLifecycleRules.ExpandCarryIdentity(book, "realm-expand",
				new List<string> { "city-b", "city-a" }, out failure), failure);
			ClassicAssert.AreSame(expanded, book.SettlementIds,
				"an exact retry must not replace the established topology object");
			ClassicAssert.AreEqual(expandedProof, book.IdentityProof);
			CollectionAssert.AreEqual(expandedWire, CarryBytes(book));

			KingdomCarryBook reloaded = RoundTrip(book);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(reloaded));
			ClassicAssert.AreEqual(expandedProof, reloaded.IdentityProof);
			CollectionAssert.AreEqual(expanded, reloaded.SettlementIds);
			CollectionAssert.AreEqual(expandedWire, CarryBytes(reloaded));
		}

		[Test]
		public void CarryIdentityExpansion_RejectsWrongRealmRemovalAndReplacement()
		{
			string failure;
			KingdomCarryBook wrongRealm = CarryBook();
			List<string> wrongRealmTopology = wrongRealm.SettlementIds;
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(wrongRealm, "realm-b",
				new List<string> { "city-a", "city-b", "city-c" }, out failure));
			ClassicAssert.IsNotEmpty(failure);
			ClassicAssert.IsFalse(wrongRealm.Quarantined);
			ClassicAssert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(wrongRealm, "realm-b",
				new List<string> { "city-a", "city-b", "city-c" }, out failure));
			ClassicAssert.IsTrue(wrongRealm.Quarantined);
			ClassicAssert.AreSame(wrongRealmTopology, wrongRealm.SettlementIds);

			KingdomCarryBook removal = CarryBook();
			List<string> removalTopology = removal.SettlementIds;
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(removal, "realm-a",
				new List<string> { "city-a" }, out failure));
			ClassicAssert.IsNotEmpty(failure);
			ClassicAssert.IsFalse(removal.Quarantined);
			ClassicAssert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(removal, "realm-a",
				new List<string> { "city-a" }, out failure));
			ClassicAssert.IsTrue(removal.Quarantined);
			ClassicAssert.AreSame(removalTopology, removal.SettlementIds);
			CollectionAssert.AreEqual(new[] { "city-a", "city-b" }, removal.SettlementIds);

			KingdomCarryBook replacement = CarryBook();
			List<string> replacementTopology = replacement.SettlementIds;
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(replacement, "realm-a",
				new List<string> { "city-a", "city-c" }, out failure));
			ClassicAssert.IsNotEmpty(failure);
			ClassicAssert.IsFalse(replacement.Quarantined);
			ClassicAssert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(replacement, "realm-a",
				new List<string> { "city-a", "city-c" }, out failure));
			ClassicAssert.IsTrue(replacement.Quarantined);
			ClassicAssert.AreSame(replacementTopology, replacement.SettlementIds);
			CollectionAssert.AreEqual(new[] { "city-a", "city-b" }, replacement.SettlementIds);
		}

		[Test]
		public void CarryIdentityExpansion_OpenReceiptDefersWithoutChangingAuthority()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation operation = BuildCarry(book, 1L, 1, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, operation));
			List<string> topology = book.SettlementIds;
			string proof = book.IdentityProof;
			byte[] before = CarryBytes(book);
			string failure;

			ClassicAssert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(book, "realm-a",
				new List<string> { "city-a", "city-b", "city-c" }, out failure));
			ClassicAssert.IsNotEmpty(failure);
			StringAssert.Contains("open", failure.ToLowerInvariant());
			ClassicAssert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(book, "realm-a",
				new List<string> { "city-a", "city-b", "city-c" }, out failure));
			ClassicAssert.IsNotEmpty(failure);
			StringAssert.Contains("open", failure.ToLowerInvariant());
			ClassicAssert.IsFalse(book.Quarantined);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book));
			ClassicAssert.AreSame(topology, book.SettlementIds);
			ClassicAssert.AreSame(operation, book.Open);
			ClassicAssert.AreEqual(proof, book.IdentityProof);
			CollectionAssert.AreEqual(before, CarryBytes(book));
		}

		[Test]
		public void CarryIdentityExpansion_MalformedAndOverCapCandidatesLeaveBookExact()
		{
			KingdomCarryBook book = CarryBook();
			List<string> topology = book.SettlementIds;
			string proof = book.IdentityProof;
			byte[] before = CarryBytes(book);
			string oversizedId = new string('x', KingdomLifecycleRules.MaxIdChars + 1);
			ICollection<string>[] malformed = new ICollection<string>[]
			{
				null,
				new List<string>(),
				new List<string> { "city-a", "city-a" },
				new List<string> { "city-a", null },
				new List<string> { "city-a", "city-b", oversizedId },
				new List<string> { "city-a", "city-b", "city-c", "city-d", "city-e" }
			};

			for (int i = 0; i < malformed.Length; i++)
			{
				string failure;
				ClassicAssert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(book, "realm-a",
					malformed[i], out failure), "preflight candidate " + i);
				ClassicAssert.IsNotEmpty(failure, "preflight candidate " + i);
				ClassicAssert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(book, "realm-a",
					malformed[i], out failure), "publish candidate " + i);
				ClassicAssert.IsNotEmpty(failure, "publish candidate " + i);
				ClassicAssert.IsFalse(book.Quarantined, "candidate " + i);
				ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book), "candidate " + i);
				ClassicAssert.AreSame(topology, book.SettlementIds, "candidate " + i);
				ClassicAssert.AreEqual(proof, book.IdentityProof, "candidate " + i);
				CollectionAssert.AreEqual(before, CarryBytes(book), "candidate " + i);
			}
		}

		[Test]
		public void CarryIdentityExpansion_HostileEnumerationCannotPublishChangedAuthority()
		{
			KingdomCarryBook preflight = CarryBook();
			List<string> preflightTopology = preflight.SettlementIds;
			MutatingCollection candidate = new MutatingCollection(
				new List<string> { "city-a", "city-b", "city-c" },
				delegate { preflight.IdentityProof = "hostile-proof"; });
			string failure;
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(preflight, "realm-a",
				candidate, out failure));
			ClassicAssert.IsNotEmpty(failure);
			StringAssert.Contains("changed", failure.ToLowerInvariant());
			ClassicAssert.AreSame(preflightTopology, preflight.SettlementIds);
			ClassicAssert.IsFalse(preflight.Quarantined);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(preflight));

			KingdomCarryBook publish = CarryBook();
			List<string> publishTopology = publish.SettlementIds;
			candidate = new MutatingCollection(
				new List<string> { "city-a", "city-b", "city-c" },
				delegate { publish.NextSequence = 2L; });
			ClassicAssert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(publish, "realm-a",
				candidate, out failure));
			ClassicAssert.IsTrue(publish.Quarantined);
			StringAssert.Contains("changed", publish.Fault);
			ClassicAssert.AreSame(publishTopology, publish.SettlementIds);
			CollectionAssert.AreEqual(new[] { "city-a", "city-b" }, publish.SettlementIds);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(publish));
		}

		[Test]
		public void SettlementIdentityCollisionScan_UsesIndependentBoundAndRejectsAliases()
		{
			List<string> fiveOtherSettlements = new List<string>
			{
				"city-1", "city-2", "city-3", "city-4", "city-5"
			};
			KingdomLifecycleBook accepted = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(accepted, "city-target",
				false, null, fiveOtherSettlements),
				"collision scan must not inherit four-city carry topology cap");
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(accepted));

			KingdomLifecycleBook duplicate = new KingdomLifecycleBook();
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(duplicate, "city-target",
				false, null, new List<string> { "city-1", "city-2", "city-3", "city-4",
					"city-5", "city-5" }));
			ClassicAssert.IsNull(duplicate.SettlementId);

			KingdomLifecycleBook target = new KingdomLifecycleBook();
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(target, "city-target",
				false, null, new List<string> { "city-1", "city-2", "city-3", "city-4",
					"city-5", "city-target" }));
			ClassicAssert.IsNull(target.SettlementId);

			List<string> maximum = new List<string>();
			for (int i = 0; i < KingdomLifecycleRules.MaxLifecycleCollisionIds; i++)
				maximum.Add("city-global-" + i);
			KingdomLifecycleBook atCap = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(atCap, "city-at-cap",
				false, null, maximum));
			maximum.Add("city-over-cap");
			KingdomLifecycleBook overCap = new KingdomLifecycleBook();
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(overCap, "city-over",
				false, null, maximum));
		}

		[Test]
		public void IdentityBinding_CallbackMutatedOrThrowingTopologyCannotPublishAuthority()
		{
			KingdomLifecycleBook mutated = new KingdomLifecycleBook();
			MutatingCollection ids = new MutatingCollection(new List<string> { "city-b" },
				delegate { mutated.PlainGuestNextSequence = 2L; });
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(mutated, "city-a", false,
				null, ids));
			ClassicAssert.IsNull(mutated.SettlementId);

			KingdomLifecycleBook throwing = new KingdomLifecycleBook();
			ids = new MutatingCollection(new List<string> { "city-b" },
				delegate { throw new InvalidOperationException("hostile enumeration"); });
			ClassicAssert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(throwing, "city-a", false,
				null, ids));
			ClassicAssert.IsNull(throwing.SettlementId);
		}

		[Test]
		public void CarryWireAndUtf8Codec_RejectFutureSchemaNoncanonicalBoolAndByteOverflow()
		{
			byte[] futureWire;
			using (MemoryStream futureBytes = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(futureBytes,
					System.Text.Encoding.UTF8, true))
				{
					writer.Write(KingdomLifecycleWireCodec.CarryMagic);
					writer.Write(KingdomLifecycleRules.CurrentCarryFormatVersion + 1);
					writer.Write(3);
					writer.Write(new byte[] { 7, 8, 9 }, 0, 3);
				}
				futureWire = futureBytes.ToArray();
				futureBytes.Position = 0;
				KingdomCarryBook future = new KingdomCarryBook();
				KingdomLifecycleWireCodec.ReadCarry(new BinaryReader(futureBytes), future);
				ClassicAssert.IsFalse(future.WireRejected);
				ClassicAssert.IsTrue(future.Quarantined);
				ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentCarryFormatVersion + 1,
					future.OpaqueWireVersion);
				CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, future.OpaquePayload);
				CollectionAssert.AreEqual(futureWire, CarryBytes(future));
			}
			using (MemoryStream stream = new MemoryStream())
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec.WriteString(
					new BinaryWriter(stream), "éé", 3));

			KingdomCarryBook book = CarryBook();
			byte[] bytes;
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream,
					System.Text.Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteCarry(writer, book);
				bytes = stream.ToArray();
			}
			bytes[12] = 2;
			using (MemoryStream stream = new MemoryStream(bytes))
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec.ReadCarry(
					new BinaryReader(stream), new KingdomCarryBook()));
		}

		[Test]
		public void CarryV5Wire_IsFrozenAndUpgradesWithoutReinterpretingProjection()
		{
			KingdomCarryBook original = CarryBook();
			byte[] v5 = CarryV5Bytes(original);
			ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyCarryFormatVersion,
				BitConverter.ToInt32(v5, 4));
			ClassicAssert.AreEqual("39d703751fdb3343d3b90c414802dd8956e4acd36454b789c47d3fb70f0b2e66",
				Sha256(v5), "PIN_CARRY_V5_SHA256");
			KingdomCarryBook loaded = new KingdomCarryBook();
			using (MemoryStream stream = new MemoryStream(v5, false))
				KingdomLifecycleWireCodec.ReadCarry(new BinaryReader(stream), loaded);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentCarryFormatVersion,
				loaded.FormatVersion);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(loaded));
			CollectionAssert.AreEqual(v5, CarryV5Bytes(loaded));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(RoundTrip(loaded)));
		}

		[Test]
		public void CompositeWire_WritesRawBytesWithoutCallingOverriddenArrayFraming()
		{
			KingdomLifecycleBook lifecycle = Book("city-hostile-writer");
			KingdomCarryBook carry = CarryBook();
			CollectionAssert.AreEqual(LifecycleBytes(lifecycle),
				LifecycleBytesWithHostileArrayWriter(lifecycle));
			CollectionAssert.AreEqual(CarryBytes(carry),
				CarryBytesWithHostileArrayWriter(carry));
		}

		private static KingdomLifecycleOperation ReadyLodgeDomain(KingdomLifecycleBook book,
			bool market = false)
		{
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 1L, 10L);
			op.ObjectId = "exact-lodge-body";
			op.Blueprint = "r_KingdomNotableGuest";
			op.ObjectName = "Mara of the Glass Road";
			op.Origin = "the glass road";
			op.Faction = "1 of Nivvun Ut, 1002 AR";
			if (market) { op.Target = 1; op.PlunderRequested = 3; }
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.WaterIntent, 2L));
			SettleCurrentPhase(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.WaterSettled, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.DomainIntent, 4L));
			return op;
		}

		private static void SettleLodgeAbandonSchedule(KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			ClassicAssert.IsTrue(KingdomLifecycleRules.BeginLodgeAbandonSchedule(book, op, op.DueBefore));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CommitLodgeAbandonSchedule(book, op, op.DueAfter));
			ClassicAssert.AreEqual(KingdomLifecycleMutationAction.Settled,
				KingdomLifecycleRules.LodgeAbandonScheduleAction(book, op, op.DueAfter));
		}

		private static void AssertAbandonedLodgeHasNoSuccessOrRefund(
			KingdomLifecycleOperation op)
		{
			ClassicAssert.AreEqual(KingdomLifecyclePhase.Terminal, op.Phase);
			ClassicAssert.AreEqual(1, op.WaterRequested);
			ClassicAssert.AreEqual(1, op.WaterProved);
			ClassicAssert.AreEqual(0, op.WaterOutstanding);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, op.Outbox.ChronicleState);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, op.Outbox.LedgerState);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, op.Outbox.MessageState);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, op.Outbox.DeedState);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, op.Outbox.GuestbookState);
			ClassicAssert.IsFalse(op.ResourceLeases.Exists(l =>
				l.Kind == KingdomLifecycleResourceKind.Population));
		}

		private static KingdomLifecycleOperation Build(KingdomLifecycleBook book,
			KingdomLifecycleLane lane, KingdomLifecycleAction action, long tick, long scheduleBefore)
		{
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book, lane, action, tick);
			ClassicAssert.NotNull(op);
			op.ZoneId = "zone-a";
			bool raid = lane == KingdomLifecycleLane.Raid;
			if (raid) SeedRaidPlan(book, op, action, tick);
			else
			{
				op.DueBefore = scheduleBefore;
				op.DueAfter = scheduleBefore + 1L;
				op.ResourceLeases.Add(KingdomLifecycleRules.TrustedAdapter.PreparePhysicalLease(
					book, op, KingdomLifecycleResourceKind.Schedule, book.SettlementId,
					KingdomLifecycleRules.ScheduleSubjectId(book.SettlementId, lane),
					scheduleBefore, 1L));
			}

			bool water = action == KingdomLifecycleAction.OfferWater
				|| action == KingdomLifecycleAction.Lodge
				|| action == KingdomLifecycleAction.RaidTribute;
			if (water)
			{
				string owner = "vessel-" + (byte)lane;
				KingdomLifecycleResourceLease lease = KingdomLifecycleRules.TrustedAdapter.PreparePhysicalLease(book, op,
					KingdomLifecycleResourceKind.WaterVessel, "zone-a", owner, 5L, -1L);
				op.ResourceLeases.Add(lease);
				op.WaterRequested = 1;
				op.WaterOutstanding = 1;
				op.WaterState = KingdomLifecyclePhysicalState.Prepared;
				op.WaterLegs.Add(new KingdomLifecycleWaterLeg
				{
					OperationId = op.Id, LeaseKey = lease.Key, OwnerId = owner,
					Blueprint = "LiquidVolume", ZoneId = "zone-a",
					Capacity = 10, Before = 5, Delta = 1, After = 4,
					Composition = "water:1000",
					ReceiptId = KingdomLifecycleRules.ChildId(op.Id, "water-receipt", 0),
					ReceiptState = KingdomLifecyclePhysicalState.Prepared,
					State = KingdomLifecyclePhysicalState.Prepared
				});
			}

			if (action == KingdomLifecycleAction.Spawn)
			{
				op.PartySize = 1;
				KingdomLifecycleProjection projection = Projection(op, 0, -1, 1);
				op.Projections.Add(projection);
				string topology = KingdomLifecycleRules.TopologyId(projection.Topology,
					projection.OwnerId, projection.ZoneId, projection.X, projection.Y);
				op.ResourceLeases.Add(KingdomLifecycleRules.TrustedAdapter.PreparePhysicalLease(book, op,
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId, 0L, 1L));
			}
			else if (action == KingdomLifecycleAction.RaidAttack)
			{
				op.PartySize = 1;
				ClassicAssert.NotNull(KingdomLifecycleRules.RaidRuntimeAdapter.PrepareProjection(book, op,
					0, KingdomLifecycleRules.ChildId(op.Id, "raider", 0), "Snapjaw",
					"zone-a", 0, 0));
			}
			else if (action == KingdomLifecycleAction.RaidDeliverDemand)
			{
				ClassicAssert.NotNull(KingdomLifecycleRules.RaidRuntimeAdapter.PrepareInventoryProjection(
					book, op, 0, op.ObjectMarker, op.Blueprint, "player-id", "zone-a"));
			}
			if (action == KingdomLifecycleAction.Depart || action == KingdomLifecycleAction.OfferWater)
			{
				op.ObjectId = KingdomLifecycleRules.ChildId(op.Id, "resident", 0);
				op.Blueprint = "Citizen";
				op.Count = 1;
				op.ObjectTopology = KingdomLifecycleTopology.Cell;
				op.ObjectX = 0;
				op.ObjectY = 0;
				op.RemovalState = KingdomLifecyclePhysicalState.Prepared;
				string topology = KingdomLifecycleRules.TopologyId(op.ObjectTopology,
					op.ObjectOwnerId, op.ZoneId, op.ObjectX, op.ObjectY);
				op.ResourceLeases.Add(KingdomLifecycleRules.TrustedAdapter.PreparePhysicalLease(book, op,
					KingdomLifecycleResourceKind.Object, topology, op.ObjectId, 1L, -1L));
			}
			if (action == KingdomLifecycleAction.RaidAttack)
			{
				op.EffectState = KingdomLifecyclePhysicalState.Prepared;
				op.PlunderRequested = 1;
			}
			if (action != KingdomLifecycleAction.Passages && !raid)
			{
				KingdomLifecycleResourceKind kind;
				long delta;
				if (action == KingdomLifecycleAction.Spawn)
				{
					kind = KingdomLifecycleResourceKind.Population;
					delta = op.PartySize;
				}
				else if (action == KingdomLifecycleAction.Depart)
				{
					kind = KingdomLifecycleResourceKind.Population;
					delta = -op.Count;
				}
				else if (action == KingdomLifecycleAction.OfferWater)
				{
					kind = KingdomLifecycleResourceKind.Standing;
					delta = op.WaterRequested;
				}
				else if (action == KingdomLifecycleAction.Lodge)
				{
					kind = KingdomLifecycleResourceKind.Roster;
					delta = 1L;
				}
				else if (lane == KingdomLifecycleLane.Raid)
				{
					kind = KingdomLifecycleResourceKind.Raid;
					delta = 1L;
				}
				else
				{
					kind = KingdomLifecycleResourceKind.Petition;
					delta = 1L;
				}
				op.ResourceLeases.Add(KingdomLifecycleRules.PrepareLease(book, op, kind,
					book.SettlementId, book.SettlementId, 100L + scheduleBefore, delta));
			}
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, "chronicle", "ledger", "message",
				"deed", "guestbook");
			if (raid) ClassicAssert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(book, op));
			return op;
		}

		private static void SeedRaidPlan(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, KingdomLifecycleAction action, long tick)
		{
			if (action == KingdomLifecycleAction.RaidWarning)
			{
				string source = KingdomLifecycleRules.ChildId(book.SettlementId,
					"test-provocation-" + op.Sequence, 0);
				op.Origin = source;
				op.ObjectId = KingdomRaidIncidentRules.GrievanceId(source);
				op.ObjectMarker = KingdomRaidIncidentRules.IncidentId(op.ObjectId);
				op.ObjectName = "test authored act";
				op.Faction = "Snapjaws";
				op.DisplayFaction = "test salt-road reach";
				op.Creed = "test-provocation";
				op.Detail = "a test scout was explicitly challenged";
				op.ArrivalText = "zone-source";
				op.Target = 1;
				op.Count = 1;
				op.DepartTick = tick + 100L;
				op.PlunderRequested = 1;
				op.Kind = 24;
				op.Blueprint = "test-profile";
				return;
			}

			KingdomRaidIncident active = KingdomRaidIncidentRules.Active(book.RaidLedger);
			if (active == null)
			{
				KingdomLifecycleOperation warning = new KingdomLifecycleOperation
				{
					Lane = KingdomLifecycleLane.Raid, Action = KingdomLifecycleAction.RaidWarning,
					SettlementId = book.SettlementId, ZoneId = "zone-a",
					Origin = KingdomLifecycleRules.ChildId(book.SettlementId,
						"test-seed-" + (byte)action, 0),
					ObjectName = "test authored act", Faction = "Snapjaws",
					DisplayFaction = "test salt-road reach", Creed = "test-provocation",
					Detail = "a test scout was explicitly challenged", ArrivalText = "zone-source",
					Target = 1, Count = 1, CreatedTick = tick - 5L, DepartTick = tick + 100L,
					PlunderRequested = 1, Kind = 24, Blueprint = "test-profile"
				};
				warning.ObjectId = KingdomRaidIncidentRules.GrievanceId(warning.Origin);
				warning.ObjectMarker = KingdomRaidIncidentRules.IncidentId(warning.ObjectId);
				ClassicAssert.IsTrue(KingdomRaidIncidentRules.TryApply(book.RaidLedger, warning,
					out KingdomRaidLedger seeded));
				book.RaidLedger = seeded;
				active = KingdomRaidIncidentRules.Active(book.RaidLedger);
			}

			bool needsDelivery = action != KingdomLifecycleAction.RaidDeliverDemand
				&& action != KingdomLifecycleAction.RaidCancel;
			if (needsDelivery)
				active = SeedRaidDelivery(book, active, tick - 4L);
			bool deadline = action == KingdomLifecycleAction.RaidDeadline
				|| action == KingdomLifecycleAction.RaidRewarning;
			bool needsAcknowledgement = needsDelivery
				&& action != KingdomLifecycleAction.RaidAcknowledgeDemand
				&& action != KingdomLifecycleAction.RaidLoseChannel;
			if (needsAcknowledgement)
				active = SeedRaidAcknowledgement(book, active, tick - 3L,
					deadline ? tick : tick + 100L);

			if (action == KingdomLifecycleAction.RaidFortifyFailure)
			{
				KingdomLifecycleOperation order = SeedRaidResponse(active,
					KingdomLifecycleAction.RaidFortifyOrder, tick - 2L);
				active = ApplyRaidSeed(book, order, active.Id);
			}
			if (action == KingdomLifecycleAction.RaidAttack)
			{
				active.State = KingdomRaidIncidentState.FightCommitted;
				active.Response = KingdomRaidResponse.Fight;
			}
			if (action == KingdomLifecycleAction.RaidResolve)
				SeedActiveRaid(active, "test-attack");
			if (action == KingdomLifecycleAction.RaidRecoveryAccept
				|| action == KingdomLifecycleAction.RaidRecoveryReady
				|| action == KingdomLifecycleAction.RaidRecoveryResolve
				|| action == KingdomLifecycleAction.RaidRecoveryDecline)
			{
				SeedActiveRaid(active, KingdomLifecycleRules.ChildId(active.Id, "test-attack", 0));
				KingdomLifecycleOperation loss = SeedRaidResponse(active,
					KingdomLifecycleAction.RaidResolve, tick - 2L);
				loss.Kind = (int)KingdomRaidResolution.StoresPlundered;
				loss.Target = 1;
				active = ApplyRaidSeed(book, loss, active.Id);
				if (action == KingdomLifecycleAction.RaidRecoveryReady
					|| action == KingdomLifecycleAction.RaidRecoveryResolve)
				{
					KingdomLifecycleOperation accept = SeedRaidResponse(active,
						KingdomLifecycleAction.RaidRecoveryAccept, tick - 1L);
					accept.Origin = active.RecoveryQuestId;
					accept.ObjectMarker = active.RecoveryStepId;
					active = ApplyRaidSeed(book, accept, active.Id);
				}
				if (action == KingdomLifecycleAction.RaidRecoveryResolve)
				{
					KingdomLifecycleOperation ready = SeedRaidResponse(active,
						KingdomLifecycleAction.RaidRecoveryReady, tick);
					ready.Origin = active.AttackOperationId;
					active = ApplyRaidSeed(book, ready, active.Id);
				}
			}
			op.ObjectId = active.Id;
			op.Faction = active.AttackerFactionId;
			op.ZoneId = active.TargetZoneId;
			switch (action)
			{
			case KingdomLifecycleAction.RaidRewarning:
			case KingdomLifecycleAction.RaidDeadline:
				break;
			case KingdomLifecycleAction.RaidDeliverDemand:
				op.Origin = active.DemandChannelId;
				op.Target = active.ChannelRevision + 1;
				op.ObjectMarker = KingdomRaidIncidentRules.DemandObjectId(
					active.DemandChannelId, op.Target);
				op.Count = 1;
				op.Blueprint = "r_KingdomSnapjawRaidDemand";
				break;
			case KingdomLifecycleAction.RaidAcknowledgeDemand:
				op.Origin = active.DemandObjectId;
				op.DepartTick = tick + 100L;
				break;
			case KingdomLifecycleAction.RaidLoseChannel:
				op.Origin = active.DemandObjectId;
				break;
			case KingdomLifecycleAction.RaidFortify:
				op.Detail = "R1;101=1[]";
				op.Defence = 1;
				break;
			case KingdomLifecycleAction.RaidAttack:
				active.State = KingdomRaidIncidentState.FightCommitted;
				active.Response = KingdomRaidResponse.Fight;
				op.Origin = "test-store";
				op.ArrivalText = "stores";
				op.Target = 1;
				op.Count = 1;
				op.PlunderRequested = active.DisclosedStake;
				break;
			case KingdomLifecycleAction.RaidResolve:
				active.State = KingdomRaidIncidentState.Active;
				active.Response = KingdomRaidResponse.Fight;
				active.ObjectiveObjectId = "test-store";
				active.ObjectiveX = 1;
				active.ObjectiveY = 1;
				active.SpawnedPartySize = active.PlannedPartySize;
				op.Kind = (int)KingdomRaidResolution.RaidersDefeated;
				op.Target = 0;
				break;
			case KingdomLifecycleAction.RaidCancel:
				op.Kind = (int)KingdomRaidResolution.SourceInvalid;
				break;
			case KingdomLifecycleAction.RaidRecoveryAccept:
				op.Origin = active.RecoveryQuestId;
				op.ObjectMarker = active.RecoveryStepId;
				break;
			case KingdomLifecycleAction.RaidRecoveryReady:
				op.Origin = active.AttackOperationId;
				break;
			}
			ClassicAssert.IsTrue(KingdomRaidIncidentRules.ValidLedger(book.RaidLedger));
		}

		private static KingdomLifecycleOperation SeedRaidResponse(KingdomRaidIncident incident,
			KingdomLifecycleAction action, long tick)
		{
			return new KingdomLifecycleOperation
			{
				Id = KingdomLifecycleRules.ChildId(incident.Id,
					"seed-response-" + (byte)action + "-" + tick, 0),
				Lane = KingdomLifecycleLane.Raid, Action = action,
				SettlementId = incident.SettlementId, ZoneId = incident.TargetZoneId,
				ObjectId = incident.Id, Faction = incident.AttackerFactionId,
				CreatedTick = tick
			};
		}

		private static KingdomRaidIncident SeedRaidDelivery(KingdomLifecycleBook book,
			KingdomRaidIncident incident, long tick)
		{
			KingdomLifecycleOperation delivery = SeedRaidResponse(incident,
				KingdomLifecycleAction.RaidDeliverDemand, tick);
			delivery.Origin = incident.DemandChannelId;
			delivery.Target = incident.ChannelRevision + 1;
			delivery.ObjectMarker = KingdomRaidIncidentRules.DemandObjectId(
				incident.DemandChannelId, delivery.Target);
			delivery.Count = 1;
			delivery.Blueprint = "r_KingdomSnapjawRaidDemand";
			return ApplyRaidSeed(book, delivery, incident.Id);
		}

		private static KingdomRaidIncident SeedRaidAcknowledgement(KingdomLifecycleBook book,
			KingdomRaidIncident incident, long tick, long due)
		{
			KingdomLifecycleOperation acknowledgement = SeedRaidResponse(incident,
				KingdomLifecycleAction.RaidAcknowledgeDemand, tick);
			acknowledgement.Origin = incident.DemandObjectId;
			acknowledgement.DepartTick = due;
			return ApplyRaidSeed(book, acknowledgement, incident.Id);
		}

		private static void SeedActiveRaid(KingdomRaidIncident incident, string attackId)
		{
			incident.State = KingdomRaidIncidentState.Active;
			incident.Response = KingdomRaidResponse.Fight;
			incident.ObjectiveCode = "stores";
			incident.ObjectiveObjectId = "test-store";
			incident.ObjectiveX = 1;
			incident.ObjectiveY = 1;
			incident.SpawnedPartySize = incident.PlannedPartySize;
			incident.AttackOperationId = attackId;
		}

		private static KingdomRaidIncident ApplyRaidSeed(KingdomLifecycleBook book,
			KingdomLifecycleOperation operation, string incidentId)
		{
			ClassicAssert.IsTrue(KingdomRaidIncidentRules.TryApply(book.RaidLedger, operation,
				out KingdomRaidLedger seeded), operation.Action.ToString());
			book.RaidLedger = seeded;
			return KingdomRaidIncidentRules.Incident(book.RaidLedger, incidentId);
		}

		private static void Settle(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, long tick)
		{
			int guard = 0;
			while (op.Phase != KingdomLifecyclePhase.Terminal && guard++ < 20)
			{
				SettleCurrentPhase(book, op);
				bool moved = false;
				foreach (KingdomLifecyclePhase phase in Enum.GetValues(typeof(KingdomLifecyclePhase)))
				{
					if (phase == KingdomLifecyclePhase.Quarantined) continue;
					if (KingdomLifecycleRules.CanTransition(op.Action, op.Phase, phase))
					{
						ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op, phase, tick + guard));
						moved = true;
						break;
					}
				}
				ClassicAssert.IsTrue(moved, op.Action + " at " + op.Phase);
			}
		}

		private static void SettleCurrentPhase(KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (op.Phase == KingdomLifecyclePhase.ProjectionIntent)
			{
				for (int i = 0; i < op.Projections.Count; i++)
					SettleProjectionLease(book, op, op.Projections[i]);
			}
			else if (op.Phase == KingdomLifecyclePhase.WaterIntent)
			{
				if (op.WaterRequested > 0)
				{
					for (int i = 0; i < op.WaterLegs.Count; i++)
					{
						KingdomLifecycleWaterLeg leg = op.WaterLegs[i];
						KingdomLifecycleResourceLease lease = op.ResourceLeases.Find(l =>
							l.Key == leg.LeaseKey);
						ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveWater(book,
							lease, leg, WaterWorld(leg)), op.Action + " water receipt");
					}
				}
			}
			else if (op.Phase == KingdomLifecyclePhase.RemovalIntent)
			{
				ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleRemoval(book,
					op, LifecycleRemovalWorld(op)), op.Action + " removal receipt");
			}
			else if (op.Phase == KingdomLifecyclePhase.DomainIntent)
			{
				if (op.Lane == KingdomLifecycleLane.Raid)
					ClassicAssert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.ProveDomain(book, op));
				else SettleLeaseKind(book, op, KingdomLifecycleResourceKind.None, true);
			}
			else if (op.Phase == KingdomLifecyclePhase.EffectIntent)
			{
				op.EffectState = KingdomLifecyclePhysicalState.Proved;
				op.PlunderProved = op.PlunderRequested;
			}
			else if (op.Phase == KingdomLifecyclePhase.Sinks)
			{
				Deliver(op.Outbox);
			}
			else if (op.Phase == KingdomLifecyclePhase.ScheduleIntent)
			{
				if (op.Lane == KingdomLifecycleLane.Raid)
					ClassicAssert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.ProveSchedule(book, op));
				else ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(book,
					op, LifecycleScheduleWorld(book, op)), op.Action + " schedule receipt");
			}
		}

		private static void SettleLeaseKind(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, KingdomLifecycleResourceKind kind, bool domain)
		{
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				bool special = lease.Kind == KingdomLifecycleResourceKind.Schedule
					|| lease.Kind == KingdomLifecycleResourceKind.WaterVessel
					|| lease.Kind == KingdomLifecycleResourceKind.Projection
					|| lease.Kind == KingdomLifecycleResourceKind.Object;
				if (domain ? special : lease.Kind != kind) continue;
				SettleLease(book, lease);
			}
		}

		private static void SettleLease(KingdomLifecycleBook book,
			KingdomLifecycleResourceLease lease)
		{
			ClassicAssert.NotNull(lease);
			bool began = KingdomLifecycleRules.BeginLease(book, lease, lease.Before);
			ClassicAssert.IsTrue(began,
				lease.Kind + " begin");
			bool committed = KingdomLifecycleRules.CommitLeaseWitness(book, lease, lease.After);
			ClassicAssert.IsTrue(committed,
				lease.Kind + " confirm");
		}

		private static void SettleProjectionLease(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection)
		{
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleProjection(book,
				op, projection, LifecycleProjectionWorld(projection)),
				op.Action + " projection receipt");
		}

		private static void SettleCarrySchedule(KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op,
				ScheduleWorld(book, op, op.ScheduleLease.Before,
					op.ScheduleLease.BeforeRevision, null)));
		}

		private static void RemoveCarrySources(KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			if (op.Phase == KingdomLifecyclePhase.Prepared)
				ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
					KingdomLifecyclePhase.RemovalIntent, 2L));
			for (int i = 0; i < op.Sources.Count; i++)
			{
				KingdomCarrySource source = op.Sources[i];
				while (source.Removed < source.PlannedCount)
					ClassicAssert.IsTrue(ProveCarryUnit(book, op, source));
			}
		}

		private static void ReadyCarryProjection(KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			RemoveCarrySources(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			SettleCarrySchedule(book, op);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 5L));
		}

		private static void Deliver(KingdomLifecycleOutbox box)
		{
			if (box.ChronicleState != KingdomLifecycleSinkState.Skipped)
				box.ChronicleState = KingdomLifecycleSinkState.Delivered;
			if (box.LedgerState != KingdomLifecycleSinkState.Skipped)
				box.LedgerState = KingdomLifecycleSinkState.Delivered;
			if (box.MessageState != KingdomLifecycleSinkState.Skipped)
				box.MessageState = KingdomLifecycleSinkState.Delivered;
			if (box.DeedState != KingdomLifecycleSinkState.Skipped)
				box.DeedState = KingdomLifecycleSinkState.Delivered;
			if (box.GuestbookState != KingdomLifecycleSinkState.Skipped)
				box.GuestbookState = KingdomLifecycleSinkState.Delivered;
		}

		private static void DeliverCarrySinks(KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			ClassicAssert.IsTrue(KingdomLifecycleRules.RecoverCarryOutbox(book, op));
			KingdomLifecycleSinkMask[] sinks = new[]
			{
				KingdomLifecycleSinkMask.Chronicle, KingdomLifecycleSinkMask.Ledger,
				KingdomLifecycleSinkMask.Message, KingdomLifecycleSinkMask.Deed,
				KingdomLifecycleSinkMask.Guestbook
			};
			for (int i = 0; i < sinks.Length; i++)
			{
				KingdomLifecycleSinkState state = CarrySinkState(op.Outbox, sinks[i]);
				if (state == KingdomLifecycleSinkState.Skipped) continue;
				ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, state);
				ClassicAssert.IsTrue(KingdomLifecycleRules.BeginCarrySink(book, op, sinks[i]));
				ClassicAssert.IsTrue(KingdomLifecycleRules.CommitCarrySink(book, op, sinks[i]));
			}
		}

		private static KingdomLifecycleSinkState CarrySinkState(KingdomLifecycleOutbox box,
			KingdomLifecycleSinkMask sink)
		{
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: return box.ChronicleState;
			case KingdomLifecycleSinkMask.Ledger: return box.LedgerState;
			case KingdomLifecycleSinkMask.Message: return box.MessageState;
			case KingdomLifecycleSinkMask.Deed: return box.DeedState;
			case KingdomLifecycleSinkMask.Guestbook: return box.GuestbookState;
			default: return KingdomLifecycleSinkState.None;
			}
		}

		private static KingdomLifecycleProjection Projection(KingdomLifecycleOperation op,
			int ordinal, int material, int count)
		{
			return new KingdomLifecycleProjection
			{
				OperationId = op.Id,
				EventId = KingdomLifecycleRules.ChildId(op.Id, "projection", ordinal),
				ObjectId = KingdomLifecycleRules.ChildId(op.Id, "object", ordinal),
				Marker = KingdomLifecycleRules.ChildId(op.Id, "marker", ordinal),
				Blueprint = material < 0 ? "Snapjaw" : "Material",
				ZoneId = "zone-a",
				Topology = KingdomLifecycleTopology.Cell,
				X = ordinal,
				Y = 0,
				Material = material,
				Count = count,
				NoStack = true,
				State = KingdomLifecyclePhysicalState.Prepared
			};
		}

		private static KingdomLifecycleLane FirstLane(KingdomLifecycleAction action)
		{
			foreach (KingdomLifecycleLane lane in Enum.GetValues(typeof(KingdomLifecycleLane)))
				if (KingdomLifecycleRules.ActionAllowedInLane(action, lane)) return lane;
			return KingdomLifecycleLane.None;
		}

		private static KingdomCarryBook CarryBook()
		{
			KingdomCarryBook book = new KingdomCarryBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindCarryIdentity(book, "realm-a",
				new List<string> { "city-b", "city-a" }, false, null));
			return book;
		}

		private static KingdomLifecycleResourceLease CopyLease(
			KingdomLifecycleResourceLease source)
		{
			return new KingdomLifecycleResourceLease
			{
				OperationId = source.OperationId,
				Kind = source.Kind,
				ScopeId = source.ScopeId,
				SubjectId = source.SubjectId,
				Key = source.Key,
				Before = source.Before,
				Delta = source.Delta,
				After = source.After,
				BeforeRevision = source.BeforeRevision,
				AfterRevision = source.AfterRevision,
				State = source.State
			};
		}

		private static KingdomCarrySource CopySource(KingdomCarrySource source)
		{
			return new KingdomCarrySource
			{
				OperationId = source.OperationId,
				SourceEventId = source.SourceEventId,
				ObjectId = source.ObjectId,
				Blueprint = source.Blueprint,
				Topology = source.Topology,
				OwnerId = source.OwnerId,
				ZoneId = source.ZoneId,
				X = source.X,
				Y = source.Y,
				Material = source.Material,
				OriginalCount = source.OriginalCount,
				PlannedCount = source.PlannedCount,
				Removed = source.Removed,
				UnitCursor = source.UnitCursor,
				UnitBefore = source.UnitBefore,
				UnitAfter = source.UnitAfter,
				UnitEventId = source.UnitEventId,
				UnitState = source.UnitState,
				ReceiptId = source.ReceiptId,
				ReceiptTopologyId = source.ReceiptTopologyId,
				ReceiptBeforeIdMatches = source.ReceiptBeforeIdMatches,
				ReceiptAfterIdMatches = source.ReceiptAfterIdMatches,
				ReceiptBeforeCount = source.ReceiptBeforeCount,
				ReceiptAfterCount = source.ReceiptAfterCount,
				ReceiptSameReference = source.ReceiptSameReference,
				ReceiptProofId = source.ReceiptProofId,
				ReceiptChainId = source.ReceiptChainId,
				ReceiptChainCount = source.ReceiptChainCount,
				ReceiptState = source.ReceiptState,
				State = source.State
			};
		}

		private static KingdomCarryOperation BuildCarry(KingdomCarryBook book,
			long tick, int original, int planned)
		{
			KingdomCarryOperation op = KingdomLifecycleRules.PrepareCarry(book, tick);
			ClassicAssert.NotNull(op);
			op.OriginSettlementId = "city-a";
			op.OriginZoneId = "zone-a";
			op.OriginX = 1;
			op.OriginY = 2;
			op.DestinationSettlementId = "city-b";
			op.DestinationSettlementName = "B";
			op.DueTick = 100L;
			op.Sources.Add(KingdomLifecycleRules.PrepareCarrySource(op, 0, "source-object",
				"Mudroot", KingdomLifecycleTopology.Inventory, "wagon", "zone-a", -1, -1,
				0, original, planned));
			op.Outputs.Add(KingdomLifecycleRules.PrepareCarryOutput(op, 0,
				KingdomLifecycleRules.ChildId(op.Id, "output", 0), "Mudroot",
				KingdomLifecycleTopology.Inventory, "destination-store", "zone-b",
				-1, -1, 0, planned));
			op.Mud = planned;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.PrepareCarrySchedule(book, op,
				ScheduleWorld(book, op, 99L, 0L, null)));
			ClassicAssert.NotNull(op.ScheduleLease);
			op.Outbox = new KingdomLifecycleOutbox
			{
				OperationId = op.Id,
				EventId = KingdomLifecycleRules.ChildId(op.Id, "outbox", 0),
				ChronicleReceiptId = KingdomLifecycleRules.ChildId(op.Id, "chronicle", 0),
				Chronicle = "chronicle",
				ChronicleDisposition = KingdomLifecycleSinkDisposition.Deliver,
				ChronicleState = KingdomLifecycleSinkState.Pending,
				Ledger = "ledger", LedgerDisposition = KingdomLifecycleSinkDisposition.Deliver,
				LedgerState = KingdomLifecycleSinkState.Pending,
				Message = "message", MessageDisposition = KingdomLifecycleSinkDisposition.Deliver,
				MessageState = KingdomLifecycleSinkState.Pending,
				DeedDisposition = KingdomLifecycleSinkDisposition.Skip,
				DeedState = KingdomLifecycleSinkState.Skipped,
				GuestbookDisposition = KingdomLifecycleSinkDisposition.Skip,
				GuestbookState = KingdomLifecycleSinkState.Skipped
			};
			return op;
		}

		private static KingdomCarryOperation BuildExactCarry(KingdomCarryBook book,
			long tick, string blueprint, int count)
		{
			KingdomCarryOperation op = KingdomLifecycleRules.PrepareExactCarry(book, tick);
			ClassicAssert.NotNull(op);
			op.OriginSettlementId = "city-a";
			op.OriginZoneId = "zone-a";
			op.OriginX = 1; op.OriginY = 2;
			op.DestinationSettlementId = "city-b";
			op.DestinationSettlementName = "B";
			op.DueTick = 100L;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TrustedAdapter.PrepareCarrySchedule(book, op,
				ScheduleWorld(book, op, 99L, 0L, null)));
			op.SpillZoneId = "zone-b"; op.SpillX = 5; op.SpillY = 6;
			KingdomCarrySource source = KingdomLifecycleRules.PrepareExactCarrySource(op, 0,
				"source-exact", blueprint, KingdomLifecycleTopology.Inventory,
				"source-container", "zone-a", -1, -1, count);
			ClassicAssert.NotNull(source); op.Sources.Add(source);
			KingdomLifecycleProjection output = KingdomLifecycleRules.PrepareExactCarryOutput(op,
				0, source, KingdomLifecycleTopology.Inventory, "destination-store", "zone-b", -1, -1);
			ClassicAssert.NotNull(output); op.Outputs.Add(output);
			op.Outbox = new KingdomLifecycleOutbox
			{
				OperationId = op.Id,
				EventId = KingdomLifecycleRules.ChildId(op.Id, "outbox", 0),
				ChronicleReceiptId = KingdomLifecycleRules.ChildId(op.Id, "chronicle", 0),
				Chronicle = "exact carry arrived",
				ChronicleDisposition = KingdomLifecycleSinkDisposition.Deliver,
				ChronicleState = KingdomLifecycleSinkState.Pending,
				Ledger = "exact carry ledger",
				LedgerDisposition = KingdomLifecycleSinkDisposition.Deliver,
				LedgerState = KingdomLifecycleSinkState.Pending,
				Message = "exact carry message",
				MessageDisposition = KingdomLifecycleSinkDisposition.Deliver,
				MessageState = KingdomLifecycleSinkState.Pending,
				DeedDisposition = KingdomLifecycleSinkDisposition.Skip,
				DeedState = KingdomLifecycleSinkState.Skipped,
				GuestbookDisposition = KingdomLifecycleSinkDisposition.Skip,
				GuestbookState = KingdomLifecycleSinkState.Skipped
			};
			ClassicAssert.IsTrue(KingdomLifecycleRules.FreezeExactCarryManifest(op, "sign-one",
				"r_KingdomCarrySign", KingdomLifecycleTopology.Inventory, "actor-one", "zone-a",
				-1, -1, 1, new List<int> { 1 }, new List<int> { 1 }));
			return op;
		}

		private static TrustedWorld ExactSignWorld(KingdomCarryOperation op)
		{
			TrustedWorld world = new TrustedWorld();
			TrustedObservation row = new TrustedObservation
			{
				ReferenceValue = new object(), ObjectIdValue = op.SignObjectId,
				BlueprintValue = op.SignBlueprint, OwnerIdValue = op.SignOwnerId,
				ZoneIdValue = op.SignZoneId, TopologyValue = op.SignTopology,
				XValue = op.SignX, YValue = op.SignY, CountValue = op.SignCount
			};
			world.Rows.Add(row);
			world.CarrySignRemovalCallback = delegate(object reference, int count, string receipt)
			{
				row.CountValue -= count;
				if (row.CountValue == 0) world.Rows.Remove(row);
				return reference;
			};
			return world;
		}

		private static TrustedWorld ExactSourceWorld(KingdomCarrySource source)
		{
			TrustedWorld world = new TrustedWorld();
			world.Rows.Add(new TrustedObservation
			{
				ReferenceValue = new object(), ObjectIdValue = source.ObjectId,
				BlueprintValue = source.Blueprint, OwnerIdValue = source.CurrentOwnerId,
				ZoneIdValue = source.CurrentZoneId, TopologyValue = source.CurrentTopology,
				XValue = source.CurrentX, YValue = source.CurrentY,
				CountValue = source.PlannedCount
			});
			return world;
		}

		private static void MoveExactOnCallback(TrustedWorld world)
		{
			world.CarryMoveCallback = delegate(object reference, int trip,
				KingdomLifecycleTopology topology, string owner, string zone,
				int x, int y, string receipt)
			{
				MoveObservation(world.Rows[0], topology, owner, zone, x, y);
				return reference;
			};
		}

		private static void MoveObservation(TrustedObservation row,
			KingdomLifecycleTopology topology, string owner, string zone, int x, int y)
		{
			row.TopologyValue = topology; row.OwnerIdValue = owner; row.ZoneIdValue = zone;
			row.XValue = x; row.YValue = y;
		}

		private static TrustedWorld ScheduleWorld(KingdomCarryBook book,
			KingdomCarryOperation op, long value, long revision, string lastOperationId)
		{
			TrustedObservation row = new TrustedObservation
			{
				ReferenceValue = new object(),
				ObjectIdValue = KingdomLifecycleRules.ResourceKey(
					KingdomLifecycleResourceKind.Schedule, book.RealmId, op.DestinationSettlementId),
				BlueprintValue = "Schedule",
				SettlementIdValue = op.DestinationSettlementId,
				ZoneIdValue = string.IsNullOrEmpty(op.DestinationZoneId) ? "zone-b" : op.DestinationZoneId,
				TopologyValue = op.DestinationTopology == KingdomLifecycleTopology.None
					? KingdomLifecycleTopology.Cell : op.DestinationTopology,
				OwnerIdValue = op.DestinationTopology == KingdomLifecycleTopology.Inventory
					? op.DestinationOwnerId : null,
				XValue = op.DestinationTopology == KingdomLifecycleTopology.Inventory
					? -1 : op.DestinationX < 0 ? 3 : op.DestinationX,
				YValue = op.DestinationTopology == KingdomLifecycleTopology.Inventory
					? -1 : op.DestinationY < 0 ? 4 : op.DestinationY,
				ValueValue = value,
				RevisionValue = revision,
				LastOperationIdValue = lastOperationId
			};
			TrustedWorld world = new TrustedWorld();
			world.Rows.Add(row);
			world.ScheduleCallback = delegate(object reference, long after, string operationId)
			{
				row.ValueValue = after;
				row.RevisionValue++;
				row.LastOperationIdValue = operationId;
				return reference;
			};
			return world;
		}

		private static TrustedObservation OutputObservation(KingdomLifecycleProjection output,
			object reference)
		{
			return new TrustedObservation
			{
				ReferenceValue = reference,
				ObjectIdValue = output.ObjectId,
				MarkerValue = output.Marker,
				BlueprintValue = output.Blueprint,
				OwnerIdValue = output.OwnerId,
				ZoneIdValue = output.ZoneId,
				TopologyValue = output.Topology,
				XValue = output.X,
				YValue = output.Y,
				CountValue = output.Count
			};
		}

		private static TrustedWorld OutputWorld(KingdomLifecycleProjection output)
		{
			TrustedWorld world = new TrustedWorld();
			world.OutputCallback = delegate(KingdomLifecycleProjection value)
			{
				object reference = new object();
				world.Rows.Add(OutputObservation(value, reference));
				return reference;
			};
			return world;
		}

		private static TrustedWorld WaterWorld(KingdomLifecycleWaterLeg leg)
		{
			TrustedObservation vessel = new TrustedObservation
			{
				ReferenceValue = new object(),
				ObjectIdValue = leg.OwnerId,
				BlueprintValue = leg.Blueprint,
				ZoneIdValue = leg.ZoneId,
				CapacityValue = leg.Capacity,
				CompositionValue = leg.Composition,
				ValueValue = leg.Before
			};
			TrustedWorld world = new TrustedWorld();
			world.Rows.Add(vessel);
			world.WaterCallback = delegate(object reference, int amount)
			{
				vessel.ValueValue -= amount;
				return reference;
			};
			return world;
		}

		private static bool ProveCarryUnit(KingdomCarryBook book,
			KingdomCarryOperation operation, KingdomCarrySource source)
		{
			return KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, operation,
				source, CarrySourceWorld(source));
		}

		private static TrustedWorld CarrySourceWorld(KingdomCarrySource source)
		{
			TrustedObservation row = new TrustedObservation
			{
				ReferenceValue = new object(), ObjectIdValue = source.ObjectId,
				BlueprintValue = source.Blueprint, OwnerIdValue = source.OwnerId,
				ZoneIdValue = source.ZoneId, TopologyValue = source.Topology,
				XValue = source.X, YValue = source.Y, CountValue = source.UnitBefore
			};
			TrustedWorld world = new TrustedWorld();
			world.Rows.Add(row);
			world.CarryRemovalCallback = delegate(object reference, int count, string eventId)
			{
				row.CountValue -= count;
				return reference;
			};
			return world;
		}

		private static TrustedWorld LifecycleProjectionWorld(KingdomLifecycleProjection projection)
		{
			TrustedWorld world = new TrustedWorld();
			world.LifecycleProjectionCallback = delegate(KingdomLifecycleProjection value)
			{
				object reference = new object();
				TrustedObservation row = OutputObservation(value, reference);
				if (!string.IsNullOrEmpty(world.ProjectionBlueprintOverride))
					row.BlueprintValue = world.ProjectionBlueprintOverride;
				world.Rows.Add(row);
				return reference;
			};
			return world;
		}

		private static TrustedWorld LifecycleRemovalWorld(KingdomLifecycleOperation operation)
		{
			TrustedObservation row = new TrustedObservation
			{
				ReferenceValue = new object(), ObjectIdValue = operation.ObjectId,
				BlueprintValue = operation.Blueprint, OwnerIdValue = operation.ObjectOwnerId,
				ZoneIdValue = operation.ZoneId, TopologyValue = operation.ObjectTopology,
				XValue = operation.ObjectX, YValue = operation.ObjectY, CountValue = operation.Count
			};
			TrustedWorld world = new TrustedWorld();
			world.Rows.Add(row);
			world.LifecycleRemovalCallback = delegate(object reference, int count, string operationId)
			{
				row.CountValue -= count;
				return reference;
			};
			return world;
		}

		private static TrustedWorld LifecycleScheduleWorld(KingdomLifecycleBook book,
			KingdomLifecycleOperation operation)
		{
			KingdomLifecycleResourceLease lease = operation.ResourceLeases.Find(value =>
				value.Kind == KingdomLifecycleResourceKind.Schedule);
			KingdomLifecycleResourceRevision resource = book.Resources.Find(value =>
				value.Key == lease.Key);
			TrustedObservation row = new TrustedObservation
			{
				ReferenceValue = new object(), ObjectIdValue = lease.Key,
				BlueprintValue = "Schedule", SettlementIdValue = operation.SettlementId,
				ZoneIdValue = operation.ZoneId, TopologyValue = KingdomLifecycleTopology.Cell,
				XValue = 0, YValue = 0, ValueValue = lease.Before,
				RevisionValue = lease.BeforeRevision,
				LastOperationIdValue = resource == null ? null : resource.LastOperationId
			};
			TrustedWorld world = new TrustedWorld();
			world.Rows.Add(row);
			world.ScheduleCallback = delegate(object reference, long after, string operationId)
			{
				row.ValueValue = after;
				row.RevisionValue++;
				row.LastOperationIdValue = operationId;
				return reference;
			};
			return world;
		}

		private static KingdomLifecycleBook RoundTrip(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteLifecycle(writer, book);
				stream.Position = 0;
				KingdomLifecycleBook result = new KingdomLifecycleBook();
				KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(stream), result);
				return result;
			}
		}

		private static KingdomCarryBook RoundTrip(KingdomCarryBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteCarry(writer, book);
				stream.Position = 0;
				KingdomCarryBook result = new KingdomCarryBook();
				KingdomLifecycleWireCodec.ReadCarry(new BinaryReader(stream), result);
				return result;
			}
		}

		private static byte[] CarryBytes(KingdomCarryBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream,
					System.Text.Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteCarry(writer, book);
				return stream.ToArray();
			}
		}

		private static byte[] CarryV5Bytes(KingdomCarryBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream,
					System.Text.Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteCarryV5Fixture(writer, book);
				return stream.ToArray();
			}
		}

		private static string Sha256(byte[] bytes)
		{
			using (SHA256 hash = SHA256.Create())
				return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "")
					.ToLowerInvariant();
		}

		private static byte[] LifecycleBytes(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream,
					System.Text.Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteLifecycle(writer, book);
				return stream.ToArray();
			}
		}

		private static byte[] LifecycleBytesWithHostileArrayWriter(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new HostileArrayWriter(stream))
					KingdomLifecycleWireCodec.WriteLifecycle(writer, book);
				return stream.ToArray();
			}
		}

		private static byte[] CarryBytesWithHostileArrayWriter(KingdomCarryBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new HostileArrayWriter(stream))
					KingdomLifecycleWireCodec.WriteCarry(writer, book);
				return stream.ToArray();
			}
		}

		private sealed class HostileArrayWriter : BinaryWriter
		{
			public HostileArrayWriter(Stream stream)
				: base(stream, System.Text.Encoding.UTF8, true)
			{
			}

			public override void Write(byte[] buffer)
			{
				throw new InvalidOperationException("typed-array framing was invoked");
			}
		}

		private sealed class TrustedObservation : IKingdomLifecycleTrustedObservation
		{
			public object ReferenceValue;
			public string ObjectIdValue;
			public string MarkerValue;
			public string BlueprintValue;
			public string SettlementIdValue;
			public string OwnerIdValue;
			public string ZoneIdValue;
			public KingdomLifecycleTopology TopologyValue;
			public int XValue = -1;
			public int YValue = -1;
			public int CountValue;
			public int CapacityValue;
			public string CompositionValue;
			public long ValueValue;
			public long RevisionValue;
			public string LastOperationIdValue;

			public object Reference { get { return ReferenceValue; } }
			public string ObjectId { get { return ObjectIdValue; } }
			public string Marker { get { return MarkerValue; } }
			public string Blueprint { get { return BlueprintValue; } }
			public string SettlementId { get { return SettlementIdValue; } }
			public string OwnerId { get { return OwnerIdValue; } }
			public string ZoneId { get { return ZoneIdValue; } }
			public KingdomLifecycleTopology Topology { get { return TopologyValue; } }
			public int X { get { return XValue; } }
			public int Y { get { return YValue; } }
			public int Count { get { return CountValue; } }
			public int Capacity { get { return CapacityValue; } }
			public string Composition { get { return CompositionValue; } }
			public long Value { get { return ValueValue; } }
			public long Revision { get { return RevisionValue; } }
			public string LastOperationId { get { return LastOperationIdValue; } }
		}

		private sealed class TrustedWorld : IKingdomLifecycleTrustedWorld
		{
			public readonly List<TrustedObservation> Rows = new List<TrustedObservation>();
			public Func<KingdomLifecycleProjection, object> OutputCallback;
			public Func<object, int, object> WaterCallback;
			public Func<object, long, string, object> ScheduleCallback;
			public Func<object, int, string, object> CarryRemovalCallback;
			public Func<object, int, string, object> CarrySignRemovalCallback;
			public Func<object, int, KingdomLifecycleTopology, string, string,
				int, int, string, object> CarryMoveCallback;
			public Func<KingdomLifecycleProjection, object> LifecycleProjectionCallback;
			public Func<object, int, string, object> LifecycleRemovalCallback;
			public object OutputReturnOverride;
			public string ProjectionBlueprintOverride;
			public bool DisableWaterCallback;
			public int ObservationCountReads;

			public int ObservationCount
			{
				get { ObservationCountReads++; return Rows.Count; }
			}
			public IKingdomLifecycleTrustedObservation Observe(int index) { return Rows[index]; }
			public object InvokeCarryOutput(KingdomLifecycleProjection output)
			{
				object value = OutputCallback == null ? null : OutputCallback(output);
				return OutputReturnOverride ?? value;
			}
			public object InvokeWater(object vesselReference, int amount)
			{
				return DisableWaterCallback || WaterCallback == null
					? null : WaterCallback(vesselReference, amount);
			}
			public object InvokeSchedule(object scheduleReference, long dueTick, string operationId)
			{
				return ScheduleCallback == null ? null
					: ScheduleCallback(scheduleReference, dueTick, operationId);
			}
			public object InvokeCarryRemoval(object sourceReference, int count, string unitEventId)
			{
				return CarryRemovalCallback == null ? null
					: CarryRemovalCallback(sourceReference, count, unitEventId);
			}
			public object InvokeCarrySignRemoval(object signReference, int count, string receiptId)
			{
				return CarrySignRemovalCallback == null ? null
					: CarrySignRemovalCallback(signReference, count, receiptId);
			}
			public object InvokeCarryMove(object sourceReference, int tripId,
				KingdomLifecycleTopology targetTopology, string targetOwnerId,
				string targetZoneId, int targetX, int targetY, string receiptId)
			{
				return CarryMoveCallback == null ? null : CarryMoveCallback(sourceReference,
					tripId, targetTopology, targetOwnerId, targetZoneId, targetX, targetY,
					receiptId);
			}
			public object InvokeLifecycleProjection(KingdomLifecycleProjection projection)
			{
				return LifecycleProjectionCallback == null ? null
					: LifecycleProjectionCallback(projection);
			}
			public object InvokeLifecycleRemoval(object objectReference, int count, string operationId)
			{
				return LifecycleRemovalCallback == null ? null
					: LifecycleRemovalCallback(objectReference, count, operationId);
			}
		}

		private sealed class MutatingCollection : ICollection<string>
		{
			private readonly List<string> values;
			private readonly Action onEnumerate;

			public MutatingCollection(List<string> Values, Action OnEnumerate)
			{
				values = Values;
				onEnumerate = OnEnumerate;
			}

			public int Count { get { return values.Count; } }
			public bool IsReadOnly { get { return true; } }
			public bool Contains(string item) { return values.Contains(item); }
			public void CopyTo(string[] array, int arrayIndex) { values.CopyTo(array, arrayIndex); }
			public IEnumerator<string> GetEnumerator()
			{
				onEnumerate();
				return values.GetEnumerator();
			}
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
			public void Add(string item) { throw new NotSupportedException(); }
			public void Clear() { throw new NotSupportedException(); }
			public bool Remove(string item) { throw new NotSupportedException(); }
		}
	}
}
#endif
