using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomData
	{
		private static void EnsureLoaded()
		{
			if (_buildings != null)
			{
				return;
			}
			_buildings = new List<KingdomRules.BuildEntry>();
			_styles = new List<string>();
			_styleDrafts = new List<KingdomStyleDraft>();
			_styleDefinitions = new List<KingdomStyleDefinition>();
			_creedDrafts = new List<KingdomCreedDraft>();
			_creedDefinitions = new List<KingdomCreedDefinition>();
			LoadCreeds();
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
			KingdomReach.ClearReach();
			KingdomCrews.ClearCrewNeeds();
			KingdomPurpose.ClearDefinitions();
			Dictionary<string, Action<XmlDataHelper>> handlers = null;
			handlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdombuildings",
					delegate(XmlDataHelper xml)
					{
						KingdomXmlSchema.HandleRoot(xml, handlers, "KingdomBuildings");
					}
				},
				{ "building", HandleBuilding },
				{ "style", HandleStyle }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot("KingdomBuildings"))
			{
				item.HandleNodes(handlers);
			}
			// Architecture freezes this complete merged building/spec view, then performs its own
			// keyed multi-stream transaction. It must run after every building declaration has
			// registered its plot spec and before any caller can commission from the catalogue.
			KingdomArchitecture.Reload(_buildings);
			KingdomSocketTransitions.Reload();
			ReportCatalogueFindings();
			_deals = new List<KingdomRules.DealEntry>();
			Dictionary<string, Action<XmlDataHelper>> dealHandlers = null;
			dealHandlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdomdeals",
					delegate(XmlDataHelper xml)
					{
						KingdomXmlSchema.HandleRoot(xml, dealHandlers, "KingdomDeals");
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
						KingdomXmlSchema.HandleRoot(xml, yardWorkHandlers, "KingdomYardWorks");
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

	}
}
