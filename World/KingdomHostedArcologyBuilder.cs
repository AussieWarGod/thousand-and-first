using System;
using ThousandAndFirst;
using XRL.World;
using XRL.World.Parts;

namespace XRL.World.ZoneBuilders
{
	/// <summary>Builds one coordinate of the root-owned 3x3x3 hosted arcology.</summary>
	public sealed class KingdomHostedArcologyBuilder
	{
		public bool BuildZone(Zone Z)
		{
			InteriorZone interior = Z as InteriorZone;
			if (interior == null || interior.Schema != KingdomHostedArcologyTopology.Schema
				|| !KingdomHostedArcologyTopology.InBounds(Z.X, Z.Y, Z.Z)) return false;
			GameObject root = KingdomHostedArcology.RootOf(Z);
			if (!GameObject.Validate(root) || string.IsNullOrEmpty(root.IDIfAssigned)) return false;
			KingdomArcologyCoordinate at = new KingdomArcologyCoordinate(Z.X, Z.Y, Z.Z);
			KingdomArcologyProgramme programme =
				KingdomHostedArcologyTopology.ProgrammeAt(at.X, at.Y, at.Z);
			Z.BaseDisplayName = KingdomHostedArcologyTopology.ProgrammeName(programme);
			Z.NameContext = "the hosted arcology";
			Z.SetZoneProperty("TAFArcologyProgramme", programme.ToString());
			Z.SetZoneProperty("PullDownLocation", "40,20");
			if (!PaintFloor(Z, KingdomHostedArcologyProgrammeBuilder.FloorFor(at.Z))
				|| !BuildShell(Z, at, root.IDIfAssigned)
				|| !KingdomHostedArcologyProgrammeBuilder.Build(
					Z, at, programme, root.IDIfAssigned)
				|| !BuildCirculation(Z, at, root.IDIfAssigned))
			{
				KingdomHostedArcology.Quarantine(root.GetPart<r_KingdomArcology>(),
					"A hosted arcology zone could not realize its exact authored programme.");
				return false;
			}
			return true;
		}

		private static bool PaintFloor(Zone Z, string Blueprint)
		{
			for (int y = 0; y < Z.Height; y++)
				for (int x = 0; x < Z.Width; x++)
				{
					Cell cell = Z.GetCell(x, y);
					int exact = 0;
					foreach (GameObject item in cell.GetObjects())
						if (item.Blueprint == Blueprint) exact++;
					if (exact > 1) return false;
					if (exact == 0 && cell.AddObject(Blueprint) == null) return false;
				}
			return true;
		}

		private static bool BuildShell(Zone Z, KingdomArcologyCoordinate At, string RootId)
		{
			bool north = KingdomHostedArcologyTopology.HasHorizontalNeighbour(
				At.X, At.Y, At.Z, KingdomArcologyDirection.North);
			bool east = KingdomHostedArcologyTopology.HasHorizontalNeighbour(
				At.X, At.Y, At.Z, KingdomArcologyDirection.East);
			bool south = KingdomHostedArcologyTopology.HasHorizontalNeighbour(
				At.X, At.Y, At.Z, KingdomArcologyDirection.South);
			bool west = KingdomHostedArcologyTopology.HasHorizontalNeighbour(
				At.X, At.Y, At.Z, KingdomArcologyDirection.West);
			string horizontal = At.Z == KingdomHostedArcologyTopology.LowerZ
				? "r_KingdomStructureRustedMetalWall" : "r_KingdomStructureConcreteWall";
			string vertical = At.Z == KingdomHostedArcologyTopology.UpperZ
				? "r_KingdomStructureConcreteWall" : "r_KingdomStructureMetalWall";
			for (int x = 0; x < Z.Width; x++)
			{
				if (!north || (x != 39 && x != 40))
					if (AddStable(Z, x, 0, horizontal, RootId,
						Role(At, "shell:north:" + x)) == null) return false;
				if (!south || (x != 39 && x != 40))
					if (AddStable(Z, x, Z.Height - 1, horizontal, RootId,
						Role(At, "shell:south:" + x)) == null) return false;
			}
			for (int y = 1; y < Z.Height - 1; y++)
			{
				if (!west || (y != 11 && y != 12))
					if (AddStable(Z, 0, y, vertical, RootId,
						Role(At, "shell:west:" + y)) == null) return false;
				if (!east || (y != 11 && y != 12))
					if (AddStable(Z, Z.Width - 1, y, vertical, RootId,
						Role(At, "shell:east:" + y)) == null) return false;
			}
			return (!north || AddThresholdPair(Z, At, RootId, "north", 39, 0, 40, 0))
				&& (!east || AddThresholdPair(Z, At, RootId, "east", 79, 11, 79, 12))
				&& (!south || AddThresholdPair(Z, At, RootId, "south", 39, 24, 40, 24))
				&& (!west || AddThresholdPair(Z, At, RootId, "west", 0, 11, 0, 12));
		}

