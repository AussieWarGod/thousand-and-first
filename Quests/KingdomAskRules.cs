using System;
using System.Collections.Generic;
using ThousandAndFirst.Api;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>
	/// What the city asks its founder for, derived from the model and from nothing else.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;5: <i>"petitions issue from model state: the granary is full
	/// and nothing hauls it &hellip; today petitions are decorative; against a model they are the
	/// city talking."</i> This is that derivation, and it is a RENDERING in the strict sense
	/// BUILDING-CATALOGUE-BRIEF Addendum 13 requires: it reads the published reading, holds no
	/// state, schedules nothing, and every condition it names is a row the model already keeps for
	/// its own arithmetic.
	/// </para>
	/// <para>
	/// <b>No new balance number appears in this file, on purpose.</b> Every ask fires on a
	/// structural fact &mdash; a stock at zero, a store at its ceiling, more people than roofs, a
	/// work past the condemned line &mdash; rather than on a tuned threshold. A reading surface
	/// that invented its own economy would be a second economy, and the first thing that would go
	/// wrong is that it would disagree with the ledger.
	/// </para>
	/// <para>
	/// It runs against <c>KingdomCityReading</c>, the PUBLISHED reading, rather than against the
	/// internal state it could reach from here. That is deliberate dogfooding: the city's own asks
	/// go through the same contract a third-party mod's do, so a gap in the contract is a gap in
	/// our own board first.
	/// </para>
	/// </summary>
	internal static class KingdomAskRules
	{
		/// <summary>The board's own ceiling. A list longer than this is a spreadsheet, and VISION
		/// forbids one. Purely a surface cap: nothing is dropped from the model.</summary>
		internal const int MaxAsks = 8;

		/// <summary>Our own asks' filing prefix, so a founder reading the board can tell the
		/// city's own voice from a mod's.</summary>
		internal const string OwnKindPrefix = "city.";

		/// <summary>
		/// Everything the city is asking for, worst first.
		/// <para>
		/// Preconditions: none. A null reading yields an empty array. Side effects: none. Failure
		/// mode: none &mdash; total over any representable reading.
		/// </para>
		/// <para>
		/// Ordering is weight-descending, then by the fixed kind order below, then by the row that
		/// raised it. Fully determined by the reading: two founders with the same book read the
		/// same board in the same order, and a reload does not shuffle it.
		/// </para>
		/// </summary>
		/// <param name="City">The published reading of the city book.</param>
		/// <param name="WorkName">Turns a design key into the name the founder sees on the
		/// building, or null to fall back to the key. A delegate rather than a catalogue lookup
		/// because these rules are engine-free and the catalogue is not.</param>
		internal static KingdomAsk[] Derive(KingdomCityReading City, Func<string, string> WorkName = null)
		{
			List<KingdomAsk> asks = new List<KingdomAsk>();
			if (City == null)
			{
				return new KingdomAsk[0];
			}
			Empty(City, asks);
			Roofs(City, asks);
			Stopped(City, asks, WorkName);
			Backed(City, asks);
			SortBoard(asks);
			if (asks.Count > MaxAsks)
			{
				asks.RemoveRange(MaxAsks, asks.Count - MaxAsks);
			}
			return asks.ToArray();
		}

		/// <summary>
		/// Where a kind sorts among asks of equal weight. A fixed table rather than a string
		/// compare, so the board's order is a decision somebody made rather than an accident of
		/// the alphabet.
		/// </summary>
		internal static int KindOrder(string Kind)
		{
			switch (Kind)
			{
			case OwnKindPrefix + "thirst":
				return 0;
			case OwnKindPrefix + "hunger":
				return 1;
			case OwnKindPrefix + "shelter":
				return 2;
			case OwnKindPrefix + "stopped":
				return 3;
			case OwnKindPrefix + "haulage":
				return 4;
			default:
				// Everything a mod taught the city sorts after everything the city says itself,
				// among asks of the same weight. Not a judgment about worth: the founder needs a
				// stable place to look for the lines they already know.
				return 100;
			}
		}

		/// <summary>
		/// Puts a whole board in order: worst first, then by the fixed kind order, then by kind and
		/// title. Used on the city's own asks and again on the board once an extension's have been
		/// added, so a mod's grave ask is not ranked below the city's passing one merely because
		/// ours were gathered first.
		/// <para>
		/// Fully determined by the asks themselves, so two founders with the same book and the same
		/// mods read the same board in the same order, and a reload does not shuffle it.
		/// </para>
		/// </summary>
		internal static void SortBoard(List<KingdomAsk> Board)
		{
			if (Board != null)
			{
				Board.Sort(Compare);
			}
		}

		private static int Compare(KingdomAsk a, KingdomAsk b)
		{
			if (a.Weight != b.Weight)
			{
				return ((int)b.Weight).CompareTo((int)a.Weight);
			}
			int order = KindOrder(a.Kind).CompareTo(KindOrder(b.Kind));
			if (order != 0)
			{
				return order;
			}
			int kind = string.CompareOrdinal(a.Kind ?? "", b.Kind ?? "");
			if (kind != 0)
			{
				return kind;
			}
			int title = string.CompareOrdinal(a.Title ?? "", b.Title ?? "");
			if (title != 0)
			{
				return title;
			}
			// Ground last, so the order is TOTAL. List.Sort is an introsort and is not stable, so
			// two asks that compared equal would be at the mercy of the algorithm rather than of
			// the model - and two idle mills on different parasangs read the same title.
			return string.CompareOrdinal(a.ZoneId ?? "", b.ZoneId ?? "");
		}

		// ==================================================================================
		// A stock at zero. Not a threshold: the floor.
		// ==================================================================================

		private static void Empty(KingdomCityReading city, List<KingdomAsk> asks)
		{
			if (city.Water.Capacity > 0L && city.Water.Level <= 0L)
			{
				asks.Add(new KingdomAsk(OwnKindPrefix + "thirst",
					"The cisterns are dry.",
					"Pour water into a dedicated vessel, or set more of the detail on the water.",
					null, KingdomAskWeight.Grave));
			}
			if (city.Food.Capacity > 0L && city.Food.Level <= 0L && city.LivingCount > 0)
			{
				asks.Add(new KingdomAsk(OwnKindPrefix + "hunger",
					"The larders are bare.",
					"Bring food to a dedicated larder, or put hands back on the fields.",
					null, KingdomAskWeight.Grave));
			}
		}

		// ==================================================================================
		// More people than roofs. The model's own count either side.
		// ==================================================================================

		private static void Roofs(KingdomCityReading city, List<KingdomAsk> asks)
		{
			int living = city.LivingCount;
			if (living <= 0)
			{
				return;
			}
			int roofs = 0;
			for (int i = 0; i < city.ZoneCount; i++)
			{
				KingdomZoneReading zone;
				if (city.TryZone(i, out zone))
				{
					roofs += (zone.Roofs > 0) ? zone.Roofs : 0;
				}
			}
			if (roofs >= living)
			{
				return;
			}
			int shortfall = living - roofs;
			asks.Add(new KingdomAsk(OwnKindPrefix + "shelter",
				(shortfall == 1)
					? "One of us sleeps where they can."
					: (shortfall + " of us sleep where we can."),
				"Raise " + ((shortfall == 1) ? "a bed" : (shortfall + " more beds")) + " on ground the city holds.",
				null, KingdomAskWeight.Pressing));
		}

		// ==================================================================================
		// A work that has stopped being one. The same two clauses the breakdown happening
		// already fires on, read forward instead of after the fact -- one definition of
		// "stopped", in KingdomHappeningRules, and this asks it rather than restating it.
		// ==================================================================================

		private static void Stopped(KingdomCityReading city, List<KingdomAsk> asks, Func<string, string> workName)
		{
			for (int i = 0; i < city.WorkCount; i++)
			{
				KingdomWorkReading work;
				if (!city.TryWork(i, out work))
				{
					continue;
				}
				if (work.ConditionPercent <= KingdomHappeningRules.BreakdownConditionFloor)
				{
					asks.Add(new KingdomAsk(OwnKindPrefix + "stopped",
						Name(work, workName) + " is worn past mending itself.",
						"Set a crew to mend it, or take it down for what it is made of.",
						work.ZoneId, KingdomAskWeight.Pressing));
					continue;
				}
				if (work.CrewAssigned <= 0 && KingdomHappeningRules.NeedsHands(KingdomReadingRules.Kind(work.Class)))
				{
					asks.Add(new KingdomAsk(OwnKindPrefix + "stopped",
						Name(work, workName) + " stands with nobody on it.",
						"Put hands on it, or let it be taken down for the timber.",
						work.ZoneId, KingdomAskWeight.Passing));
				}
			}
		}

		// ==================================================================================
		// A store at its ceiling while another zone has room. Addendum 12(f)'s haulage, asked
		// for rather than assumed: the city cannot move it and is saying so.
		// ==================================================================================

		private static void Backed(KingdomCityReading city, List<KingdomAsk> asks)
		{
			for (int i = 0; i < city.ZoneCount; i++)
			{
				KingdomZoneReading zone;
				if (!city.TryZone(i, out zone) || zone.Food.Capacity <= 0L || zone.Food.Room > 0L)
				{
					continue;
				}
				if (!RoomElsewhere(city, i))
				{
					continue;
				}
				asks.Add(new KingdomAsk(OwnKindPrefix + "haulage",
					"A larder is full to the lid, and there is room for it elsewhere.",
					"Set hands to haulage, or raise another larder where the food is grown.",
					zone.ZoneId, KingdomAskWeight.Passing));
			}
		}

		private static bool RoomElsewhere(KingdomCityReading city, int exceptIndex)
		{
			for (int i = 0; i < city.ZoneCount; i++)
			{
				KingdomZoneReading other;
				if (i == exceptIndex || !city.TryZone(i, out other))
				{
					continue;
				}
				if (other.Food.Room > 0L)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// What a work is called on the board: the name the founder sees on the building when the
		/// caller can resolve one, and the design key when it cannot. The model carries no display
		/// name of its own &mdash; appearance stays on the object (&sect;1.2(c)) &mdash; so this is
		/// the honest fallback rather than a second name store.
		/// </summary>
		internal static string Name(KingdomWorkReading Work, Func<string, string> Resolve)
		{
			string resolved = (Resolve == null || string.IsNullOrEmpty(Work.DesignKey)) ? null : Resolve(Work.DesignKey);
			if (!string.IsNullOrEmpty(resolved))
			{
				return Capitalised(resolved);
			}
			return string.IsNullOrEmpty(Work.DesignKey) ? "A work" : ("The " + Work.DesignKey);
		}

		/// <summary>First letter up, and nothing else touched. Local rather than the engine's
		/// <c>Grammar.InitCap</c> because these rules are engine-free by construction, and one
		/// character is not worth the dependency.</summary>
		private static string Capitalised(string text)
		{
			if (string.IsNullOrEmpty(text) || text[0] < 'a' || text[0] > 'z')
			{
				return text;
			}
			return (char)(text[0] - 32) + text.Substring(1);
		}
	}
}
