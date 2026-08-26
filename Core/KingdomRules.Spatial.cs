namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		public static bool TryParseZoneID(string ZoneID, out string World, out int GX, out int GY, out int Z)
		{
			World = null;
			GX = 0;
			GY = 0;
			Z = 0;
			if (string.IsNullOrEmpty(ZoneID))
			{
				return false;
			}
			string[] array = ZoneID.Split('.');
			if (array.Length != 6)
			{
				return false;
			}
			if (!int.TryParse(array[1], out var wx) || !int.TryParse(array[2], out var wy) || !int.TryParse(array[3], out var zx) || !int.TryParse(array[4], out var zy) || !int.TryParse(array[5], out Z))
			{
				return false;
			}
			World = array[0];
			GX = wx * 3 + zx;
			GY = wy * 3 + zy;
			return true;
		}

		/// <summary>
		/// Chebyshev adjacency between two zones in global zone coordinates, with an optional
		/// vertical neighbour on top of it. Engine-free so it stays unit-testable; callers inside
		/// the game should obtain coordinates from <c>XRL.World.ZoneID.Parse</c>, which also
		/// understands instanced zone IDs.
		/// <para>
		/// <paramref name="IncludeVertical"/> defaults false so every existing caller of this
		/// overload &mdash; and every existing test of it &mdash; keeps exactly the answer it
		/// always got. Territorial claiming is the one caller that opts in
		/// (<c>KingdomFounding.ZonesAdjacent</c>), because a cellar directly below a held zone or
		/// a tower directly above it is the settlement's own ground, not a neighbour's.
		/// </para>
		/// </summary>
		/// <returns>True if the zones touch (including diagonally) on the same stratum and are
		/// not the same zone, or &mdash; only when <paramref name="IncludeVertical"/> is true and
		/// only in the same column (no diagonal-and-different-stratum case counts) &mdash; are
		/// exactly one stratum apart.</returns>
		/// <summary>
		/// Which edges of a claimed zone face ground the settlement does not hold.
		/// <para>
		/// A camp becomes a city across several zones, so a wall is not a per-zone decoration -
		/// it belongs on the frontier of the whole claim. Claim the neighbour and that edge stops
		/// being frontier; the wall standing there becomes an inner wall rather than a mistake,
		/// which is how real cities grow and means expansion never wastes what was already built.
		/// </para>
		/// </summary>
		[System.Flags]
		public enum Frontier
		{
			None = 0,
			North = 1,
			South = 2,
			West = 4,
			East = 8
		}

		/// <summary>
		/// The edges of <paramref name="ZoneID"/> that border unclaimed ground.
		/// </summary>
		/// <param name="ZoneID">A zone the settlement holds. An unparseable id has no frontier.</param>
		/// <param name="ClaimedZones">Every zone the realm holds, this one included.</param>
		/// <returns>Every edge facing ground the realm does not hold; <see cref="Frontier.None"/>
		/// when the zone is entirely surrounded by the settlement's own ground.</returns>
		public static Frontier FrontierEdges(string ZoneID, System.Collections.Generic.IEnumerable<string> ClaimedZones)
		{
			if (!TryParseZoneID(ZoneID, out var world, out var gx, out var gy, out var z) || ClaimedZones == null)
			{
				return Frontier.None;
			}
			bool north = false;
			bool south = false;
			bool west = false;
			bool east = false;
			foreach (string other in ClaimedZones)
			{
				if (!TryParseZoneID(other, out var otherWorld, out var ox, out var oy, out var oz))
				{
					continue;
				}
				if (otherWorld != world || oz != z)
				{
					continue;
				}
				// North is decreasing GY: the world map counts southward.
				if (ox == gx && oy == gy - 1) { north = true; }
				if (ox == gx && oy == gy + 1) { south = true; }
				if (oy == gy && ox == gx - 1) { west = true; }
				if (oy == gy && ox == gx + 1) { east = true; }
			}
			Frontier edges = Frontier.None;
			if (!north) { edges |= Frontier.North; }
			if (!south) { edges |= Frontier.South; }
			if (!west) { edges |= Frontier.West; }
			if (!east) { edges |= Frontier.East; }
			return edges;
		}

		/// <summary>How deep from a zone edge still counts as the wall line.</summary>
		public const int FrontierBandCells = 2;

		/// <summary>
		/// Whether a cell sits on one of the given frontier edges, and so is wall ground.
		/// </summary>
		/// <param name="X">Cell x, 0 to Width-1.</param>
		/// <param name="Y">Cell y, 0 to Height-1.</param>
		/// <param name="Width">Zone width in cells.</param>
		/// <param name="Height">Zone height in cells.</param>
		/// <param name="Edges">Edges facing unclaimed ground, from <see cref="FrontierEdges"/>.</param>
		public static bool IsOnFrontier(int X, int Y, int Width, int Height, Frontier Edges)
		{
			if (Edges == Frontier.None || Width <= 0 || Height <= 0)
			{
				return false;
			}
			if ((Edges & Frontier.North) != 0 && Y < FrontierBandCells) { return true; }
			if ((Edges & Frontier.South) != 0 && Y >= Height - FrontierBandCells) { return true; }
			if ((Edges & Frontier.West) != 0 && X < FrontierBandCells) { return true; }
			if ((Edges & Frontier.East) != 0 && X >= Width - FrontierBandCells) { return true; }
			return false;
		}

		public static bool CoordsAdjacent(string WorldA, int GXA, int GYA, int ZA, string WorldB, int GXB, int GYB, int ZB, bool IncludeVertical = false)
		{
			if (WorldA != WorldB)
			{
				return false;
			}
			int dx = (GXA > GXB) ? (GXA - GXB) : (GXB - GXA);
			int dy = (GYA > GYB) ? (GYA - GYB) : (GYB - GYA);
			if (ZA == ZB)
			{
				if (dx <= 1 && dy <= 1)
				{
					return dx + dy > 0;
				}
				return false;
			}
			if (!IncludeVertical)
			{
				return false;
			}
			int dz = (ZA > ZB) ? (ZA - ZB) : (ZB - ZA);
			return dx == 0 && dy == 0 && dz == 1;
		}

		public static bool ZonesAdjacent(string A, string B)
		{
			return ZonesAdjacent(A, B, IncludeVertical: false);
		}

		/// <summary>
		/// As <see cref="ZonesAdjacent(string, string)"/>, but a zone directly above or below the
		/// other &mdash; same world column, one stratum apart &mdash; counts as adjacent too when
		/// <paramref name="IncludeVertical"/> is true. A malformed ID on either side refuses the
		/// claim rather than guessing: see <see cref="TryParseZoneID"/>.
		/// </summary>
		public static bool ZonesAdjacent(string A, string B, bool IncludeVertical)
		{
			if (!TryParseZoneID(A, out var worldA, out var gxA, out var gyA, out var zA) || !TryParseZoneID(B, out var worldB, out var gxB, out var gyB, out var zB))
			{
				return false;
			}
			return CoordsAdjacent(worldA, gxA, gyA, zA, worldB, gxB, gyB, zB, IncludeVertical);
		}

		/// <summary>True if <paramref name="ZoneFaction"/> names a faction other than the
		/// kingdom's own and isn't empty &mdash; ground someone else already answers to. Null and
		/// empty read as unclaimed, which is what an ordinary wilderness zone's faction property
		/// actually is.</summary>
		public static bool GroundIsForeignFaction(string ZoneFaction, string KingdomFactionName)
		{
			return !string.IsNullOrEmpty(ZoneFaction) && ZoneFaction != KingdomFactionName;
		}

	}
}
