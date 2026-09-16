#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>VALUE tests for which root a teardown case reads (run 43: the fixture kept reading
	/// the retired works root after production re-rooted the output on the final building).</summary>
	[TestFixture]
	public sealed class KingdomTeardownRootResolutionTests
	{
		[Test]
		public void ACompleteRowsOutputIdWinsOverTheCapturedWorksRoot()
		{
			string id = KingdomTeardownRootResolution.Choose("works-1", true, true, "final-9", null, out string source);
			ClassicAssert.AreEqual("final-9", id);
			ClassicAssert.AreEqual(KingdomTeardownRootResolution.SourceFinalOutput, source);
			// The final root may keep the works id (rooted in place): still the row's word.
			ClassicAssert.AreEqual("works-1", KingdomTeardownRootResolution.Choose("works-1", true, true, "works-1", null, out source));
			ClassicAssert.AreEqual(KingdomTeardownRootResolution.SourceFinalOutput, source);
		}

		[Test]
		public void AWorkingRowStillReadsTheWorksRoot()
		{
			foreach (string output in new[] { null, "", "works-1", "final-9" })
			{
				string id = KingdomTeardownRootResolution.Choose("works-1", true, false, output, "built-3", out string source);
				ClassicAssert.AreEqual("works-1", id, "output=" + output);
				ClassicAssert.AreEqual(KingdomTeardownRootResolution.SourceWorksRoot, source);
			}
		}

		[Test]
		public void ACompactedRowFallsBackToTheBuiltReceiptHolderThenTheWorksRoot()
		{
			string id = KingdomTeardownRootResolution.Choose("works-1", false, false, null, "built-3", out string source);
			ClassicAssert.AreEqual("built-3", id);
			ClassicAssert.AreEqual(KingdomTeardownRootResolution.SourceBuiltReceipt, source);
			id = KingdomTeardownRootResolution.Choose("works-1", false, false, null, "", out source);
			ClassicAssert.AreEqual("works-1", id);
			ClassicAssert.AreEqual(KingdomTeardownRootResolution.SourceWorksRoot, source);
		}

		[Test]
		public void ACompleteRowWithoutAnOutputIdNeverInventsOne()
		{
			string id = KingdomTeardownRootResolution.Choose("works-1", true, true, "", "built-3", out string source);
			ClassicAssert.AreEqual("works-1", id);
			ClassicAssert.AreEqual(KingdomTeardownRootResolution.SourceWorksRoot, source);
		}
	}
}
#endif
