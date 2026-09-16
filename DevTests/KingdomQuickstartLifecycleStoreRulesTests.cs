#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// VALUE tests for the store selection the lifecycle steps consult (native run 38, 2fa563c:
	/// the heart's own dry store joined the bootstrap chest mid-advance and a scan-based "exactly
	/// one" refused the save). Pure and engine-free, compiled in both public projects. The
	/// source-only pins over how the steps call this class live in
	/// DevTests/KingdomQuickstartLifecycleContractTests.cs.
	/// </summary>
	[TestFixture]
	public sealed class KingdomQuickstartLifecycleStoreRulesTests
	{
		private static readonly string[] Two = { "495", "1203" };

		[Test]
		public void AmbiguityWithABoundIdResolvesToTheBoundStore()
		{
			ClassicAssert.IsTrue(KingdomQuickstartLifecycleStoreRules.Select("495", Two,
				out string selected, out string failure), failure);
			ClassicAssert.AreEqual("495", selected);
			ClassicAssert.IsNull(failure);
			ClassicAssert.IsTrue(KingdomQuickstartLifecycleStoreRules.Select("1203", Two,
				out selected, out failure), failure);
			ClassicAssert.AreEqual("1203", selected, "the bound store wins whatever its scan position");
		}

		[Test]
		public void AmbiguityWithoutABindingRefusesRatherThanGuessing()
		{
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleStoreRules.Select(null, Two,
				out string selected, out string failure));
			ClassicAssert.IsNull(selected);
			StringAssert.Contains("more than one dedicated stockpile", failure);
			StringAssert.Contains("495,1203", failure);
			StringAssert.Contains("none is bound yet", failure);
		}

		[Test]
		public void ABoundStoreThatNoLongerStandsRefusesNamingWhatDoes()
		{
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleStoreRules.Select("7", Two,
				out string selected, out string failure));
			ClassicAssert.IsNull(selected);
			StringAssert.Contains("bound at lifecycle-open (7) no longer stands", failure);
			StringAssert.Contains("2 dedicated store(s) stand: 495,1203", failure);
		}

		[Test]
		public void ASingleUnboundStoreResolvesAndNoneReadsTheExactWaitText()
		{
			ClassicAssert.IsTrue(KingdomQuickstartLifecycleStoreRules.Select(null, new[] { "495" },
				out string selected, out string failure));
			ClassicAssert.AreEqual("495", selected);
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleStoreRules.Select(null, new string[0],
				out selected, out failure));
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStoreRules.NoneFailure, failure);
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleStoreRules.Select("495", new string[0],
				out selected, out failure));
			StringAssert.Contains("no longer stands here, and no dedicated stockpile does", failure);
		}

		[Test]
		public void TheOpenedReceiptRoundTripsItsStoreIdAndRefusesMalformedText()
		{
			string receipt = KingdomQuickstartLifecycleStoreRules.OpenedReceipt("JoppaWorld.8.22.1.1.10", "495", 128725L);
			ClassicAssert.AreEqual("JoppaWorld.8.22.1.1.10|495|128725", receipt);
			ClassicAssert.AreEqual("495", KingdomQuickstartLifecycleStoreRules.BoundStoreId(receipt));
			ClassicAssert.IsNull(KingdomQuickstartLifecycleStoreRules.BoundStoreId(null));
			ClassicAssert.IsNull(KingdomQuickstartLifecycleStoreRules.BoundStoreId(""));
			ClassicAssert.IsNull(KingdomQuickstartLifecycleStoreRules.BoundStoreId("zone|495"));
			ClassicAssert.IsNull(KingdomQuickstartLifecycleStoreRules.BoundStoreId("zone||1"));
			ClassicAssert.IsNull(KingdomQuickstartLifecycleStoreRules.BoundStoreId("a|b|c|d"));
		}
	}
}
#endif
