using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomLodgingRoomTests
	{
		private static KingdomLodgingRoomRules.Reading Read(string[] Rows,
			Action<int, int> Observed = null, int Capacity = 1, bool FloorOnly = false)
		{
			var scope = new List<KingdomBenefitCell>();
			var beds = new List<KingdomLodgingRoomRules.SleepingPlace>();
			for (int y = 0; y < Rows.Length; y++)
				for (int x = 0; x < Rows[y].Length; x++)
				{
					if (Rows[y][x] != '.' && (!FloorOnly || Rows[y][x] != '#' && Rows[y][x] != 'd'))
						scope.Add(new KingdomBenefitCell(x, y,
						KingdomBenefitCellUse.Plot));
					if (Rows[y][x] == 'b') beds.Add(new KingdomLodgingRoomRules.SleepingPlace(x, y, Capacity));
				}
			return KingdomLodgingRoomRules.Measure(scope, beds, (x, y) => {
				Observed?.Invoke(x, y);
				if (y < 0 || y >= Rows.Length || x < 0 || x >= Rows[y].Length)
					return new KingdomAdoptRules.CellObservation(KingdomAdoptRules.EnclosureRegion.Outside);
				char cell = Rows[y][x];
				return new KingdomAdoptRules.CellObservation(cell == '#' || cell == 'l'
					? KingdomAdoptRules.EnclosureRegion.Shell : cell == 'd'
					? KingdomAdoptRules.EnclosureRegion.Ingress
					: KingdomAdoptRules.EnclosureRegion.Membership, cell != 'x');
			});
		}

		private static string[] Shared() => new[] {
			"########", "#bibibi#", "#iiiiii#", "#iiiiii#", "#iiiiii#", "###d####" };

		[Test]
		public void SpaciousSharedRoomDoesNotBecomePrivate()
		{
			var reading = Read(Shared());
			ClassicAssert.AreEqual(1, reading.SleepingRooms);
			ClassicAssert.AreEqual(3, reading.SleepingPlaces);
			ClassicAssert.AreEqual(24, reading.UsableFloorCells);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Close, reading.Quarters);
		}

		[Test]
		public void AdoptedFloorUsesAdjacentWallsWithoutClaimingTheirGround()
		{
			var reading = Read(Shared(), FloorOnly: true);
			ClassicAssert.AreEqual(1, reading.SleepingRooms);
			ClassicAssert.AreEqual(24, reading.UsableFloorCells);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Close, reading.Quarters);
		}

		[TestCase('#', 1, 0, 3)]
		[TestCase('l', 1, 0, 3)]
		[TestCase('i', 0, 3, 0)]
		public void MissingDoorAndLockedDoorHaveDifferentPhysicalConsequences(
			char Replacement, int Rooms, int Exposed, int Unusable)
		{
			string[] rows = Shared(); rows[5] = rows[5].Replace('d', Replacement);
			var reading = Read(rows);
			ClassicAssert.AreEqual(Rooms, reading.SleepingRooms);
			ClassicAssert.AreEqual(Exposed, reading.ExposedPlaces);
			ClassicAssert.AreEqual(Unusable, reading.UnusablePlaces);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Packed, reading.Quarters);
		}

		[Test]
		public void SeparateFurnishedRoomsEarnPrivacyAndRemovingPartitionLosesIt()
		{
			string[] rows = { "#############", "#biiii#iiiib#", "#iiixi#ixiii#",
				"#iiiii#iiiii#", "#iiiii#iiiii#", "###d#####d###" };
			var reading = Read(rows);
			ClassicAssert.AreEqual(2, reading.SleepingRooms);
			ClassicAssert.AreEqual(38, reading.UsableFloorCells);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Private, reading.Quarters);
			rows[3] = "#iiiiiiiiiii#";
			reading = Read(rows);
			ClassicAssert.AreEqual(1, reading.SleepingRooms);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Close, reading.Quarters);
		}

		[Test]
		public void PairedBedroomsAreRoomedButNotPrivate()
		{
			var reading = Read(new[] { "#############", "#biiib#biiib#", "#iiiii#iiiii#",
				"#iiiii#iiiii#", "#iiiii#iiiii#", "###d#####d###" });
			ClassicAssert.AreEqual(2, reading.SleepingRooms);
			ClassicAssert.AreEqual(4, reading.SleepingPlaces);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Roomed, reading.Quarters);
		}

		[Test]
		public void SolidFurnitureReducesUsableSpaceWithoutInventingPartitions()
		{
			string[] rows = Shared(); rows[3] = "#xxxxxx#";
			var reading = Read(rows);
			ClassicAssert.AreEqual(1, reading.SleepingRooms);
			ClassicAssert.AreEqual(6, reading.UsableFloorCells);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Packed, reading.Quarters);
		}

		[Test]
		public void SameRoomIsObservedOnceAndRotationPreservesReading()
		{
			string[] rows = Shared();
			for (int rotation = 0; rotation < 4; rotation++)
			{
				var seen = new HashSet<string>();
				var reading = Read(rows, (x, y) => ClassicAssert.IsTrue(seen.Add(x + ":" + y)));
				ClassicAssert.AreEqual(1, reading.SleepingRooms);
				ClassicAssert.AreEqual(24, reading.UsableFloorCells);
				ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Close, reading.Quarters);
				var turned = new string[rows[0].Length];
				for (int y = 0; y < turned.Length; y++)
				{
					char[] line = new char[rows.Length];
					for (int x = 0; x < line.Length; x++) line[x] = rows[rows.Length - x - 1][y];
					turned[y] = new string(line);
				}
				rows = turned;
			}
		}

		[Test]
		public void YardAndUndesignatedCellsCannotBecomeBedroomArea()
		{
			string[] rows = Shared(); rows[2] = "#ii.iii#";
			var reading = Read(rows);
			ClassicAssert.AreEqual(3, reading.ExposedPlaces);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Packed, reading.Quarters);
		}

		[TestCase(0)]
		[TestCase(-1)]
		[TestCase(int.MaxValue)]
		public void InvalidOrOverflowingCapacityCannotGrantPrivacy(int Capacity)
		{
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Packed,
				Read(Shared(), Capacity: Capacity).Quarters);
		}

		[Test]
		public void LargeDesignatedHallIsSharedRatherThanFalselyExposedByAdoptionLimit()
		{
			var rows = new string[18];
			rows[0] = new string('#', 20); rows[17] = "#########d##########";
			for (int y = 1; y < 17; y++) rows[y] = "#" + new string('i', 18) + "#";
			rows[1] = "#bibib" + new string('i', 13) + "#";
			var reading = Read(rows);
			ClassicAssert.AreEqual(288, reading.UsableFloorCells);
			ClassicAssert.AreEqual(0, reading.ExposedPlaces);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Close, reading.Quarters);
		}

		[Test]
		public void MultiplePlacesOnOneProviderStillShareTheRoom()
		{
			var reading = Read(new[] { "########", "#biiiii#", "#iiiiii#",
				"#iiiiii#", "#iiiiii#", "###d####" }, Capacity: 3);
			ClassicAssert.AreEqual(3, reading.SleepingPlaces);
			ClassicAssert.AreEqual(KingdomLodgingRules.Closeness.Close, reading.Quarters);
		}
	}
}
