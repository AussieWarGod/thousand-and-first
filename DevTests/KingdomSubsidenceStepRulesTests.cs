#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomResidentDepartureRules.Valid(departure));
			return departure;
		}

		private static KingdomSubsidenceStepBook Begin(int quota = 5)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(fresh, Realm, Settlement, out KingdomSubsidenceStepBook admitted));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(admitted, Anchor,
				Anchor + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, quota,
				out KingdomSubsidenceStepBook active, 0, "water"));
			return active;
		}

		private static KingdomSubsidenceStepBook RoundTrip(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook decoded));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(decoded, out string repeated));
			ClassicAssert.AreEqual(wire, repeated);
			return decoded;
		}

		private static KingdomSubsidenceStepBook Credit(KingdomSubsidenceStepBook book, int ordinal,
			GrowthStage stage = GrowthStage.City)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(ordinal), out book));
			book = RoundTrip(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(ordinal), stage, out book));
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
				ClassicAssert.AreEqual(i, book.Active.Completed);
				ClassicAssert.AreEqual(5, book.Active.Quota);
				ClassicAssert.AreEqual(step, book.Active.Id);
				ClassicAssert.AreEqual(Anchor, book.Active.AnchorTick);
				ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, Anchor, long.MaxValue,
					GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
				ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long _));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(i), out book));
				book = RoundTrip(book);
				ClassicAssert.AreEqual(i == 5, KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long _));
			}
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long due));
			ClassicAssert.AreEqual(Anchor + KingdomSubsidenceStepRules.StepTicks, due);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Anchor, out KingdomSubsidenceStepBook _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, due, out book));
			ClassicAssert.IsNull(book.Active);
			ClassicAssert.AreEqual(1L, book.Sequence);
			ClassicAssert.AreEqual(due, book.LastRetiredTick);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, Anchor, due,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, due, due,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(book, due, due + KingdomSubsidenceStepRules.StepTicks,
				GrowthStage.City, 5, out book, 0, "water"));
			ClassicAssert.AreEqual(2L, book.Sequence);
			ClassicAssert.AreNotEqual(step, book.Active.Id);
		}

		[Test]
		public void CreditAndAcknowledgementAreAtomicAndExact()
		{
			KingdomSubsidenceStepBook book = Credit(Begin(), 1);
			ClassicAssert.IsTrue(book.Active.PendingCredited);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.City, out KingdomSubsidenceStepBook duplicate));
			ClassicAssert.AreSame(book, duplicate);
			ClassicAssert.AreEqual(1, duplicate.Active.Completed);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCredit(book, Departure(2), GrowthStage.City, out duplicate));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(2), out duplicate));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(2), out duplicate));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryReleaseRolledBack(book, Departure(1), out duplicate));
		}

		[Test]
		public void RollbackReleasesAssociationWithoutInventingCredit()
		{
			KingdomSubsidenceStepBook book = Begin();
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(1), out book));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out KingdomSubsidenceStepBook _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRolledBack(book, Departure(1), out book));
			ClassicAssert.AreEqual(0, book.Active.Completed);
			ClassicAssert.AreEqual("", book.Active.PendingDepartureId);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(2), out book));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CancellationWaitsForPendingCommitOrProvenRollback(bool committed)
		{
			KingdomSubsidenceStepBook book = Begin();
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(1), out book));
			long cancelledAt = book.Active.DueTick + 300;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, cancelledAt, 7, out book));
			book = RoundTrip(book);
			ClassicAssert.AreEqual(KingdomSubsidenceStepPhase.Departing, book.Active.Phase);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long _));
			if (committed)
			{
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.City, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			}
			else ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRolledBack(book, Departure(1), out book));
			book = RoundTrip(book);
			ClassicAssert.AreEqual(committed ? 1 : 0, book.Active.Completed);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(2), out KingdomSubsidenceStepBook _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long checkpoint));
			ClassicAssert.AreEqual(cancelledAt, checkpoint);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, checkpoint, out book));
		}

		[Test]
		public void EarnedRungDebtSurvivesCancellationAndPreventsRetirement()
		{
			KingdomSubsidenceStepBook book = Credit(Begin(), 1, GrowthStage.Town);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, book.Active.DueTick, 0, out book));
			book = RoundTrip(book);
			ClassicAssert.AreEqual(GrowthStage.Town, book.Active.ReachedStage);
			ClassicAssert.AreEqual("sr1:pending", book.Active.RungModel);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, book.Active.DueTick, out KingdomSubsidenceStepBook _));
		}

		[TestCase(-1L)]
		[TestCase(501L)]
		[TestCase(5299L)]
		[TestCase(5301L)]
		public void OnlyExactClockBeforeOrAfterCanRetire(long checkpoint)
		{
			KingdomSubsidenceStepBook book = Credit(Begin(1), 1);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, checkpoint, out long _));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("ss1:bad")]
		[TestCase("ss2:new")]
		[TestCase(" ss1:new")]
		[TestCase("ss1:new\n")]
		public void InvalidAdmissionNeverBecomesFresh(string wire)
		{
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook book));
			ClassicAssert.IsNull(book);
		}

		[Test]
		public void CodecRejectsNoncanonicalPayloadAndInvalidMutableState()
		{
			KingdomSubsidenceStepBook book = Begin();
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryDecode(wire.Insert(12, "\n"), out KingdomSubsidenceStepBook _));
			byte[] bytes = Convert.FromBase64String(wire.Substring(4));
			Array.Resize(ref bytes, bytes.Length + 1);
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryDecode("ss1:" + Convert.ToBase64String(bytes), out KingdomSubsidenceStepBook _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryDecode(new string('x', KingdomSubsidenceStepCodec.MaxWireChars + 1), out KingdomSubsidenceStepBook _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(completed: 6), 1), out string _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(pendingCredited: true), 1), out string _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(rungModel: "anything"), 1), out string _));
		}

		[Test]
		public void AdmissionCannotRebindAndArithmeticCannotOverflow()
		{
			KingdomSubsidenceStepBook book = Begin();
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryAdmit(book, Realm,
				KingdomIdentityRules.SettlementPrefix + new string('c', 64), out KingdomSubsidenceStepBook _));
			KingdomSubsidenceStepBook empty = new KingdomSubsidenceStepBook(book.Admission,
				Realm, Settlement, long.MaxValue, null, Anchor);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryBegin(empty, Anchor, long.MaxValue,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
			empty = new KingdomSubsidenceStepBook(book.Admission, Realm, Settlement, 1, null, Anchor);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryBegin(empty, long.MaxValue - 1, long.MaxValue,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryBegin(empty, -1, long.MaxValue,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook _, 0, "water"));
		}

		[Test]
		public void ReleasedCreditCannotBeReusedForAnotherSlot()
		{
			KingdomSubsidenceStepBook book = Begin(3);
			for (int i = 1; i <= 2; i++)
			{
				book = Credit(book, i);
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(i), out book));
				book = RoundTrip(book);
				ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(1), out KingdomSubsidenceStepBook _));
				ClassicAssert.AreEqual(i, book.Active.Completed);
			}
			string first = Departure(1).Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + Departure(1);
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(
				creditedDepartureIds: first + first), book.Sequence), out string _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book.With(book.Active.Copy(
				creditedDepartureIds: "0" + book.Active.CreditedDepartureIds), book.Sequence), out string _));
		}

		[Test]
		public void CompletedStepCannotCancelOrChangeItsEarnedCheckpoint()
		{
			KingdomSubsidenceStepBook book = Credit(Begin(1), 1);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, book.Active.DueTick + 10, 7,
				out KingdomSubsidenceStepBook _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, book.Active.DueTick + 10, 7,
				out KingdomSubsidenceStepBook _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long target));
			ClassicAssert.AreEqual(book.Active.DueTick, target);
		}

		[Test]
		public void IdempotentCreditAndCancellationMustRepeatExactWitnesses()
		{
			KingdomSubsidenceStepBook book = Credit(Begin(), 1, GrowthStage.Town);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.Town,
				out KingdomSubsidenceStepBook repeated));
			ClassicAssert.AreSame(book, repeated);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.City, out repeated));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.Village, out repeated));
			long tick = book.Active.DueTick + 10;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, tick, 7, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, tick, 7, out repeated));
			ClassicAssert.AreSame(book, repeated);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, tick + 1, 7, out repeated));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCancel(book, tick, 8, out repeated));
		}

		[Test]
		public void FirstActiveSequenceCannotCarryAPreviouslyRetiredCheckpoint()
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomSubsidenceStepBook corrupt = new KingdomSubsidenceStepBook(book.Admission,
				book.RealmId, book.SettlementId, 1, book.Active, Anchor);
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(corrupt, out string _));
		}

		[Test]
		public void LastPendingCommitAfterCancellationStillSpendsOnlyTheCompletedStep()
		{
			KingdomSubsidenceStepBook book = Begin(1);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, PreparedDeparture(1), out book));
			long due = book.Active.DueTick;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, due + 500, 7, out book));
			book = RoundTrip(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, Departure(1), GrowthStage.City, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, Departure(1), out book));
			book = RoundTrip(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long target));
			ClassicAssert.AreEqual(due, target);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, due + 500, out KingdomSubsidenceStepBook _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, target, out book));
			ClassicAssert.AreEqual(due, book.LastRetiredTick);
		}
	}
}
#endif
