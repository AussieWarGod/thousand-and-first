using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The keepers' map: the dependency graph the catalogue has always contained, drawn.
	/// <para>
	/// <c>_notes/DIVERSITY-AND-TECH-TREES.md</c> &sect;2.3 found the tree already built and never
	/// seen: <i>"it is real and it is enforced &hellip; and it is never drawn. A founder cannot see
	/// that certifying the solar still is what stands between them and a condensing hall until they
	/// try to build one."</i> This is &sect;2.8's rendering, and it obeys &sect;2.2's ruling in
	/// full.
	/// </para>
	/// <para>
	/// <b>The one-line test, restated because it governs every line of this file:</b> <i>can the
	/// founder do anything at this surface except look?</i> No. There is no spend, no queue, no
	/// percentage, no timer, and nothing to press. <c>KingdomZoningRules</c>'s own comment &mdash;
	/// <i>"this mod has no research tree and does not want one"</i> &mdash; forbids a screen the
	/// founder pays into; it does not forbid showing them the graph their catalogue already has.
	/// </para>
	/// <para>
	/// Engine-free and total. The catalogue and the roster arrive as already-read values.
	/// </para>
	/// </summary>
	internal static partial class KingdomTechMapRules
	{
		/// <summary>Locked designs the map names before it stops. Sorted by how close, so the
		/// eight it shows are the eight worth walking toward.</summary>
		internal const int MaxLocked = 8;

		/// <summary>
		/// How many gates stand between the settlement and a design.
		/// <para>
		/// Districts are deliberately NOT counted. A district gate is a fact about the tile the
		/// founder stakes on, not about the settlement &mdash; it is answered by walking somewhere
		/// else, which is not distance in the sense this number means. It is still named in
		/// <see cref="Missing"/>, because a founder who cannot see it will think the design is
		/// open and be refused at the stake.
		/// </para>
		/// </summary>
		internal static int Distance(bool TechShort, int MissingKnowledge, bool ZonesShort, bool StageShort)
		{
			return Distance(TechShort, MissingKnowledge, ZonesShort, StageShort, 0, false);
		}

		/// <summary>
		/// The same count with Addendum 16's creed stack folded in. A creed-work whose
		/// congregation is too small, or whose hands are not here, is NOT "within reach", and the
		/// map saying it was would be the same lie a docstring tells when the code moved out from
		/// under it.
		/// <para>
		/// Alignment is deliberately not counted, and cannot be: a design nobody here has ever
		/// aligned with is not on the map at all (the visibility law), so every row that reaches
		/// this arithmetic has already passed that gate.
		/// </para>
		/// </summary>
		/// <param name="MissingBuilders">Unmet <c>Builders</c> requirements.</param>
		/// <param name="CreedShort">Whether the creed is held here by too few of the city.</param>
		internal static int Distance(bool TechShort, int MissingKnowledge, bool ZonesShort, bool StageShort,
			int MissingBuilders, bool CreedShort)
		{
			int distance = 0;
			if (MissingBuilders > 0)
			{
				distance += MissingBuilders;
			}
			if (CreedShort)
			{
				distance++;
			}
			if (TechShort)
			{
				distance++;
			}
			if (MissingKnowledge > 0)
			{
				distance += MissingKnowledge;
			}
			if (ZonesShort)
			{
				distance++;
			}
			if (StageShort)
			{
				distance++;
			}
			return distance;
		}

		/// <summary>
		/// What is in the way of a design, listed in the order the gates are judged in
		/// (<c>KingdomZoningRules.ZoningVerdict</c>'s own order): what the keepers do not know,
		/// then craft, then ground, then stage, then the district.
		/// </summary>
		/// <returns>The clause, or empty when nothing about the settlement is in the way.</returns>
		internal static string Missing(IList<string> UnknownNames, string WantedTech, string HaveTech,
			int WantedZones, int HeldZones, string WantedStage, string District)
		{
			return Missing(UnknownNames, WantedTech, HaveTech, WantedZones, HeldZones, WantedStage, District, null, null, 0, 0, 0);
		}

		/// <summary>
		/// The same clause with the creed stack named first, in <c>Judge</c>'s own order: the
		/// hands, then the congregation, then everything the four older gates already said.
		/// </summary>
		/// <param name="MissingHands">Unmet <c>Builders</c> requirements, already read as prose.</param>
		/// <param name="Creed">The creed the design belongs to, as the founder reads it, or null.</param>
		/// <param name="WantedShare">Share of the city the design wants holding it.</param>
		/// <param name="HeldShare">Share the city actually holds.</param>
		/// <param name="Holding">People holding it, for the sentence that names them.</param>
		internal static string Missing(IList<string> UnknownNames, string WantedTech, string HaveTech,
			int WantedZones, int HeldZones, string WantedStage, string District,
			IList<string> MissingHands, string Creed, int WantedShare, int HeldShare, int Holding)
		{
			List<string> parts = new List<string>();
			if (MissingHands != null && MissingHands.Count > 0)
			{
				parts.Add("it is raised by " + KingdomZoningRules.JoinAnd(MissingHands) + ", and there is nobody here who is");
			}
			if (!string.IsNullOrEmpty(Creed) && WantedShare > 0)
			{
				parts.Add("it wants " + WantedShare + "% of the city holding with " + Creed + " and " + Holding
					+ ((Holding == 1) ? " person does" : " people do") + " (" + HeldShare + "%)");
			}
			if (UnknownNames != null && UnknownNames.Count > 0)
			{
				parts.Add("the keepers have never been taught " + KingdomZoningRules.JoinAnd(UnknownNames));
			}
			if (!string.IsNullOrEmpty(WantedTech))
			{
				parts.Add("it wants " + WantedTech + " craft and the settlement works at " + HaveTech);
			}
			if (WantedZones > HeldZones)
			{
				parts.Add("it wants " + WantedZones + " parasangs and the city holds " + HeldZones);
			}
			if (!string.IsNullOrEmpty(WantedStage))
			{
				parts.Add("it wants a settlement grown to " + WantedStage);
			}
			if (parts.Count == 0 && string.IsNullOrEmpty(District))
			{
				return "";
			}
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < parts.Count; i++)
			{
				builder.Append((i == 0) ? "" : "; ").Append(parts[i]);
			}
			if (!string.IsNullOrEmpty(District))
			{
				builder.Append((builder.Length > 0) ? "; " : "").Append("and it is raised on ").Append(District);
			}
			return builder.ToString() + ".";
		}

		/// <summary>
		/// The head of the map: where the settlement's craft stands and what the next rung costs,
		/// in the same numbers the keepers' own readout uses. One arithmetic, two surfaces.
		/// </summary>
		internal static string Header(string CityName, int Points, string LevelName, int ToNext, string NextLevelName)
		{
			return "The keepers of {{C|" + (string.IsNullOrEmpty(CityName) ? "the city" : CityName) + "}} work at {{W|"
				+ LevelName + "}}, on " + Points + ((Points == 1) ? " thing" : " things") + " learned."
				+ ((ToNext <= 0)
					? "\n{{K|There is no higher craft to reach.}}"
					: ("\n{{K|" + ToNext + " more would reach " + NextLevelName + ".}}"));
		}

		/// <summary>
		/// The roads not taken: which of the ways a settlement can learn anything at all this city
		/// has never used.
		/// <para>
		/// <c>_notes/DIVERSITY-AND-TECH-TREES.md</c> &sect;2.4 names the channels, and this is
		/// &sect;2.8's third rendering. It names ways, never nodes: the branching is visible without
		/// anything being clickable, and every one of them is a thing that happens out in the world.
		/// </para>
		/// </summary>
		/// <returns>The paragraph, or empty when the city has walked all of them.</returns>
		internal static string RoadsNotTaken(bool AnyDisk, bool AnyMachine, bool AnyOrigin)
		{
			return RoadsNotTaken(AnyDisk, AnyMachine, AnyOrigin, AnyRite: true);
		}

		/// <summary>
		/// The same paragraph with the fourth road on it: water shared with somebody who already
		/// does the work.
		/// <para>
		/// This is the visibility law's escape valve, and under Addendum 14 it matters MORE than it
		/// did. It names KINDS OF LEARNING this city has never done &mdash; never a node, never a
		/// design, never a count &mdash; so the founder can tell there is more world and cannot tell
		/// what is in it.
		/// </para>
		/// </summary>
		internal static string RoadsNotTaken(bool AnyDisk, bool AnyMachine, bool AnyOrigin, bool AnyRite)
		{
			List<string> roads = new List<string>();
			if (!AnyRite)
			{
				roads.Add("The keepers know nothing of anybody else's way. Share water with a people who do this work and they will tell you where a road begins — never where it ends.");
			}
			if (!AnyDisk)
			{
				roads.Add("No data disk has been read to them. Carry one home and they will read it and hand it back.");
			}
			if (!AnyMachine)
			{
				roads.Add("They have certified no machine. Drag one home from a ruin, pay the water and the hands, and it goes on the grid.");
			}
			if (!AnyOrigin)
			{
				roads.Add("Nobody has arrived carrying a trade of their own. That one is not bought; it walks in, and it leaves when they do.");
			}
			if (roads.Count == 0)
			{
				return "";
			}
			StringBuilder builder = new StringBuilder("{{K|Roads not taken:}}");
			for (int i = 0; i < roads.Count; i++)
			{
				builder.Append("\n  {{K|").Append(roads[i]).Append("}}");
			}
			return builder.ToString();
		}
	}
}
