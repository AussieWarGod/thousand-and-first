using System.Collections.Generic;

namespace ThousandAndFirst
{
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
	public static partial class KingdomReachRules
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

		/// <summary>The plot tier an exact designation physically occupies. Runtime reach uses
		/// this instead of trusting the catalogue tier named by an adopted or external room.</summary>
		public static KingdomPlotRules.PlotSize SizeForDesignation(
			IReadOnlyList<KingdomBenefitCell> Cells)
		{
			int count = 0;
			for (int i = 0; Cells != null && i < Cells.Count; i++)
				if ((Cells[i].Use & KingdomBenefitCellUse.Plot) != 0) count++;
			if (count <= 0) return KingdomPlotRules.PlotSize.None;
			if (count <= KingdomPlotRules.SmallWidth * KingdomPlotRules.SmallHeight)
				return KingdomPlotRules.PlotSize.Small;
			if (count <= KingdomPlotRules.MediumWidth * KingdomPlotRules.MediumHeight)
				return KingdomPlotRules.PlotSize.Medium;
			if (count <= KingdomPlotRules.LargeWidth * KingdomPlotRules.LargeHeight)
				return KingdomPlotRules.PlotSize.Large;
			return KingdomPlotRules.PlotSize.Huge;
		}

		/// <summary>Exact membership, not the bounding rectangle: gaps in an irregular adopted
		/// room remain outside that room's plot-band reach.</summary>
		public static bool ContainsPlotCell(IReadOnlyList<KingdomBenefitCell> Cells, int X, int Y)
		{
			for (int i = 0; Cells != null && i < Cells.Count; i++)
				if (Cells[i].X == X && Cells[i].Y == Y
					&& (Cells[i].Use & KingdomBenefitCellUse.Plot) != 0) return true;
			return false;
		}

		/// <summary>Whether one effective physical amount is a subsistence lift. The benefit
		/// index also carries structural defence, which is a different economic channel and must
		/// never become comfort merely because it is non-binding. Unknown catalogue support kinds
		/// remain lifts for third-party compatibility.</summary>
		public static bool IsPhysicalLift(string Kind)
		{
			return !string.Equals((Kind ?? "").Trim(), "defence",
				System.StringComparison.OrdinalIgnoreCase) && ScopedByReach(Kind);
		}

		/// <summary>Stable quarter anchor. Prefer the root only when it stands on the exact
		/// designation; otherwise use the first normalized plot cell.</summary>
		public static bool TryDesignationAnchor(IReadOnlyList<KingdomBenefitCell> Cells,
			int PreferredX, int PreferredY, out int X, out int Y)
		{
			X = 0; Y = 0;
			if (ContainsPlotCell(Cells, PreferredX, PreferredY))
			{
				X = PreferredX; Y = PreferredY; return true;
			}
			for (int i = 0; Cells != null && i < Cells.Count; i++)
				if ((Cells[i].Use & KingdomBenefitCellUse.Plot) != 0)
				{
					X = Cells[i].X; Y = Cells[i].Y; return true;
				}
			return false;
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

	}
}
