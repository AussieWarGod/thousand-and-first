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
			string key = xml.GetAttribute("Key");
			if (!KingdomRules.TryParseBuildAttributes(key, xml.GetAttribute("DisplayName"), xml.GetAttribute("Blueprint"), xml.GetAttribute("Cost"), xml.GetAttribute("Ticks"), xml.GetAttribute("Styles"), xml.GetAttribute("Category"), xml.GetAttribute("MinStage"), xml.GetAttribute("Staff"), xml.GetAttribute("Manning"), xml.GetAttribute("Defence"), out var entry, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				// Nothing is registered and nothing already registered is cleared: a malformed
				// entry does not replace the design of the same key that is already loaded, so it
				// must not replace that design's gate or chain either.
				SkipChildren(xml);
				return;
			}
			// Every optional gate and chain attribute is read whether or not it is present: the
			// engine warns about attributes a parse pass never asked for, and an absent one is the
			// ungated, unchanging default in both registries.
			entry.Carries = xml.GetAttribute("Carries");
			entry.Materials = xml.GetAttribute("Materials");
			KingdomZoning.RegisterGate(entry.Key, xml.GetAttribute("Districts"), xml.GetAttribute("MinZones"), xml.GetAttribute("Knowledge"), xml.GetAttribute("MinTech"));
			KingdomUpgrade.RegisterChain(entry.Key, xml.GetAttribute("UpgradesTo"), xml.GetAttribute("UpgradeCost"), xml.GetAttribute("UpgradeTicks"), xml.GetAttribute("UpgradeCrew"), xml.GetAttribute("UpgradeMinStage"));
			KingdomMaterials.RegisterCost(entry.Key, xml.GetAttribute("Materials"), xml.GetAttribute("UpgradeMaterials"));
			KingdomPlots.RegisterSpec(entry.Key, xml.GetAttribute("Plot"), xml.GetAttribute("Open"), xml.GetAttribute("Sky"), xml.GetAttribute("Contents"));
			KingdomRules.BuildEntry parsed = entry;
			for (int i = 0; i < _buildings.Count; i++)
			{
				if (_buildings[i].Key == entry.Key)
				{
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
						HandleSkin(skinXml, parsed);
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
					CostDrams = entry.CostDrams,
					Materials = entry.Materials,
					Carries = entry.Carries,
					Staff = entry.Staff,
					Manning = entry.Manning,
					Defence = entry.Defence,
					SuccessorKey = (chain == null) ? null : chain.SuccessorKey
				});
			}
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(view, _styles);
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

		private static void HandleSkin(XmlDataHelper xml, KingdomRules.BuildEntry Entry)
		{
			if (!KingdomDesignRules.TryParseSkinAttributes(xml.GetAttribute("Key"), xml.GetAttribute("Style"), xml.GetAttribute("ColorString"), xml.GetAttribute("DetailColor"), xml.GetAttribute("RenderString"), xml.GetAttribute("Tile"), out var skin, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Entry.Key + ": " + error);
			}
			else if (!KingdomRules.TryAddSkin(Entry, skin, out var clash))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + clash);
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
