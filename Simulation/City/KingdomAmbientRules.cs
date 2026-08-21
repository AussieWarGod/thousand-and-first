using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What the attended zone sounds and smells like, read off the rows that are already there.
	/// <para>
	/// Every field is a count or a flag the model keeps for another reason entirely &mdash; a work
	/// row's condition and crew, a resident row's day shape, a work's last run tick. <b>Nothing is
	/// stored for the sake of ambience</b>, which is BUILDING-CATALOGUE-BRIEF Addendum 13's mesh
	/// condition as a property of the type rather than a promise about it.
	/// </para>
	/// </summary>
	internal readonly struct KingdomAmbientReading
	{
		/// <summary>Works that need hands, have them, and are not worn past the line: the ones
		/// making a noise.</summary>
		internal readonly int Turning;

		/// <summary>Works that have stopped &mdash; <c>KingdomHappeningRules.Broken</c>. The
		/// silence.</summary>
		internal readonly int Stopped;

		/// <summary>Whether anything that cooks or refines ran inside the last day.</summary>
		internal readonly bool CookedToday;

		/// <summary>People whose day shape is the shrine and who are here to keep it.</summary>
		internal readonly int Shrine;

		/// <summary>People whose day shape is the hearth.</summary>
		internal readonly int Hearth;

		/// <summary>People whose day shape is the watch.</summary>
		internal readonly int Watch;

		/// <summary>Whether the zone owes the ground water it has not been able to land. The one
		/// reading that is a shortage rather than a texture.</summary>
		internal readonly bool Dry;

		internal KingdomAmbientReading(int turning, int stopped, bool cookedToday, int shrine, int hearth, int watch, bool dry)
		{
			Turning = turning;
			Stopped = stopped;
			CookedToday = cookedToday;
			Shrine = shrine;
			Hearth = hearth;
			Watch = watch;
			Dry = dry;
		}

		/// <summary>A city with nothing standing and nobody in it. What a book with no rows
		/// reads as.</summary>
		internal static KingdomAmbientReading Empty
		{
			get { return new KingdomAmbientReading(0, 0, false, 0, 0, 0, false); }
		}
	}

	/// <summary>
	/// Lane 3 of BUILDING-CATALOGUE-BRIEF Addendum 13: <i>"the message log breathes from model
	/// state &mdash; the mill's clatter, bread-smell, the shrine's hour, silence where the wheel
	/// stopped."</i> Pure, engine-free, and drawless.
	/// <para>
	/// <b>Three rules hold this to a texture rather than a spam channel.</b>
	/// </para>
	/// <list type="number">
	/// <item><description><b>One line, chosen, never rolled.</b> There is no draw here at all:
	/// the reading and the band decide the line, so the same city at the same hour in the same
	/// state says the same thing, and a reload never re-rolls the room.</description></item>
	/// <item><description><b>A key, not a timer.</b> Every line carries a
	/// <see cref="Key"/>, and the caller says a line only when its key differs from the last one
	/// said or the day has turned. That is Addendum 13's <i>"a line per state-change or per day,
	/// never per slice"</i> as arithmetic.</description></item>
	/// <item><description><b>The heartbeat's budget is above this.</b>
	/// LIVING-CITY-ARCHITECTURE &sect;3.6 gives the whole slice at most one told line an in-game
	/// hour, city-wide, and ambience spends out of that budget rather than beside it.</description></item>
	/// </list>
	/// </summary>
	internal static class KingdomAmbientRules
	{
		/// <summary>No line. Zero rather than -1 so a book that has never said anything and a
		/// book whose last line was "nothing" read the same, which they should.</summary>
		internal const int NoKey = 0;

		/// <summary>
		/// The city's one line for this hour, or none.
		/// <para>
		/// Ordered by what a person standing in the street would notice first, and the order is
		/// the whole design: <b>a stopped wheel outranks everything</b>, because silence where
		/// there was noise is the only ambient line that is also news (STANDARDS 7b &mdash; a
		/// thing that stalled must be able to say so). After that it is the hour's own texture,
		/// and last a line for a city that is simply standing there.
		/// </para>
		/// </summary>
		/// <param name="reading">What the rows say.</param>
		/// <param name="band">The hour, from <c>KingdomPlacementRules.BandFor</c> &mdash; the same
		/// bands the placement layer anchors people by, so the prose and the people agree about
		/// what time it is.</param>
		/// <param name="line">The line, in Qud's own register. Empty when there is nothing to
		/// say.</param>
		/// <param name="key">What was said, as a small stable number. The caller stores it and
		/// refuses to say the same thing twice inside one day.</param>
		/// <returns>True when there is a line.</returns>
		internal static bool TryLine(KingdomAmbientReading reading, KingdomDayBand band, out string line, out int key)
		{
			int index = Choose(reading, band);
			line = Text(index, reading);
			key = string.IsNullOrEmpty(line) ? NoKey : Compose(index, band);
			return key != NoKey;
		}

		/// <summary>
		/// Whether this line may be said, given what was said last and when.
		/// <para>
		/// A line repeats only across a day boundary; inside one day it must be a different line.
		/// That is what stops the mill announcing its own clatter every hour for a season, and it
		/// is checked here rather than at the call site so the rule is testable without a game.
		/// </para>
		/// </summary>
		/// <param name="key">The candidate line's key.</param>
		/// <param name="lastKey">The key the book last said.</param>
		/// <param name="dayOrdinal">Which world-day it is: <c>tick / TicksPerDay</c>.</param>
		/// <param name="lastDayOrdinal">The day the book last said anything.</param>
		internal static bool Speakable(int key, int lastKey, long dayOrdinal, long lastDayOrdinal)
		{
			if (key == NoKey)
			{
				return false;
			}
			return key != lastKey || dayOrdinal != lastDayOrdinal;
		}

		/// <summary>Which world-day a tick falls in. The stamp <see cref="Speakable"/> compares,
		/// named here so the caller and the rule cannot disagree about it.</summary>
		internal static long DayOrdinal(long tick)
		{
			long day = tick / KingdomHappeningRules.TicksPerDay;
			return (tick < 0L) ? (day - 1L) : day;
		}

		// ==================================================================================
		// The choosing
		// ==================================================================================

		/// <summary>How many lines there are. The key packs a line and a band into one int, and
		/// this is the stride that keeps them apart.</summary>
		private const int LineCount = 9;

		private static int Compose(int index, KingdomDayBand band)
		{
			// One-based on purpose: index 0 is a real line, and NoKey must stay distinguishable
			// from it.
			return ((int)band * LineCount) + index + 1;
		}

		private static int Choose(KingdomAmbientReading reading, KingdomDayBand band)
		{
			if (reading.Stopped > 0)
			{
				return 0;
			}
			if (reading.Dry)
			{
				return 1;
			}
			switch (band)
			{
			case KingdomDayBand.Rising:
				return (reading.Turning > 0) ? 2 : 3;
			case KingdomDayBand.SaltSun:
				return reading.CookedToday ? 4 : ((reading.Turning > 0) ? 2 : 3);
			case KingdomDayBand.Hindsun:
				return (reading.Shrine > 0) ? 5 : 6;
			case KingdomDayBand.JeweledDusk:
				return (reading.Hearth > 0) ? 6 : 3;
			default:
				return (reading.Watch > 0) ? 7 : 8;
			}
		}

		/// <summary>
		/// The lines themselves. Qud's own register: short, sensory, and never addressed to the
		/// founder as an instruction. Nothing here is a status report &mdash; the report is the
		/// ledger, and this is the room.
		/// </summary>
		private static string Text(int index, KingdomAmbientReading reading)
		{
			switch (index)
			{
			case 0:
				return (reading.Stopped == 1)
					? "Something has stopped turning. You can hear the water going past it."
					: "The yards have gone quiet. Nothing is turning that ought to be.";
			case 1:
				return "The cisterns knock hollow when somebody walks past them.";
			case 2:
				return "The mill takes up its clatter, and the day starts on the third beat.";
			case 3:
				return "Dust, and the sound of somebody sweeping it somewhere else.";
			case 4:
				return "Bread-smell gets over the wall before anybody does.";
			case 5:
				return "Someone is singing the hour at the shrine. Badly, and all the way through.";
			case 6:
				return "The hearths take one after another, and the market gate is shut.";
			case 7:
				return "The watch changes. Nothing on the wall but beetles.";
			default:
				return "Beetle moon over the roofs, and the whole place breathing.";
			}
		}
	}
}
