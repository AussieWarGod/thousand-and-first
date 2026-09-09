#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;
using F = ThousandAndFirst.KingdomMaterialRules.ForageFacts;

namespace ThousandAndFirst.Tests
{
	public class KingdomForageTests
	{
		[TestCase(F.Plant, true)]
		[TestCase(F.None, false)]
		[TestCase(F.Plant | F.Tree, false)]
		[TestCase(F.Plant | F.Wall, false, TestName = "KingdomForageTests.CanvasWallIsNeverBrush")]
		[TestCase(F.Wall, false, TestName = "KingdomForageTests.FungalWallIsNeverBrush")]
		[TestCase(F.Plant | F.Creature, false)]
		[TestCase(F.Plant | F.Food, false)]
		[TestCase(F.Plant | F.Owned, false)]
		[TestCase(F.Plant | F.UntakenSeed, false)]
		[TestCase(F.Plant | F.Protected, false)]
		[TestCase(F.Plant | F.Plot, false)]
		[TestCase(F.Plant | F.Portable, false)]
		[TestCase((F)1025, false)]
		public void CandidateRequiresOnlyPositiveWildPlantEvidence(F facts, bool expected)
			=> Assert.That(KingdomMaterialRules.ForageCandidate(facts), Is.EqualTo(expected));

		[TestCase(3, 100, 0, 12)]
		[TestCase(3, 100, 12, 0)]
		[TestCase(3, 100, 11, 1)]
		[TestCase(3, 1, 0, 3)]
		[TestCase(99, 1, 0, 3)]
		[TestCase(0, 100, 0, 0)]
		[TestCase(-1, 100, 0, 0)]
		[TestCase(3, 0, 0, 0)]
		[TestCase(3, -1, 0, 0)]
		[TestCase(3, 1, -1, 0)]
		[TestCase(3, int.MaxValue, 0, 12)]
		public void YieldNeverExceedsLabourOrPhysicalCeiling(int hands, int days, int held, int expected)
			=> Assert.That(KingdomMaterialRules.ForageUnits(hands, days, held), Is.EqualTo(expected));

		[Test]
		public void ThreeStrikeDaysAreNotBankedForTheFourthPass()
		{
			long checkpoint = 1200;
			for (int pass = 1; pass <= 4; pass++)
			{
				int days = KingdomMaterialRules.ForageDays(ref checkpoint, 1200 + pass * 1200);
				int foraged = pass <= 3 ? 0 : KingdomMaterialRules.ForageUnits(3, days, 0);
				Assert.That(foraged, Is.EqualTo(pass <= 3 ? 0 : 3));
			}
			Assert.That(checkpoint, Is.EqualTo(6000));
		}

		[Test]
		public void FreshAttachmentDoesNotHarvestAndFractionalDaysCarry()
		{
			long checkpoint = 0;
			Assert.That(KingdomMaterialRules.ForageDays(ref checkpoint, 9000), Is.Zero);
			Assert.That(checkpoint, Is.EqualTo(9000));
			Assert.That(KingdomMaterialRules.ForageDays(ref checkpoint, 9600), Is.Zero);
			Assert.That(checkpoint, Is.EqualTo(9000));
			Assert.That(KingdomMaterialRules.ForageDays(ref checkpoint, 10300), Is.EqualTo(1));
			Assert.That(checkpoint, Is.EqualTo(10200));
			Assert.That(KingdomMaterialRules.ForageDays(ref checkpoint, 11400), Is.EqualTo(1));
		}

		[Test]
		public void AnnouncementIsOncePerBlockAndRearmsOnRecovery()
		{
			bool announced = false;
			Assert.That(KingdomMaterialRules.ForageAnnounce(ref announced, true), Is.True);
			Assert.That(KingdomMaterialRules.ForageAnnounce(ref announced, true), Is.False);
			Assert.That(KingdomMaterialRules.ForageAnnounce(ref announced, false), Is.False);
			Assert.That(KingdomMaterialRules.ForageAnnounce(ref announced, true), Is.True);
		}

		[Test]
		public void OrderIsChebyshevThenYThenX()
		{
			Assert.That(KingdomMaterialRules.ForageDistance(12, 12, 0, 0), Is.EqualTo(12));
			Assert.That(KingdomMaterialRules.ForageOrder(1, 1, 2, 0, 0, 0), Is.LessThan(0));
			Assert.That(KingdomMaterialRules.ForageOrder(2, 0, 0, 2, 0, 0), Is.LessThan(0));
			Assert.That(KingdomMaterialRules.ForageOrder(-2, 0, 2, 0, 0, 0), Is.LessThan(0));
			Assert.That(KingdomMaterialRules.ForageDistance(int.MinValue, 0, int.MaxValue, 0),
				Is.EqualTo(4294967295L));
		}

		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		[Test]
		public void CheckpointPrecedesStrikeAndClearanceExcludesForage()
		{
			string source = Read("Growth/KingdomMaterials.10.SettlementPassAndYards.cs");
			Assert.That(source.IndexOf("KingdomMaterialRules.ForageDays", StringComparison.Ordinal),
				Is.LessThan(source.IndexOf("if (strike != null)", StringComparison.Ordinal)));
			Assert.That(source, Does.Contain("WorkClearance(System, Z, stakeObject, stake, hands, timeTicks);\n\t\t\t\treturn;"));
			Assert.That(source.IndexOf("WorkForage(", StringComparison.Ordinal),
				Is.GreaterThan(source.IndexOf("WorkClearance(", StringComparison.Ordinal)));
		}

		[Test]
		public void RuntimeUsesNamedIndexesAndReprovesBeforeIrreversibleCalls()
		{
			string source = Read("Growth/KingdomMaterials.16.ForageWork.cs");
			foreach (string token in new[] { "Survey.ForagePlants", "Survey.ForagePlots", "Survey.CropRows",
				"Item.HasTag(\"Plant\") || Item.HasTag(\"LivePlant\")", "Item.IsWall()", "Item.HasTag(\"Tree\")",
				"Item.IsCreature", "Item.HasPart(\"Harvestable\")", "GetPart<Physics>().Owner",
				"WildSeedTakenProperty", "IsProtected(Item, out _)", "PlotPartProperty", "Item.IsTakeable()",
				"item.CurrentCell != plant.Cell", "item.ID != plant.Id", "State.Held = true;",
				"custody != KingdomDepositCustody.Settled", "if (GameObject.Validate(item))",
				"State.Held = gone || item.CurrentCell != plant.Cell;" })
				Assert.That(source, Does.Contain(token), token);
			Assert.That(source, Does.Not.Contain("GetObjects("));
			Assert.That(source, Does.Not.Contain("Survey.Objects"));
			Assert.That(source.IndexOf("TryProveEmpty(item", StringComparison.Ordinal),
				Is.LessThan(source.IndexOf("item.Obliterate(", StringComparison.Ordinal)));
			Assert.That(source.IndexOf("ObserveRemovedFromActive", StringComparison.Ordinal),
				Is.LessThan(source.IndexOf("stock.Put(", StringComparison.Ordinal)));
		}
	}
}
#endif
