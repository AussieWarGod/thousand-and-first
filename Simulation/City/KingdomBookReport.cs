using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst.Api;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The city book, opened. Six chapters, all of them readings and none of them a control.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;5's whole complaint about the model was that it was
	/// invisible: <i>"without it the model is invisible and the whole wave is worthless."</i> W1
	/// through W4 put stocks, zones, works, residents, clocks and a told-log on the book, and until
	/// now the only way to see any of it was the perf receipt and the homecoming digest.
	/// </para>
	/// <para>
	/// One Charter entry and a chapter picker, rather than six entries: the Charter's letters are
	/// all spoken for, and a book is the shape this is anyway.
	/// </para>
	/// </summary>
	public static class KingdomBookReport
	{
		/// <summary>
		/// Opens the book and keeps it open until the founder closes it.
		/// <para>
		/// Preconditions: a founded realm. Side effects: none whatsoever &mdash; every chapter is
		/// a projection, and nothing on any of them can be pressed. Failure mode: an unreadable
		/// book says so and closes.
		/// </para>
		/// </summary>
		public static void Open(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			while (true)
			{
				KingdomCityReading reading = Read(System);
				if (reading == null)
				{
					// STANDARDS §7b. An unreadable book is a fault the founder must be told about
					// plainly, and never one that degrades into a page of zeroes.
					Popup.Show("{{r|The book of " + KingdomPresentation.Rich(System.SeatName) + " cannot be read.}}\n\nNothing has been lost — the city goes on keeping itself — but nothing about it can be shown until the book reads again. The log names the fault.");
					return;
				}
				int num = Popup.PickOption(
					Title: "The book of " + KingdomPresentation.Rich(System.SeatName),
					Intro: Headline(System, reading),
					Options: new string[6]
					{
						"The stores, and what holds them",
						"The works, and what they are waiting on",
						"The people, and where their day puts them",
						"The turn of the year",
						"What has happened here",
						"Who else writes in this book"
					},
					// One letter per chapter, each taken from the chapter's own name. Checked by
					// hand for the same reason the Charter's own inventory comment is: a duplicate
					// silently opens whichever comes first.
					Hotkeys: new char[6] { 's', 'w', 'p', 'y', 'h', 'e' },
					AllowEscape: true);
				switch (num)
				{
				case 0:
					Popup.Show(KingdomBookRules.Stores(reading, GroundName,
						KingdomPresentation.Rich));
					break;
				case 1:
					Popup.Show(KingdomBookRules.Works(reading, KingdomAsks.DisplayNameOf,
						GroundName, KingdomPresentation.Rich));
					break;
				case 2:
					Popup.Show(KingdomBookRules.Roll(reading,
						KingdomOfficeRules.ChooseTitle(System.SeatName),
						KingdomNotables.HolderName(System), KingdomPresentation.Rich)
						+ Rite(System));
					break;
				case 3:
					Popup.Show(Year(System));
					break;
				case 4:
					Popup.Show(Happened(System));
					break;
				case 5:
					Popup.Show(Writers());
					break;
				default:
					return;
				}
			}
		}

		/// <summary>The reading the whole book is drawn from, or null when the book will not
		/// read.</summary>
		internal static KingdomCityReading Read(KingdomSystem System)
		{
			KingdomCityReading reading = null;
			KingdomSystem.Guard("city book reading", delegate
			{
				KingdomCityState state;
				KingdomCityFault fault;
				if (System.City != null && System.City.TryRead(out state, out fault))
				{
					reading = KingdomReadingRules.Project(KingdomPresentation.Rich(System.SeatName), state,
						System.City.ExtensionModel);
				}
			});
			return reading;
		}

		/// <summary>
		/// The line above the chapter list: how far the model has been carried, and how long ago
		/// that was. A book whose date the founder cannot see is a book they cannot judge.
		/// </summary>
		private static string Headline(KingdomSystem System, KingdomCityReading reading)
		{
			long now = (The.Game == null) ? 0L : The.Game.TimeTicks;
			long behind = now - reading.ProcessedThroughTick;
			int days = (behind > 0L) ? KingdomRules.ElapsedDays(behind) : 0;
			return "Carried through tick " + reading.ProcessedThroughTick
				+ ((days > 0) ? (" {{K|(" + days + ((days == 1) ? " day" : " days") + " behind now)}}") : " {{K|(current)}}")
				+ "\n" + reading.ZoneCount + ((reading.ZoneCount == 1) ? " parasang" : " parasangs")
				+ ", " + reading.WorkCount + ((reading.WorkCount == 1) ? " work" : " works")
				+ ", " + reading.LivingCount + " living.";
		}

		/// <summary>
		/// How many of the citizens standing here will share water with the founder, appended to
		/// the people chapter. Lane 1 of BUILDING-CATALOGUE-BRIEF Addendum 13 has a state, and a
		/// state nobody can see is a state nobody can tell is broken.
		/// </summary>
		private static string Rite(KingdomSystem System)
		{
			string line = KingdomCitizenRite.DumpLine(System, The.Player?.CurrentZone);
			return string.IsNullOrEmpty(line) ? "" : ("\n\n{{K|" + line + ".}}");
		}

		// ==================================================================================
		// The turn of the year -- Qud's own calendar, and the heart's rung
		// ==================================================================================

		private static string Year(KingdomSystem System)
		{
			StringBuilder builder = new StringBuilder();
			long now = (The.Game == null) ? 0L : The.Game.TimeTicks;
			builder.Append("It is the ").Append(XRL.World.Calendar.GetDay()).Append(" of ")
				.Append(XRL.World.Calendar.GetMonth()).Append(", ").Append(XRL.World.Calendar.GetYear()).Append(" AR.");
			long due;
			KingdomFestivalAnchor anchor;
			if (KingdomHappeningRules.TryNextFestival(now, out due, out anchor))
			{
				int days = KingdomRules.ElapsedDays(due - now);
				builder.Append("\n\nThe next feast is ").Append(KingdomHappeningRules.AnchorName(anchor))
					.Append((days <= 0) ? ", and it is today." : (", in " + days + ((days == 1) ? " day." : " days.")));
				string dish = (System.DishName ?? "").Trim();
				builder.Append(string.IsNullOrEmpty(dish)
					? "\n{{K|The kitchens have not settled on what the city is known for yet.}}"
					: ("\n{{K|" + KingdomPresentation.Rich(System.SeatName) + " keeps it with " + dish + ".}}"));
				builder.Append("\n{{K|Stand in the city on the day and you will hear it kept; be elsewhere and you will read about it.}}");
			}
			int rung = Rung(System);
			// The rung's own name is resolved once and checked once: a catalogue that another mod
			// retitled or removed the rung from answers null, and ": ." is not a sentence.
			string standing = (rung <= 0) ? null : KingdomAsks.DisplayNameOf(KingdomPlotRules.HeartKeyForRung(rung));
			builder.Append("\n\n").Append((rung <= 0)
				? "Nothing stands on the rite ground yet."
				: ("The heart stands at its " + Ordinal(rung) + " rung"
					+ (string.IsNullOrEmpty(standing) ? "." : (": " + standing + "."))));
			if (rung > 0 && rung < KingdomPlotRules.HeartRungKeys.Length)
			{
				string next = KingdomAsks.DisplayNameOf(KingdomPlotRules.HeartKeyForRung(rung + 1));
				builder.Append("\n{{K|Above it, when the city can carry one: ").Append(string.IsNullOrEmpty(next) ? "another rung" : next).Append(".}}");
			}
			return builder.ToString();
		}

		/// <summary>The rung standing on this realm's rite ground. Read off the seated zone when
		/// the founder is on it, and off nothing otherwise: the heart is ground, and ground the
		/// founder is not standing on is not asked to load itself to answer a report.</summary>
		private static int Rung(KingdomSystem System)
		{
			Zone here = The.Player?.CurrentZone;
			if (here == null || System.ClaimedZones == null || !System.ClaimedZones.Contains(here.ZoneID))
			{
				return 0;
			}
			return KingdomPlots.HeartRung(here);
		}

		private static string Ordinal(int rung)
		{
			switch (rung)
			{
			case 1:
				return "first";
			case 2:
				return "second";
			case 3:
				return "third";
			case 4:
				return "fourth";
			default:
				return rung + "th";
			}
		}

		// ==================================================================================
		// What has happened here -- the told-log ring, read rather than digested
		// ==================================================================================

		private static string Happened(KingdomSystem System)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			if (System.City == null || !System.City.TryRead(out state, out fault) || state.ToldCount <= 0)
			{
				return "Nothing has been written here yet. The city is young, or it has been quiet.";
			}
			int[] counts = new int[16];
			long[] latest = new long[16];
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow row;
				if (!state.TryTold(i, out row))
				{
					continue;
				}
				int kind = (int)row.Kind;
				if (kind < 0 || kind >= counts.Length)
				{
					continue;
				}
				counts[kind]++;
				if (row.Tick > latest[kind])
				{
					latest[kind] = row.Tick;
				}
			}
			long now = (The.Game == null) ? 0L : The.Game.TimeTicks;
			StringBuilder builder = new StringBuilder();
			builder.Append("The last ").Append(state.ToldCount).Append(" things ").Append(KingdomPresentation.Rich(System.SeatName)).Append(" remembers:");
			for (int kind = 0; kind < counts.Length; kind++)
			{
				string line = KingdomHappeningRules.ToldLine((KingdomToldKind)kind, counts[kind]);
				if (string.IsNullOrEmpty(line))
				{
					continue;
				}
				int days = (now > latest[kind]) ? KingdomRules.ElapsedDays(now - latest[kind]) : 0;
				builder.Append("\n  ").Append(line).Append(" {{K|(the last ")
					.Append((days <= 0) ? "today" : (days + ((days == 1) ? " day ago" : " days ago"))).Append(")}}");
			}
			builder.Append("\n\n{{K|The city keeps ").Append(KingdomCityState.MaxToldEntries)
				.Append(" of these at a time. What falls off the end is in the chronicle.}}");
			return builder.ToString();
		}

		// ==================================================================================
		// Who else writes in this book -- the published contract, from the founder's side
		// ==================================================================================

		private static string Writers()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("This city's model is a published contract, at version ")
				.Append(KingdomExtensions.Version).Append(".");
			if (!KingdomExtensions.Enabled)
			{
				builder.Append("\n\n{{K|You have turned the behaviour lane off. Other mods can still add buildings, deals, works and settlers through their data files; none of them may run code against your city.}}");
				return builder.ToString();
			}
			List<string> admitted = KingdomExtensions.Admitted();
			builder.Append((admitted.Count == 0)
				? "\n\n{{K|Nothing is extending it. The city is entirely its own.}}"
				: ("\n\nWriting in it:"));
			for (int i = 0; i < admitted.Count; i++)
			{
				builder.Append("\n  {{W|").Append(admitted[i]).Append("}}");
			}
			List<string> refused = KingdomExtensions.Refusals();
			if (refused.Count > 0)
			{
				builder.Append("\n\n{{R|Refused:}}");
				for (int i = 0; i < refused.Count; i++)
				{
					builder.Append("\n  {{r|").Append(refused[i]).Append("}}");
				}
			}
			return builder.ToString();
		}

		private static string GroundName(string zoneId)
		{
			if (string.IsNullOrEmpty(zoneId) || The.ZoneManager == null)
			{
				return null;
			}
			Zone here = The.Player?.CurrentZone;
			if (here != null && here.ZoneID == zoneId)
			{
				return "Here, where you are standing";
			}
			// Named from the id and never fetched: GetZone builds ground that is not resident, and
			// a report that materialises a parasang to write a heading would be the most expensive
			// sentence in the mod.
			string name = The.ZoneManager.GetZoneDisplayName(zoneId, WithIndefiniteArticle: true);
			return string.IsNullOrEmpty(name) ? null : XRL.Language.Grammar.InitCap(name);
		}
	}
}
