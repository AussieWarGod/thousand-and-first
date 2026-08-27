using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomCreed
	{

		/// <summary>
		/// What the Charter shows about the two cities. Always available while the realm holds two,
		/// whatever the temper, so the founder can watch this coming from the first muttering
		/// rather than being told about it once.
		/// </summary>
		/// <returns>The report, or a line explaining why there is nothing to report.</returns>
		public static string Report(KingdomSystem System)
		{
			if (!Enabled)
			{
				return "You are not keeping account of what your cities believe.";
			}
			if (System == null || !System.Founded)
			{
				return "There is no realm to take the temper of.";
			}
			if (System.Seceded != null)
			{
				return "{{C|" + KingdomPresentation.Rich(System.Seceded.SettlementName ?? "One of your cities") + "}} left the realm and has not come back. It is "
					+ KingdomCreedRules.CreedClause(CreedName(CreedOf(System.Seceded))) + ", and it still keeps everything it kept.\n\n"
					+ "Stand on their ground and ask, when what split you is no longer true.";
			}
			if (System.SettlementCount < KingdomSettlement.MaxSettlements)
			{
				return "{{C|" + KingdomPresentation.Rich(System.SeatName) + "}} is " + KingdomCreedRules.CreedClause(CreedName(SeatCreed(System)))
					+ ".\n\nOne city cannot fall out with itself. Nothing here needs watching.";
			}
			string report = KingdomCreedRules.TemperReport(Temper(System), System.Dissent, KingdomPresentation.Rich(System.SeatName),
				(System.Away != null) ? KingdomPresentation.Rich(System.Away.SettlementName) : null,
				CreedName(SeatCreed(System)), CreedName(AwayCreed(System)));
			if (!string.IsNullOrEmpty(System.DeclaredCreed))
			{
				report += "\n\nThe realm has declared for {{C|" + CreedName(System.DeclaredCreed) + "}}. Whoever walks the roads here knows it.";
			}
			return report;
		}

		/// <summary>
		/// The factions a settler arriving here could plausibly hold with: everyone on the realm's
		/// standings ledger, plus whatever its cities already hold and whatever it declared, minus
		/// the realm's own faction and anything <see cref="CanBeCreed"/> rejects.
		/// </summary>
		public static List<string> Candidates(KingdomSystem System)
		{
			List<string> candidates = new List<string>();
			foreach (KeyValuePair<string, int> standing in System.Standings)
			{
				Consider(System, candidates, standing.Key);
			}
			foreach (KeyValuePair<string, int> held in System.CreedCounts)
			{
				Consider(System, candidates, held.Key);
			}
			if (System.Away != null)
			{
				foreach (KeyValuePair<string, int> held in System.Away.CreedCounts)
				{
					Consider(System, candidates, held.Key);
				}
			}
			Consider(System, candidates, System.DeclaredCreed);
			candidates.Sort(global::System.StringComparer.Ordinal);
			return candidates;
		}

		private static void Consider(KingdomSystem System, List<string> Candidates, string FactionName)
		{
			if (string.IsNullOrEmpty(FactionName) || FactionName == System.KingdomFactionName || FactionName == "Player" || Candidates.Contains(FactionName))
			{
				return;
			}
			if (CanBeCreed(Factions.GetIfExists(FactionName)))
			{
				Candidates.Add(FactionName);
			}
		}

		/// <summary>
		/// Speaks and chronicles a worsening, once per tier. The hysteresis is
		/// <see cref="KingdomCreedRules.RememberedTemper"/>: jitter across one threshold says
		/// nothing further, and only easing all the way back to concord re-arms the ladder.
		/// </summary>
		private static void Announce(KingdomSystem System, string HereCreed, string ThereCreed)
		{
			CityTemper temper = KingdomCreedRules.ClassifyTemper(System.Dissent);
			CityTemper spoken = (CityTemper)System.DissentSpoken;
			bool speak = KingdomCreedRules.ShouldSpeak(temper, spoken);
			System.DissentSpoken = (int)KingdomCreedRules.RememberedTemper(temper, spoken);
			if (!speak)
			{
				return;
			}
			string awayName = (System.Away != null) ? System.Away.SettlementName : null;
			string speech = KingdomCreedRules.TemperSpeech(temper, KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(awayName));
			if (!string.IsNullOrEmpty(speech))
			{
				MessageQueue.AddPlayerMessage(speech + " {{K|(Charter: how your cities hold each other)}}");
			}
			string entry = KingdomCreedRules.TemperChronicle(temper, KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(awayName));
			if (!string.IsNullOrEmpty(entry))
			{
				KingdomChronicle.Record(System, entry);
			}
			KingdomLog.Log("dissent: " + System.Dissent + " temper=" + temper + " here=" + (HereCreed ?? "-") + " there=" + (ThereCreed ?? "-"));
		}

		/// <summary>Re-arms the spoken ladder after a lever eased the quarrel, so a founder who
		/// mends it and then lets it slip is warned again rather than silently.</summary>
		private static void Rearm(KingdomSystem System)
		{
			System.DissentSpoken = (int)KingdomCreedRules.RememberedTemper(KingdomCreedRules.ClassifyTemper(System.Dissent), (CityTemper)System.DissentSpoken);
			if (System.Dissent >= KingdomCreedRules.DissentBreaking || !KingdomBrink.CityStands())
			{
				return;
			}
			// A lever eased it back off the breaking point. The brink goes on the spot rather than
			// on the next pass -- the founder poured the water, and they are owed the answer in
			// the same breath -- and the clock restarts from now, so the days the realm spent
			// standing at the edge are not billed again the moment it steps back.
			string leaver;
			string kept;
			NameTheLeaver(System, SeatCreed(System), AwayCreed(System), out leaver, out kept);
			bool wasWarned = KingdomBrink.OfCity(System).Warned;
			KingdomBrink.LiftCity(System, The.Game.TimeTicks);
			if (wasWarned)
			{
				// Only what was actually said is unsaid.
				KingdomBrink.Unsay(System, BrinkKind.City, leaver, leaver == System.SeatName, leaver);
			}
		}

		/// <summary>
		/// Trims a city's creed tally back to something its population can support. Deaths that
		/// nothing attributed — a raid, a fall, a save from before this build — leave counts above
		/// the roll they were counted from, and a count that outlives its believers would make a
		/// city's creed permanent. Self-healing rather than a migration, because the tally is
		/// knowledge and not a ledger.
		/// </summary>
		private static void Reconcile(KingdomSystem System)
		{
			Trim(System.CreedCounts, System.Population);
			// And the history, on exactly the same terms and for a sharper reason. A past tally
			// standing above the roll it was counted from would keep a creed-work VISIBLE in a city
			// where nobody who ever held that creed is still alive -- and the visibility law is the
			// one gate that shows nothing rather than refusing out loud, so the error would read as
			// a design that simply exists rather than as one the city has no path to. Each entry
			// counts PEOPLE, so the population is its ceiling exactly as it is the present tally's,
			// even though the sum across creeds may legitimately exceed it (one person can be
			// remembered under MaxKeptCreeds names).
			Trim(System.CreedPastCounts, System.Population);
			if (System.Away != null)
			{
				Trim(System.Away.CreedCounts, System.Away.Population);
				Trim(System.Away.CreedPastCounts, System.Away.Population);
			}
		}

		private static void Trim(Dictionary<string, int> Counts, int Population)
		{
			if (Counts == null || Counts.Count == 0)
			{
				return;
			}
			List<string> held = new List<string>(Counts.Keys);
			int room = (Population > 0) ? Population : 0;
			foreach (string creed in held)
			{
				int count = Counts[creed];
				if (count <= 0 || room <= 0)
				{
					Counts.Remove(creed);
					continue;
				}
				if (count > room)
				{
					Counts[creed] = room;
				}
			}
		}

		/// <summary>
		/// The creed line <c>kingdom:dump</c> appends: what the seated city holds with, and what it
		/// has HELD AND LEFT. The second half is the one the alignment gate reads and the
		/// visibility law hides designs on, so it has to be visible somewhere a tester can look
		/// (STANDARDS 7b: a thing that decides what a founder may see says so out loud somewhere).
		/// </summary>
		/// <param name="System">The realm. Null or unfounded reports an empty string.</param>
		public static string DumpLine(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return "";
			}
			System.Text.StringBuilder line = new System.Text.StringBuilder("\nCreeds held: ");
			line.Append(Tally(System.CreedCounts));
			line.Append("   once held: ").Append(Tally(System.CreedPastCounts));
			return line.ToString();
		}

		// Sorted by name so two runs of the same state print the same line: a dump nobody can diff
		// is a dump nobody reads twice.
		private static string Tally(Dictionary<string, int> Counts)
		{
			if (Counts == null || Counts.Count == 0)
			{
				return "(none)";
			}
			List<string> names = new List<string>(Counts.Keys);
			names.Sort(System.StringComparer.Ordinal);
			List<string> parts = new List<string>();
			for (int i = 0; i < names.Count; i++)
			{
				parts.Add(names[i] + " x" + Counts[names[i]]);
			}
			return string.Join(", ", parts.ToArray());
		}

		private static string OtherCityName(KingdomSystem System)
		{
			string name = (System.Away != null) ? System.Away.SettlementName : null;
			return string.IsNullOrEmpty(name) ? "your other city" : ("{{C|" + KingdomPresentation.Rich(name) + "}}");
		}

		private static string SecessionRefusal(SecessionVerdict Verdict)
		{
			switch (Verdict)
			{
			case SecessionVerdict.OneCity:
				return "A realm of one city has nobody to fall out with.";
			case SecessionVerdict.NoClash:
				return "Your two cities hold nothing against each other worth leaving over.";
			default:
				return "It has not come to that, and it is not going to today.";
			}
		}
	}
}
