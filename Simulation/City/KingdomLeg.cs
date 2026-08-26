namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One leg of a job: which zone it crosses, where it enters and leaves, how long the crossing
	/// is, and the two ticks that date it.
	/// <para>
	/// The endpoints and the length are model truth; the in-between is a redrawing that may differ
	/// by a cell or two if the ground changed (LIVING-CITY-ARCHITECTURE &sect;3.7). Storing a full
	/// cell path would be up to eighty entries a leg and would make a wall raised across the route
	/// a contradiction rather than a detour.
	/// </para>
	/// </summary>
	internal readonly struct KingdomLeg
	{
		internal readonly string ZoneId;

		internal readonly short EnterX;

		internal readonly short EnterY;

		internal readonly short ExitX;

		internal readonly short ExitY;

		/// <summary>Cells to walk. At creation this is an estimate; at render the real length is
		/// measured once and the leg re-projects.</summary>
		internal readonly int PathLength;

		internal readonly long DepartTick;

		internal readonly long ArriveTick;

		internal KingdomLeg(string zoneId, short enterX, short enterY, short exitX, short exitY, int pathLength, long departTick, long arriveTick)
		{
			ZoneId = zoneId;
			EnterX = enterX;
			EnterY = enterY;
			ExitX = exitX;
			ExitY = exitY;
			PathLength = pathLength;
			DepartTick = departTick;
			ArriveTick = arriveTick;
		}

		internal KingdomLeg WithTicks(long departTick, long arriveTick)
		{
			return new KingdomLeg(ZoneId, EnterX, EnterY, ExitX, ExitY, PathLength, departTick, arriveTick);
		}
	}
}
