using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// How far what a work gives actually carries. Derived from the ground it stands on
	/// (<see cref="KingdomPlotRules.PlotSize"/>) and where in its own improvement chain it sits,
	/// never authored per design unless an author insists.
	/// </summary>
	public enum ReachBand
	{
		/// <summary>Its own footprint and nothing beyond it. The wayside statue, the cask rack,
		/// the shrine stone.</summary>
		Plot = 0,

		/// <summary>The cluster of built ground it stands in &mdash; what the layout grammar
		/// gathers around it, measured rather than declared
		/// (<see cref="KingdomReachRules.QuarterMarks"/>).</summary>
		Quarter = 1,

		/// <summary>Everything standing in the same zone.</summary>
		Zone = 2,

		/// <summary>Every zone the seated city claims.</summary>
		City = 3,

		/// <summary>Every zone the realm holds, the second city included.</summary>
		Realm = 4
	}

	/// <summary>
	/// How near a place is to a work, in the only terms reach cares about. Ordered from farthest
	/// to nearest so a covering test is one comparison and can never disagree with itself.
	/// </summary>
	public enum ReachRelation
	{
		/// <summary>Not the realm's ground at all.</summary>
		Elsewhere = 0,

		/// <summary>The realm holds it, but the work's own city does not.</summary>
		SameRealm = 1,

		/// <summary>A zone of the work's own city, but not the work's own zone.</summary>
		SameCity = 2,

		/// <summary>The work's own zone, outside its quarter.</summary>
		SameZone = 3,

		/// <summary>Inside the cluster of built ground the work stands in.</summary>
		SameQuarter = 4,

		/// <summary>On the work's own footprint.</summary>
		SamePlot = 5
	}

	/// <summary>
	/// What shades one piece of ground: every lift in reach of it, folded by kind, and which kind
	/// is the loudest. The readable half of Addendum 6 &mdash; a quarter's character is a thing
	/// the founder can be told, not an invisible modifier.
	/// </summary>
	public sealed class GroundCharacter
	{
		/// <summary>One entry per kind, amounts summed, in
		/// <see cref="KingdomReachRules.LiftOrder"/> and then first-seen order. Never null.
		/// </summary>
		public List<KindAmount> Lifts = new List<KindAmount>();

		/// <summary>Every lift together.</summary>
		public int Total;

		/// <summary>The loudest kind, or null when nothing shades this ground.</summary>
		public string Dominant;

		/// <summary>How much of <see cref="Dominant"/> there is. Zero when there is none.
		/// </summary>
		public int DominantAmount;
	}

	/// <summary>
	/// Engine-free rules for Addendum 6: what a work reaches, what falls inside that reach, and
	/// what the ground inside it is like to stand on.
	/// <para>
	/// <b>The ladder.</b> Reach is derived, never authored by default: S reaches its own plot, M
	/// its quarter, L its zone, XL the city &mdash; and an XL that is the last tier of its own
	/// chain reaches the whole realm. Tier moves the edge inside the band as well
	/// (<see cref="QuarterRadius"/>), so a shrine garth shades further than a shrine stone
	/// without either design saying so. A <c>Reach</c> attribute overrides the derivation for the
	/// author who needs it; every design that says nothing &mdash; including every modded one
	/// &mdash; is placed on the ladder correctly the day it is written.
	/// </para>
	/// <para>
	/// <b>What scopes and what does not.</b> Only lifts scope. Water, food and roofs are drawn
	/// and carried, so they stay the citywide pools <c>KingdomCatalogueRules.BindingSupports</c>
	/// has always made them (<see cref="ScopedByReach"/>); faith, order, learning, luxury and
	/// craft shade whoever is IN REACH. That is the whole of how a temple quarter becomes
	/// different ground from a tanners' row, and no code anywhere has to know the word.
	/// </para>
	/// <para>
	/// <b>Where "quarter" lives.</b> Nowhere, as a type or a field. A quarter is measured on
	/// demand from the marks the layout grammar already reads
	/// (<c>KingdomLayoutRules.LayoutMark</c>): built ground within <see cref="QuarterLinkCells"/>
	/// of built ground is one cluster, transitively, and the work's quarter is the cluster the
	/// work stands in. Nothing is stored, so a quarter that grows, splits, or is struck is simply
	/// measured differently the next time somebody asks.
	/// </para>
	/// <para>
	/// <b>What this file never does.</b> Read a clock, keep a meter, or punish. Reach decides
	/// WHERE a lift lands, and staffing and condition decide how much of it there is
	/// (<see cref="Scaled"/>). A great work nobody heads keeps working at a smaller reach
	/// (<see cref="Unheaded"/>) and says so once; it never stops, and no clock ever reduces it
	/// &mdash; only an event does, through the wear ladder (Addendum 7).
	/// </para>
	/// </summary>
	public static class KingdomReachRules
	{
		// --- The ladder ---------------------------------------------------------------------

		/// <summary>The band a tier reaches on the ground alone, before its place in its own
		/// chain is considered. A single-cell design reaches its own cell, which is
		/// <see cref="ReachBand.Plot"/> exactly as a small plot is.</summary>
		public static ReachBand BandForSize(KingdomPlotRules.PlotSize Size)
		{
			switch (Size)
			{
			case KingdomPlotRules.PlotSize.Medium:
				return ReachBand.Quarter;
			case KingdomPlotRules.PlotSize.Large:
				return ReachBand.Zone;
			case KingdomPlotRules.PlotSize.Huge:
				return ReachBand.City;
			default:
				return ReachBand.Plot;
			}
		}

		/// <summary>
		/// Reach from size and tier together. Size sets the band; the only band tier moves is the
		/// great work's, because that is the only one the ruling gives two names to &mdash; XL
		/// reaches the city, or the realm. A chain's last link is the realm's; every earlier link
		/// of the same chain, and every XL that never improves, reaches the city.
		/// </summary>
		/// <param name="Size">The plot tier the design stands on.</param>
		/// <param name="TierIndex">How many designs improve INTO this one, so zero is the first
		/// tier of a chain. Negative reads as zero.</param>
		/// <param name="TierCount">Links in the whole chain, this one included. One or less is a
		/// design that never changes, which is never a last link.</param>
		public static ReachBand Derive(KingdomPlotRules.PlotSize Size, int TierIndex, int TierCount)
		{
			ReachBand band = BandForSize(Size);
			if (band != ReachBand.City)
			{
				return band;
			}
			int index = (TierIndex < 0) ? 0 : TierIndex;
			return (TierCount >= 2 && index == TierCount - 1) ? ReachBand.Realm : ReachBand.City;
		}

		/// <summary>How the mod spells a band in an attribute and says it in the log.</summary>
		public static readonly string[] BandNames = new string[5] { "plot", "quarter", "zone", "city", "realm" };

		/// <summary>The spelling of one band. Never null.</summary>
		public static string BandName(ReachBand Band)
		{
			int index = (int)Band;
			return (index >= 0 && index < BandNames.Length) ? BandNames[index] : BandNames[0];
		}

		/// <summary>
		/// Reads a <c>Reach</c> attribute. Case and surrounding whitespace are folded; anything
		/// else is refused with a reason for the log, and the caller keeps the derivation
		/// (STANDARDS 9: a malformed attribute disables itself and never takes a design out of
		/// the catalogue).
		/// </summary>
		/// <param name="Raw">The attribute, or null.</param>
		/// <param name="Band">The parsed band; <see cref="ReachBand.Plot"/> and meaningless on
		/// failure.</param>
		/// <param name="Error">Null on success, else one log-facing sentence.</param>
		/// <returns>False for a blank attribute as well as a bad one: blank means "derive me",
		/// and the caller tells them apart by <paramref name="Error"/> being null.</returns>
		public static bool TryParseBand(string Raw, out ReachBand Band, out string Error)
		{
			Band = ReachBand.Plot;
			Error = null;
			string folded = Fold(Raw);
			if (folded == null)
			{
				return false;
			}
			for (int i = 0; i < BandNames.Length; i++)
			{
				if (BandNames[i] == folded)
				{
					Band = (ReachBand)i;
					return true;
				}
			}
			Error = "\"" + Raw.Trim() + "\" is none of " + Join(BandNames);
			return false;
		}

		/// <summary>
		/// What a design actually reaches: its declared <c>Reach</c> where it has one, else the
		/// derivation. The one entry point a registry should use, so an override and a
		/// derivation can never be resolved in two different orders in two places.
		/// </summary>
		/// <param name="DeclaredReach">The raw attribute. Null or blank derives.</param>
		/// <param name="Size">The plot tier.</param>
		/// <param name="TierIndex">See <see cref="Derive"/>.</param>
		/// <param name="TierCount">See <see cref="Derive"/>.</param>
		/// <param name="Overridden">True when the answer came from the attribute.</param>
		/// <param name="Error">Null unless the attribute was written and unreadable, in which
		/// case the derivation is returned and this says why.</param>
		public static ReachBand Resolve(string DeclaredReach, KingdomPlotRules.PlotSize Size, int TierIndex, int TierCount, out bool Overridden, out string Error)
		{
			ReachBand declared;
			Overridden = TryParseBand(DeclaredReach, out declared, out Error);
			return Overridden ? declared : Derive(Size, TierIndex, TierCount);
		}

		// --- Covering ------------------------------------------------------------------------

		/// <summary>How near a place must be for a band to reach it.</summary>
		public static ReachRelation RelationRequired(ReachBand Band)
		{
			switch (Band)
			{
			case ReachBand.Quarter:
				return ReachRelation.SameQuarter;
			case ReachBand.Zone:
				return ReachRelation.SameZone;
			case ReachBand.City:
				return ReachRelation.SameCity;
			case ReachBand.Realm:
				return ReachRelation.SameRealm;
			default:
				return ReachRelation.SamePlot;
			}
		}

		/// <summary>Whether a work of this band reaches a place standing this near it. Ground the
		/// realm does not hold is never reached, by any band.</summary>
		public static bool Covers(ReachBand Band, ReachRelation Where)
		{
			return Where != ReachRelation.Elsewhere && Where >= RelationRequired(Band);
		}

		/// <summary>
		/// The nearest true description of where a place is, from the four facts an engine caller
		/// can measure. Nearer facts win, so a caller that reports a footprint hit without also
		/// reporting the zone still gets the right answer.
		/// </summary>
		/// <param name="SameRealm">The realm holds the ground.</param>
		/// <param name="SameCity">The work's own city holds it.</param>
		/// <param name="SameZone">The work's own zone.</param>
		/// <param name="InQuarter">Inside the work's measured cluster, from
		/// <see cref="QuarterMarks"/>.</param>
		/// <param name="OnFootprint">On the work's own ground.</param>
		public static ReachRelation RelationAt(bool SameRealm, bool SameCity, bool SameZone, bool InQuarter, bool OnFootprint)
		{
			if (OnFootprint)
			{
				return ReachRelation.SamePlot;
			}
			if (InQuarter)
			{
				return ReachRelation.SameQuarter;
			}
			if (SameZone)
			{
				return ReachRelation.SameZone;
			}
			if (SameCity)
			{
				return ReachRelation.SameCity;
			}
			return SameRealm ? ReachRelation.SameRealm : ReachRelation.Elsewhere;
		}

		// --- The quarter, measured ------------------------------------------------------------

		/// <summary>
		/// How near two pieces of built ground must be to read as one quarter. Six cells: wider
		/// than the lane <c>KingdomLayoutRules.CrowdPenalty</c> keeps between neighbours, narrower
		/// than the ring <c>KingdomLayoutRules.FieldRingCells</c> puts the fields out at, so a
		/// housing cluster holds together and the fields past it are their own ground.
		/// </summary>
		public const int QuarterLinkCells = 6;

		/// <summary>How far a first-tier work shades past the built ground of its own
		/// quarter.</summary>
		public const int QuarterBaseRadius = 4;

		/// <summary>How much further each tier above the first shades. This is the whole of
		/// "tier shifts within the band": the garth carries past the stone without either of them
		/// changing what band they are in.</summary>
		public const int QuarterRadiusPerTier = 2;

		/// <summary>The furthest any quarter-band work shades, however long its chain. A quarter
		/// that swallowed the zone would make the zone band mean nothing.</summary>
		public const int QuarterRadiusCap = 10;

		/// <summary>The shading radius a tier carries. Clamped at both ends.</summary>
		public static int QuarterRadius(int TierIndex)
		{
			int index = (TierIndex < 0) ? 0 : TierIndex;
			int radius = QuarterBaseRadius + (QuarterRadiusPerTier * index);
			return (radius > QuarterRadiusCap) ? QuarterRadiusCap : radius;
		}

		/// <summary>
		/// The cluster of built ground one work stands in: every mark reachable from the work by
		/// steps of at most <paramref name="LinkCells"/>, transitively. Single-linkage on the
		/// marks the layout grammar already reads, which is why a quarter needs no type, no
		/// field, and no save slot &mdash; it is whatever the city has built, measured now.
		/// </summary>
		/// <param name="Marks">Everything standing, from <c>KingdomLayout.ReadMarks</c>. Null
		/// reads as none.</param>
		/// <param name="X">The work's cell.</param>
		/// <param name="Y">The work's cell.</param>
		/// <param name="LinkCells">Chebyshev step that joins two marks. Below one, nothing links
		/// and every work is its own quarter.</param>
		/// <returns>Indices into <paramref name="Marks"/>, ascending. Empty when nothing is built
		/// within a step of the work, which is a work standing alone and is not a fault.</returns>
		public static List<int> QuarterMarks(IList<KingdomLayoutRules.LayoutMark> Marks, int X, int Y, int LinkCells)
		{
			List<int> cluster = new List<int>();
			if (Marks == null || Marks.Count == 0 || LinkCells < 1)
			{
				return cluster;
			}
			bool[] taken = new bool[Marks.Count];
			List<int> frontier = new List<int>();
			for (int i = 0; i < Marks.Count; i++)
			{
				if (KingdomLayoutRules.Chebyshev(Marks[i].X, Marks[i].Y, X, Y) <= LinkCells)
				{
					taken[i] = true;
					cluster.Add(i);
					frontier.Add(i);
				}
			}
			while (frontier.Count > 0)
			{
				int at = frontier[frontier.Count - 1];
				frontier.RemoveAt(frontier.Count - 1);
				for (int i = 0; i < Marks.Count; i++)
				{
					if (taken[i])
					{
						continue;
					}
					if (KingdomLayoutRules.Chebyshev(Marks[i].X, Marks[i].Y, Marks[at].X, Marks[at].Y) <= LinkCells)
					{
						taken[i] = true;
						cluster.Add(i);
						frontier.Add(i);
					}
				}
			}
			cluster.Sort();
			return cluster;
		}

		/// <summary>
		/// Whether a place stands in a work's quarter: within <paramref name="Radius"/> of the
		/// work itself, or of any built ground in the work's own cluster.
		/// </summary>
		/// <param name="Marks">Everything standing. Null reads as none, which leaves only the
		/// work's own radius.</param>
		/// <param name="WorkX">The work's cell.</param>
		/// <param name="WorkY">The work's cell.</param>
		/// <param name="AtX">The place being asked about.</param>
		/// <param name="AtY">The place being asked about.</param>
		/// <param name="LinkCells">See <see cref="QuarterMarks"/>.</param>
		/// <param name="Radius">How far past built ground the shading carries,
		/// <see cref="QuarterRadius"/>. Negative reads as zero.</param>
		public static bool InQuarter(IList<KingdomLayoutRules.LayoutMark> Marks, int WorkX, int WorkY, int AtX, int AtY, int LinkCells, int Radius)
		{
			int radius = (Radius < 0) ? 0 : Radius;
			if (KingdomLayoutRules.Chebyshev(WorkX, WorkY, AtX, AtY) <= radius)
			{
				return true;
			}
			List<int> cluster = QuarterMarks(Marks, WorkX, WorkY, LinkCells);
			for (int i = 0; i < cluster.Count; i++)
			{
				KingdomLayoutRules.LayoutMark mark = Marks[cluster[i]];
				if (KingdomLayoutRules.Chebyshev(mark.X, mark.Y, AtX, AtY) <= radius)
				{
					return true;
				}
			}
			return false;
		}

		// --- What scopes ----------------------------------------------------------------------

		/// <summary>
		/// Whether a support is one that reach scopes. The three binding goods are drawn and
		/// carried, so they stay citywide pools whatever building supplies them; everything else
		/// &mdash; including a good a third party invents, which
		/// <c>KingdomCatalogueRules.IsKnownSupport</c> already treats as a lift &mdash; shades
		/// only what its work reaches.
		/// </summary>
		public static bool ScopedByReach(string Kind)
		{
			return !KingdomCatalogueRules.IsBindingSupport(Kind);
		}

		/// <summary>
		/// How much of a work's declared amount actually lands, given how well the work is
		/// running. Reach decides where a lift goes; this decides how much of it there is, and it
		/// is the one place staffing and (later) damage compose &mdash; a work at half
		/// effectiveness shades half as hard, and is still a work.
		/// </summary>
		/// <param name="Amount">The declared amount. Negative reads as zero.</param>
		/// <param name="Percent">How well it runs, <c>KingdomRules.CrewEffectiveness</c>'s own
		/// scale. Negative reads as zero; above a hundred is kept, because a future bonus is not
		/// this function's to refuse.</param>
		/// <returns>Floored, never negative. A work running at all keeps at least one point of a
		/// lift it declares, so a shrine with one hand at it is never silently nothing.</returns>
		public static int Scaled(int Amount, int Percent)
		{
			if (Amount <= 0 || Percent <= 0)
			{
				return 0;
			}
			int scaled = Amount * Percent / 100;
			return (scaled < 1) ? 1 : scaled;
		}

		/// <summary>
		/// How much of one work's lift reaches the settlement's own level: the share of the homes
		/// it covers. This is the whole of Addendum 6's second clause on the level side &mdash;
		/// binding needs stay citywide pools, and a lift only lifts the people it actually
		/// reaches, so a shrine among the houses is worth its full amount and the same shrine out
		/// past the fields is worth what it touches.
		/// <para>
		/// Floored the way <see cref="Scaled"/> floors, and for the same reason: a work that
		/// reaches anybody at all keeps one point of what it declares. A work that reaches nobody
		/// keeps nothing here &mdash; it still shades the ground it stands on
		/// (<see cref="Character"/>), which is what an S plot was always for.
		/// </para>
		/// </summary>
		/// <param name="Amount">The work's declared lift, already scaled by how well it runs.
		/// Negative or zero lands nothing.</param>
		/// <param name="Reached">Homes this work covers. Clamped to <paramref name="Homes"/>, so a
		/// caller that double-counts a home cannot mint a lift.</param>
		/// <param name="Homes">Homes the settlement has. Zero or fewer lands nothing: a
		/// settlement with nowhere to live has nobody to lift, and its roof pool binds the level
		/// to the floor anyway.</param>
		public static int Landed(int Amount, int Reached, int Homes)
		{
			if (Amount <= 0 || Reached <= 0 || Homes <= 0)
			{
				return 0;
			}
			int reached = (Reached > Homes) ? Homes : Reached;
			return Scaled(Amount, reached * 100 / Homes);
		}

		/// <summary>The order kinds are listed and ties are broken in: the catalogue's own
		/// lifting supports, in the order it declares them. A kind neither file names keeps its
		/// first-seen place after all of these.</summary>
		public static readonly string[] LiftOrder = KingdomCatalogueRules.LiftingSupports;

		/// <summary>
		/// Folds every lift reaching one piece of ground into what that ground is like: amounts
		/// summed per kind, listed in <see cref="LiftOrder"/> and then first-seen order, with the
		/// loudest kind named.
		/// </summary>
		/// <param name="Lifts">Every in-reach contribution, in any order, repeats allowed. Null
		/// reads as none. Binding supports among them are ignored rather than counted: they are
		/// citywide pools and have no business shading one quarter.</param>
		/// <returns>Never null. <see cref="GroundCharacter.Dominant"/> is null for ground nothing
		/// shades.</returns>
		public static GroundCharacter Character(IEnumerable<KindAmount> Lifts)
		{
			GroundCharacter character = new GroundCharacter();
			if (Lifts == null)
			{
				return character;
			}
			List<string> kinds = new List<string>();
			List<int> amounts = new List<int>();
			foreach (KindAmount lift in Lifts)
			{
				string kind = Fold(lift.Kind);
				if (kind == null || lift.Amount <= 0 || !ScopedByReach(kind))
				{
					continue;
				}
				int at = kinds.IndexOf(kind);
				if (at < 0)
				{
					kinds.Add(kind);
					amounts.Add(lift.Amount);
				}
				else
				{
					amounts[at] += lift.Amount;
				}
			}
			int[] order = new int[kinds.Count];
			for (int i = 0; i < kinds.Count; i++)
			{
				int rank = IndexIn(LiftOrder, kinds[i]);
				order[i] = (rank < 0) ? (LiftOrder.Length + i) : rank;
			}
			for (int i = 0; i < kinds.Count; i++)
			{
				int best = -1;
				for (int j = 0; j < kinds.Count; j++)
				{
					if (amounts[j] >= 0 && (best < 0 || order[j] < order[best]))
					{
						best = j;
					}
				}
				if (best < 0)
				{
					break;
				}
				character.Lifts.Add(new KindAmount(kinds[best], amounts[best]));
				character.Total += amounts[best];
				if (amounts[best] > character.DominantAmount)
				{
					character.Dominant = kinds[best];
					character.DominantAmount = amounts[best];
				}
				amounts[best] = -1;
			}
			return character;
		}

		// --- What the ground is called ----------------------------------------------------------

		/// <summary>What a lift is called in a sentence. A kind this build does not know is said
		/// as itself rather than dressed up in prose that would be a guess.</summary>
		public static string CharacterWord(string Kind)
		{
			switch (Fold(Kind))
			{
			case "spirit":
				return "faith";
			case "learning":
				return "learning";
			case "craft":
				return "craft";
			case "order":
				return "order";
			case "luxury":
				return "comfort";
			default:
				return Fold(Kind) ?? "nothing";
			}
		}

		/// <summary>
		/// What the settlement calls ground of this character. Names the quarter the way the
		/// people living there would &mdash; never a district, never a type, just the phrase a
		/// founder reads and recognises.
		/// </summary>
		public static string QuarterName(string Kind)
		{
			switch (Fold(Kind))
			{
			case "spirit":
				return "the temple quarter";
			case "learning":
				return "the scribes' quarter";
			case "craft":
				return "the workers' quarter";
			case "order":
				return "the watch's quarter";
			case "luxury":
				return "the fine quarter";
			case null:
				return "ordinary ground";
			default:
				return "a quarter of its own";
			}
		}

		/// <summary>
		/// One line naming what shades the ground the founder is standing on, for the status
		/// report. Ground nothing reaches says exactly that rather than nothing at all, so the
		/// surface is readable before the first shrine as well as after it.
		/// </summary>
		public static string QuarterLine(GroundCharacter Character)
		{
			if (Character == null || Character.Lifts.Count == 0)
			{
				return "This ground: ordinary ground, shaded by nothing standing near it.";
			}
			string list = "";
			for (int i = 0; i < Character.Lifts.Count; i++)
			{
				list += ((i == 0) ? "" : ", ") + CharacterWord(Character.Lifts[i].Kind) + " " + Character.Lifts[i].Amount;
			}
			return "This ground: " + QuarterName(Character.Dominant) + " — " + list + ".";
		}

		/// <summary>The clause naming how far a design carries, for the catalogue's own
		/// description of it.</summary>
		public static string ReachClause(ReachBand Band)
		{
			switch (Band)
			{
			case ReachBand.Quarter:
				return "shades its own quarter";
			case ReachBand.Zone:
				return "shades everything built around it";
			case ReachBand.City:
				return "shades the whole city, while somebody heads it";
			case ReachBand.Realm:
				return "shades the whole realm, while somebody heads it";
			default:
				return "shades the ground it stands on";
			}
		}

		// --- Seats: the great work is an office ---------------------------------------------

		/// <summary>
		/// Whether a band only works while a named notable heads it. The great works, and only
		/// them: an S plot is any hands, forever, and that is the point of it.
		/// </summary>
		public static bool RequiresSeat(ReachBand Band)
		{
			return Band >= ReachBand.City;
		}

		/// <summary>
		/// What a great work reaches while no one heads it. Never nothing: the temple with no
		/// keeper of rites is still a temple to the people who live beside it, so it drops to the
		/// zone it stands in and says so once (STANDARDS 7b). Nothing here ever closes a work,
		/// and nothing here decays &mdash; naming a keeper restores the band the same pass.
		/// </summary>
		public static ReachBand Unheaded(ReachBand Band)
		{
			return RequiresSeat(Band) ? ReachBand.Zone : Band;
		}

		/// <summary>
		/// What the settlement calls whoever heads a work of this purpose. A name, never a rank
		/// &mdash; the same posture <c>KingdomOfficeRules.OfficeTitles</c> takes with the
		/// settlement's own office. A category this build does not know gets the plain title
		/// rather than a guess at somebody else's vocabulary.
		/// </summary>
		/// <param name="Category">A <c>BuildEntry.Category</c>. Null or unknown reads as the
		/// plain keeper.</param>
		public static string SeatTitle(string Category)
		{
			switch (Fold(Category))
			{
			case "faith":
				return "keeper of rites";
			case "knowledge":
				return "archivist";
			case "craft":
				return "master of the yard";
			case "food":
				return "reeve of the fields";
			case "storage":
				return "warden of the stores";
			case "defense":
			case "defence":
				return "captain of the watch";
			case "housing":
				return "steward of the house";
			case "civic":
				return "steward";
			case "memorial":
				return "keeper of the names";
			default:
				return "keeper";
			}
		}

		/// <summary>
		/// How well one settler would head a work of this purpose, read off who they already are.
		/// Derive-first: nothing is assigned, nothing is trained, and the founder chooses nobody
		/// &mdash; a settler another mod shipped is scored by the same attributes the game gives
		/// every creature.
		/// <para>
		/// The governing attribute doubles and a second one counts once, so a candidate is
		/// plainly better at the thing the work does rather than plainly better in general.
		/// </para>
		/// </summary>
		/// <returns>Never negative. Zero for a candidate with nothing to bring.</returns>
		public static int SeatFitness(string Category, int Strength, int Agility, int Toughness, int Intelligence, int Willpower, int Ego)
		{
			int primary;
			int secondary;
			switch (Fold(Category))
			{
			case "faith":
				primary = Willpower;
				secondary = Ego;
				break;
			case "knowledge":
				primary = Intelligence;
				secondary = Willpower;
				break;
			case "craft":
				// Addendum 7's own reading of a crew: strength is what stonework and haulage
				// actually ask for, and the hand comes after the arm.
				primary = Strength;
				secondary = Agility;
				break;
			case "food":
				primary = Toughness;
				secondary = Strength;
				break;
			case "storage":
				primary = Toughness;
				secondary = Intelligence;
				break;
			case "defense":
			case "defence":
				primary = Strength;
				secondary = Toughness;
				break;
			default:
				// Including every category a third party invents: who the settlement listens to,
				// which is the honest answer when nobody has said what the work is.
				primary = Ego;
				secondary = Willpower;
				break;
			}
			int score = (2 * Clamp(primary)) + Clamp(secondary);
			return (score < 0) ? 0 : score;
		}

		/// <summary>
		/// How much better a challenger must be before a seated notable is replaced. Without it
		/// the seat would change hands whenever two settlers' attributes happened to swap order,
		/// and the chronicle would fill with an office nobody actually lost.
		/// </summary>
		public const int SeatUnseatMargin = 3;

		/// <summary>Whether a challenger takes a seated notable's place. An empty seat is taken
		/// by anybody, which is <c>IncumbentScore</c> below zero.</summary>
		public static bool ShouldUnseat(int IncumbentScore, int ChallengerScore)
		{
			if (IncumbentScore < 0)
			{
				return ChallengerScore >= 0;
			}
			return ChallengerScore >= IncumbentScore + SeatUnseatMargin;
		}

		/// <summary>
		/// The line a great work with nobody at its head gives the founder, once (STANDARDS 7b).
		/// Names what would lift it, because a founder who cannot see why the city stopped
		/// gaining anything from its own cathedral is the exact failure the rule exists for.
		/// </summary>
		/// <param name="WorkName">What the founder calls the work.</param>
		/// <param name="Title">From <see cref="SeatTitle"/>.</param>
		public static string UnheadedLine(string WorkName, string Title)
		{
			string name = string.IsNullOrEmpty(WorkName) ? "the great work" : WorkName;
			string title = string.IsNullOrEmpty(Title) ? "keeper" : Title;
			return "{{W|" + name + " stands, and no " + title + " has been named. It keeps its own ground until one is.}}";
		}

		/// <summary>
		/// The chronicle's telling of a work's seat changing hands, or empty for
		/// <c>OfficeTransition.None</c>, which is never announced. Deliberately classified by
		/// <c>KingdomOfficeRules.ClassifyTransition</c> rather than by a second rule of its own:
		/// a great work IS an office, and there is one grammar for an office changing hands.
		/// </summary>
		public static string SeatChronicle(KingdomOfficeRules.OfficeTransition Transition, string Title, string Holder, string WorkName)
		{
			string name = string.IsNullOrEmpty(WorkName) ? "the great work" : WorkName;
			switch (Transition)
			{
			case KingdomOfficeRules.OfficeTransition.FirstHolder:
				return Holder + " is named " + Title + " of " + name;
			case KingdomOfficeRules.OfficeTransition.Passed:
				return "the office of " + Title + " of " + name + " passes to " + Holder;
			case KingdomOfficeRules.OfficeTransition.Vacant:
				return name + " has no " + Title + " left to head it";
			default:
				return "";
			}
		}

		/// <summary>The line spoken live when a seat changes hands, or empty when the chronicle
		/// has nothing to say either.</summary>
		public static string SeatMessage(KingdomOfficeRules.OfficeTransition Transition, string Title, string Holder, string WorkName)
		{
			string chronicle = SeatChronicle(Transition, Title, Holder, WorkName);
			if (chronicle.Length == 0)
			{
				return "";
			}
			string colour = (Transition == KingdomOfficeRules.OfficeTransition.Vacant) ? "r" : "W";
			return "{{" + colour + "|" + char.ToUpperInvariant(chronicle[0]) + chronicle.Substring(1) + ".}}";
		}

		// --- Small shared helpers -------------------------------------------------------------

		private static int Clamp(int Value)
		{
			return (Value < 0) ? 0 : Value;
		}

		private static int IndexIn(string[] Set, string Value)
		{
			for (int i = 0; i < Set.Length; i++)
			{
				if (Set[i] == Value)
				{
					return i;
				}
			}
			return -1;
		}

		private static string Join(string[] Values)
		{
			string joined = "";
			for (int i = 0; i < Values.Length; i++)
			{
				if (i > 0)
				{
					joined += (i == Values.Length - 1) ? " and " : ", ";
				}
				joined += Values[i];
			}
			return joined;
		}

		/// <summary>Trims and lower-cases one token. Null for anything that was only space, so
		/// every caller has one thing to test rather than two.</summary>
		private static string Fold(string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return null;
			}
			string trimmed = Value.Trim().ToLowerInvariant();
			return (trimmed.Length == 0) ? null : trimmed;
		}
	}
}
