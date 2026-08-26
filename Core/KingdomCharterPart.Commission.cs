using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		public void CommissionBuilding(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			int stored = (zone != null) ? KingdomGrowth.CountStoredWater(zone) : 0;
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
				// The whole catalogue is shown, blocked designs included, each carrying the one
				// thing standing in its way. A list that silently shortens teaches nothing.
				options[i] = available[i].DisplayName + " {{C|[" + available[i].CostDrams + " drams]}}"
					+ (KingdomZoning.GateNote(System, zone?.ZoneID, available[i]) ?? "");
			}
			int num = Popup.PickOption(Title: "Commission ({{C|" + stored + " drams}} in the stores)", Options: options, AllowEscape: true);
			if (num >= 0)
			{
				// Asked before the water moves, and only for a design that actually has a look to
				// choose; escaping the skin prompt takes the design's own. Not asked at all off the
				// kingdom's ground, where the commission below is going to be refused anyway.
				bool onGround = zone != null && System.ClaimedZones.Contains(zone.ZoneID);
				string skin = onGround ? KingdomDesign.ChooseSkin(available[num], System.Style)?.Key : null;
				// How much ground to stake is the founder's decision, and it is the one decision that
				// sets the ceiling on everything this plot will ever become. The whole chain's
				// footprints are shown before the stake goes in, never after it.
				KingdomPlotRules.PlotSize stake = KingdomPlotRules.PlotSize.None;
				if (onGround)
				{
					System.Collections.Generic.List<KingdomPlotRules.PlotSize> grounds = KingdomPlots.StakeableSizes(System, available[num]);
					if (grounds.Count > 1)
					{
						System.Collections.Generic.List<KingdomPlotRules.ChainStep> chain = KingdomPlots.ChainOf(available[num]);
						string[] stakes = new string[grounds.Count];
						for (int i = 0; i < grounds.Count; i++)
						{
							stakes[i] = KingdomPlotRules.StakeOptionLine(grounds[i], chain);
						}
						int ground = Popup.PickOption(Title: "How much ground?", Intro: KingdomPlotRules.ForesightLine(grounds[0], chain), Options: stakes, AllowEscape: true);
						if (ground < 0)
						{
							return;
						}
						stake = grounds[ground];
					}
				}
				KingdomPlotQuote quote = null;
				if (onGround && KingdomPlots.IsPlotDesign(available[num].Key))
				{
					if (!KingdomPlots.TryQuoteCommission(System, zone, available[num], skin, stake,
						out quote, out string quoteFailure))
					{
						Popup.Show(quoteFailure
							?? "The exact production plan cannot be prepared. Nothing was spent.");
						return;
					}
					if (!KingdomArchitecturePreview.TryRender(quote.Architecture, available[num],
						quote.LabourTicks, out string preview, out string previewFailure))
					{
						Popup.Show(previewFailure
							?? "The exact production plan cannot be previewed. Nothing was spent.");
						return;
					}
					preview = KingdomPurpose.AppendPreview(preview, quote.PurposeReceipt);
					string blocker = null;
					if (stored < quote.WaterDrams)
						blocker = "BLOCKED: the stores are short "
							+ (quote.WaterDrams - stored) + " drams.";
					else if (!KingdomMaterials.CanPay(zone, available[num].Key,
						out string materialBlocker)) blocker = "BLOCKED: " + materialBlocker;
					string intro = preview + (blocker == null ? ""
						: ("\n" + blocker + "\nNothing can be commissioned until this is lifted."));
					string[] choices = blocker == null
						? new string[1] { "Commission this exact plan {{G|[confirm]}}" }
						: new string[1] { "Return without commissioning" };
					int confirmed = Popup.PickOption(Title: "Production plan: "
						+ available[num].Name, Intro: intro, Options: choices, AllowEscape: true);
					if (confirmed < 0 || blocker != null) return;
				}
				if (!KingdomCommission.Commission(System, available[num].Key, skin, stake,
					quote, out var failure))
				{
					Popup.Show(failure);
					return;
				}
			}
		}

	}
}
