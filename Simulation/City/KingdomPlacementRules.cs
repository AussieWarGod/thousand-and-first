using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The stretch of a day the city reads a person's whereabouts against.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.2(b): <b>the day-shape vocabulary already exists, in the
	/// game's own register.</b> These five bands are unions of <c>Calendar.GetTime(int)</c>'s own
	/// eight named stretches, cut at the boundaries the calendar already cuts at
	/// (<c>D/XRL/World/Calendar.cs:296-352</c>) &mdash; so the band a settler walks on is the same
	/// band the founder reads off the clock, and nothing here invents a second day.
	/// </para>
	/// </summary>
	internal enum KingdomDayBand : byte
	{
		/// <summary>Waxing Beetle Moon through Zenith through Waning: 1051&ndash;1200 and
		/// 0&ndash;150. Hearths, except the watch &mdash; and vanilla's own <c>Bed</c> already
		/// walks a settler tagged <c>SleepOnBed</c> to a bed for us, free.</summary>
		BeetleMoon = 0,

		/// <summary>The Shallows and Harvest Dawn: 151&ndash;450. Rising, hearth to
		/// workplace.</summary>
		Rising = 1,

		/// <summary>Waxing, High and Waning Salt Sun: 451&ndash;750. Everyone at post.</summary>
		SaltSun = 2,

		/// <summary>Hindsun: 751&ndash;900. Trades wind down; market and shrine busiest.</summary>
		Hindsun = 3,

		/// <summary>Jeweled Dusk: 901&ndash;1050. Homeward; the hearths fill.</summary>
		JeweledDusk = 4
	}

	/// <summary>
	/// Where the model says a person belongs at this hour. Two states, because there are two
	/// answers a city has about somebody: they are wanted at their post, or they are not and their
	/// own hearth is where they go.
	/// </summary>
	internal enum KingdomPost : byte
	{
		/// <summary>Home. The anchor is released from any station holding it.</summary>
		Hearth = 0,

		/// <summary>At the work their job names. The anchor moves there and vanilla walks
		/// them.</summary>
		Station = 1
	}

	/// <summary>
	/// Placement by the hour: which band a tick falls in, and where a <see cref="KingdomDayShape"/>
	/// stands in it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.2(b). <b>The model decides where a person belongs at this
	/// hour; the anchor is set at activation; and an <c>r_</c> part on the workplace claims them
	/// through <c>IdleQueryEvent</c> while the founder watches.</b> This file is the first clause
	/// and only the first clause: pure, engine-free, total, and holding no times of its own.
	/// </para>
	/// <para>
	/// It is deliberately <b>not</b> a scheduler. There is no queue, no per-settler timetable and
	/// no second turn loop; there is a function from (shape, tick) to a place, evaluated when
	/// somebody is looking. Vanilla ships no NPC scheduler and this design does not add one
	/// (<c>D/XRL/World/AI/GoalHandlers/Bored.cs:262-330</c> is the whole daily-life surface the
	/// engine has).
	/// </para>
	/// </summary>
	internal static class KingdomPlacementRules
	{
		/// <summary><c>Calendar.TurnsPerDay</c> (<c>D/XRL/World/Calendar.cs:13</c>).</summary>
		internal const int TicksPerDay = 1200;

		/// <summary><c>Calendar.TurnsPerHour</c> (<c>D/XRL/World/Calendar.cs:15</c>). One in-game
		/// hour, and the cadence &sect;3.6 gives the heartbeat.</summary>
		internal const int TicksPerHour = 50;

		/// <summary>Where "The Shallows" begins in <c>Calendar.GetTime</c>.</summary>
		internal const int RisingStartTick = 151;

		/// <summary>Where "Waxing Salt Sun" begins.</summary>
		internal const int SaltSunStartTick = 451;

		/// <summary>Where "Hindsun" begins.</summary>
		internal const int HindsunStartTick = 751;

		/// <summary>Where "Jeweled Dusk" begins.</summary>
		internal const int DuskStartTick = 901;

		/// <summary>Where "Waxing Beetle Moon" begins. Past here the day wraps into the small
		/// hours, which is why <see cref="KingdomDayBand.BeetleMoon"/> is the one band that is not
		/// a contiguous run of ticks.</summary>
		internal const int BeetleMoonStartTick = 1051;

		/// <summary>
		/// How long a station must wait before it may claim another settler's turn.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.2(b) constraint 2: returning <c>false</c> from
		/// <c>IdleQueryEvent</c> costs the actor its turn, so a station must be selective or the
		/// settlement stands around doing one thing &mdash; <i>"&sect;3.6 gives that constraint a
		/// number"</i>, and this is that number, the heartbeat's own cadence. Vanilla's <c>Bed</c>
		/// keeps exactly this discipline with exactly this figure
		/// (<c>D/XRL/World/Parts/Bed.cs:209-212</c>: <c>currentTurn - lastIdleUsed &lt; 50</c>).
		/// </para>
		/// </summary>
		internal const int ClaimCooldownTicks = KingdomBudgetRules.HeartbeatCadenceTicks;

		/// <summary>Where in the day a tick falls, 0 to 1199. Total over every representable
		/// input, including a negative one: a clock that cannot be read is worse than a clock read
		/// from the wrong end of an epoch that cannot occur.</summary>
		internal static int TickOfDay(long timeTicks)
		{
			int within = (int)(timeTicks % TicksPerDay);
			return (within < 0) ? (within + TicksPerDay) : within;
		}

		/// <summary>The band a tick falls in.</summary>
		internal static KingdomDayBand BandFor(long timeTicks)
		{
			int within = TickOfDay(timeTicks);
			if (within < RisingStartTick || within >= BeetleMoonStartTick)
			{
				return KingdomDayBand.BeetleMoon;
			}
			if (within < SaltSunStartTick)
			{
				return KingdomDayBand.Rising;
			}
			if (within < HindsunStartTick)
			{
				return KingdomDayBand.SaltSun;
			}
			if (within < DuskStartTick)
			{
				return KingdomDayBand.Hindsun;
			}
			return KingdomDayBand.JeweledDusk;
		}

		/// <summary>
		/// Where a day shape puts a person in this band. LIVING-CITY-ARCHITECTURE &sect;3.2(b)'s
		/// table, and nothing else anywhere may hold a second copy of it.
		/// <para>
		/// The watch is the one shape that keeps its post in every band, which is what a watch is.
		/// Market and shrine keep theirs through Hindsun, when the trades are winding down and
		/// those two are busiest. Everybody else goes home at Hindsun and stays home through the
		/// dusk and the night.
		/// </para>
		/// </summary>
		internal static KingdomPost PostFor(KingdomDayShape shape, KingdomDayBand band)
		{
			if (shape == KingdomDayShape.Hearth)
			{
				// Somebody the works have no room for genuinely spends their day at home. That is
				// KingdomResidentRules.DayShapeFor's own ruling and it is not softened here.
				return KingdomPost.Hearth;
			}
			if (shape == KingdomDayShape.Watch)
			{
				return KingdomPost.Station;
			}
			switch (band)
			{
			case KingdomDayBand.Rising:
			case KingdomDayBand.SaltSun:
				return KingdomPost.Station;
			case KingdomDayBand.Hindsun:
				return (shape == KingdomDayShape.Market || shape == KingdomDayShape.Shrine)
					? KingdomPost.Station
					: KingdomPost.Hearth;
			default:
				return KingdomPost.Hearth;
			}
		}

		/// <summary>Whether two ticks fall in different bands &mdash; the one question a
		/// re-anchoring asks, and the reason the bands are an enum rather than a string.</summary>
		internal static bool BandChanged(long fromTick, long toTick)
		{
			return BandFor(fromTick) != BandFor(toTick);
		}

		/// <summary>
		/// Whether a station may claim an actor's turn again, given when it last did.
		/// <para>
		/// A station that has never claimed (<paramref name="lastClaimTick"/> at or below zero) may
		/// always claim: the cooldown is a rate limit on a thing that has happened, never a delay
		/// before it may happen at all.
		/// </para>
		/// </summary>
		internal static bool MayClaim(long lastClaimTick, long nowTick)
		{
			if (lastClaimTick <= 0L)
			{
				return true;
			}
			return nowTick - lastClaimTick >= ClaimCooldownTicks;
		}
	}
}
