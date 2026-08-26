using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// What a research node names as its effect, beyond the roster keys it mints. Three kinds in
	/// v1, all of them modest and all of them read by a lane that already exists.
	/// </summary>
	public readonly struct ResearchEffect
	{
		/// <summary>One of <see cref="KingdomResearchRules.EffectEfficiency"/>,
		/// <see cref="KingdomResearchRules.EffectStatCap"/>, or
		/// <see cref="KingdomResearchRules.EffectRecruitReveal"/>. A kind this build does not know
		/// is carried rather than refused &mdash; somebody else's vocabulary (STANDARDS 9).</summary>
		public readonly string Kind;

		/// <summary>The stat a <see cref="KingdomResearchRules.EffectStatCap"/> raises the city's
		/// headroom in, folded to lower case, or null for the other kinds.</summary>
		public readonly string Stat;

		public readonly int Amount;

		public ResearchEffect(string Kind, string Stat, int Amount)
		{
			this.Kind = Kind;
			this.Stat = Stat;
			this.Amount = Amount;
		}
	}
}
