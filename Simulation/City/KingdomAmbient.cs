using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts.Mutation;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What the city says when nothing in particular has happened: the hour's own texture
	/// (BUILDING-CATALOGUE-BRIEF Addendum 13 lane 3) and what its creed makes of the founder's own
	/// body (lane 2).
	/// <para>
	/// Two lanes in one file because they are the same shape: both are a <b>rendering</b> of state
	/// that is already there, both are chosen rather than rolled, both are keyed so they are said
	/// once per state-change, and both spend out of the heartbeat's one told line an in-game hour
	/// (LIVING-CITY-ARCHITECTURE &sect;3.6) rather than opening a channel of their own. The
	/// arithmetic and the prose are in <see cref="KingdomAmbientRules"/> and
	/// <see cref="KingdomNatureRules"/>, which are engine-free; this is the reading.
	/// </para>
	/// </summary>
	public static class KingdomAmbient
	{
		// ==================================================================================
		// Lane 3 — the room
		// ==================================================================================

		/// <summary>
		/// One line about the hour, or none. Spends a told line only when the line is one the city
		/// has not already said today.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="book">The city's book, which also carries what it last said.</param>
		/// <param name="label">The city's display name.</param>
		/// <param name="here">Whether the founder is standing in it.</param>
		/// <param name="nowTick">Now.</param>
		/// <returns>1 when a line was pushed, 0 otherwise.</returns>
		internal static int Speak(KingdomSystem System, KingdomCityBook book, string label, bool here, long nowTick)
		{
			if (!KingdomHappenings.Enabled || System == null || book == null || nowTick <= 0L)
			{
				return 0;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!book.TryRead(out state, out fault))
			{
				return 0;
			}
			string line;
			int key;
			if (!KingdomAmbientRules.TryLine(Read(state, nowTick), KingdomPlacementRules.BandFor(nowTick), out line, out key))
			{
				return 0;
			}
			long day = KingdomAmbientRules.DayOrdinal(nowTick);
			if (!KingdomAmbientRules.Speakable(key, book.AmbientKey, day, book.AmbientDayOrdinal))
			{
				return 0;
			}
			book.AmbientKey = key;
			book.AmbientDayOrdinal = day;
			KingdomWord.Ambient(System, label, here, line);
			return 1;
		}

		/// <summary>
		/// The city as a set of counts, off rows the model already keeps. Bounded by rows and never
		/// by elapsed: one pass over works and one over residents, which is 2R and is what the
		/// heartbeat's amortised budget is written in (&sect;0.0).
		/// </summary>
		internal static KingdomAmbientReading Read(KingdomCityState state, long nowTick)
		{
			if (state == null)
			{
				return KingdomAmbientReading.Empty;
			}
			int turning = 0;
			int stopped = 0;
			bool cooked = false;
			long since = nowTick - KingdomHappeningRules.TicksPerDay;
			for (int i = 0; i < state.WorkCount; i++)
			{
				KingdomWorkRow row;
				if (!state.TryWork(i, out row) || row.WorkId <= 0)
				{
					continue;
				}
				if (KingdomHappeningRules.Broken(row))
				{
					stopped++;
					continue;
				}
				if (KingdomHappeningRules.NeedsHands(row.RunState.Kind))
				{
					turning++;
					cooked |= row.RanThroughTick >= since;
				}
			}
			int shrine = 0;
			int hearth = 0;
			int watch = 0;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row) || row.Standing != KingdomResidentStanding.Resident)
				{
					continue;
				}
				switch (row.DayShape)
				{
				case KingdomDayShape.Shrine:
					shrine++;
					break;
				case KingdomDayShape.Watch:
					watch++;
					break;
				case KingdomDayShape.Hearth:
					hearth++;
					break;
				}
			}
			KingdomStocks stocks;
			bool dry = KingdomCityRules.TryCityStocks(state, out stocks) && stocks.Water.Capacity > 0L && stocks.Water.Level <= 0L;
			return new KingdomAmbientReading(turning, stopped, cooked, shrine, hearth, watch, dry);
		}

		// ==================================================================================
		// Lane 2 — what the creed makes of you
		// ==================================================================================

		/// <summary>
		/// What this city's creed says about the founder's own body, said once per state-change.
		/// <para>
		/// <b>Never a mechanic.</b> Nothing here moves standing, refuses a settler, or changes what
		/// the settlement produces &mdash; it is a line, in the same push lane the ambience uses,
		/// and a clause in the book. Addendum 13 lane 2 asks for wonder and fear, and this is
		/// people talking.
		/// </para>
		/// </summary>
		/// <returns>1 when a line was pushed, 0 otherwise.</returns>
		internal static int Regard(KingdomSystem System, KingdomCityBook book, string creedFactionName, string label, bool here)
		{
			if (!KingdomHappenings.Enabled || System == null || book == null || The.Player == null)
			{
				return 0;
			}
			KingdomFounderNature nature = NatureOf(The.Player, creedFactionName);
			int key = KingdomNatureRules.RegardKey(creedFactionName, nature);
			if (key == KingdomNatureRules.NoKey || key == book.RegardKey)
			{
				return 0;
			}
			book.RegardKey = key;
			string creed = KingdomCreed.CreedName(creedFactionName);
			string city = KingdomPresentation.Rich(KingdomWord.CityName(System, label));
			string line = KingdomNatureRules.RegardLine(nature, creed, city);
			if (string.IsNullOrEmpty(line))
			{
				return 0;
			}
			string telling = KingdomNatureRules.RegardTelling(nature, creed, city,
				KingdomPresentation.Rich(KingdomChronicle.FounderName()));
			if (!string.IsNullOrEmpty(telling))
			{
				KingdomChronicle.Record(System, telling);
			}
			KingdomWord.Ambient(System, label, here, KingdomVoices.Say(System, VoiceOccasion.FounderRegarded, line));
			KingdomLog.Log("regard: creed=" + (creedFactionName ?? "-") + " part=" + (string.IsNullOrEmpty(nature.RegardedPart) ? "-" : nature.RegardedPart)
				+ " feeling=" + nature.PartFeeling + " chrome=" + nature.Chrome + " verdict=" + KingdomNatureRules.Judge(nature));
			return 1;
		}

		/// <summary>
		/// The founder as vanilla describes them, read the same derived way a settler's
		/// quality-of-life profile is (<c>KingdomQol.TruthOf</c>): every field below is a read
		/// vanilla itself performs somewhere, so a genotype, a mutation or an implant another mod
		/// ships answers correctly without that mod knowing this system exists.
		/// </summary>
		internal static KingdomFounderNature NatureOf(GameObject Founder, string CreedFactionName)
		{
			if (Founder == null)
			{
				return KingdomFounderNature.Unremarkable;
			}
			List<GameObject> chrome = Founder.GetInstalledCyberneticsReadonly();
			string part = null;
			int feeling = 0;
			bool revere = false;
			bool refuse = false;
			Faction creed = string.IsNullOrEmpty(CreedFactionName) ? null : Factions.GetIfExists(CreedFactionName);
			if (creed != null)
			{
				// Faction.PartReputation is the game's OWN table of which factions admire or fear
				// which bodies (D/XRL/World/Faction.cs:150, loaded at D/XRL/World/Factions.cs:664-670
				// and folded into every reputation read at D/XRL/World/Reputation.cs:142-150). We
				// read it and write nothing: the strongest feeling the creed has about anything the
				// founder carries is the one they talk about.
				if (creed.PartReputation != null)
				{
					foreach (KeyValuePair<string, int> entry in creed.PartReputation)
					{
						if (!Founder.HasPart(entry.Key) || Math.Abs(entry.Value) <= Math.Abs(feeling))
						{
							continue;
						}
						feeling = entry.Value;
						part = PartPhrase(Founder, entry.Key);
					}
				}
				ChromeInterest(creed, out revere, out refuse);
			}
			return new KingdomFounderNature(
				Founder.GetGenotype(),
				(chrome == null) ? 0 : chrome.Count,
				part,
				feeling,
				revere,
				refuse);
		}

		/// <summary>
		/// Whether this creed's own <c>&lt;interests&gt;</c> list has an opinion about chrome.
		/// Vanilla's <c>Inverse</c> flag is the whole of "and they define themselves against it"
		/// &mdash; the Putus Templar list <c>cybernetics</c> twice, once inverted under "the modern
		/// world" and once plainly (<c>B/Factions.xml:1271-1272</c>), which is why the inverted
		/// reading wins in <c>KingdomNatureRules.Judge</c>.
		/// </summary>
		private static void ChromeInterest(Faction Creed, out bool Revere, out bool Refuse)
		{
			Revere = false;
			Refuse = false;
			if (Creed.Interests == null)
			{
				return;
			}
			for (int i = 0; i < Creed.Interests.Count; i++)
			{
				FactionInterest interest = Creed.Interests[i];
				if (interest == null || interest.TagList == null || !interest.TagList.Contains(KingdomNatureRules.ChromeInterestTag))
				{
					continue;
				}
				if (interest.Inverse)
				{
					Refuse = true;
				}
				else
				{
					Revere = true;
				}
			}
		}

		/// <summary>
		/// What to call the part in a sentence: the mutation's own display name when it has one,
		/// and otherwise the class name unpacked into words. Never the bare class name, which
		/// would put <c>MassMind</c> into the founder's message log.
		/// </summary>
		private static string PartPhrase(GameObject Founder, string PartName)
		{
			BaseMutation mutation = Founder.GetPart(PartName) as BaseMutation;
			string displayName = mutation?.GetDisplayName();
			if (!string.IsNullOrEmpty(displayName))
			{
				return displayName.ToLowerInvariant();
			}
			return Unpack(PartName);
		}

		/// <summary>Turns <c>ThickFur</c> into <c>thick fur</c>. Plain enough that a part from any
		/// mod reads as English without that mod having declared anything.</summary>
		private static string Unpack(string Name)
		{
			if (string.IsNullOrEmpty(Name))
			{
				return "";
			}
			System.Text.StringBuilder builder = new System.Text.StringBuilder(Name.Length + 4);
			for (int i = 0; i < Name.Length; i++)
			{
				char c = Name[i];
				if (i > 0 && char.IsUpper(c) && !char.IsUpper(Name[i - 1]))
				{
					builder.Append(' ');
				}
				builder.Append(char.ToLowerInvariant(c));
			}
			return builder.ToString();
		}
	}
}
