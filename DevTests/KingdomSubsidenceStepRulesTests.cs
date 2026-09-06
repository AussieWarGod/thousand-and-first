#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomSubsidenceStepRulesTests
	{
		private const long Anchor = 500L;
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);
		private static string Departure(int ordinal) => PreparedDeparture(ordinal).OperationId;

		private static KingdomResidentDepartureOperation PreparedDeparture(int ordinal)
		{
			string body = "subsidence-test-body-" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
			long tick = Anchor + KingdomSubsidenceStepRules.StepTicks;
			KingdomResidentDepartureOperation departure = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, Revision = 1,
				RealmId = Realm, SettlementId = Settlement, ResidentId = ordinal,
				BodyObjectId = body, ZoneId = "JoppaWorld.12.24.1.1.10",
				ResidentName = "Subsidence fixture resident", PreparedTick = tick,
				OperationId = KingdomResidentDepartureRules.Id(Realm, Settlement, ordinal, body, tick)
			};
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(departure));
			return departure;
		}

		private static KingdomSubsidenceStepBook Begin(int quota = 5)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(fresh, Realm, Settlement, out KingdomSubsidenceStepBook admitted));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(admitted, Anchor,
				Anchor + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, quota,
				out KingdomSubsidenceStepBook active, 0, "water"));
			return active;
		}

		private static KingdomSubsidenceStepBook RoundTrip(KingdomSubsidenceStepBook book)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook decoded));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(decoded, out string repeated));
			Assert.AreEqual(wire, repeated);
			return decoded;
		}

		private static KingdomSubsidenceStepBook Credit(KingdomSubsidenceStepBook book, int ordinal,
			GrowthStage stage = GrowthStage.City)
		{
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(ordinal), out book));
			book = RoundTrip(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(ordinal), stage, out book));
			return RoundTrip(book);
		}

		[Test]
		public void PartialHouseholdKeepsItsOriginalQuotaAndClockAcrossEveryRoundTrip()
		{
			KingdomSubsidenceStepBook book = Begin();
			string step = book.Active.Id;
			for (int i = 1; i <= 5; i++)
			{
				book = Credit(book, i);
				Assert.AreEqual(i, book.Active.Completed);
				Assert.AreEqual(5, book.Active.Quota);
				Assert.AreEqual(step, book.Active.Id);
				Assert.AreEqual(Anchor, book.Active.AnchorTick);
				Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, Anchor, long.MaxValue,
					GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
				Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long _));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(i), out book));
				book = RoundTrip(book);
				Assert.AreEqual(i == 5, KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long _));
			}
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long due));
			Assert.AreEqual(Anchor + KingdomSubsidenceStepRules.StepTicks, due);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Anchor, out KingdomSubsidenceStepBook _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, due, out book));
			Assert.IsNull(book.Active);
			Assert.AreEqual(1L, book.Sequence);
			Assert.AreEqual(due, book.LastRetiredTick);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, Anchor, due,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, due, due,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(book, due, due + KingdomSubsidenceStepRules.StepTicks,
				GrowthStage.City, 5, out book, 0, "water"));
			Assert.AreEqual(2L, book.Sequence);
			Assert.AreNotEqual(step, book.Active.Id);
		}

		[Test]
		public void CreditAndAcknowledgementAreAtomicAndExact()
		{
			KingdomSubsidenceStepBook book = Credit(Begin(), 1);
			Assert.IsTrue(book.Active.PendingCredited);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.City, out KingdomSubsidenceStepBook duplicate));
			Assert.AreSame(book, duplicate);
			Assert.AreEqual(1, duplicate.Active.Completed);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCredit(book, Departure(2), GrowthStage.City, out duplicate));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(2), out duplicate));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(2), out duplicate));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryReleaseRolledBack(book, Departure(1), out duplicate));
		}

		[Test]
		public void RollbackReleasesAssociationWithoutInventingCredit()
		{
			KingdomSubsidenceStepBook book = Begin();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(1), out book));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out KingdomSubsidenceStepBook _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRolledBack(book, Departure(1), out book));
			Assert.AreEqual(0, book.Active.Completed);
			Assert.AreEqual("", book.Active.PendingDepartureId);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(2), out book));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CancellationWaitsForPendingCommitOrProvenRollback(bool committed)
		{
			KingdomSubsidenceStepBook book = Begin();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(1), out book));
			long cancelledAt = book.Active.DueTick + 300;
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, cancelledAt, 7, out book));
			book = RoundTrip(book);
			Assert.AreEqual(KingdomSubsidenceStepPhase.Departing, book.Active.Phase);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long _));
			if (committed)
			{
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.City, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			}
			else Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRolledBack(book, Departure(1), out book));
			book = RoundTrip(book);
			Assert.AreEqual(committed ? 1 : 0, book.Active.Completed);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(2), out KingdomSubsidenceStepBook _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long checkpoint));
			Assert.AreEqual(cancelledAt, checkpoint);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, checkpoint, out book));
		}

		[Test]
		public void EarnedRungDebtSurvivesCancellationAndPreventsRetirement()
		{
			KingdomSubsidenceStepBook book = Credit(Begin(), 1, GrowthStage.Town);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, book.Active.DueTick, 0, out book));
			book = RoundTrip(book);
			Assert.AreEqual(GrowthStage.Town, book.Active.ReachedStage);
			Assert.AreEqual("sr1:pending", book.Active.RungModel);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long _));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, book.Active.DueTick, out KingdomSubsidenceStepBook _));
		}

		[TestCase(-1L)]
		[TestCase(501L)]
		[TestCase(5299L)]
		[TestCase(5301L)]
		public void OnlyExactClockBeforeOrAfterCanRetire(long checkpoint)
		{
			KingdomSubsidenceStepBook book = Credit(Begin(1), 1);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, checkpoint, out long _));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("ss1:bad")]
		[TestCase("ss2:new")]
		[TestCase(" ss1:new")]
		[TestCase("ss1:new\n")]
		public void InvalidAdmissionNeverBecomesFresh(string wire)
		{
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook book));
			Assert.IsNull(book);
		}

		[Test]
		public void CodecRejectsNoncanonicalPayloadAndInvalidMutableState()
		{
			KingdomSubsidenceStepBook book = Begin();
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryDecode(wire.Insert(12, "\n"), out KingdomSubsidenceStepBook _));
			byte[] bytes = Convert.FromBase64String(wire.Substring(4));
			Array.Resize(ref bytes, bytes.Length + 1);
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryDecode("ss1:" + Convert.ToBase64String(bytes), out KingdomSubsidenceStepBook _));
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryDecode(new string('x', KingdomSubsidenceStepCodec.MaxWireChars + 1), out KingdomSubsidenceStepBook _));
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(completed: 6), 1), out string _));
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(pendingCredited: true), 1), out string _));
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(rungModel: "anything"), 1), out string _));
		}

		[Test]
		public void AdmissionCannotRebindAndArithmeticCannotOverflow()
		{
			KingdomSubsidenceStepBook book = Begin();
			Assert.IsFalse(KingdomSubsidenceStepRules.TryAdmit(book, Realm,
				KingdomIdentityRules.SettlementPrefix + new string('c', 64), out KingdomSubsidenceStepBook _));
			KingdomSubsidenceStepBook empty = new KingdomSubsidenceStepBook(book.Admission,
				Realm, Settlement, long.MaxValue, null, Anchor);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(empty, Anchor, long.MaxValue,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
			empty = new KingdomSubsidenceStepBook(book.Admission, Realm, Settlement, 1, null, Anchor);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(empty, long.MaxValue - 1, long.MaxValue,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(empty, -1, long.MaxValue,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
		}

		[Test]
		public void ReleasedCreditCannotBeReusedForAnotherSlot()
		{
			KingdomSubsidenceStepBook book = Begin(3);
			for (int i = 1; i <= 2; i++)
			{
				book = Credit(book, i);
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(i), out book));
				book = RoundTrip(book);
				Assert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(1), out KingdomSubsidenceStepBook _));
				Assert.AreEqual(i, book.Active.Completed);
			}
			string first = Departure(1).Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + Departure(1);
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(
				creditedDepartureIds: first + first), book.Sequence), out string _));
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(
				creditedDepartureIds: "0" + book.Active.CreditedDepartureIds), book.Sequence), out string _));
		}

		[Test]
		public void CompletedStepCannotCancelOrChangeItsEarnedCheckpoint()
		{
			KingdomSubsidenceStepBook book = Credit(Begin(1), 1);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, book.Active.DueTick + 10, 7,
				out KingdomSubsidenceStepBook _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, book.Active.DueTick + 10, 7,
				out KingdomSubsidenceStepBook _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long target));
			Assert.AreEqual(book.Active.DueTick, target);
		}

		[Test]
		public void IdempotentCreditAndCancellationMustRepeatExactWitnesses()
		{
			KingdomSubsidenceStepBook book = Credit(Begin(), 1, GrowthStage.Town);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.Town,
				out KingdomSubsidenceStepBook repeated));
			Assert.AreSame(book, repeated);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.City, out repeated));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.Village, out repeated));
			long tick = book.Active.DueTick + 10;
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, tick, 7, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, tick, 7, out repeated));
			Assert.AreSame(book, repeated);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, tick + 1, 7, out repeated));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, tick, 8, out repeated));
		}

		[Test]
		public void FirstActiveSequenceCannotCarryAPreviouslyRetiredCheckpoint()
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomSubsidenceStepBook corrupt = new KingdomSubsidenceStepBook(book.Admission,
				book.RealmId, book.SettlementId, 1, book.Active, Anchor);
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(corrupt, out string _));
		}

		[Test]
		public void LastPendingCommitAfterCancellationStillSpendsOnlyTheCompletedStep()
		{
			KingdomSubsidenceStepBook book = Begin(1);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(1), out book));
			long due = book.Active.DueTick;
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, due + 500, 7, out book));
			book = RoundTrip(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.City, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			book = RoundTrip(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long target));
			Assert.AreEqual(due, target);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, due + 500, out KingdomSubsidenceStepBook _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, target, out book));
			Assert.AreEqual(due, book.LastRetiredTick);
		}
	}
}
#endif
