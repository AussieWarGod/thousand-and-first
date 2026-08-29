using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		public void StrikeTradeCharter(KingdomSystem System)
		{
			if (!KingdomTrade.Enabled)
			{
				Popup.Show("Trade is disabled. Existing receipts remain recorded, but no new charter is struck.");
				return;
			}
			System.Collections.Generic.List<KingdomRules.DealEntry> deals = KingdomData.Deals;
			if (deals.Count == 0)
			{
				Popup.Show("No charters are known.");
				return;
			}
			string[] dealOptions = new string[deals.Count];
			for (int i = 0; i < deals.Count; i++)
			{
				dealOptions[i] = deals[i].DisplayName + " {{C|[standing " + deals[i].MinStanding + "+]}}";
			}
			int dealPick = Popup.PickOption(Title: "Which charter?", Options: dealOptions, AllowEscape: true);
			if (dealPick < 0)
			{
				return;
			}
			System.Collections.Generic.List<string> eligible = new System.Collections.Generic.List<string>();
			System.Collections.Generic.List<string> labels = new System.Collections.Generic.List<string>();
			foreach (Faction faction in Factions.Loop())
			{
				if (faction.Visible && faction.Name != System.KingdomFactionName &&
					faction.Name != "Player" &&
					System.GetRegardForRealm(faction.Name) >= deals[dealPick].MinStanding)
				{
					eligible.Add(faction.Name);
					labels.Add(faction.DisplayName + " (their regard " +
						System.GetRegardForRealm(faction.Name) + ")");
					if (eligible.Count >= 20)
					{
						break;
					}
				}
			}
			if (eligible.Count == 0)
			{
				Popup.Show("No faction holds the kingdom in high enough regard for that charter.");
				return;
			}
			int factionPick = Popup.PickOption(Title: "With whom?", Options: labels.ToArray(), AllowEscape: true);
			if (factionPick >= 0)
			{
				if (!KingdomTrade.StrikeDeal(System, deals[dealPick].Key, eligible[factionPick], out var failure))
				{
					Popup.Show(failure);
				}
			}
		}

		/// <summary>Loads the realm's one receipt-bound manifest through <see cref="KingdomTrade"/>.</summary>
		public void LoadManifest(KingdomSystem System)
		{
			if (!KingdomTrade.Enabled)
			{
				Popup.Show("Trade is disabled. Existing manifests keep their receipts, but no new load leaves the stores.");
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			bool onGround = zone != null && System.ClaimedZones.Contains(zone.ZoneID);
			if (onGround)
			{
				KingdomManifest lapsed = KingdomTrade.ExpireManifestIfStale(System, zone, The.Game.TimeTicks);
				if (lapsed != null)
				{
					Popup.Show("The manifest road has closed. Its " + lapsed.Drams
						+ " drams remain held under their permanent receipt; none were destroyed or reissued.");
					return;
				}
			}
			KingdomTradeManifestState current = KingdomTrade.CurrentManifest(System);
			int stored = onGround ? KingdomGrowth.CountStoredWater(zone) : 0;
			int rawAmount = KingdomManifestRules.ManifestAmount(stored, System.Population);
			if (current != null)
			{
				if (current.Status == KingdomTradeManifestStatus.Quarantined)
					Popup.Show("The manifest receipt is held for inspection: " +
						(current.Fault ?? "its physical state is uncertain") +
						". No second load will be issued against it.");
				else Popup.Show(KingdomManifestRules.ManifestInFlightStatus(current.OriginName,
					current.DestinationName, current.EscrowDrams, The.Game.TimeTicks,
					current.DeadlineTick));
				return;
			}
			System.Collections.Generic.List<KingdomSettlement> destinations =
				System.NonSeatSettlements();
			KingdomSettlement destinationCity = null;
			if (destinations.Count == 1) destinationCity = destinations[0];
			else if (destinations.Count > 1)
			{
				string[] options = new string[destinations.Count];
				for (int i = 0; i < destinations.Count; i++) options[i] =
					KingdomPresentation.Rich(destinations[i].SettlementName) +
					KingdomSettlement.VocationSuffix(destinations[i].Vocation);
				int chosen = Popup.PickOption(Title: "Send water where?", Options: options,
					AllowEscape: true);
				if (chosen < 0) return;
				destinationCity = destinations[chosen];
			}
			// Sized against what the realm BELIEVES the other city can take - the figure it had
			// when the founder last stood there. Loading to that belief is what makes arriving
			// with nowhere to put it a rare, specific event rather than routine spillage.
			int believedRoom = destinationCity?.LastKnownStorageSpace ?? 0;
			int amount = KingdomManifestRules.CapToDestination(rawAmount, believedRoom);
			KingdomManifestRules.ManifestVerdict verdict = KingdomManifestRules.JudgeManifest(
				onGround, destinationCity != null, false, rawAmount, believedRoom);
			if (verdict != KingdomManifestRules.ManifestVerdict.Allowed)
			{
				Popup.Show(KingdomManifestRules.ManifestRefusal(verdict,
					KingdomPresentation.Rich(destinationCity?.SettlementName)));
				return;
			}
			// The price is named before the water moves. Every other spending action in this
			// menu tells the founder what it costs and lets them back out; a manifest sends the
			// largest single amount of water in the mod, and must not be the exception.
			if (Popup.ShowYesNo("Send {{C|" + amount + " drams}} from " + KingdomPresentation.Rich(System.SeatName) + " to " + KingdomPresentation.Rich(destinationCity.SettlementName)
				+ "?\n\nThe water leaves the stores here now. It arrives when you next stand in "
				+ KingdomPresentation.Rich(destinationCity.SettlementName) + ", and if you have not come within "
				+ KingdomManifestRules.ManifestWindowDays + " days the carters turn back and bring it home.") != DialogResult.Yes)
			{
				return;
			}
			string origin = System.SeatName;
			string destination = destinationCity.SettlementName;
			if (!KingdomTrade.TryLoadManifest(System, zone, amount, origin, destination,
				out string failure))
			{
				Popup.Show(failure);
				return;
			}
			KingdomGovernanceScope.Commit("send water manifest");
			KingdomTradeManifestState loaded = KingdomTrade.CurrentManifest(System);
			KingdomLog.Log("manifest: loaded id=" + loaded?.Id + " amount=" + amount
				+ " deadline=" + loaded?.DeadlineTick);
		}

	}
}
