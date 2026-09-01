namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One rendered zone's sparse endpoint observation. Runtime-only and rebuildable.</summary>
	internal sealed class KingdomDistanceZoneCache
	{
		internal string ZoneId = "";

		internal ulong StructureA = 0UL;

		internal ulong StructureB = 0UL;

		internal ulong EligibilityA = 0UL;

		internal ulong EligibilityB = 0UL;

		internal bool Observed = false;

		internal bool BoundaryObserved = false;

		internal int Width = 0;

		internal int Height = 0;

		/// <summary>Two 64-bit words per edge; horizontal zones need at most 80 bits and
		/// vertical edges at most 25. Retained structure only, never a full ground grid.</summary>
		internal ulong[] BoundaryPassable = new ulong[KingdomDistanceRules.EdgesPerZone * 2];

		internal ulong[] BoundaryPaved = new ulong[KingdomDistanceRules.EdgesPerZone * 2];

		internal short[] PortalX = EmptyPortals();

		internal short[] PortalY = EmptyPortals();

		internal ushort[] PortalPairs = new ushort[KingdomDistanceRules.EdgesPerZone
			* KingdomDistanceRules.EdgesPerZone];

		internal KingdomDistanceEndpointState[] Endpoints = new KingdomDistanceEndpointState[0];

		private static short[] EmptyPortals()
		{
			short[] value = new short[KingdomDistanceRules.EdgesPerZone];
			for (int i = 0; i < value.Length; i++) value[i] = -1;
			return value;
		}
	}
}
