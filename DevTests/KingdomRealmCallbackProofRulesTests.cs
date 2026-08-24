using NUnit.Framework;

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
			Assert.IsTrue(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, AfterA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Lost, BeforeB, BeforeB, AfterB,
				Terminal: true, out bool lost));
			Assert.IsTrue(lost);
			Assert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, BeforeA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Lost, BeforeB, BeforeB, AfterB,
				Terminal: true, out lost));
			Assert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, AfterA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Lost, AfterB, BeforeB, AfterB,
				Terminal: true, out lost));
		}

		[Test]
		public void ThirdHashAndNoncanonicalHashAlwaysFailClosed()
		{
			Assert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, new string('e', 64), BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Delivered, AfterB, BeforeB, AfterB,
				Terminal: true, out bool ignored));
			Assert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Delivered, AfterA.ToUpperInvariant(), BeforeA,
				AfterA, KingdomChronicleSinkDisposition.Delivered, AfterB, BeforeB, AfterB,
				Terminal: true, out ignored));
		}

		[Test]
		public void PendingAndAttemptingPermitOnlyFrozenBeforeOrDeclaredAfter()
		{
			Assert.IsTrue(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Pending, BeforeA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Attempting, AfterB, BeforeB, AfterB,
				Terminal: false, out bool lost));
			Assert.IsFalse(lost);
			Assert.IsFalse(KingdomRealmCallbackProofRules.ChronicleListsMatch(
				KingdomChronicleSinkDisposition.Pending, BeforeA, BeforeA, AfterA,
				KingdomChronicleSinkDisposition.Attempting, AfterB, BeforeB, AfterB,
				Terminal: true, out lost));
		}

		[Test]
		public void ChronicleFaultAllowsOnlyFrozenOrLastLostSinkDiagnostic()
		{
			Assert.IsTrue(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
				KingdomChronicleSinkDisposition.Delivered,
				KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleSinkDisposition.Delivered,
				"0:outsider-interleaved-after-intent", "old"));
			Assert.IsFalse(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
				KingdomChronicleSinkDisposition.Delivered,
				KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleSinkDisposition.Delivered,
				"0:official-interleaved", "old"));
			Assert.IsTrue(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
				KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleSinkDisposition.Delivered,
				KingdomChronicleSinkDisposition.Lost,
				"0:journal-attempt-uncertain", "old"));
			Assert.IsFalse(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
				KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleSinkDisposition.Delivered,
				KingdomChronicleSinkDisposition.Lost,
				"0:hostile", "old"));
			Assert.IsFalse(KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, false,
				KingdomChronicleSinkDisposition.Attempting,
				KingdomChronicleSinkDisposition.Pending,
				KingdomChronicleSinkDisposition.Pending,
				"0:official-interleaved", "old"));
		}
	}
}
