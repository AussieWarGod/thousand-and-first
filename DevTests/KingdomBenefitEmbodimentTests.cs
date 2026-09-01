using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomBenefitEmbodimentTests
	{
		[Test]
		public void CatalogueCapNeverCreatesPhysicalSupply()
		{
			List<KindAmount> caps = new List<KindAmount> { new KindAmount("roof", 4) };
			Assert.That(KingdomBenefitEmbodimentRules.Clamp(caps,
				new List<KindAmount>()), Is.Empty);
			List<KindAmount> result = KingdomBenefitEmbodimentRules.Clamp(caps,
				new List<KindAmount> { new KindAmount("roof", 7) });
			Assert.That(result.Count, Is.EqualTo(1));
			Assert.That(result[0].Amount, Is.EqualTo(4));
		}

		[TestCase("water:1")]
		[TestCase("food:1")]
		public void GenericProviderRejectsCustodyOnlyResources(string carries)
		{
			Assert.That(KingdomBenefitProviderRules.TryDescribe("test:provider", carries, "",
				"building", "present", "", out _, out string failure), Is.False);
			StringAssert.Contains("physical inventory", failure);
		}

		[TestCase("-taf:dark")]
		[TestCase("taf:dark|taf:sky")]
		[TestCase("taf:dark,,taf:sky")]
		public void ProviderTagsArePositiveBoundedTokens(string tags)
		{
			Assert.That(KingdomBenefitProviderRules.TryDescribe("test:provider", "", tags,
				"building", "present", "", out _, out _), Is.False);
		}

		[Test]
		public void DuplicateAmountsFoldBeforeCap()
		{
			Assert.That(KingdomBenefitProviderRules.TryDescribe("test:provider",
				"roof:1,roof:2", "", "habitable", "present", "",
				out KingdomBenefitProviderDeclaration row, out _), Is.True);
			Assert.That(row.Carries.Count, Is.EqualTo(1));
			Assert.That(row.Carries[0].Amount, Is.EqualTo(3));
		}

		[Test]
		public void CoveredYardIsCoherentButInteriorYardIsNot()
		{
			KingdomBenefitDesignation coveredYard = Row("yard-a", "root-a",
				new KingdomBenefitCell(2, 2, KingdomBenefitCellUse.Plot
					| KingdomBenefitCellUse.Yard | KingdomBenefitCellUse.Covered,
					KingdomBenefitCover.Soft));
			Assert.That(KingdomDesignationRules.TryNormalize(coveredYard, "zone", 80, 25,
				out _, out _), Is.True);
			coveredYard.Cells[0] = new KingdomBenefitCell(2, 2,
				coveredYard.Cells[0].Use | KingdomBenefitCellUse.Interior,
				KingdomBenefitCover.Soft);
			Assert.That(KingdomDesignationRules.TryNormalize(coveredYard, "zone", 80, 25,
				out _, out _), Is.False);
		}

		[Test]
		public void CoverKindMustAgreeWithCoveredBit()
		{
			KingdomBenefitDesignation row = Row("a", "root-a", new KingdomBenefitCell(1, 1,
				KingdomBenefitCellUse.Plot | KingdomBenefitCellUse.Building,
				KingdomBenefitCover.Walled));
			Assert.That(KingdomDesignationRules.TryNormalize(row, "zone", 80, 25,
				out _, out _), Is.False);
		}

		[Test]
		public void SameRootCannotBecomeTwoBuildings()
		{
			KingdomBenefitDesignation a = Row("a", "root", new KingdomBenefitCell(1, 1,
				KingdomBenefitCellUse.Plot));
			KingdomBenefitDesignation b = Row("b", "root", new KingdomBenefitCell(8, 8,
				KingdomBenefitCellUse.Plot));
			Assert.That(KingdomDesignationIndex.TryCreate(new[] { a, b }, "zone", 80, 25,
				out _, out string failure), Is.False);
			StringAssert.Contains("root is duplicated", failure);
		}

		[Test]
		public void DesignationSnapshotEqualityCoversTheWholeNormalizedAuthority()
		{
			KingdomBenefitDesignation baseline = Row("a", "root-a",
				new KingdomBenefitCell(1, 1, KingdomBenefitCellUse.Plot));
			baseline.LotId = "lot-a"; baseline.Caps.Add(new KindAmount("beds", 2));
			baseline.AcceptedTags.Add("taf:cooking");
			Assert.That(KingdomDesignationIndex.SameExactDesignation(baseline,
				KingdomDesignationRules.Copy(baseline)), Is.True);
			Action<KingdomBenefitDesignation>[] mutations = {
				r => r.ProviderId = "other.provider", r => r.ProviderVersion = "2",
				r => r.Identity = "b", r => r.Revision = "def", r => r.ZoneId = "other-zone",
				r => r.RootId = "root-b", r => r.BuildingKey = "smithy", r => r.LotId = "lot-b",
				r => r.Caps[0] = new KindAmount("beds", 3),
				r => r.AcceptedTags[0] = "taf:shrine",
				r => r.Cells[0] = new KingdomBenefitCell(2, 1, KingdomBenefitCellUse.Plot),
				r => r.Cells[0] = new KingdomBenefitCell(1, 1,
					KingdomBenefitCellUse.Plot | KingdomBenefitCellUse.Network, "road")
			};
			for (int i = 0; i < mutations.Length; i++)
			{
				KingdomBenefitDesignation changed = KingdomDesignationRules.Copy(baseline);
				mutations[i](changed);
				Assert.That(KingdomDesignationIndex.SameExactDesignation(baseline, changed),
					Is.False, "mutation " + i);
			}
		}

		[Test]
		public void DesignationSnapshotEqualityRejectsAuthorityAdditionsAndRemovals()
		{
			KingdomBenefitDesignation a = Row("a", "root-a",
				new KingdomBenefitCell(1, 1, KingdomBenefitCellUse.Plot));
			KingdomBenefitDesignation b = Row("b", "root-b",
				new KingdomBenefitCell(2, 2, KingdomBenefitCellUse.Plot));
			Assert.That(KingdomDesignationIndex.TryCreate(new[] { a }, "zone", 80, 25,
				out KingdomDesignationIndex first, out string failure), Is.True, failure);
			Assert.That(KingdomDesignationIndex.TryCreate(new[] {
				KingdomDesignationRules.Copy(a) }, "zone", 80, 25,
				out KingdomDesignationIndex same, out failure), Is.True, failure);
			Assert.That(KingdomDesignationIndex.TryCreate(new[] { a, b }, "zone", 80, 25,
				out KingdomDesignationIndex added, out failure), Is.True, failure);
			Assert.That(first.SameSnapshot(same), Is.True);
			Assert.That(first.SameSnapshot(added), Is.False);
		}

		[Test]
		public void ProviderCustodyDependsOnlyOnCurrentUniquePlacement()
		{
			Assert.That(KingdomBenefitEmbodimentRules.ProviderBelongs(true, 1), Is.True);
			Assert.That(KingdomBenefitEmbodimentRules.ProviderBelongs(true, 2), Is.False);
			Assert.That(KingdomBenefitEmbodimentRules.ProviderBelongs(false, 1), Is.False);
		}

		[Test]
		public void CoveredAdjacentFurnitureIsInteriorButBlockedShellIsNot()
		{
			Assert.That(KingdomBenefitEmbodimentRules.AuthoredInterior(true, true, false),
				Is.True, "Adjacent and Walkable are both non-blocked authored cells");
			Assert.That(KingdomBenefitEmbodimentRules.AuthoredInterior(true, true, true),
				Is.False, "blocked shell must remain outside Interior");
			Assert.That(KingdomBenefitEmbodimentRules.AuthoredInterior(false, true, false),
				Is.False, "covered yard is not Interior");
		}

		[Test]
		public void InteriorFurnitureDoesNotErasePhysicalShellDefence()
		{
			List<KingdomBenefitCell> cells = new List<KingdomBenefitCell> {
				new KingdomBenefitCell(1, 1, KingdomBenefitCellUse.Plot
					| KingdomBenefitCellUse.Building | KingdomBenefitCellUse.Covered
					| KingdomBenefitCellUse.Interior, KingdomBenefitCover.Walled),
				new KingdomBenefitCell(1, 2, KingdomBenefitCellUse.Plot
					| KingdomBenefitCellUse.Building | KingdomBenefitCellUse.Covered,
					KingdomBenefitCover.Walled),
				new KingdomBenefitCell(2, 2, KingdomBenefitCellUse.Plot
					| KingdomBenefitCellUse.Building | KingdomBenefitCellUse.Covered,
					KingdomBenefitCover.Natural)
			};
			int shell = KingdomBenefitEmbodimentRules.StructuralShellCells(cells);
			Assert.That(shell, Is.EqualTo(2));
			Assert.That(KingdomBenefitEmbodimentRules.OperationalStructureAmount(shell, 100),
				Is.GreaterThan(0));
		}

		[TestCase(12, 0, 0)]
		[TestCase(12, 25, 3)]
		[TestCase(12, 50, 6)]
		[TestCase(12, 100, 12)]
		[TestCase(12, 140, 12)]
		public void DefensiveShellNeedsCurrentOperationalEffectiveness(int physical,
			int effectiveness, int expected)
		{
			Assert.That(KingdomBenefitEmbodimentRules.OperationalStructureAmount(
				physical, effectiveness), Is.EqualTo(expected));
		}

		[Test]
		public void DefensiveShellRuntimeReprovesFabricStaffingConditionAndAffinity()
		{
			string source = TestMain.ReadRepositoryText(System.IO.Path.Combine(
				"Growth", "KingdomBenefitIndex.Aggregation.cs"));
			StringAssert.Contains("TryVerifyBenefitShell(row.Root, Z", source);
			StringAssert.Contains("row.Root.IsBroken()", source);
			StringAssert.Contains("KingdomWear.EffectivenessOf(row.Root)", source);
			StringAssert.Contains("OperationalStructureAmount", source);
		}

		[Test]
		public void StructureTagsRespectCoverAndDepth()
		{
			Assert.That(KingdomDesignationIndex.StructuralTags(
				KingdomBenefitCover.Soft, false), Is.EqualTo(new[] { KingdomQolRules.TagSky }));
			Assert.That(KingdomDesignationIndex.StructuralTags(
				KingdomBenefitCover.Walled, false), Is.EqualTo(new[] { KingdomQolRules.TagDark }));
			Assert.That(KingdomDesignationIndex.StructuralTags(
				KingdomBenefitCover.Soft, true), Is.EqualTo(new[] { KingdomQolRules.TagDark }));
		}

		private static KingdomBenefitDesignation Row(string id, string root,
			KingdomBenefitCell cell)
		{
			KingdomBenefitDesignation result = new KingdomBenefitDesignation {
				ProviderId = "test.provider", ProviderVersion = "1", Identity = id,
				Revision = "abc", ZoneId = "zone", RootId = root, BuildingKey = "house" };
			result.Cells.Add(cell); return result;
		}
	}
}
