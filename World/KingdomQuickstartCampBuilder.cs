using System;
using System.Collections.Generic;
using ThousandAndFirst;
using XRL;
using XRL.World;

namespace XRL.World.ZoneBuilders
{
	/// <summary>
	/// Makes only the founding apron, supply path, and the one reserved shelter lot usable. The
	/// rest of the wilderness zone is untouched; owned things, loose items, pools, and creatures
	/// are moved rather than deleted.
	/// </summary>
	public sealed class KingdomQuickstartCampBuilder
	{
		private sealed class Move
		{
			public GameObject Object;
			public Cell Target;
		}

		public bool BuildZone(Zone Z)
		{
			try
			{
				KingdomQuickstartProfile profile;
				if (!ExactGround(Z, out profile)) return false;
				List<Move> moves = new List<Move>();
				List<GameObject> clear = new List<GameObject>();
				HashSet<Cell> destinations = new HashSet<Cell>();

				for (int y = 1; y < Z.Height - 1; y++)
					for (int x = 1; x < Z.Width - 1; x++)
					{
						if (!Required(x, y)) continue;
						Cell source = Z.GetCell(x, y);
						if (source == null) return false;
						List<GameObject> objects = new List<GameObject>(source.GetObjects());
						for (int i = 0; i < objects.Count; i++)
						{
							GameObject item = objects[i];
							if (!GameObject.Validate(item)) continue;
							if (item.HasPart("StairsUp") || item.HasPart("StairsDown")) return false;
							KingdomPlotRules.GroundKind kind = KingdomPlots.ReadObject(item);
							if (!item.IsCreature && kind == KingdomPlotRules.GroundKind.Bare)
								continue;
							if (item.IsCreature || KingdomPlotRules.Refuses(kind))
							{
								Cell target = FindDestination(Z, item, destinations);
								if (target == null) return false;
								destinations.Add(target);
								moves.Add(new Move { Object = item, Target = target });
							}
							else clear.Add(item);
						}
					}

				for (int i = 0; i < moves.Count; i++)
				{
					Move move = moves[i];
					if (!GameObject.Validate(move.Object) || move.Target == null
						|| !move.Object.SystemLongDistanceMoveTo(move.Target, 0,
							forced: true, ignoreCombat: true)
						|| move.Object.CurrentCell != move.Target) return false;
				}
				for (int i = 0; i < clear.Count; i++)
				{
					GameObject item = clear[i];
					if (!GameObject.Validate(item)) continue;
					bool removed = item.Obliterate(null, Silent: true);
					if (!removed && GameObject.Validate(item)) return false;
				}
				return Ready(Z);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst quickstart camp preparation", ex);
				return false;
			}
		}

		/// <summary>Measures the post-builder result again immediately before any grant.</summary>
		public static bool Ready(Zone Z)
		{
			KingdomQuickstartProfile profile;
			if (!ExactGround(Z, out profile)) return false;
			for (int y = 1; y < Z.Height - 1; y++)
				for (int x = 1; x < Z.Width - 1; x++)
				{
					if (!Required(x, y)) continue;
					Cell cell = Z.GetCell(x, y);
					if (cell == null || !cell.IsPassable() || cell.HasOpenLiquidVolume())
						return false;
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (!GameObject.Validate(item)) continue;
						if (item.IsCreature || item.HasPart("StairsUp") || item.HasPart("StairsDown")
							|| KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare)
							return false;
					}
				}
			return true;
		}

		/// <summary>Checks prepared ground with only the exact placed founder exempted from occupancy.</summary>
		internal static bool ReadyForFounder(Zone Z, GameObject Founder)
		{
			return ReadyForFounder(Z, Founder, out _);
		}

