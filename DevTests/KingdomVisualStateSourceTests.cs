#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomVisualStateSourceTests
	{
		private static string Source(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static string Method(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, signature);
			int open = source.IndexOf('{', start);
			ClassicAssert.Greater(open, start, signature);
			int depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				if (source[i] != '}') continue;
				depth--;
				if (depth == 0) return source.Substring(start, i - start + 1);
			}
			Assert.Fail("method has no closing brace: " + signature);
			return "";
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
			ClassicAssert.IsFalse(source.Contains("GameObject.Create"));
			ClassicAssert.IsFalse(source.Contains("AddObject("));
			ClassicAssert.IsFalse(source.Contains("Destroy("));
			ClassicAssert.IsFalse(source.Contains("Obliterate("));
		}

		[Test]
		public void ConstructionUsesOneStampedGangNotGlobalFreeHandsPerRoot()
		{
			string presence = Source("Growth/KingdomConstructionPresence.cs");
			StringAssert.Contains("KingdomConstructionPresenceRules.Plan(readings", presence);
			StringAssert.Contains("KingdomCrews.AvailableSettlers(System, Survey)", presence);
			StringAssert.Contains("KingdomCrews.WorkHandCount(System, available)", presence);
			StringAssert.DoesNotContain("System.Population - System.WaterCrew", presence);
			StringAssert.Contains("KingdomCrews.AssignRaising(selected.Root", presence);
			StringAssert.Contains("selected.Root.SetIntProperty(HandsProperty, assigned)", presence);
			StringAssert.Contains("KingdomStations.Post(free[at], workId, KingdomWorkKind.Construction)",
				presence);
			ClassicAssert.IsFalse(presence.Contains("GameObject.Create"));
			ClassicAssert.IsFalse(presence.Contains("AddObject("));

			string scaffold = KingdomScaffoldLogicalSource.Read();
			string plot = KingdomPlot2LogicalSource.Read();
			StringAssert.Contains("KingdomConstructionPresence.EffectivenessOf(ParentObject, System",
				scaffold);
			StringAssert.Contains("KingdomConstructionPresence.EffectivenessOf(Root, System", plot);

			string crews = Source("Growth/KingdomCrews.Assignments.cs");
			int raising = crews.IndexOf("internal static KingdomCrewRules.CrewOutcome AssignRaising",
				StringComparison.Ordinal);
			int extension = crews.IndexOf("ExtensionAffinities(demand, Settlers, pool.Length)",
				raising, StringComparison.Ordinal);
			int allocation = crews.IndexOf("AssignCrew(pool, demand, extensionAffinities)[0]",
				extension, StringComparison.Ordinal);
			ClassicAssert.Greater(raising, 0);
			ClassicAssert.Greater(extension, raising);
			ClassicAssert.Greater(allocation, extension);
		}

		[Test]
		public void AssignmentWorksEvenWhenThereAreNoFinishedCrewedWorks()
		{
			string growth = KingdomGrowthLogicalSource.Read();
			int method = growth.IndexOf("public static void AssignWork", StringComparison.Ordinal);
			int end = growth.IndexOf("public static bool Emigrate", method, StringComparison.Ordinal);
			string assign = growth.Substring(method, end - method);
			ClassicAssert.IsFalse(assign.Contains("if (Survey.Works.Count == 0)"));
			StringAssert.Contains("KingdomConstructionPresence.Assign(System, Survey)", assign);
		}

		[Test]
		public void CompletionReleasesAnchorsWithoutTeleportingOrCloningBodies()
		{
			string construction = KingdomConstructionLogicalSource.Read();
			StringAssert.Contains("KingdomConstructionPresence.ReleaseFinished(Z, Survey)", construction);
			StringAssert.Contains("KingdomVisualState.Refresh(System, Z, Survey)", construction);

			string stations = Source("Simulation/City/KingdomStations.cs");
			string release = Method(stations, "internal static bool Release");
			StringAssert.Contains("Settler.Brain.Stay(target)", release);
			StringAssert.Contains("new MoveTo(target, careful: true)", release);
			ClassicAssert.IsFalse(release.Contains("AddObject("));
			ClassicAssert.IsFalse(release.Contains("Teleport"));
		}

		[Test]
		public void ShippedPositionalConstructionPartsGainNoFields()
		{
			string presence = Source("Growth/KingdomConstructionPresence.cs");
			StringAssert.Contains("Named properties only", presence);
			StringAssert.Contains("r_TAF_ConstructionCrewSchema", presence);
			string plot = KingdomPlot2LogicalSource.Read();
			StringAssert.Contains("public int DoorY;", plot);
			string scaffold = KingdomScaffoldLogicalSource.Read();
			ClassicAssert.IsFalse(scaffold.Contains("public int ConstructionCrew"));
		}
	}
}
#endif
