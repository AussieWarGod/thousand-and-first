#if TAF_TESTS
using System;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCanvasHomeTests
	{
		[Test]
		public void PaidRoomWitnessReadsCompiledCoordinateAnchorsAcrossEveryMediumVariantAndPose()
		{
			var corpus = KingdomArchitectureCorpusFixture.Load();
			int checkedPoses = 0;
			foreach (var item in corpus.Cases.Where(c => c.Binding.Size == ArchitectureLotSize.Medium
				&& (c.Tier.BuildKey == "tentrow" || c.Tier.BuildKey == "hutyard")))
			foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
			{
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(
					KingdomArchitectureCorpusFixture.Request(corpus, item, facing), out var snapshot, out string failure), failure);
				int expected = item.Tier.BuildKey == "tentrow" ? 17 : item.Variant.Key == "fallback" ? 16 : 15;
				ClassicAssert.AreEqual(expected, Harness.KingdomPaidHousingRoomRules.ExpectedFloor(snapshot),
					item.Tier.BuildKey + "/" + item.Variant.Key + "/" + facing);
				checkedPoses++;
			}
			ClassicAssert.GreaterOrEqual(checkedPoses, 7 * 4);
		}

		[Test]
		public void EveryCanvasConversionRetainsProtectedFixturesAndFundsItsCompiledDelta()
		{
			var corpus = KingdomArchitectureCorpusFixture.Load();
			var routes = XDocument.Parse(TestMain.ReadRepositoryText("Architecture/KingdomArchitectureTransitions.xml"));
			int checkedRoutes = 0, checkedPoses = 0;
			foreach (var route in routes.Root.Elements("transition"))
			{
				string from = (string)route.Attribute("From"), to = (string)route.Attribute("To");
				if (from != "tent" && from != "tentrow") continue;
				string sizeKey = (string)route.Attribute("Size");
				var size = sizeKey == "S" ? ArchitectureLotSize.Small : sizeKey == "M"
					? ArchitectureLotSize.Medium : sizeKey == "L" ? ArchitectureLotSize.Large : ArchitectureLotSize.Huge;
				var sources = corpus.Cases.Where(c => c.Tier.BuildKey == from && c.Binding.Size == size).ToList();
				var targets = corpus.Cases.Where(c => c.Tier.BuildKey == to && c.Binding.Size == size).ToList();
				ClassicAssert.Greater(sources.Count, 0); ClassicAssert.Greater(targets.Count, 0);
				ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterialCost((string)route.Attribute("Materials"),
					out var bill, out string failure), failure);
				foreach (var source in sources)
				foreach (var target in targets)
				foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
				{
					string context = from + "->" + to + "/" + sizeKey + "/" + target.Variant.Key + "/" + facing;
					ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(
						KingdomArchitectureCorpusFixture.Request(corpus, source, facing), out var before, out failure), context + failure);
					ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(
						KingdomArchitectureCorpusFixture.Request(corpus, target, facing), out var after, out failure), context + failure);
					// Production freezes the declared socket edge onto the chosen target snapshot.
					after.IncomingTransitionMode = ArchitectureTransitionMode.Renovate;
					ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after,
						ArchitectureTransitionMode.Renovate, out var delta, out failure), context + ": " + failure);
					ClassicAssert.IsFalse(delta.Removed.Any(p => !string.IsNullOrEmpty(p.StatefulAnchor) || p.ExistingAuthority), context);
					foreach (var added in delta.Added.Where(p => !p.Natural && !p.ExistingAuthority))
					{
						ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterial(added.Material, out var material));
						ClassicAssert.Greater(bill.Get(material), 0, context + ": added " + added.Blueprint + "/" + added.Material);
					}
					checkedPoses++;
				}
				checkedRoutes++;
			}
			ClassicAssert.AreEqual(24, checkedRoutes);
			ClassicAssert.GreaterOrEqual(checkedPoses, 24 * 4);
		}

		[TestCase(ArchitectureLotSize.Medium)]
		[TestCase(ArchitectureLotSize.Large)]
		[TestCase(ArchitectureLotSize.Huge)]
		public void SharedShelterUpgradeAddsTwoPaidBedsWithoutStrikingExistingFurniture(ArchitectureLotSize Size)
		{
			var corpus = KingdomArchitectureCorpusFixture.Load();
			var beforeCase = corpus.Cases.Single(c => c.Tier.BuildKey == "tent" && c.Binding.Size == Size);
			var afterCase = corpus.Cases.Single(c => c.Tier.BuildKey == "tentrow" && c.Binding.Size == Size);
			var entry = XDocument.Parse(TestMain.ReadRepositoryText("RuntimeData/KingdomBuildings.xml"))
				.Root.Elements("building").Single(b => (string)b.Attribute("Key") == "tent");
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterialCost((string)entry.Attribute("UpgradeMaterials"),
				out var bill, out string failure), failure);
			foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
			{
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(
					KingdomArchitectureCorpusFixture.Request(corpus, beforeCase, facing), out var before, out failure), failure);
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(
					KingdomArchitectureCorpusFixture.Request(corpus, afterCase, facing), out var after, out failure), failure);
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after, out var delta, out failure), failure);
				ClassicAssert.AreEqual(2, delta.Added.Count(p => p.Blueprint == "r_KingdomFixtureBedrollCanvas"));
				ClassicAssert.AreEqual(0, delta.Removed.Count(p => !string.IsNullOrEmpty(p.StatefulAnchor) || p.ExistingAuthority));
				foreach (var added in delta.Added.Where(p => !p.Natural && !p.ExistingAuthority))
				{
					ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterial(added.Material, out var material));
					ClassicAssert.Greater(bill.Get(material), 0, Size + "/" + facing + ": " + added.Slot);
				}
			}
		}
	}
}
#endif
