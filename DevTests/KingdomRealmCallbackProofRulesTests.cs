using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomRealmCallbackProofRulesTests
	{
		private static readonly string BeforeA = new string('a', 64);
		private static readonly string AfterA = new string('b', 64);
		private static readonly string BeforeB = new string('c', 64);
		private static readonly string AfterB = new string('d', 64);

		[Test]
		public void DeliveredRequiresDeclaredAfterAndLostRequiresExactBefore()
		{
			ClassicAssert.IsTrue(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, AfterA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Lost, BeforeB, BeforeB, AfterB,
				Terminal: true, out bool lost));
			ClassicAssert.IsTrue(lost);
			ClassicAssert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, BeforeA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Lost, BeforeB, BeforeB, AfterB,
				Terminal: true, out lost));
			ClassicAssert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, AfterA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Lost, AfterB, BeforeB, AfterB,
				Terminal: true, out lost));
		}

		[Test]
		public void ThirdHashAndNoncanonicalHashAlwaysFailClosed()
		{
			ClassicAssert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, new string('e', 64), BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Delivered, AfterB, BeforeB, AfterB,
				Terminal: true, out bool ignored));
			ClassicAssert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, AfterA.ToUpperInvariant(), BeforeA,
				AfterA, KingdomChronicleSinkDisposition.Delivered, AfterB, BeforeB, AfterB,
				Terminal: true, out ignored));
		}

		[Test]
		public void PendingAndAttemptingPermitOnlyFrozenBeforeOrDeclaredAfter()
		{
			ClassicAssert.IsTrue(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Pending, BeforeA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Attempting, AfterB, BeforeB, AfterB,
				Terminal: false, out bool lost));
			ClassicAssert.IsFalse(lost);
			ClassicAssert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Pending, BeforeA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Attempting, AfterB, BeforeB, AfterB,
				Terminal: true, out lost));
		}

		[Test]
		public void ChronicleFaultAllowsOnlyFrozenOrLastLostSinkDiagnostic()
		{
			ClassicAssert.IsTrue(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
				KingdomChronicleSinkDisposition.Delivered,
				KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleSinkDisposition.Delivered,
				"0:outsider-interleaved-after-intent", "old"));
			ClassicAssert.IsFalse(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
				KingdomChronicleSinkDisposition.Delivered,
				KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleSinkDisposition.Delivered,
				"0:official-interleaved", "old"));
			ClassicAssert.IsTrue(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
				KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleSinkDisposition.Delivered,
				KingdomChronicleSinkDisposition.Lost,
				"0:journal-attempt-uncertain", "old"));
			ClassicAssert.IsFalse(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
				KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleSinkDisposition.Delivered,
				KingdomChronicleSinkDisposition.Lost,
				"0:hostile", "old"));
			ClassicAssert.IsFalse(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, false,
				KingdomChronicleSinkDisposition.Attempting,
				KingdomChronicleSinkDisposition.Pending,
				KingdomChronicleSinkDisposition.Pending,
				"0:official-interleaved", "old"));
		}
	}
}
