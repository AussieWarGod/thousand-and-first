using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	[Serializable]
	public class KingdomCharterPart : IPart
	{
		public const string COMMAND = "r_KingdomCharterMenu";

		public Guid ActivatedAbilityID = Guid.Empty;

		public override void Attach()
		{
			base.Attach();
			for (int num = ParentObject.PartsList.Count - 1; num >= 0; num--)
			{
				IPart part = ParentObject.PartsList[num];
				if (part != this && part.GetType().Name == "KingdomCharterPart")
				{
					ParentObject.PartsList.RemoveAt(num);
				}
			}
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register(COMMAND);
			base.Register(Object, Registrar);
		}

		public void EnsureAbility()
		{
			if (ActivatedAbilityID != Guid.Empty)
			{
				return;
			}
			if (ParentObject.ActivatedAbilities != null && ParentObject.ActivatedAbilities.AbilityByGuid != null)
			{
				foreach (System.Collections.Generic.KeyValuePair<Guid, ActivatedAbilityEntry> item in ParentObject.ActivatedAbilities.AbilityByGuid)
				{
					if (item.Value.Command == COMMAND)
					{
						ActivatedAbilityID = item.Key;
						return;
					}
				}
			}
			ActivatedAbilityID = AddMyActivatedAbility("Charter", COMMAND, "Kingdom");
		}

		public void RemoveAbility()
		{
			RemoveMyActivatedAbility(ref ActivatedAbilityID);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == COMMAND)
			{
				OpenMenu();
			}
			return base.FireEvent(E);
		}

		public void OpenMenu()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			while (true)
			{
				int num = Popup.PickOption(Title: system.KingdomDisplayName, Options: new string[9] { "Status", "The Chronicle", "As others tell it", "Standings", "Designate district", "Commission a building", "Pay tribute", "Dedicate a vessel to the stores", "Strike a trade charter" }, Hotkeys: "scandmtvr".ToCharArray(), AllowEscape: true);
				switch (num)
				{
				case 0:
					Popup.Show(KingdomReports.Status(system));
					break;
				case 1:
					Popup.Show(KingdomReports.Chronicle(system));
					break;
				case 2:
					Popup.Show(KingdomReports.Chronicle(system, Outsider: true));
					break;
				case 3:
					Popup.Show(KingdomReports.Standings(system));
					break;
				case 4:
					DesignateDistrict(system);
					break;
				case 5:
					CommissionBuilding(system);
					break;
				case 6:
					PayTribute(system);
					break;
				case 7:
					DedicateVessel(system);
					break;
				case 8:
					StrikeTradeCharter(system);
					break;
				default:
					return;
				}
			}
		}

		public void DesignateDistrict(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Districts are declared on the kingdom's own ground.");
				return;
			}
			int num = Popup.PickOption(Title: "Declare this ground", Options: KingdomRules.Districts, AllowEscape: true);
			if (num >= 0)
			{
				string district = KingdomRules.Districts[num];
				System.ZoneDistricts[zone.ZoneID] = district;
				KingdomChronicle.Record(System, "the ground here was declared a " + district + " district of " + System.KingdomDisplayName);
				Popup.Show("This ground is declared a {{C|" + district + "}} district.");
			}
		}

		public void CommissionBuilding(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			int stored = (zone != null) ? KingdomGrowth.CountStoredWater(zone) : 0;
			System.Collections.Generic.List<KingdomRules.BuildEntry> available = new System.Collections.Generic.List<KingdomRules.BuildEntry>();
			foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
			{
				if (KingdomRules.StyleAllows(entry.Styles, System.Style) && System.Stage >= entry.MinStage)
				{
					available.Add(entry);
				}
			}
			if (available.Count == 0)
			{
				Popup.Show("No designs are known here.");
				return;
			}
			string[] options = new string[available.Count];
			for (int i = 0; i < available.Count; i++)
			{
				options[i] = available[i].DisplayName + " {{C|[" + available[i].CostDrams + " drams]}}";
			}
			int num = Popup.PickOption(Title: "Commission ({{C|" + stored + " drams}} in the stores)", Options: options, AllowEscape: true);
			if (num >= 0)
			{
				if (!KingdomCommission.Commission(System, available[num].Key, out var failure))
				{
					Popup.Show(failure);
				}
			}
		}

		public void PayTribute(KingdomSystem System)
		{
			if (!KingdomRaids.TryTribute(System, ParentObject.CurrentZone, out var failure))
			{
				Popup.Show(failure);
			}
		}

		public void StrikeTradeCharter(KingdomSystem System)
		{
			System.Collections.Generic.List<KingdomRules.DealEntry> deals = KingdomData.Deals;
			if (deals.Count == 0)
			{
				Popup.Show("No charters are known.");
				return;
			}
			string[] dealOptions = new string[deals.Count];
			for (int i = 0; i < deals.Count; i++)
			{
				dealOptions[i] = deals[i].DisplayName + " {{C|[standing " + deals[i].MinStanding + "+]}}";
			}
			int dealPick = Popup.PickOption(Title: "Which charter?", Options: dealOptions, AllowEscape: true);
			if (dealPick < 0)
			{
				return;
			}
			System.Collections.Generic.List<string> eligible = new System.Collections.Generic.List<string>();
			System.Collections.Generic.List<string> labels = new System.Collections.Generic.List<string>();
			foreach (Faction faction in Factions.Loop())
			{
				if (faction.Visible && faction.Name != System.KingdomFactionName && faction.Name != "Player" && System.GetStanding(faction.Name) >= deals[dealPick].MinStanding)
				{
					eligible.Add(faction.Name);
					labels.Add(faction.DisplayName + " (standing " + System.GetStanding(faction.Name) + ")");
					if (eligible.Count >= 20)
					{
						break;
					}
				}
			}
			if (eligible.Count == 0)
			{
				Popup.Show("No faction holds the kingdom in high enough regard for that charter.");
				return;
			}
			int factionPick = Popup.PickOption(Title: "With whom?", Options: labels.ToArray(), AllowEscape: true);
			if (factionPick >= 0)
			{
				if (!KingdomTrade.StrikeDeal(System, deals[dealPick].Key, eligible[factionPick], out var failure))
				{
					Popup.Show(failure);
				}
			}
		}

		public void DedicateVessel(KingdomSystem System)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			System.Collections.Generic.List<GameObject> vessels = new System.Collections.Generic.List<GameObject>();
			foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
			{
				foreach (GameObject item in adjacentCell.GetObjectsWithPart("LiquidVolume"))
				{
					if (item.GetPart<XRL.World.Parts.LiquidVolume>().MaxVolume > 0)
					{
						vessels.Add(item);
					}
				}
			}
			if (vessels.Count == 0)
			{
				Popup.Show("Stand beside a vessel to dedicate it. What is dedicated feeds the settlement; what is not is yours alone, and no settler will touch it.");
				return;
			}
			string[] options = new string[vessels.Count];
			for (int i = 0; i < vessels.Count; i++)
			{
				options[i] = vessels[i].ShortDisplayName + ((vessels[i].GetIntProperty("KingdomStores") == 1) ? " {{G|[dedicated]}}" : " {{K|[personal]}}");
			}
			int num = Popup.PickOption(Title: "Dedicate or release", Options: options, AllowEscape: true);
			if (num >= 0)
			{
				GameObject vessel = vessels[num];
				if (vessel.GetIntProperty("KingdomStores") == 1)
				{
					vessel.SetIntProperty("KingdomStores", 0);
					Popup.Show("The " + vessel.ShortDisplayName + " is yours alone again.");
				}
				else
				{
					vessel.SetIntProperty("KingdomStores", 1);
					Popup.Show("The " + vessel.ShortDisplayName + " is dedicated to the stores of " + System.KingdomDisplayName + ".");
				}
			}
		}
	}
}
