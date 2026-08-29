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
		[WishCommand("kingdom:status", null)]
		public static void StatusWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show(system.Exiled
					? (ExileReport(system) + "\n\n{{K|You hold no realm. The basin still pours: kingdom:found NAME founds a new one, and shuts the door on this one for good.}}")
					: "No kingdom founded. Wish {{W|kingdom:found NAME}} to begin.");
				return;
			}
			Popup.Show(SeatLine(system) + "\n" + RegardReport(system) + "\n" + KingdomReports.Status(system) + "\n\n" + KingdomReports.Standings(system)
				+ (system.Exiled ? ("\n\n" + ExileReport(system)) : ""));
		}

		[WishCommand("kingdom:standing", null)]
		public static void StandingWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet.");
				return;
			}
			if (!TryParseFactionAmount(Parameter, out var faction, out var amount))
			{
				Popup.Show("Usage: {{W|kingdom:standing FactionName:Amount}}");
				return;
			}
			if (!system.TrySetRegardForRealm(faction.Name, amount))
			{
				Popup.Show("That directional regard edge is unavailable or reserved.");
				return;
			}
			faction.FactionFeeling.TryGetValue(system.KingdomFactionName, out var feeling);
			Popup.Show(faction.DisplayName + "'s regard for the realm is now " + amount +
				".\nTheir projected feeling toward its citizens is " + feeling + ".");
		}

		[WishCommand("kingdom:policy", null)]
		public static void PolicyWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet.");
				return;
			}
			if (!TryParseFactionAmount(Parameter, out var faction, out var amount))
			{
				Popup.Show("Usage: {{W|kingdom:policy FactionName:Amount}}");
				return;
			}
			if (!system.TrySetRealmPolicyToward(faction.Name, amount))
			{
				Popup.Show("That directional policy edge is unavailable or reserved.");
				return;
			}
			Faction realm = Factions.GetIfExists(system.KingdomFactionName);
			int feeling = 0;
			if (realm != null) realm.FactionFeeling.TryGetValue(faction.Name, out feeling);
			Popup.Show("The realm's policy toward " + faction.DisplayName + " is now " +
				amount + ".\nIts projected feeling is " + feeling + ".");
		}

		[WishCommand("kingdom:rep", null)]
		public static void RepWish(string Parameter)
		{
			if (!TryParseFactionAmount(Parameter, out var faction, out var amount))
			{
				Popup.Show("Usage: {{W|kingdom:rep FactionName:Amount}}");
				return;
			}
			The.Game.PlayerReputation.Modify(faction, amount, "Wish");
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				faction.FactionFeeling.TryGetValue(system.KingdomFactionName, out var feeling);
				Popup.Show("Spillover check: " + faction.DisplayName + "'s regard for the realm is " +
					system.GetRegardForRealm(faction.Name) + ", with projected feeling " + feeling + ".");
			}
		}

		[WishCommand("kingdom:chronicle", null)]
		public static void ChronicleWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("The chronicle is empty.");
				return;
			}
			Popup.Show(KingdomReports.Chronicle(system) + "\n\n" + KingdomReports.Chronicle(system, Outsider: true));
		}

		[WishCommand("kingdom:grow", null)]
		public static void GrowWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (!system.Founded || zone == null || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Stand in a claimed zone first ({{W|kingdom:claim}}).");
				return;
			}
			system.NextArrivalTick = The.Game.TimeTicks;
			int before = system.Population;
			KingdomGrowth.OnZoneActivated(system, zone);
			Popup.Show("Forced growth pass: population " + before + " -> " + system.Population + ", stored water now " + KingdomGrowth.CountStoredWater(zone) + " drams.");
		}

	}
}
