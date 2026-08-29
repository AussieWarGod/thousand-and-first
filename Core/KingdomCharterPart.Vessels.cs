using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		public void DedicateVessel(KingdomSystem System)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Vessels are dedicated on the kingdom's own ground, not in other people's houses.");
				return;
			}
			System.Collections.Generic.List<GameObject> vessels = new System.Collections.Generic.List<GameObject>();
			System.Collections.Generic.List<GameObject> larders = new System.Collections.Generic.List<GameObject>();
			foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
			{
				foreach (GameObject item in adjacentCell.GetObjectsWithPart("LiquidVolume"))
				{
					if (item.GetPart<XRL.World.Parts.LiquidVolume>().MaxVolume > 0)
					{
						vessels.Add(item);
					}
				}
				// A larder is anything that holds things rather than liquid: a chest, a
				// footlocker, a shelf. Water and food are accounted separately, by different
				// people, so they carry different marks and different caps.
				foreach (GameObject item in adjacentCell.GetObjects())
				{
					if (item.Inventory != null && item.GetPart<XRL.World.Parts.LiquidVolume>() == null && !larders.Contains(item))
					{
						larders.Add(item);
					}
				}
			}
			if (vessels.Count == 0 && larders.Count == 0)
			{
				Popup.Show("Stand beside a vessel or a store to dedicate it. What is dedicated feeds the settlement; what is not is yours alone, and no settler will touch it.");
				return;
			}
			string[] options = new string[vessels.Count + larders.Count + 1];
			options[0] = "{{W|Dedicate everything here}}";
			for (int i = 0; i < vessels.Count; i++)
			{
				options[i + 1] = vessels[i].ShortDisplayName + ((vessels[i].GetIntProperty("KingdomStores") == 1) ? " {{G|[dedicated]}}" : " {{K|[personal]}}");
			}
			for (int i = 0; i < larders.Count; i++)
			{
				bool isLarder = larders[i].GetIntProperty("KingdomLarder") == 1;
				bool isStockpile = KingdomMaterials.IsStockpile(larders[i]);
				options[vessels.Count + i + 1] = larders[i].ShortDisplayName + " {{K|(store)}}"
					+ (isLarder ? " {{G|[larder]}}" : "")
					+ (isStockpile ? " {{G|[stockpile]}}" : "")
					+ ((!isLarder && !isStockpile) ? " {{K|[personal]}}" : "");
			}
			int num = Popup.PickOption(Title: "Dedicate or release", Options: options, AllowEscape: true);
			if (num == 0)
			{
				int dedicated = 0;
				int room = KingdomRules.MaxDedicatedVessels - KingdomGrowth.CountDedicatedVessels(zone);
				foreach (GameObject candidate in vessels)
				{
					if (room <= 0)
					{
						break;
					}
					if (candidate.GetIntProperty("KingdomStores") != 1)
					{
						candidate.SetIntProperty("KingdomStores", 1);
						dedicated++;
						room--;
						if (!KingdomGovernanceScope.HasCommitted)
						{
							KingdomGovernanceScope.Commit("dedicate stores");
						}
					}
				}
				int larderRoom = KingdomRules.MaxDedicatedLarders - KingdomGrowth.CountDedicatedLarders(zone);
				foreach (GameObject candidate in larders)
				{
					if (larderRoom <= 0)
					{
						break;
					}
					if (candidate.GetIntProperty("KingdomLarder") != 1)
					{
						candidate.SetIntProperty("KingdomLarder", 1);
						dedicated++;
						larderRoom--;
						if (!KingdomGovernanceScope.HasCommitted)
						{
							KingdomGovernanceScope.Commit("dedicate stores");
						}
					}
				}
				Popup.Show((dedicated > 0) ? (dedicated + " are dedicated to the stores of " + KingdomPresentation.Rich(System.SeatName) + ".") : "Everything here is already dedicated, or the keepers can account for no more.");
				return;
			}
			if (num > 0 && num <= vessels.Count)
			{
				GameObject vessel = vessels[num - 1];
				if (vessel.GetIntProperty("KingdomStores") != 1 && KingdomGrowth.CountDedicatedVessels(zone) >= KingdomRules.MaxDedicatedVessels)
				{
					Popup.Show("The stores are already as many vessels as the water-keepers can account for.");
					return;
				}
				if (vessel.GetIntProperty("KingdomStores") == 1)
				{
					if (!KingdomDesignationReleaseAuthority.TryCanRelease(
						System, zone, vessel, out string releaseFailure))
					{
						Popup.Show(releaseFailure);
						return;
					}
					vessel.SetIntProperty("KingdomStores", 0);
					KingdomGovernanceScope.Commit("change water stores");
					Popup.Show("The " + vessel.ShortDisplayName + " is yours alone again.");
				}
				else
				{
					vessel.SetIntProperty("KingdomStores", 1);
					KingdomGovernanceScope.Commit("change water stores");
					Popup.Show("The " + vessel.ShortDisplayName + " is dedicated to the stores of " + KingdomPresentation.Rich(System.SeatName) + ".");
				}
				return;
			}
			if (num > vessels.Count)
			{
				GameObject store = larders[num - vessels.Count - 1];
				bool isLarder = store.GetIntProperty("KingdomLarder") == 1;
				bool isStockpile = KingdomMaterials.IsStockpile(store);
				// Food and material are separate accounts kept by separate people, so one chest may be
				// a larder, a stockpile, or both. Dedication is a mark either way: what is inside stays
				// where it is and stays the founder's. The settlement only counts it.
				int pick = Popup.PickOption(Title: store.ShortDisplayName,
					Intro: "What should the settlement count what is in here as?",
					Options: new string[2] {
						(isLarder ? "{{G|Stop counting it as a larder}}" : "Dedicate it as a {{W|larder}} — food for the shared meal"),
						(isStockpile ? "{{G|Stop counting it as a stockpile}}" : "Dedicate it as a {{W|stockpile}} — timber, stone, and whatever else is cleared")
					}, AllowEscape: true);
				if (pick < 0)
				{
					return;
				}
				if (pick == 0)
				{
					if (!isLarder && KingdomGrowth.CountDedicatedLarders(zone) >= KingdomRules.MaxDedicatedLarders)
					{
						Popup.Show("The settlement keeps as many larders as anyone can keep an honest account of.");
						return;
					}
					if (isLarder)
					{
						if (!KingdomDesignationReleaseAuthority.TryCanRelease(
							System, zone, store, out string releaseFailure))
						{
							Popup.Show(releaseFailure);
							return;
						}
						store.SetIntProperty("KingdomLarder", 0);
						KingdomGovernanceScope.Commit("change larder");
						Popup.Show("The " + store.ShortDisplayName + " is no longer a larder. Nothing in it will be counted as food.");
					}
					else
					{
						store.SetIntProperty("KingdomLarder", 1);
						KingdomGovernanceScope.Commit("change larder");
						Popup.Show("The " + store.ShortDisplayName + " is a larder of " + KingdomPresentation.Rich(System.SeatName) + " now. What is in it is counted, and still yours.");
					}
					return;
				}
				if (!KingdomMaterials.DedicateStockpile(System, zone, store, out var stockpileFailure))
				{
					Popup.Show(stockpileFailure);
				}
				else
				{
					KingdomGovernanceScope.Commit("change stockpile");
				}
			}
		}

	}
}