		internal static bool ReadyForFounder(Zone Z, GameObject Founder, out string Failure)
		{
			Failure = "";
			const int maximumCells = 4096, maximumObjects = 65536;
			XRLGame game = The.Game;
			ZoneManager manager = The.ZoneManager;
			if (Z == null || Z.Width <= 45 || Z.Height <= 18
				|| (long)Z.Width * Z.Height > maximumCells)
				return RefuseReadiness("zone dimensions exceed the founding bounds", null, null, out Failure);
			int width = Z.Width, height = Z.Height;
			Cell start = Z.GetCell(KingdomQuickstartRules.StartCellX, KingdomQuickstartRules.StartCellY);
			if (!ExactFounder(game, manager, Z, start, Founder))
				return RefuseReadiness("placed founder ownership differs", start, Founder, out Failure);
			if (!ExactGround(Z, out KingdomQuickstartProfile profile))
				return RefuseReadiness("reserved profile differs", null, null, out Failure);
			int references = 0, visited = 0;
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null || !ReferenceEquals(cell.ParentZone, Z)
						|| cell.Objects.Count > maximumObjects - visited)
						return RefuseReadiness("cell ownership or object bound differs", cell, null, out Failure);
					List<GameObject> objects = cell.GetObjects();
					if (objects.Count > maximumObjects - visited)
						return RefuseReadiness("object snapshot exceeds the bound", cell, null, out Failure);
					visited += objects.Count;
					bool required = Required(x, y);
					if (required && !ReferenceEquals(cell, start))
					{
						if (cell.HasOpenLiquidVolume())
							return RefuseReadiness("open liquid remains", cell, null, out Failure);
						if (!cell.IsPassable())
							return RefuseReadiness("cell is impassable", cell, null, out Failure);
					}
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (ReferenceEquals(item, Founder))
						{
							if (!ReferenceEquals(cell, start) || ++references != 1)
								return RefuseReadiness("founder reference is misplaced or duplicated", cell, item, out Failure);
							continue;
						}
						if (!required) continue;
						if (!GameObject.Validate(item))
							return RefuseReadiness("object fails engine validation", cell, item, out Failure);
						if (item.IsCreature || item.HasPart("StairsUp") || item.HasPart("StairsDown")
							|| KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare)
							return RefuseReadiness("creature, stairs or non-bare ground remains", cell, item, out Failure);
						// Cell.IsPassable(Founder) still treats a solid founder as its own obstruction.
						if (ReferenceEquals(cell, start)
							&& (item.IsCombatObject() || item.ConsiderSolid() || item.IsOpenLiquidVolume()))
							return RefuseReadiness("founder cell contains another obstruction", cell, item, out Failure);
					}
				}
			if (references == 1 && Z.Width == width && Z.Height == height
				&& ExactFounder(game, manager, Z, start, Founder)
				&& ExactGround(Z, out KingdomQuickstartProfile current) && current.Key == profile.Key) return true;
			return RefuseReadiness("final founder or profile proof changed", start, Founder, out Failure);
		}

		private static bool RefuseReadiness(string Reason, Cell Cell, GameObject Object, out string Failure)
		{
			string blueprint = Object?.Blueprint ?? "none";
			if (blueprint.Length > 96) blueprint = blueprint.Substring(0, 96);
			blueprint = blueprint.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
			Failure = Reason + (Cell == null ? "" : " at (" + Cell.X + "," + Cell.Y + ")")
				+ "; blueprint=" + blueprint + "; physics=" + (Object?.Physics != null)
				+ "; graveyard=" + (Object != null && Object.IsInGraveyard());
			return false;
		}

		private static bool ExactFounder(XRLGame Game, ZoneManager Manager, Zone Z,
			Cell Start, GameObject Founder)
		{
			return Game != null && ReferenceEquals(The.Game, Game) && Manager != null
				&& ReferenceEquals(The.ZoneManager, Manager) && ReferenceEquals(Game.ZoneManager, Manager)
				&& ReferenceEquals(Manager.ActiveZone, Z) && GameObject.Validate(Founder)
				&& ReferenceEquals(The.Player, Founder) && Start != null
				&& ReferenceEquals(Start.ParentZone, Z) && ReferenceEquals(Founder.CurrentCell, Start)
				&& Start.X == KingdomQuickstartRules.StartCellX && Start.Y == KingdomQuickstartRules.StartCellY;
		}

		/// <summary>Small heart apron, one supply column, a three-cell-wide approach, the two heart
		/// ingress endpoints, and the reserved shelter lot.</summary>
		internal static bool Required(int X, int Y)
		{
			return KingdomQuickstartRules.RequiresPreparedGround(X, Y);
		}

		private static bool ExactGround(Zone Z, out KingdomQuickstartProfile Profile)
		{
			Profile = null;
			XRLGame game = The.Game;
			return Z != null && game != null && KingdomQuickstartRules.IsMode(game.gameMode)
				&& game.GetBooleanGameState("r_TAF_KingdomMode")
				&& KingdomQuickstartRules.TryProfile(game.GetStringGameState(
					KingdomQuickstartRules.ProfileState, null), out Profile)
				&& string.Equals(Z.ZoneID, Profile.ZoneId, StringComparison.Ordinal)
				&& KingdomQuickstartRules.WorldReservationMatches(game.GetStringGameState(
					KingdomQuickstartRules.WorldReservationState, null), Profile);
		}

		private static Cell FindDestination(Zone Z, GameObject Object,
			HashSet<Cell> Destinations)
		{
			for (int y = Z.Height - 2; y >= 1; y--)
				for (int x = Z.Width - 2; x >= 1; x--)
				{
					if (Required(x, y)) continue;
					Cell candidate = Z.GetCell(x, y);
					if (candidate == null || Destinations.Contains(candidate)
						|| candidate.HasOpenLiquidVolume()
						|| !candidate.IsPassable(Object)) continue;
					bool occupied = false;
					List<GameObject> objects = candidate.GetObjects();
					for (int i = 0; i < objects.Count; i++)
						if (GameObject.Validate(objects[i]) && objects[i].IsCreature)
						{
							occupied = true;
							break;
						}
					if (!occupied) return candidate;
				}
			return null;
		}
	}
}
