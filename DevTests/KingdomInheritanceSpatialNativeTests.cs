#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Installed-base proof for the vanilla floor used by sealed street graphs.</summary>
	[TestFixture]
	public class KingdomInheritanceSpatialNativeTests
	{
		[Test]
		public void ReconstructedStreetUsesVanillaPassableDirtPath()
		{
			string terrain = File.ReadAllText(Path.Combine(LocateBase(),
				"ObjectBlueprints", "ZoneTerrain.xml"));
			StringAssert.Contains("<object Name=\"DirtPath\" Inherits=\"DirtFloor\">", terrain);
			StringAssert.Contains("<object Name=\"DirtFloor\" Inherits=\"Floor\">", terrain);
			int floorAt = terrain.IndexOf("<object Name=\"Floor\"", StringComparison.Ordinal);
			Assert.GreaterOrEqual(floorAt, 0);
			int floorEnd = terrain.IndexOf("</object>", floorAt, StringComparison.Ordinal);
			Assert.Greater(floorEnd, floorAt);
			string floor = terrain.Substring(floorAt, floorEnd - floorAt);
			StringAssert.Contains("<part Name=\"Physics\" Solid=\"false\"", floor);
			StringAssert.Contains("Takeable=\"false\"", floor);
		}

		private static string LocateBase()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (supplied != null)
			{
				if (!string.IsNullOrWhiteSpace(supplied) && File.Exists(Path.Combine(supplied,
					"ObjectBlueprints", "ZoneTerrain.xml"))) return supplied;
				throw new InvalidOperationException(
					"TAF_QUD_BASE is set but does not contain ObjectBlueprints/ZoneTerrain.xml: "
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
					&& File.Exists(Path.Combine(candidates[i], "ObjectBlueprints", "ZoneTerrain.xml")))
					return candidates[i];
			}
			Assert.Ignore(
				"Inheritance native test requires TAF_QUD_BASE or the configured Caves of Qud base.");
			return null;
		}
	}
}
#endif
