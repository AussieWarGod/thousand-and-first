using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomData
	{
		/// <summary>
		/// Says out loud what is wrong with the merged catalogue, once per load. Nothing is
		/// unregistered: a design that is wrong about itself stays buildable and becomes visible,
		/// which is the only shape a check on third-party content can honestly take. The checks that
		/// matter here are the ones no single entry can see &mdash; an improvement into a key nothing
		/// declares, a chain that rings, an improvement onto a larger plot, a family a camp cannot
		/// reach.
		/// </summary>
		private static void ReportCatalogueFindings()
		{
			List<CatalogueEntry> view = new List<CatalogueEntry>(_buildings.Count);
			for (int i = 0; i < _buildings.Count; i++)
			{
				KingdomRules.BuildEntry entry = _buildings[i];
				KingdomUpgradeRules.UpgradeChain chain;
				KingdomUpgrade.TryGetChain(entry.Key, out chain);
				KingdomPlotRules.PlotSpec spec;
				KingdomPlots.TryGetSpec(entry.Key, out spec);
				KingdomPlotRules.PlotSpec successorSpec = null;
				if (chain != null && !string.IsNullOrEmpty(chain.SuccessorKey))
					KingdomPlots.TryGetSpec(chain.SuccessorKey, out successorSpec);
				int declarations = KingdomMergeRules.DeclarationsOf(entry.Key);
				BuildingDraft design;
				KingdomMergeRules.TryGetDraft(entry.Key, out design);
				view.Add(new CatalogueEntry
				{
					Key = entry.Key,
					DisplayName = entry.DisplayName,
					Category = entry.Category,
					Styles = entry.Styles,
					MinStage = entry.MinStage,
					Plot = (spec == null) ? KingdomPlotRules.PlotSize.None : spec.Size,
					Open = (spec != null && spec.Open),
					Contents = (spec == null) ? null : spec.Contents,
					FootprintWidth = (spec == null) ? 0 : spec.FootprintWidth,
					FootprintHeight = (spec == null) ? 0 : spec.FootprintHeight,
					Roof = (spec == null) ? KingdomPlotRules.RoofState.Walled : spec.Roof,
					RoofDeclared = (spec != null && spec.RoofDeclared),
					RequiresSky = (spec != null && spec.RequiresSky),
					CostDrams = entry.CostDrams,
					Materials = entry.Materials,
					Carries = entry.Carries,
					Staff = entry.Staff,
					Manning = entry.Manning,
					Defence = entry.Defence,
					SuccessorKey = (chain == null) ? null : chain.SuccessorKey,
					SuccessorEnvelopeGrowth = chain != null && spec != null
						&& successorSpec != null
						&& KingdomArchitecture.HasAuthorizedEnvelopeSuccessor(entry.Key,
							chain.SuccessorKey, entry.Category, spec.Size, successorSpec.Size),
					// How many files this design is the merge of, so a fault in a layered design says
					// so and the author knows to look past their own file.
					Declarations = (declarations < 1) ? 1 : declarations,
					Origin = (design == null) ? null : design.Origin
				});
			}
			// The merges speak first, then the whole-file checks, so one load makes one log and it
			// reads in the order the files were read.
			List<CatalogueFinding> findings = new List<CatalogueFinding>(KingdomMergeRules.Findings);
			findings.AddRange(KingdomCatalogueRules.Validate(view, _styles));
			for (int i = 0; i < findings.Count; i++)
			{
				if (findings[i].Severity == CatalogueSeverity.Fault)
				{
					MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + findings[i].Message);
				}
				else
				{
					KingdomLog.Log("KingdomBuildings: " + findings[i].Message);
				}
			}
		}

		// A malformed <building> is already reported; its children are walked past without a second
		// round of warnings about nodes that were never going to be read.
		private static void SkipChildren(XmlDataHelper xml)
		{
			xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>(), delegate(XmlDataHelper child)
			{
				child.DoneWithElement();
			});
		}

		private static void HandleSkin(XmlDataHelper xml, KingdomRules.BuildEntry Entry, BuildingDraft Design)
		{
			if (!KingdomDesignRules.TryParseSkinAttributes(xml.GetAttribute("Key"), xml.GetAttribute("Style"), xml.GetAttribute("ColorString"), xml.GetAttribute("DetailColor"), xml.GetAttribute("RenderString"), xml.GetAttribute("Tile"), out var skin, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Entry.Key + ": " + error);
			}
			else if (!KingdomMergeRules.TryMergeSkin(Design, skin, out _, out var clash))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + clash);
			}
			else
			{
				// The draft owns the merged list -- an earlier file's skins plus this element's, with
				// a repeated key replaced where it already sat -- and the entry shows exactly it.
				Entry.Skins = Design.Skins;
			}
			xml.DoneWithElement();
		}

	}
}
