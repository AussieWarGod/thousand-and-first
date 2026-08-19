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
			if (!KingdomRules.TryParseDealAttributes(xml.GetAttribute("Key"), xml.GetAttribute("DisplayName"), xml.GetAttribute("MinStanding"), xml.GetAttribute("Income"), xml.GetAttribute("Interval"), xml.GetAttribute("Caravan"), out var entry, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomDeals: " + error);
			}
			else
			{
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
			if (!KingdomRules.TryParseBuildAttributes(xml.GetAttribute("Key"), xml.GetAttribute("DisplayName"), xml.GetAttribute("Blueprint"), xml.GetAttribute("Cost"), xml.GetAttribute("Ticks"), xml.GetAttribute("Styles"), xml.GetAttribute("Category"), xml.GetAttribute("MinStage"), xml.GetAttribute("Staff"), xml.GetAttribute("Manning"), xml.GetAttribute("Defence"), out var entry, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
			}
			else
			{
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
