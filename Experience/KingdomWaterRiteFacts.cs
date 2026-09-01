using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Everything that actually stands between one settler and the realm's affiliation or belief,
	/// gathered by <c>KingdomWaterRite</c> off real people and real buildings and handed here as
	/// plain data.
	/// Nothing in this struct is a meter and nothing in it decays; each field is a fact about
	/// tonight.
	/// </summary>
	public readonly struct WaterRiteFacts
	{
		/// <summary>0-100, from <c>KingdomCreed.HostilityBetween</c> on their creed and the
		/// realm's. Zero for a settler who holds nothing, for two creeds that get on, and for a
		/// pair the engine has no opinion about.</summary>
		public readonly int Hostility;

		/// <summary>Attended passes this settler has been present for. See
		/// <see cref="KingdomWaterRiteRules.SharedDaysAfter"/> for what it is denominated in.</summary>
		public readonly int SharedDays;

		/// <summary>Whether they hold a creed key of their own, as against holding nothing in
		/// particular. Crossing from an affiliation or belief is further than crossing from
		/// none.</summary>
		public readonly bool HoldsACreed;

		/// <summary>Whether a live shrine capability consecrated to something other than the
		/// realm's creed has an exact designation whose physical reach covers their own door.</summary>
		public readonly bool RivalShrine;

		/// <summary>Whether their quality-of-life profile <em>prefers</em> the faith tag &mdash;
		/// belief is a thing they think about, so it is not a thing they trade.</summary>
		public readonly bool Devout;

		/// <summary>Whether their profile <em>refuses</em> the faith tag. Absolute at every
		/// distance, exactly as an authored <c>Refuses</c> is absolute at every closeness rung.
		/// </summary>
		public readonly bool Steadfast;

		/// <summary>The realm's own creed key, as a faction name. What they are being asked to take,
		/// and the thing whose changing re-opens every closed door here.</summary>
		public readonly string RealmCreed;

		public WaterRiteFacts(int Hostility, int SharedDays, bool HoldsACreed, bool RivalShrine, bool Devout, bool Steadfast, string RealmCreed)
		{
			this.Hostility = Hostility;
			this.SharedDays = SharedDays;
			this.HoldsACreed = HoldsACreed;
			this.RivalShrine = RivalShrine;
			this.Devout = Devout;
			this.Steadfast = Steadfast;
			this.RealmCreed = RealmCreed;
		}
	}
}
