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
				int num = Popup.PickOption(Title: system.KingdomDisplayName, Options: new string[7] { "Status", "The Chronicle", "As others tell it", "Standings", "Designate district", "Commission a building", "Pay tribute" }, Hotkeys: "scandmt".ToCharArray(), AllowEscape: true);
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
			string[] options = new string[KingdomRules.BuildCatalog.Length];
			for (int i = 0; i < KingdomRules.BuildCatalog.Length; i++)
			{
				options[i] = KingdomRules.BuildCatalog[i].DisplayName + " {{C|[" + KingdomRules.BuildCatalog[i].CostDrams + " drams]}}";
			}
			int num = Popup.PickOption(Title: "Commission ({{C|" + stored + " drams}} in the stores)", Options: options, AllowEscape: true);
			if (num >= 0)
			{
				if (!KingdomCommission.Commission(System, KingdomRules.BuildCatalog[num].Key, out var failure))
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
	}
}
