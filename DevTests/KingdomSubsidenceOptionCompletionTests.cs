#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Executes pure storage/publication rules. Codec roundtrips are not engine or game save/load evidence.
	public sealed class KingdomSubsidenceOptionCompletionTests
	{
		private const long Anchor = 500;
		private const long Due = Anchor + KingdomSubsidenceStepRules.StepTicks;
		private const long TargetTick = Due + 500;
		private const string PriorWire = "v1|E|100|2";
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);

		[TestCase(false)] [TestCase(true)]
		public void FirstAdmittedBookClearsOnlyAfterExactOptionPublicationAndExactCheckpoint(bool priorPresent)
		{
			KingdomSubsidenceStepBook original = Admitted();
			string before = Wire(original);
			KingdomSubsidenceStepBook frozen = Freeze(original, priorPresent, out KingdomSubsidenceOptionIntent intent);
			ClassicAssert.IsNull(frozen.Active); ClassicAssert.AreEqual(0, frozen.Sequence);
			ClassicAssert.AreEqual(before, Wire(original));
			ClassicAssert.AreEqual(KingdomSubsidenceStepRules.NoOption, original.OptionModel);
			frozen = RoundTrip(frozen);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TrySnapshot(intent,
				out KingdomSubsidenceOptionRules.Snapshot snapshot));
			KingdomDurableKeyObservation prior = Reading(priorPresent ? 1 : 0, priorPresent ? PriorWire : null);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.CanPublish(snapshot, prior, out string target));
			ClassicAssert.AreEqual(intent.NextWire, target);
			RefusesFinish(frozen, Anchor, prior);
			RefusesFinish(frozen, TargetTick, prior);
			KingdomDurableKeyObservation published = Reading(1, target);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, published));
			RefusesFinish(frozen, Anchor, published);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFinishOption(frozen, TargetTick, published,
				out KingdomSubsidenceStepBook finished));
			AssertOnlyOptionCleared(frozen, finished);
			ClassicAssert.AreEqual(before, Wire(finished));
			RefusesFinish(finished, TargetTick, published);
		}

		[TestCase(false)] [TestCase(true)]
		public void CompletionPreservesRetiredSequenceAndEarnedReceiptForCancelledOrFullStep(bool fullQuota)
		{
			KingdomSubsidenceStepBook frozen = Retired(fullQuota, out KingdomSubsidenceOptionIntent intent);
			frozen = RoundTrip(frozen);
			ClassicAssert.AreEqual(1, frozen.Sequence);
			ClassicAssert.AreEqual(fullQuota ? Due : TargetTick, frozen.LastRetiredTick);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFinishOption(frozen, TargetTick,
				Reading(1, intent.NextWire), out KingdomSubsidenceStepBook finished));
			AssertOnlyOptionCleared(frozen, finished);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(finished, TargetTick,
				TargetTick + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, 1,
				out KingdomSubsidenceStepBook nextStep, 0, "water"));
			ClassicAssert.AreEqual(2, nextStep.Sequence);
			ClassicAssert.AreEqual(finished.LastRetiredTick, nextStep.LastRetiredTick);
		}

		[TestCase(0)] [TestCase(2)] [TestCase(4)] [TestCase(8)] [TestCase(16)]
		[TestCase(3)] [TestCase(5)] [TestCase(9)] [TestCase(17)] [TestCase(31)]
		public void AbsentWrongTableAndMultipleTableTargetsCannotFinishFrozenIntent(int mask)
		{
			KingdomSubsidenceStepBook frozen = Freeze(Admitted(), true, out KingdomSubsidenceOptionIntent intent);
			KingdomDurableKeyObservation observed = Reading(mask, intent.NextWire);
			RefusesFinish(frozen, TargetTick, observed);
			ClassicAssert.AreEqual(mask, Mask(observed));
			ClassicAssert.AreEqual(intent.NextWire, observed.String);
		}

		[TestCase(null)] [TestCase("")] [TestCase("broken")] [TestCase(PriorWire)]
		[TestCase("v1|D|5801|2")] [TestCase("v1|D|5800|3")] [TestCase("v1|E|5800|2")]
		public void PresentEmptyMalformedPriorOrForeignTargetBytesCannotFinish(string wire)
		{
			KingdomSubsidenceStepBook frozen = Freeze(Admitted(), true, out KingdomSubsidenceOptionIntent intent);
			ClassicAssert.AreNotEqual(intent.NextWire, wire);
			KingdomDurableKeyObservation observed = Reading(1, wire);
			RefusesFinish(frozen, TargetTick, observed);
			ClassicAssert.AreEqual(wire, observed.String); ClassicAssert.AreEqual(1, Mask(observed));
		}

		[Test]
		public void MissingObservationCannotProvePublishedTarget()
		{
			RefusesFinish(Freeze(Admitted(), true, out _), TargetTick, null);
		}

		[TestCase(-1L)] [TestCase(0L)] [TestCase(Anchor)] [TestCase(Due)]
		[TestCase(TargetTick - 1)] [TestCase(TargetTick + 1)]
		public void CorrectPublishedOptionDoesNotSubstituteForTheExactTransitionCheckpoint(long checkpoint)
		{
			KingdomSubsidenceStepBook frozen = Retired(true, out KingdomSubsidenceOptionIntent intent);
			RefusesFinish(frozen, checkpoint, Reading(1, intent.NextWire));
		}

		[TestCase(false)] [TestCase(true)]
		public void ActiveStepPreventsOptionCompletionEvenWithPublishedTargetAndExactClock(bool pending)
		{
			KingdomSubsidenceStepBook book = Begin();
			if (pending) ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, Departure(7), out book));
			book = Freeze(book, true, out KingdomSubsidenceOptionIntent intent);
			RefusesFinish(book, TargetTick, Reading(1, intent.NextWire));
		}

		[Test]
		public void RepeatedFreezeIsRefusedWithoutReplacingTheHeldIntent()
		{
			KingdomSubsidenceStepBook frozen = Freeze(Admitted(), true, out KingdomSubsidenceOptionIntent intent);
			string before = Wire(frozen);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryFreezeOption(frozen, intent,
				out KingdomSubsidenceStepBook refused));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(before, Wire(frozen));
		}

		[Test]
		public void FreezeRefusesNullIntentWithoutMutatingAnAdmittedBook()
		{
			KingdomSubsidenceStepBook book = Admitted();
			string before = Wire(book);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryFreezeOption(book, null, out KingdomSubsidenceStepBook refused));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(before, Wire(book));
		}

		[TestCase(false)] [TestCase(true)]
		public void FrozenOptionBlocksNewDeparturesButAllowsExactHeldAcknowledgementAndCredit(bool heldDeparture)
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomResidentDepartureOperation held = Departure(7);
			if (heldDeparture) ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, held, out book));
			book = RoundTrip(Freeze(book, true, out _));
			string before = Wire(book), option = book.OptionModel;
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, Departure(8), out KingdomSubsidenceStepBook refused));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(before, Wire(book));
			if (!heldDeparture) return;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, held, out KingdomSubsidenceStepBook repeated));
			ClassicAssert.AreSame(book, repeated); ClassicAssert.AreEqual(before, Wire(book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, held.OperationId, GrowthStage.City,
				out KingdomSubsidenceStepBook credited));
			ClassicAssert.AreEqual(1, credited.Active.Completed); ClassicAssert.IsTrue(credited.Active.PendingCredited);
			ClassicAssert.AreEqual(option, credited.OptionModel); ClassicAssert.AreEqual(before, Wire(book));
			credited = RoundTrip(credited);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(credited, held.OperationId, GrowthStage.City, out repeated));
			ClassicAssert.AreSame(credited, repeated);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(credited, held.OperationId, out repeated));
			ClassicAssert.AreEqual(option, repeated.OptionModel); ClassicAssert.AreEqual("", repeated.Active.PendingDepartureId);
		}

		private static KingdomSubsidenceStepBook Admitted()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(book, Realm, Settlement, out book)); return book;
		}
		private static KingdomSubsidenceStepBook Begin()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), Anchor, Due,
				GrowthStage.City, 1, out KingdomSubsidenceStepBook book, 0, "water")); return book;
		}
		private static KingdomSubsidenceStepBook Freeze(KingdomSubsidenceStepBook book, bool priorPresent,
			out KingdomSubsidenceOptionIntent intent)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.Observe(Reading(priorPresent ? 1 : 0,
				priorPresent ? PriorWire : null), !priorPresent, 2, TargetTick,
				out KingdomSubsidenceOptionRules.Snapshot snapshot).Valid);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(book, Anchor, snapshot, out intent));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeOption(book, intent, out KingdomSubsidenceStepBook frozen));
			return frozen;
		}
		private static KingdomSubsidenceStepBook Retired(bool fullQuota, out KingdomSubsidenceOptionIntent intent)
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomResidentDepartureOperation held = Departure(7);
			if (fullQuota) ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, held, out book));
			book = Freeze(book, true, out intent);
			if (fullQuota)
			{
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, held.OperationId, GrowthStage.City, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, held.OperationId, out book));
			}
			else ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, TargetTick, 2, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, fullQuota ? Due : TargetTick, out book));
			return book;
		}
		private static KingdomResidentDepartureOperation Departure(int id)
		{
			string body = "option-completion-body-" + id;
			KingdomResidentDepartureOperation result = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion, Revision = 1,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, RealmId = Realm, SettlementId = Settlement,
				ResidentId = id, BodyObjectId = body, ZoneId = "JoppaWorld.12.24.1.1.10",
				ResidentName = "Option completion fixture", PreparedTick = Due,
				OperationId = KingdomResidentDepartureRules.Id(Realm, Settlement, id, body, Due)
			};
			ClassicAssert.IsTrue(KingdomResidentDepartureRules.Valid(result)); return result;
		}
		private static KingdomDurableKeyObservation Reading(int mask, string wire)
		{
			return new KingdomDurableKeyObservation { HasString = (mask & 1) != 0, String = wire,
				HasInt = (mask & 2) != 0, HasInt64 = (mask & 4) != 0,
				HasObject = (mask & 8) != 0, HasBoolean = (mask & 16) != 0 };
		}
		private static int Mask(KingdomDurableKeyObservation reading)
			=> (reading.HasString ? 1 : 0) | (reading.HasInt ? 2 : 0) | (reading.HasInt64 ? 4 : 0)
				| (reading.HasObject ? 8 : 0) | (reading.HasBoolean ? 16 : 0);
		private static string Wire(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire)); return wire;
		}
		private static KingdomSubsidenceStepBook RoundTrip(KingdomSubsidenceStepBook book)
		{
			string wire = Wire(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook restored));
			ClassicAssert.AreEqual(wire, Wire(restored)); return restored;
		}
		private static void RefusesFinish(KingdomSubsidenceStepBook book, long tick, KingdomDurableKeyObservation observed)
		{
			string before = Wire(book);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryFinishOption(book, tick, observed, out KingdomSubsidenceStepBook refused));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(before, Wire(book));
		}
		private static void AssertOnlyOptionCleared(KingdomSubsidenceStepBook before, KingdomSubsidenceStepBook after)
		{
			ClassicAssert.AreNotSame(before, after); ClassicAssert.AreSame(before.Active, after.Active);
			ClassicAssert.AreEqual(before.Admission, after.Admission); ClassicAssert.AreEqual(before.RealmId, after.RealmId);
			ClassicAssert.AreEqual(before.SettlementId, after.SettlementId); ClassicAssert.AreEqual(before.Sequence, after.Sequence);
			ClassicAssert.AreEqual(before.LastRetiredTick, after.LastRetiredTick);
			ClassicAssert.AreEqual(KingdomSubsidenceStepRules.NoOption, after.OptionModel);
			ClassicAssert.AreNotEqual(KingdomSubsidenceStepRules.NoOption, before.OptionModel);
			ClassicAssert.AreEqual(Wire(before.WithOption(KingdomSubsidenceStepRules.NoOption)), Wire(after));
		}
	}
}
#endif
