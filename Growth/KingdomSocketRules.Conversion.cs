using System;

namespace ThousandAndFirst
{
	public static partial class KingdomSocketRules
	{
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
			string verb = (Kind == ChangeKind.SameSet) ? "Changing" : "Restaking this lot and changing";
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
