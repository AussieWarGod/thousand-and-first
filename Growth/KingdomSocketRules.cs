using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for the plot as a SOCKET (BUILDING-CATALOGUE-BRIEF.md's addendum,
	/// 2026-08-21): condemning a plot-raised building keeps its ground a re-buildable slot rather
	/// than turning it into wilderness, "change what this plot is" is one strike-and-rebuild
	/// ceremony rather than two separate errands, and re-dressing a standing building in any
	/// registered skin is a trivial, non-structural act.
	/// <para>
	/// Addendum 2 (also 2026-08-21) types the plot by
	/// (<c>Category</c> &times; <c>KingdomPlotRules.PlotSize</c>) and splits the ceremony into two
	/// verbs: <em>change the building</em>, cheap and ordinary, when the chosen design shares the
	/// standing building's own (type, size); and <em>re-type the plot</em>, rare, when it does
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
	public static class KingdomSocketRules
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

			/// <summary>A different type, a different size, or both: "re-type the plot", the
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

		// --- Conversion: one figure, before anything moves ----------------------------------------

		/// <summary>
		/// What one strike-and-rebuild ceremony adds up to: the old work's own strike effort (in
		/// crew-days for one pair of hands, the same unit an ordinary strike is quoted in), what
		/// striking it returns to the stockpiles, the new work's full water cost, and what the new
		/// work's material cost nets to once the old work's own salvage is credited against it.
		/// <para>
		/// Payment itself still goes through the ordinary, unmodified paths &mdash; the new
		/// design's full water and material cost is what is actually drawn from the stores at
		/// order time, exactly as an ordinary commission draws it, and the old design's own strike
		/// still returns its own salvage on its own schedule when the crew actually finishes
		/// taking it down. <see cref="ConversionQuote.NetMaterials"/> is the honest ACCOUNTING of
		/// what the whole affair nets to for the founder's own understanding &mdash; the figure
		/// the addendum asks be disclosed once, before anything moves &mdash; not a second,
		/// discounted payment mechanism sitting beside the first.
		/// </para>
		/// <para>This full strike-and-rebuild quote belongs only to <see
		/// cref="ChangeKind.Retype"/>. Same-set changes are priced by
		/// <see cref="AssessPlanChange"/> from their exact authored route and perform neither
		/// strike nor salvage.</para>
		/// </summary>
		public struct ConversionQuote
		{
			/// <summary>Effort points the strike costs, from the old design's own material cost
			/// and water cost &mdash; <c>KingdomMaterialRules.StrikeEffort</c>, unchanged.</summary>
			public int StrikeEffort;

			/// <summary>Days a single pair of hands would need to work the strike off.</summary>
			public int EffortDays;

			/// <summary>What striking the old work returns to the stockpiles. Never null.</summary>
			public KingdomMaterialTally Salvage;

			/// <summary>The new design's own full water cost. Charged in full; salvage never
			/// refunds water, matching the lifecycle rule that an ordinary strike refunds none
			/// either.</summary>
			public int NewDrams;

			/// <summary>The new design's own full material cost, before any salvage credit. Never
			/// null.</summary>
			public KingdomMaterialTally NewMaterials;

			/// <summary>The new design's material cost after the old design's own salvage is
			/// credited against it, per material, floored at zero. Never null. The figure the
			/// founder is actually told the conversion "nets to" in material.</summary>
			public KingdomMaterialTally NetMaterials;

			/// <summary>Exact authored work duration; zero for ordinary strike/rebuild quotes.</summary>
			public long WorkTicks;
		}

		/// <summary>Quotes only an explicitly authored same-set transition delta.</summary>
		public static ConversionQuote AssessPlanChange(KingdomSocketTransition Transition)
		{
			ConversionQuote quote = default(ConversionQuote);
			quote.Salvage = new KingdomMaterialTally();
			quote.NewDrams = Transition == null ? 0 : Transition.WaterDrams;
			quote.NewMaterials = Transition == null || Transition.Materials == null
				? new KingdomMaterialTally() : Transition.Materials;
			quote.NetMaterials = quote.NewMaterials.Copy();
			quote.WorkTicks = Transition == null ? 0L : Transition.WorkTicks;
			return quote;
		}

		/// <summary>
		/// Composes one <see cref="ConversionQuote"/> from the two designs' own registered costs.
		/// Pure arithmetic over <c>KingdomMaterialRules</c>' own strike math and
		/// <c>KingdomMaterialTally.Add</c>'s own zero-floor, so nothing here re-derives either.
		/// </summary>
		/// <param name="OldMaterialCost">The standing design's registered material cost. Null
		/// reads as empty, which is every design written before materials existed.</param>
		/// <param name="OldCostDrams">The standing design's registered water cost.</param>
		/// <param name="NewMaterialCost">The chosen design's registered material cost. Null reads
		/// as empty.</param>
		/// <param name="NewCostDrams">The chosen design's registered water cost.</param>
		public static ConversionQuote AssessConversion(KingdomMaterialTally OldMaterialCost, int OldCostDrams, KingdomMaterialTally NewMaterialCost, int NewCostDrams)
		{
			KingdomMaterialTally oldCost = OldMaterialCost ?? new KingdomMaterialTally();
			KingdomMaterialTally newCost = NewMaterialCost ?? new KingdomMaterialTally();
			ConversionQuote quote = default;
			quote.StrikeEffort = KingdomMaterialRules.StrikeEffort(oldCost.Total(), (OldCostDrams > 0) ? OldCostDrams : 0);
			quote.EffortDays = KingdomMaterialRules.DaysForOneHand(quote.StrikeEffort);
			quote.Salvage = KingdomMaterialRules.StrikeSalvage(oldCost);
			quote.NewDrams = (NewCostDrams > 0) ? NewCostDrams : 0;
			quote.NewMaterials = newCost;
			quote.NetMaterials = newCost.Copy();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial material = (KingdomMaterial)i;
				quote.NetMaterials.Add(material, -quote.Salvage.Get(material));
			}
			return quote;
		}

		/// <summary>
		/// The one disclosed figure, composed before anything moves: the strike's own cost in
		/// crew-days, what it returns, and the new work's own water and net material cost. Every
		/// piece is named, because a founder asked to confirm a single number they cannot see the
		/// parts of is not a disclosure.
		/// </summary>
		public static string DescribeConversion(string OldName, string NewName, ChangeKind Kind, ConversionQuote Quote)
		{
			if (Kind == ChangeKind.SameSet)
			{
				string materials = Quote.NetMaterials.Describe();
				return "Changing the " + OldName + " into a " + NewName
					+ " keeps its exact lot, facing, and standing fabric. The declared change costs {{C|"
					+ Quote.NewDrams + " drams"
					+ (materials == null ? "" : " and " + materials)
					+ "}} and takes {{C|" + Quote.WorkTicks + " ticks}}. Nothing is struck or salvaged.";
			}
			string verb = (Kind == ChangeKind.SameSet) ? "Changing" : "Re-typing this plot and changing";
			string salvage = Quote.Salvage.Describe();
			string net = Quote.NetMaterials.Describe();
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			text.Append(verb).Append(" the ").Append(OldName).Append(" into a ").Append(NewName).Append(" costs ")
				.Append("{{C|").Append(Quote.EffortDays).Append((Quote.EffortDays == 1) ? " hand-day" : " hand-days").Append("}} to strike it, and ")
				.Append("{{C|").Append(Quote.NewDrams).Append(" drams");
			if (net != null)
			{
				text.Append(" and ").Append(net);
			}
			text.Append("}} to raise the ").Append(NewName).Append(" in its place");
			text.Append((salvage == null) ? ". Nothing of the old work is worth keeping." : (" — {{C|" + salvage + "}} comes back out of the old walls first."));
			text.Append(" No water is ever refunded.");
			return text.ToString();
		}
	}
}
