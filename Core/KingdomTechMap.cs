using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.UI;

namespace ThousandAndFirst
{
	/// <summary>
	/// The keepers' map, drawn from the catalogue's own gates. A reading surface and nothing else.
	/// <para>
	/// <c>_notes/DIVERSITY-AND-TECH-TREES.md</c> &sect;2.3's finding, answered: the dependency
	/// graph is real, enforced in a fixed refusal order, and had no surface at all. Three chapters,
	/// all three &sect;2.8's: what each thing the keepers know OPENED, what the nearest locked
	/// designs are waiting on, and which ways of learning this city has never walked.
	/// </para>
	/// <para>
	/// It reads <c>KingdomZoning</c> and the catalogue and writes to neither. Nothing on it can be
	/// pressed &mdash; the ruling in <c>KingdomZoningRules</c> forbids a screen the founder pays
	/// into, and this one takes nothing.
	/// </para>
	/// </summary>
	public static class KingdomTechMap
	{
		/// <summary>Rides the zoning gate: with the gates off, every design is open and a map of
		/// them would be a page of the same sentence.</summary>
		public static bool Enabled
		{
			get { return KingdomZoning.Enabled; }
		}

		/// <summary>
		/// The whole map as the founder reads it.
		/// <para>
		/// Preconditions: a founded realm. Side effects: none. Failure mode: any fault degrades to
		/// the head of the map, which is the same arithmetic the keepers' screen already shows.
		/// </para>
		/// </summary>
		public static string Draw(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return "You rule nothing yet.";
			}
			if (!Enabled)
			{
				return "The gates are off. Every design in the catalogue is open to " + System.SeatName
					+ ", so there is no map to draw.";
			}
			StringBuilder builder = new StringBuilder();
			KingdomSystem.Guard("keepers' map", delegate
			{
				// The map is not a screen the founder is given; it is a thing the keepers begin to
				// draw. Until somebody here writes anything down there is one sentence and no
				// chapters, and every "how does the founder find the tree" question resolves the
				// same way everything else in this mod does: by doing something in the world.
				if (!KingdomResearch.KeepsNotes(System))
				{
					builder.Append(KingdomResearchRules.NothingWrittenDown(System.SeatName));
					return;
				}
				List<string> roster = KingdomZoning.Roster(System);
				int points = KingdomZoningRules.TechPoints(roster);
				TechLevel level = KingdomZoningRules.LevelForPoints(points);
				int toNext = KingdomZoningRules.PointsToNext(points);
				builder.Append(KingdomTechMapRules.Header(System.SeatName, points,
					KingdomZoningRules.TechName(level),
					toNext,
					KingdomZoningRules.TechName((TechLevel)((int)level + 1))));
				Opened(System, roster, builder);
				Working(System, builder);
				Locked(System, roster, level, builder);
				HeardOf(System, builder);
				string roads = KingdomTechMapRules.RoadsNotTaken(
					Any(roster, KingdomZoningRules.KindDisk),
					Any(roster, KingdomZoningRules.KindMachine),
					Any(roster, KingdomZoningRules.KindOrigin),
					Any(roster, KingdomZoningRules.KindRite));
				if (!string.IsNullOrEmpty(roads))
				{
					builder.Append("\n\n").Append(roads);
				}
			});
			// A fault before the first line would otherwise open an empty popup, which reads as the
			// mod being broken rather than as one report having failed. STANDARDS §7b.
			return (builder.Length > 0)
				? builder.ToString()
				: ("The keepers' map of " + System.SeatName + " could not be drawn. Nothing has been lost; what they know is still on the keepers' own screen.");
		}

		// ==================================================================================
		// What each thing the keepers know opened (§2.8 rendering 1)
		// ==================================================================================

		private static void Opened(KingdomSystem system, List<string> roster, StringBuilder builder)
		{
			if (roster == null || roster.Count == 0)
			{
				builder.Append("\n\n{{K|The keepers have been taught nothing yet. Everything the catalogue asks for by name is shut to them.}}");
				return;
			}
			builder.Append("\n\n{{W|What they know, and what it opened:}}");
			foreach (string key in roster)
			{
				string name = KingdomZoningRules.NameOf(key);
				List<string> designs = DesignsNeeding(system, key);
				builder.Append("\n  ").Append(string.IsNullOrEmpty(name) ? key : name);
				builder.Append((designs.Count == 0)
					? " {{K|— nothing in the catalogue asks for it, yet}}"
					: (" {{K|— " + KingdomZoningRules.JoinAnd(designs) + "}}"));
			}
		}

		/// <summary>Every design whose <c>Knowledge</c> gate this one roster key satisfies a token
		/// of. Asked one key at a time, which is the only way to say what a key OPENED rather than
		/// what the roster as a whole opened.</summary>
		private static List<string> DesignsNeeding(KingdomSystem system, string key)
		{
			List<string> designs = new List<string>();
			List<string> one = new List<string> { key };
			List<KingdomRules.BuildEntry> buildings = KingdomData.Buildings;
			for (int i = 0; buildings != null && i < buildings.Count; i++)
			{
				KingdomRules.BuildEntry entry = buildings[i];
				if (entry == null || string.IsNullOrEmpty(entry.Key) || !KingdomZoning.Visible(system, entry))
				{
					continue;
				}
				ZoneGate gate = KingdomZoning.GateFor(entry.Key);
				if (string.IsNullOrEmpty(gate.Knowledge))
				{
					continue;
				}
				foreach (string token in KingdomZoningRules.Tokens(gate.Knowledge))
				{
					if (KingdomZoningRules.Knows(one, token))
					{
						designs.Add(Named(entry));
						break;
					}
				}
			}
			return designs;
		}

