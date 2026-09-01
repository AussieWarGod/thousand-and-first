#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomAdoptRulesTests
	{
		[TestCase(KingdomPlotRules.PlotSize.Small, 4)]
		[TestCase(KingdomPlotRules.PlotSize.Medium, 12)]
		[TestCase(KingdomPlotRules.PlotSize.Large, 24)]
		[TestCase(KingdomPlotRules.PlotSize.Huge, 40)]
		public void AdoptedRoleFloorScalesWithDeclaredPlotTier(
			KingdomPlotRules.PlotSize size, int expected)
		{
			Assert.That(KingdomAdoptRules.MinimumUsableCells(
				KingdomAdoptRules.RoleKind.Work, size), Is.EqualTo(expected));
			Assert.That(KingdomAdoptRules.MinimumUsableCells(
				KingdomAdoptRules.RoleKind.Housing, size), Is.EqualTo(expected));
		}

		[Test]
		public void ExactContainerRoleKeepsItsOwnOneCellGeometry()
		{
			Assert.That(KingdomAdoptRules.MinimumUsableCells(
				KingdomAdoptRules.RoleKind.Storage, KingdomPlotRules.PlotSize.Huge),
				Is.EqualTo(1));
		}
		// --- ClassifyRole: BuildEntry.Category reused as the adoption taxonomy ----------------

		[TestCase("housing", KingdomAdoptRules.RoleKind.Housing)]
		[TestCase("Housing", KingdomAdoptRules.RoleKind.Housing)]
		[TestCase("HOUSING", KingdomAdoptRules.RoleKind.Housing)]
		[TestCase("  housing  ", KingdomAdoptRules.RoleKind.Housing)]
		[TestCase("storage", KingdomAdoptRules.RoleKind.Storage)]
		[TestCase("Storage", KingdomAdoptRules.RoleKind.Storage)]
		[TestCase("civic", KingdomAdoptRules.RoleKind.Work)]
		[TestCase("faith", KingdomAdoptRules.RoleKind.Work)]
		[TestCase("craft", KingdomAdoptRules.RoleKind.Work)]
		[TestCase("knowledge", KingdomAdoptRules.RoleKind.Work)]
		[TestCase("power", KingdomAdoptRules.RoleKind.Work)]
		[TestCase("defense", KingdomAdoptRules.RoleKind.Work)]
		[TestCase("food", KingdomAdoptRules.RoleKind.Work)]
		[TestCase("memorial", KingdomAdoptRules.RoleKind.Work)]
		[TestCase("some-third-party-category", KingdomAdoptRules.RoleKind.Work)]
		[TestCase(null, KingdomAdoptRules.RoleKind.Work)]
		[TestCase("", KingdomAdoptRules.RoleKind.Work)]
		public void ClassifyRole_MatchesTheTaxonomy(string category, KingdomAdoptRules.RoleKind expected)
		{
			Assert.AreEqual(expected, KingdomAdoptRules.ClassifyRole(category));
		}

		// --- MeasureEnclosure: the honest, bounded flood fill ----------------------------------

		/// <summary>Builds a lookup from a row-major grid: <c>#</c> is a wall, <c>+</c> is a
		/// door, anything else is open floor. A coordinate outside the grid reads as a wall, so
		/// tests never need to hand-draw a border.</summary>
		private static KingdomAdoptRules.CellLookup FromGrid(string[] Rows)
		{
			return delegate(int X, int Y)
			{
				if (Y < 0 || Y >= Rows.Length)
				{
					return KingdomAdoptRules.CellKind.Wall;
				}
				string row = Rows[Y];
				if (X < 0 || X >= row.Length)
				{
					return KingdomAdoptRules.CellKind.Wall;
				}
				char c = row[X];
				if (c == '+')
				{
					return KingdomAdoptRules.CellKind.Door;
				}
				if (c == '#')
				{
					return KingdomAdoptRules.CellKind.Wall;
				}
				return KingdomAdoptRules.CellKind.Open;
			};
		}

		private static KingdomAdoptRules.ExactCellLookup FromExactGrid(string[] Rows)
		{
			return delegate(int X, int Y)
			{
				if (Y < 0 || Y >= Rows.Length || X < 0 || X >= Rows[Y].Length)
					return new KingdomAdoptRules.CellObservation(
						KingdomAdoptRules.EnclosureRegion.Outside);
				switch (Rows[Y][X])
				{
				case '#': return new KingdomAdoptRules.CellObservation(
					KingdomAdoptRules.EnclosureRegion.Shell);
				case '+': return new KingdomAdoptRules.CellObservation(
					KingdomAdoptRules.EnclosureRegion.Ingress);
				case 'X': return new KingdomAdoptRules.CellObservation(
					KingdomAdoptRules.EnclosureRegion.Membership, false);
				default: return new KingdomAdoptRules.CellObservation(
					KingdomAdoptRules.EnclosureRegion.Membership, true);
				}
			};
		}

		[Test]
		public void MeasureEnclosure_ClosedRoomWithOneDoorIsBounded()
		{
			string[] room =
			{
				"#####",
				"#...#",
				"#...+",
				"#...#",
				"#####"
			};
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(2, 2, FromGrid(room));
			Assert.IsTrue(measurement.Bounded);
			Assert.AreEqual(9, measurement.RoomCells);
			Assert.AreEqual(1, measurement.DoorCells);
		}

		[Test]
		public void MeasureEnclosure_CountsEachDoorOnceEvenApproachedFromMultipleSides()
		{
			// A mutation that marked a door visited only from the direction it was first
			// approached, rather than adding it to the shared visited set, would count this
			// central door up to four times - once per interior neighbour.
			string[] room =
			{
				"#####",
				"#...#",
				"#.+.#",
				"#...#",
				"#####"
			};
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(1, 1, FromGrid(room));
			Assert.IsTrue(measurement.Bounded);
			Assert.AreEqual(8, measurement.RoomCells);
			Assert.AreEqual(1, measurement.DoorCells);
		}

		[Test]
		public void MeasureEnclosure_TwoDoorsBothCount()
		{
			string[] room =
			{
				"#####",
				"+...#",
				"#...#",
				"#...+",
				"#####"
			};
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(2, 2, FromGrid(room));
			Assert.IsTrue(measurement.Bounded);
			Assert.AreEqual(9, measurement.RoomCells);
			Assert.AreEqual(2, measurement.DoorCells);
		}

		[Test]
		public void MeasureEnclosure_SealedRoomWithNoDoorIsStillBounded()
		{
			// Bounded and door-less are different facts. MeasureEnclosure only measures the
			// room; KingdomAdoptRules.Assess is what turns "no door" into a refusal.
			string[] room =
			{
				"#####",
				"#...#",
				"#...#",
				"#...#",
				"#####"
			};
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(2, 2, FromGrid(room));
			Assert.IsTrue(measurement.Bounded);
			Assert.AreEqual(9, measurement.RoomCells);
			Assert.AreEqual(0, measurement.DoorCells);
		}

		[Test]
		public void MeasureEnclosure_ExactlyMinRoomSizeIsBounded()
		{
			string[] room =
			{
				"####",
				"#..#",
				"#..+",
				"####"
			};
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(1, 1, FromGrid(room));
			Assert.IsTrue(measurement.Bounded);
			Assert.AreEqual(KingdomAdoptRules.MinEnclosedRoomCells, measurement.RoomCells);
		}

		[Test]
		public void MeasureEnclosure_StartingOnAWallFindsNothing()
		{
			string[] room =
			{
				"#####",
				"#...#",
				"#...#",
				"#####"
			};
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(0, 0, FromGrid(room));
			Assert.IsFalse(measurement.Bounded);
			Assert.AreEqual(0, measurement.RoomCells);
			Assert.AreEqual(0, measurement.DoorCells);
		}

		[Test]
		public void MeasureEnclosure_StartingOnADoorFindsNothing()
		{
			// The founder must be standing on the room's own floor, not in the doorway, for the
			// fill to say anything about what is on either side of them.
			string[] room =
			{
				"#####",
				"#...+",
				"#####"
			};
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(4, 1, FromGrid(room));
			Assert.IsFalse(measurement.Bounded);
		}

		[Test]
		public void MeasureEnclosure_NullLookupFindsNothing()
		{
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(0, 0, null);
			Assert.IsFalse(measurement.Bounded);
			Assert.AreEqual(0, measurement.RoomCells);
		}

		[Test]
		public void MeasureEnclosure_OpenFieldExceedsBudgetAndReportsUnbounded()
		{
			// The honest cheaper proxy the design calls for: ground with nowhere to stop blows
			// the fill's budget almost immediately rather than being walked in full.
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(0, 0, (X, Y) => KingdomAdoptRules.CellKind.Open);
			Assert.IsFalse(measurement.Bounded);
			Assert.AreEqual(KingdomAdoptRules.MaxEnclosedRoomCells, measurement.RoomCells);
		}

		[TestCase(200, true)]
		[TestCase(201, false)]
		public void MeasureEnclosure_AcceptsExactBudgetButRefusesOneCellMore(int width,
			bool expectedBounded)
		{
			KingdomAdoptRules.CellLookup lookup = delegate(int x, int y)
			{
				if (x == 0 && y == 1) return KingdomAdoptRules.CellKind.Door;
				if (y == 1 && x >= 1 && x <= width) return KingdomAdoptRules.CellKind.Open;
				return KingdomAdoptRules.CellKind.Wall;
			};
			KingdomAdoptRules.EnclosureMeasurement measurement =
				KingdomAdoptRules.MeasureEnclosure(1, 1, lookup);
			Assert.That(measurement.Bounded, Is.EqualTo(expectedBounded));
			Assert.That(measurement.RoomCells,
				Is.EqualTo(System.Math.Min(width, KingdomAdoptRules.MaxEnclosedRoomCells)));
		}

		[Test]
		public void MeasureEnclosure_RoundTripsNegativeCoordinates()
		{
			// The visited-set packing must not collide or truncate negative coordinates; this
			// room is anchored entirely on the negative side of the origin to prove it.
			KingdomAdoptRules.CellLookup lookup = delegate(int X, int Y)
			{
				if (X == -3 && Y == -1)
				{
					return KingdomAdoptRules.CellKind.Door;
				}
				if (X >= -2 && X <= 0 && Y >= -2 && Y <= 0)
				{
					return KingdomAdoptRules.CellKind.Open;
				}
				return KingdomAdoptRules.CellKind.Wall;
			};
			KingdomAdoptRules.EnclosureMeasurement measurement = KingdomAdoptRules.MeasureEnclosure(-1, -1, lookup);
			Assert.IsTrue(measurement.Bounded);
			Assert.AreEqual(9, measurement.RoomCells);
			Assert.AreEqual(1, measurement.DoorCells);
		}

		[Test]
		public void ExactEnclosureSeparatesMembershipShellIngressAndReachableFloor()
		{
			string[] room = {
				"#######",
				"+..X..#",
				"#..X..#",
				"#..X..#",
				"#######"
			};
			KingdomAdoptRules.EnclosureMeasurement measured =
				KingdomAdoptRules.MeasureExactEnclosure(1, 1, FromExactGrid(room));
			Assert.Multiple(() => {
				Assert.That(measured.Bounded, Is.True);
				Assert.That(measured.RoomCells, Is.EqualTo(15));
				Assert.That(measured.DoorCells, Is.EqualTo(1));
				Assert.That(measured.IngressCells.Count, Is.EqualTo(1));
				Assert.That(measured.ShellCells.Count, Is.EqualTo(15));
				Assert.That(measured.UsableCells, Is.EqualTo(6),
					"a solid furnishing band may not lend the unreachable floor behind it");
			});
		}

		[Test]
		public void FurnitureChangesUsabilityButNeverSignedMembership()
		{
			string[] open = { "#####", "+...#", "#...#", "#####" };
			string[] furnished = { "#####", "+.X.#", "#...#", "#####" };
			KingdomAdoptRules.EnclosureMeasurement first =
				KingdomAdoptRules.MeasureExactEnclosure(1, 1, FromExactGrid(open));
			KingdomAdoptRules.EnclosureMeasurement second =
				KingdomAdoptRules.MeasureExactEnclosure(1, 1, FromExactGrid(furnished));
			Assert.That(KingdomAdoptRules.SameMembership(
				first.MembershipCells, second.MembershipCells), Is.True);
			Assert.That(second.UsableCells, Is.EqualTo(first.UsableCells - 1));
		}

		[Test]
		public void TierMinimumConsumesReachableUsableFloorNotMembershipArea()
		{
			string[] room = {
				"########",
				"+..X...#",
				"#..X...#",
				"########"
			};
			KingdomAdoptRules.EnclosureMeasurement measured =
				KingdomAdoptRules.MeasureExactEnclosure(1, 1, FromExactGrid(room));
			Assert.That(measured.RoomCells, Is.EqualTo(12));
			Assert.That(measured.UsableCells, Is.EqualTo(4));
			Assert.That(KingdomAdoptRules.MeetsMinimumUsable(
				KingdomAdoptRules.RoleKind.Work, KingdomPlotRules.PlotSize.Medium,
				measured), Is.False);
		}

		// --- Assess: Housing --------------------------------------------------------------------

		[TestCase(true, false, KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing)]
		[TestCase(false, true, KingdomAdoptRules.AdoptionVerdict.RefusedBelowStage)]
		[TestCase(false, false, KingdomAdoptRules.AdoptionVerdict.Adopted)]
		public void Assess_Housing_ChecksInProtectiveOrder(bool alreadyServing, bool belowStage,
			KingdomAdoptRules.AdoptionVerdict expected)
		{
			KingdomAdoptRules.EnclosureMeasurement enclosure = new KingdomAdoptRules.EnclosureMeasurement {
				Bounded = true, RoomCells = 8, DoorCells = 1 };
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(
				KingdomAdoptRules.RoleKind.Housing, alreadyServing, belowStage, false, enclosure);
			Assert.AreEqual(expected, verdict);
		}

		// --- Assess: Storage --------------------------------------------------------------------

		[TestCase(true, false, true, KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing)]
		[TestCase(false, true, true, KingdomAdoptRules.AdoptionVerdict.RefusedBelowStage)]
		[TestCase(false, false, true, KingdomAdoptRules.AdoptionVerdict.Adopted)]
		[TestCase(false, false, false, KingdomAdoptRules.AdoptionVerdict.RefusedNotStorageCapable)]
		public void Assess_Storage_ChecksInProtectiveOrder(bool alreadyServing, bool belowStage, bool isStorageCapable, KingdomAdoptRules.AdoptionVerdict expected)
		{
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Storage, alreadyServing, belowStage, isStorageCapable, default);
			Assert.AreEqual(expected, verdict);
		}

		// --- Assess: Work (civic, faith, craft, knowledge, power, defense, and unknowns) --------

		[Test]
		public void Assess_Work_AlreadyServingBeatsAPerfectRoom()
		{
			KingdomAdoptRules.EnclosureMeasurement enclosure = new KingdomAdoptRules.EnclosureMeasurement { Bounded = true, RoomCells = 20, DoorCells = 2 };
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, true, false, false, enclosure);
			Assert.AreEqual(KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing, verdict);
		}

		[Test]
		public void Assess_Work_BelowStageBeatsAPerfectRoom()
		{
			KingdomAdoptRules.EnclosureMeasurement enclosure = new KingdomAdoptRules.EnclosureMeasurement { Bounded = true, RoomCells = 20, DoorCells = 2 };
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, false, true, false, enclosure);
			Assert.AreEqual(KingdomAdoptRules.AdoptionVerdict.RefusedBelowStage, verdict);
		}

		[TestCase(false, 0, 0, KingdomAdoptRules.AdoptionVerdict.RefusedUnbounded)]
		[TestCase(true, 3, 1, KingdomAdoptRules.AdoptionVerdict.RefusedTooSmall)]
		[TestCase(true, 3, 0, KingdomAdoptRules.AdoptionVerdict.RefusedTooSmall)]
		[TestCase(true, 20, 0, KingdomAdoptRules.AdoptionVerdict.RefusedNoDoor)]
		[TestCase(true, 4, 1, KingdomAdoptRules.AdoptionVerdict.Adopted)]
		[TestCase(true, 20, 1, KingdomAdoptRules.AdoptionVerdict.Adopted)]
		public void Assess_Work_ClassifiesEnclosure(bool bounded, int roomCells, int doorCells, KingdomAdoptRules.AdoptionVerdict expected)
		{
			KingdomAdoptRules.EnclosureMeasurement enclosure = new KingdomAdoptRules.EnclosureMeasurement { Bounded = bounded, RoomCells = roomCells, DoorCells = doorCells };
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, false, false, false, enclosure);
			Assert.AreEqual(expected, verdict);
		}

		[Test]
		public void Assess_Work_NeverAdoptsWithoutADoorRegardlessOfRoomSize()
		{
			// A mutation that dropped the door check would let a sealed, windowless void pass
			// as a building the moment it was merely big enough.
			for (int roomCells = KingdomAdoptRules.MinEnclosedRoomCells; roomCells <= KingdomAdoptRules.MaxEnclosedRoomCells; roomCells += 11)
			{
				KingdomAdoptRules.EnclosureMeasurement enclosure = new KingdomAdoptRules.EnclosureMeasurement { Bounded = true, RoomCells = roomCells, DoorCells = 0 };
				KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, false, false, false, enclosure);
				Assert.AreNotEqual(KingdomAdoptRules.AdoptionVerdict.Adopted, verdict, "roomCells=" + roomCells);
			}
		}

		[Test]
		public void Assess_Work_NeverAdoptsAnUnboundedFill()
		{
			// A mutation that dropped the Bounded check would let a room that merely blew its
			// own search budget pass anyway, on nothing but a door count and a cell tally that
			// were never proven to belong to a closed room.
			for (int roomCells = 0; roomCells <= KingdomAdoptRules.MaxEnclosedRoomCells * 2; roomCells += 17)
			{
				KingdomAdoptRules.EnclosureMeasurement enclosure = new KingdomAdoptRules.EnclosureMeasurement { Bounded = false, RoomCells = roomCells, DoorCells = 3 };
				KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, false, false, false, enclosure);
				Assert.AreNotEqual(KingdomAdoptRules.AdoptionVerdict.Adopted, verdict, "roomCells=" + roomCells);
			}
		}

		// --- Shared helpers ----------------------------------------------------------------------

		[Test]
		public void IsRefusal_TrueForEveryVerdictExceptAdopted()
		{
			foreach (KingdomAdoptRules.AdoptionVerdict verdict in Enum.GetValues(typeof(KingdomAdoptRules.AdoptionVerdict)))
			{
				bool expected = verdict != KingdomAdoptRules.AdoptionVerdict.Adopted;
				Assert.AreEqual(expected, KingdomAdoptRules.IsRefusal(verdict), verdict.ToString());
			}
		}

		[Test]
		public void EnclosureConstants_AreSaneAndOrdered()
		{
			// A zeroed or inverted pair here would either let a single cell count as a building
			// or make the flood fill's own budget smaller than the minimum it must accept.
			Assert.Greater(KingdomAdoptRules.MinEnclosedRoomCells, 0);
			Assert.Greater(KingdomAdoptRules.MaxEnclosedRoomCells, KingdomAdoptRules.MinEnclosedRoomCells);
		}

		[Test]
		public void AdoptionReceiptBindsAndHashesExactForeignFootprintEvidence()
		{
			ArchitecturePoint[] cells = { new ArchitecturePoint(2, 2),
				new ArchitecturePoint(1, 1), new ArchitecturePoint(2, 1),
				new ArchitecturePoint(1, 2) };
			Assert.IsTrue(KingdomAdoptionDesignationRules.TryCreate("zone.1", "root-1",
				"tent", cells, false, "Hearthpyre", "2.2.3",
				"00000000-0000-0000-0000-000000000001", "abc123",
				out KingdomAdoptionDesignationReceipt receipt, out string failure), failure);
			string encoded = KingdomAdoptionDesignationRules.Encode(receipt);
			Assert.IsTrue(KingdomAdoptionDesignationRules.TryDecode(encoded,
				out KingdomAdoptionDesignationReceipt decoded, out failure), failure);
			Assert.AreEqual("Hearthpyre", decoded.ForeignProviderId);
			Assert.AreEqual("2.2.3", decoded.ForeignProviderVersion);
			Assert.AreEqual("00000000-0000-0000-0000-000000000001",
				decoded.ForeignIdentity);
			Assert.AreEqual("abc123", decoded.ForeignRevision);
			Assert.AreEqual(new ArchitecturePoint(1, 1), decoded.Cells[0]);

			Assert.IsTrue(KingdomAdoptionDesignationRules.TryCreate("zone.1", "root-1",
				"tent", cells, false, "Hearthpyre", "2.2.3",
				decoded.ForeignIdentity, "def456", out KingdomAdoptionDesignationReceipt changed,
				out failure), failure);
			Assert.AreNotEqual(receipt.Revision, changed.Revision);
			Assert.IsFalse(KingdomAdoptionDesignationRules.TryCreate("zone.1", "root-1",
				"tent", cells, false, "Hearthpyre", null, null, null,
				out _, out _), "partial foreign identity must fail closed");
		}

		[Test]
		public void AdoptionPublicationIsPhasedRecoverableAndAuthorityIsLast()
		{
			string transaction = TestMain.ReadRepositoryText(System.IO.Path.Combine(
				"Growth", "KingdomAdopt.Transaction.cs"));
			string work = TestMain.ReadRepositoryText(System.IO.Path.Combine(
				"Growth", "KingdomAdopt.Work.cs"));
			string existing = TestMain.ReadRepositoryText(System.IO.Path.Combine(
				"Growth", "KingdomAdopt.cs"));
			string release = TestMain.ReadRepositoryText(System.IO.Path.Combine(
				"Growth", "KingdomAdopt.Release.cs"));

			Assert.That(transaction.IndexOf("Target.SetIntProperty(BuiltProperty, 1)"),
				Is.LessThan(transaction.IndexOf("Target.SetIntProperty(AdoptedProperty, 1)")));
			StringAssert.Contains("CompleteEvidence(Target, Key)", transaction);
			StringAssert.Contains("KingdomAdoptionDesignation.Clear(Target)", transaction);
			StringAssert.Contains("KingdomPlots.ReleaseAdoptedPlot(Target)", transaction);
			StringAssert.Contains("ZoneActivatedEvent.ID", transaction);
			StringAssert.Contains("ZoneThawedEvent.ID", transaction);
			StringAssert.Contains("if (created && Target.Blueprint == WorkMarkerBlueprint)",
				transaction);
			StringAssert.Contains("ReproveRoomForCommit", work);
			StringAssert.Contains("ContainerIsUnclaimed", existing);
			StringAssert.DoesNotContain("ApplyRoleFixtures", work + existing);
			Assert.That(release.IndexOf("KingdomDesignationReleaseAuthority.TryCanRelease"),
				Is.LessThan(release.IndexOf("KingdomAdoptionDesignation.Clear(Adopted)")));
			Assert.That(release.IndexOf("KingdomAdoptionDesignation.Clear(Adopted)"),
				Is.LessThan(release.IndexOf("ClearTyped(Adopted, AdoptedProperty)")));
		}
	}
}
#endif
