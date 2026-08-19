using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	[Serializable]
	public class r_FounderBasin : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Found", "found a settlement", "r_FoundKingdom", null, 'f', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_FoundKingdom" && E.Actor != null && E.Actor.IsPlayer())
			{
				AttemptFounding(E.Actor);
			}
			return base.HandleEvent(E);
		}

		public void AttemptFounding(GameObject Actor)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				Popup.Show("The kingdom of {{C|" + system.KingdomDisplayName + "}} is already founded. One covenant at a time.");
				return;
			}
			LiquidVolume liquidVolume = ParentObject.GetPart<LiquidVolume>();
			int drams = KingdomLiquids.HasFreshWater(liquidVolume) ? liquidVolume.Volume : 0;
			if (drams < KingdomRules.FoundingCostDrams)
			{
				Popup.Show("The rite asks for {{C|" + KingdomRules.FoundingCostDrams + " drams}} of fresh water pooled in the basin. It holds " + drams + ".");
				return;
			}
			string name = Popup.AskString("Name the settlement.", "", MaxLength: 30, ReturnNullForEscape: true);
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			KingdomLiquids.Drain(liquidVolume, KingdomRules.FoundingCostDrams);
			KingdomFounding.Found(name);
			KingdomFounding.ClaimZone(Actor.CurrentZone);
			Popup.Show("You pour the first water, and those gathered drink.\n\n{{C|" + name + "}} is founded on this ground. Your thirst is theirs; their water is yours.\n\nLive and drink.");
		}
	}
}
