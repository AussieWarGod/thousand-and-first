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
