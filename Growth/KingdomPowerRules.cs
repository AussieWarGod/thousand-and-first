namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for the settlement's power. Power here is civic labour, not wiring:
	/// a work makes a rated amount in a day, short crew cuts it in proportion, weather and
	/// standing water cut it again, the molten-salt store holds what the day did not spend,
	/// and the whole of it reads as one line a founder understands. The engine-coupled parts
	/// that carry this state are <c>r_KingdomPowerWork</c> and <c>r_KingdomPowerStore</c>, in
	/// the same folder.
	/// </summary>
	/// <remarks>
	/// Every quantity is in vanilla charge units, the same currency a chem cell (10,000) and
	/// the charging post's cradle (4,000) are measured in, so nothing here needs converting
	/// before it reaches an engine part.
	/// </remarks>
	public static class KingdomPowerRules
	{
		/// <summary>
		/// What turns a work. Named for the thing a settler would name, not for a machine
		/// class: hands on a bar, a brook under a wheel, wind in a sail.
		/// </summary>
		public enum PowerSource
		{
			Hands = 0,
			Water = 1,
			Wind = 2
		}

		/// <summary>
		/// Coarse read on what the settlement makes against what its charging posts could
		/// spend. <see cref="SupplyTier.None"/> means nothing has been built to make power at
		/// all; <see cref="SupplyTier.Idle"/> means the works exist and are making nothing,
		/// which is a different sentence and a different remedy.
		/// </summary>
		public enum SupplyTier
		{
			None = 0,
			Idle = 1,
			Thin = 2,
			Steady = 3,
			Ample = 4
		}

		/// <summary>Charge a fully crewed crank mill makes in a day.</summary>
		public const int MillChargePerDay = 2400;

		/// <summary>Charge a water wheel makes in a day on water at or above
		/// <see cref="HydraulicRatedDrams"/>.</summary>
		public const int WaterWheelChargePerDay = 4800;

		/// <summary>Charge a sailvane makes in a day in wind at or above
		/// <see cref="RatedWindSpeedKph"/>.</summary>
		public const int SailvaneChargePerDay = 3600;

		/// <summary>
		/// Open water beside a wheel below which it does not turn at all. Deliberately the
		/// same figure vanilla's own <c>HydroTurbine</c> carries on the wooden water wheel
		/// (<c>MinimumEffectiveVolume="400"</c>), so the settlement's report and the wheel's
		/// own working state never disagree in front of the player.
		/// </summary>
		public const int HydraulicMinimumDrams = 400;

		/// <summary>Open water at which a wheel makes its full rating. Vanilla's
		/// <c>MaximumEffectiveVolume="4000"</c> on the same part.</summary>
		public const int HydraulicRatedDrams = 4000;

		/// <summary>
		/// Wind at which a sailvane makes its full rating, in the kph units
		/// <c>Zone.CurrentWindSpeed</c> reports. Surface zones roll 0-60 to 0-100, so a rating
		/// at 60 means a good day is full output and a great day is not wasted.
		/// </summary>
		public const int RatedWindSpeedKph = 60;

		/// <summary>
		/// What the wind is worth on a day nobody watched. A gust read at the moment the
		/// founder walks in is evidence about that moment only, so it speaks for one day and
		/// this figure speaks for the rest &mdash; see <see cref="WindAvailabilityPercent"/>.
		/// </summary>
		public const int TypicalWindAvailabilityPercent = 50;

		/// <summary>
		/// Divisor turning a store's capacity into what it can take in, or give back, in one
		/// day. Molten salt is poured and drawn by the same hands that turn everything else
		/// here: a store twice the size is worked twice as fast, and a store is never a bucket
		/// that empties in an instant.
		/// </summary>
		public const int SaltStoreThroughputDivisor = 2;

		/// <summary>
		/// Charge one charging post can put to use in a day; the yardstick
		/// <see cref="ClassifySupply"/> measures supply against. One cradle-full, which is the
		/// unit a founder actually experiences ("the post was full when I got back").
		/// </summary>
		public const int PostDailyNeedCharge = 4000;

		/// <summary>Days of settlement life one visit may credit. Absence beyond this is
		/// forgiven, exactly as it is for water.</summary>
		public const int MaxDaysCredited = KingdomRules.MaxUpkeepDaysCharged;

		public const string IdleNoWorks = "Nothing here makes power yet. A crank mill would give the post something to draw on.";

		public const string IdleNoCrew = "No one is on the works. More settlers, or fewer works.";

		public const string IdleNoWater = "The wheel stands dry. It wants open water beside it, and it will never drink the cisterns.";

		public const string IdleNoWind = "The vane hangs still. There is no wind worth the name on this ground.";

		/// <summary>What a work of this kind makes in a day, fully crewed and fully served.</summary>
		/// <param name="Source">Kind of work.</param>
		/// <returns>Charge per day; always positive for every kind this build defines.</returns>
		public static int RatedChargePerDay(PowerSource Source)
		{
			switch (Source)
			{
			case PowerSource.Water:
				return WaterWheelChargePerDay;
			case PowerSource.Wind:
				return SailvaneChargePerDay;
			default:
				return MillChargePerDay;
			}
		}

		/// <summary>
		/// Reads a <c>Source</c> attribute off a building's part declaration. Third-party XML
		/// is untrusted: an unknown or empty value is rejected rather than defaulted, so a
		/// misspelt work disables itself instead of quietly becoming a mill.
		/// </summary>
		/// <param name="Text">Attribute text, case-insensitive.</param>
		/// <param name="Source">Set to the parsed kind on success; <see cref="PowerSource.Hands"/> otherwise.</param>
		/// <returns>True when the text named a kind this build knows.</returns>
		public static bool TryParseSource(string Text, out PowerSource Source)
		{
			Source = PowerSource.Hands;
			if (string.IsNullOrEmpty(Text))
			{
				return false;
			}
			string text = Text.Trim().ToLowerInvariant();
			switch (text)
			{
			case "hands":
				Source = PowerSource.Hands;
				return true;
			case "water":
				Source = PowerSource.Water;
				return true;
			case "wind":
				Source = PowerSource.Wind;
				return true;
			default:
				return false;
			}
		}

		/// <summary>Clamps a percentage into 0-100.</summary>
		public static int ClampPercent(int Percent)
		{
			if (Percent < 0)
			{
				return 0;
			}
			return (Percent > 100) ? 100 : Percent;
		}

		/// <summary>Clamps a day count into the span one visit may credit.</summary>
		public static int ClampDays(int Days)
		{
			if (Days <= 0)
			{
				return 0;
			}
			return (Days > MaxDaysCredited) ? MaxDaysCredited : Days;
		}

		/// <summary>
		/// What a water wheel is worth on the open water standing beside it. Zero below
		/// <see cref="HydraulicMinimumDrams"/> &mdash; a wheel in a puddle is a wheel that does
		/// not turn &mdash; then straight up to full at <see cref="HydraulicRatedDrams"/>.
		/// </summary>
		/// <param name="NearbyDrams">Drams of <em>open</em> water in and beside the wheel's cell.
		/// The settlement's dedicated cisterns are never counted here: the hydraulics do not
		/// drink what the people drink.</param>
		/// <returns>Availability percent, 0 to 100.</returns>
		public static int WaterAvailabilityPercent(int NearbyDrams)
		{
			if (NearbyDrams < HydraulicMinimumDrams)
			{
				return 0;
			}
			if (NearbyDrams >= HydraulicRatedDrams)
			{
				return 100;
			}
			return (NearbyDrams - HydraulicMinimumDrams) * 100 / (HydraulicRatedDrams - HydraulicMinimumDrams);
		}

		/// <summary>
		/// What a sailvane is worth over a span. The wind is only witnessed on the day the
		/// founder is standing in it, so that day is credited at the gust actually read and
		/// every further day at <see cref="TypicalWindAvailabilityPercent"/>. A calm afternoon
		/// therefore cannot cost the settlement a week of milling, and a gale cannot pay for
		/// one.
		/// </summary>
		/// <param name="SampledSpeedKph">Wind speed read this visit, in kph.</param>
		/// <param name="Days">Days being credited; 0 or fewer yields 0.</param>
		/// <returns>Availability percent, 0 to 100.</returns>
		public static int WindAvailabilityPercent(int SampledSpeedKph, int Days)
		{
			int days = ClampDays(Days);
			if (days <= 0)
			{
				return 0;
			}
			int sampled = (SampledSpeedKph <= 0) ? 0 : ClampPercent(SampledSpeedKph * 100 / RatedWindSpeedKph);
			if (days == 1)
			{
				return sampled;
			}
			return (sampled + TypicalWindAvailabilityPercent * (days - 1)) / days;
		}

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
		/// What a day's output comes to across an absence. Days beyond
		/// <see cref="MaxDaysCredited"/> are forgiven rather than paid, the same bargain water
		/// keeps: a season away is worth the same as three days, so leaving is never rewarded
		/// and never punished.
		/// </summary>
		/// <param name="DailyCharge">One day's output, from <see cref="DailyOutput"/>.</param>
		/// <param name="Days">Days elapsed; anything at or below zero yields nothing.</param>
		public static int ChargeForDays(int DailyCharge, int Days)
		{
			if (DailyCharge <= 0)
			{
				return 0;
			}
			return DailyCharge * ClampDays(Days);
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
			return Capacity / SaltStoreThroughputDivisor * ClampDays(Days);
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
