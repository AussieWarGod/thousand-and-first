#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomAdoptRulesTests
	{
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
			Assert.Greater(measurement.RoomCells, KingdomAdoptRules.MaxEnclosedRoomCells);
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

		// --- Assess: Housing --------------------------------------------------------------------

		[TestCase(true, false, true, KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing)]
		[TestCase(false, true, true, KingdomAdoptRules.AdoptionVerdict.RefusedBelowStage)]
		[TestCase(false, false, true, KingdomAdoptRules.AdoptionVerdict.Adopted)]
		[TestCase(false, false, false, KingdomAdoptRules.AdoptionVerdict.RefusedNoBed)]
		public void Assess_Housing_ChecksInProtectiveOrder(bool alreadyServing, bool belowStage, bool hasBed, KingdomAdoptRules.AdoptionVerdict expected)
		{
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Housing, alreadyServing, belowStage, hasBed, false, default);
			Assert.AreEqual(expected, verdict);
		}

		// --- Assess: Storage --------------------------------------------------------------------

		[TestCase(true, false, true, KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing)]
		[TestCase(false, true, true, KingdomAdoptRules.AdoptionVerdict.RefusedBelowStage)]
		[TestCase(false, false, true, KingdomAdoptRules.AdoptionVerdict.Adopted)]
		[TestCase(false, false, false, KingdomAdoptRules.AdoptionVerdict.RefusedNotStorageCapable)]
		public void Assess_Storage_ChecksInProtectiveOrder(bool alreadyServing, bool belowStage, bool isStorageCapable, KingdomAdoptRules.AdoptionVerdict expected)
		{
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Storage, alreadyServing, belowStage, false, isStorageCapable, default);
			Assert.AreEqual(expected, verdict);
		}

		// --- Assess: Work (civic, faith, craft, knowledge, power, defense, and unknowns) --------

		[Test]
		public void Assess_Work_AlreadyServingBeatsAPerfectRoom()
		{
			KingdomAdoptRules.EnclosureMeasurement enclosure = new KingdomAdoptRules.EnclosureMeasurement { Bounded = true, RoomCells = 20, DoorCells = 2 };
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, true, false, false, false, enclosure);
			Assert.AreEqual(KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing, verdict);
		}

		[Test]
		public void Assess_Work_BelowStageBeatsAPerfectRoom()
		{
			KingdomAdoptRules.EnclosureMeasurement enclosure = new KingdomAdoptRules.EnclosureMeasurement { Bounded = true, RoomCells = 20, DoorCells = 2 };
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, false, true, false, false, enclosure);
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
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, false, false, false, false, enclosure);
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
				KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, false, false, false, false, enclosure);
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
				KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(KingdomAdoptRules.RoleKind.Work, false, false, false, false, enclosure);
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
	}
}
#endif
