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
		/// Calls a shared meal from the ground's dedicated larders. The service does its own
		/// eligibility check and success messaging; this only surfaces a decline, matching
		/// every other action here.
		/// </summary>
		public void HoldSharedMeal(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			// Asked before anything is eaten. The food is the founder's - dedicating a larder is
			// consent to it being counted, not consent to it being spent without a word.
			KingdomSurvey survey = (zone != null) ? KingdomSurvey.Take(zone, System) : null;
			if (survey != null && survey.FoodAbundance != KingdomRules.PantryTier.Empty)
			{
				int cost = KingdomRules.MealCost(survey.FoodAbundance);
				if (Popup.ShowYesNo("Call " + KingdomRules.MealSizeName(survey.FoodAbundance) + " for " + KingdomPresentation.Rich(System.SeatName)
					+ "?\n\nIt will take {{C|" + cost + "}} of the " + survey.FoodStored
					+ " the larders hold.") != DialogResult.Yes)
				{
					return;
				}
			}
			if (!KingdomLarder.HoldSharedMeal(System, zone, out var failure))
			{
				Popup.Show(failure);
				return;
			}
		}

		/// <summary>
		/// Certifies a machine hauled home from a ruin fit for the settlement's grid, or takes
		/// an already-certified one back off it. The cost is disclosed here, before anything is
		/// spent; KingdomSalvage does the actual eligibility check, the spend, and the messaging.
		/// </summary>
		public void CertifyMachine(KingdomSystem System)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A machine is certified on the kingdom's own ground, not in other people's houses.");
				return;
			}
			System.Collections.Generic.List<GameObject> machines = new System.Collections.Generic.List<GameObject>();
			foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
			{
				foreach (GameObject item in adjacentCell.GetObjects())
				{
					if (item.IsCreature || item.GetPart<XRL.World.Parts.LiquidVolume>() != null || machines.Contains(item))
					{
						continue;
					}
					// A "machine" here is anything the settlement's own Examiner/TinkerItem
					// readings can actually inspect; plain scenery never shows up in this list.
					if (item.GetPart<XRL.World.Parts.Examiner>()?.Complexity > 0 || item.GetPart<XRL.World.Parts.TinkerItem>() != null)
					{
						machines.Add(item);
					}
				}
			}
			if (machines.Count == 0)
			{
				Popup.Show("Stand beside a machine to certify it. Nothing here reads as one.");
				return;
			}
			string[] options = new string[machines.Count];
			for (int i = 0; i < machines.Count; i++)
			{
				options[i] = machines[i].ShortDisplayName + ((machines[i].GetIntProperty(KingdomSalvage.CertifiedProperty) == 1) ? " {{G|[certified]}}" : " {{K|[uncertified]}}");
			}
			int index = Popup.PickOption(Title: "Certify a machine", Options: options, AllowEscape: true);
			if (index < 0)
			{
				return;
			}
			GameObject machine = machines[index];
			KingdomSalvage.SalvageAssessment assessment = KingdomSalvage.Assess(System, zone, machine);
			if (assessment.AlreadyCertified)
			{
				if (Popup.ShowYesNo("Take " + machine.ShortDisplayName + " off the grid of " + KingdomPresentation.Rich(System.SeatName) + "? It will stand exactly where it stands; the settlement will simply stop answering for it.") != DialogResult.Yes)
				{
					return;
				}
			}
			else if (assessment.Verdict == KingdomSalvageRules.SalvageVerdict.Certified)
			{
				if (Popup.ShowYesNo("Certify " + machine.ShortDisplayName + " fit for the grid of " + KingdomPresentation.Rich(System.SeatName) + "?\n\nIt will cost {{C|" + assessment.WaterCost + "}} drams and " + assessment.HandsRequired + " hands free to test it.") != DialogResult.Yes)
				{
					return;
				}
			}
			if (!KingdomSalvage.Certify(System, zone, machine, out var failure))
			{
				Popup.Show(failure);
				return;
			}
		}

		public void AdoptBuilding(KingdomSystem System)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A building is adopted on the kingdom's own ground, not in other people's houses.");
				return;
			}
			System.Collections.Generic.List<KingdomRules.BuildEntry> buildings = KingdomData.Buildings;
			if (buildings.Count == 0)
			{
				Popup.Show("There is nothing in the plan to adopt a building as.");
				return;
			}
			string[] designOptions = new string[buildings.Count];
			for (int i = 0; i < buildings.Count; i++)
			{
				designOptions[i] = buildings[i].Name + " {{K|(" + buildings[i].Category + ")}}";
			}
			int designIndex = Popup.PickOption(Title: "Adopt a building as...", Intro: "Choose what kind of building this counts as. The settlement checks the space, not who built it.", Options: designOptions, AllowEscape: true);
			if (designIndex < 0)
			{
				return;
			}
			KingdomRules.BuildEntry entry = buildings[designIndex];
			KingdomAdoptRules.RoleKind role = KingdomAdoptRules.ClassifyRole(entry.Category);
			if (role == KingdomAdoptRules.RoleKind.Work)
			{
				if (Popup.ShowYesNo("Adopt this room as " + XRL.Language.Grammar.A(entry.Name) + "? A small marker is set down where you stand; nothing you built is touched.") != DialogResult.Yes)
				{
					return;
				}
				if (!KingdomAdopt.AdoptWork(System, zone, cell, entry.Key, out var workFailure))
				{
					Popup.Show(workFailure);
					return;
				}
				return;
			}
			System.Collections.Generic.List<GameObject> candidates = new System.Collections.Generic.List<GameObject>();
			foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
			{
				foreach (GameObject item in adjacentCell.GetObjects())
				{
					if (item.GetIntProperty("KingdomBuilt") == 1 || candidates.Contains(item))
					{
						continue;
					}
					if (role == KingdomAdoptRules.RoleKind.Housing && item.HasPart("Bed"))
					{
						candidates.Add(item);
					}
					else if (role == KingdomAdoptRules.RoleKind.Storage)
					{
						XRL.World.Parts.LiquidVolume lv = item.GetPart<XRL.World.Parts.LiquidVolume>();
						bool isVessel = lv != null && lv.MaxVolume > 0;
						bool isLarder = lv == null && item.Inventory != null;
						if (isVessel || isLarder)
						{
							candidates.Add(item);
						}
					}
				}
			}
			if (candidates.Count == 0)
			{
				Popup.Show((role == KingdomAdoptRules.RoleKind.Housing) ? "Stand beside a bed to adopt it. Nothing here is one." : "Stand beside a vessel or a store to adopt it. Nothing here is one.");
				return;
			}
			string[] candidateOptions = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				candidateOptions[i] = candidates[i].ShortDisplayName + ((candidates[i].GetIntProperty("KingdomStores") == 1 || candidates[i].GetIntProperty("KingdomLarder") == 1) ? " {{G|[dedicated]}}" : "");
			}
			int candidateIndex = Popup.PickOption(Title: "Adopt which one?", Options: candidateOptions, AllowEscape: true);
			if (candidateIndex < 0)
			{
				return;
			}
			GameObject candidate = candidates[candidateIndex];
			if (Popup.ShowYesNo("Adopt " + candidate.ShortDisplayName + " as " + XRL.Language.Grammar.A(entry.Name) + "?") != DialogResult.Yes)
			{
				return;
			}
			if (!KingdomAdopt.AdoptExisting(System, zone, candidate, entry.Key, out var failure))
			{
				Popup.Show(failure);
				return;
			}
		}

	}
}
