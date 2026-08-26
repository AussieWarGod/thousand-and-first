#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomCovenantRulesTests
	{
		[TestCase(null, null)]
		[TestCase("", "")]
		[TestCase("  ", "\t")]
		public void AbsentPairPreservesOpenLegacyCatalogue(string faction, string standing)
		{
			Assert.IsTrue(KingdomZoningRules.TryParseCovenantAttributes("hut", faction, standing,
				out CovenantGate gate, out string error));
			Assert.IsNull(error);
			Assert.IsTrue(gate.IsOpen);
			Assert.IsTrue(KingdomZoningRules.JudgeCovenant(gate, int.MinValue).Permitted);
		}

		[Test]
		public void CompletePairFreezesFactionAndThreshold()
		{
			Assert.IsTrue(KingdomZoningRules.TryParseCovenantAttributes("reliquary",
				"  Mechanimists  ", " 250 ", out CovenantGate gate, out string error));
			Assert.IsNull(error);
			Assert.AreEqual("Mechanimists", gate.Faction);
			Assert.AreEqual(250, gate.MinStanding);
			Assert.IsFalse(gate.IsOpen);
		}

		[TestCase("Mechanimists", null)]
		[TestCase(null, "250")]
		[TestCase("Mechanimists", "not-a-number")]
		[TestCase("Mechanimists", "-1001")]
		[TestCase("Mechanimists", "1001")]
		[TestCase("Mechani\nmists", "250")]
		public void MalformedPairsFailLoudly(string faction, string standing)
		{
			Assert.IsFalse(KingdomZoningRules.TryParseCovenantAttributes("foreignwork",
				faction, standing, out CovenantGate gate, out string error));
			Assert.IsTrue(gate.IsOpen);
			Assert.IsFalse(string.IsNullOrEmpty(error));
			StringAssert.Contains("building foreignwork", error);
		}

		[Test]
		public void OversizedFactionKeyIsRejectedBeforeRegistryLookup()
		{
			string oversized = new string('x', KingdomZoningRules.CovenantFactionMaxLength + 1);
			Assert.IsFalse(KingdomZoningRules.TryParseCovenantAttributes("work", oversized, "0",
				out _, out string error));
			StringAssert.Contains("overlong", error);
		}

		[Test]
		public void BoundaryIsHardAndVerdictOrdinalIsAppendOnly()
		{
			CovenantGate gate = new CovenantGate("Consortium", 400);
			ZoningJudgement below = KingdomZoningRules.JudgeCovenant(gate, 399);
			Assert.AreEqual(ZoningVerdict.RefusedCovenantStanding, below.Verdict);
			Assert.AreEqual("Consortium", below.Detail);
			StringAssert.Contains("400", below.Note);
			Assert.IsTrue(KingdomZoningRules.JudgeCovenant(gate, 400).Permitted);
			Assert.IsTrue(KingdomZoningRules.JudgeCovenant(gate, 900).Permitted);
			Assert.AreEqual(13, (int)ZoningVerdict.RefusedCovenantStanding,
				"published verdict ordinals may only be appended");
		}

		[Test]
		public void CovenantAttributesAreLiveMergeGatesNotSpentOrStampedState()
		{
			Assert.AreEqual(MergeReach.Read,
				KingdomMergeRules.Classify(KingdomMergeRules.AttrCovenant));
			Assert.AreEqual(MergeReach.Read,
				KingdomMergeRules.Classify(KingdomMergeRules.AttrMinStanding));
		}

		[Test]
		public void RuntimeLoaderAndCentralJudgementConsumeTheMergedPair()
		{
			string data = TestMain.ReadRepositoryText(Path.Combine("Core", "KingdomData.cs"));
			StringAssert.Contains("xml.GetAttribute(\"Covenant\")", data);
			StringAssert.Contains("xml.GetAttribute(\"MinStanding\")", data);
			StringAssert.Contains("TryParseCovenantAttributes(design.Key", data);
			StringAssert.Contains("Factions.GetIfExists(covenant.Faction) == null", data);
			StringAssert.Contains("entry.CovenantFaction = covenant.Faction", data);

			string zoning = TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomZoning.cs"));
			int judge = zoning.IndexOf("private static ZoningJudgement JudgeAt", StringComparison.Ordinal);
			int covenant = zoning.IndexOf("JudgeCovenant(", judge, StringComparison.Ordinal);
			int ordinary = zoning.IndexOf("KingdomZoningRules.Judge(GateFor", judge, StringComparison.Ordinal);
			Assert.Greater(covenant, judge);
			Assert.Greater(ordinary, covenant, "covenant must refuse before plot/knowledge gates");
			StringAssert.Contains("case ZoningVerdict.RefusedCovenantStanding:", zoning);
		}

		[Test]
		public void ShippedCatalogueExercisesSeveralReachableCovenants()
		{
			string catalogue = TestMain.ReadRepositoryText("KingdomBuildings.xml");
			StringAssert.Contains("Covenant=\"Barathrumites\" MinStanding=\"250\"", catalogue);
			StringAssert.Contains("Covenant=\"Mechanimists\" MinStanding=\"250\"", catalogue);
			StringAssert.Contains("Covenant=\"Consortium\" MinStanding=\"400\"", catalogue);
		}
	}
}
#endif
