using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static class KingdomData
	{
		private static List<KingdomRules.BuildEntry> _buildings;

		private static List<string> _styles;

		private static List<KingdomRules.DealEntry> _deals;

		public static List<KingdomRules.DealEntry> Deals
		{
			get
			{
				EnsureLoaded();
				return _deals;
			}
		}

		public static bool TryGetDeal(string Key, out KingdomRules.DealEntry Entry)
		{
			EnsureLoaded();
			for (int i = 0; i < _deals.Count; i++)
			{
				if (_deals[i].Key == Key)
				{
					Entry = _deals[i];
					return true;
				}
			}
			Entry = null;
			return false;
		}

		public static List<KingdomRules.BuildEntry> Buildings
		{
			get
			{
				EnsureLoaded();
				return _buildings;
			}
		}

		public static List<string> Styles
		{
			get
			{
				EnsureLoaded();
				return _styles;
			}
		}

		public static void Reload()
		{
			_buildings = null;
			_styles = null;
			_deals = null;
			EnsureLoaded();
		}

		/// <summary>
		/// Reads the registries if they have not been read yet, and does nothing if they have.
		/// The trigger for anything that lives beside the catalog rather than in it &mdash; zoning
		/// gates, upgrade chains &mdash; which are filled during this same pass and would otherwise
		/// answer from an empty table for whoever asked first.
		/// </summary>
		public static void EnsureBuildings()
		{
			EnsureLoaded();
		}

		public static bool TryGetBuilding(string Key, out KingdomRules.BuildEntry Entry)
		{
			EnsureLoaded();
			for (int i = 0; i < _buildings.Count; i++)
			{
				if (_buildings[i].Key == Key)
				{
					Entry = _buildings[i];
					return true;
				}
			}
			Entry = null;
			return false;
		}

		private static void EnsureLoaded()
		{
			if (_buildings != null)
			{
				return;
			}
			_buildings = new List<KingdomRules.BuildEntry>();
			_styles = new List<string> { "common" };
			// Everything keyed by a building Key but held outside the entry is emptied here and
			// refilled by HandleBuilding, in this one pass. A second pass over the same streams
			// would read the same file twice and make the engine's own unused-attribute check warn
			// about every attribute that pass did not happen to want.
			KingdomZoning.ClearGates();
			KingdomUpgrade.ClearChains();
			KingdomMaterials.ClearCosts();
			KingdomPlots.ClearSpecs();
			KingdomYards.ClearSpecs();
			KingdomMergeRules.ClearDrafts();
			KingdomQol.ClearProvides();
			KingdomLodging.ClearCloseness();
			Dictionary<string, Action<XmlDataHelper>> handlers = null;
			handlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdombuildings",
					delegate(XmlDataHelper xml)
					{
						xml.HandleNodes(handlers);
					}
				},
				{ "building", HandleBuilding },
				{ "style", HandleStyle }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot("KingdomBuildings"))
			{
				item.HandleNodes(handlers);
			}
			ReportCatalogueFindings();
			_deals = new List<KingdomRules.DealEntry>();
			Dictionary<string, Action<XmlDataHelper>> dealHandlers = null;
			dealHandlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdomdeals",
					delegate(XmlDataHelper xml)
					{
						xml.HandleNodes(dealHandlers);
					}
				},
				{ "deal", HandleDeal }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot("KingdomDeals"))
			{
				item.HandleNodes(dealHandlers);
			}
			Dictionary<string, Action<XmlDataHelper>> yardWorkHandlers = null;
			yardWorkHandlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdomyardworks",
					delegate(XmlDataHelper xml)
					{
						xml.HandleNodes(yardWorkHandlers);
					}
				},
				{ "yardwork", HandleYardWork }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot("KingdomYardWorks"))
			{
				item.HandleNodes(yardWorkHandlers);
			}
		}

		private static void HandleYardWork(XmlDataHelper xml)
		{
			KingdomYards.RegisterSpec(xml.GetAttribute("Key"), xml.GetAttribute("DisplayName"), xml.GetAttribute("Blueprint"), xml.GetAttribute("Trade"), xml.GetAttribute("Shades"), xml.GetAttribute("Goods"));
			xml.DoneWithElement();
		}

		private static void HandleDeal(XmlDataHelper xml)
		{
			// Read whether or not the parse below succeeds, for the same reason the building
			// handler reads its optional attributes unconditionally: the engine warns about an
			// attribute a pass never asked for, and an absent one is the water-only default.
			string materials = xml.GetAttribute("Materials");
			if (!KingdomRules.TryParseDealAttributes(xml.GetAttribute("Key"), xml.GetAttribute("DisplayName"), xml.GetAttribute("MinStanding"), xml.GetAttribute("Income"), xml.GetAttribute("Interval"), xml.GetAttribute("Caravan"), out var entry, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomDeals: " + error);
			}
			else
			{
				KingdomMaterials.RegisterDealMaterials(entry.Key, materials);
				for (int i = 0; i < _deals.Count; i++)
				{
					if (_deals[i].Key == entry.Key)
					{
						_deals[i] = entry;
						entry = null;
						break;
					}
				}
				if (entry != null)
				{
					_deals.Add(entry);
				}
			}
			xml.DoneWithElement();
		}

		private static void HandleBuilding(XmlDataHelper xml)
		{
			// One read of every attribute, into one draft, before anything is parsed. Merge-by-key
			// happens on the raw strings (KingdomMergeRules): a later <building Key="X"> overrides the
			// attributes it names, leaves the ones it omits standing, and appends its skins, so every
			// parser below goes on reading exactly the string it always read and none of them has to
			// learn that catalogues layer. Reading each attribute unconditionally is required either
			// way: the engine records which attributes a pass asked for and warns about the rest.
			BuildingDraft declared = new BuildingDraft(xml.GetAttribute("Key"));
			declared.Set(KingdomMergeRules.AttrDisplayName, xml.GetAttribute("DisplayName"));
			declared.Set(KingdomMergeRules.AttrBlueprint, xml.GetAttribute("Blueprint"));
			declared.Set(KingdomMergeRules.AttrCost, xml.GetAttribute("Cost"));
			declared.Set(KingdomMergeRules.AttrTicks, xml.GetAttribute("Ticks"));
			declared.Set(KingdomMergeRules.AttrStyles, xml.GetAttribute("Styles"));
			declared.Set(KingdomMergeRules.AttrCategory, xml.GetAttribute("Category"));
			declared.Set(KingdomMergeRules.AttrMinStage, xml.GetAttribute("MinStage"));
			declared.Set(KingdomMergeRules.AttrStaff, xml.GetAttribute("Staff"));
			declared.Set(KingdomMergeRules.AttrManning, xml.GetAttribute("Manning"));
			declared.Set(KingdomMergeRules.AttrDefence, xml.GetAttribute("Defence"));
			declared.Set(KingdomMergeRules.AttrCarries, xml.GetAttribute("Carries"));
			declared.Set(KingdomMergeRules.AttrMaterials, xml.GetAttribute("Materials"));
			declared.Set(KingdomMergeRules.AttrDistricts, xml.GetAttribute("Districts"));
			declared.Set(KingdomMergeRules.AttrMinZones, xml.GetAttribute("MinZones"));
			declared.Set(KingdomMergeRules.AttrKnowledge, xml.GetAttribute("Knowledge"));
			declared.Set(KingdomMergeRules.AttrMinTech, xml.GetAttribute("MinTech"));
			declared.Set(KingdomMergeRules.AttrUpgradesTo, xml.GetAttribute("UpgradesTo"));
			declared.Set(KingdomMergeRules.AttrUpgradeCost, xml.GetAttribute("UpgradeCost"));
			declared.Set(KingdomMergeRules.AttrUpgradeTicks, xml.GetAttribute("UpgradeTicks"));
			declared.Set(KingdomMergeRules.AttrUpgradeCrew, xml.GetAttribute("UpgradeCrew"));
			declared.Set(KingdomMergeRules.AttrUpgradeMinStage, xml.GetAttribute("UpgradeMinStage"));
			declared.Set(KingdomMergeRules.AttrUpgradeMaterials, xml.GetAttribute("UpgradeMaterials"));
			declared.Set(KingdomMergeRules.AttrPlot, xml.GetAttribute("Plot"));
			declared.Set(KingdomMergeRules.AttrOpen, xml.GetAttribute("Open"));
			declared.Set(KingdomMergeRules.AttrSky, xml.GetAttribute("Sky"));
			declared.Set(KingdomMergeRules.AttrContents, xml.GetAttribute("Contents"));
			declared.Set(KingdomMergeRules.AttrFootprint, xml.GetAttribute("Footprint"));
			declared.Set(KingdomMergeRules.AttrRoof, xml.GetAttribute("Roof"));
			declared.Set(KingdomMergeRules.AttrProvides, xml.GetAttribute("Provides"));
			declared.Set(KingdomMergeRules.AttrCloseness, xml.GetAttribute("Closeness"));
			BuildingDraft design = KingdomMergeRules.Absorb(declared);
			if (!KingdomRules.TryParseBuildAttributes(design.Key, design.Get(KingdomMergeRules.AttrDisplayName), design.Get(KingdomMergeRules.AttrBlueprint), design.Get(KingdomMergeRules.AttrCost), design.Get(KingdomMergeRules.AttrTicks), design.Get(KingdomMergeRules.AttrStyles), design.Get(KingdomMergeRules.AttrCategory), design.Get(KingdomMergeRules.AttrMinStage), design.Get(KingdomMergeRules.AttrStaff), design.Get(KingdomMergeRules.AttrManning), design.Get(KingdomMergeRules.AttrDefence), out var entry, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				// Nothing is registered and nothing already registered is cleared: a malformed entry
				// does not replace the design of the same key that is already loaded, so it must not
				// replace that design's gate or chain either. The draft is kept even so, so a later
				// file that completes a half-declaration still can.
				SkipChildren(xml);
				return;
			}
			entry.Carries = design.Get(KingdomMergeRules.AttrCarries);
			entry.Materials = design.Get(KingdomMergeRules.AttrMaterials);
			entry.Skins = design.Skins;
			KingdomZoning.RegisterGate(entry.Key, design.Get(KingdomMergeRules.AttrDistricts), design.Get(KingdomMergeRules.AttrMinZones), design.Get(KingdomMergeRules.AttrKnowledge), design.Get(KingdomMergeRules.AttrMinTech));
			KingdomUpgrade.RegisterChain(entry.Key, design.Get(KingdomMergeRules.AttrUpgradesTo), design.Get(KingdomMergeRules.AttrUpgradeCost), design.Get(KingdomMergeRules.AttrUpgradeTicks), design.Get(KingdomMergeRules.AttrUpgradeCrew), design.Get(KingdomMergeRules.AttrUpgradeMinStage));
			KingdomMaterials.RegisterCost(entry.Key, design.Get(KingdomMergeRules.AttrMaterials), design.Get(KingdomMergeRules.AttrUpgradeMaterials));
			// The footprint and roof are registered post-merge like everything else: a file that
			// shrinks the plot and a file that declares the footprint are two files, and only the
			// merged pair is the design the validator can check.
			KingdomPlots.RegisterSpec(entry.Key, design.Get(KingdomMergeRules.AttrPlot), design.Get(KingdomMergeRules.AttrOpen), design.Get(KingdomMergeRules.AttrSky), design.Get(KingdomMergeRules.AttrContents), design.Get(KingdomMergeRules.AttrFootprint), design.Get(KingdomMergeRules.AttrRoof));
			// After the plot spec, and for the same reason it is registered post-merge: what a design
			// offers a resident is the tags its author declared plus the ones its roof gives, and the
			// roof is only settled once the merged spec is in.
			KingdomQol.RegisterProvides(entry.Key, design.Get(KingdomMergeRules.AttrProvides));
			// Last of the post-merge registrations, and after the plot spec on purpose: a design that
			// declares no Closeness is measured, and what it is measured against is the beds in its
			// merged Carries over the footprint the merged plot spec just registered. Registering it
			// before either would measure a design nobody had finished declaring.
			KingdomLodging.RegisterCloseness(entry.Key, design.Get(KingdomMergeRules.AttrCloseness));
			KingdomRules.BuildEntry parsed = entry;
			for (int i = 0; i < _buildings.Count; i++)
			{
				if (_buildings[i].Key == entry.Key)
				{
					// In place, so the catalogue keeps first-declaration order: a mod that re-costs
					// the tent does not move the tent to the bottom of the founder's list.
					_buildings[i] = entry;
					entry = null;
					break;
				}
			}
			if (entry != null)
			{
				_buildings.Add(entry);
			}
			// HandleNodes stands in for DoneWithElement: it returns at once on a self-closing
			// <building/>, which is every entry that declares no skins, and otherwise dispatches
			// the children.
			xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"skin",
					delegate(XmlDataHelper skinXml)
					{
						HandleSkin(skinXml, parsed, design);
					}
				}
			});
		}

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

		private static void HandleStyle(XmlDataHelper xml)
		{
			string attribute = xml.GetAttribute("Name");
			if (!string.IsNullOrEmpty(attribute) && !_styles.Contains(attribute))
			{
				_styles.Add(attribute);
			}
			xml.DoneWithElement();
		}
	}
}
