using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomWishes
	{
		[WishCommand("kingdom:claim", null)]
		public static void ClaimWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
			}
			else if (KingdomFounding.ClaimZone(zone))
			{
				Popup.Show("This zone now belongs to the kingdom: {{C|" + zone.ZoneID + "}}\n\nFuture spawns here will enroll as citizens.");
			}
			else
			{
				Popup.Show("A claim must border the kingdom's existing ground. ({{W|kingdom:claimforce}} overrides for testing.)");
			}
		}

		[WishCommand("kingdom:claimforce", null)]
		public static void ClaimForceWish()
		{
			Zone zone = The.Player?.CurrentZone;
			if (KingdomFounding.ClaimZone(zone, Force: true))
			{
				Popup.Show("Claimed by decree: {{C|" + zone.ZoneID + "}}");
			}
			else
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
			}
		}

	}
}
