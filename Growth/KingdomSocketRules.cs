using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for a reserved lot as a SOCKET (BUILDING-CATALOGUE-BRIEF.md's addendum,
	/// 2026-08-21): condemning a building keeps its reserved ground as a re-buildable lot rather
	/// than turning it into wilderness, and changing what stands on the lot is one strike-and-rebuild
	/// ceremony rather than two separate errands, and re-dressing a standing building in any
	/// registered skin is a trivial, non-structural act.
	/// <para>
	/// Addendum 2 (also 2026-08-21) types the plot by
	/// (<c>Category</c> &times; <c>KingdomPlotRules.PlotSize</c>) and splits the ceremony into two
	/// verbs: <em>change the building</em>, cheap and ordinary, when the chosen design shares the
	/// standing building's own (type, size); and <em>restake the lot</em>, rare, when it does
	/// not. <see cref="ClassifyChange"/> is that split, applied at the socket eligibility layer as
	/// directed. Same-set changes use only an explicit transition declaration; true retypes use
	/// the ordinary full strike and fresh-siting bill.
	/// </para>
	/// <para>
	/// Nothing here touches <c>XRL</c>. The engine-coupled half &mdash; reading a real plot's
	/// rect, sweeping its walls and floor, staking the replacement, applying a skin &mdash; is
	/// <c>KingdomSocket</c>, in the same folder.
	/// </para>
	/// </summary>
	public static partial class KingdomSocketRules
	{
		/// <summary>
		/// Which of the two Addendum-2 verbs a change is. Classification only: nothing here
		/// decides eligibility by classification alone. A same-set choice also needs an exact,
		/// directional <see cref="KingdomSocketTransition"/> for the standing actual size; a
		/// retype uses the ordinary fresh-siting and full-build path.
		/// </summary>
		public enum ChangeKind
		{
			/// <summary>Same (Category, PlotSize) as what already stands: "change the building",
			/// the cheap, ordinary verb.</summary>
			SameSet = 0,

			/// <summary>A different type, a different size, or both: "restake the lot", the
			/// rare, full ceremony.</summary>
			Retype = 1
		}

		/// <summary>
		/// Folds a <c>Category</c> for comparison the same way
		/// <c>KingdomCatalogueRules</c>'s own private <c>Fold</c> does for its chain-category
		/// check: trim, lower-case, and treat blank as blank rather than as a wildcard, so two
		/// designs that both forgot to declare a category are not silently treated as the same
		/// one.
		/// </summary>
		private static string Fold(string Value)
		{
			return string.IsNullOrWhiteSpace(Value) ? "" : Value.Trim().ToLowerInvariant();
		}

		/// <summary>
		/// Classifies a change from one design's (Category, PlotSize) to another's. Size is
		/// compared first and exactly &mdash; a design that keeps its category but wants more or
		/// less ground is still re-typing the plot, because the plot's own ceiling is what is
		/// actually changing.
		/// </summary>
		public static ChangeKind ClassifyChange(string CurrentCategory, KingdomPlotRules.PlotSize CurrentSize, string TargetCategory, KingdomPlotRules.PlotSize TargetSize)
		{
			if (CurrentSize != TargetSize)
			{
				return ChangeKind.Retype;
			}
			return (Fold(CurrentCategory) == Fold(TargetCategory)) ? ChangeKind.SameSet : ChangeKind.Retype;
		}

		/// <summary>Recovers the actual staked tier, accepting a quarter-turned rectangle.</summary>
		public static bool TryActualSize(int Width, int Height,
			out KingdomPlotRules.PlotSize Size)
		{
			Size = KingdomPlotRules.PlotSize.None;
			for (int i = (int)KingdomPlotRules.PlotSize.Small;
				i <= (int)KingdomPlotRules.PlotSize.Huge; i++)
			{
				KingdomPlotRules.PlotSize candidate = (KingdomPlotRules.PlotSize)i;
				int width;
				int height;
				if (KingdomPlotRules.TryDimensions(candidate, out width, out height)
					&& ((Width == width && Height == height)
						|| (Width == height && Height == width)))
				{
					Size = candidate;
					return true;
				}
			}
			return false;
		}

		/// <summary>Whether the target can inhabit the standing typed lot without resizing it.</summary>
		public static bool FitsSameSet(string CurrentCategory,
			KingdomPlotRules.PlotSize ActualSize, string TargetCategory,
			KingdomPlotRules.PlotSize TargetMinimum)
		{
			return ActualSize != KingdomPlotRules.PlotSize.None
				&& TargetMinimum != KingdomPlotRules.PlotSize.None
				&& ActualSize >= TargetMinimum
				&& Fold(CurrentCategory) != ""
				&& Fold(CurrentCategory) == Fold(TargetCategory);
		}

		/// <summary>The word a confirmation or a chronicle line uses for a <see cref="ChangeKind"/>.</summary>
		public static string VerbFor(ChangeKind Kind)
		{
			return (Kind == ChangeKind.SameSet) ? "changed" : "re-typed";
		}

		// --- Footprint fit: "footprint <= plot", checked at rebuild too, not only at first stake ---

		/// <summary>
		/// Whether a design's footprint fits ground of the given size. The addendum's own
		/// invariant ("footprint belongs to the building's tier, never the plot... the sole
		/// invariant is footprint &lt;= plot, validator-checked at load and refused by name at
		/// upgrade") applied a second time, at the socket: a plot staked small stays small until
		/// it is struck and re-staked, so a founder converting a hut plot into a design that wants
		/// a hall is refused here by exactly the same rule that refuses it at first commission.
		/// </summary>
		public static bool FootprintFits(int PlotWidth, int PlotHeight, int NeedWidth, int NeedHeight)
		{
			return NeedWidth <= PlotWidth && NeedHeight <= PlotHeight;
		}

		/// <summary>Names the design and the shortfall. Never silent (STANDARDS 7b): a refusal
		/// this file hands back always says what would lift it &mdash; a bigger plot, or a
		/// smaller design.</summary>
		public static string RefuseTooSmall(string DesignName, int PlotWidth, int PlotHeight, int NeedWidth, int NeedHeight)
		{
			return "A " + DesignName + " wants " + NeedWidth + " by " + NeedHeight
				+ " to stand in, and this plot is only " + PlotWidth + " by " + PlotHeight
				+ ". Strike it and stake wider ground, or choose something this plot can actually hold.";
		}

		/// <summary>Convert and re-dress are both socket acts: they touch ground the settlement
		/// itself raised. Adoption is a mark on ground the founder built by hand, and it owns that
		/// ground until it is released &mdash; the socket never reaches for it.</summary>
		public static string RefuseAdopted(string BuildingName)
		{
			return "The " + BuildingName + " was adopted, not raised. It stands because the founder built it, and only the founder's own hands take it down or change what it is. Release the adoption first if the settlement should own this ground outright.";
		}

		/// <summary>Neither the standing design nor the one being asked for names a plot at all.
		/// The socket only ever holds a rect a plot design laid out; a single-cell work has no
		/// ground to keep.</summary>
		public static string RefuseNotAPlot(string Name)
		{
			return "The " + Name + " does not stand on a plot of its own. Strike it and commission the next thing fresh, the ordinary way.";
		}

		/// <summary>The design asked for is already what stands here. Converting a thing into
		/// itself has nothing to do and nothing to cost.</summary>
		public static string RefuseAlreadyThat(string Name)
		{
			return "That is already what the " + Name + " is.";
		}

		/// <summary>A strike is already ordered on this building, with no conversion attached.
		/// Converting on top of it would be a second order for the same crew on the same walls.</summary>
		public static string RefuseCondemned(string Name)
		{
			return "The " + Name + " is already condemned and waiting on the crew. Call that order off first, or let it finish, before converting it into something else.";
		}

		/// <summary>The founder's own standing "leave this one as it is", or an improvement
		/// already under way. Both are the founder's own intent for this exact work; converting
		/// out from under either would throw away a choice already made.</summary>
		public static string RefuseImproving(string Name)
		{
			return "The " + Name + " is in the middle of being bettered into something else already. Let that finish, or call it off, before converting it into a different kind of thing entirely.";
		}

		/// <summary>The skin key a founder chose does not resolve against the standing building's
		/// own current design &mdash; not authored for it, spelled differently, or withdrawn by
		/// whichever mod added it since. <c>KingdomDesignRules.FindSkin</c>'s own contract is "never
		/// throws on a stale or third-party-withdrawn key"; this is the founder-facing sentence for
		/// exactly that case, so the tile it would have painted with is unknown by that name.</summary>
		public static string RefuseUnknownSkin(string RequestedKey, string BuildingName)
		{
			return "The settlement knows no look called {{C|" + RequestedKey + "}} for the " + BuildingName
				+ ". Its tile stays what it is, because a tile nobody can name is a tile nobody can paint with.";
		}

		/// <summary>The building's own design no longer resolves in the catalogue at all &mdash;
		/// a third-party file that added it was removed. There is nothing to re-look-up a skin
		/// list against.</summary>
		public static string RefuseUnknownDesign(string BuildingName)
		{
			return "Whatever design raised the " + BuildingName + " is no longer in the catalogue. Nothing here can say what it is allowed to look like.";
		}

		// --- Re-dress: trivial cost, no I/O change ------------------------------------------------

		/// <summary>
		/// Share of a design's own material cost that re-dressing it draws down. A tenth, and
		/// rounded down like every other partial draw in this economy (<see
		/// cref="KingdomMaterialTally.Scaled"/>): a coat of colour is not a second building, and a
		/// water-only design (or a cheap one) costs nothing at all to re-dress, which is exactly
		/// the "trivial" the addendum asks for.
		/// </summary>
		public const int RedressCostPercent = 10;

		/// <summary>What re-dressing a building of the given full build cost draws from the
		/// stockpiles. Never null; empty for a water-only design, matching the whole material
		/// economy's own "an absent cost is empty" rule.</summary>
		public static KingdomMaterialTally RedressCost(KingdomMaterialTally BuildCost)
		{
			return (BuildCost ?? new KingdomMaterialTally()).Scaled(RedressCostPercent);
		}

	}
}
