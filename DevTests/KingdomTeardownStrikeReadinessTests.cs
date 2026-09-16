#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>VALUE tests for the strike-readiness verdict (run 45: a just-completed building's
	/// own terminal receipt was not yet supersedable and the case refused instead of waiting).</summary>
	[TestFixture]
	public sealed class KingdomTeardownStrikeReadinessTests
	{
		[Test]
		public void ATerminalReceiptWhoseClosureIsUnsettledWaitsRatherThanRefusing()
		{
			ClassicAssert.AreEqual(KingdomTeardownStrikeReadiness.Verdict.WaitClosure,
				KingdomTeardownStrikeReadiness.Judge(true, true, true, true, false));
			ClassicAssert.AreEqual("real", KingdomTeardownStrikeReadiness.ReceiptSource(true, true));
		}

		[Test]
		public void ASupersedableTerminalReceiptStrikes()
		{
			ClassicAssert.AreEqual(KingdomTeardownStrikeReadiness.Verdict.Strike,
				KingdomTeardownStrikeReadiness.Judge(true, true, true, true, true));
		}

		[Test]
		public void NoReceiptOrACompactedRowStrikes()
		{
			ClassicAssert.AreEqual(KingdomTeardownStrikeReadiness.Verdict.Strike,
				KingdomTeardownStrikeReadiness.Judge(true, false, false, false, false));
			ClassicAssert.AreEqual(KingdomTeardownStrikeReadiness.Verdict.Strike,
				KingdomTeardownStrikeReadiness.Judge(true, true, false, false, false));
			ClassicAssert.AreEqual("none", KingdomTeardownStrikeReadiness.ReceiptSource(false, false));
			ClassicAssert.AreEqual("real-row-compacted", KingdomTeardownStrikeReadiness.ReceiptSource(true, false));
		}

		[Test]
		public void ANonTerminalForeignReceiptRefusesAndAnUnbuiltRootWaits()
		{
			ClassicAssert.AreEqual(KingdomTeardownStrikeReadiness.Verdict.RefuseForeignReceipt,
				KingdomTeardownStrikeReadiness.Judge(true, true, true, false, false));
			foreach (bool any in new[] { false, true })
				ClassicAssert.AreEqual(KingdomTeardownStrikeReadiness.Verdict.WaitBuilt,
					KingdomTeardownStrikeReadiness.Judge(false, any, any, any, any));
		}
	}
}
#endif
