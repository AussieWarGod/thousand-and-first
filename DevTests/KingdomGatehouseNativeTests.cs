#if TAF_TESTS
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Installed-base proof that the authored root inherits Qud's actual vanilla Door.</summary>
	[TestFixture]
	public class KingdomGatehouseNativeTests
	{
		[Test]
		public void GateRootRetainsVanillaDoorPartAndOwnsOnlyTopology()
		{
			string baseRoot = LocateBase();
			// Qud ships a few XML-1.0-forbidden control references later in this file. Read
			// only the literal native Door block instead of weakening XML parsing globally.
			string furniture = File.ReadAllText(Path.Combine(baseRoot,
				"ObjectBlueprints", "Furniture.xml"));
			int doorAt = furniture.IndexOf("<object Name=\"Door\"", StringComparison.Ordinal);
			Assert.GreaterOrEqual(doorAt, 0);
			int doorEnd = furniture.IndexOf("</object>", doorAt, StringComparison.Ordinal);
			Assert.Greater(doorEnd, doorAt);
			string door = furniture.Substring(doorAt, doorEnd - doorAt);
			StringAssert.Contains("Inherits=\"MountedFurniture\"", door);
			StringAssert.Contains("<part Name=\"Door\"", door);
			StringAssert.Contains("<tag Name=\"Door\"", door);

			XDocument authored = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement gate = authored.Descendants("object")
				.Single(e => (string)e.Attribute("Name") == "r_KingdomGatehouse");
			Assert.AreEqual("Door", (string)gate.Attribute("Inherits"));
			Assert.IsTrue(gate.Elements("part")
				.Any(e => (string)e.Attribute("Name") == "r_KingdomGatehouse"));
			Assert.IsFalse(gate.Elements("removepart")
				.Any(e => (string)e.Attribute("Name") == "Door"));
			XElement physics = gate.Elements("part")
				.Single(e => (string)e.Attribute("Name") == "Physics");
			Assert.IsNull(physics.Attribute("Solid"),
				"vanilla Door, not a frozen solid furniture root, owns open/closed passability");
		}

		private static string LocateBase()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (supplied != null)
			{
				if (!string.IsNullOrWhiteSpace(supplied) && File.Exists(Path.Combine(supplied,
					"ObjectBlueprints", "Furniture.xml"))) return supplied;
				throw new InvalidOperationException(
					"TAF_QUD_BASE is set but does not contain ObjectBlueprints/Furniture.xml: "
					+ supplied);
			}
			string[] candidates = new string[]
			{
				@"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base",
				"/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
			};
			for (int i = 0; i < candidates.Length; i++)
			{
				if (!string.IsNullOrEmpty(candidates[i])
					&& File.Exists(Path.Combine(candidates[i], "ObjectBlueprints", "Furniture.xml")))
					return candidates[i];
			}
			Assert.Ignore(
				"Gatehouse native test requires TAF_QUD_BASE or the configured Caves of Qud base.");
			return null;
		}
	}
}
#endif
