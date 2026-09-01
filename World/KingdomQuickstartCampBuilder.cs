using System;
using System.Collections.Generic;
using ThousandAndFirst;
using XRL;
using XRL.World;

namespace XRL.World.ZoneBuilders
{
	/// <summary>
	/// Makes only the founding apron and supply path usable. The rest of the wilderness zone is
	/// untouched; owned things, loose items, pools, and creatures are moved rather than deleted.
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

		/// <summary>Small heart apron, one supply column, and a three-cell-wide approach.</summary>
		internal static bool Required(int X, int Y)
		{
			bool apron = X >= 37 && X <= 44 && Y >= 10 && Y <= 15;
			bool supply = X >= 27 && X <= 30 && Y >= 9 && Y <= 17;
			bool approach = X >= 29 && X <= 37 && Y >= 11 && Y <= 13;
			return apron || supply || approach;
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
