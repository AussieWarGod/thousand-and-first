#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

using ThousandAndFirst.Tests;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityNamedFigurePresentationRulesTests
	{
		[Test]
		public void ActiveDeedFigureAppearsWithoutProofIdentifiers()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityFigurePromotionFacts facts = new KingdomPolityFigurePromotionFacts
			{
				PolityId = KingdomPolityTestData.Realm,
				SettlementId = KingdomPolityTestData.Settlement,
				ResidentId = 41, DisplayName = "Nara", RoleKey = "patrol",
				Origin = KingdomPolityFigureOrigin.PromotedByDeed,
				CauseRef = "taf:fact:deed:v1:rich-find",
				ChronicleRef = "taf:chronicle:rich-find",
				DeedSummary = "returned from a salvage expedition with a rich find"
			};
			Assert.IsTrue(KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision,
				facts, out KingdomPolityPublicationResult _, out string failure), failure);
			Assert.IsTrue(KingdomPolityNamedFigurePresentationRules.TryActiveDeeds(ledger,
				KingdomPolityTestData.Realm, KingdomPolityTestData.Settlement,
				out List<KingdomPolityNamedFigureView> views, out failure), failure);
			Assert.AreEqual(1, views.Count);
			Assert.AreEqual("Nara", views[0].DisplayName);
			Assert.AreEqual("patrol", views[0].Role);
			Assert.AreEqual(facts.DeedSummary, views[0].DeedSummary);
			StringAssert.DoesNotContain("taf:", views[0].DisplayName + views[0].Role +
				views[0].DeedSummary);
		}

		[Test]
		public void ConcludedFigureStaysOutOfRoll()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			ledger.NamedFigures.Add(new KingdomPolityNamedFigureRecord
			{
				FigureId = "taf:figure:promotion:v1:departed", PolityId = KingdomPolityTestData.Realm,
				DisplayName = "Old Nara", RoleKey = "patrol",
				Origin = KingdomPolityFigureOrigin.PromotedByDeed,
				Phase = KingdomPolityFigurePhase.Departed,
				CauseRef = "taf:fact:deed:v1:old", DeedSummary = "found an old cistern",
				ConclusionRef = "taf:conclusion:resident-transition:v1:old"
			});
			ledger.NamedFigures.Sort((a, b) => string.CompareOrdinal(a.FigureId, b.FigureId));
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			Assert.IsTrue(KingdomPolityNamedFigurePresentationRules.TryActiveDeeds(ledger,
				KingdomPolityTestData.Realm, KingdomPolityTestData.Settlement,
				out List<KingdomPolityNamedFigureView> views, out failure), failure);
			CollectionAssert.IsEmpty(views);
		}

		[Test]
		public void SettlerRollWiresSafeNotableAppendix()
		{
			string roll = TestMain.ReadRepositoryText("Core/KingdomReportsPeople.cs");
			string appendix = TestMain.ReadRepositoryText(
				"Core/KingdomReportsPeople.Notables.cs");
			StringAssert.Contains("AppendDeedNotables(stringBuilder, System, state.SettlementId)",
				roll);
			StringAssert.Contains("Notable deeds:", appendix);
			StringAssert.Contains("KingdomPresentation.Rich(figure.DeedSummary)", appendix);
			StringAssert.DoesNotContain("CauseRef", appendix);
			StringAssert.DoesNotContain("ChronicleRef", appendix);
			StringAssert.DoesNotContain("FigureId", appendix);
		}
	}
}
#endif
