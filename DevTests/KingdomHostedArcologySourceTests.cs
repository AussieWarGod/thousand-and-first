#if TAF_TESTS
using System;
using System.IO;
using System.Xml;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomHostedArcologySourceTests
	{
		private static string Read(params string[] Parts)
		{
			return TestMain.ReadRepositoryText(Path.Combine(Parts));
		}

		[Test]
		public void CatalogueMakesOneFinalHeartShellAndPrivateHostedLots()
		{
			XmlDocument doc = new XmlDocument(); doc.LoadXml(Read("RuntimeData", "KingdomBuildings.xml"));
			XmlElement court = (XmlElement)doc.SelectSingleNode("//building[@Key='heartcourt']");
			XmlElement shell = (XmlElement)doc.SelectSingleNode("//building[@Key='arcology']");
			XmlElement ward = (XmlElement)doc.SelectSingleNode("//building[@Key='arcologyward']");
			XmlElement terrace = (XmlElement)doc.SelectSingleNode("//building[@Key='arcologyterrace']");
			Assert.AreEqual("arcology", court.GetAttribute("UpgradesTo"));
			Assert.AreEqual("yes", shell.GetAttribute("Capital"));
			Assert.AreEqual("yes", shell.GetAttribute("Megastructure"));
			Assert.AreEqual("arcology", ward.GetAttribute("Strata"));
			Assert.AreEqual("arcology", terrace.GetAttribute("Strata"));
			Assert.AreEqual("r_KingdomArcologyGrowbed",
				terrace.GetAttribute("HostedProducerBlueprint"));
			Assert.AreEqual("14", terrace.GetAttribute("HostedProducerCount"));
			StringAssert.DoesNotContain("surface", ward.GetAttribute("Strata"));
			StringAssert.DoesNotContain("surface", terrace.GetAttribute("Strata"));
		}

		[Test]
		public void BlueprintsUsePersistentVanillaInteriorsAndNoSurfaceSimulation()
		{
			XmlDocument doc = new XmlDocument(); doc.LoadXml(Read("RuntimeData", "ObjectBlueprints.xml"));
			XmlNode root = doc.SelectSingleNode("//object[@Name='r_KingdomArcology']");
			Assert.IsNotNull(root.SelectSingleNode("part[@Name='r_KingdomArcology']"));
			Assert.IsNotNull(root.SelectSingleNode("part[@Name='Interior'][@Cell='TAFArcologyAtrium']"));
			Assert.IsNotNull(root.SelectSingleNode("part[@Name='NoDamage']"));
			Assert.IsNull(root.SelectSingleNode("part[@Name='Bed']"));
			XmlNode ward = doc.SelectSingleNode("//object[@Name='r_KingdomArcologyWard']");
			XmlNode terrace = doc.SelectSingleNode("//object[@Name='r_KingdomArcologyTerrace']");
			Assert.IsNull(ward.SelectSingleNode("part[@Name='Bed']"));
			Assert.AreEqual("Furniture", terrace.Attributes["Inherits"].Value);
			Assert.IsNull(terrace.SelectSingleNode("tag[@Name='r_KingdomCropRows']"));
			XmlNode growbed = doc.SelectSingleNode("//object[@Name='r_KingdomArcologyGrowbed']");
			Assert.IsNull(growbed.SelectSingleNode("part[@Name='r_KingdomPlot']"));
			Assert.IsNull(growbed.SelectSingleNode("tag[@Name='r_KingdomCropRows']"));
			Assert.AreEqual("2", growbed.SelectSingleNode(
				"tag[@Name='r_TAF_HostedCropRows']").Attributes["Value"].Value);
			Assert.IsNotNull(doc.SelectSingleNode(
				"//object[@Name='r_KingdomArcologyWardLift']/part[@Name='Interior'][@Cell='TAFArcologyWard']"));
			Assert.IsNotNull(doc.SelectSingleNode(
				"//object[@Name='r_KingdomArcologyExit']/part[@Name='InteriorPortal']"));
		}

		[Test]
		public void InteriorWorldOwnsExactlyTheThreeBoundedHostedCells()
		{
			XmlDocument doc = new XmlDocument(); doc.LoadXml(Read("RuntimeData", "Worlds.xml"));
			Assert.AreEqual(1, doc.SelectNodes("/worlds/world[@Name='Interior']").Count);
			Assert.AreEqual(1, doc.SelectNodes("//cell[@Name='TAFArcologyAtrium']").Count);
			Assert.AreEqual(1, doc.SelectNodes("//cell[@Name='TAFArcologyWard']").Count);
			Assert.AreEqual(1, doc.SelectNodes("//cell[@Name='TAFArcologyTerrace']").Count);
			Assert.AreEqual(3, doc.SelectNodes(
				"//builder[@Class='KingdomHostedArcologyBuilder']").Count);
		}

		[Test]
		public void HostedTerraceFoodIsBackedByExactlyTwentyEightPhysicalRows()
		{
			string visual = Read("Growth", "KingdomHostedArcology.Visual.cs");
			int growbeds = System.Text.RegularExpressions.Regex.Matches(visual,
				"new Fixture\\([^)]*\\\"r_KingdomArcologyGrowbed\\\"").Count;
			Assert.AreEqual(14, growbeds);
			Assert.AreEqual(14, growbeds * 2
				* KingdomCropRules.YieldPerRow / KingdomCropRules.CropDays);
			string rules = Read("Growth", "KingdomHostedArcologyRules.cs");
			StringAssert.Contains("PhysicalProducerBlueprint = \"r_KingdomArcologyGrowbed\"",
				rules);
			StringAssert.Contains("PhysicalProducerCount = 14", rules);
			string runtime = Read("Growth", "KingdomHostedArcology.Runtime.cs");
			StringAssert.Contains("row.Supports != definition.Supports", runtime);
			StringAssert.Contains("row.RequiresWater != definition.RequiresWater", runtime);
		}

		[Test]
		public void AuthorityAndConstructionFailClosedAtExactCarrierBoundaries()
		{
			string authority = Read("Growth", "KingdomHostedArcology.Authority.cs");
			string begin = Read("Growth", "KingdomUpgrade.14.Begin.cs");
			string handover = Read("Growth", "KingdomUpgrade.20.HandOver.cs");
			string construction = Read("Growth", "KingdomHostedArcology.Construction.cs");
			StringAssert.Contains("AdvanceLaborAfterMasterEdge", construction);
			StringAssert.Contains("System.MasterOptionTick", construction);
			string zoning = Read("Growth", "KingdomZoning.02.OffersAndJudgment.cs");
			string strike = Read("Growth", "KingdomMaterials.08.StrikeOrdering.cs");
			StringAssert.Contains("AuthoritySlotKeys", authority);
			StringAssert.Contains("AuthoritySlotForWrite", authority);
			StringAssert.DoesNotContain("AuthorityPrefix + System.RealmId", authority);
			StringAssert.Contains("KingdomCrown.CrownedOn(System, ZoneId)", authority);
			StringAssert.Contains("another exact carrier", authority);
			StringAssert.Contains("|| !system.ClaimedZones.Contains(zone.ZoneID)) return true;",
				authority);
			StringAssert.Contains("TryReserve(System, Z, Work", begin);
			StringAssert.Contains("BitCostFor(A.SuccessorKey)", begin);
			StringAssert.Contains("BindAuthority(ownerSystem", handover);
			StringAssert.Contains("CanReserveAt(System, ZoneID", zoning);
			StringAssert.Contains("Building.GetPart<r_KingdomArcology>()", strike);
			StringAssert.Contains("cannot be struck", strike);
			StringAssert.Contains("TryDecodeLot(Job.PhysicalReceipt", construction);
			StringAssert.Contains("physical.StaffingBasis = 0", construction);
		}

		[Test]
		public void ReadOnlyProviderReceivesLoadedHostAndExposesNoResearchMutation()
		{
			string source = Read("Growth", "KingdomHostedArcology.Interaction.cs");
			StringAssert.Contains("KingdomHostedReadOnlyEligibility(KingdomSystem System,",
				source);
			StringAssert.Contains("Zone HostZone, GameObject HostRoot, out string Refusal", source);
			StringAssert.Contains("provider.Eligibility(system, shell.CurrentZone, shell", source);
			StringAssert.Contains("provider.View(system)", source);
			StringAssert.Contains("Zone provedZone = shell.CurrentZone", source);
			StringAssert.Contains("changed while the view was drawn", source);
			StringAssert.DoesNotContain("Learn(", source);
			StringAssert.DoesNotContain("Queue", source);
			StringAssert.DoesNotContain("Budget", source);
		}

		[Test]
		public void RuntimeNeverLoadsRemoteZonesOrSimulatesHostedActors()
		{
			string runtime = Read("Growth", "KingdomHostedArcology.Runtime.cs");
			StringAssert.Contains("int need = 0", runtime);
			StringAssert.Contains("need = 4", runtime);
			StringAssert.Contains("&& Operational(work)", runtime);
			string source = Read("Growth", "KingdomHostedArcology.Authority.cs")
				+ Read("Growth", "KingdomHostedArcology.Construction.cs")
				+ runtime
				+ Read("Growth", "KingdomHostedArcology.Visual.cs");
			StringAssert.DoesNotContain("ZoneManager.GetZone", source);
			StringAssert.DoesNotContain("GetZone(", source);
			StringAssert.DoesNotContain("DirectMove", source);
			StringAssert.DoesNotContain("IsCreature", source);
			StringAssert.DoesNotContain("BaseHumanoid", source);
		}

		[Test]
		public void EveryHostedProductionShardStaysBelowThreeHundredLines()
		{
			string[] files = new string[] {
				"Growth/KingdomHostedLotDefinition.cs",
				"Growth/KingdomHostedArcologyRules.cs",
				"Growth/KingdomHostedArcologyReceipt.cs",
				"Growth/KingdomHostedArcology.Authority.cs",
				"Growth/KingdomHostedArcology.Construction.cs",
				"Growth/KingdomHostedArcology.Interaction.cs",
				"Growth/KingdomHostedArcology.Runtime.cs",
				"Growth/KingdomHostedArcology.Visual.cs",
				"Growth/r_KingdomArcology.cs",
				"Growth/r_KingdomArcologyZoneAnchor.cs",
				"World/KingdomHostedArcologyBuilder.cs"
			};
			for (int i = 0; i < files.Length; i++)
			{
				int lines = Read(files[i].Split('/')).Split('\n').Length;
				Assert.Less(lines, 300, files[i]);
			}
		}
	}
}
#endif