		private static bool AddThresholdPair(Zone Z, KingdomArcologyCoordinate At,
			string RootId, string Edge, int AX, int AY, int BX, int BY)
		{
			return AddStable(Z, AX, AY, "r_KingdomArcologyThreshold", RootId,
				Role(At, "threshold:" + Edge + ":a")) != null
				&& AddStable(Z, BX, BY, "r_KingdomArcologyThreshold", RootId,
					Role(At, "threshold:" + Edge + ":b")) != null;
		}

		private static bool BuildCirculation(Zone Z, KingdomArcologyCoordinate At,
			string RootId)
		{
			if (KingdomHostedArcologyTopology.HasStairsUp(At.Z))
				if (AddStable(Z, KingdomHostedArcologyTopology.StairsUpX(At.Z),
					KingdomHostedArcologyTopology.StairY, "r_KingdomArcologyStairsUp", RootId,
					Role(At, "stairs:up")) == null) return false;
			if (KingdomHostedArcologyTopology.HasStairsDown(At.Z))
				if (AddStable(Z, KingdomHostedArcologyTopology.StairsDownX(At.Z),
					KingdomHostedArcologyTopology.StairY, "r_KingdomArcologyStairsDown", RootId,
					Role(At, "stairs:down")) == null) return false;

			string lotKey = KingdomHostedArcologyTopology.HostedLotAt(At.X, At.Y, At.Z);
			GameObject anchor = AddStable(Z, 40, 3, "r_KingdomArcologyZoneAnchor", RootId,
				Role(At, "anchor"));
			r_KingdomArcologyZoneAnchor part = anchor?.GetPart<r_KingdomArcologyZoneAnchor>();
			if (part == null) return false;
			part.ZoneX = At.X;
			part.ZoneY = At.Y;
			part.ZoneZ = At.Z;
			part.LotKey = lotKey;
			if (KingdomHostedArcologyTopology.IsSurfaceExit(At.X, At.Y, At.Z))
			{
				if (AddStable(Z, 40, 12, "r_KingdomArcologyBasin", RootId,
					Role(At, "court:basin")) == null
					|| AddStable(Z, 40, 22, "r_KingdomArcologyExit", RootId,
						Role(At, "surface:exit")) == null) return false;
			}
			// Paid fixtures are realized by KingdomSystem's ordered activation/thaw guard after
			// current-realm authority is proved. Zone generation owns only the neutral shell.
			return true;
		}

		internal static GameObject AddStable(Zone Z, int X, int Y, string Blueprint,
			string RootId, string Role)
		{
			if (Z == null || string.IsNullOrEmpty(Blueprint) || string.IsNullOrEmpty(RootId)
				|| string.IsNullOrEmpty(Role) || X < 0 || Y < 0 || X >= Z.Width || Y >= Z.Height)
				return null;
			string id = KingdomHostedArcologyRules.StableChildId(RootId, Role);
			if (string.IsNullOrEmpty(id)) return null;
			GameObject exact = null;
			int count = 0;
			foreach (GameObject candidate in Z.GetObjects())
				if (candidate.IDIfAssigned == id) { exact = candidate; count++; }
			Cell cell = Z.GetCell(X, Y);
			if (count == 1) return exact.Blueprint == Blueprint && exact.CurrentCell == cell
				? exact : null;
			if (count != 0 || !cell.IsPassable()) return null;
			try
			{
				GameObject item = GameObject.Create(Blueprint);
				item.ID = id;
				GameObject accepted = cell.AddObject(item, Forced: true, System: true,
					NoStack: true, Silent: true);
				return ReferenceEquals(item, accepted) ? item : null;
			}
			catch { return null; }
		}

		private static string Role(KingdomArcologyCoordinate At, string Value)
		{
			return KingdomHostedArcologyTopology.StableRole(At.X, At.Y, At.Z, Value);
		}
	}
}
