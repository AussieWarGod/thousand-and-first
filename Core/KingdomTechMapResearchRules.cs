using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomTechMapRules
	{
		// ==================================================================================
		// The research chapters (Addendum 14's visibility law, §6.4)
		// ==================================================================================

		/// <summary>Nodes the map names before it stops, for the same reason
		/// <see cref="MaxLocked"/> exists.</summary>
		internal const int MaxHeardOf = 8;

		/// <summary>
		/// What is in the way of a node the founder has heard of, in the order the bench judges
		/// them: what the keepers do not know, then the mind, then the craft. Written in the same
		/// register as <see cref="Missing"/> and deliberately carrying no number for the WORK
		/// itself &mdash; distance is prose, and the only numbers here are the two the founder can
		/// go and change.
		/// </summary>
		/// <param name="UnknownNames">Unmet requirements, already read as prose.</param>
		/// <param name="WantedMind">Intelligence the tier wants, or 0 when the tier is reached.</param>
		/// <param name="HaveMind">Intelligence the city's ablest keeper has.</param>
		/// <param name="WantedTech">Craft rung wanted, or null when it is reached.</param>
		/// <param name="HaveTech">Craft rung the city works at.</param>
		internal static string MissingForNode(IList<string> UnknownNames, int WantedMind, int HaveMind,
			string WantedTech, string HaveTech)
		{
			List<string> parts = new List<string>();
			if (UnknownNames != null && UnknownNames.Count > 0)
			{
				parts.Add("the keepers have never been taught " + KingdomZoningRules.JoinAnd(UnknownNames));
			}
			if (WantedMind > 0)
			{
				parts.Add("it wants a mind of " + WantedMind + " and the ablest keeper here has "
					+ ((HaveMind > 0) ? HaveMind.ToString() : "none to speak of"));
			}
			if (!string.IsNullOrEmpty(WantedTech))
			{
				parts.Add("it wants " + WantedTech + " craft and the settlement works at " + HaveTech);
			}
			if (parts.Count == 0)
			{
				return "";
			}
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < parts.Count; i++)
			{
				builder.Append((i == 0) ? "" : "; ").Append(parts[i]);
			}
			return builder.ToString() + ".";
		}

		/// <summary>
		/// The map's second chapter: the one subject the bench is working out, how near it is in
		/// words, and what else is on the shelf. Shelved subjects are named and nothing more &mdash;
		/// they are memory, not a queue, and there is nothing to press on any of them.
		/// </summary>
		/// <returns>The chapter, never empty.</returns>
		internal static string WorkingChapter(string Subject, string Reach, IList<string> Shelved)
		{
			StringBuilder builder = new StringBuilder("{{W|What they are working out:}}");
			builder.Append("\n  ").Append(string.IsNullOrEmpty(Subject) ? "nothing" : Subject).Append(" — ").Append(Reach);
			if (Shelved != null && Shelved.Count > 0)
			{
				builder.Append("\n  {{K|Set aside, and remembered: ").Append(KingdomZoningRules.JoinAnd(Shelved)).Append(".}}");
			}
			return builder.ToString();
		}

		/// <summary>
		/// The map's third chapter: what the keepers have HEARD of and do not hold.
		/// <para>
		/// The row set is the discovered-and-unheld nodes only, and the tail counts those rows and
		/// nothing else. This is the deviation the visibility law deletes: the old chapter walked
		/// the whole catalogue and tailed with a count over every locked design in it, which let the
		/// founder COUNT what they could not see. Vanilla's own precedent for an unknown recipe is
		/// total omission &mdash; not a greyed row, not a silhouette, not a number.
		/// </para>
		/// </summary>
		internal static string HeardOfChapter(IList<ResearchRow> Rows)
		{
			StringBuilder builder = new StringBuilder("{{W|What they have heard of:}}");
			if (Rows == null || Rows.Count == 0)
			{
				builder.Append("\n  {{K|Nothing they have not already worked out. What is left, they have not heard of.}}");
				return builder.ToString();
			}
			for (int i = 0; i < Rows.Count && i < MaxHeardOf; i++)
			{
				builder.Append("\n  ").Append(Rows[i].Name).Append(" — ").Append(KingdomResearchRules.Reach(Rows[i].Distance, Rows[i].Begun));
				if (!string.IsNullOrEmpty(Rows[i].Missing))
				{
					builder.Append("\n    {{K|").Append(Rows[i].Missing).Append("}}");
				}
			}
			if (Rows.Count > MaxHeardOf)
			{
				builder.Append("\n  {{K|And ").Append(Rows.Count - MaxHeardOf).Append(" further off.}}");
			}
			return builder.ToString();
		}

		/// <summary>
		/// Sorts the research chapter: the nearest first, a begun subject ahead of an untouched one
		/// at the same distance, then by name. Deterministic, so a reload never shuffles it.
		/// </summary>
		internal static void SortResearch(List<ResearchRow> Rows)
		{
			if (Rows == null)
			{
				return;
			}
			Rows.Sort(delegate(ResearchRow a, ResearchRow b)
			{
				if (a.Distance != b.Distance)
				{
					return a.Distance.CompareTo(b.Distance);
				}
				if (a.Begun != b.Begun)
				{
					return a.Begun ? -1 : 1;
				}
				return string.CompareOrdinal(a.Name ?? a.Key ?? "", b.Name ?? b.Key ?? "");
			});
		}

		/// <summary>
		/// Sorts the map: the nearest first, then by name, so two settlements one gate apart read
		/// the same list in the same order and a reload never shuffles it.
		/// </summary>
		internal static void Sort(List<TechMapRow> Rows)
		{
			if (Rows == null)
			{
				return;
			}
			Rows.Sort(delegate(TechMapRow a, TechMapRow b)
			{
				if (a.Distance != b.Distance)
				{
					return a.Distance.CompareTo(b.Distance);
				}
				return string.CompareOrdinal(a.Name ?? a.Key ?? "", b.Name ?? b.Key ?? "");
			});
		}

		/// <summary>How far off, in words, so the number never has to be interpreted.</summary>
		internal static string Reach(int Distance)
		{
			switch (Distance)
			{
			case 0:
				return "{{G|within reach}}";
			case 1:
				return "{{W|one thing away}}";
			default:
				return "{{K|" + Distance + " things away}}";
			}
		}
	}
}
