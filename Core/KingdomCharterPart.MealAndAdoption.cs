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
			int kitchens = (survey == null) ? 0 : KingdomCapabilityRuntime.Count(zone, survey,
				KingdomBenefitCapabilities.Cooking, "shared meal preview");
			if (survey != null && KingdomRules.CanHoldSharedMeal(
				survey.FoodStored, System.Population, kitchens))
			{
				int cost = KingdomRules.MealCost(survey.FoodAbundance);
				if (Popup.ShowYesNo("Call " + KingdomRules.MealSizeName(survey.FoodAbundance) + " for " + KingdomPresentation.Rich(System.SeatName)
					+ "?\n\nIt will take {{C|" + cost + "}} of the " + survey.FoodStored
					+ " the larders hold. A currently capable kitchen will cook it.") != DialogResult.Yes)
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
			System.Collections.Generic.List<KingdomRules.BuildEntry> buildings =
				new System.Collections.Generic.List<KingdomRules.BuildEntry>();
			System.Collections.Generic.List<KingdomAdoptionTargetKind> targets =
				new System.Collections.Generic.List<KingdomAdoptionTargetKind>();
			foreach (KingdomRules.BuildEntry buildCandidate in KingdomData.Buildings)
			{
				if (!buildCandidate.Adoptable
					|| !KingdomPlots.TryGetSpec(buildCandidate.Key, out KingdomPlotRules.PlotSpec spec)
					|| !KingdomAdoptabilityRules.TryClassify(buildCandidate.Key, buildCandidate.Category,
						spec.Size, spec.Open, out KingdomAdoptionTargetKind target, out _)) continue;
				buildings.Add(buildCandidate); targets.Add(target);
			}
			if (buildings.Count == 0)
			{
				Popup.Show("There is nothing in the plan to adopt a building as.");
				return;
			}
			string[] designOptions = new string[buildings.Count];
			for (int i = 0; i < buildings.Count; i++)
			{
				string shape = targets[i] == KingdomAdoptionTargetKind.Room ? "room"
					: targets[i] == KingdomAdoptionTargetKind.OpenPlot ? "open plot"
					: "container";
				designOptions[i] = buildings[i].Name + " {{K|(" + buildings[i].Category
					+ "; " + shape + ")}}";
			}
			int designIndex = Popup.PickOption(Title: "Adopt a building as...", Intro: "Choose what kind of building this counts as. The settlement checks the space, not who built it.", Options: designOptions, AllowEscape: true);
			if (designIndex < 0)
			{
				return;
			}
			KingdomRules.BuildEntry entry = buildings[designIndex];
			KingdomAdoptionTargetKind targetKind = targets[designIndex];
			if (targetKind == KingdomAdoptionTargetKind.OpenPlot)
			{
				Cell center = ParentObject.Physics.PickDestinationCell(9999,
					AllowVis.OnlyExplored, Locked: false, IgnoreSolid: true, IgnoreLOS: true,
						RequireCombat: false, PickTarget.PickStyle.EmptyCell,
						"Centre the " + entry.Name + " plot where?");
				if (center == null) return;
				string plotFailure = null;
				if (center.ParentZone != zone
					|| !KingdomPlots.TryGetSpec(entry.Key, out KingdomPlotRules.PlotSpec spec)
					|| !KingdomAdoptionPlotRules.TryCenteredCells(center.X, center.Y, spec.Size,
						zone.Width, zone.Height, out KingdomPlotRules.PlotRect rect, out _,
						out plotFailure))
				{
					Popup.Show(plotFailure ?? "Choose a centre on this settlement's ground.");
					return;
				}
				string dimensions = (rect.X2 - rect.X1 + 1) + "×" + (rect.Y2 - rect.Y1 + 1);
				if (Popup.ShowYesNo("Designate this exact " + dimensions + " open plot as "
					+ XRL.Language.Grammar.A(entry.Name) + "? Its civic marker reserves the shown"
					+ " ground; only real qualifying furniture or machinery supplies benefits.")
					!= DialogResult.Yes) return;
				if (!KingdomAdopt.AdoptOpenPlot(System, zone, center, entry.Key,
					out string openFailure)) Popup.Show(openFailure);
				return;
			}
			if (targetKind == KingdomAdoptionTargetKind.Room)
			{
				if (Popup.ShowYesNo("Designate this exact bounded room as " + XRL.Language.Grammar.A(entry.Name) + "? A small civic marker records its cells. The room grants nothing until real qualifying furniture or technology stands inside it.") != DialogResult.Yes)
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
					if (targetKind == KingdomAdoptionTargetKind.Larder)
					{
						XRL.World.Parts.LiquidVolume lv = item.GetPart<XRL.World.Parts.LiquidVolume>();
						if (KingdomAdoptabilityRules.CandidateMatches(targetKind, lv != null,
							item.Inventory != null))
						{
							candidates.Add(item);
						}
					}
				}
			}
			if (candidates.Count == 0)
			{
				Popup.Show("Stand beside a dry container to designate it as a larder.");
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
