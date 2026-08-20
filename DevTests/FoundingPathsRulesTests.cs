#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Pure-rule coverage for the three founding-paths additions: ruin restoration, the village
	/// charter's consent gate, and vertical territorial adjacency. Each table is written to fail
	/// if a condition were inverted or a constant nudged &mdash; not merely to hit a line.
	/// </summary>
	public class FoundingPathsRulesTests
	{
		[TestCase("TerrainRuins", true)]
		[TestCase("TerrainBaroqueRuins", true)]
		[TestCase("TerrainGritGate", false)]
		[TestCase("TerrainRustWell", false)]
		[TestCase("TerrainBethesdaSusa", false)]
		[TestCase("TerrainTheSpindle", false)]
		[TestCase("TerrainJoppaRuins", false)]
		[TestCase("terrainruins", false)]
		[TestCase("TerrainRuinsButNotReally", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		public void IsRuinSite(string terrainBlueprint, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.IsRuinSite(terrainBlueprint));
		}

		[TestCase(-1, "")]
		[TestCase(0, "")]
		[TestCase(1, ", and one of its standing works is the settlement's now")]
		[TestCase(2, ", and 2 of its standing works are the settlement's now")]
		[TestCase(5, ", and 5 of its standing works are the settlement's now")]
		public void RuinRestorationClause(int structuresRestored, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.RuinRestorationClause(structuresRestored));
		}

		[TestCase(null, "Kavvat", false)]
		[TestCase("", "Kavvat", false)]
		[TestCase("Kavvat", "Kavvat", false)]
		[TestCase("villagers of Bey Lah", "Kavvat", true)]
		[TestCase("villagers of Bey Lah", null, true)]
		[TestCase("villagers of Bey Lah", "", true)]
		public void GroundIsForeignFaction(string zoneFaction, string kingdomFactionName, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.GroundIsForeignFaction(zoneFaction, kingdomFactionName));
		}

		[TestCase(null, "Kavvat", false, KingdomRules.GroundClaimVerdict.Unclaimed)]
		[TestCase("Kavvat", "Kavvat", false, KingdomRules.GroundClaimVerdict.Unclaimed)]
		[TestCase("villagers of Bey Lah", "Kavvat", true, KingdomRules.GroundClaimVerdict.ForeignVillage)]
		[TestCase("Girsh", "Kavvat", false, KingdomRules.GroundClaimVerdict.ForeignOther)]
		[TestCase("Girsh", "Kavvat", true, KingdomRules.GroundClaimVerdict.ForeignVillage)]
		public void JudgeGroundFaction(string zoneFaction, string kingdomFactionName, bool zoneFactionIsVillage, KingdomRules.GroundClaimVerdict expected)
		{
			Assert.AreEqual(expected, KingdomRules.JudgeGroundFaction(zoneFaction, kingdomFactionName, zoneFactionIsVillage));
		}

		[TestCase(false, false, 0, KingdomRules.VillageCharterVerdict.RealmNotFounded)]
		[TestCase(false, true, 1000, KingdomRules.VillageCharterVerdict.RealmNotFounded)]
		[TestCase(true, true, 1000, KingdomRules.VillageCharterVerdict.AlreadyChartered)]
		[TestCase(true, false, 0, KingdomRules.VillageCharterVerdict.OpinionTooLow)]
		[TestCase(true, false, 249, KingdomRules.VillageCharterVerdict.OpinionTooLow)]
		[TestCase(true, false, 250, KingdomRules.VillageCharterVerdict.Allowed)]
		[TestCase(true, false, 600, KingdomRules.VillageCharterVerdict.Allowed)]
		[TestCase(true, false, -600, KingdomRules.VillageCharterVerdict.OpinionTooLow)]
		public void JudgeVillageCharter(bool founded, bool alreadyChartered, int playerReputation, KingdomRules.VillageCharterVerdict expected)
		{
			Assert.AreEqual(expected, KingdomRules.JudgeVillageCharter(founded, alreadyChartered, playerReputation));
		}

		[Test]
		public void EveryVillageCharterRefusalSaysSomethingAndAllowedSaysNothing()
		{
			foreach (KingdomRules.VillageCharterVerdict verdict in Enum.GetValues(typeof(KingdomRules.VillageCharterVerdict)))
			{
				string refusal = KingdomRules.VillageCharterRefusal(verdict, "villagers of Bey Lah");
				if (verdict == KingdomRules.VillageCharterVerdict.Allowed)
				{
					Assert.AreEqual("", refusal, "an allowed charter refuses nothing");
				}
				else
				{
					Assert.IsTrue(refusal.Length > 0, verdict + " must tell the founder why");
				}
			}
			Assert.IsTrue(KingdomRules.VillageCharterRefusal(KingdomRules.VillageCharterVerdict.OpinionTooLow, "villagers of Bey Lah").Contains("Bey Lah"));
			Assert.IsTrue(KingdomRules.VillageCharterRefusal(KingdomRules.VillageCharterVerdict.OpinionTooLow, null).Contains("this village"));
		}

		// Same column, one stratum apart: a cellar below or a tower above — but only once opted
		// into, which is exactly what the default-false parameter exists to gate.
		[TestCase("JoppaWorld", 10, 10, 10, "JoppaWorld", 10, 10, 11, false, false)]
		[TestCase("JoppaWorld", 10, 10, 10, "JoppaWorld", 10, 10, 11, true, true)]
		[TestCase("JoppaWorld", 10, 10, 10, "JoppaWorld", 10, 10, 9, true, true)]
		// Two strata apart is never adjacent, vertical or not.
		[TestCase("JoppaWorld", 10, 10, 10, "JoppaWorld", 10, 10, 12, true, false)]
		// A diagonal neighbour one stratum up is not a vertical neighbour: the column must match.
		[TestCase("JoppaWorld", 10, 10, 10, "JoppaWorld", 11, 11, 11, true, false)]
		[TestCase("JoppaWorld", 10, 10, 10, "JoppaWorld", 11, 10, 11, true, false)]
		// Different worlds never touch, at any stratum.
		[TestCase("JoppaWorld", 10, 10, 10, "OtherWorld", 10, 10, 11, true, false)]
		// The horizontal case is unaffected by the new parameter either way.
		[TestCase("JoppaWorld", 10, 10, 10, "JoppaWorld", 11, 10, 10, false, true)]
		[TestCase("JoppaWorld", 10, 10, 10, "JoppaWorld", 11, 10, 10, true, true)]
		[TestCase("JoppaWorld", 10, 10, 10, "JoppaWorld", 10, 10, 10, true, false)]
		public void CoordsAdjacentVertical(string worldA, int gxA, int gyA, int zA, string worldB, int gxB, int gyB, int zB, bool includeVertical, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.CoordsAdjacent(worldA, gxA, gyA, zA, worldB, gxB, gyB, zB, includeVertical));
		}

		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.10", false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.11", false)]
		public void ZonesAdjacentDefaultsToHorizontalOnly(string a, string b, bool expected)
		{
			// The two-argument overload must still answer exactly as it always did — vertical
			// adjacency is opt-in, never a silent behaviour change for existing callers.
			Assert.AreEqual(expected, KingdomRules.ZonesAdjacent(a, b));
		}

		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.11", true, true)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.11", false, false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.12", true, false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.2.11", true, false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.10", true, false)]
		// Malformed IDs on either side must refuse the claim, never guess it — six parts or bust.
		[TestCase("JoppaWorld.11.22.1.1", "JoppaWorld.11.22.1.1.11", true, false)]
		[TestCase("JoppaWorld.11.22.1.1.10.1", "JoppaWorld.11.22.1.1.11", true, false)]
		[TestCase("JoppaWorld.a.22.1.1.10", "JoppaWorld.11.22.1.1.11", true, false)]
		[TestCase("garbage", "JoppaWorld.11.22.1.1.11", true, false)]
		[TestCase("", "JoppaWorld.11.22.1.1.11", true, false)]
		[TestCase(null, "JoppaWorld.11.22.1.1.11", true, false)]
		[TestCase("JoppaWorld.11.22.1.1.10", null, true, false)]
		public void ZonesAdjacentWithVertical(string a, string b, bool includeVertical, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.ZonesAdjacent(a, b, includeVertical));
		}
	}
}
#endif
