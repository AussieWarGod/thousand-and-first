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
		[WishCommand("kingdom:reset", null)]
		public static void ResetWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded && !system.Exiled)
			{
				Popup.Show("Nothing to reset.");
				return;
			}
			string held = system.Founded ? ("{{C|" + KingdomPresentation.Rich(system.KingdomDisplayName) + "}}, all " + system.SettlementCount + " of its cities") : "the realm you hold (none)";
			string remembered = system.Exiled ? (", and {{C|" + KingdomPresentation.Rich(system.ExiledDisplayName) + "}}, which put you out") : "";
			if (Popup.ShowYesNo("Dissolve " + held + remembered + ", and wipe all kingdom state? (Debug only; claimed-zone properties in unvisited zones are left behind.)") != DialogResult.Yes)
			{
				return;
			}
			Zone zone = The.Player?.CurrentZone;
			if (!KingdomFoundingTransaction.TryPrepareDebugReset(system, The.Player,
				zone, out var resetFailure))
			{
				Popup.Show("Reset refused without changing kingdom state: " + resetFailure +
					" Resolve or resume that founding transaction first.");
				return;
			}
			string tradeResetFailure;
			if (!KingdomTrade.ResetAuthority(system, out tradeResetFailure))
			{
				Popup.Show(tradeResetFailure);
				return;
			}
			foreach (string name in new string[2] { system.KingdomFactionName, system.ExiledFactionName })
			{
				if (string.IsNullOrEmpty(name))
				{
					continue;
				}
				// A faction cannot be unregistered at runtime; hiding it and dropping every edge
				// to it is as close as a debug reset can honestly get.
				Faction faction = Factions.GetIfExists(name);
				if (faction != null)
				{
					faction.Visible = false;
				}
				foreach (Faction item in Factions.Loop())
				{
					item.FactionFeeling.Remove(name);
				}
				The.Game.PlayerReputation.ReputationValues.Remove(name);
			}
			KingdomCharterPart part = The.Player?.GetPart<KingdomCharterPart>();
			if (part != null)
			{
				part.RemoveAbility();
				The.Player.RemovePart(part);
			}
			system.KingdomFactionName = null;
			system.KingdomDisplayName = null;
			List<KingdomSettlement> nonSeat = system.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				if (!system.TryRemoveNonSeatSettlement(nonSeat[i], out string topologyFailure))
				{
					Popup.Show("Reset stopped at an inexact non-seat topology: " + topologyFailure);
					return;
				}
			// Seating a blank settlement clears every carried per-settlement field; removing each
			// exact non-seat row above clears the rest without treating a wire projection as authority.
			system.Restore(new KingdomSettlement());
			// The exile slot is state too, and a reset that left a remembered realm behind would
			// have the next founding start with a door already shut.
			system.ExiledFactionName = null;
			system.ExiledDisplayName = null;
			system.ExiledSeat = null;
			system.ExiledSettlementTopology = new KingdomSettlementTopology();
			system.ExiledDeed = null;
			system.ExiledTick = 0L;
			system.ExiledStandings.Clear();
			system.ExiledRealmPolicyToward.Clear();
			system.ExiledRegardSpilloverRemainders.Clear();
			system.ExiledRegardSpilloverObservedReputation.Clear();
			system.RegardSpoken = (int)RealmRegard.Beloved;
			system.ReturnAskedRegard = int.MinValue;
			system.DoorClosedTold = false;
			system.ActiveDealKeys.Clear();
			system.ActiveDealFactions.Clear();
			system.DealNextTicks.Clear();
			system.ChronicleEntries.Clear();
			system.OutsiderEntries.Clear();
			system.Standings.Clear();
			system.RealmPolicyToward.Clear();
			system.RegardSpilloverRemainders.Clear();
			system.RegardSpilloverObservedReputation.Clear();
			system.DirectionalStandingSchemaVersion = 0;
			system.SynchronizeLegacyManifestProjection();
			Popup.Show("All cities are dissolved. The ground forgets; the chronicle does not survive it.");
		}

		[WishCommand("kingdom:citizen", null)]
		public static void CitizenWish()
		{
			GameObject target = null;
			Cell cell = The.Player?.CurrentCell;
			if (cell != null)
			{
				foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
				{
					target = adjacentCell.GetFirstObjectWithPart("Brain");
					if (target != null && !target.IsPlayer())
					{
						break;
					}
					target = null;
				}
			}
			if (target == null)
			{
				Popup.Show("Stand next to a creature to enroll it.");
			}
			else if (KingdomFounding.EnrollCitizen(target))
			{
				Popup.Show(target.The + target.ShortDisplayName + " joins the kingdom as a citizen.");
			}
			else
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
			}
		}

	}
}
