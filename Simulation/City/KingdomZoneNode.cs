using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One node of the level-1 graph: a claimed zone, by id and by the world coordinates its
	/// <c>ZoneID</c> already carries.
	/// <para>
	/// <c>ZoneID</c> carries the stratum &mdash; <c>Assemble(...).Append(ZoneZ)</c>
	/// (<c>D/XRL/World/ZoneID.cs:12-24</c>) &mdash; so a city three parasangs wide and three strata
	/// deep is the same <i>arithmetic</i> as a flat one.
	/// </para>
	/// <para>
	/// <b>The arithmetic was free and the ground never was.</b> This file used to say verticality
	/// cost nothing, and it was true of the sums and false of the world: rock is not a doorway, and
	/// a carrier cannot walk down through it because the coordinates happen to differ by one. What
	/// makes the descent real is a shaft somebody cut (<see cref="KingdomDelveRules"/>), and
	/// <see cref="Shaft"/> is where the node carries whether one stands here.
	/// </para>
	/// </summary>
	internal readonly struct KingdomZoneNode
	{
		internal readonly string ZoneId;

		/// <summary>Global zone x, as <c>KingdomRules.TryParseZoneID</c> composes it
		/// (<c>parasangX * 3 + zoneX</c>).</summary>
		internal readonly int GlobalX;

		internal readonly int GlobalY;

		internal readonly int Stratum;

		/// <summary>Whether a finished delve goes down from this ground, which is the only thing
		/// that makes the stratum below it an edge of this graph at all.</summary>
		internal readonly bool Shaft;

		/// <summary>Ground with no shaft in it, which is every piece of ground a caller has not
		/// said otherwise about. The conservative default on purpose: an edge nobody vouched for
		/// is unbroken rock, and a route through unbroken rock is refused rather than estimated.</summary>
		internal KingdomZoneNode(string zoneId, int globalX, int globalY, int stratum)
			: this(zoneId, globalX, globalY, stratum, shaft: false)
		{
		}

		internal KingdomZoneNode(string zoneId, int globalX, int globalY, int stratum, bool shaft)
		{
			ZoneId = zoneId;
			GlobalX = globalX;
			GlobalY = globalY;
			Stratum = stratum;
			Shaft = shaft;
		}
	}
}