		// ==================================================================================
		// What they are working out (§6.4 chapter 2) and what they have heard of (chapter 3)
		// ==================================================================================

		private static void Working(KingdomSystem system, StringBuilder builder)
		{
			string chapter = KingdomResearch.Working(system);
			if (!string.IsNullOrEmpty(chapter))
			{
				builder.Append("\n\n").Append(chapter);
			}
		}

		private static void HeardOf(KingdomSystem system, StringBuilder builder)
		{
			if (!KingdomResearch.Enabled)
			{
				return;
			}
			builder.Append("\n\n").Append(KingdomTechMapRules.HeardOfChapter(KingdomResearch.HeardOf(system)));
		}

		// ==================================================================================
		// The nearest locked things, and why (§2.8 rendering 2, under the visibility law)
		// ==================================================================================

		// The row set and the tail count are computed over the DISCOVERED set only, and the filter
		// is KingdomZoning.Visible's -- a design waiting on a node the founder has never heard of is
		// absent, not greyed, and is not in the number either. This is the one line of shipped
		// behaviour Addendum 14 deletes: the chapter used to walk the whole catalogue and tail with
		// a count over every locked design in it, which let the founder COUNT what they could not
		// see. It is a regression to the founder's convenience and it is the correct one.
		private static void Locked(KingdomSystem system, List<string> roster, TechLevel level, StringBuilder builder)
		{
			List<TechMapRow> rows = new List<TechMapRow>();
			BuilderRoll roll = KingdomZoning.BuilderRollOf(system);
			int open = 0;
			List<KingdomRules.BuildEntry> buildings = KingdomData.Buildings;
			for (int i = 0; buildings != null && i < buildings.Count; i++)
			{
				KingdomRules.BuildEntry entry = buildings[i];
				// The visibility law reaches the map too, and it has to: the map's whole job is to
				// say what is nearly in reach, and a creed-work nobody here has ever aligned with
				// is not nearly in reach -- naming it would be the spoiler screen Addendum 14
				// forbids.
				if (entry == null || string.IsNullOrEmpty(entry.Key) || !KingdomZoning.Visible(system, entry))
				{
					continue;
				}
				ZoneGate gate = KingdomZoning.GateFor(entry.Key);
				List<string> unknown = KingdomZoningRules.DescribeKeys(KingdomZoningRules.MissingKnowledge(roster, gate.Knowledge));
				bool techShort = gate.MinTech > level;
				bool zonesShort = gate.MinZones > system.ClaimedZones.Count;
				bool stageShort = entry.MinStage > system.Stage;
				List<string> hands = KingdomZoningRules.DescribeBuilders(KingdomZoningRules.MissingBuilders(roll, gate.Builders));
				int holding = string.IsNullOrEmpty(gate.Creed) ? 0 : roll.HoldingNow(gate.Creed);
				bool creedShort = !string.IsNullOrEmpty(gate.Creed) && roll.Known
					&& !KingdomZoningRules.CreedShareMet(holding, roll.People, gate.EffectiveCreedShare);
				int distance = KingdomTechMapRules.Distance(techShort, unknown.Count, zonesShort, stageShort, hands.Count, creedShort);
				if (distance <= 0)
				{
					open++;
					continue;
				}
				rows.Add(new TechMapRow(entry.Key, Named(entry), distance, KingdomTechMapRules.Missing(
					unknown,
					techShort ? KingdomZoningRules.TechName(gate.MinTech) : null,
					KingdomZoningRules.TechName(level),
					gate.MinZones,
					system.ClaimedZones.Count,
					stageShort ? entry.MinStage.ToString() : null,
					KingdomZoningRules.DescribeDistricts(gate.Districts),
					hands,
					creedShort ? KingdomCreed.CreedName(gate.Creed) : null,
					creedShort ? gate.EffectiveCreedShare : 0,
					KingdomZoningRules.ShareHeld(holding, roll.People),
					holding)));
			}
			KingdomTechMapRules.Sort(rows);
			builder.Append("\n\n{{W|What is nearly in reach:}}");
			if (rows.Count == 0)
			{
				builder.Append("\n  {{K|Everything in the catalogue is open to this settlement. What is left is ground, water, and hands.}}");
			}
			for (int i = 0; i < rows.Count && i < KingdomTechMapRules.MaxLocked; i++)
			{
				builder.Append("\n  ").Append(rows[i].Name).Append(" — ").Append(KingdomTechMapRules.Reach(rows[i].Distance))
					.Append("\n    {{K|").Append(rows[i].Missing).Append("}}");
			}
			if (rows.Count > KingdomTechMapRules.MaxLocked)
			{
				builder.Append("\n  {{K|And ").Append(rows.Count - KingdomTechMapRules.MaxLocked).Append(" further off.}}");
			}
			builder.Append("\n\n{{K|").Append(open).Append(open == 1 ? " design is" : " designs are")
				.Append(" already open to the settlement's own state; where you may stake them is the ground's business.}}");
		}

		private static bool Any(List<string> roster, string kind)
		{
			for (int i = 0; roster != null && i < roster.Count; i++)
			{
				if (KingdomZoningRules.KindOf(roster[i]) == kind)
				{
					return true;
				}
			}
			return false;
		}

		private static string Named(KingdomRules.BuildEntry entry)
		{
			return string.IsNullOrEmpty(entry.DisplayName) ? entry.Key : entry.DisplayName;
		}
	}
}
