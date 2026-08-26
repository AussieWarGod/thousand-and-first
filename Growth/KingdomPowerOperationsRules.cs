namespace ThousandAndFirst
{
	public static partial class KingdomPowerRules
	{
		/// <summary>
		/// What one work makes in a day: its rating cut by the crew it has and again by what
		/// the ground or the sky is giving it.
		/// </summary>
		/// <param name="Source">Kind of work.</param>
		/// <param name="CrewEffectiveness">0-100, as <c>KingdomRules.CrewEffectiveness</c> reports it.</param>
		/// <param name="AvailabilityPercent">0-100 from the matching availability rule; hands are always 100.</param>
		/// <returns>Charge per day, never negative.</returns>
		public static int DailyOutput(PowerSource Source, int CrewEffectiveness, int AvailabilityPercent)
		{
			long rated = RatedChargePerDay(Source);
			long crew = ClampPercent(CrewEffectiveness);
			long available = ClampPercent(AvailabilityPercent);
			return (int)(rated * crew * available / 10000L);
		}

		/// <summary>
		/// What a day's output comes to across a stretch of world time. The full elapsed, in
		/// full: a wheel in a river turns while the founder is elsewhere (Addendum 8 clause 1).
		/// <para>
		/// A day's output is already crew effectiveness times availability
		/// (<see cref="DailyOutput"/>), so an unstaffed work multiplies a season by zero and gets
		/// zero &mdash; clause 2, and the reason this needed no ceiling of its own. What the
		/// settlement can KEEP of the answer is the stores' business
		/// (<see cref="Absorbable"/>), and a store the founder never built holds nothing.
		/// </para>
		/// </summary>
		/// <param name="DailyCharge">One day's output, from <see cref="DailyOutput"/>.</param>
		/// <param name="Days">Days elapsed; anything at or below zero yields nothing.</param>
		public static int ChargeForDays(int DailyCharge, int Days)
		{
			if (DailyCharge <= 0)
			{
				return 0;
			}
			long charge = (long)DailyCharge * ClampDays(Days);
			return (charge > int.MaxValue) ? int.MaxValue : (int)charge;
		}

		/// <summary>Charge the stores may take in, or give back, across a span.</summary>
		/// <param name="Capacity">Total capacity of every store the settlement has.</param>
		/// <param name="Days">Days being credited.</param>
		public static int ThroughputForDays(int Capacity, int Days)
		{
			if (Capacity <= 0)
			{
				return 0;
			}
			long throughput = (long)(Capacity / SaltStoreThroughputDivisor) * ClampDays(Days);
			return (throughput > int.MaxValue) ? int.MaxValue : (int)throughput;
		}

		/// <summary>
		/// How much of an offer the molten-salt store will actually take: what it has room
		/// for, and no faster than the crew can pour it.
		/// </summary>
		/// <param name="Offered">Charge on offer.</param>
		/// <param name="StoredCharge">What the stores already hold.</param>
		/// <param name="Capacity">Total capacity of every store.</param>
		/// <param name="Days">Days being credited.</param>
		/// <returns>Charge accepted, 0 or more and never above <paramref name="Offered"/>.</returns>
		public static int Absorbable(int Offered, int StoredCharge, int Capacity, int Days)
		{
			if (Offered <= 0)
			{
				return 0;
			}
			int room = Capacity - ((StoredCharge > 0) ? StoredCharge : 0);
			if (room <= 0)
			{
				return 0;
			}
			int take = Offered;
			if (take > room)
			{
				take = room;
			}
			int throughput = ThroughputForDays(Capacity, Days);
			return (take > throughput) ? throughput : take;
		}

		/// <summary>
		/// How much the store will give back across a span: what it holds, no faster than the
		/// crew can draw it. Never more than is there, so a store cannot be overdrawn into a
		/// debt &mdash; nothing in this settlement runs a deficit.
		/// </summary>
		public static int Releasable(int StoredCharge, int Capacity, int Days)
		{
			if (StoredCharge <= 0)
			{
				return 0;
			}
			int throughput = ThroughputForDays(Capacity, Days);
			return (StoredCharge > throughput) ? throughput : StoredCharge;
		}

		/// <summary>
		/// Where the settlement stands: what it makes against what its posts could spend. Works
		/// that exist but make nothing read as <see cref="SupplyTier.Idle"/> rather than
		/// <see cref="SupplyTier.None"/>, because the founder's next move is different in each
		/// case and the line they read has to say which.
		/// </summary>
		/// <param name="ChargePerDay">What every crewed work makes in a day.</param>
		/// <param name="Works">Power works standing, crewed or not.</param>
		/// <param name="Posts">Things the settlement built that spend charge &mdash; charging
		/// posts and anything like them. Zero is measured as one, so the tier still means
		/// something in a settlement that has power before it has anywhere to put it.</param>
		public static SupplyTier ClassifySupply(int ChargePerDay, int Works, int Posts)
		{
			if (Works <= 0)
			{
				return SupplyTier.None;
			}
			if (ChargePerDay <= 0)
			{
				return SupplyTier.Idle;
			}
			int need = PostDailyNeedCharge * ((Posts > 0) ? Posts : 1);
			if (ChargePerDay * 2 < need)
			{
				return SupplyTier.Thin;
			}
			return (ChargePerDay >= need * 2) ? SupplyTier.Ample : SupplyTier.Steady;
		}

		/// <summary>Plain word for a tier, as it appears in the Charter's status line.</summary>
		public static string SupplyTierName(SupplyTier Tier)
		{
			switch (Tier)
			{
			case SupplyTier.Idle:
				return "idle";
			case SupplyTier.Thin:
				return "thin";
			case SupplyTier.Steady:
				return "steady";
			case SupplyTier.Ample:
				return "ample";
			default:
				return "none";
			}
		}

		/// <summary>The sentence that tells a founder why a work of this kind is making nothing.</summary>
		public static string IdleReason(PowerSource Source)
		{
			switch (Source)
			{
			case PowerSource.Water:
				return IdleNoWater;
			case PowerSource.Wind:
				return IdleNoWind;
			default:
				return IdleNoCrew;
			}
		}

		/// <summary>
		/// The settlement's power in one line: a word for how it stands, what the works make in
		/// a day, and what the salt is holding. Never a table of charge numbers &mdash; a
		/// founder is told the state of their settlement, not asked to audit it.
		/// </summary>
		/// <param name="Tier">Read from <see cref="ClassifySupply"/>.</param>
		/// <param name="ChargePerDay">What every crewed work makes in a day.</param>
		/// <param name="StoredCharge">What the stores hold now.</param>
		/// <param name="Capacity">Total capacity of every store; 0 when none is built.</param>
		/// <param name="Reason">Why nothing is being made, when nothing is; ignored otherwise.</param>
		/// <returns>One line, already colour-marked, or empty when there is nothing to report.</returns>
		public static string SupplyLine(SupplyTier Tier, int ChargePerDay, int StoredCharge, int Capacity, string Reason)
		{
			if (Tier == SupplyTier.None)
			{
				return "";
			}
			if (Tier == SupplyTier.Idle)
			{
				return "Power: {{r|idle}} — " + (string.IsNullOrEmpty(Reason) ? IdleNoCrew : Reason);
			}
			string colour = (Tier == SupplyTier.Thin) ? "{{r|" : "{{G|";
			string store = (Capacity > 0)
				? (", and the salt store holds " + StoredCharge + " of " + Capacity + ".")
				: ", and nothing holds it overnight. A molten-salt store would keep the night's share.";
			return "Power: " + colour + SupplyTierName(Tier) + "}} — the works make " + ChargePerDay + " a day" + store;
		}
	}
}
