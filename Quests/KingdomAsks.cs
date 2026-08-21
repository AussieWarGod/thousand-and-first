using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;
using ThousandAndFirst.Api;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>
	/// The asks board: what the city wants, in one place, read and never pressed.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;5 and &sect;7.4 W5 &mdash; <i>"the city asks you for things,
	/// and another mod can teach it a new thing to ask for."</i> The board is the surfacing half of
	/// BUILDING-CATALOGUE-BRIEF Addendum 13's lane 8: petitions become quest-shaped entries a
	/// founder can read, and the verbs that answer them are the Charter's own, already built.
	/// </para>
	/// <para>
	/// <b>Nothing on this screen can be pressed</b>, which is the rule
	/// <c>_notes/DIVERSITY-AND-TECH-TREES.md</c> &sect;2.2 states for every reading surface and
	/// which VISION's "not a second job" pillar requires. No accept, no queue, no timer, no
	/// reward: every ask names what it is and what would settle it, and the founder decides
	/// whether they care.
	/// </para>
	/// <para>
	/// <b>The city's own voice goes through the published contract.</b> <see cref="CityAsks"/>
	/// implements <c>IKingdomAskSource</c> exactly as a third-party mod's source does, and crosses
	/// the same executor seam. A contract with one implementation is indistinguishable from that
	/// implementation's accidents (&sect;6.6); dogfooding it here is how we find out.
	/// </para>
	/// </summary>
	public static class KingdomAsks
	{
		/// <summary>Rides the petitions gate: the board's first line IS the standing petition, and
		/// a board offered while petitions are off would be a second petition surface.</summary>
		public static bool Enabled
		{
			get { return KingdomPetitions.Enabled; }
		}

		/// <summary>
		/// The whole board as the founder reads it.
		/// <para>
		/// Preconditions: a founded realm. Side effects: none &mdash; this reads the book and
		/// writes nothing back. Failure mode: any fault degrades to the plainest honest line
		/// ("the city is asking for nothing"), never to a blank screen.
		/// </para>
		/// </summary>
		public static string Board(KingdomSystem System)
		{
			StringBuilder builder = new StringBuilder();
			if (System == null || !System.Founded)
			{
				return "You rule nothing yet.";
			}
			builder.Append("{{C|").Append(System.SeatName).Append("}} is asking for the following.");
			if (!Enabled)
			{
				// STANDARDS §7b: applicable but blocked. A board that silently showed nothing
				// while the option that feeds it was off would read as a contented city.
				return builder.ToString()
					+ "\n\n{{K|You have turned petitions off, so the city keeps its complaints to itself.}}";
			}
			Petition(System, builder);
			List<string> stalled = new List<string>();
			bool read;
			List<KingdomAsk> asks = Gather(System, stalled, out read);
			if (!read)
			{
				// STANDARDS §7b again, and the sharpest case of it: an unreadable book reported as
				// a contented city is the report telling the founder the opposite of the truth.
				builder.Append("\n\n{{r|The city's own book could not be read, so nothing below it is the city speaking. What it is asking for is unknown until the book reads again.}}");
			}
			else if (asks.Count == 0)
			{
				builder.Append("\n\n{{K|Nothing else. The stores hold, the roofs are enough, and every work that wants hands has them.}}");
			}
			for (int i = 0; i < asks.Count; i++)
			{
				Line(System, asks[i], builder);
			}
			Stalls(stalled, builder);
			Posted(System, builder);
			builder.Append("\n\n{{K|Nothing here is taken up at this table. The Charter's own entries answer them.}}");
			return builder.ToString();
		}

		/// <summary>
		/// Every ask the city has, the city's own and its extensions', in board order.
		/// <para>
		/// Both halves cross <c>KingdomExecutor.Submit</c>, so neither can stall the other and
		/// neither can reach the ground. Ours run first and sort first among equals, so a founder
		/// always finds the lines they know in the same place.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Stalled">Collects the mod names of sources that faulted, so the board can
		/// say so rather than quietly showing fewer lines.</param>
		/// <param name="Read">False when the city's own book could not be read at all &mdash; which
		/// is a different thing from a city with nothing to ask for, and must not be reported as
		/// one.</param>
		internal static List<KingdomAsk> Gather(KingdomSystem System, List<string> Stalled, out bool Read)
		{
			List<KingdomAsk> asks = new List<KingdomAsk>();
			bool read = false;
			if (System == null || !Enabled)
			{
				Read = false;
				return asks;
			}
			KingdomSystem.Guard("asks board", delegate
			{
				KingdomCityState state;
				KingdomCityFault fault = KingdomCityFault.None;
				if (System.City == null || !System.City.TryRead(out state, out fault))
				{
					KingdomLog.Log("asks board: the book would not read (" + fault + ")");
					return;
				}
				read = true;
				KingdomCityReading reading = KingdomReadingRules.Project(System.SeatName, state);
				asks.AddRange(KingdomExtensions.Run(System, reading, Own, System.SeatName, true, Stalled));
				asks.AddRange(KingdomExtensions.Asks(System, reading, Stalled));
				// Ordered once, over the whole board. Gathering ours first is about isolation, not
				// about rank: a mod's grave ask must not sort below the city's passing one merely
				// because of the order the sources were asked in.
				KingdomAskRules.SortBoard(asks);
				// And capped once, over the whole board. The cap is a promise about the SCREEN —
				// VISION forbids a spreadsheet — so ten installed mods must not turn it into one.
				// Worst-first is what makes the trim honest: what survives is what matters.
				if (asks.Count > KingdomAskRules.MaxAsks)
				{
					asks.RemoveRange(KingdomAskRules.MaxAsks, asks.Count - KingdomAskRules.MaxAsks);
				}
			});
			Read = read;
			return asks;
		}

		/// <summary>Names the sources that broke while this board was being built, once, where the
		/// founder will see it. A log line is not a surface.</summary>
		private static void Stalls(List<string> stalled, StringBuilder builder)
		{
			if (stalled == null || stalled.Count == 0)
			{
				return;
			}
			builder.Append("\n\n{{r|Not everything writing in this book answered: ");
			for (int i = 0; i < stalled.Count; i++)
			{
				builder.Append((i == 0) ? "" : ", ").Append(stalled[i]);
			}
			builder.Append(" stalled this reading. The city is unaffected, and the log names the fault.}}");
		}

		/// <summary>The name on the building for a catalogue key, or null when the registry has
		/// never heard of it &mdash; which is the ordinary case for a key another mod merged in and
		/// then removed.</summary>
		internal static string DisplayNameOf(string DesignKey)
		{
			if (string.IsNullOrEmpty(DesignKey))
			{
				return null;
			}
			List<KingdomRules.BuildEntry> buildings = KingdomData.Buildings;
			for (int i = 0; buildings != null && i < buildings.Count; i++)
			{
				if (buildings[i] != null && buildings[i].Key == DesignKey)
				{
					return string.IsNullOrEmpty(buildings[i].DisplayName) ? DesignKey : buildings[i].DisplayName;
				}
			}
			return null;
		}

		private static void Petition(KingdomSystem System, StringBuilder builder)
		{
			if (System.PetitionKind == KingdomRules.PetitionKind.None)
			{
				return;
			}
			builder.Append("\n\n{{W|").Append(System.PetitionPetitioner).Append(" is waiting to speak}} — about ")
				.Append(KingdomPetitions.Subject(System.PetitionKind)).Append(".\n  {{K|Hear them at the Charter.}}");
		}

		private static void Line(KingdomSystem System, KingdomAsk ask, StringBuilder builder)
		{
			builder.Append("\n\n").Append(Mark(ask.Weight)).Append(" ").Append(ask.Title);
			if (!string.IsNullOrEmpty(ask.Want))
			{
				builder.Append("\n  {{K|").Append(ask.Want).Append("}}");
			}
			string where = Where(System, ask.ZoneId);
			if (!string.IsNullOrEmpty(where))
			{
				builder.Append("\n  {{K|").Append(where).Append("}}");
			}
		}

		/// <summary>
		/// Where an ask is, in the founder's words rather than a zone id. A zone the founder is
		/// standing in says so; anywhere else is named the way the engine names ground.
		/// </summary>
		private static string Where(KingdomSystem System, string ZoneId)
		{
			if (string.IsNullOrEmpty(ZoneId))
			{
				return "";
			}
			Zone here = The.Player?.CurrentZone;
			if (here != null && here.ZoneID == ZoneId)
			{
				return "Here, where you are standing.";
			}
			// Named from the id, never by fetching the zone: GetZone BUILDS ground that is not
			// resident, and a reading surface that materialises a parasang to write one line of
			// prose would be the most expensive sentence in the mod.
			string name = (The.ZoneManager == null) ? null : The.ZoneManager.GetZoneDisplayName(ZoneId, WithIndefiniteArticle: true);
			return string.IsNullOrEmpty(name) ? "" : ("At " + name + ".");
		}

		private static string Mark(KingdomAskWeight weight)
		{
			switch (weight)
			{
			case KingdomAskWeight.Grave:
				return "{{R|!!}}";
			case KingdomAskWeight.Pressing:
				return "{{W|!}}";
			default:
				return "{{K|·}}";
			}
		}

		private static void Posted(KingdomSystem System, StringBuilder builder)
		{
			Zone zone = The.Player?.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			List<GameObject> notices = KingdomBounty.Notices(zone);
			if (notices == null || notices.Count == 0)
			{
				return;
			}
			builder.Append("\n\n{{K|And ").Append(notices.Count)
				.Append((notices.Count == 1) ? " notice of your own stands" : " notices of your own stand")
				.Append(" at the heart, where the city can read them.}}");
		}

		// ==================================================================================
		// The city's own source, through the published contract (§6.6)
		// ==================================================================================

		/// <summary>The city's own source. One instance: it holds nothing.</summary>
		private static readonly CityAsks Own = new CityAsks();

		/// <summary>
		/// The city speaking for itself, as an extension. Registered by hand rather than by the
		/// marker attribute: the attribute scan exists so a THIRD PARTY needs no line of ours, and
		/// discovering our own class through it would make the mod's own behaviour depend on a
		/// reflection cache it does not need.
		/// </summary>
		internal sealed class CityAsks : IKingdomAskSource
		{
			public int ApiVersion
			{
				get { return KingdomApiRules.Version; }
			}

			public KingdomAsk[] Ask(KingdomCityReading City, IKingdomDraws Draws)
			{
				return KingdomAskRules.Derive(City, DisplayNameOf);
			}
		}
	}
}
