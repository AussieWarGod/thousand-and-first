using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// One node's standing on the keepers' map: what the founder calls it, how far off it is in
	/// things rather than in numbers, whether the bench has already begun it, and what is in the
	/// way. Only ever built for a node the founder has HEARD of; a hidden one has no row.
	/// </summary>
	public readonly struct ResearchRow
	{
		public readonly string Key;

		public readonly string Name;

		/// <summary>Gates unmet. Zero means the bench could take this up today.</summary>
		public readonly int Distance;

		/// <summary>Whether labour already stands against it, shelved or current.</summary>
		public readonly bool Begun;

		/// <summary>What is in the way, in the founder's words. Empty when nothing is.</summary>
		public readonly string Missing;

		public ResearchRow(string Key, string Name, int Distance, bool Begun, string Missing)
		{
			this.Key = Key;
			this.Name = Name;
			this.Distance = Distance;
			this.Begun = Begun;
			this.Missing = Missing;
		}
	}
}
