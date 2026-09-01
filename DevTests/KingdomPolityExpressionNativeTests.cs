using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityExpressionNativeTests
	{
		[Test]
		public void EveryResolvedEngineKeyExistsInInstalledQud()
		{
			string root = LocateBase();
			string skills = File.ReadAllText(Path.Combine(root, "Skills.xml"));
			string mutations = File.ReadAllText(Path.Combine(root, "Mutations.xml"));
			foreach (string key in new[] { "WatervineFarmer", "Snapjaw Warrior", "Goatfolk",
				"Dromad", "HindrenVillager", "Scrapbot", "Club", "Leather Armor",
				"Wooden Buckler", "Long Sword", "Long Sword2", "Steel Long Sword",
				"Long Sword3", "Chain Mail", "Carbide Plate Armor", "Waterskin" })
				Assert.IsTrue(ContainsInTree(Path.Combine(root, "ObjectBlueprints"),
					"Name=\"" + key + "\""), key);
			foreach (string key in new[] { "LongBlades", "Tactics", "Customs", "Persuasion",
				"Tinkering", "Survival", "Tactics_Hurdle", "CookingAndGathering",
				"CookingAndGathering_MealPreparation" })
				StringAssert.Contains("Class=\"" + key + "\"", skills, key);
			foreach (string key in new[] { "HeightenedHearing", "DarkVision",
				"PhotosyntheticSkin" })
				StringAssert.Contains("Class=\"" + key + "\"", mutations, key);
		}

		private static bool ContainsInTree(string DirectoryPath, string Needle)
		{
			string[] files = Directory.GetFiles(DirectoryPath, "*.xml",
				SearchOption.AllDirectories); Array.Sort(files, StringComparer.Ordinal);
			for (int i = 0; i < files.Length; i++)
				if (File.ReadAllText(files[i]).Contains(Needle)) return true;
			return false;
		}

		private static string LocateBase()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (!string.IsNullOrEmpty(supplied) && File.Exists(Path.Combine(supplied, "Skills.xml")))
				return supplied;
			string[] candidates = { @"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base",
				"/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base" };
			for (int i = 0; i < candidates.Length; i++)
				if (File.Exists(Path.Combine(candidates[i], "Skills.xml"))) return candidates[i];
			Assert.Ignore("Polity expression native test requires TAF_QUD_BASE or installed Qud.");
			return null;
		}
	}
}
