using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The realm-scope facts a seal needs that a settlement does not hold: who the lineage is,
	/// which game it came from, and how deep the line runs.
	/// <para>
	/// Separate from <see cref="KingdomSealRecord"/> because these are the fields the interregnum
	/// draw is seeded from, and the draw must be seeded from <b>immutable legacy data only</b>
	/// &mdash; never the target world's seed, the calendar, or anything the player can turn over
	/// again (<c>DECISIONS.md:174-186</c>). Keeping them in one named place is what makes that
	/// reviewable.
	/// </para>
	/// </summary>
	internal sealed class KingdomSealLineage
	{
		public string LineageId = "";

		public string LegacyId = "";

		public string OriginGameId = "";

		public int Generation;

		public int Revision;

		public KingdomSealLineage()
		{
		}

		public KingdomSealLineage(string LineageId, string LegacyId, string OriginGameId, int Generation, int Revision)
		{
			this.LineageId = LineageId ?? "";
			this.LegacyId = LegacyId ?? "";
			this.OriginGameId = OriginGameId ?? "";
			this.Generation = Generation;
			this.Revision = Revision;
		}
	}
}
