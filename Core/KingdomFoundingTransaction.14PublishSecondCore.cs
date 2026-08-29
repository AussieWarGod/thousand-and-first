using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		private static void PublishSecondCore(r_FounderBasin Basin, KingdomSystem System,
			Zone Site)
		{
			Faction faction = Factions.GetIfExists(System.KingdomFactionName);
			if (faction == null || !FactionRegistryCoherent(System.KingdomFactionName, faction) ||
				!SiteReservationMatches(Site, Basin.PendingAuthority) ||
				!DirectRecoveryMatches(Site, Basin.PendingName, System.KingdomFactionName,
					Basin.PendingTransactionID))
			{
				throw new InvalidOperationException("The realm or recovery binding disappeared during second founding.");
			}
			if (!CommitExternalBinding(Basin, Site, out string externalFailure))
			{
				throw new InvalidOperationException(
					"External ownership changed before city publication: " + externalFailure);
			}
			if (!TryFreezeSecondIdentity(Basin, System, Site,
				out string frozenSettlementId, out string identityFailure))
			{
				throw new InvalidOperationException("The second-city identity could not be frozen: " +
					identityFailure);
			}
			string publicationAuthority = Site.GetZoneProperty(
				SecondPublicationAuthorityProperty, null);
			if (!string.IsNullOrEmpty(publicationAuthority) &&
				publicationAuthority != Basin.PendingAuthority)
			{
				throw new InvalidOperationException(
					"The ground carries another transaction's permanent city marker.");
			}
			bool published = SecondPublished(System, Basin.PendingName, Site.ZoneID,
				Basin.PendingTransactionID) &&
				PublishedSecondAuthorityMatches(Site, Basin.PendingAuthority);
			bool targetIsExactSeat = SecondIsExactSeat(System, Basin.PendingName,
				Site.ZoneID, Basin.PendingTransactionID);
			bool targetIsExactNonSeat = SecondIsExactNonSeat(System, Basin.PendingName,
				Site.ZoneID, Basin.PendingTransactionID);
			if (!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				System.SettlementCount, KingdomSettlement.MaxSettlements,
				System.NonSeatSettlementCount < KingdomSettlementTopologyRules.MaxNonSeatSettlements,
				targetIsExactSeat, targetIsExactNonSeat, published))
			{
				throw new InvalidOperationException("The second-city cap or seat changed before projection.");
			}
			if (string.IsNullOrEmpty(publicationAuthority))
			{
				Site.SetZoneProperty(SecondPublicationAuthorityProperty,
					Basin.PendingAuthority);
			}
			if (!PublishedSecondAuthorityMatches(Site, Basin.PendingAuthority))
			{
				throw new InvalidOperationException(
					"The permanent second-city authority was not retained.");
			}
			Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;
			string siteFaction = Site.GetZoneProperty("faction", null);
			if (KingdomRules.GroundIsForeignFaction(siteFaction, System.KingdomFactionName))
			{
				throw new InvalidOperationException("Foreign ground cannot be overwritten during recovery.");
			}
			Site.SetZoneProperty("faction", System.KingdomFactionName);
			if (Site.GetZoneProperty("faction", null) != System.KingdomFactionName)
			{
				throw new InvalidOperationException("The zone faction projection was refused.");
			}
			if (!faction.HolyPlaces.Contains(Site.ZoneID))
			{
				faction.HolyPlaces.Add(Site.ZoneID);
			}
			if (!faction.HolyPlaces.Contains(Site.ZoneID))
			{
				throw new InvalidOperationException("The holy-place projection was refused.");
			}
			Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;

			if (!SecondPublished(System, Basin.PendingName, Site.ZoneID,
				Basin.PendingTransactionID))
			{
				long foundedTick;
				if (!long.TryParse(Site.GetZoneProperty(SiteReservationTickProperty, null),
					out foundedTick) || foundedTick < 0L)
				{
					throw new InvalidOperationException("The reserved founding tick is malformed.");
				}
				string vocation = Site.GetZoneProperty(SiteReservationVocationProperty, null);
				if (!KingdomSettlement.IsKnownVocation(vocation) || vocation != Basin.PendingVocation)
				{
					throw new InvalidOperationException("The reserved second-city vocation is malformed.");
				}
				KingdomSettlement founded = new KingdomSettlement
				{
					SettlementName = Basin.PendingName,
					Vocation = vocation,
					FoundedTick = foundedTick,
					LastHeartbeatTick = foundedTick,
					LastVisitTick = foundedTick,
					LastSemanticTick = foundedTick
				};
				string lifecycleFailure;
				List<string> existingSettlementIds = System.LifecycleCollisionIds(
					IncludeSeat: true, IncludeAway: true);
				if (!KingdomSystem.TryBindSettlementIdentity(founded, frozenSettlementId,
					Basin.PendingTransactionID, Site.ZoneID, foundedTick,
					existingSettlementIds, out lifecycleFailure))
				{
					throw new InvalidOperationException(
						"The new city's exact lifecycle identity could not bind: " +
						lifecycleFailure);
				}
				founded.Style = KingdomFounding.ResolveFoundingStyle(Site,
					out var terrainBlueprint, out var regionName, out var zLevel);
				founded.FoundingTerrainBlueprint = terrainBlueprint;
				founded.FoundingRegionName = regionName;
				founded.FoundingZLevel = zLevel;
				founded.ClaimedZones.Add(Site.ZoneID);
				founded.NextArrivalTick = foundedTick +
					KingdomRules.ArrivalIntervalTicks(founded.Population);

				bool nonSeatIsNew = SecondIsExactNonSeat(System, Basin.PendingName, Site.ZoneID,
					Basin.PendingTransactionID);
				if (!nonSeatIsNew)
				{
					if (!System.TryAddNonSeatSettlement(founded, out string topologyFailure))
					{
						throw new InvalidOperationException(
							"The bounded non-seat topology refused the exact city: " +
							topologyFailure);
					}
				}
				if (!SeatSecond(System, Basin.PendingName, Site, Basin.PendingAuthority,
					Basin.PendingTransactionID))
				{
					throw new InvalidOperationException("The exact transaction city could not take the open seat.");
				}
				if (!SecondPublished(System, Basin.PendingName, Site.ZoneID,
					Basin.PendingTransactionID))
				{
					throw new InvalidOperationException("The city seat did not retain the new settlement.");
				}
			}
		}

		/// <summary>Freezes the later city's immutable output before Trade topology expansion,
		/// the permanent site marker, faction/holy-place callbacks, or Away publication. Exact
		/// partial writes may resume; any third value quarantines the founding receipt.</summary>
	}
}
