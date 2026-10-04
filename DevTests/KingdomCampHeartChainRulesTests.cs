#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Executed contracts for the paid heart chain's engine-free rules (#264 review): every target
	/// rung names exactly the design production hands its handover, so the post-payment resident
	/// probe arms for the rung it watches, and the capital seed's crown gate cannot pass on a city
	/// book that has not read the crown hall.
	/// </summary>
	public class KingdomCampHeartChainRulesTests
	{
		private const string HallBlueprint = "r_KingdomCrownHall";
		private const string HallZone = "JoppaWorld.8.22.1.0.10";

		[TestCase(3, "heartwaterstone", "heartmoot")]
		[TestCase(4, "heartmoot", "heartcourt")]
		[TestCase(5, "heartcourt", "arcology")]
		public void EachPaidTargetNamesItsExactPredecessorAndSuccessor(int target, string from, string to)
		{
			Assert.That(KingdomCampHeartChainRules.PredecessorKey(target), Is.EqualTo(from));
			Assert.That(KingdomCampHeartChainRules.SuccessorKey(target), Is.EqualTo(to));
		}

		[TestCase(-1)]
		[TestCase(0)]
		[TestCase(2)]
		[TestCase(6)]
		public void NoTargetOffThePaidChainNamesADesign(int target)
		{
			Assert.That(KingdomCampHeartChainRules.PredecessorKey(target), Is.Null);
			Assert.That(KingdomCampHeartChainRules.SuccessorKey(target), Is.Null);
		}

		[Test]
		public void EachSuccessorIsTheNextRungsPredecessor()
		{
			for (int target = 3; target < 5; target++)
				Assert.That(KingdomCampHeartChainRules.SuccessorKey(target),
					Is.EqualTo(KingdomCampHeartChainRules.PredecessorKey(target + 1)), "target " + target);
		}

		private static bool Holds(int[] ids, string[] zones, string[] designs, int hall = 42,
			string zone = HallZone, string key = "crownhall", string blueprint = HallBlueprint)
		{
			return KingdomCampHeartChainRules.CrownBookHolds(ids, zones, designs, hall, zone, key, blueprint);
		}

		[Test]
		public void TheBookHoldsOneExactRowOfTheHallOnItsOwnZone()
		{
			var ids = new[] { 7, 42, 9 };
			var zones = new[] { "JoppaWorld.8.22.1.1.10", HallZone, HallZone };
			Assert.That(Holds(ids, zones, new[] { "r_KingdomTentRow", HallBlueprint, "r_KingdomTentRow" }), Is.True);
			// The crown reads the book's column by blueprint or by catalogue key, whatever the case.
			Assert.That(Holds(ids, zones, new[] { "x", "R_KINGDOMCROWNHALL", "y" }), Is.True);
			Assert.That(Holds(ids, zones, new[] { "x", "CrownHall", "y" }), Is.True);
		}

		[TestCase("other-zone")]
		[TestCase("other-hall")]
		[TestCase("other-design")]
		[TestCase("duplicate-row")]
		[TestCase("unread-book")]
		[TestCase("short-zone-column")]
		[TestCase("short-design-column")]
		public void TheBookNeverHoldsAHallItHasNotReadExactly(string fault)
		{
			var ids = new List<int> { 7, 42 };
			var zones = new List<string> { "JoppaWorld.8.22.1.1.10", HallZone };
			var designs = new List<string> { "r_KingdomTentRow", HallBlueprint };
			switch (fault)
			{
				case "other-zone": zones[1] = "JoppaWorld.8.22.1.2.10"; break;
				case "other-hall": ids[1] = 43; break;
				case "other-design": designs[1] = "r_KingdomTentRow"; break;
				case "duplicate-row": ids.Add(42); zones.Add(HallZone); designs.Add(HallBlueprint); break;
				case "unread-book": ids.Clear(); zones.Clear(); designs.Clear(); break;
				case "short-zone-column": zones.RemoveAt(1); break;
				case "short-design-column": designs.RemoveAt(1); break;
			}
			Assert.That(KingdomCampHeartChainRules.CrownBookHolds(ids, zones, designs, 42, HallZone,
				"crownhall", HallBlueprint), Is.False, fault);
		}

		[Test]
		public void AMissingColumnZoneOrKeyNeverHolds()
		{
			var ids = new[] { 42 };
			var zones = new[] { HallZone };
			var designs = new[] { HallBlueprint };
			Assert.That(Holds(null, zones, designs), Is.False);
			Assert.That(Holds(ids, null, designs), Is.False);
			Assert.That(Holds(ids, zones, null), Is.False);
			Assert.That(Holds(ids, zones, designs, zone: ""), Is.False);
			Assert.That(Holds(ids, zones, designs, key: null), Is.False);
			// The blueprint is optional: the catalogue key alone still reads a key-shaped row.
			Assert.That(Holds(ids, zones, new[] { "crownhall" }, blueprint: null), Is.True);
			Assert.That(Holds(ids, zones, designs, blueprint: null), Is.False);
		}
	}
}
#endif
