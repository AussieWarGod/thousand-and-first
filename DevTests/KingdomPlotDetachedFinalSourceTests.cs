#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.GreaterOrEqual(start, 0);
			return source.Substring(start);
		}

		/// <summary>
		/// The engine assigns object ids lazily. The created branch of TryFinishOutput must ask the
		/// freshly created building for .ID -- which ASSIGNS one -- before anything reads
		/// IDIfAssigned off it, or the custody root is asked to publish under a null id and the
		/// whole finish refuses in silence on every pass (issue #172; the same class was fixed on
		/// the staging side in RootStagingOutput on 2026-08-30). Source-only: this fixture has no
		/// game host, so the lazy-id behaviour itself is pinned at both sites rather than executed.
		/// </summary>
		[Test]
		public void TheCreatedFinalOutputAsksForItsIdBeforeReadingIDIfAssigned()
		{
			string finish = Read("Growth/KingdomPlot2.31.FinishOutput.cs");
			StringAssert.Contains("expectedOutput = building.ID;", finish);
			string created = Between(finish, "building = GameObject.Create(entry.Blueprint)",
				"bool exactEndpoint =");
			int firstAssigning = created.IndexOf("building.ID;", StringComparison.Ordinal);
			int firstLazyRead = created.IndexOf("building.IDIfAssigned", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(firstAssigning, 0,
				"the created final output must ask for building.ID");
			ClassicAssert.IsTrue(firstLazyRead < 0 || firstAssigning < firstLazyRead,
				"building.IDIfAssigned must never be read before building.ID has assigned one");
			StringAssert.DoesNotContain("expectedOutput = building.IDIfAssigned;", created);
			// The staging sibling this mirrors, so neither side can regress alone.
			string staging = Read("Growth/KingdomArchitectureStamper.StagingCustody.cs");
			Ordered(staging, "private static bool RootStagingOutput(",
				"string id = Output.ID;", "StagingRootPrefix + id");
		}

		/// <summary>
		/// A refused finish, a refused stage and the revert that follows them all say so. This is
		/// the stall signature of issue #172: at most one line per plot per pass, because
		/// TryFinishOutput returns at its first fault and Advance breaks at its first refusal.
		/// </summary>
		[Test]
		public void ARefusedFinishStageAndRevertAreAllNamed()
		{
			string finish = Read("Growth/KingdomPlot2.31.FinishOutput.cs");
			Ordered(finish,
				"private static bool FinishOutputFault(GameObject Parent, string Step)",
				"KingdomLog.Log(\"plot finish refused: \" + Step + \" (lot \"",
				"return false;");
			foreach (string step in new[] {
				"no final building to publish",
				"the new final output is not prepared",
				"the published final output id did not stick",
				"the physical phase would not reach pending",
				"the pending physical endpoints disagree",
				"the final output is not prepared for placing",
				"the physical phase would not settle",
				"the placed final building is not exact" })
				StringAssert.Contains("FinishOutputFault(parent, \"" + step, finish);
			// Wrapped onto its own line because it names the id it could not root under.
			Ordered(finish, "return FinishOutputFault(parent,",
				"\"the new final output could not be rooted under id \"",
				"string.IsNullOrEmpty(expectedOutput) ? \"<none>\" : expectedOutput");
			// The two wrapped calls: the root fault names the id, and both receipt faults name
			// which side of the publication would not take it.
			foreach (string wrapped in new[] {
				"\"the receipt would not take the rooted final output id\"",
				"\"the receipt would not take the new final output id\"" })
				StringAssert.Contains(wrapped, finish);
			string labour = Read("Growth/KingdomPlot2.26.Labour.cs");
			Ordered(labour, "if (!Apply(Works, next))",
				"KingdomLog.Log(\"plot stage refused: \"",
				"\" stage=\" + next + \" applied=\"",
				"break;");
			string inspect = Read("Growth/KingdomPlot2.16.RecoveryInspect.cs");
			Ordered(inspect, "KingdomLog.Log(\"plot job reverted to working: \" + inspected.Id",
				"KingdomConstruction.FinishProjection(ref inspected, true, true);");
		}

		private static string Read(string path)
		{
			return Regex.Replace(TestMain.ReadRepositoryText(path), @"\s+", " ");
		}

		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(last, first, end);
			return source.Substring(first, last - first);
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = 0;
			foreach (string term in terms)
			{
				int at = source.IndexOf(term, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, term);
				cursor = at + term.Length;
			}
		}
	}
}
#endif
