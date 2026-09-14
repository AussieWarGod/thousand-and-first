using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal sealed partial class KingdomLodgingRoomNativeFixture
	{
		internal readonly XRLGame Game;
		internal readonly Zone Zone;
		internal readonly KingdomSystem System;
		private readonly List<GameObject> Owned = new List<GameObject>();
		private readonly List<GameObject> Beds = new List<GameObject>();
		private readonly List<GameObject> Blockers = new List<GameObject>();
		private readonly List<string> Passed = new List<string>();
		private readonly long Began;
		private KingdomPlotRules.PlotRect Rect;
		private GameObject Root, DoorObject, Wall, Newcomer;
		private Door Door;
		private string RootId;

		internal KingdomLodgingRoomNativeFixture(XRLGame Game, Zone Zone, KingdomSystem System)
		{
			this.Game = Game; this.Zone = Zone; this.System = System; Began = Game.TimeTicks;
		}

		internal string Run()
		{
			Build();
			Check("shared-capped", KingdomLodgingRules.Closeness.Close, 3, 20, true);
			Detach(Beds[1]); Detach(Beds[2]);
			Check("private-room", KingdomLodgingRules.Closeness.Private, 1, 22, true);
			Door.PerformOpen(); Require(Door.Open && !DoorObject.ConsiderSolid(), "native door failed to open");
			Check("open-door", KingdomLodgingRules.Closeness.Private, 1, 22, true);
			Require(Door.AttemptClose(Silent: true) && !Door.Open, "native door failed to close");
			Check("closed-door", KingdomLodgingRules.Closeness.Private, 1, 22, true);
			Door.Locked = true;
			Check("locked-door", KingdomLodgingRules.Closeness.Packed, 0, 0, false);
			Door.Locked = false;
			Check("unlocked-door", KingdomLodgingRules.Closeness.Private, 1, 22, true);
			CheckFurnitureAccess();
			for (int y = 1; y <= 4; y++) for (int x = 3; x <= 6; x++)
			{
				if (x == 6 && y == 1) continue;
				GameObject chest = Create("Chest");
				Require(chest.Inventory != null && chest.Inventory.Objects.Count == 0 && !chest.ConsiderSolid(),
					"room obstruction is not an empty walkable native chest");
				Place(chest, At(x, y)); Blockers.Add(chest);
			}
			Check("furnished-floor", KingdomLodgingRules.Closeness.Roomed, 1, 7, true);
			foreach (GameObject cabinet in Blockers) Detach(cabinet);
			Check("floor-restored", KingdomLodgingRules.Closeness.Private, 1, 22, true);
			Cell wallCell = Wall.CurrentCell; Detach(Wall);
			Check("wall-loss", KingdomLodgingRules.Closeness.Packed, 0, 0, false);
			Place(Wall, wallCell);
			Check("wall-restored", KingdomLodgingRules.Closeness.Private, 1, 22, true);
			Cell bedCell = Beds[0].CurrentCell; Detach(Beds[0]);
			Check("bed-loss", KingdomLodgingRules.Closeness.Packed, 0, 0, false);
			Place(Beds[0], bedCell);
			Check("bed-restored", KingdomLodgingRules.Closeness.Private, 1, 22, true);
			CheckOccupied();
			Place(Beds[1], At(3, 1)); Place(Beds[2], At(5, 1));
			Check("bunks-restored", KingdomLodgingRules.Closeness.Close, 3, 20, true);
			CheckSharedHall();
			Require(Passed.Count == 28, "room scenario omitted a required case");
			return "native-lodging-room cases=28 passed=28 failed=0; real-quickstart=true; synthetic-room=true; "
				+ "roof-credit=1; usable-bunks=3; same-root=true; no-cold-load-claim=true";
		}

		private void Build()
		{
			RequireWorld();
			bool found = false;
			for (int y = 2; y <= Zone.Height - 8 && !found; y++)
				for (int x = 54; x <= Zone.Width - 10 && !found; x++)
				{
					var candidate = new KingdomPlotRules.PlotRect(x, y, x + 7, y + 5);
					if (!Vacant(candidate)) continue;
					Rect = candidate; found = true;
				}
			Require(found, "no untouched 8x6 room with clear apron east of the camp");
			for (int y = 0; y < 6; y++) for (int x = 0; x < 8; x++)
			{
				if (x != 0 && x != 7 && y != 0 && y != 5) continue;
				GameObject item = Create(x == 1 && y == 5 ? "r_KingdomFixtureDoorTimber" : "r_KingdomStructureCanvasWall");
				Place(item, At(x, y));
				if (x == 1 && y == 5) { DoorObject = item; Door = item.GetPart<Door>(); }
				if (x == 0 && y == 2) Wall = item;
			}
			Require(Door != null && !Door.Locked && Wall.IsWall(), "native canvas or door shape changed");
			for (int x = 1; x <= 5; x += 2)
			{
				GameObject bed = Create("r_KingdomFixtureBedrollCanvas");
				Require(bed.HasPart("Bed"), "sleep fixture has no native Bed part");
				Place(bed, At(x, 1)); Beds.Add(bed);
			}
			Require(KingdomAdopt.AdoptWork(System, Zone, At(6, 1), "tent", out string failure),
				"ordinary room adoption refused: " + failure);
			foreach (GameObject item in At(6, 1).Objects)
				if (item.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1)
				{
					Require(Root == null, "multiple adoption roots"); Root = item;
				}
			Require(Root != null && !string.IsNullOrEmpty(Root.IDIfAssigned), "adoption root absent");
			RootId = Root.IDIfAssigned;
			Newcomer = Create("NPC");
			Require(Newcomer.IsAlive && Newcomer.IsCreature && Newcomer.CurrentCell == null,
				"arrival probe is not a live unplaced NPC");
		}

		private bool Vacant(KingdomPlotRules.PlotRect Candidate)
		{
			for (int y = Candidate.Y1 - 1; y <= Candidate.Y2 + 1; y++)
				for (int x = Candidate.X1 - 1; x <= Candidate.X2 + 1; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || cell.HasOpenLiquidVolume() || !cell.IsPassable()) return false;
					foreach (GameObject item in cell.Objects)
						if (!GameObject.Validate(item) || item.IsCreature
							|| KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare) return false;
				}
			return true;
		}

		private Cell At(int X, int Y) => Zone.GetCell(Rect.X1 + X, Rect.Y1 + Y);
		private GameObject Create(string Blueprint)
		{
			RequireWorld(); GameObject item = GameObject.Create(Blueprint);
			Require(GameObject.Validate(item) && !string.IsNullOrEmpty(item.ID), "fixture creation lacks identity");
			Owned.Add(item); return item;
		}
		private void Place(GameObject Item, Cell Cell)
		{
			RequireWorld();
			Require(GameObject.Validate(Item) && Item.CurrentCell == null && Item.InInventory == null
				&& Item.Equipped == null && Cell?.ParentZone == Zone, "placement custody differs");
			string id = Item.IDIfAssigned;
			Require(ReferenceEquals(Cell.AddObject(Item, NoStack: true), Item)
				&& Item.CurrentCell == Cell && Item.IDIfAssigned == id, "fixture placement substituted identity");
		}
		private void Detach(GameObject Item)
		{
			RequireWorld(); string id = Item.IDIfAssigned;
			Require(Owned.Contains(Item) && Item.CurrentZone == Zone, "cannot detach an unowned fixture");
			Item.CurrentCell.RemoveObject(Item);
			Require(GameObject.Validate(Item) && Item.CurrentCell == null && Item.IDIfAssigned == id,
				"fixture detachment changed identity");
		}
		private void RequireWorld()
		{
			Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(The.Player?.CurrentZone, Zone)
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && Game.TimeTicks == Began
				&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && !KingdomSurvey.HasBoundPass,
				"room fixture world, clock or pass ownership changed");
		}
		private static void Require(bool Value, string Failure) => KingdomLodgingRoomNativeProvider.Require(Value, Failure);
	}
}
