#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomLifecycleRulesTests
	{
		private static KingdomLifecycleBook Book(string id = "city-a")
		{
			KingdomLifecycleBook book = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(book, id, false,
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
					Assert.AreEqual(value, draft != null, action + " / " + lane);
				}
				Assert.Greater(allowed, 0, action.ToString());
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
				KingdomLifecycleOperation op = Build(book, lane, action, tick, tick);
				Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op), action.ToString());
				Assert.IsFalse(KingdomLifecycleRules.CanTransition(action,
					KingdomLifecyclePhase.Prepared, KingdomLifecyclePhase.Terminal), action.ToString());
				Settle(book, op, tick + 1L);
				Assert.AreEqual(KingdomLifecyclePhase.Terminal, op.Phase, action.ToString());
				Assert.IsTrue(KingdomLifecycleRules.Retire(book, op, tick + 100L), action.ToString());
				tick += 10L;
			}
		}

		[Test]
		public void LegalEdge_StillRefusesMissingPhysicalDomainSinkAndScheduleProof()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			Assert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Projected, 3L));
			Assert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Projected, 3L), "object witness alone lacks projection lease proof");
			SettleProjectionLease(book, op, op.Projections[0]);
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Projected, 3L));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.DomainIntent, 4L));
			Assert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.DomainSettled, 5L));
			SettleLease(book, op.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Population));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.DomainSettled, 5L));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Sinks, 6L));
			Assert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 7L));
			Deliver(op.Outbox);
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 7L));
			Assert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Terminal, 8L));
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(book,
				op, LifecycleScheduleWorld(book, op)));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Terminal, 8L));
		}

		[Test]
		public void PreparedPhysicalCall_RequiresExactBeforeAndNotAfter()
		{
			Assert.AreEqual(KingdomLifecycleMutationAction.InvokeOnce,
				KingdomLifecycleRules.MutationAction(KingdomLifecyclePhysicalState.Prepared, true, false));
			Assert.AreEqual(KingdomLifecycleMutationAction.Quarantine,
				KingdomLifecycleRules.MutationAction(KingdomLifecyclePhysicalState.Prepared, false, false));
			Assert.AreEqual(KingdomLifecycleMutationAction.Quarantine,
				KingdomLifecycleRules.MutationAction(KingdomLifecyclePhysicalState.Prepared, true, true));
			Assert.AreEqual(KingdomLifecycleMutationAction.ConfirmAfter,
				KingdomLifecycleRules.MutationAction(KingdomLifecyclePhysicalState.Intent, false, true));
			Assert.AreEqual(KingdomLifecycleMutationAction.Quarantine,
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

			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, guest));
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(book, raid), "persisted lease blocks overlap");
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, guest,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			SettleProjectionLease(book, guest, guest.Projections[0]);
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, guest,
				KingdomLifecyclePhase.Projected, 3L));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, guest,
				KingdomLifecyclePhase.DomainIntent, 4L));
			Assert.IsTrue(KingdomLifecycleRules.BeginLease(book, guestShared, guestShared.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitLeaseWitness(book, guestShared, guestShared.After));
			Assert.AreEqual(KingdomLifecycleCasAction.Quarantine,
				KingdomLifecycleRules.LeaseAction(book, raidShared, guestShared.After),
				"same scalar after is not another op's proof");
		}

		[Test]
		public void LeaseMutation_RequiresExactPublishedMemberPhaseAndRowWitness()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			KingdomLifecycleResourceLease domain = op.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Raid);
			KingdomLifecycleResourceLease schedule = op.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Schedule);

			Assert.IsFalse(KingdomLifecycleRules.BeginLease(book, schedule, schedule.Before),
				"Prepared cannot debit the terminal schedule");
			Assert.AreEqual(KingdomLifecycleCasAction.Quarantine,
				KingdomLifecycleRules.LeaseAction(book, schedule, schedule.Before));
			Assert.IsFalse(KingdomLifecycleRules.CommitLeaseWitness(book, schedule, schedule.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.DomainIntent, 2L));
			KingdomLifecycleResourceLease forged = CopyLease(domain);
			Assert.IsFalse(KingdomLifecycleRules.BeginLease(book, forged, forged.Before),
				"equal fields are not exact lease membership");
			Assert.IsTrue(KingdomLifecycleRules.BeginLease(book, domain, domain.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitLeaseWitness(book, domain, domain.After));

			KingdomLifecycleBook mismatched = Book("city-mismatch");
			KingdomLifecycleOperation bad = Build(mismatched, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(mismatched, bad));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(mismatched, bad,
				KingdomLifecyclePhase.DomainIntent, 2L));
			KingdomLifecycleResourceLease badDomain = bad.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Raid);
			badDomain.State = KingdomLifecycleLeaseState.Proved;
			KingdomLifecycleBook reloaded = RoundTrip(mismatched);
			Assert.IsTrue(reloaded.Quarantined,
				"a Proved enum without the exact revision/last-op witness owns no authority");
		}

		[Test]
		public void ProjectionReceipt_RequiresCallbackExactMarkerObjectBlueprintAndTopology()
		{
			KingdomLifecycleBook noCallbackBook = Book("city-projection-no-callback");
			KingdomLifecycleOperation noCallbackOp = Build(noCallbackBook,
				KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(noCallbackBook, noCallbackOp));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(noCallbackBook, noCallbackOp,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleProjection(
				noCallbackBook, noCallbackOp, noCallbackOp.Projections[0], new TrustedWorld()));
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent,
				noCallbackOp.Projections[0].State);

			KingdomLifecycleBook wrongBlueprintBook = Book("city-projection-blueprint");
			KingdomLifecycleOperation wrongBlueprintOp = Build(wrongBlueprintBook,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Spawn, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(wrongBlueprintBook, wrongBlueprintOp));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(wrongBlueprintBook, wrongBlueprintOp,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			TrustedWorld wrongBlueprint = LifecycleProjectionWorld(wrongBlueprintOp.Projections[0]);
			wrongBlueprint.ProjectionBlueprintOverride = "ForeignBlueprint";
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleProjection(
				wrongBlueprintBook, wrongBlueprintOp, wrongBlueprintOp.Projections[0], wrongBlueprint));

			KingdomLifecycleBook book = Book("city-projection-happy");
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 2L));
			SettleProjectionLease(book, op, op.Projections[0]);
		}

		[Test]
		public void FrozenPlanAndQuarantinedBook_CannotAdvanceOrRetire()
		{
			KingdomLifecycleBook stale = Book();
			KingdomLifecycleOperation op = Build(stale, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(stale, op));
			op.Detail = "post-publication rewrite";
			Assert.IsFalse(KingdomLifecycleRules.AdvancePhase(stale, op,
				KingdomLifecyclePhase.Sinks, 2L));

			KingdomLifecycleBook terminal = Book("city-terminal");
			op = Build(terminal, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(terminal, op));
			Settle(terminal, op, 2L);
			op.Detail = "receipt no longer matches";
			Assert.IsFalse(KingdomLifecycleRules.Retire(terminal, op, 100L));
			Assert.AreSame(op, terminal.PlainGuest);

			op.Detail = null;
			terminal.Quarantined = true;
			Assert.IsFalse(KingdomLifecycleRules.Retire(terminal, op, 100L));
		}

		[Test]
		public void DraftFailure_DoesNotConsumeCounterOrMintCompetingId()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			string canonical = op.Id;
			op.Id = KingdomLifecycleRules.ChildId(canonical, "forged", 0);
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(book, op));
			Assert.AreEqual(1L, book.RaidNextSequence);
			Assert.IsNull(book.Raid);
			op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			Assert.AreEqual(canonical, op.Id);
		}

		[Test]
		public void RetirementRefusesUnresolvedQuarantinedAndValueClaims()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			op.Phase = KingdomLifecyclePhase.Terminal;
			Assert.IsFalse(KingdomLifecycleRules.Retire(book, op, 2L));
			Assert.AreSame(op, book.NotableGuest);
			Assert.IsTrue(KingdomLifecycleRules.Quarantine(op, "uncertain debit"));
			Assert.IsFalse(KingdomLifecycleRules.Retire(book, op, 3L));
			Assert.AreSame(op, book.NotableGuest, "full quarantined value evidence is retained");
			Assert.AreEqual(op.Id, book.Resources[0].ActiveOperationId);
		}

		[Test]
		public void OutOfOrderLaneRetirement_UsesPerLaneBarriers()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation slow = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, slow));
			for (int i = 0; i < 8; i++)
			{
				KingdomLifecycleOperation fast = Build(book, KingdomLifecycleLane.Raid,
					KingdomLifecycleAction.RaidWarning, i + 2L, i);
				Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, fast));
				Settle(book, fast, i + 20L);
				Assert.IsTrue(KingdomLifecycleRules.Retire(book, fast, i + 30L));
			}
			KingdomLifecycleRules.Normalize(book);
			Assert.IsFalse(book.Quarantined);
			Assert.AreSame(slow, book.PlainGuest);
			Assert.AreEqual(0L, book.PlainGuestRetiredThrough);
			Assert.AreEqual(8L, book.RaidRetiredThrough);
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
				Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op), "publish " + i);
				if (i == 0) first = op.Id;
				Settle(book, op, i + 200L);
				Assert.IsTrue(KingdomLifecycleRules.Retire(book, op, i + 300L), "retire " + i);
			}
			Assert.AreEqual(96L, book.PlainGuestRetiredThrough);
			Assert.AreEqual(97L, book.PlainGuestNextSequence);
			Assert.AreEqual(KingdomLifecycleRules.MaxRecentProofs, book.RecentProofs.Count);
			Assert.IsFalse(book.RecentProofs.Exists(p => p.Id == first));
		}

		[Test]
		public void DuplicateCanonicalProof_IsQuarantinedWithoutTailRewrite()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 0L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			Settle(book, op, 2L);
			Assert.IsTrue(KingdomLifecycleRules.Retire(book, op, 30L));
			book.RecentProofs.Add(book.RecentProofs[0]);
			KingdomLifecycleRules.Normalize(book);
			Assert.IsTrue(book.Quarantined);
			Assert.AreEqual(2, book.RecentProofs.Count, "raw duplicate evidence remains visible");
		}

		[Test]
		public void WaterAndStandingConservation_RejectOverflowAndAmbiguity()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.WaterConserved(op, false));
			op.WaterAmbiguous = 1;
			Assert.IsFalse(KingdomLifecycleRules.WaterConserved(op, false));
			op.WaterAmbiguous = 0;
			op.WaterOutstanding = 0;
			op.WaterLost = 1;
			Assert.IsTrue(KingdomLifecycleRules.WaterConserved(op, false),
				"explicit loss replaces outstanding water; it is not extra water");
			Assert.IsFalse(KingdomLifecycleRules.WaterConserved(op, true),
				"loss evidence cannot retire as a proved debit");
			op.WaterOutstanding = 1;
			Assert.IsFalse(KingdomLifecycleRules.WaterConserved(op, false),
				"lost plus outstanding cannot exceed the request");
			long ignored;
			Assert.IsFalse(KingdomLifecycleRules.CheckedAdd(long.MaxValue, 1L, out ignored));
			Assert.IsNull(KingdomLifecycleRules.PrepareLease(book, op,
				KingdomLifecycleResourceKind.Standing, "city-a", "faction-a", long.MaxValue, 1L));
		}

		[Test]
		public void CarryPerUnitIntentReload_CannotMintEscrowWithoutCallbackReceipt()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 3, 2);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			KingdomCarrySource source = op.Sources[0];
			TrustedWorld noCallback = CarrySourceWorld(source);
			noCallback.CarryRemovalCallback = null;
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op,
				source, noCallback));

			KingdomCarryBook reloaded = RoundTrip(book);
			op = reloaded.Open; source = op.Sources[0];
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, source.UnitState);
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(reloaded, op,
				source, CarrySourceWorld(source)));
			Assert.AreEqual(0, source.Removed);
			Assert.AreEqual(0, op.EscrowMud);

			KingdomCarryBook happy = CarryBook();
			KingdomCarryOperation happyOp = BuildCarry(happy, 1L, 3, 2);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(happy, happyOp));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(happy, happyOp,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			Assert.IsTrue(ProveCarryUnit(happy, happyOp, happyOp.Sources[0]));
			Assert.IsTrue(ProveCarryUnit(happy, happyOp, happyOp.Sources[0]));
			Assert.AreEqual(2, happyOp.EscrowMud);
			Assert.IsTrue(KingdomLifecycleRules.CarryConserved(happyOp));
		}

		[Test]
		public void CarryIdentityProjection_ForcesNoStackAndExactTopology()
		{
			KingdomCarryBook stackBook = CarryBook();
			KingdomCarryOperation stack = BuildCarry(stackBook, 1L, 3, 2);
			stack.Outputs[0].NoStack = false;
			Assert.IsFalse(KingdomLifecycleRules.TryPublishCarry(stackBook, stack));
			Assert.AreEqual(1L, stackBook.NextSequence);

			KingdomCarryBook topologyBook = CarryBook();
			KingdomCarryOperation topology = BuildCarry(topologyBook, 1L, 3, 2);
			topology.Outputs[0].Topology = KingdomLifecycleTopology.Inventory;
			topology.Outputs[0].X = 4;
			Assert.IsFalse(KingdomLifecycleRules.TryPublishCarry(topologyBook, topology));

			KingdomCarryBook collisionBook = CarryBook();
			KingdomCarryOperation collision = BuildCarry(collisionBook, 1L, 3, 2);
			collision.Outputs[0].ObjectId = collision.Sources[0].ObjectId;
			Assert.IsFalse(KingdomLifecycleRules.TryPublishCarry(collisionBook, collision),
				"a partial source survivor and output cannot share one global object id");

			KingdomCarryBook realmBook = CarryBook();
			KingdomCarryOperation realm = BuildCarry(realmBook, 1L, 3, 2);
			realm.SettlementIds.RemoveAt(0);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishCarry(realmBook, realm),
				"carry plan freezes the full sorted realm settlement topology");
		}

		[Test]
		public void CarryMutation_RequiresOpenMemberFrozenPlanAndLegalPhase()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 3, 2);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			KingdomCarrySource source = op.Sources[0];
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op, source,
				CarrySourceWorld(source)),
				"Prepared has no physical removal authority");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			TrustedWorld wrongBlueprint = CarrySourceWorld(source);
			wrongBlueprint.Rows[0].BlueprintValue = "ForeignBlueprint";
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op,
				source, wrongBlueprint), "wrong blueprint cannot remove or escrow");
			KingdomCarrySource forged = CopySource(source);
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op, forged,
				CarrySourceWorld(forged)),
				"equal source fields are not exact source membership");
			string destination = op.DestinationSettlementName;
			op.DestinationSettlementName = "rewritten destination";
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySource(book, op, source,
				CarrySourceWorld(source)));
			op.DestinationSettlementName = destination;
			Assert.IsTrue(ProveCarryUnit(book, op, source));
		}

		[Test]
		public void CarryConservationAndRetirement_RefuseNonzeroEscrow()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 2, 2);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			KingdomCarrySource source = op.Sources[0];
			while (source.Removed < source.PlannedCount)
				Assert.IsTrue(ProveCarryUnit(book, op, source));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			op.Phase = KingdomLifecyclePhase.Terminal;
			Assert.IsFalse(KingdomLifecycleRules.RetireCarry(book, op, 4L));
			Assert.AreSame(op, book.Open);
			op.EscrowMud++;
			Assert.IsFalse(KingdomLifecycleRules.CarryConserved(op));
		}

		[Test]
		public void CarryHappyPath_ConservesSourceOutputEscrowAndLoss()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 2, 2);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			while (op.Sources[0].Removed < op.Sources[0].PlannedCount)
				Assert.IsTrue(ProveCarryUnit(book, op, op.Sources[0]));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			SettleCarrySchedule(book, op);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 5L));
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(book, op,
				op.Outputs[0], OutputWorld(op.Outputs[0])));
			Assert.IsTrue(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], false));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Projected, 6L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Sinks, 7L));
			Deliver(op.Outbox);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Terminal, 8L));
			Assert.IsTrue(KingdomLifecycleRules.RetireCarry(book, op, 9L));
			Assert.AreEqual(0, KingdomLifecycleRules.CarryEscrow(op));
			Assert.AreEqual(2, op.DeliveredMud);
		}

		[Test]
		public void CarryRoadLoss_RequiresSkippedOutputProofBeforeEscrowRelease()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 1, 1);
			op.LostOnRoad = true;
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			Assert.IsTrue(ProveCarryUnit(book, op, op.Sources[0]));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			SettleCarrySchedule(book, op);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 5L));
			Assert.IsFalse(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], true));
			Assert.AreEqual(1, op.EscrowMud, "failed release rolls back exactly");
			op.Outputs[0].State = KingdomLifecyclePhysicalState.Skipped;
			op.OutputIndex = 1;
			Assert.IsFalse(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Projected, 6L), "skipped output cannot strand escrow");
			op.OutputIndex = 0;
			op.Outputs[0].State = KingdomLifecyclePhysicalState.Prepared;
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarryRoadAbsence(book, op,
				op.Outputs[0], new TrustedWorld()));
			Assert.IsTrue(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], true));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Projected, 6L));
			Assert.AreEqual(1, op.LostMud);
			Assert.IsTrue(KingdomLifecycleRules.CarryConserved(op));
		}

		[Test]
		public void UndefinedOptionAndClockRegression_FailClosedWithoutRewrite()
		{
			KingdomLifecycleOptionState raw = (KingdomLifecycleOptionState)255;
			KingdomLifecycleOptionDecision invalid = KingdomLifecycleRules.ObserveOption(raw,
				10L, true, 11L, false);
			Assert.IsFalse(invalid.Valid);
			Assert.AreEqual(KingdomLifecycleOptionAction.Quarantine, invalid.Action);
			Assert.AreEqual(raw, invalid.State);
			Assert.IsFalse(KingdomLifecycleRules.ObserveOption(KingdomLifecycleOptionState.Enabled,
				10L, true, 9L, false).Valid);

			KingdomLifecycleBook book = Book();
			book.RaidOption = raw;
			KingdomLifecycleRules.Normalize(book);
			Assert.IsTrue(book.Quarantined);
			Assert.AreEqual(raw, book.RaidOption);
		}

		[Test]
		public void EnableRestamp_HasNoBacklogAndElapsedGateIsAtomic()
		{
			KingdomLifecycleOptionDecision enabled = KingdomLifecycleRules.ObserveOption(
				KingdomLifecycleOptionState.Disabled, 10L, true, 100L, false);
			Assert.IsTrue(enabled.Valid);
			Assert.AreEqual(KingdomLifecycleOptionAction.EnableAndRestamp, enabled.Action);
			Assert.AreEqual(100L, enabled.Tick);
			Assert.IsFalse(enabled.AllowNewWork);

			KingdomLifecycleOptionDecision steady = KingdomLifecycleRules.ObserveOption(
				KingdomLifecycleOptionState.Enabled, 100L, true, 101L, false);
			Assert.IsFalse(KingdomLifecycleRules.CanStartAfterOption(steady, 109L, 10L));
			Assert.IsTrue(KingdomLifecycleRules.CanStartAfterOption(steady, 110L, 10L));
			KingdomLifecycleOptionDecision open = KingdomLifecycleRules.ObserveOption(
				KingdomLifecycleOptionState.Enabled, 100L, false, 110L, true);
			Assert.IsFalse(open.AllowNewWork);
			Assert.IsTrue(open.ReconcileOpenWork, "disable gates only new work");
		}

		[Test]
		public void ChronicleIntentRetriesByReceipt_MessageIntentBecomesLost()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Sinks, 2L));
			string receipt = op.Outbox.ChronicleReceiptId;
			op.Outbox.ChronicleState = KingdomLifecycleSinkState.Intent;
			op.Outbox.MessageState = KingdomLifecycleSinkState.Intent;
			Assert.IsTrue(KingdomLifecycleRules.RecoverOutbox(book, op));
			Assert.AreEqual(receipt, op.Outbox.ChronicleReceiptId);
			Assert.AreEqual(KingdomLifecycleSinkState.Pending, op.Outbox.ChronicleState);
			Assert.AreEqual(KingdomLifecycleSinkState.Lost, op.Outbox.MessageState);
		}

		[Test]
		public void RequiredSinkTextAndChronicleReceipt_ArePlanAuthority()
		{
			KingdomLifecycleBook missing = Book();
			KingdomLifecycleOperation op = Build(missing, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			op.Outbox.Message = null;
			op.Outbox.MessageState = KingdomLifecycleSinkState.Skipped;
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(missing, op));
			Assert.AreEqual(1L, missing.RaidNextSequence);

			KingdomLifecycleBook forged = Book();
			op = Build(forged, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			op.Outbox.ChronicleReceiptId = KingdomLifecycleRules.ChildId(op.Id, "message", 0);
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(forged, op));

			KingdomLifecycleBook frozen = Book();
			op = Build(frozen, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			op.Outbox.DeedDisposition = KingdomLifecycleSinkDisposition.Skip;
			op.Outbox.DeedState = KingdomLifecycleSinkState.Skipped;
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(frozen, op),
				"optional content disposition is frozen before mutation");
			op.Outbox.DeedDisposition = KingdomLifecycleSinkDisposition.Deliver;
			KingdomLifecycleRules.Normalize(frozen);
			Assert.IsTrue(frozen.Quarantined, "a later disposition rewrite changes plan authority");
		}

		[Test]
		public void BoundedCodec_RoundTripsAndRejectsFutureOrOversizedBeforeAllocation()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			KingdomLifecycleBook copy = RoundTrip(book);
			Assert.IsFalse(copy.WireRejected);
			Assert.AreEqual(op.Id, copy.Raid.Id);

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
				Assert.IsTrue(future.WireRejected);
				Assert.IsTrue(future.Quarantined);
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
			Assert.IsTrue(poisoned.WireRejected);
			Assert.IsTrue(poisoned.Quarantined);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(poisoned));
		}

		[Test]
		public void OverCapAuthority_IsNotTruncatedIntoWritableCommand()
		{
			KingdomLifecycleBook book = Book();
			for (int i = 0; i <= KingdomLifecycleRules.MaxResourceRows; i++)
				book.Resources.Add(new KingdomLifecycleResourceRevision());
			KingdomLifecycleRules.Normalize(book);
			Assert.IsTrue(book.Quarantined);
			Assert.AreEqual(KingdomLifecycleRules.MaxResourceRows + 1, book.Resources.Count);
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
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(book, op));
			Assert.AreEqual(KingdomLifecycleRules.MaxResourceRows, book.Resources.Count);
			Assert.AreEqual(1L, book.PlainGuestNextSequence);
			Assert.IsNull(book.PlainGuest);
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
			Assert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(duplicate));
			Assert.IsNull(KingdomLifecycleRules.PrepareOperation(duplicate,
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
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(replay, replayOp),
				"a row already carrying this deterministic operation id cannot replay it");

			KingdomLifecycleBook gap = Book("city-gap");
			gap.PlainGuestNextSequence = 3L;
			KingdomLifecycleRules.Normalize(gap);
			Assert.IsTrue(gap.Quarantined, "unaccounted sequence consumption is not canonical replay state");
		}

		[Test]
		public void FuturePhase_RemainsRawAndOwnsNoAuthority()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			op.Phase = (KingdomLifecyclePhase)255;
			KingdomLifecycleRules.Normalize(book);
			Assert.IsTrue(book.Quarantined);
			Assert.AreEqual((KingdomLifecyclePhase)255, op.Phase);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(book));
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
			Assert.AreNotEqual(one.Id, two.Id);
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(b, one),
				"a self-canonical foreign operation is not authority for this book");
			Assert.AreEqual(1L, b.PlainGuestNextSequence);
			Assert.IsNull(b.PlainGuest);
			two.SettlementId = a.SettlementId;
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(b, two));

			KingdomLifecycleBook migration = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(migration, "legacy-city", true,
				"legacy-source", new List<string> { "city-a", "city-b" }));
			KingdomLifecycleBook collision = new KingdomLifecycleBook();
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(collision, "city-a", true,
				"legacy-source", new List<string> { "city-a", "city-b" }));
		}

		[Test]
		public void CarryOutputCallbackReceipt_RequiresGlobalUniquenessSameReferenceAndFrozenTopology()
		{
			KingdomCarryBook duplicateBook = CarryBook();
			KingdomCarryOperation duplicateOp = BuildCarry(duplicateBook, 1L, 1, 1);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(duplicateBook, duplicateOp));
			ReadyCarryProjection(duplicateBook, duplicateOp);
			TrustedWorld duplicate = new TrustedWorld();
			duplicate.Rows.Add(OutputObservation(duplicateOp.Outputs[0], new object()));
			duplicate.Rows.Add(OutputObservation(duplicateOp.Outputs[0], new object()));
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(duplicateBook,
				duplicateOp, duplicateOp.Outputs[0], duplicate));
			Assert.AreEqual(KingdomLifecyclePhysicalState.Prepared,
				duplicateOp.Outputs[0].ReceiptState);

			KingdomCarryBook noCallbackBook = CarryBook();
			KingdomCarryOperation noCallbackOp = BuildCarry(noCallbackBook, 1L, 1, 1);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(noCallbackBook, noCallbackOp));
			ReadyCarryProjection(noCallbackBook, noCallbackOp);
			TrustedWorld noCallback = new TrustedWorld();
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(noCallbackBook,
				noCallbackOp, noCallbackOp.Outputs[0], noCallback));
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent,
				noCallbackOp.Outputs[0].ReceiptState, "missing callback cannot mint proof");

			KingdomCarryBook wrongRefBook = CarryBook();
			KingdomCarryOperation wrongRefOp = BuildCarry(wrongRefBook, 1L, 1, 1);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(wrongRefBook, wrongRefOp));
			ReadyCarryProjection(wrongRefBook, wrongRefOp);
			TrustedWorld wrongRef = OutputWorld(wrongRefOp.Outputs[0]);
			wrongRef.OutputReturnOverride = new object();
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(wrongRefBook,
				wrongRefOp, wrongRefOp.Outputs[0], wrongRef));

			KingdomCarryBook mutatedBook = CarryBook();
			KingdomCarryOperation mutatedOp = BuildCarry(mutatedBook, 1L, 1, 1);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(mutatedBook, mutatedOp));
			ReadyCarryProjection(mutatedBook, mutatedOp);
			TrustedWorld mutated = new TrustedWorld();
			mutated.OutputCallback = delegate(KingdomLifecycleProjection value)
			{
				value.ZoneId = "callback-zone";
				object reference = new object();
				mutated.Rows.Add(OutputObservation(value, reference));
				return reference;
			};
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(mutatedBook,
				mutatedOp, mutatedOp.Outputs[0], mutated), "callback cannot rewrite frozen plan");

			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 1, 1);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			ReadyCarryProjection(book, op);
			TrustedWorld happyWorld = OutputWorld(op.Outputs[0]);
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(book, op,
				op.Outputs[0], happyWorld));
			Assert.AreEqual(2, happyWorld.ObservationCountReads,
				"bounded scan snapshots observation count once before and once after callback");
			Assert.IsTrue(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], false));
		}

		[Test]
		public void CarryScheduleIntent_RequiresExactMemberRevisionCasBeforeProjection()
		{
			KingdomCarryBook noCallbackBook = CarryBook();
			KingdomCarryOperation noCallbackOp = BuildCarry(noCallbackBook, 1L, 1, 1);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(noCallbackBook, noCallbackOp));
			RemoveCarrySources(noCallbackBook, noCallbackOp);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(noCallbackBook, noCallbackOp,
				KingdomLifecyclePhase.Removed, 3L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(noCallbackBook, noCallbackOp,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			TrustedWorld noCallback = ScheduleWorld(noCallbackBook, noCallbackOp,
				noCallbackOp.ScheduleLease.Before, noCallbackOp.ScheduleLease.BeforeRevision, null);
			noCallback.ScheduleCallback = null;
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(noCallbackBook,
				noCallbackOp, noCallback));
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent,
				noCallbackOp.ScheduleReceiptState, "no callback cannot mint schedule proof");

			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 1, 1);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op,
				ScheduleWorld(book, op, op.ScheduleLease.Before,
					op.ScheduleLease.BeforeRevision, null)), "Prepared has no scheduling authority");
			RemoveCarrySources(book, op);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			TrustedWorld foreign = ScheduleWorld(book, op, op.ScheduleLease.Before,
				op.ScheduleLease.BeforeRevision, null);
			foreign.Rows[0].ZoneIdValue = "foreign-zone";
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op, foreign));
			TrustedWorld stale = ScheduleWorld(book, op, op.ScheduleLease.Before,
				op.ScheduleLease.BeforeRevision + 1L, null);
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op, stale));
			TrustedWorld duplicate = ScheduleWorld(book, op, op.ScheduleLease.Before,
				op.ScheduleLease.BeforeRevision, null);
			duplicate.Rows.Add(duplicate.Rows[0]);
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op, duplicate));
			TrustedWorld world = ScheduleWorld(book, op, op.ScheduleLease.Before,
				op.ScheduleLease.BeforeRevision, null);
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op, world));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ProjectionIntent, 5L));
		}

		[Test]
		public void DomainPlan_RejectsArbitraryLeaseAndDepartedValueClaim()
		{
			KingdomLifecycleBook departed = Book("city-departed");
			KingdomLifecycleOperation depart = Build(departed, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Depart, 1L, 10L);
			depart.DepartedCount = depart.Count;
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(departed, depart),
				"Prepared cannot claim a departure before its exact domain CAS");

			KingdomLifecycleBook arbitrary = Book("city-arbitrary");
			KingdomLifecycleOperation spawn = Build(arbitrary, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			spawn.ResourceLeases.Add(KingdomLifecycleRules.PrepareLease(arbitrary, spawn,
				KingdomLifecycleResourceKind.Standing, arbitrary.SettlementId,
				arbitrary.SettlementId, 10L, 1L));
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(arbitrary, spawn),
				"an unrelated lease cannot substitute for or accompany the action table");

			KingdomLifecycleBook wrongDelta = Book("city-wrong-delta");
			spawn = Build(wrongDelta, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L, 10L);
			KingdomLifecycleResourceLease domain = spawn.ResourceLeases.Find(l =>
				l.Kind == KingdomLifecycleResourceKind.Population);
			domain.Delta++;
			domain.After++;
			Assert.IsFalse(KingdomLifecycleRules.TryPublish(wrongDelta, spawn));
		}

		[Test]
		public void WaterCallbackReceipt_RequiresExactUniqueVesselCompositionAndReference()
		{
			KingdomLifecycleBook foreignBook = Book("city-water-foreign");
			KingdomLifecycleOperation foreignOp = Build(foreignBook, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(foreignBook, foreignOp));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(foreignBook, foreignOp,
				KingdomLifecyclePhase.WaterIntent, 2L));
			KingdomLifecycleWaterLeg foreignLeg = foreignOp.WaterLegs[0];
			KingdomLifecycleResourceLease foreignLease = foreignOp.ResourceLeases.Find(l =>
				l.Key == foreignLeg.LeaseKey);
			TrustedWorld foreign = WaterWorld(foreignLeg);
			foreign.Rows[0].ObjectIdValue = "foreign-vessel";
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveWater(foreignBook,
				foreignLease, foreignLeg, foreign));

			KingdomLifecycleBook noCallbackBook = Book("city-water-no-callback");
			KingdomLifecycleOperation noCallbackOp = Build(noCallbackBook,
				KingdomLifecycleLane.NotableGuest, KingdomLifecycleAction.Lodge, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(noCallbackBook, noCallbackOp));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(noCallbackBook, noCallbackOp,
				KingdomLifecyclePhase.WaterIntent, 2L));
			KingdomLifecycleWaterLeg noCallbackLeg = noCallbackOp.WaterLegs[0];
			KingdomLifecycleResourceLease noCallbackLease = noCallbackOp.ResourceLeases.Find(l =>
				l.Key == noCallbackLeg.LeaseKey);
			TrustedWorld noCallback = WaterWorld(noCallbackLeg);
			noCallback.DisableWaterCallback = true;
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveWater(noCallbackBook,
				noCallbackLease, noCallbackLeg, noCallback));
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, noCallbackLeg.ReceiptState);

			KingdomLifecycleBook duplicateBook = Book("city-water-duplicate");
			KingdomLifecycleOperation duplicateOp = Build(duplicateBook,
				KingdomLifecycleLane.NotableGuest, KingdomLifecycleAction.Lodge, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(duplicateBook, duplicateOp));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(duplicateBook, duplicateOp,
				KingdomLifecyclePhase.WaterIntent, 2L));
			KingdomLifecycleWaterLeg duplicateLeg = duplicateOp.WaterLegs[0];
			KingdomLifecycleResourceLease duplicateLease = duplicateOp.ResourceLeases.Find(l =>
				l.Key == duplicateLeg.LeaseKey);
			TrustedWorld duplicate = WaterWorld(duplicateLeg);
			duplicate.Rows.Add(duplicate.Rows[0]);
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveWater(duplicateBook,
				duplicateLease, duplicateLeg, duplicate));

			KingdomLifecycleBook book = Book("city-water-happy");
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.NotableGuest,
				KingdomLifecycleAction.Lodge, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.WaterIntent, 2L));
			KingdomLifecycleWaterLeg leg = op.WaterLegs[0];
			KingdomLifecycleResourceLease lease = op.ResourceLeases.Find(l => l.Key == leg.LeaseKey);
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveWater(book, lease, leg,
				WaterWorld(leg)));
			Assert.AreEqual(op.WaterRequested, op.WaterProved);
			Assert.AreEqual(0, op.WaterOutstanding);
		}

		[Test]
		public void DeliverDisposition_CannotRetireThroughSkippedState()
		{
			KingdomLifecycleBook book = Book();
			KingdomLifecycleOperation op = Build(book, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Passages, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.Sinks, 2L));
			op.Outbox.LedgerState = KingdomLifecycleSinkState.Skipped;
			Assert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 3L));
			KingdomLifecycleRules.Normalize(book);
			Assert.IsTrue(book.Quarantined);
		}

		[Test]
		public void CarryDeliverSinks_CannotPublishOrRetireAsSkipped()
		{
			KingdomCarryBook publicationBook = CarryBook();
			KingdomCarryOperation publication = BuildCarry(publicationBook, 1L, 1, 1);
			publication.Outbox.LedgerDisposition = KingdomLifecycleSinkDisposition.Skip;
			publication.Outbox.LedgerState = KingdomLifecycleSinkState.Skipped;
			Assert.IsFalse(KingdomLifecycleRules.TryPublishCarry(publicationBook, publication));

			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation op = BuildCarry(book, 1L, 1, 1);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, op));
			ReadyCarryProjection(book, op);
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarryOutput(book, op,
				op.Outputs[0], OutputWorld(op.Outputs[0])));
			Assert.IsTrue(KingdomLifecycleRules.MoveCarryEscrow(book, op, op.Outputs[0], false));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Projected, 6L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Sinks, 7L));
			Deliver(op.Outbox);
			op.Outbox.MessageState = KingdomLifecycleSinkState.Skipped;
			Assert.IsFalse(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Terminal, 8L));
			KingdomLifecycleRules.Normalize(book);
			Assert.IsTrue(book.Quarantined);
		}

		[Test]
		public void LifecycleScheduleAndRemoval_RequireExactZoneBlueprintAndCallback()
		{
			KingdomLifecycleBook scheduleBook = Book("city-lifecycle-schedule");
			KingdomLifecycleOperation schedule = Build(scheduleBook,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Passages, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(scheduleBook, schedule));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(scheduleBook, schedule,
				KingdomLifecyclePhase.Sinks, 2L));
			Deliver(schedule.Outbox);
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(scheduleBook, schedule,
				KingdomLifecyclePhase.ScheduleIntent, 3L));
			TrustedWorld foreignZone = LifecycleScheduleWorld(scheduleBook, schedule);
			foreignZone.Rows[0].ZoneIdValue = "foreign-zone";
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(
				scheduleBook, schedule, foreignZone));
			TrustedWorld noScheduleCallback = LifecycleScheduleWorld(scheduleBook, schedule);
			noScheduleCallback.ScheduleCallback = null;
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(
				scheduleBook, schedule, noScheduleCallback));

			KingdomLifecycleBook removalBook = Book("city-lifecycle-removal");
			KingdomLifecycleOperation removal = Build(removalBook,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Depart, 1L, 10L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublish(removalBook, removal));
			Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(removalBook, removal,
				KingdomLifecyclePhase.RemovalIntent, 2L));
			TrustedWorld wrongBlueprint = LifecycleRemovalWorld(removal);
			wrongBlueprint.Rows[0].BlueprintValue = "ForeignCitizen";
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleRemoval(
				removalBook, removal, wrongBlueprint));
			TrustedWorld noRemovalCallback = LifecycleRemovalWorld(removal);
			noRemovalCallback.LifecycleRemovalCallback = null;
			Assert.IsFalse(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleRemoval(
				removalBook, removal, noRemovalCallback));
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, removal.RemovalState);
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
				Assert.IsNull(typeof(KingdomLifecycleRules).GetMethod(removed[i],
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
					removed[i]);
			Assert.IsFalse(typeof(KingdomLifecycleRules.TrustedAdapter).IsPublic);
			KingdomLifecycleBook book = Book("city-public-physical");
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Spawn, 1L);
			Assert.IsNull(KingdomLifecycleRules.PrepareLease(book, op,
				KingdomLifecycleResourceKind.Schedule, book.SettlementId, "schedule", 1L, 1L));
			Assert.IsNull(KingdomLifecycleRules.PrepareLease(book, op,
				KingdomLifecycleResourceKind.Object, "topology", "object", 1L, -1L));
		}

		[Test]
		public void IdentityBinding_RequiresPristineStateExactMigrationKeyAndFullCarrySet()
		{
			KingdomLifecycleBook dirty = new KingdomLifecycleBook { PlainGuestNextSequence = 2L };
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(dirty, "city-a", false,
				null, null));
			Assert.IsNull(dirty.SettlementId);
			KingdomLifecycleBook migration = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(migration, "city-a", true,
				"migration-a", new List<string>()));
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(migration, "city-a", true,
				"migration-b", new List<string>()));
			KingdomLifecycleBook preseeded = new KingdomLifecycleBook { SettlementId = "city-a" };
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(preseeded, "city-a", false,
				null, new List<string>()), "preseeded id has no durable binding receipt");
			KingdomLifecycleRules.Normalize(preseeded);
			Assert.IsTrue(preseeded.Quarantined);
			KingdomLifecycleBook established = Book("city-established");
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(established,
				"city-established", false, null, null), "established binding still needs a scan");
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(established,
				"city-established", false, null, new List<string> { "city-established" }));
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(established,
				"city-established", false, null, new List<string>()));

			KingdomCarryBook carry = new KingdomCarryBook();
			Assert.IsFalse(KingdomLifecycleRules.BindCarryIdentity(carry, "realm-a",
				new List<string> { "city-a", "city-a" }, false, null));
			Assert.IsTrue(KingdomLifecycleRules.BindCarryIdentity(carry, "realm-a",
				new List<string> { "city-b", "city-a" }, false, null));
			CollectionAssert.AreEqual(new List<string> { "city-a", "city-b" }, carry.SettlementIds);
			Assert.IsFalse(KingdomLifecycleRules.BindCarryIdentity(carry, "realm-a",
				new List<string> { "city-a" }, false, null));
			KingdomCarryBook preseededCarry = new KingdomCarryBook
			{
				RealmId = "realm-a", SettlementIds = new List<string> { "city-a", "city-b" }
			};
			Assert.IsFalse(KingdomLifecycleRules.BindCarryIdentity(preseededCarry, "realm-a",
				new List<string> { "city-a", "city-b" }, false, null));
			KingdomLifecycleRules.Normalize(preseededCarry);
			Assert.IsTrue(preseededCarry.Quarantined);
			KingdomCarryOperation op = BuildCarry(carry, 1L, 1, 1);
			op.DestinationSettlementId = "foreign-city";
			Assert.IsFalse(KingdomLifecycleRules.TryPublishCarry(carry, op));
		}

		[Test]
		public void FirstFoundingCarryBinding_PublishesOneAtomicIdentityReceipt()
		{
			KingdomCarryBook book = new KingdomCarryBook();
			Assert.IsTrue(KingdomLifecycleRules.BindCarryIdentity(book, "realm-first",
				new List<string> { "city-first" }, false, null));
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book));
			Assert.AreEqual("realm-first", book.RealmId);
			CollectionAssert.AreEqual(new[] { "city-first" }, book.SettlementIds);
			Assert.IsTrue(book.IdentityBound);
			Assert.IsNotEmpty(book.IdentityProof);

			KingdomCarryBook preseeded = new KingdomCarryBook { RealmId = "realm-first" };
			KingdomLifecycleRules.Normalize(preseeded);
			Assert.IsTrue(preseeded.Quarantined,
				"a realm id without its atomic identity receipt owns no authority");
			Assert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(preseeded));
		}

		[Test]
		public void CarryIdentityExpansion_IsCanonicalMonotoneRetryStableAndWireStable()
		{
			KingdomCarryBook book = new KingdomCarryBook();
			Assert.IsTrue(KingdomLifecycleRules.BindCarryIdentity(book, "realm-expand",
				new List<string> { "city-b" }, false, null));
			List<string> singleton = book.SettlementIds;
			string singletonProof = book.IdentityProof;
			byte[] singletonWire = CarryBytes(book);
			string failure;

			Assert.IsTrue(KingdomLifecycleRules.CanExpandCarryIdentity(book, "realm-expand",
				new List<string> { "city-b", "city-a" }, out failure), failure);
			Assert.AreSame(singleton, book.SettlementIds);
			Assert.AreEqual(singletonProof, book.IdentityProof);
			CollectionAssert.AreEqual(singletonWire, CarryBytes(book));
			Assert.IsTrue(KingdomLifecycleRules.ExpandCarryIdentity(book, "realm-expand",
				new List<string> { "city-b", "city-a" }, out failure), failure);
			Assert.AreNotSame(singleton, book.SettlementIds);
			Assert.AreNotEqual(singletonProof, book.IdentityProof);
			CollectionAssert.AreEqual(new[] { "city-a", "city-b" }, book.SettlementIds);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book));

			List<string> expanded = book.SettlementIds;
			string expandedProof = book.IdentityProof;
			byte[] expandedWire = CarryBytes(book);
			Assert.IsTrue(KingdomLifecycleRules.CanExpandCarryIdentity(book, "realm-expand",
				new List<string> { "city-b", "city-a" }, out failure), failure);
			Assert.IsTrue(KingdomLifecycleRules.ExpandCarryIdentity(book, "realm-expand",
				new List<string> { "city-b", "city-a" }, out failure), failure);
			Assert.AreSame(expanded, book.SettlementIds,
				"an exact retry must not replace the established topology object");
			Assert.AreEqual(expandedProof, book.IdentityProof);
			CollectionAssert.AreEqual(expandedWire, CarryBytes(book));

			KingdomCarryBook reloaded = RoundTrip(book);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(reloaded));
			Assert.AreEqual(expandedProof, reloaded.IdentityProof);
			CollectionAssert.AreEqual(expanded, reloaded.SettlementIds);
			CollectionAssert.AreEqual(expandedWire, CarryBytes(reloaded));
		}

		[Test]
		public void CarryIdentityExpansion_RejectsWrongRealmRemovalAndReplacement()
		{
			string failure;
			KingdomCarryBook wrongRealm = CarryBook();
			List<string> wrongRealmTopology = wrongRealm.SettlementIds;
			Assert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(wrongRealm, "realm-b",
				new List<string> { "city-a", "city-b", "city-c" }, out failure));
			Assert.IsNotEmpty(failure);
			Assert.IsFalse(wrongRealm.Quarantined);
			Assert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(wrongRealm, "realm-b",
				new List<string> { "city-a", "city-b", "city-c" }, out failure));
			Assert.IsTrue(wrongRealm.Quarantined);
			Assert.AreSame(wrongRealmTopology, wrongRealm.SettlementIds);

			KingdomCarryBook removal = CarryBook();
			List<string> removalTopology = removal.SettlementIds;
			Assert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(removal, "realm-a",
				new List<string> { "city-a" }, out failure));
			Assert.IsNotEmpty(failure);
			Assert.IsFalse(removal.Quarantined);
			Assert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(removal, "realm-a",
				new List<string> { "city-a" }, out failure));
			Assert.IsTrue(removal.Quarantined);
			Assert.AreSame(removalTopology, removal.SettlementIds);
			CollectionAssert.AreEqual(new[] { "city-a", "city-b" }, removal.SettlementIds);

			KingdomCarryBook replacement = CarryBook();
			List<string> replacementTopology = replacement.SettlementIds;
			Assert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(replacement, "realm-a",
				new List<string> { "city-a", "city-c" }, out failure));
			Assert.IsNotEmpty(failure);
			Assert.IsFalse(replacement.Quarantined);
			Assert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(replacement, "realm-a",
				new List<string> { "city-a", "city-c" }, out failure));
			Assert.IsTrue(replacement.Quarantined);
			Assert.AreSame(replacementTopology, replacement.SettlementIds);
			CollectionAssert.AreEqual(new[] { "city-a", "city-b" }, replacement.SettlementIds);
		}

		[Test]
		public void CarryIdentityExpansion_OpenReceiptDefersWithoutChangingAuthority()
		{
			KingdomCarryBook book = CarryBook();
			KingdomCarryOperation operation = BuildCarry(book, 1L, 1, 1);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishCarry(book, operation));
			List<string> topology = book.SettlementIds;
			string proof = book.IdentityProof;
			byte[] before = CarryBytes(book);
			string failure;

			Assert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(book, "realm-a",
				new List<string> { "city-a", "city-b", "city-c" }, out failure));
			Assert.IsNotEmpty(failure);
			StringAssert.Contains("open", failure.ToLowerInvariant());
			Assert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(book, "realm-a",
				new List<string> { "city-a", "city-b", "city-c" }, out failure));
			Assert.IsNotEmpty(failure);
			StringAssert.Contains("open", failure.ToLowerInvariant());
			Assert.IsFalse(book.Quarantined);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book));
			Assert.AreSame(topology, book.SettlementIds);
			Assert.AreSame(operation, book.Open);
			Assert.AreEqual(proof, book.IdentityProof);
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
				Assert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(book, "realm-a",
					malformed[i], out failure), "preflight candidate " + i);
				Assert.IsNotEmpty(failure, "preflight candidate " + i);
				Assert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(book, "realm-a",
					malformed[i], out failure), "publish candidate " + i);
				Assert.IsNotEmpty(failure, "publish candidate " + i);
				Assert.IsFalse(book.Quarantined, "candidate " + i);
				Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book), "candidate " + i);
				Assert.AreSame(topology, book.SettlementIds, "candidate " + i);
				Assert.AreEqual(proof, book.IdentityProof, "candidate " + i);
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
			Assert.IsFalse(KingdomLifecycleRules.CanExpandCarryIdentity(preflight, "realm-a",
				candidate, out failure));
			Assert.IsNotEmpty(failure);
			StringAssert.Contains("changed", failure.ToLowerInvariant());
			Assert.AreSame(preflightTopology, preflight.SettlementIds);
			Assert.IsFalse(preflight.Quarantined);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(preflight));

			KingdomCarryBook publish = CarryBook();
			List<string> publishTopology = publish.SettlementIds;
			candidate = new MutatingCollection(
				new List<string> { "city-a", "city-b", "city-c" },
				delegate { publish.NextSequence = 2L; });
			Assert.IsFalse(KingdomLifecycleRules.ExpandCarryIdentity(publish, "realm-a",
				candidate, out failure));
			Assert.IsTrue(publish.Quarantined);
			StringAssert.Contains("changed", publish.Fault);
			Assert.AreSame(publishTopology, publish.SettlementIds);
			CollectionAssert.AreEqual(new[] { "city-a", "city-b" }, publish.SettlementIds);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(publish));
		}

		[Test]
		public void SettlementIdentityCollisionScan_UsesIndependentBoundAndRejectsAliases()
		{
			List<string> fiveOtherSettlements = new List<string>
			{
				"city-1", "city-2", "city-3", "city-4", "city-5"
			};
			KingdomLifecycleBook accepted = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(accepted, "city-target",
				false, null, fiveOtherSettlements),
				"collision scan must not inherit four-city carry topology cap");
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(accepted));

			KingdomLifecycleBook duplicate = new KingdomLifecycleBook();
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(duplicate, "city-target",
				false, null, new List<string> { "city-1", "city-2", "city-3", "city-4",
					"city-5", "city-5" }));
			Assert.IsNull(duplicate.SettlementId);

			KingdomLifecycleBook target = new KingdomLifecycleBook();
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(target, "city-target",
				false, null, new List<string> { "city-1", "city-2", "city-3", "city-4",
					"city-5", "city-target" }));
			Assert.IsNull(target.SettlementId);

			List<string> maximum = new List<string>();
			for (int i = 0; i < KingdomLifecycleRules.MaxLifecycleCollisionIds; i++)
				maximum.Add("city-global-" + i);
			KingdomLifecycleBook atCap = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(atCap, "city-at-cap",
				false, null, maximum));
			maximum.Add("city-over-cap");
			KingdomLifecycleBook overCap = new KingdomLifecycleBook();
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(overCap, "city-over",
				false, null, maximum));
		}

		[Test]
		public void IdentityBinding_CallbackMutatedOrThrowingTopologyCannotPublishAuthority()
		{
			KingdomLifecycleBook mutated = new KingdomLifecycleBook();
			MutatingCollection ids = new MutatingCollection(new List<string> { "city-b" },
				delegate { mutated.PlainGuestNextSequence = 2L; });
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(mutated, "city-a", false,
				null, ids));
			Assert.IsNull(mutated.SettlementId);

			KingdomLifecycleBook throwing = new KingdomLifecycleBook();
			ids = new MutatingCollection(new List<string> { "city-b" },
				delegate { throw new InvalidOperationException("hostile enumeration"); });
			Assert.IsFalse(KingdomLifecycleRules.BindSettlementIdentity(throwing, "city-a", false,
				null, ids));
			Assert.IsNull(throwing.SettlementId);
		}

		[Test]
		public void CarryWireAndUtf8Codec_RejectFutureSchemaNoncanonicalBoolAndByteOverflow()
		{
			using (MemoryStream futureBytes = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(futureBytes,
					System.Text.Encoding.UTF8, true))
				{
					writer.Write(KingdomLifecycleWireCodec.CarryMagic);
					writer.Write(KingdomLifecycleRules.CurrentFormatVersion + 1);
				}
				futureBytes.Position = 0;
				KingdomCarryBook future = new KingdomCarryBook();
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec.ReadCarry(
					new BinaryReader(futureBytes), future));
				Assert.IsTrue(future.WireRejected);
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
			bytes[8] = 2;
			using (MemoryStream stream = new MemoryStream(bytes))
				Assert.Throws<InvalidDataException>(() => KingdomLifecycleWireCodec.ReadCarry(
					new BinaryReader(stream), new KingdomCarryBook()));
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

		private static KingdomLifecycleOperation Build(KingdomLifecycleBook book,
			KingdomLifecycleLane lane, KingdomLifecycleAction action, long tick, long scheduleBefore)
		{
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book, lane, action, tick);
			Assert.NotNull(op);
			op.ZoneId = "zone-a";
			op.DueBefore = scheduleBefore;
			op.DueAfter = scheduleBefore + 1L;
			op.ResourceLeases.Add(KingdomLifecycleRules.TrustedAdapter.PreparePhysicalLease(book, op,
				KingdomLifecycleResourceKind.Schedule, book.SettlementId,
				KingdomLifecycleRules.ScheduleSubjectId(book.SettlementId, lane), scheduleBefore, 1L));

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

			if (action == KingdomLifecycleAction.Spawn || action == KingdomLifecycleAction.RaidAttack)
			{
				op.PartySize = 1;
				KingdomLifecycleProjection projection = Projection(op, 0, -1, 1);
				op.Projections.Add(projection);
				string topology = KingdomLifecycleRules.TopologyId(projection.Topology,
					projection.OwnerId, projection.ZoneId, projection.X, projection.Y);
				op.ResourceLeases.Add(KingdomLifecycleRules.TrustedAdapter.PreparePhysicalLease(book, op,
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId, 0L, 1L));
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
			if (action != KingdomLifecycleAction.Passages)
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
			return op;
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
						Assert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op, phase, tick + guard));
						moved = true;
						break;
					}
				}
				Assert.IsTrue(moved, op.Action + " at " + op.Phase);
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
						Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveWater(book,
							lease, leg, WaterWorld(leg)), op.Action + " water receipt");
					}
				}
			}
			else if (op.Phase == KingdomLifecyclePhase.RemovalIntent)
			{
				Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleRemoval(book,
					op, LifecycleRemovalWorld(op)), op.Action + " removal receipt");
			}
			else if (op.Phase == KingdomLifecyclePhase.DomainIntent)
			{
				SettleLeaseKind(book, op, KingdomLifecycleResourceKind.None, true);
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
				Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(book,
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
			Assert.NotNull(lease);
			bool began = KingdomLifecycleRules.BeginLease(book, lease, lease.Before);
			Assert.IsTrue(began,
				lease.Kind + " begin");
			bool committed = KingdomLifecycleRules.CommitLeaseWitness(book, lease, lease.After);
			Assert.IsTrue(committed,
				lease.Kind + " confirm");
		}

		private static void SettleProjectionLease(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection)
		{
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveLifecycleProjection(book,
				op, projection, LifecycleProjectionWorld(projection)),
				op.Action + " projection receipt");
		}

		private static void SettleCarrySchedule(KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op,
				ScheduleWorld(book, op, op.ScheduleLease.Before,
					op.ScheduleLease.BeforeRevision, null)));
		}

		private static void RemoveCarrySources(KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			if (op.Phase == KingdomLifecyclePhase.Prepared)
				Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
					KingdomLifecyclePhase.RemovalIntent, 2L));
			for (int i = 0; i < op.Sources.Count; i++)
			{
				KingdomCarrySource source = op.Sources[i];
				while (source.Removed < source.PlannedCount)
					Assert.IsTrue(ProveCarryUnit(book, op, source));
			}
		}

		private static void ReadyCarryProjection(KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			RemoveCarrySources(book, op);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.Removed, 3L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
				KingdomLifecyclePhase.ScheduleIntent, 4L));
			SettleCarrySchedule(book, op);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceCarryPhase(book, op,
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
			Assert.IsTrue(KingdomLifecycleRules.BindCarryIdentity(book, "realm-a",
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
			Assert.NotNull(op);
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
			Assert.IsTrue(KingdomLifecycleRules.TrustedAdapter.PrepareCarrySchedule(book, op,
				ScheduleWorld(book, op, 99L, 0L, null)));
			Assert.NotNull(op.ScheduleLease);
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
