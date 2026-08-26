using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// One design's standing on the map: whether the settlement could raise it, how far off it is
	/// if not, and what is in the way.
	/// </summary>
	internal readonly struct TechMapRow
	{
		/// <summary>The catalogue key.</summary>
		internal readonly string Key;

		/// <summary>What the founder calls it.</summary>
		internal readonly string Name;

		/// <summary>Gates unmet. Zero means the only thing between the founder and this design is
		/// ground and water.</summary>
		internal readonly int Distance;

		/// <summary>What is in the way, in the founder's words. Empty when nothing is.</summary>
		internal readonly string Missing;

		internal TechMapRow(string key, string name, int distance, string missing)
		{
			Key = key;
			Name = name;
			Distance = distance;
			Missing = missing;
		}

		/// <summary>Whether the settlement's own state, rather than the ground it is standing on,
		/// is what stops this.</summary>
		internal bool Open
		{
			get { return Distance <= 0; }
		}
	}

}
