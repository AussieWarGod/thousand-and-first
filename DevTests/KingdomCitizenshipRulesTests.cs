#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomCitizenshipRulesTests
	{
		[Test]
		public void CitizenshipPartKeepsExactSerializedFieldAbiAndDefaults()
		{
			string source = KingdomCitizenshipLogicalSource.Read();
			StringAssert.Contains("namespace XRL.World.Parts", source);
			StringAssert.Contains("[Serializable]", source);
			StringAssert.Contains("public sealed class r_KingdomCitizenship : IPart", source);
			StringAssert.Contains("namespace ThousandAndFirst", source);
			StringAssert.Contains("public static partial class KingdomCitizenship", source);
			string[] fields =
			{
				"public int ReceiptVersion;", "public KingdomCitizenshipPhase Phase;",
				"public KingdomCitizenshipPriorKind PriorKind;", "public int PriorValue;",
				"public int AppliedValue;", "public string OwnerRealmId = \"\";",
				"public string OwnerSettlementId = \"\";", "public string FactionId = \"\";",
				"public string BodyObjectId = \"\";", "public int EnrollmentReason;",
				"public int RemovalReason;", "public long AppliedTick;",
				"public long RemovedTick;", "public bool NoticePublished;",
				"public string Fault = \"\";"
			};
			int prior = -1;
			for (int i = 0; i < fields.Length; i++)
			{
				int at = source.IndexOf(fields[i], System.StringComparison.Ordinal);
				Assert.Greater(at, prior, "citizenship field order " + i);
				prior = at;
			}
		}

		[TestCase(false, 0)]
		[TestCase(true, -100)]
		[TestCase(true, 0)]
		[TestCase(true, 50)]
		[TestCase(true, 2147483647)]
		[TestCase(true, -2147483648)]
		public void PreparedEnrollmentOwnsOnlyTheExactPriorSlot(bool priorPresent, int priorValue)
		{
			KingdomCitizenshipPriorKind prior = priorPresent
				? KingdomCitizenshipPriorKind.Present : KingdomCitizenshipPriorKind.Absent;
			KingdomCitizenshipMutation expected = priorPresent && priorValue == 100
				? KingdomCitizenshipMutation.ConfirmApplied
				: KingdomCitizenshipMutation.ApplyOwnedValue;
			Assert.AreEqual(expected, KingdomCitizenshipRules.JudgeApply(
				KingdomCitizenshipPhase.Prepared, prior, priorValue, priorPresent,
				priorValue, 100));
		}

		[Test]
		public void ActiveReceiptIsIdempotentOnlyAtItsExactOwnedValue()
		{
			Assert.AreEqual(KingdomCitizenshipMutation.ConfirmApplied,
				KingdomCitizenshipRules.JudgeApply(KingdomCitizenshipPhase.Applied,
					KingdomCitizenshipPriorKind.Present, 37, true, 100, 100));
			Assert.AreEqual(KingdomCitizenshipMutation.Quarantine,
				KingdomCitizenshipRules.JudgeApply(KingdomCitizenshipPhase.Applied,
					KingdomCitizenshipPriorKind.Present, 37, true, 99, 100));
			Assert.AreEqual(KingdomCitizenshipMutation.Quarantine,
				KingdomCitizenshipRules.JudgeApply(KingdomCitizenshipPhase.Applied,
					KingdomCitizenshipPriorKind.Absent, 0, false, 0, 100));
		}

		[TestCase(-100)]
		[TestCase(0)]
		[TestCase(50)]
		[TestCase(100)]
		[TestCase(2147483647)]
		[TestCase(-2147483648)]
		public void RemovalRestoresEveryPriorIntegerExactly(int priorValue)
		{
			Assert.AreEqual(KingdomCitizenshipMutation.RestorePriorValue,
				KingdomCitizenshipRules.JudgeRemove(KingdomCitizenshipPhase.Applied,
					KingdomCitizenshipPriorKind.Present, priorValue, true, 100, 100));
		}

		[Test]
		public void RemovalDeletesOnlyAnOriginallyAbsentSlot()
		{
			Assert.AreEqual(KingdomCitizenshipMutation.RemoveOwnedValue,
				KingdomCitizenshipRules.JudgeRemove(KingdomCitizenshipPhase.Applied,
					KingdomCitizenshipPriorKind.Absent, 0, true, 100, 100));
		}

		[Test]
		public void ExternalInterferenceFailsClosedEvenWhenItLooksLikeAPostState()
		{
			Assert.AreEqual(KingdomCitizenshipMutation.Quarantine,
				KingdomCitizenshipRules.JudgeApply(KingdomCitizenshipPhase.Prepared,
					KingdomCitizenshipPriorKind.Present, 25, true, 100, 100));
			Assert.AreEqual(KingdomCitizenshipMutation.Quarantine,
				KingdomCitizenshipRules.JudgeRemove(KingdomCitizenshipPhase.Applied,
					KingdomCitizenshipPriorKind.Present, 25, true, 25, 100));
		}

		[Test]
		public void LegacyUnknownCanRelinquishButNeverInventAPriorValue()
		{
			Assert.AreEqual(KingdomCitizenshipMutation.RemoveOwnedValue,
				KingdomCitizenshipRules.JudgeRemove(
					KingdomCitizenshipPhase.LegacyPriorUnknown,
					KingdomCitizenshipPriorKind.Unknown, 0, true, 100, 100));
			Assert.AreEqual(KingdomCitizenshipMutation.Quarantine,
				KingdomCitizenshipRules.JudgeRemove(
					KingdomCitizenshipPhase.LegacyPriorUnknown,
					KingdomCitizenshipPriorKind.Unknown, 0, true, 50, 100));
		}

		[Test]
		public void RemovalPostSupportsExactRollbackIncludingLegacyAbsence()
		{
			Assert.IsTrue(KingdomCitizenshipRules.MatchesRemovalPost(
				KingdomCitizenshipPriorKind.Unknown, 0, false, 0));
			Assert.IsTrue(KingdomCitizenshipRules.MatchesRemovalPost(
				KingdomCitizenshipPriorKind.Absent, 0, false, 0));
			Assert.IsTrue(KingdomCitizenshipRules.MatchesRemovalPost(
				KingdomCitizenshipPriorKind.Present, 37, true, 37));
			Assert.IsFalse(KingdomCitizenshipRules.MatchesRemovalPost(
				KingdomCitizenshipPriorKind.Unknown, 0, true, 100));
			Assert.IsFalse(KingdomCitizenshipRules.MatchesRemovalPost(
				KingdomCitizenshipPriorKind.Present, 37, false, 0));
		}

		[Test]
		public void ReceiptShapeBindsLegacyAmbiguityAndRemovalReason()
		{
			Assert.IsTrue(KingdomCitizenshipRules.ValidReceiptShape(
				KingdomCitizenshipPhase.LegacyPriorUnknown,
				KingdomCitizenshipPriorKind.Unknown, 100,
				(int)KingdomCitizenshipEnrollmentReason.LegacyObservation, 0, 0L, 0L));
			Assert.IsFalse(KingdomCitizenshipRules.ValidReceiptShape(
				KingdomCitizenshipPhase.Applied, KingdomCitizenshipPriorKind.Unknown, 100,
				(int)KingdomCitizenshipEnrollmentReason.Arrival, 0, 0L, 0L));
			Assert.IsFalse(KingdomCitizenshipRules.ValidReceiptShape(
				KingdomCitizenshipPhase.Removed, KingdomCitizenshipPriorKind.Absent, 100,
				(int)KingdomCitizenshipEnrollmentReason.Arrival, 0, 0L, 0L));
			Assert.IsTrue(KingdomCitizenshipRules.ValidReceiptShape(
				KingdomCitizenshipPhase.Removed, KingdomCitizenshipPriorKind.Absent, 100,
				(int)KingdomCitizenshipEnrollmentReason.Arrival,
				(int)KingdomCitizenshipRemovalReason.Accession, 0L, 0L));
		}

		[TestCase(-1L)]
		[TestCase(-9223372036854775808L)]
		public void NegativeFrozenTicksAreNeverValidReceipts(long tick)
		{
			Assert.IsFalse(KingdomCitizenshipRules.ValidReceiptShape(
				KingdomCitizenshipPhase.Prepared, KingdomCitizenshipPriorKind.Absent, 100,
				(int)KingdomCitizenshipEnrollmentReason.Arrival, 0, tick, 0L));
		}
	}
}
#endif
