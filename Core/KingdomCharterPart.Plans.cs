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
		/// Stakes a plan on the ground the founder is standing on: names a design from the same
		/// registry <see cref="CommissionBuilding"/> reads, and leaves a marker for the settlement
		/// to raise later, on its own settlement pass, once the stores and the plan allow it.
		/// Nothing is spent here -- see <c>ThousandAndFirst.KingdomPlanMarker.OnSettlementPass</c>
		/// (Growth/KingdomPlanMarker.cs) for where the water actually leaves the stores, only once,
		/// at the moment the scaffold goes up.
		/// </summary>
		public void PlaceBuildingPlan(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Plans are staked on the kingdom's own ground.");
				return;
			}
			Cell cell = ParentObject.CurrentCell;
			if (cell == null || !cell.IsPassable() || cell.HasObjectWithPart("LiquidVolume"))
			{
				Popup.Show("Stand on clear, dry ground to stake a plan.");
				return;
			}
			foreach (GameObject occupant in cell.GetObjects())
			{
				if (occupant.HasPart("r_KingdomPlanMarker") || occupant.HasPart("r_KingdomScaffold") || occupant.GetIntProperty("KingdomBuilt") == 1)
				{
					Popup.Show("Something already stands here.");
					return;
				}
			}
			System.Collections.Generic.List<KingdomRules.BuildEntry> available = new System.Collections.Generic.List<KingdomRules.BuildEntry>();
			foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
			{
				// Style, stage, and the visibility law (KingdomZoning.Offered): the whole
				// catalogue is shown with its gates named, EXCEPT a creed-work this city has no
				// way to at all, which is not a locked door but a door in another city.
				if (KingdomZoning.Offered(System, entry))
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
				options[i] = available[i].DisplayName + " {{C|[" + available[i].CostDrams + " drams]}}"
					+ (KingdomZoning.GateNote(System, zone.ZoneID, available[i]) ?? "");
			}
			int num = Popup.PickOption(Title: "Stake a plan", Intro: "Nothing is spent now. " + KingdomPresentation.Rich(System.SeatName) + " raises this when the stores and the plan allow it.", Options: options, AllowEscape: true);
			if (num < 0)
			{
				return;
			}
			// Judged where the plan is staked, not where it is realised, because this cell is the
			// founder's decision and they are standing on it now.
			if (!KingdomZoning.Permits(System, zone.ZoneID, available[num], out var refusal))
			{
				Popup.Show(refusal);
				return;
			}
			KingdomRules.BuildEntry chosen = available[num];
			string plannedSkin = KingdomDesign.ChooseSkin(chosen, System.Style)?.Key;
			KingdomPlotQuote quote = null;
			if (KingdomPlots.IsPlotDesign(chosen.Key))
			{
				KingdomPlotRules.PlotSize stake = KingdomPlotRules.PlotSize.None;
				System.Collections.Generic.List<KingdomPlotRules.PlotSize> grounds =
					KingdomPlots.StakeableSizes(System, chosen);
				if (grounds.Count > 1)
				{
					System.Collections.Generic.List<KingdomPlotRules.ChainStep> chain =
						KingdomPlots.ChainOf(chosen);
					string[] stakes = new string[grounds.Count];
					for (int i = 0; i < grounds.Count; i++)
						stakes[i] = KingdomPlotRules.StakeOptionLine(grounds[i], chain);
					int ground = Popup.PickOption(Title: "How much ground will this plan reserve?",
						Intro: KingdomPlotRules.ForesightLine(grounds[0], chain), Options: stakes,
						AllowEscape: true);
					if (ground < 0) return;
					stake = grounds[ground];
				}
				if (!KingdomPlots.TryQuotePlan(System, zone, chosen, plannedSkin, stake, cell,
					out quote, out string quoteFailure))
				{
					Popup.Show(quoteFailure ?? "No exact authored lot can be reserved here.");
					return;
				}
				if (!KingdomArchitecturePreview.TryRender(quote.Architecture, chosen,
					quote.LabourTicks, out string preview, out string previewFailure))
				{
					Popup.Show(previewFailure ?? "The exact plan cannot be previewed.");
					return;
				}
				preview = KingdomPurpose.AppendPreview(preview, quote.PurposeReceipt);
				string waiting = "Nothing is spent now. The whole shown lot is reserved now; "
					+ "its frozen price is paid only when work begins.";
				int confirmed = Popup.PickOption(Title: "Reserve exact plan: " + chosen.Name,
					Intro: preview + "\n" + waiting,
					Options: new string[1] { "Drive the survey stake {{G|[confirm]}}" },
					AllowEscape: true);
				if (confirmed < 0) return;
			}
			GameObject marker = GameObject.Create("r_KingdomPlanMarker");
			if (marker == null)
			{
				Popup.Show("The plan could not be staked.");
				return;
			}
			r_KingdomPlanMarker part = marker.GetPart<r_KingdomPlanMarker>();
			string freezeFailure = null;
			part?.ApplyDesign(chosen);
			if (part == null || (quote != null
				&& !KingdomPlots.TryFreezePlan(marker, chosen, quote, out freezeFailure)))
			{
				marker.Obliterate(null, Silent: true);
				Popup.Show(freezeFailure ?? "The exact plan receipt could not be frozen. Nothing was staked.");
				return;
			}
			if (!string.IsNullOrEmpty(plannedSkin))
			{
				marker.SetStringProperty(KingdomDesign.PlannedSkinProperty, plannedSkin);
			}
			KingdomCeremony.StakePlan(marker, chosen, plannedSkin);
			cell.AddObject(marker);
			if (marker.CurrentCell != cell)
			{
				marker.Obliterate(null, Silent: true);
				Popup.Show("The plan could not be staked.");
				return;
			}
			KingdomGovernanceScope.Commit("stake building plan");
			KingdomChronicle.Record(System, "a plan for " + XRL.Language.Grammar.A(chosen.Name) + " was staked at " + KingdomPresentation.Rich(System.KingdomDisplayName));
			Popup.Show("{{G|The plan is staked.}} " + (quote == null
				? (KingdomPresentation.Rich(System.SeatName) + " will raise it when the water and the room allow.")
				: ("Its exact " + KingdomPlotRules.SizeName(quote.StakedSize)
					+ " lot, authored map, price, and labour are reserved until it is raised or cancelled.")));
		}

		/// <summary>
		/// Where the founder sees what is staked and calls plans off. Cancelling costs nothing and
		/// returns nothing, because a staked plan never spends anything until the moment it is
		/// realised -- there is nothing to refund.
		/// </summary>
		public void ManagePlans(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			System.Collections.Generic.List<GameObject> markers = KingdomPlanMarker.FindPending(zone);
			while (true)
			{
				string[] options = new string[markers.Count + 1];
				options[0] = "{{W|Stake a new plan}}";
				for (int i = 0; i < markers.Count; i++)
				{
					options[i + 1] = KingdomPlanMarker.Describe(markers[i]) + " {{K|[cancel]}}";
				}
				int num = Popup.PickOption(Title: "Plans staked at " + KingdomPresentation.Rich(System.SeatName), Options: options, AllowEscape: true);
				if (num < 0)
				{
					return;
				}
				if (num == 0)
				{
					PlaceBuildingPlan(System);
					if (KingdomGovernanceScope.HasCommitted)
					{
						return;
					}
					markers = KingdomPlanMarker.FindPending(zone);
					continue;
				}
				GameObject target = markers[num - 1];
				string name = KingdomPlanMarker.Describe(target);
				if (Popup.ShowYesNo("Cancel the plan for " + name + "? Nothing was spent, and nothing is returned, because nothing was taken.") == DialogResult.Yes)
				{
					KingdomChronicle.Record(System, "the plan for " + name + " at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " was called off");
					KingdomPlanMarker.Cancel(target);
					markers = KingdomPlanMarker.FindPending(zone);
				}
			}
		}

	}
}
