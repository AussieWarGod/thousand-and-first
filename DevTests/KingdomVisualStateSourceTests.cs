#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomVisualStateSourceTests
	{
		private static string Source(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		[Test]
		public void VisualPartDerivesOnlyFromGameplayStateAndUsesVanillaRenderChannel()
		{
			string source = Source("Growth/KingdomVisualState.cs");
			StringAssert.Contains("E.RenderEffectIndicator(cue.Glyph, cue.Tile", source);
			StringAssert.Contains("KingdomMaterials.StrikeEffortProperty", source);
			StringAssert.Contains("wear.RepairEffortLeft > 0", source);
			StringAssert.Contains("wear == null ? 0 : wear.Wear", source);
			StringAssert.Contains("GetIntProperty(\"KingdomBrownout\") == 1", source);
			StringAssert.Contains("GetIntProperty(\"KingdomEffectiveness\")", source);
			Assert.IsFalse(source.Contains("GameObject.Create"));
			Assert.IsFalse(source.Contains("AddObject("));
			Assert.IsFalse(source.Contains("Destroy("));
			Assert.IsFalse(source.Contains("Obliterate("));
		}

		[Test]
		public void ConstructionUsesOneStampedGangNotGlobalFreeHandsPerRoot()
		{
			string presence = Source("Growth/KingdomConstructionPresence.cs");
			StringAssert.Contains("KingdomConstructionPresenceRules.Plan(readings", presence);
			StringAssert.Contains("KingdomCrews.AssignRaising(selected.Root", presence);
			StringAssert.Contains("selected.Root.SetIntProperty(HandsProperty, assigned)", presence);
			StringAssert.Contains("KingdomStations.Post(free[at], workId, KingdomWorkKind.Construction)",
				presence);
			Assert.IsFalse(presence.Contains("GameObject.Create"));
			Assert.IsFalse(presence.Contains("AddObject("));

			string scaffold = Source("Growth/KingdomScaffold.cs");
			string plot = Source("Growth/KingdomPlot2.cs");
			StringAssert.Contains("KingdomConstructionPresence.EffectivenessOf(ParentObject, System",
				scaffold);
			StringAssert.Contains("KingdomConstructionPresence.EffectivenessOf(parent, System", plot);

			string crews = Source("Growth/KingdomCrews.cs");
			int raising = crews.IndexOf("internal static KingdomCrewRules.CrewOutcome AssignRaising",
				StringComparison.Ordinal);
			int extension = crews.IndexOf("ExtensionAffinities(demand, Settlers, pool.Length)",
				raising, StringComparison.Ordinal);
			int allocation = crews.IndexOf("AssignCrew(pool, demand, extensionAffinities)[0]",
				extension, StringComparison.Ordinal);
			Assert.Greater(raising, 0);
			Assert.Greater(extension, raising);
			Assert.Greater(allocation, extension);
		}

		[Test]
		public void AssignmentWorksEvenWhenThereAreNoFinishedCrewedWorks()
		{
			string growth = Source("Growth/KingdomGrowth.cs");
			int method = growth.IndexOf("public static void AssignWork", StringComparison.Ordinal);
			int end = growth.IndexOf("public static bool Emigrate", method, StringComparison.Ordinal);
			string assign = growth.Substring(method, end - method);
			Assert.IsFalse(assign.Contains("if (Survey.Works.Count == 0)"));
			StringAssert.Contains("KingdomConstructionPresence.Assign(System, Survey)", assign);
		}

		[Test]
		public void CompletionReleasesAnchorsWithoutTeleportingOrCloningBodies()
		{
			string construction = Source("Growth/KingdomConstruction.cs");
			StringAssert.Contains("KingdomConstructionPresence.ReleaseFinished(Z, Survey)", construction);
			StringAssert.Contains("KingdomVisualState.Refresh(System, Z, Survey)", construction);

			string stations = Source("Simulation/City/KingdomStations.cs");
			int start = stations.IndexOf("internal static bool Release", StringComparison.Ordinal);
			int end = stations.IndexOf("private static bool TryReading", start,
				StringComparison.Ordinal);
			string release = stations.Substring(start, end - start);
			StringAssert.Contains("Settler.Brain.Stay(target)", release);
			StringAssert.Contains("new MoveTo(target, careful: true)", release);
			Assert.IsFalse(release.Contains("AddObject("));
			Assert.IsFalse(release.Contains("Teleport"));
		}

		[Test]
		public void ShippedPositionalConstructionPartsGainNoFields()
		{
			string presence = Source("Growth/KingdomConstructionPresence.cs");
			StringAssert.Contains("Named properties only", presence);
			StringAssert.Contains("r_TAF_ConstructionCrewSchema", presence);
			string plot = Source("Growth/KingdomPlot2.cs");
			StringAssert.Contains("public int DoorY;", plot);
			string scaffold = Source("Growth/KingdomScaffold.cs");
			Assert.IsFalse(scaffold.Contains("public int ConstructionCrew"));
		}
	}
}
#endif
