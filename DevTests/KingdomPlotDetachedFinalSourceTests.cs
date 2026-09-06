#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	// Source-only boundary regressions; these do not execute native creation or placement.
	[TestFixture]
	public sealed class KingdomPlotDetachedFinalSourceTests
	{
		[Test]
		public void DetachedRectUsesFrozenStampsAndTheSameLiveParentZoneBounds()
		{
			string prepared = Prepared();
			Ordered(prepared,
				"if (!GameObject.Validate(Parent)) return false;",
				"Zone zone = Parent.CurrentZone;", "if (zone == null) return false;",
				"Building.CurrentCell == null && Building.CurrentZone == null",
				"TryReadStampedRect(Building, out KingdomPlotRules.PlotRect rect)",
				"SameRect(rect, Rect)", "PlotPlanMarkerRemovalProofMatches(Parent, Building)",
				"GameObject.Validate(Parent) && object.ReferenceEquals(Parent.CurrentZone, zone)",
				"KingdomPlotRules.ValidZoneRect(rect, zone.Width, zone.Height)",
				"KingdomPlotRules.ValidZoneRect(Footprint, zone.Width, zone.Height)");
			StringAssert.DoesNotContain("TryReadRect(", prepared);
			StringAssert.DoesNotContain("TryReadFootprint(", prepared);
			StringAssert.DoesNotContain("AddObject(", prepared);
		}

		[Test]
		public void DetachedFootprintRequiresEveryExactTypedFrozenCoordinate()
		{
			string prepared = Prepared();
			foreach (string coordinate in new[] { "X1", "Y1", "X2", "Y2" })
				StringAssert.Contains("ExactFoundingHeartInt(Building, Foot" + coordinate
					+ "Property, Footprint." + coordinate + ")", prepared);
			string exact = Between(Read("Growth/KingdomPlot2.07f.FoundingHeartStakeTruth.cs"),
				"private static bool ExactFoundingHeartInt(",
				"private static bool ExactFoundingHeartString(");
			StringAssert.Contains("return Object.HasIntProperty(Key)"
				+ " && !Object.HasStringProperty(Key) && Object.GetIntProperty(Key) == Expected;",
				exact);
			StringAssert.Contains("RoofOf(Building) == Roof", prepared);
		}

		[Test]
		public void DetachedAdmissionRetainsIdentityCustodyAndConstructionProvenance()
		{
			string prepared = Prepared();
			Ordered(prepared,
				"return GameObject.Validate(Building)",
				"Building.IDIfAssigned == ExpectedId && Building.Blueprint == Entry.Blueprint",
				"Building.GetStringProperty(PlotFinalPredecessorProperty) == Parent.IDIfAssigned",
				"Building.CurrentCell == null && Building.CurrentZone == null",
				"Building.InInventory == null && Building.GetIntProperty(\"KingdomBuilt\") == 1",
				"Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == Entry.Key",
				"Building.GetStringProperty(PlotIdProperty) == PlotId",
				"(string.IsNullOrEmpty(Receipt)"
					+ " || Building.GetStringProperty(KingdomConstruction.ReceiptProperty) == Receipt)",
				"(Job == null || KingdomConstruction.HasReceipt(Building, Job)"
					+ " && KingdomConstruction.PaidBuildMatches(Building, Job))",
				"PlotPlanMarkerRemovalProofMatches(Parent, Building)");
		}

		[Test]
		public void FinalCreationProvesDetachedOutputBeforeRootingAndBeforePlacement()
		{
			string finish = Read("Growth/KingdomPlot2.31.FinishOutput.cs");
			Ordered(finish,
				"PrepareFinalBuilding(building, entry, receipt, id, Rect, Footprint, Roof,",
				"building.SetStringProperty(PlotFinalPredecessorProperty, parent.IDIfAssigned);",
				"if (!PreparedPlotFinalOutput(building, parent, entry, receipt, id, Rect,",
				"|| !RootPlotFinalOutput(expectedOutput, building)) return false;");
			string placement = Between(finish,
				"if (building.CurrentCell == null && building.InInventory == null)",
				"bool exactEndpoint =");
			Ordered(placement,
				"if (!PreparedPlotFinalOutput(building, parent, entry, receipt, id, Rect,",
				"Footprint, Roof, expectedOutput, construction)) return false;",
				"accepted = cell.AddObject(building);",
				"KingdomSurvey.ObserveAddResultInActive(Z, building, accepted)");
			string prepare = Between(Read("Growth/KingdomPlot2.27.FinalBuilding.cs"),
				"private static void PrepareFinalBuilding(",
				"private static bool ExactFinalBuilding(");
			Ordered(prepare, "StampRect(Building, Rect);",
				"StampFootprint(Building, Footprint, Roof);");
		}

		[Test]
		public void PlacedReadersStillRequireObjectZoneAndRejectTornGeometry()
		{
			string geometry = Read("Growth/KingdomPlot2.06.Geometry.cs");
			string rect = Between(geometry, "public static bool TryReadRect(",
				"public static bool TryReadStampedRect(");
			Ordered(rect, "Zone zone = Object == null ? null : Object.CurrentZone;",
				"if (zone == null) { return false; }", "if (mistyped) return false;",
				"if (anyProperties && (!allProperties",
				"KingdomPlotRules.ValidZoneRect(Rect, zone.Width, zone.Height)");
			string footprint = geometry.Substring(geometry.IndexOf(
				"public static bool TryReadFootprint(", StringComparison.Ordinal));
			Ordered(footprint, "Object.HasStringProperty(FootX1Property)",
				"Zone zone = Object.CurrentZone;",
				"if (zone == null || !x1 || !y1 || !x2 || !y2) return false;",
				"KingdomPlotRules.ValidZoneRect(Footprint, zone.Width, zone.Height)");
			string placed = Between(Read("Growth/KingdomPlot2.27.FinalBuilding.cs"),
				"private static bool ExactFinalBuilding(", "private static bool ClearGround(");
			Ordered(placed, "Building.CurrentZone != Z", "Building.CurrentCell != Cell",
				"!TryReadRect(Building, out var observed)",
				"!TryReadFootprint(Building, out var foot)");
		}

		private static string Prepared()
		{
			string source = Read("Growth/KingdomPlot2.31b.FinishOutputCustody.cs");
			int start = source.IndexOf("private static bool PreparedPlotFinalOutput(",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0);
			return source.Substring(start);
		}

		private static string Read(string path)
		{
			return Regex.Replace(TestMain.ReadRepositoryText(path), @"\s+", " ");
		}

		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			Assert.Greater(last, first, end);
			return source.Substring(first, last - first);
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = 0;
			foreach (string term in terms)
			{
				int at = source.IndexOf(term, cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(at, cursor, term);
				cursor = at + term.Length;
			}
		}
	}
}
#endif
