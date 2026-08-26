using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCatalogueRules
	{
		private static void ValidateEntry(CatalogueEntry Entry, Dictionary<string, CatalogueEntry> ByKey, List<CatalogueFinding> Findings)
		{
			// Defence is an output, not a siting override. A plotted watch-lodge keeps its whole
			// authored lot and contributes its base rating; only a defensive design with no plot is
			// a frontier work. KingdomRules.IsFrontierWork is the shared runtime law.
			if (Entry.Open && !string.IsNullOrEmpty(Entry.Contents))
			{
				// An open plot has no interior, so the table would furnish the weather.
				Findings.Add(new CatalogueFinding(Entry.Key, "Contents", CatalogueSeverity.Note,
					"building " + Entry.Key + " is an open plot and names furnishings; there is no interior to put them in"));
			}
			if (Entry.MinStage < KingdomPlotRules.StageForSize(Entry.Plot))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "MinStage", CatalogueSeverity.Note,
					"building " + Entry.Key + " is offered from " + StageWord(Entry.MinStage) + " but wants " + PlotWord(Entry.Plot) + ", so it waits for " + StageWord(KingdomPlotRules.StageForSize(Entry.Plot)) + " anyway"));
			}

			List<KindAmount> carries;
			if (!TryParseTally(Entry.Carries, out carries, out var carriesError))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Carries", CatalogueSeverity.Fault,
					"building " + Entry.Key + " has a bad Carries: " + carriesError));
			}
			for (int i = 0; i < carries.Count; i++)
			{
				if (!IsKnownSupport(carries[i].Kind))
				{
					Findings.Add(new CatalogueFinding(Entry.Key, "Carries", CatalogueSeverity.Note,
						"building " + Entry.Key + " carries " + carries[i].Kind + ", which nothing binds on; it lifts the level instead"));
				}
			}
			// The material vocabulary and its parser belong to KingdomMaterialRules; this only
			// reports the verdict, so a seventh material never has to be added in two places.
			if (!KingdomMaterialRules.TryParseMaterialCost(Entry.Materials, out _, out var materialsError))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Materials", CatalogueSeverity.Fault,
					"building " + Entry.Key + " has a bad Materials: " + materialsError));
			}
			if (Entry.Staff > 0 && Entry.Defence == 0 && carries.Count == 0)
			{
				// Buildings are people: a work that takes a crew off the water detail and adds
				// nothing to what the settlement carries is a net loss the founder cannot see.
				Findings.Add(new CatalogueFinding(Entry.Key, "Carries", CatalogueSeverity.Note,
					"building " + Entry.Key + " takes a crew of " + Entry.Staff + " and adds nothing to what the settlement carries"));
			}
			if (Entry.Staff == 0 && Fold(Entry.Manning) == "threshold")
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Manning", CatalogueSeverity.Note,
					"building " + Entry.Key + " sets Manning but wants no staff, so the setting decides nothing"));
			}
			string manning = Fold(Entry.Manning);
			if (manning != null && manning != "scaled" && manning != "threshold")
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Manning", CatalogueSeverity.Note,
					"building " + Entry.Key + " has a Manning of " + manning + ", which is neither scaled nor threshold"));
			}
			if (KingdomZoningRules.NaturalDistricts(Entry.Category) == null)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Category", CatalogueSeverity.Note,
					"building " + Entry.Key + " is filed under " + (Fold(Entry.Category) ?? "nothing") + ", which no district claims; the plan will build it where the founder stands"));
			}
			ValidateFootprint(Entry, Findings);
			// A tier that DECLARED its roof has made a claim the design can contradict. A design
			// that declared nothing has claimed nothing, and is raised exactly as it always was.
			if (Entry.RequiresSky && Entry.RoofDeclared && !KingdomPlotRules.AdmitsSky(Entry.Roof))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Roof", CatalogueSeverity.Fault,
					"building " + Entry.Key + " needs weather and declares a tier that is " + KingdomPlotRules.RoofWord(Entry.Roof)
					+ "; it would be refused wherever it was raised" + Layered(Entry)));
			}
			int roofCapacity = AmountOf(carries, SupportRoof);
			bool claimsHousing = Fold(Entry.Category) == "housing" || roofCapacity > 0;
			if (Entry.Plot != KingdomPlotRules.PlotSize.None && claimsHousing
				&& !KingdomPlotRules.HoldsBeds(Entry.Roof))
			{
				Findings.Add(new CatalogueFinding(Entry.Key,
					roofCapacity > 0 ? "Carries" : "Roof", CatalogueSeverity.Fault,
					"building " + Entry.Key + (roofCapacity > 0
						? " carries roof capacity " + roofCapacity : " is filed as housing")
						+ " but its effective roof is " + KingdomPlotRules.RoofWord(Entry.Roof)
						+ "; nobody sleeps in the open" + Layered(Entry)));
			}
			ValidateChain(Entry, ByKey, Findings);
		}

		/// <summary>
		/// The sole footprint invariant: footprint &le; plot. The tier declares what it covers and
		/// the plot is only the envelope, so nothing here has an opinion about how big a tier
		/// should be &mdash; only about whether it fits on the ground the founder staked.
		/// <para>
		/// This is the check merge-by-key most needs. One file may declare the tier and its
		/// footprint; a second, wanting a smaller building, may override nothing but <c>Plot</c>.
		/// Neither file is wrong on its own and neither author can see the other's, so the only
		/// place the contradiction exists is the merged design &mdash; here.
		/// </para>
		/// </summary>
		private static void ValidateFootprint(CatalogueEntry Entry, List<CatalogueFinding> Findings)
		{
			if (Entry.FootprintWidth <= 0 && Entry.FootprintHeight <= 0)
			{
				return;
			}
			if (Entry.FootprintWidth <= 0 || Entry.FootprintHeight <= 0)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Footprint", CatalogueSeverity.Fault,
					"building " + Entry.Key + " declares a footprint of " + Entry.FootprintWidth + " by " + Entry.FootprintHeight + "; a footprint needs both a width and a height" + Layered(Entry)));
				return;
			}
			if (Entry.Plot == KingdomPlotRules.PlotSize.None)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Footprint", CatalogueSeverity.Fault,
					"building " + Entry.Key + " declares a footprint of " + Entry.FootprintWidth + " by " + Entry.FootprintHeight + " and no plot to stand it in" + Layered(Entry)));
				return;
			}
			int width;
			int height;
			if (!KingdomPlotRules.TryDimensions(Entry.Plot, out width, out height))
			{
				return;
			}
			if (Entry.FootprintWidth > width || Entry.FootprintHeight > height)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Footprint", CatalogueSeverity.Fault,
					"building " + Entry.Key + " covers " + Entry.FootprintWidth + " by " + Entry.FootprintHeight + " and stands on " + PlotWord(Entry.Plot) + ", which is " + width + " by " + height + "; a tier's footprint fits inside its plot or it is never raised" + Layered(Entry)));
			}
		}

	}
}
