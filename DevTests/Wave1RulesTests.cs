#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class Wave1RulesTests
	{
		[TestCase("garrison", 2)]
		[TestCase("agrarian", 0)]
		[TestCase("market", 0)]
		[TestCase("craft", 0)]
		[TestCase("shrine", 0)]
		[TestCase("academy", 0)]
		[TestCase("nonesuch", 0)]
		[TestCase("", 0)]
		[TestCase(null, 0)]
		public void DistrictDefenceBonus(string district, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.DistrictDefenceBonus(district));
		}

		[TestCase("agrarian", 90)]
		[TestCase("market", 100)]
		[TestCase("craft", 100)]
		[TestCase("shrine", 100)]
		[TestCase("garrison", 100)]
		[TestCase("academy", 100)]
		[TestCase("nonesuch", 100)]
		[TestCase("", 100)]
		[TestCase(null, 100)]
		public void DistrictUpkeepPercent(string district, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.DistrictUpkeepPercent(district));
		}

		[TestCase("market", 1)]
		[TestCase("agrarian", 0)]
		[TestCase("craft", 0)]
		[TestCase("shrine", 0)]
		[TestCase("garrison", 0)]
		[TestCase("academy", 0)]
		[TestCase("nonesuch", 0)]
		[TestCase("", 0)]
		[TestCase(null, 0)]
		public void DistrictShopTierBonus(string district, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.DistrictShopTierBonus(district));
		}

		[TestCase("craft", 80)]
		[TestCase("agrarian", 100)]
		[TestCase("market", 100)]
		[TestCase("shrine", 100)]
		[TestCase("garrison", 100)]
		[TestCase("academy", 100)]
		[TestCase("nonesuch", 100)]
		[TestCase("", 100)]
		[TestCase(null, 100)]
		public void DistrictBuildPercent(string district, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.DistrictBuildPercent(district));
		}

		[TestCase("shrine", 75)]
		[TestCase("agrarian", 100)]
		[TestCase("market", 100)]
		[TestCase("craft", 100)]
		[TestCase("garrison", 100)]
		[TestCase("academy", 100)]
		[TestCase("nonesuch", 100)]
		[TestCase("", 100)]
		[TestCase(null, 100)]
		public void DistrictPetitionIntervalPercent(string district, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.DistrictPetitionIntervalPercent(district));
		}

		[TestCase("academy", 50)]
		[TestCase("agrarian", 100)]
		[TestCase("market", 100)]
		[TestCase("craft", 100)]
		[TestCase("shrine", 100)]
		[TestCase("garrison", 100)]
		[TestCase("nonesuch", 100)]
		[TestCase("", 100)]
		[TestCase(null, 100)]
		public void DistrictDriftPercent(string district, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.DistrictDriftPercent(district));
		}

		[Test]
		public void EachDistrictMovesExactlyOneQuantity()
		{
			string[] keys = KingdomRules.Districts;
			Assert.AreEqual(6, keys.Length, "the six districts are the menu; a seventh needs its own effect");
			for (int i = 0; i < keys.Length; i++)
			{
				string[] one = new string[1] { keys[i] };
				int moved = 0;
				if (KingdomRules.DistrictsDefenceBonus(one) != 0)
				{
					moved++;
				}
				if (KingdomRules.DistrictsUpkeepPercent(one) != KingdomRules.DistrictNeutralPercent)
				{
					moved++;
				}
				if (KingdomRules.DistrictsShopTierBonus(one) != 0)
				{
					moved++;
				}
				if (KingdomRules.DistrictsBuildPercent(one) != KingdomRules.DistrictNeutralPercent)
				{
					moved++;
				}
				if (KingdomRules.DistrictsPetitionIntervalPercent(one) != KingdomRules.DistrictNeutralPercent)
				{
					moved++;
				}
				if (KingdomRules.DistrictsDriftPercent(one) != KingdomRules.DistrictNeutralPercent)
				{
					moved++;
				}
				Assert.AreEqual(1, moved, keys[i] + " must earn its menu entry by moving exactly one aggregated quantity");
			}
		}

		[Test]
		public void NoDistrictsLeavesEveryQuantityWhole()
		{
			string[] none = new string[0];
			Assert.AreEqual(0, KingdomRules.DistrictsDefenceBonus(none));
			Assert.AreEqual(0, KingdomRules.DistrictsShopTierBonus(none));
			Assert.AreEqual(100, KingdomRules.DistrictsUpkeepPercent(none));
			Assert.AreEqual(100, KingdomRules.DistrictsBuildPercent(none));
			Assert.AreEqual(100, KingdomRules.DistrictsPetitionIntervalPercent(none));
			Assert.AreEqual(100, KingdomRules.DistrictsDriftPercent(none));

			Assert.AreEqual(0, KingdomRules.DistrictsDefenceBonus(null), "a realm with no claims is not a realm with a penalty");
			Assert.AreEqual(0, KingdomRules.DistrictsShopTierBonus(null));
			Assert.AreEqual(100, KingdomRules.DistrictsUpkeepPercent(null));
			Assert.AreEqual(100, KingdomRules.DistrictsBuildPercent(null));
			Assert.AreEqual(100, KingdomRules.DistrictsPetitionIntervalPercent(null));
			Assert.AreEqual(100, KingdomRules.DistrictsDriftPercent(null));
		}

		[Test]
		public void PercentDistrictsDoNotStack()
		{
			Assert.AreEqual(90, KingdomRules.DistrictsUpkeepPercent(new string[3] { "agrarian", "agrarian", "agrarian" }), "a second vinelands feeds the same city, not the city twice");
			Assert.AreEqual(80, KingdomRules.DistrictsBuildPercent(new string[2] { "craft", "craft" }));
			Assert.AreEqual(75, KingdomRules.DistrictsPetitionIntervalPercent(new string[4] { "shrine", "shrine", "shrine", "shrine" }));
			Assert.AreEqual(50, KingdomRules.DistrictsDriftPercent(new string[3] { "academy", "academy", "academy" }));
			Assert.AreEqual(1, KingdomRules.DistrictsShopTierBonus(new string[3] { "market", "market", "market" }), "a second bazaar is another place to shop, not deeper stock in both");
		}

		[Test]
		public void DefenceStacksAcrossClaimedZones()
		{
			Assert.AreEqual(2, KingdomRules.DistrictsDefenceBonus(new string[1] { "garrison" }));
			Assert.AreEqual(6, KingdomRules.DistrictsDefenceBonus(new string[3] { "garrison", "garrison", "garrison" }), "bodies on a wall are the one thing that plainly adds up");
			Assert.AreEqual(4, KingdomRules.DistrictsDefenceBonus(new string[4] { "garrison", "market", "garrison", "academy" }));
		}

		[Test]
		public void AggregatesIgnoreBlankAndUnknownKeys()
		{
			string[] mixed = new string[6] { null, "", "   ", "necropolis", "agrarian", null };
			Assert.AreEqual(90, KingdomRules.DistrictsUpkeepPercent(mixed));
			Assert.AreEqual(0, KingdomRules.DistrictsDefenceBonus(mixed));
			Assert.AreEqual(0, KingdomRules.DistrictsShopTierBonus(mixed));
			Assert.AreEqual(100, KingdomRules.DistrictsBuildPercent(mixed), "an unknown key must not reach into a quantity it does not own");
			Assert.AreEqual(100, KingdomRules.DistrictsPetitionIntervalPercent(mixed));
			Assert.AreEqual(100, KingdomRules.DistrictsDriftPercent(mixed));
		}

		[Test]
		public void EveryDistrictReadsOnItsOwnQuantityWhenClaimedTogether()
		{
			string[] all = new string[6] { "agrarian", "market", "craft", "shrine", "garrison", "academy" };
			Assert.AreEqual(2, KingdomRules.DistrictsDefenceBonus(all));
			Assert.AreEqual(1, KingdomRules.DistrictsShopTierBonus(all));
			Assert.AreEqual(90, KingdomRules.DistrictsUpkeepPercent(all));
			Assert.AreEqual(80, KingdomRules.DistrictsBuildPercent(all));
			Assert.AreEqual(75, KingdomRules.DistrictsPetitionIntervalPercent(all));
			Assert.AreEqual(50, KingdomRules.DistrictsDriftPercent(all));
		}

		[Test]
		public void AggregatedPercentsStayInTheDocumentedBand()
		{
			string[] probes = new string[9] { "agrarian", "market", "craft", "shrine", "garrison", "academy", "nonesuch", "", null };
			for (int i = 0; i < probes.Length; i++)
			{
				string[] one = new string[2] { probes[i], probes[i] };
				AssertInBand(KingdomRules.DistrictsUpkeepPercent(one), probes[i], "upkeep");
				AssertInBand(KingdomRules.DistrictsBuildPercent(one), probes[i], "build");
				AssertInBand(KingdomRules.DistrictsPetitionIntervalPercent(one), probes[i], "petition");
				AssertInBand(KingdomRules.DistrictsDriftPercent(one), probes[i], "drift");
			}
		}

		private static void AssertInBand(int percent, string district, string quantity)
		{
			string where = quantity + " under " + (district ?? "null");
			Assert.IsTrue(percent >= KingdomRules.DistrictPercentFloor, where + " cut below the documented floor");
			Assert.IsTrue(percent <= KingdomRules.DistrictNeutralPercent, where + " rose above whole, which no district may do");
		}

		[TestCase("common", true)]
		[TestCase("verdant", true)]
		[TestCase("fungal", true)]
		[TestCase("moonstair", true)]
		[TestCase("eater", true)]
		[TestCase("gyre", false)]
		[TestCase("Common", false)]
		[TestCase("mechanimist", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		public void IsKnownStyle(string style, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.IsKnownStyle(style));
		}

		[Test]
		public void StylesAreTheFiveDeclaredInModdingDoc()
		{
			Assert.AreEqual(5, KingdomRules.Styles.Length);
			Assert.AreEqual("common", KingdomRules.Styles[0], "common is the fallback and every base design allows it");
		}

		[TestCase("TerrainSaltmarsh", "Saltmarsh", 10, "verdant")]
		[TestCase("TerrainSaltmarsh2", "Saltmarsh", 10, "verdant")]
		[TestCase("TerrainWatervine", "Saltmarsh", 10, "verdant")]
		[TestCase("TerrainFlowerfields", "Flowerfields", 10, "verdant")]
		[TestCase("TerrainBananaGrove", "BananaGrove", 10, "verdant")]
		[TestCase("TerrainDeepJungle", "DeepJungle", 10, "verdant")]
		[TestCase("TerrainKyakukya", "Jungle", 10, "verdant")]
		[TestCase("TerrainFungal", "Fungal", 10, "fungal")]
		[TestCase("TerrainFungalOuterGw", "Fungal", 10, "fungal")]
		[TestCase("TerrainFungalCenter", "Fungal", 10, "fungal")]
		[TestCase("TerrainBrightsheol", "Brightsheol", 10, "moonstair")]
		[TestCase("TerrainMoonStair", "MoonStair", 10, "moonstair")]
		[TestCase("TerrainRuins", "Ruins", 10, "eater")]
		[TestCase("TerrainBaroqueRuins", "BaroqueRuins", 10, "eater")]
		[TestCase("TerrainGritGate", "Ruins", 10, "eater")]
		[TestCase("TerrainBethesdaSusa", "Mountains", 10, "eater")]
		[TestCase("TerrainTheSpindle", "TheSpindle", 10, "eater")]
		[TestCase("TerrainSaltdunes", "Saltdunes", 10, "common")]
		[TestCase("TerrainRedRock", "DesertCanyon", 10, "common")]
		[TestCase("TerrainHills", "Hills", 10, "common")]
		[TestCase("TerrainMountains", "Mountains", 10, "common")]
		[TestCase("TerrainMountainsSpindleShadow", "Mountains", 10, "common")]
		[TestCase("TerrainTremblezone", "Tremblezone", 10, "common")]
		[TestCase(null, "Saltmarsh", 10, "verdant")]
		[TestCase("", "Fungal", 10, "fungal")]
		[TestCase(null, null, 10, "common")]
		[TestCase("", "", 10, "common")]
		[TestCase("TerrainOfSomeFutureUpdate", "Nowhere", 10, "common")]
		public void StyleForSite(string blueprint, string region, int zLevel, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.StyleForSite(blueprint, region, zLevel));
		}

		[Test]
		public void BlueprintOutranksRegion()
		{
			Assert.AreEqual("eater", KingdomRules.StyleForSite("TerrainJoppaRuins", "Saltmarsh", 10), "the ruins are what you are building in, whatever region they sit in");
			Assert.AreEqual("verdant", KingdomRules.StyleForSite("TerrainOfSomeFutureUpdate", "Saltmarsh", 10), "the region is the fallback reading, not the ignored one");
		}

		[TestCase("TerrainSaltmarsh", "Saltmarsh", 11, "common")]
		[TestCase("TerrainWatervine", "Saltmarsh", 40, "common")]
		[TestCase("TerrainMoonStair", "MoonStair", 11, "common")]
		[TestCase("TerrainFungalCenter", "Fungal", 25, "fungal")]
		[TestCase("TerrainBaroqueRuins", "BaroqueRuins", 45, "eater")]
		[TestCase("TerrainSaltmarsh", "Saltmarsh", 10, "verdant")]
		[TestCase("TerrainMoonStair", "MoonStair", 9, "moonstair")]
		public void StyleForSiteBelowTheSurface(string blueprint, string region, int zLevel, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.StyleForSite(blueprint, region, zLevel));
		}

		[Test]
		public void StyleForSiteAlwaysAnswersAKnownStyle()
		{
			string[] blueprints = new string[7] { null, "", "   ", "Terrain", "TerrainOfSomeFutureUpdate", "TerrainFungalOuterGw", "{{r|not a blueprint at all}}" };
			string[] regions = new string[5] { null, "", "Saltmarsh", "NoSuchRegion", "Ruins" };
			int[] levels = new int[5] { -5, 0, 9, 10, 48 };
			for (int i = 0; i < blueprints.Length; i++)
			{
				for (int j = 0; j < regions.Length; j++)
				{
					for (int k = 0; k < levels.Length; k++)
					{
						string style = KingdomRules.StyleForSite(blueprints[i], regions[j], levels[k]);
						Assert.IsTrue(KingdomRules.IsKnownStyle(style), "the fallback must be total; got " + (style ?? "null"));
					}
				}
			}
		}

		[Test]
		public void EveryProvokableFactionCanFieldARaid()
		{
			Assert.IsTrue(KingdomRules.ProvokableFactions.Length > 1, "provoked factions is a plural the game has to be able to deliver");
			for (int i = 0; i < KingdomRules.ProvokableFactions.Length; i++)
			{
				string faction = KingdomRules.ProvokableFactions[i];
				string[] table = KingdomRules.RaiderTableFor(faction);
				Assert.IsNotNull(table, faction + " is provokable but fields nobody");
				Assert.IsTrue(table.Length > 0, faction + " has an empty raider table");
				for (int j = 0; j < table.Length; j++)
				{
					Assert.IsFalse(string.IsNullOrEmpty(table[j]), faction + " table entry " + j + " is blank, which spawns nothing and says nothing");
				}
			}
		}

		[Test]
		public void NoRaiderTableWithoutAProvokableFaction()
		{
			string[] probes = new string[14] { "Snapjaws", "Baboons", "Goatfolk", "Cannibals", "Issachari", "Joppa", "Barathrumites", "Mechanimists", "Templar", "Cragmensch", "Svardym", "Apes", "Issachari tribe", "snapjaws" };
			for (int i = 0; i < probes.Length; i++)
			{
				bool listed = Contains(KingdomRules.ProvokableFactions, probes[i]);
				bool answered = KingdomRules.RaiderTableFor(probes[i]) != null;
				Assert.AreEqual(listed, answered, probes[i] + ": the provokable list and the raider tables must agree in both directions");
			}
			Assert.IsNull(KingdomRules.RaiderTableFor(null));
			Assert.IsNull(KingdomRules.RaiderTableFor(""));
		}

		[Test]
		public void RaiderTablesAreScavengerWeightedAndDistinct()
		{
			for (int i = 0; i < KingdomRules.ProvokableFactions.Length; i++)
			{
				string[] table = KingdomRules.RaiderTableFor(KingdomRules.ProvokableFactions[i]);
				Assert.AreEqual(table[0], table[1], KingdomRules.ProvokableFactions[i] + " should weight its scavenger tier by doubling it");
				Assert.AreNotEqual(table[0], table[table.Length - 1], KingdomRules.ProvokableFactions[i] + " fields one creature repeated, which is not a war party");
				for (int j = 0; j < i; j++)
				{
					string[] other = KingdomRules.RaiderTableFor(KingdomRules.ProvokableFactions[j]);
					Assert.AreNotEqual(other[0], table[0], "two factions share a raider table; one of them is wired to the wrong creatures");
				}
			}
		}

		// --- Raiding party size: what the walls turn back -------------------------------

		[Test]
		public void RaidingPartySize_RepelledSendsNobodyThrough()
		{
			Assert.AreEqual(0, KingdomRules.RaidingPartySize(6, 40, KingdomRules.RaidOutcome.Repelled));
		}

		[Test]
		public void RaidingPartySize_NoDefenceLetsTheWholeBandIn()
		{
			Assert.AreEqual(6, KingdomRules.RaidingPartySize(6, 0, KingdomRules.RaidOutcome.Overrun));
			Assert.AreEqual(6, KingdomRules.RaidingPartySize(6, -3, KingdomRules.RaidOutcome.Overrun));
		}

		[TestCase(10, 1, KingdomRules.RaidOutcome.Plundered, 9)]
		[TestCase(10, 5, KingdomRules.RaidOutcome.Plundered, 7)]
		[TestCase(10, 10, KingdomRules.RaidOutcome.Plundered, 4)]
		public void RaidingPartySize_DefenceTurnsBackProportionally(int size, int defence, KingdomRules.RaidOutcome outcome, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.RaidingPartySize(size, defence, outcome));
		}

		[Test]
		public void RaidingPartySize_TurnBackIsCappedSoWallsAreNeverTotal()
		{
			// 60% is the ceiling; a defence of 10 already reaches it, and 100 cannot beat it.
			Assert.AreEqual(
				KingdomRules.RaidingPartySize(10, 10, KingdomRules.RaidOutcome.Plundered),
				KingdomRules.RaidingPartySize(10, 100, KingdomRules.RaidOutcome.Plundered));
		}

		[Test]
		public void RaidingPartySize_SomeoneAlwaysGetsThroughUnlessRepelled()
		{
			// A small band against a huge wall still puts one raider on the ground: being
			// well-walled is not the same as being spared.
			Assert.AreEqual(1, KingdomRules.RaidingPartySize(1, 99, KingdomRules.RaidOutcome.Plundered));
			Assert.AreEqual(1, KingdomRules.RaidingPartySize(2, 99, KingdomRules.RaidOutcome.Plundered));
		}

		[Test]
		public void RaidingPartySize_NoRaidersMeansNoParty()
		{
			Assert.AreEqual(0, KingdomRules.RaidingPartySize(0, 5, KingdomRules.RaidOutcome.Plundered));
			Assert.AreEqual(0, KingdomRules.RaidingPartySize(-4, 5, KingdomRules.RaidOutcome.Plundered));
		}

		[Test]
		public void RaidingPartySize_MoreDefenceNeverLetsMoreThrough()
		{
			int previous = int.MaxValue;
			for (int defence = 0; defence <= 20; defence++)
			{
				int through = KingdomRules.RaidingPartySize(12, defence, KingdomRules.RaidOutcome.Plundered);
				Assert.LessOrEqual(through, previous, "defence " + defence + " let more raiders through than " + (defence - 1));
				previous = through;
			}
		}

		private static bool Contains(string[] Names, string Name)
		{
			for (int i = 0; i < Names.Length; i++)
			{
				if (Names[i] == Name)
				{
					return true;
				}
			}
			return false;
		}
	}
}
#endif
