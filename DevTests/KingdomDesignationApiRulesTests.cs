#if TAF_TESTS
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
			ClassicAssert.IsTrue(KingdomDesignationRules.TryTranslate(
				Row(new KingdomApiCell(3, 4), new KingdomApiCell(4, 4)), 80, 25,
				out KingdomBenefitDesignation row, out string failure), failure);
			ClassicAssert.AreEqual("other.mod", row.ProviderId);
			ClassicAssert.AreEqual("3", row.ProviderVersion);
			ClassicAssert.AreEqual("hall:7", row.Identity);
			ClassicAssert.AreEqual("r9", row.Revision);
			ClassicAssert.AreEqual("JoppaWorld.11.22.1.1.10", row.ZoneId);
			ClassicAssert.AreEqual("42", row.RootId);
			ClassicAssert.AreEqual("meeting-hall", row.BuildingKey);
			ClassicAssert.AreEqual("lot:1", row.LotId);
			ClassicAssert.AreEqual(2, row.Cells.Count);
			ClassicAssert.AreEqual(3, row.Cells[0].X); ClassicAssert.AreEqual(4, row.Cells[0].Y);
			ClassicAssert.AreEqual(KingdomDesignationRules.ExternalCellUse, row.Cells[0].Use);
			ClassicAssert.AreEqual(0, (int)(row.Cells[0].Use & (KingdomBenefitCellUse.Covered
				| KingdomBenefitCellUse.Interior | KingdomBenefitCellUse.Ingress)));
			ClassicAssert.AreEqual(0, row.Caps.Count);
			ClassicAssert.AreEqual(0, row.AcceptedTags.Count);
		}

		[TestCase(-1, 0)]
		[TestCase(0, -1)]
		[TestCase(80, 0)]
		[TestCase(0, 25)]
		public void ACellOutsideTheActiveZoneRefusesTheWholeRow(int X, int Y)
		{
			ClassicAssert.IsFalse(KingdomDesignationRules.TryTranslate(
				Row(new KingdomApiCell(1, 1), new KingdomApiCell(X, Y)), 80, 25,
				out KingdomBenefitDesignation row, out string failure));
			ClassicAssert.IsNull(row);
			StringAssert.Contains("outside the active zone", failure);
		}

		[Test]
		public void ADuplicatedCellRefusesTheWholeRow()
		{
			ClassicAssert.IsFalse(KingdomDesignationRules.TryTranslate(
				Row(new KingdomApiCell(5, 5), new KingdomApiCell(5, 5)), 80, 25,
				out KingdomBenefitDesignation row, out string failure));
			ClassicAssert.IsNull(row);
			StringAssert.Contains("duplicated", failure);
		}

		[Test]
		public void NullEmptyOrOverBoundCellsRefuse()
		{
			ClassicAssert.IsFalse(KingdomDesignationRules.TryTranslate(Row((KingdomApiCell[])null),
				80, 25, out _, out string nullFailure));
			StringAssert.Contains("no bounded exact cells", nullFailure);
			ClassicAssert.IsFalse(KingdomDesignationRules.TryTranslate(Row(), 80, 25, out _, out _));
			KingdomApiCell[] over = new KingdomApiCell[
				KingdomDesignationRules.MaxCellsPerDesignation + 1];
			ClassicAssert.IsFalse(KingdomDesignationRules.TryTranslate(Row(over), 80, 25, out _, out _));
			ClassicAssert.IsFalse(KingdomDesignationRules.TryTranslate(null, 80, 25, out _, out _));
			ClassicAssert.IsFalse(KingdomDesignationRules.TryTranslate(Row(new KingdomApiCell(0, 0)),
				0, 25, out _, out _));
		}

		[Test]
		public void TheApiFaceRoundTripsIdentityAndCells()
		{
			KingdomApiDesignation source = Row(new KingdomApiCell(7, 8), new KingdomApiCell(8, 8));
			ClassicAssert.IsTrue(KingdomDesignationRules.TryTranslate(source, 80, 25,
				out KingdomBenefitDesignation row, out _));
			KingdomApiDesignation back = KingdomDesignationRules.ToApi(row);
			ClassicAssert.AreEqual(source.Identity, back.Identity);
			ClassicAssert.AreEqual(source.RootId, back.RootId);
			ClassicAssert.AreEqual(source.BuildingKey, back.BuildingKey);
			ClassicAssert.AreEqual(2, back.Cells.Length);
			ClassicAssert.AreEqual(new KingdomApiCell(7, 8), back.Cells[0]);
			ClassicAssert.AreEqual(new KingdomApiCell(8, 8), back.Cells[1]);
			ClassicAssert.IsNull(KingdomDesignationRules.ToApi(null));
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
