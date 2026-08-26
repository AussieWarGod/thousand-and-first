using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		/// <summary>
		/// The homecoming report, asked for rather than pushed. The settlement says it has news
		/// on the way in (nonmodal); this is where the founder reads it, if they want it.
		/// </summary>
		public void ShowHomecoming(KingdomSystem System)
		{
			if (!System.Ledger.Any)
			{
				Popup.Show("Nothing has happened here since you last stood on this ground.");
				return;
			}
			Popup.Show(System.Ledger.Digest(System.SeatName, System.HomecomingDays));
			// A report remains durable until it has actually been shown. Reading it is
			// bookkeeping, so this reset never marks the governance scope or costs a turn.
			System.Ledger.Reset();
			System.HomecomingDays = 0;
		}

		/// <summary>Hears the settler who is waiting, and lets the founder decline.</summary>
		public void HearPetition(KingdomSystem System)
		{
			PetitionLifecycle status = KingdomPetitions.Status(System);
			if (status != PetitionLifecycle.Offered && status != PetitionLifecycle.Accepted)
			{
				Popup.Show("No one is waiting. The settlement is content, or too busy to complain.");
				return;
			}
			if (status == PetitionLifecycle.Accepted)
			{
				Popup.Show(KingdomPetitions.Speech(System)
					+ "\n\n{{G|You gave your word.}} The petition remains open until the thing asked for is true, or its time runs out.");
				return;
			}
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Popup.Show(KingdomPetitions.Speech(System)
					+ "\n\n{{K|The realm is paused. You may read the petition, but no answer is recorded until settlement simulation resumes.}}");
				return;
			}
			int num = Popup.PickOption(Title: KingdomPresentation.Rich(System.PetitionPetitioner)
				+ " of " + KingdomPresentation.Rich(System.SeatName), Intro: KingdomPetitions.Speech(System), Options: new string[2] { "Say it will be seen to", "Tell them it must wait" }, AllowEscape: true);
			if (num == 0)
			{
				if (KingdomPetitions.Accept(System))
				{
					Popup.Show("{{G|You give your word.}} The petition will be judged against exactly what was asked for today.");
				}
				return;
			}
			if (num == 1)
			{
				if (KingdomPetitions.Decline(System))
				{
					Popup.Show("They nod, and go back to work. Nothing is held against you; the thing simply remains undone.");
				}
			}
		}

		/// <summary>
		/// Standing policy: the founder sets intent once and the settlement lives by it. Both
		/// choices trade one good thing for another, so neither is correct.
		/// </summary>
		public void SetPolicy(KingdomSystem System)
		{
			while (true)
			{
				int num = Popup.PickOption(Title: "The standing policy of " + KingdomPresentation.Rich(System.SeatName), Options: new string[2]
				{
					"Gates: {{C|" + KingdomRules.GatePolicyNames[(int)System.Gate] + "}} — " + KingdomRules.GatePolicyBlurbs[(int)System.Gate],
					"Stores: {{C|" + KingdomRules.StoresPolicyNames[(int)System.Stores] + "}} — " + KingdomRules.StoresPolicyBlurbs[(int)System.Stores]
				}, AllowEscape: true);
				if (num < 0)
				{
					return;
				}
				if (num == 0)
				{
					System.Gate = (System.Gate == KingdomRules.GatePolicy.Open) ? KingdomRules.GatePolicy.Guarded : KingdomRules.GatePolicy.Open;
					KingdomGovernanceScope.Commit("set gate policy");
					KingdomChronicle.Record(System, KingdomPresentation.Rich(System.SeatName) + " set its gates " + ((System.Gate == KingdomRules.GatePolicy.Open) ? "open to all comers" : "under the watch"));
				}
				else
				{
					System.Stores = (System.Stores == KingdomRules.StoresPolicy.Plenty) ? KingdomRules.StoresPolicy.Thrift : KingdomRules.StoresPolicy.Plenty;
					KingdomGovernanceScope.Commit("set stores policy");
					KingdomChronicle.Record(System, "the water-keepers of " + KingdomPresentation.Rich(System.SeatName) + " were told to " + ((System.Stores == KingdomRules.StoresPolicy.Thrift) ? "ration" : "pour freely"));
				}
				return;
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
			int num = Popup.PickOption(Title: "Declare this ground", Options: KingdomRules.DistrictNames, AllowEscape: true);
			if (num >= 0)
			{
				string district = KingdomRules.Districts[num];
				string currentDistrict;
				if (System.ZoneDistricts.TryGetValue(zone.ZoneID, out currentDistrict)
					&& currentDistrict == district)
				{
					Popup.Show("This ground is already the {{C|" + KingdomRules.DistrictName(district)
						+ "}} of " + KingdomPresentation.Rich(System.SeatName) + ".");
					return;
				}
				// Zoning is a decision, and a decision whose price the founder cannot see is a
				// trap: what this naming would put out of reach here is said before it does it.
				string lockout = KingdomZoning.LockoutWarning(System, zone.ZoneID, district);
				if (lockout != null && Popup.ShowYesNo(lockout + "\n\nName it the " + KingdomRules.DistrictName(district) + " anyway?") != DialogResult.Yes)
				{
					return;
				}
				System.ZoneDistricts[zone.ZoneID] = district;
				KingdomGovernanceScope.Commit("designate district");
				KingdomChronicle.Record(System, "the ground here was named the " + KingdomRules.DistrictName(district) + " of " + KingdomPresentation.Rich(System.SeatName));
				Popup.Show("This ground is the {{C|" + KingdomRules.DistrictName(district) + "}} of " + KingdomPresentation.Rich(System.SeatName) + ".");
			}
		}

	}
}
