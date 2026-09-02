#if TAF_TESTS
using System.IO;
using NUnit.Framework;
using ThousandAndFirst.Api;

namespace ThousandAndFirst.Tests
{
	/// <summary>The Api seam for designations: Api rows become internal rows only after every
	/// cell is proved inside the zone and unique, and nothing but Api types cross the seam.</summary>
	public class KingdomDesignationApiRulesTests
	{
		private static KingdomApiDesignation Row(params KingdomApiCell[] Cells)
		{
			return new KingdomApiDesignation {
				ProviderId = "other.mod", ProviderVersion = "3", Identity = "hall:7",
				Revision = "r9", ZoneId = "JoppaWorld.11.22.1.1.10", RootId = "42",
				BuildingKey = "meeting-hall", LotId = "lot:1", Cells = Cells
			};
		}

		[Test]
		public void TranslationCarriesIdentityAndGrantsOnlyOpenYardUse()
		{
			Assert.IsTrue(KingdomDesignationRules.TryTranslate(
				Row(new KingdomApiCell(3, 4), new KingdomApiCell(4, 4)), 80, 25,
				out KingdomBenefitDesignation row, out string failure), failure);
			Assert.AreEqual("other.mod", row.ProviderId);
			Assert.AreEqual("3", row.ProviderVersion);
			Assert.AreEqual("hall:7", row.Identity);
			Assert.AreEqual("r9", row.Revision);
			Assert.AreEqual("JoppaWorld.11.22.1.1.10", row.ZoneId);
			Assert.AreEqual("42", row.RootId);
			Assert.AreEqual("meeting-hall", row.BuildingKey);
			Assert.AreEqual("lot:1", row.LotId);
			Assert.AreEqual(2, row.Cells.Count);
			Assert.AreEqual(3, row.Cells[0].X); Assert.AreEqual(4, row.Cells[0].Y);
			Assert.AreEqual(KingdomDesignationRules.ExternalCellUse, row.Cells[0].Use);
			Assert.AreEqual(0, (int)(row.Cells[0].Use & (KingdomBenefitCellUse.Covered
				| KingdomBenefitCellUse.Interior | KingdomBenefitCellUse.Ingress)));
			Assert.AreEqual(0, row.Caps.Count);
			Assert.AreEqual(0, row.AcceptedTags.Count);
		}

		[TestCase(-1, 0)]
		[TestCase(0, -1)]
		[TestCase(80, 0)]
		[TestCase(0, 25)]
		public void ACellOutsideTheActiveZoneRefusesTheWholeRow(int X, int Y)
		{
			Assert.IsFalse(KingdomDesignationRules.TryTranslate(
				Row(new KingdomApiCell(1, 1), new KingdomApiCell(X, Y)), 80, 25,
				out KingdomBenefitDesignation row, out string failure));
			Assert.IsNull(row);
			StringAssert.Contains("outside the active zone", failure);
		}

		[Test]
		public void ADuplicatedCellRefusesTheWholeRow()
		{
			Assert.IsFalse(KingdomDesignationRules.TryTranslate(
				Row(new KingdomApiCell(5, 5), new KingdomApiCell(5, 5)), 80, 25,
				out KingdomBenefitDesignation row, out string failure));
			Assert.IsNull(row);
			StringAssert.Contains("duplicated", failure);
		}

		[Test]
		public void NullEmptyOrOverBoundCellsRefuse()
		{
			Assert.IsFalse(KingdomDesignationRules.TryTranslate(Row((KingdomApiCell[])null),
				80, 25, out _, out string nullFailure));
			StringAssert.Contains("no bounded exact cells", nullFailure);
			Assert.IsFalse(KingdomDesignationRules.TryTranslate(Row(), 80, 25, out _, out _));
			KingdomApiCell[] over = new KingdomApiCell[
				KingdomDesignationRules.MaxCellsPerDesignation + 1];
			Assert.IsFalse(KingdomDesignationRules.TryTranslate(Row(over), 80, 25, out _, out _));
			Assert.IsFalse(KingdomDesignationRules.TryTranslate(null, 80, 25, out _, out _));
			Assert.IsFalse(KingdomDesignationRules.TryTranslate(Row(new KingdomApiCell(0, 0)),
				0, 25, out _, out _));
		}

		[Test]
		public void TheApiFaceRoundTripsIdentityAndCells()
		{
			KingdomApiDesignation source = Row(new KingdomApiCell(7, 8), new KingdomApiCell(8, 8));
			Assert.IsTrue(KingdomDesignationRules.TryTranslate(source, 80, 25,
				out KingdomBenefitDesignation row, out _));
			KingdomApiDesignation back = KingdomDesignationRules.ToApi(row);
			Assert.AreEqual(source.Identity, back.Identity);
			Assert.AreEqual(source.RootId, back.RootId);
			Assert.AreEqual(source.BuildingKey, back.BuildingKey);
			Assert.AreEqual(2, back.Cells.Length);
			Assert.AreEqual(new KingdomApiCell(7, 8), back.Cells[0]);
			Assert.AreEqual(new KingdomApiCell(8, 8), back.Cells[1]);
			Assert.IsNull(KingdomDesignationRules.ToApi(null));
		}

		/// <summary>The published contracts name no internal type: a Growth layout change cannot
		/// silently break a provider compiled against the Api.</summary>
		[Test]
		public void ProviderContractsNameOnlyApiTypes()
		{
			foreach (string file in new[] { "KingdomDesignationProvider.cs",
				"KingdomForeignFootprintProvider.cs", "KingdomApiDesignation.cs",
				"KingdomApiCell.cs" })
			{
				string source = TestMain.ReadRepositoryText(Path.Combine("Api", file));
				StringAssert.DoesNotContain("KingdomBenefitDesignation", source);
				StringAssert.DoesNotContain("KingdomBenefitCell", source);
				StringAssert.DoesNotContain("ArchitecturePoint", source);
				StringAssert.DoesNotContain("using ThousandAndFirst;", source);
			}
			string runtime = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomDesignationIndex.Runtime.cs"));
			StringAssert.Contains("KingdomDesignationRules.TryTranslate(reported[i]", runtime);
			StringAssert.Contains("IKingdomTrustedDesignationSource", runtime);
		}
	}
}
#endif
