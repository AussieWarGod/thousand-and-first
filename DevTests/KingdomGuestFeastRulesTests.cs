using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGuestFeastRulesTests
	{
		private const string RealmTransaction = "ffffffffffffffffffffffffffffffff";
		private const string CityTransaction = "00000000000000000000000000000001";

		private static string Realm()
		{
			Assert.IsTrue(KingdomIdentityRules.TryMintRealm(RealmTransaction,
				out string realm, out KingdomIdentityFault fault), fault.ToString());
			return realm;
		}

		private static string Settlement(string transaction = CityTransaction)
		{
			Assert.IsTrue(KingdomIdentityRules.TryMintSettlement(Realm(), transaction,
				out string settlement, out KingdomIdentityFault fault), fault.ToString());
			return settlement;
		}

		private static KingdomGuestFeastBook Bound()
		{
			KingdomGuestFeastBook book = new KingdomGuestFeastBook();
			Assert.IsTrue(KingdomGuestFeastRules.TryBindEmptyIdentity(book, Realm(),
				out string failure), failure);
			return book;
		}

		private static KingdomGrowthFirstGuestOpportunity Guest(
			KingdomGrowthFirstGuestChoiceState choice =
				KingdomGrowthFirstGuestChoiceState.AwaitingChoice, bool physical = false)
		{
			string settlement = Settlement(); long cause = 1L, cadence = 100L;
			KingdomGrowthFirstGuestOpportunity row = new KingdomGrowthFirstGuestOpportunity
			{
				OpportunityId = KingdomGrowthFirstGuestIdentityRules.OpportunityId(settlement, 1L),
				CauseId = KingdomGrowthFirstGuestIdentityRules.CauseId(settlement, 1L,
					cause, cadence), CauseTick = cause, OfferedTick = 2L, CadenceTicks = cadence,
				FactsState = KingdomGrowthFirstGuestFactsState.Exact, CohortSize = 1,
				PopulationBefore = 1, PopulationCap = 100, SupportedLevel = 1,
				SupportCap = 100, WaterAvailable = 100, WaterRequired = 1,
				ChoiceState = choice, RulesVersion = physical ? 2 : 1
			};
			if (choice == KingdomGrowthFirstGuestChoiceState.Admitted
				|| choice == KingdomGrowthFirstGuestChoiceState.Declined)
			{
				row.DecisionTick = 5L;
				row.DecisionReceiptId = "taf:growth-first-guest-receipt:"
					+ new string(choice == KingdomGrowthFirstGuestChoiceState.Admitted ? 'a' : 'd', 64);
				if (physical && choice == KingdomGrowthFirstGuestChoiceState.Admitted)
				{
					row.BodyReservationId = "taf:experience-body:first-guest:v1:"
						+ new string('b', 64);
					row.BodyRealmId = Realm(); row.BodyOptionKind = KingdomExperienceOptionKind.CivicStory;
					row.BodyEnableEpoch = 1L; row.BodyReservedTick = row.DecisionTick;
					row.BodyLeaseState = KingdomGrowthFirstGuestBodyLeaseState.Reserved;
					row.GuestPhase = KingdomGrowthFirstGuestGuestPhase.Preparing;
				}
			}
			return row;
		}

		private static KingdomFirstFeastReceipt Practice(KingdomFirstFeastPhase phase =
			KingdomFirstFeastPhase.Adopted, long decided = 12L)
		{
			bool affirmative = phase == KingdomFirstFeastPhase.Adopted
				|| phase == KingdomFirstFeastPhase.Adapted;
			KingdomGrowthFirstGuestTerminalReceipt terminal = Terminal(
				KingdomGrowthArrivalDisposition.Joined);
			KingdomGuestFeastReceipt terminalRow = TerminalRow(terminal);
			KingdomFirstFeastDeed deed = new KingdomFirstFeastDeed {
				SettlementId = Settlement(), SettlementName = "Kyakukya",
				DeedText = KingdomFirstFeastRules.AuthoredDeed, DeedTick = 10L,
				GuestTerminalReceiptId = terminal.ReceiptId,
				GuestTerminalDigest = KingdomGuestFeastRules.TerminalDigest(terminalRow),
				GuestTerminalTick = terminal.TerminalTick,
				AdventureEventId = "taf:adventure:guest-feast",
				AdventureFingerprint = new string('f', 64) };
			Assert.IsTrue(KingdomFirstFeastRules.TryBuildDeedId(deed, out deed.DeedId));
			return new KingdomFirstFeastReceipt
			{
				Phase = phase, Choice = phase == KingdomFirstFeastPhase.Refused
					? KingdomFirstFeastChoice.Refuse : phase == KingdomFirstFeastPhase.Adapted
						? KingdomFirstFeastChoice.Adapt : KingdomFirstFeastChoice.Adopt,
				Generation = 1, SettlementId = deed.SettlementId, SettlementName = deed.SettlementName,
				DeedId = deed.DeedId, DeedText = deed.DeedText, DeedTick = deed.DeedTick,
				GuestTerminalReceiptId = deed.GuestTerminalReceiptId,
				GuestTerminalDigest = deed.GuestTerminalDigest,
				GuestTerminalTick = deed.GuestTerminalTick,
				AdventureEventId = deed.AdventureEventId,
				AdventureFingerprint = deed.AdventureFingerprint,
				ProposerResidentId = 1, ProposerName = "Ava", WitnessResidentId = 2,
				WitnessName = "Yla", DishName = KingdomFirstFeastRules.AuthoredDish,
				Ingredients = KingdomFirstFeastRules.AuthoredIngredients,
				OfferedDedication = KingdomFirstFeastRules.OfferedDedication,
				AdaptedDedication = phase == KingdomFirstFeastPhase.Adapted
					? KingdomFirstFeastRules.TravelerDedication : null,
				PracticeId = affirmative ? KingdomFirstFeastRules.PracticePrefix
					+ deed.DeedId.Substring(KingdomFirstFeastRules.DeedPrefix.Length) : null,
				OfferedTick = 11L, DecidedTick = decided, EnableEpoch = 1L
			};
		}

		private static KingdomGuestFeastReceipt StartCycling(KingdomGuestFeastBook book)
		{
			KingdomGrowthFirstGuestOpportunity awaiting = Guest();
			Assert.IsTrue(KingdomGuestFeastRules.TryBegin(book, book.Revision, Settlement(),
				awaiting, out _, out string failure), failure);
			KingdomGrowthFirstGuestOpportunity admitted = Guest(
				KingdomGrowthFirstGuestChoiceState.Admitted);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestDecision(book, book.Revision,
				Settlement(), admitted, out _, out failure), failure);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestTerminal(book, book.Revision,
				Settlement(), Terminal(KingdomGrowthArrivalDisposition.Joined), out _, out failure),
				failure);
			Assert.IsTrue(KingdomGuestFeastRules.TryObservePractice(book, book.Revision,
				Settlement(), Practice(), out KingdomGuestFeastReceipt row, out failure), failure);
			Assert.IsTrue(KingdomGuestFeastRules.TryBuildLocusReceipt(Realm(), Settlement(),
				1, "bench-1", "JoppaWorld.1.1.1.1.10", "r_KingdomBench", 13L,
				out KingdomGuestFeastLocusReceipt locus));
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveLocus(book, book.Revision,
				Settlement(), locus, out failure), failure);
			KingdomGuestFeastRules.TryFind(book, Settlement(), out row);
			Assert.AreEqual(KingdomGuestFeastPhase.Cycling, row.Phase); return row;
		}

		private static KingdomGuestFeastReceipt TerminalRow(
			KingdomGrowthFirstGuestTerminalReceipt terminal)
		{
			return new KingdomGuestFeastReceipt { SettlementId = terminal.SettlementId,
				GrowthTerminalReceiptId = terminal.ReceiptId, GuestCandidateId = terminal.CandidateId,
				GuestObjectId = terminal.CandidateObjectId,
				GuestArrivalOperationId = terminal.ArrivalOperationId,
				GuestArrivalOutboxEventId = terminal.ArrivalOutboxEventId,
				GuestName = terminal.PersonName, GuestOrigin = terminal.PersonOrigin,
				GuestCreed = terminal.PersonCreed, GuestResidentId = terminal.ResidentId,
				GuestResult = terminal.Result, GuestTerminalTick = terminal.TerminalTick };
		}

		private static KingdomGrowthFirstGuestTerminalReceipt Terminal(
			KingdomGrowthArrivalDisposition result, bool physical = false,
			KingdomGrowthFirstGuestTerminalState terminalState =
				KingdomGrowthFirstGuestTerminalState.None)
		{
			KingdomGrowthFirstGuestOpportunity opportunity = Guest(result ==
				KingdomGrowthArrivalDisposition.Declined
					? KingdomGrowthFirstGuestChoiceState.Declined
					: KingdomGrowthFirstGuestChoiceState.Admitted, physical);
			string candidate = "taf:growth-arrival-candidate:" + new string('1', 64);
			string operation = "taf:growth-operation:" + new string('2', 64);
			long tick = 9L;
			if (physical && result != KingdomGrowthArrivalDisposition.Declined)
			{
				if (terminalState == KingdomGrowthFirstGuestTerminalState.None)
					terminalState = result == KingdomGrowthArrivalDisposition.Joined
						? KingdomGrowthFirstGuestTerminalState.Citizen
						: result == KingdomGrowthArrivalDisposition.NoAcceptableHome
							? KingdomGrowthFirstGuestTerminalState.CouldNotJoin
							: KingdomGrowthFirstGuestTerminalState.Departed;
				opportunity.GuestPhase = KingdomGrowthFirstGuestGuestPhase.Terminal;
				opportunity.GuestTerminalState = terminalState;
				opportunity.GuestTerminalTick = tick;
				opportunity.GuestTerminalReceiptId = "taf:growth-first-guest-receipt:"
					+ new string('e', 64);
				opportunity.BodyLeaseState = KingdomGrowthFirstGuestBodyLeaseState.Released;
				if (terminalState != KingdomGrowthFirstGuestTerminalState.Died)
				{
					opportunity.GuestActionTick = 6L;
					opportunity.GuestActionReceiptId = "taf:growth-first-guest-receipt:"
						+ new string('c', 64);
				}
			}
			return new KingdomGrowthFirstGuestTerminalReceipt
			{
				ReceiptId = KingdomGrowthFirstGuestIdentityRules.TerminalReceiptId(candidate,
					opportunity.DecisionReceiptId, operation, result, tick),
				SettlementId = Settlement(), CandidateId = candidate,
				CandidateObjectId = result == KingdomGrowthArrivalDisposition.Declined
					? null : "taf:object:first-guest", Blueprint = "r_KingdomSettler",
				PersonName = "Ari", PersonOrigin = "Joppa", PersonCreed = "Water",
				ResidentId = result == KingdomGrowthArrivalDisposition.Joined ? 1 : 0,
				Result = result, ArrivalOperationId = operation,
				ArrivalOutboxEventId = "taf:growth-outbox:" + new string('3', 64),
				TerminalTick = tick, Opportunity = opportunity
			};
		}

		[Test]
		public void SharedFirstGuestIdentityKeepsTheFrozenLifecycleBytes()
		{
			Assert.AreEqual(
				"taf:growth-first-guest-opportunity:" +
				"6bae49508d144fa4420c9625bb235cbd3be11e0233b454986cf686999d1982d5",
				KingdomGrowthFirstGuestIdentityRules.OpportunityId("settlement", 1L));
			Assert.AreEqual(
				"taf:growth-first-guest-cause:" +
				"152df05d430f32e4012b14655716ec56620ed4cdf292103b877a04facf3f3e1d",
				KingdomGrowthFirstGuestIdentityRules.CauseId("settlement", 1L, 2L, 100L));
		}

		[Test]
		public void ExactGrowthDecisionObservationIsCasBoundAndReplayIdempotent()
		{
			KingdomGuestFeastBook book = Bound();
			KingdomGrowthFirstGuestOpportunity awaiting = Guest();
			Assert.IsTrue(KingdomGuestFeastRules.TryBegin(book, book.Revision, Settlement(),
				awaiting, out _, out string failure), failure);
			long before = book.Revision;
			KingdomGrowthFirstGuestOpportunity foreign = Guest(
				KingdomGrowthFirstGuestChoiceState.Admitted);
			foreign.CauseTick = 2L;
			foreign.CauseId = KingdomGrowthFirstGuestIdentityRules.CauseId(
				Settlement(), 1L, foreign.CauseTick, foreign.CadenceTicks);
			Assert.IsFalse(KingdomGuestFeastRules.TryObserveGuestDecision(book, before,
				Settlement(), foreign, out _, out _));
			Assert.AreEqual(before, book.Revision);
			KingdomGrowthFirstGuestOpportunity admitted = Guest(
				KingdomGrowthFirstGuestChoiceState.Admitted);
			Assert.IsFalse(KingdomGuestFeastRules.TryObserveGuestDecision(book, before + 1L,
				Settlement(), admitted, out _, out _));
			Assert.AreEqual(before, book.Revision);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestDecision(book, before,
				Settlement(), admitted, out KingdomGuestFeastReceipt observed, out failure),
				failure);
			Assert.IsTrue(KingdomGuestFeastRules.ExactGuestReference(observed, admitted));
			long committed = book.Revision;
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestDecision(book, 0L,
				Settlement(), admitted, out observed, out failure), failure);
			Assert.AreEqual(committed, book.Revision);
		}

		[Test]
		public void WholeLoopReferencesOwnersThenExhaustsAfterExactlyThreeReturns()
		{
			KingdomGuestFeastBook book = Bound(); StartCycling(book);
			for (int cycle = 1; cycle <= 3; cycle++)
			{
				Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, book.Revision,
					Settlement(), false, true, out bool changed, out string failure), failure);
				Assert.IsTrue(changed);
				Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, book.Revision,
					Settlement(), true, true, out changed, out failure), failure);
				Assert.IsTrue(changed);
				KingdomGuestFeastRules.TryFind(book, Settlement(),
					out KingdomGuestFeastReceipt row);
				Assert.AreEqual(cycle, row.HomeCycles);
			}
			KingdomGuestFeastRules.TryFind(book, Settlement(), out KingdomGuestFeastReceipt ended);
			Assert.AreEqual(KingdomGuestFeastPhase.Exhausted, ended.Phase);
			long quiet = book.Revision;
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, quiet, Settlement(),
				false, true, out bool changedAgain, out string failureAgain), failureAgain);
			Assert.IsFalse(changedAgain); Assert.AreEqual(quiet, book.Revision);
			Assert.IsTrue(KingdomGuestFeastRules.TryTrace(book, Settlement(), Practice(),
				out string trace));
			StringAssert.Contains("grants no meal, recipe, or boon", trace);
		}

		[Test]
		public void HomeAndRepeatedSameSideObservationsCannotInventAJourney()
		{
			KingdomGuestFeastBook book = Bound(); StartCycling(book);
			long start = book.Revision;
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, start,
				Settlement(), true, true, out bool changed, out string failure), failure);
			Assert.IsFalse(changed); Assert.AreEqual(start, book.Revision);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, book.Revision,
				Settlement(), false, true, out changed, out failure), failure);
			Assert.IsTrue(changed); long armed = book.Revision;
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, armed,
				Settlement(), false, true, out changed, out failure), failure);
			Assert.IsFalse(changed); Assert.AreEqual(armed, book.Revision);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, book.Revision,
				Settlement(), true, true, out changed, out failure), failure);
			Assert.IsTrue(changed); long returned = book.Revision;
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, returned,
				Settlement(), true, true, out changed, out failure), failure);
			Assert.IsFalse(changed); Assert.AreEqual(returned, book.Revision);
			KingdomGuestFeastRules.TryFind(book, Settlement(), out KingdomGuestFeastReceipt row);
			Assert.AreEqual(1, row.HomeCycles); Assert.IsFalse(row.AwayArmed);
		}

		[Test]
		public void DisabledOptionDisarmsAwayCycleWithoutBacklogOrCredit()
		{
			KingdomGuestFeastBook book = Bound(); StartCycling(book);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, book.Revision,
				Settlement(), false, true, out _, out string failure), failure);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, book.Revision,
				Settlement(), false, false, out _, out failure), failure);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, book.Revision,
				Settlement(), true, true, out bool changed, out failure), failure);
			Assert.IsFalse(changed);
			KingdomGuestFeastRules.TryFind(book, Settlement(), out KingdomGuestFeastReceipt row);
			Assert.AreEqual(0, row.HomeCycles); Assert.IsFalse(row.AwayArmed);
		}

		[Test]
		public void NegativeAndMissingLocusBranchesResolveWithoutOutOfOrder()
		{
			KingdomGuestFeastBook declined = Bound();
			Assert.IsTrue(KingdomGuestFeastRules.TryBegin(declined, declined.Revision,
				Settlement(), Guest(KingdomGrowthFirstGuestChoiceState.Declined),
				out KingdomGuestFeastReceipt closed, out string failure), failure);
			Assert.AreEqual(KingdomGuestFeastPhase.AwaitingGuestResult, closed.Phase);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestTerminal(declined,
				declined.Revision, Settlement(), Terminal(KingdomGrowthArrivalDisposition.Declined),
				out closed, out failure), failure);
			Assert.AreEqual(KingdomGuestFeastPhase.GuestDeclined, closed.Phase);
			Assert.IsNull(closed.PracticeId);

			KingdomGuestFeastBook outOfOrder = Bound();
			Assert.IsTrue(KingdomGuestFeastRules.TryBegin(outOfOrder, outOfOrder.Revision,
				Settlement(), Guest(), out _, out failure), failure);
			Assert.IsFalse(KingdomGuestFeastRules.TryObservePractice(outOfOrder,
				outOfOrder.Revision, Settlement(), Practice(), out closed, out failure));
			KingdomGrowthFirstGuestOpportunity admitted = Guest(
				KingdomGrowthFirstGuestChoiceState.Admitted);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestDecision(outOfOrder,
				outOfOrder.Revision, Settlement(), admitted, out _, out failure), failure);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestTerminal(outOfOrder,
				outOfOrder.Revision, Settlement(), Terminal(KingdomGrowthArrivalDisposition.Joined),
				out closed, out failure), failure);
			Assert.AreEqual(KingdomGuestFeastPhase.AwaitingPractice, closed.Phase);
			Assert.IsTrue(KingdomGuestFeastRules.TryBuildLocusReceipt(Realm(), Settlement(),
				1, "bench-1", "JoppaWorld.1.1.1.1.10", "r_KingdomBench", 13L,
				out KingdomGuestFeastLocusReceipt exactLocus));
			Assert.IsFalse(KingdomGuestFeastRules.TryObserveLocus(outOfOrder,
				outOfOrder.Revision, Settlement(), exactLocus, out failure));
			Assert.IsTrue(KingdomGuestFeastRules.TryObservePractice(outOfOrder,
				outOfOrder.Revision, Settlement(), Practice(), out closed, out failure), failure);
			Assert.AreEqual(KingdomGuestFeastPhase.AwaitingLocus, closed.Phase);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveLocus(outOfOrder,
				outOfOrder.Revision, Settlement(), exactLocus, out failure), failure);
			KingdomGuestFeastRules.TryFind(outOfOrder, Settlement(), out closed);
			Assert.AreEqual(KingdomGuestFeastPhase.Cycling, closed.Phase);

			KingdomGuestFeastBook refused = Bound();
			Assert.IsTrue(KingdomGuestFeastRules.TryBegin(refused, refused.Revision,
				Settlement(), Guest(KingdomGrowthFirstGuestChoiceState.Admitted),
				out _, out failure), failure);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestTerminal(refused,
				refused.Revision, Settlement(), Terminal(KingdomGrowthArrivalDisposition.Joined),
				out _, out failure), failure);
			Assert.IsFalse(KingdomGuestFeastRules.TryObserveLocus(refused, refused.Revision,
				Settlement(), exactLocus, out failure));
			Assert.IsTrue(KingdomGuestFeastRules.TryObservePractice(refused, refused.Revision,
				Settlement(), Practice(KingdomFirstFeastPhase.Refused), out closed,
				out failure), failure);
			Assert.AreEqual(KingdomGuestFeastPhase.PracticeRefused, closed.Phase);
		}

		[TestCase(KingdomGrowthFirstGuestTerminalState.Departed)]
		[TestCase(KingdomGrowthFirstGuestTerminalState.Died)]
		public void PhysicalGuestDepartureClosesWithoutPracticeRewardOrCycle(
			KingdomGrowthFirstGuestTerminalState observed)
		{
			KingdomGuestFeastBook book = Bound();
			Assert.IsTrue(KingdomGuestFeastRules.TryBegin(book, book.Revision, Settlement(),
				Guest(physical: true), out _, out string failure), failure);
			KingdomGrowthFirstGuestOpportunity admitted = Guest(
				KingdomGrowthFirstGuestChoiceState.Admitted, true);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestDecision(book, book.Revision,
				Settlement(), admitted, out _, out failure), failure);
			KingdomGrowthFirstGuestTerminalReceipt terminal = Terminal(
				KingdomGrowthArrivalDisposition.Departed, true, observed);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestTerminal(book, book.Revision,
				Settlement(), terminal, out KingdomGuestFeastReceipt closed, out failure), failure);
			Assert.AreEqual(KingdomGuestFeastPhase.GuestDeparted, closed.Phase);
			Assert.IsTrue(KingdomGuestFeastRules.IsTerminal(closed.Phase));
			Assert.AreEqual(KingdomGrowthArrivalDisposition.Departed, closed.GuestResult);
			Assert.IsNull(closed.DeedId); Assert.IsNull(closed.PracticeId);
			Assert.AreEqual(KingdomFirstFeastPhase.None, closed.PracticeOutcome);
			Assert.IsNull(closed.LocusProjectionId); Assert.AreEqual(0, closed.HomeCycles);
			Assert.AreEqual(KingdomGuestFeastPointerKind.None, closed.PointerKind);
			long frozen = book.Revision;
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestTerminal(book, 0L,
				Settlement(), terminal, out _, out failure), failure);
			Assert.AreEqual(frozen, book.Revision);
			Assert.IsFalse(KingdomGuestFeastRules.TryObservePractice(book, frozen,
				Settlement(), Practice(), out _, out _));
			Assert.AreEqual(frozen, book.Revision);

			KingdomGrowthFirstGuestTerminalReceipt forged = Terminal(
				KingdomGrowthArrivalDisposition.Departed, true,
				KingdomGrowthFirstGuestTerminalState.Citizen);
			Assert.IsFalse(KingdomGuestFeastRules.TryObserveGuestTerminal(book, frozen,
				Settlement(), forged, out _, out _));
			Assert.AreEqual(frozen, book.Revision);
			KingdomGuestFeastBook restored = KingdomGuestFeastCodec.DecodeEnvelope(
				KingdomGuestFeastCodec.EncodeEnvelope(book));
			Assert.AreEqual(KingdomGuestFeastReceipt.CurrentVersion, restored.Rows[0].Version);
			Assert.AreEqual(KingdomGuestFeastPhase.GuestDeparted, restored.Rows[0].Phase);
		}

		[TestCase(KingdomGrowthArrivalDisposition.Joined,
			KingdomGuestFeastPhase.AwaitingPractice)]
		[TestCase(KingdomGrowthArrivalDisposition.NoAcceptableHome,
			KingdomGuestFeastPhase.GuestCouldNotJoin)]
		public void PhysicalCitizenshipTerminalsKeepTheirExactBranch(
			KingdomGrowthArrivalDisposition result, KingdomGuestFeastPhase expected)
		{
			KingdomGuestFeastBook book = Bound();
			KingdomGrowthFirstGuestOpportunity admitted = Guest(
				KingdomGrowthFirstGuestChoiceState.Admitted, true);
			Assert.IsTrue(KingdomGuestFeastRules.TryBegin(book, book.Revision, Settlement(),
				admitted, out _, out string failure), failure);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestTerminal(book, book.Revision,
				Settlement(), Terminal(result, true),
				out KingdomGuestFeastReceipt row, out failure), failure);
			Assert.AreEqual(expected, row.Phase);
			Assert.AreEqual(result != KingdomGrowthArrivalDisposition.Joined,
				KingdomGuestFeastRules.IsTerminal(row.Phase));
		}

		[Test]
		public void HistoricalBodyLeaseTerminalStillAdvancesToPractice()
		{
			KingdomGuestFeastBook book = Bound();
			Assert.IsTrue(KingdomGuestFeastRules.TryBegin(book, book.Revision, Settlement(),
				Guest(), out _, out string failure), failure);
			KingdomGrowthFirstGuestOpportunity admitted = Guest(
				KingdomGrowthFirstGuestChoiceState.Admitted);
			SetHistoricalBody(admitted, KingdomGrowthFirstGuestBodyLeaseState.Reserved);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestDecision(book, book.Revision,
				Settlement(), admitted, out _, out failure), failure);
			KingdomGrowthFirstGuestTerminalReceipt terminal = Terminal(
				KingdomGrowthArrivalDisposition.Joined);
			SetHistoricalBody(terminal.Opportunity,
				KingdomGrowthFirstGuestBodyLeaseState.Released);
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveGuestTerminal(book, book.Revision,
				Settlement(), terminal, out KingdomGuestFeastReceipt row, out failure), failure);
			Assert.AreEqual(KingdomGuestFeastPhase.AwaitingPractice, row.Phase);
		}

		[Test]
		public void CodecMaximumIsExactAndMalformedAndFutureRemainOpaque()
		{
			KingdomGuestFeastBook book = Bound();
			for (int i = 1; i <= KingdomGuestFeastRules.MaxRows; i++)
			{
				string transaction = i.ToString("x32");
				book.Rows.Add(MaximumRow(Settlement(transaction), transaction, (char)('a' + i)));
			}
			book.Rows.Sort((a, b) => string.CompareOrdinal(a.SettlementId, b.SettlementId));
			book.Revision++;
			Assert.IsTrue(KingdomGuestFeastRules.TryValidate(book, out string failure), failure);
			byte[] maximum = KingdomGuestFeastCodec.EncodeEnvelope(book);
			Assert.AreEqual(KingdomGuestFeastCodec.MaxEnvelopeBytes, maximum.Length);
			KingdomGuestFeastBook restored = KingdomGuestFeastCodec.DecodeEnvelope(maximum);
			CollectionAssert.AreEqual(maximum, KingdomGuestFeastCodec.EncodeEnvelope(restored));

			byte[] corrupt = (byte[])maximum.Clone(); corrupt[corrupt.Length - 1] ^= 1;
			KingdomGuestFeastBook quarantine = KingdomGuestFeastCodec.DecodeEnvelope(corrupt);
			Assert.AreEqual(KingdomExperienceSchemaState.Quarantined, quarantine.SchemaState);
			CollectionAssert.AreEqual(corrupt, KingdomGuestFeastCodec.EncodeEnvelope(quarantine));

			MethodInfo frame = typeof(KingdomGuestFeastCodec).GetMethod("Frame",
				BindingFlags.NonPublic | BindingFlags.Static);
			byte[] future = (byte[])frame.Invoke(null, new object[] { 5, new byte[] { 9 } });
			KingdomGuestFeastBook unknown = KingdomGuestFeastCodec.DecodeEnvelope(future);
			Assert.AreEqual(KingdomExperienceSchemaState.Unknown, unknown.SchemaState);
			CollectionAssert.AreEqual(future, KingdomGuestFeastCodec.EncodeEnvelope(unknown));
			KingdomGuestFeastBook forged = new KingdomGuestFeastBook
			{
				SchemaState = KingdomExperienceSchemaState.Unknown, OpaqueWireVersion = 4,
				OpaqueFuturePayload = new byte[] { 9 },
				OpaqueEnvelope = KingdomGuestFeastCodec.EncodeEnvelope(new KingdomGuestFeastBook())
			};
			Assert.Throws<InvalidDataException>(() => KingdomGuestFeastCodec.EncodeEnvelope(forged));
			Assert.Throws<InvalidDataException>(() => KingdomGuestFeastCodec.DecodeEnvelope(
				new byte[KingdomGuestFeastCodec.MaxEnvelopeBytes + 1]));

			KingdomGuestFeastBook exhausted = Bound(); exhausted.Revision = long.MaxValue;
			byte[] last = KingdomGuestFeastCodec.EncodeEnvelope(exhausted);
			KingdomGuestFeastBook impossible = Bound(); impossible.Revision = long.MaxValue;
			Assert.IsFalse(KingdomGuestFeastCodec.TryPrepareCas(last,
				KingdomGuestFeastCodec.DigestHex(last), impossible, out _, out _, out _),
				"byte CAS must refuse the last revision before addition can wrap");
		}

		[Test]
		public void OptionalOwnerPointersRequireExactPracticeAndNeverGateExhaustion()
		{
			KingdomGuestFeastBook book = Bound();
			KingdomGuestFeastReceipt feast = StartCycling(book);
			long frozen = book.Revision;
			KingdomGrowthFirstGuestOpportunity foreignGuest = Guest(
				KingdomGrowthFirstGuestChoiceState.Admitted);
			foreignGuest.DecisionReceiptId = "taf:growth-first-guest-receipt:"
				+ new string('f', 64);
			Assert.IsFalse(KingdomGuestFeastRules.TryObserveGuestDecision(book, frozen,
				Settlement(), foreignGuest, out _, out _));
			Assert.IsFalse(KingdomGuestFeastRules.TryObservePractice(book, frozen,
				Settlement(), Practice(decided: 9L), out _, out _));
			Assert.AreEqual(frozen, book.Revision);
			KingdomCuriosityBook curiosity = new KingdomCuriosityBook();
			KingdomCuriosityCause cause = new KingdomCuriosityCause
			{
				SourceId = feast.PracticeId, SourceVersion = 1,
				SettlementId = feast.SettlementId, CuratorResidentId = 1,
				CuratorName = "Ava", CuratorObjectId = "taf:object:ava",
				Reason = "The guest named a road.", RequiredCategory = "Historic Sites",
				CompletedTick = feast.PracticeDecisionTick
			};
			var notes = new System.Collections.Generic.List<KingdomCuriosityNote>
			{
				new KingdomCuriosityNote("taf:note:guest-road",
					"JoppaWorld.1.1.1.1.10", "A guest's road", "Historic Sites", true)
			};
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(curiosity, 0L, cause, notes,
				out _, out string failure), failure);
			long before = book.Revision;
			Assert.IsFalse(KingdomGuestFeastRules.TryAttachCuratorPointer(book, before,
				Settlement(), curiosity, "taf:foreign:practice", true, out _));
			Assert.AreEqual(before, book.Revision);
			Assert.IsTrue(KingdomGuestFeastRules.TryAttachCuratorPointer(book, before,
				Settlement(), curiosity, feast.PracticeId, true, out failure), failure);
			KingdomGuestFeastRules.TryFind(book, Settlement(), out feast);
			Assert.AreEqual(KingdomGuestFeastPointerKind.Curator, feast.PointerKind);
			for (int cycle = 0; cycle < 3; cycle++)
			{
				Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, book.Revision,
					Settlement(), false, true, out _, out failure), failure);
				Assert.IsTrue(KingdomGuestFeastRules.TryObserveZoneCycle(book, book.Revision,
					Settlement(), true, true, out _, out failure), failure);
			}
			KingdomGuestFeastRules.TryFind(book, Settlement(), out feast);
			Assert.AreEqual(KingdomGuestFeastPhase.Exhausted, feast.Phase);
			Assert.AreEqual(KingdomGuestFeastPointerKind.Curator, feast.PointerKind);
		}

		[Test]
		public void WireV1GoldenDemotesUnprovenHistoryWithoutInventingAuthority()
		{
			const string golden = "VEdGMQEAAADEAQAAVEdGMQEAAABNAAAAdGFmOnJlYWxtOnYxOjA5NmU3Mjk0ZjljZDYwYjFhMDM5NzIzYzMzZTQ1ZmYxYzA0Yjg5OTk0Njk4ZjcxMTNkZDNmODVlMmI3MDBkMTQBAgAAAAAAAAABAAAAAQAAAAFSAAAAdGFmOnNldHRsZW1lbnQ6djE6ZTg2MjM2NWM3NjM5NDQ3NDFhMDFlZDMzYzJlMDc4MDkxNDI5ZTMxYWJjZTdkZTM1ZGJlMGIzODIzMjhlNjczZWMAAAB0YWY6Z3Jvd3RoLWZpcnN0LWd1ZXN0LW9wcG9ydHVuaXR5OmFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFdAAAAdGFmOmdyb3d0aC1maXJzdC1ndWVzdC1jYXVzZTpiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJi//////////////////////////8BAAAAAAAAAP///////////////////////////////wAAAAAAAAA/ErALHm8MuMDTtTfWp6agnWRvlsGyhCJ0ROZEj+MysQ==";
			KingdomGuestFeastBook migrated = KingdomGuestFeastCodec.DecodeEnvelope(
				Convert.FromBase64String(golden));
			Assert.IsTrue(KingdomGuestFeastRules.TryValidate(migrated, out string failure), failure);
			Assert.AreEqual(2L, migrated.Revision); Assert.AreEqual(1, migrated.Rows.Count);
			KingdomGuestFeastReceipt row = migrated.Rows[0];
			Assert.AreEqual(KingdomGuestFeastPhase.AwaitingGuestChoice, row.Phase);
			Assert.IsNull(row.GrowthTerminalReceiptId); Assert.IsNull(row.DeedId);
			Assert.IsNull(row.LocusProjectionId); Assert.AreEqual(0, row.HomeCycles);
		}

		[Test]
		public void CloneReflectionParityIncludesExactLocusAndIsMutationIsolated()
		{
			KingdomGuestFeastBook original = Bound(); StartCycling(original);
			KingdomGuestFeastBook clone = KingdomGuestFeastRules.Clone(original);
			FieldInfo[] fields = typeof(KingdomGuestFeastReceipt).GetFields(
				BindingFlags.Instance | BindingFlags.Public);
			for (int i = 0; i < fields.Length; i++)
				Assert.AreEqual(fields[i].GetValue(original.Rows[0]),
					fields[i].GetValue(clone.Rows[0]), fields[i].Name);
			clone.Rows[0].LocusObjectId = "changed";
			Assert.AreNotEqual(clone.Rows[0].LocusObjectId, original.Rows[0].LocusObjectId);
		}

		[Test]
		public void FutureOpaqueArraysAndInputAreMutationIsolated()
		{
			MethodInfo frame = typeof(KingdomGuestFeastCodec).GetMethod("Frame",
				BindingFlags.NonPublic | BindingFlags.Static);
			byte[] envelope = (byte[])frame.Invoke(null, new object[] { 5, new byte[] { 9, 8 } });
			byte[] frozen = (byte[])envelope.Clone();
			KingdomGuestFeastBook unknown = KingdomGuestFeastCodec.DecodeEnvelope(envelope);
			envelope[0] ^= 1; unknown.OpaqueFuturePayload[0] ^= 1;
			unknown.OpaqueEnvelope[0] ^= 1;
			CollectionAssert.AreEqual(frozen, KingdomGuestFeastCodec.EncodeEnvelope(
				KingdomGuestFeastCodec.DecodeEnvelope(frozen)));
			Assert.Throws<InvalidDataException>(() => KingdomGuestFeastCodec.EncodeEnvelope(unknown));
		}

		[Test]
		public void DestroyedExactLocusReturnsToAwaitingAndAcceptsOnlyLaterReplacement()
		{
			KingdomGuestFeastBook book = Bound(); KingdomGuestFeastReceipt row = StartCycling(book);
			long replayRevision = book.Revision;
			Assert.IsTrue(KingdomGuestFeastRules.TryObservePractice(book, 0L, Settlement(),
				Practice(), out _, out string replayFailure), replayFailure);
			Assert.AreEqual(replayRevision, book.Revision);
			KingdomGuestFeastLocusReceipt lost = new KingdomGuestFeastLocusReceipt {
				ProjectionId = row.LocusProjectionId, RealmId = row.LocusRealmId,
				SettlementId = row.LocusSettlementId, WorkId = row.LocusWorkId,
				ObjectId = row.LocusObjectId, ZoneId = row.LocusZoneId,
				Blueprint = row.LocusBlueprint, ObservedTick = row.LocusObservedTick };
			Assert.IsTrue(KingdomGuestFeastRules.TryLoseLocus(book, book.Revision,
				Settlement(), lost, out string failure), failure);
			KingdomGuestFeastRules.TryFind(book, Settlement(), out row);
			Assert.AreEqual(KingdomGuestFeastPhase.AwaitingLocus, row.Phase);
			Assert.IsNull(row.LocusProjectionId); Assert.AreEqual(0, row.HomeCycles);
			Assert.IsTrue(KingdomGuestFeastRules.TryBuildLocusReceipt(Realm(), Settlement(),
				2, "bench-2", "JoppaWorld.1.1.1.1.10", "r_KingdomBench", 14L,
				out KingdomGuestFeastLocusReceipt replacement));
			Assert.IsTrue(KingdomGuestFeastRules.TryObserveLocus(book, book.Revision,
				Settlement(), replacement, out failure), failure);
			KingdomGuestFeastRules.TryFind(book, Settlement(), out row);
			Assert.AreEqual(KingdomGuestFeastPhase.Cycling, row.Phase);
			Assert.AreEqual("bench-2", row.LocusObjectId);
		}

		private static KingdomGuestFeastReceipt MaximumRow(string settlement,
			string transaction, char fill)
		{
			string hex = new string(fill <= 'f' ? fill : 'a', 64);
			string practice = KingdomFirstFeastRules.PracticePrefix + hex;
			string candidate = new string('c', KingdomGuestFeastRules.MaxStringBytes);
			string operation = new string('o', KingdomGuestFeastRules.MaxStringBytes);
			string decision = "taf:growth-first-guest-receipt:" + hex;
			long terminalTick = 5L;
			Assert.IsTrue(KingdomGuestFeastRules.TryBuildLocusReceipt(Realm(), settlement,
				1, new string('l', KingdomGuestFeastRules.MaxStringBytes),
				new string('z', KingdomGuestFeastRules.MaxStringBytes),
				new string('b', KingdomGuestFeastRules.MaxStringBytes), 4L,
				out KingdomGuestFeastLocusReceipt locus));
			return new KingdomGuestFeastReceipt
			{
				Phase = KingdomGuestFeastPhase.Cycling, SettlementId = settlement,
				OpportunityId = "taf:growth-first-guest-opportunity:" + hex,
				CauseId = "taf:growth-first-guest-cause:" + hex,
				GuestDecisionReceiptId = decision,
				GrowthTerminalReceiptId = KingdomGrowthFirstGuestIdentityRules.TerminalReceiptId(
					candidate, decision, operation, KingdomGrowthArrivalDisposition.Joined,
					terminalTick), GuestCandidateId = candidate,
				GuestObjectId = new string('b', KingdomGuestFeastRules.MaxStringBytes),
				GuestArrivalOperationId = operation,
				GuestArrivalOutboxEventId = new string('e', KingdomGuestFeastRules.MaxStringBytes),
				GuestName = new string('n', KingdomGuestFeastRules.MaxStringBytes),
				GuestOrigin = new string('r', KingdomGuestFeastRules.MaxStringBytes),
				GuestCreed = new string('k', KingdomGuestFeastRules.MaxStringBytes),
				GuestResidentId = 1, GuestResult = KingdomGrowthArrivalDisposition.Joined,
				GuestTerminalTick = terminalTick,
				DeedId = KingdomFirstFeastRules.DeedPrefix + hex,
				PracticeId = practice, PracticeOutcome = KingdomFirstFeastPhase.Adopted,
				PointerSourceId = practice,
				PointerTargetId = new string('x', KingdomGuestFeastRules.MaxStringBytes),
				CauseTick = 1L, GuestDecisionTick = 2L, PracticeDecisionTick = 3L,
				PointerTick = 4L, HomeCycles = 2,
				LocusProjectionId = locus.ProjectionId, LocusRealmId = locus.RealmId,
				LocusSettlementId = locus.SettlementId, LocusWorkId = locus.WorkId,
				LocusObjectId = locus.ObjectId, LocusZoneId = locus.ZoneId,
				LocusBlueprint = locus.Blueprint, LocusObservedTick = locus.ObservedTick,
				AwayArmed = true,
				PointerKind = KingdomGuestFeastPointerKind.Curator
			};
		}

		private static void SetHistoricalBody(KingdomGrowthFirstGuestOpportunity opportunity,
			KingdomGrowthFirstGuestBodyLeaseState state)
		{
			opportunity.BodyReservationId = "taf:experience-body:first-guest:v1:"
				+ new string('b', 64);
			opportunity.BodyRealmId = Realm();
			opportunity.BodyOptionKind = KingdomExperienceOptionKind.CivicStory;
			opportunity.BodyEnableEpoch = 1L;
			opportunity.BodyReservedTick = opportunity.DecisionTick;
			opportunity.BodyLeaseState = state;
		}
	}
}
