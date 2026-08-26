#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomInheritanceSiteRulesTests
	{
		private static KingdomInheritanceSiteCandidate Safe(string ZoneId, string Terrain,
			string TerrainTag, int TerrainRank, int Tier)
		{
			return new KingdomInheritanceSiteCandidate
			{
				ZoneId = ZoneId,
				TerrainBlueprint = Terrain,
				TerrainTag = TerrainTag,
				TerrainRank = TerrainRank,
				Tier = Tier,
				Mutable = true
			};
		}

		[Test]
		public void ExactOldGroundWinsOnlyWhileStillSafe()
		{
			KingdomInheritanceSiteCandidate old = Safe("JoppaWorld.10.5.1.1.10",
				"TerrainHills", "Hills", 3, 4);
			KingdomInheritanceSiteCandidate better = Safe("JoppaWorld.10.5.2.1.10",
				"TerrainSaltdunes", "Saltdunes", 0, 1);
			KingdomInheritanceSiteCandidate selected;
			KingdomInheritanceSiteFault fault;
			Assert.IsTrue(KingdomInheritanceSiteRules.TrySelect(
				new[] { better, old }, "legacy-a", old.ZoneId, better.TerrainBlueprint,
				out selected, out fault));
			Assert.AreSame(old, selected);

			old.Built = true;
			Assert.IsTrue(KingdomInheritanceSiteRules.TrySelect(
				new[] { old, better }, "legacy-a", old.ZoneId, old.TerrainBlueprint,
				out selected, out fault));
			Assert.AreSame(better, selected);
		}

		[Test]
		public void PreferredTerrainAndStableHashAreOrderIndependent()
		{
			KingdomInheritanceSiteCandidate a = Safe("JoppaWorld.20.10.0.0.10",
				"TerrainSaltdunes", "Saltdunes", 0, 2);
			KingdomInheritanceSiteCandidate b = Safe("JoppaWorld.20.10.1.0.10",
				"TerrainFlowerfields", "Flowerfields", 1, 2);
			KingdomInheritanceSiteCandidate c = Safe("JoppaWorld.20.10.2.0.10",
				"TerrainFlowerfields", "Flowerfields", 1, 2);
			KingdomInheritanceSiteCandidate forward;
			KingdomInheritanceSiteCandidate reverse;
			KingdomInheritanceSiteFault fault;
			Assert.IsTrue(KingdomInheritanceSiteRules.TrySelect(new[] { a, b, c },
				"legacy-stable", "", "TerrainFlowerfields", out forward, out fault));
			Assert.IsTrue(KingdomInheritanceSiteRules.TrySelect(new[] { c, b, a },
				"legacy-stable", "", "TerrainFlowerfields", out reverse, out fault));
			Assert.AreEqual(forward.ZoneId, reverse.ZoneId);
			Assert.AreNotSame(a, forward);
		}

		[Test]
		public void EveryWorldConflictFailsClosed()
		{
			List<KingdomInheritanceSiteCandidate> conflicts = new List<KingdomInheritanceSiteCandidate>();
			KingdomInheritanceSiteCandidate built = Safe("JoppaWorld.1.1.0.0.10", "TerrainHills", "Hills", 3, 2);
			built.Built = true;
			conflicts.Add(built);
			KingdomInheritanceSiteCandidate water = Safe("JoppaWorld.1.1.1.0.10", "TerrainHills", "Hills", 3, 2);
			water.Water = true;
			conflicts.Add(water);
			KingdomInheritanceSiteCandidate note = Safe("JoppaWorld.1.1.2.0.10", "TerrainHills", "Hills", 3, 2);
			note.HasMapNote = true;
			conflicts.Add(note);
			KingdomInheritanceSiteCandidate location = Safe("JoppaWorld.1.1.0.1.10", "TerrainHills", "Hills", 3, 2);
			location.HasGeneratedLocation = true;
			conflicts.Add(location);
			KingdomInheritanceSiteCandidate builder = Safe("JoppaWorld.1.1.1.1.10", "TerrainHills", "Hills", 3, 2);
			builder.HasZoneBuilder = true;
			conflicts.Add(builder);
			KingdomInheritanceSiteCandidate named = Safe("JoppaWorld.1.1.2.1.10", "TerrainHills", "Hills", 3, 2);
			named.HasExplicitName = true;
			conflicts.Add(named);
			KingdomInheritanceSiteCandidate special = Safe("JoppaWorld.1.1.0.2.10", "TerrainHills", "Hills", 3, 2);
			special.Special = true;
			conflicts.Add(special);
			KingdomInheritanceSiteCandidate property = Safe("JoppaWorld.1.1.1.2.10", "TerrainHills", "Hills", 3, 2);
			property.HasReservedZoneProperty = true;
			conflicts.Add(property);

			KingdomInheritanceSiteCandidate selected;
			KingdomInheritanceSiteFault fault;
			Assert.IsFalse(KingdomInheritanceSiteRules.TrySelect(conflicts, "legacy-a", "", "",
				out selected, out fault));
			Assert.AreEqual(KingdomInheritanceSiteFault.NoSafeSite, fault);
			Assert.IsNull(selected);
		}

		[TestCase("JoppaWorld.0.0.0.0.10", true)]
		[TestCase("JoppaWorld.79.24.2.2.10", true)]
		[TestCase("JoppaWorld.00.0.0.0.10", false)]
		[TestCase("JoppaWorld.+1.0.0.0.10", false)]
		[TestCase("JoppaWorld.80.0.0.0.10", false)]
		[TestCase("JoppaWorld.0.25.0.0.10", false)]
		[TestCase("JoppaWorld.0.0.3.0.10", false)]
		[TestCase("JoppaWorld.0.0.0.3.10", false)]
		[TestCase("JoppaWorld.0.0.0.0.9", false)]
		[TestCase("OtherWorld.0.0.0.0.10", false)]
		[TestCase("JoppaWorld.0.0.0.10", false)]
		[TestCase(null, false)]
		public void SurfaceZoneGrammarIsCanonicalAndBounded(string ZoneId, bool Expected)
		{
			Assert.AreEqual(Expected,
				KingdomInheritanceSiteRules.IsCanonicalSurfaceZoneId(ZoneId));
		}

		[Test]
		public void MissingTerrainRestoreTagIsNeverSelectable()
		{
			KingdomInheritanceSiteCandidate candidate = Safe("JoppaWorld.2.2.1.1.10",
				"TerrainSaltdunes", "", 0, 2);
			Assert.IsFalse(KingdomInheritanceSiteRules.IsSafe(candidate));
		}

		[Test]
		public void WorldExtensionLogicalSourceKeepsBuilderAndReservationOrder()
		{
			string source = KingdomInheritanceWorldExtensionLogicalSource.Read();
			string[] ordered = new string[]
			{
				"[JoppaWorldBuilderExtension]",
				"public sealed class KingdomInheritanceWorldExtension : IJoppaWorldBuilderExtension",
				"state.TrySelectionInputs(out legacyId",
				"new KingdomInheritanceWorldIndex(",
				"KingdomInheritanceWorldRuntime.TryCandidate(Builder.WorldZone",
				"KingdomInheritanceSiteRules.TrySelect(candidates",
				"Builder.mutableMap.RemoveMutableLocation",
				"state.StageSite(selected",
				"internal sealed class KingdomInheritanceWorldIndex",
				"internal sealed class ParasangFacts",
				"internal static class KingdomInheritanceWorldRuntime"
			};
			int cursor = -1;
			for (int i = 0; i < ordered.Length; i++)
			{
				int next = source.IndexOf(ordered[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, ordered[i]);
				cursor = next;
			}
			StringAssert.Contains(
				"RestoreRemoved(removedMap, removedX, removedY, removedTerrain);", source);
			StringAssert.Contains(
				"MetricsManager.LogError(\"ThousandAndFirst inheritance world extension\", ex);",
				source);
		}
	}
}
#endif
