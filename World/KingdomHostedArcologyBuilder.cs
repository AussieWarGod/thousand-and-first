using System;
using ThousandAndFirst;
using XRL.World;
using XRL.World.Parts;

namespace XRL.World.ZoneBuilders
{
	/// <summary>Vanilla Interior-zone shell for the atrium and its two hosted floors.</summary>
	public sealed class KingdomHostedArcologyBuilder
	{
		public string Kind;

		public bool BuildZone(Zone Z)
		{
			GameObject root = KingdomHostedArcology.RootOf(Z);
			if (!GameObject.Validate(root) || string.IsNullOrEmpty(root.IDIfAssigned)
				|| (Kind != "atrium" && Kind != "ward"
				&& Kind != "terrace")) return false;
			Z.SetZoneProperty("PullDownLocation", "40,22");
			PaintFloor(Z, Kind == "atrium" ? "MarbleFloor" : "SmallHexFloor");
			Border(Z);
			if (Kind == "atrium") BuildAtrium(Z, root);
			else BuildHostedFloor(Z, root, Kind == "ward" ? "arcologyward" : "arcologyterrace");
			return true;
		}

		private static void PaintFloor(Zone Z, string Blueprint)
		{
			for (int y = 0; y < Z.Height; y++) for (int x = 0; x < Z.Width; x++)
				Z.GetCell(x, y).AddObject(Blueprint);
		}

		private static void Border(Zone Z)
		{
			for (int x = 0; x < Z.Width; x++)
			{
				Z.GetCell(x, 0).AddObject("r_KingdomStructureConcreteWall");
				if (x != 40) Z.GetCell(x, Z.Height - 1).AddObject("r_KingdomStructureConcreteWall");
			}
			for (int y = 1; y < Z.Height - 1; y++)
			{
				Z.GetCell(0, y).AddObject("r_KingdomStructureMetalWall");
				Z.GetCell(Z.Width - 1, y).AddObject("r_KingdomStructureMetalWall");
			}
			Z.GetCell(40, Z.Height - 1).AddObject("r_KingdomFixtureDoorMetal");
		}

		private static void BuildAtrium(Zone Z, GameObject Root)
		{
			for (int x = 8; x <= 71; x += 7)
			{
				Z.GetCell(x, 7).AddObject("r_KingdomStructureLowConcrete");
				Z.GetCell(x, 17).AddObject("r_KingdomStructureLowConcrete");
			}
			AddStable(Z, 40, 12, "r_KingdomArcologyBasin", Root.IDIfAssigned, "atrium:basin");
			AddStable(Z, 40, 23, "r_KingdomArcologyExit", Root.IDIfAssigned, "atrium:exit");
			GameObject ward = AddStable(Z, 15, 12, "r_KingdomArcologyWardLift",
				Root.IDIfAssigned, "lift:arcologyward");
			GameObject terrace = AddStable(Z, 64, 12, "r_KingdomArcologyTerraceLift",
				Root.IDIfAssigned, "lift:arcologyterrace");
			ward?.SetStringProperty("r_TAF_ArcologyRootId", Root.IDIfAssigned);
			terrace?.SetStringProperty("r_TAF_ArcologyRootId", Root.IDIfAssigned);
			for (int x = 28; x <= 52; x += 8)
			{
				AddStable(Z, x, 9, "r_KingdomFixtureBenchTimber", Root.IDIfAssigned, "atrium:bench:n:" + x);
				AddStable(Z, x, 15, "r_KingdomFixtureBenchTimber", Root.IDIfAssigned, "atrium:bench:s:" + x);
			}
		}

		private static void BuildHostedFloor(Zone Z, GameObject Root, string LotKey)
		{
			for (int y = 3; y <= 21; y++)
			{
				if (y != 12)
				{
					Z.GetCell(4, y).AddObject("r_KingdomStructureLowMetalScreen");
					Z.GetCell(75, y).AddObject("r_KingdomStructureLowMetalScreen");
				}
			}
			if (LotKey == "arcologyward") WardPartitions(Z); else TerracePartitions(Z);
			GameObject anchor = AddStable(Z, 40, 22, "r_KingdomArcologyZoneAnchor",
				Root.IDIfAssigned, LotKey + ":anchor");
			if (anchor != null) anchor.GetPart<r_KingdomArcologyZoneAnchor>().LotKey = LotKey;
			AddStable(Z, 40, 23, "r_KingdomArcologyExit", Root.IDIfAssigned, LotKey + ":exit");
			KingdomHostedArcologyVisual.Reconcile(Z, LotKey);
		}

		private static void WardPartitions(Zone Z)
		{
			int[] xs = new int[] { 12, 29, 50, 67 };
			for (int i = 0; i < xs.Length; i++) for (int y = 3; y <= 20; y++)
				if (y != 7 && y != 12 && y != 17) Z.GetCell(xs[i], y).AddObject("r_KingdomStructureConcreteWall");
		}

		private static void TerracePartitions(Zone Z)
		{
			for (int x = 5; x <= 74; x++) if (x < 36 || x > 44)
			{
				Z.GetCell(x, 10).AddObject("r_KingdomStructureLowMetalScreen");
				Z.GetCell(x, 14).AddObject("r_KingdomStructureLowMetalScreen");
			}
		}

		private static GameObject AddStable(Zone Z, int X, int Y, string Blueprint,
			string RootId, string Role)
		{
			GameObject item = GameObject.Create(Blueprint);
			item.ID = KingdomHostedArcologyRules.StableChildId(RootId, Role);
			return Z.GetCell(X, Y).AddObject(item, Forced: true, System: true,
				NoStack: true, Silent: true);
		}
	}
}
