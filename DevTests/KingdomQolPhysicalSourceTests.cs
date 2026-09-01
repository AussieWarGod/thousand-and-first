#if TAF_TESTS
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomQolPhysicalSourceTests
	{
		[Test]
		public void LiveOfferUsesOneCurrentPhysicalReadingAndAssignedIdentity()
		{
			string source = Source("Core/KingdomQol.Physical.cs");
			StringAssert.Contains("Survey.TryBenefits(out KingdomBenefitIndex benefits", source);
			StringAssert.Contains("Benefits.ReadingForRoot(rootId)", source);
			StringAssert.Contains("Work.IDIfAssigned", source);
			StringAssert.DoesNotContain("Work.ID;", source);
			StringAssert.DoesNotContain("KingdomQol.OfferOf(", source);
			StringAssert.DoesNotContain("DeclaredProvides", source);
		}

		[Test]
		public void CatalogueOfferIsExplicitlyPreviewOnly()
		{
			string source = Source("Core/KingdomQol.cs");
			StringAssert.Contains("catalogue ceilings for previews and authoring validation", source);
			StringAssert.Contains("This is catalogue-preview authority only", source);
			StringAssert.Contains("Runtime building supply must use", source);
			StringAssert.Contains("TryPhysicalOfferOf", source);
			StringAssert.Contains("CatalogueOfferOf", source);
			StringAssert.Contains("use CatalogueOfferOf or TryPhysicalOfferOf.", source);
			string questions = Source("Core/KingdomQolQuestions.cs");
			foreach (string name in new[] { "PreviewJudge", "PreviewWillLive",
				"PreviewTolerates", "PreviewPreferShade", "PreviewPreferFlags",
				"PreviewFirstTolerable" }) StringAssert.Contains(name, questions);
			Assert.GreaterOrEqual(System.Text.RegularExpressions.Regex.Matches(questions,
				"Catalogue preview only; use Preview").Count, 6);
		}

		private static string Source(string Relative)
		{
			string path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
				"..", "..", "..", "..", Relative));
			if (!File.Exists(path)) path = Path.GetFullPath(Relative);
			return File.ReadAllText(path);
		}
	}
}
#endif
