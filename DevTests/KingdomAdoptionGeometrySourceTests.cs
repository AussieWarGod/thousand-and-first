#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomAdoptionGeometrySourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		[Test]
		public void RuntimeReaderSeparatesStructureFromSafeUsability()
		{
			string source = Read("Growth/KingdomAdopt.Helpers.cs");
			foreach (string evidence in new[] {
				"CellObservationAt", "EnclosureRegion.Ingress", "EnclosureRegion.Shell",
				"EnclosureRegion.Membership, UsableCell(cell)", "cell.HasWall()",
				"Cell.HasOpenLiquidVolume()", "item.IsPlayer() || item.IsCreature",
				"down != null && down.PullDown", "mine != null && mine.Armed",
				"item.HasPart(\"Gas\")",
				"item.HasTagOrProperty(\"Pit\")", "item.HasTagOrProperty(\"NoAutowalk\")",
				"item.Physics.Takeable", "item.ConsiderSolid()", "door.Locked",
				"MaxUsableNavigationWeight = 5", "Cell.IsPassable(null, false)",
				"Cell.NavigationWeight(null, Smart: true",
				"IgnoreCreatures: true) <= MaxUsableNavigationWeight" })
				StringAssert.Contains(evidence, source);
			Assert.That(source.IndexOf("if (door)", StringComparison.Ordinal),
				Is.LessThan(source.IndexOf("if (cell.HasWall())", StringComparison.Ordinal)),
				"a safely openable door on wall ground is ingress, not shell");
		}

		[Test]
		public void AdmissionCommitAndBenefitReadUseOneExactLiveModel()
		{
			string work = Read("Growth/KingdomAdopt.Work.cs");
			string designation = Read("Growth/KingdomAdoptionDesignation.cs");
			string source = Read("Growth/KingdomDesignationSources.Adopted.cs");
			Assert.That(Count(work, "MeasureExactRoom("), Is.EqualTo(2),
				"admission and pre-commit must both observe current ground");
			StringAssert.Contains("KingdomAdoptRules.MeetsMinimumUsable(role, spec.Size",
				work);
			StringAssert.Contains("KingdomAdopt.MeasureExactRoom(", designation);
			StringAssert.Contains("KingdomAdoptRules.MeetsMinimumUsable(", designation);
			StringAssert.Contains("KingdomAdoptRules.SameMembership(", designation);
			StringAssert.Contains("KingdomAdoptionDesignation.TryReproveLocal", source);
			StringAssert.DoesNotContain("MeasureEnclosure(", designation + source + work);
		}

		[Test]
		public void IrregularRoomCollisionUsesOnlyExactMembershipCells()
		{
			string work = Read("Growth/KingdomAdopt.Work.cs");
			string open = Read("Growth/KingdomAdopt.OpenPlot.cs");
			int method = open.IndexOf("private static bool CellsAreUnclaimed",
				StringComparison.Ordinal);
			Assert.That(method, Is.GreaterThanOrEqualTo(0));
			string body = open.Substring(method);
			StringAssert.Contains("for (int i = 0; i < Cells.Count; i++)", body);
			StringAssert.Contains("index.Containing(Cells[i].X, Cells[i].Y", body);
			StringAssert.Contains("CellsAreUnclaimed(Z, Enclosure.FloorCells", work);
			foreach (string banned in new[] { "int x1", "int x2", "int y1", "int y2",
				"for (int x =", "for (int y =" })
				StringAssert.DoesNotContain(banned, body,
					"a concave room must not claim gaps in its bounding box");
		}

		[Test]
		public void CurrentWireAddsOnlyOpenPlotAuthorityAndKeepsD1Canonical()
		{
			string runtime = Read("Growth/KingdomAdoptionDesignation.cs");
			string rules = Read("Growth/KingdomAdoptionDesignationRules.cs");
			StringAssert.Contains("public const int ReceiptSchema = 1;", runtime);
			StringAssert.Contains("public const int Schema = 1;", rules);
			StringAssert.Contains("fields[0] == \"d1\"", rules);
			StringAssert.Contains("fields[0] == \"d2\"", rules);
			StringAssert.Contains("Receipt.WireVersion == 1 ? \"d1\" : \"d2\"", rules);
			StringAssert.Contains("Receipt.OpenPlot", rules);
			StringAssert.DoesNotContain("UsableCells", rules);
			StringAssert.DoesNotContain("ShellCells", rules);
			StringAssert.DoesNotContain("IngressCells", rules);
		}

		[Test]
		public void AdoptionProductionUnitsStayUnderTheServiceLimit()
		{
			foreach (string path in new[] { "Growth/KingdomAdoptRules.Enclosure.cs",
				"Growth/KingdomAdopt.Helpers.cs", "Growth/KingdomAdopt.Work.cs",
				"Growth/KingdomAdopt.OpenPlot.cs", "Growth/KingdomAdoptionPlotRules.cs",
				"Growth/KingdomAdoptionDesignationRules.cs",
				"Growth/KingdomAdoptionDesignation.cs",
				"Growth/KingdomDesignationSources.Adopted.cs" })
			{
				int lines = File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot, path)).Length;
				Assert.That(lines, Is.LessThan(300), path);
			}
		}

		private static int Count(string source, string needle)
		{
			int count = 0, at = 0;
			while ((at = source.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
			{
				count++; at += needle.Length;
			}
			return count;
		}
	}
}
#endif
