using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal sealed partial class KingdomLodgingRoomNativeFixture
	{
		private void CheckSharedHall()
		{
			bool priorOpen = Door.Open, priorLocked = Door.Locked, priorWasLocked = Door.WasLocked;
			Detach(Beds[1]); Detach(Beds[2]);
			var partition = new List<GameObject>();
			for (int x = 1; x <= 6; x++)
			{
				GameObject item = Create(x == 5 ? "r_KingdomFixtureDoorTimber" : "r_KingdomStructureCanvasWall");
				Place(item, At(x, 3)); partition.Add(item);
			}
			GameObject chair = Create("Chair");
			Require(chair.HasPart("Chair") && !chair.ConsiderSolid(), "hall obstruction must be a walkable chair");
			GameObject outsideWall = null;
			foreach (GameObject item in At(5, 5).Objects)
				if (Owned.Contains(item) && item.IsWall()) outsideWall = item;
			Require(outsideWall != null, "alternate doorway lacks owned wall");
			GameObject alternate = Create("r_KingdomFixtureDoorTimber");
			GameObject chest = Create("Chest");
			Require(!chest.ConsiderSolid(), "alternate doorway obstruction must be walkable storage");
			try
			{
				HallReading("hall-connected", 10, 0);
				Place(chair, At(3, 4));
				HallReading("hall-furniture-blocked", 0, 1);
				Detach(outsideWall); Place(alternate, At(5, 5));
				HallReading("hall-alternate-exit", 10, 0);
				Place(chest, At(5, 5));
				HallReading("hall-alternate-obstructed", 0, 1);
				Detach(chest); Detach(alternate); Place(outsideWall, At(5, 5)); Detach(chair);
				HallReading("hall-route-restored", 10, 0);
				Require(Door.Open && !DoorObject.ConsiderSolid(), "earlier furniture entry did not leave the door open");
				Door.Locked = true;
				HallReading("hall-open-locked", 10, 0);
				Require(Door.AttemptClose(Silent: true) && !Door.Open && DoorObject.ConsiderSolid(),
					"locked entrance was not physically closed");
				Door.Lock();
				Require(Door.Locked && !Door.Open && KingdomBenefitIndex.ReadFurnishedRoomCell(
					Zone, Rect.X1 + 1, Rect.Y1 + 5).Region == KingdomAdoptRules.EnclosureRegion.Shell,
					"closed entrance did not retain its lock and block production ingress");
				HallReading("hall-exterior-locked", 0, 1);
				Door.Unlock();
				HallReading("hall-exterior-unlocked", 10, 0);
			}
			finally
			{
				Door.Locked = false;
				foreach (GameObject item in partition) if (item.CurrentCell != null) Detach(item);
				if (chest.CurrentCell != null) Detach(chest);
				if (chair.CurrentCell != null) Detach(chair);
				if (alternate.CurrentCell != null) Detach(alternate);
				if (outsideWall.CurrentCell == null) Place(outsideWall, At(5, 5));
				Place(Beds[1], At(3, 1)); Place(Beds[2], At(5, 1));
				if (priorOpen) Door.PerformOpen();
				else Require(Door.AttemptClose(Silent: true), "original closed door state was not restored");
				Door.Locked = priorLocked; Door.WasLocked = priorWasLocked;
			}
			Check("hall-partitions-restored", KingdomLodgingRules.Closeness.Close, 3, 20, true);
		}

		private void HallReading(string Name, int Floor, int Unusable)
		{
			bool ok = false;
			string detail = "case=" + Name + "; synthetic-building-designation=true; native-cells=true";
			try
			{
				RequireWorld();
				var designation = new List<KingdomBenefitCell>();
				for (int y = Rect.Y1; y <= Rect.Y2; y++)
					for (int x = Rect.X1; x <= Rect.X2; x++)
						designation.Add(new KingdomBenefitCell(x, y, KingdomBenefitCellUse.Plot));
				Require(Beds[0].CurrentCell == At(1, 1) && Beds[0].HasPart("Bed"), "native sleep place moved");
				var beds = new[] { new KingdomLodgingRoomRules.SleepingPlace(Rect.X1 + 1, Rect.Y1 + 1, 1) };
				var reading = KingdomLodgingRoomRules.Measure(designation, beds,
					(x, y) => KingdomBenefitIndex.ReadFurnishedRoomCell(Zone, x, y));
				detail += "; floor=" + reading.UsableFloorCells + "; unusable=" + reading.UnusablePlaces
					+ "; rooms=" + reading.SleepingRooms + "; quarters=" + reading.Quarters;
				Require(reading.SleepingPlaces == 1 && reading.SleepingRooms == 1
					&& reading.ExposedPlaces == 0 && reading.UnusablePlaces == Unusable
					&& reading.UsableFloorCells == Floor && reading.Quarters == (Unusable == 0
						? KingdomLodgingRules.Closeness.Private : KingdomLodgingRules.Closeness.Packed), detail);
				RequireWorld(); Passed.Add(Name); ok = true;
			}
			finally { KingdomScenarioJournal.Append("room-" + Name, ok, detail); }
		}
	}
}
