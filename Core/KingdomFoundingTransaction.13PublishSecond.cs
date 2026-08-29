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
		private static void PublishSecond(r_FounderBasin Basin, GameObject Actor, Zone Site,
			ref KingdomFoundingProjection Projection, bool Force)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Faction faction = Factions.GetIfExists(system.KingdomFactionName);
			if (Basin.PendingRealmFaction != system.KingdomFactionName ||
				string.IsNullOrEmpty(Basin.PendingTransactionID) ||
				!FactionRegistryCoherent(system.KingdomFactionName, faction))
			{
				throw new InvalidOperationException("The second founding no longer binds one coherent realm faction.");
			}
			bool published = SecondPublished(system, Basin.PendingName, Site.ZoneID,
				Basin.PendingTransactionID) &&
				PublishedSecondAuthorityMatches(Site, Basin.PendingAuthority);
			bool targetIsExactSeat = SecondIsExactSeat(system, Basin.PendingName,
				Site.ZoneID, Basin.PendingTransactionID);
			bool targetIsExactNonSeat = SecondIsExactNonSeat(system, Basin.PendingName,
				Site.ZoneID, Basin.PendingTransactionID);
			if (!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				system.SettlementCount, KingdomSettlement.MaxSettlements,
				system.NonSeatSettlementCount < KingdomSettlementTopologyRules.MaxNonSeatSettlements,
				targetIsExactSeat, targetIsExactNonSeat, published) ||
				!SiteReservationMatches(Site, Basin.PendingAuthority))
			{
				throw new InvalidOperationException("The second founding cannot replace the realm's current city seats.");
			}
			bool partialClaim = Site.GetZoneProperty("faction", null) ==
				system.KingdomFactionName || faction.HolyPlaces.Contains(Site.ZoneID);
			if (!published)
			{
				if (!DirectRecoveryMatches(Site, Basin.PendingName,
					system.KingdomFactionName, Basin.PendingTransactionID))
				{
					throw new InvalidOperationException("This ground carries another second-founding transaction.");
				}
				// A marker alone publishes nothing. Until zone faction/holy-place projection exists,
				// the site must still pass the ordinary founding verdict on every retry.
				if (!partialClaim)
				{
					KingdomSettlement.SecondFoundingVerdict verdict = KingdomFounding.JudgeSite(system, Site);
					bool allowed = verdict == KingdomSettlement.SecondFoundingVerdict.Allowed ||
						(Force && verdict == KingdomSettlement.SecondFoundingVerdict.GroundIsTooClose);
					if (!allowed || KingdomRules.GroundIsForeignFaction(
						Site.GetZoneProperty("faction"), system.KingdomFactionName))
					{
						throw new InvalidOperationException("The second-city site changed before publication.");
					}
				}
				PublishSecondCore(Basin, system, Site);
			}
			if (!SeatSecond(system, Basin.PendingName, Site, Basin.PendingAuthority,
				Basin.PendingTransactionID))
			{
				throw new InvalidOperationException("The second city exists, but its ground cannot take the seat.");
			}
			if (!system.TrySettlePendingSettlementIdentity(Basin.PendingTransactionID,
				Site.ZoneID, Basin.PendingAuthority, out string topologyFailure))
			{
				throw new InvalidOperationException(
					"The published city could not settle paired Trade and Carry topology: " +
					topologyFailure);
			}
			Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;
			Projection = KingdomFoundingProjection.Identity;
			if (faction == null || !system.ClaimedZones.Contains(Site.ZoneID) ||
				Site.GetZoneProperty("faction", null) != system.KingdomFactionName ||
				!faction.HolyPlaces.Contains(Site.ZoneID))
			{
				throw new InvalidOperationException("The second city's claim projections are incomplete.");
			}
			Projection = KingdomFoundingProjection.Claim;
			if (system.NonSeatSettlementCount < 1 || system.SettlementCount < 2 ||
				system.SettlementCount > KingdomSettlement.MaxSettlements ||
				system.SettlementName != Basin.PendingName)
			{
				throw new InvalidOperationException("The second city is not seated exactly once.");
			}
			Projection = KingdomFoundingProjection.Seat;

			if (!EnsureAbility(Actor))
			{
				throw new InvalidOperationException("The Charter ability could not be verified.");
			}
			Projection = KingdomFoundingProjection.Ability;
			if (!EnsurePlacement(system, Site, Basin.PendingRiteX, Basin.PendingRiteY))
			{
				throw new InvalidOperationException("The second city's rite ground could not be placed exactly.");
			}
			Projection = KingdomFoundingProjection.Placement;

			string chronicleEvent = Basin.PendingChronicleEventID;
			string storedEvent = Site.GetZoneProperty(SecondChronicleProperty, null);
			if (string.IsNullOrEmpty(storedEvent))
			{
				Site.SetZoneProperty(SecondChronicleProperty, chronicleEvent);
				Site.SetZoneProperty(SecondChronicleStageProperty, "0");
				Site.SetZoneProperty(SecondChronicleDispositionProperty,
					((int)KingdomChronicleDisposition.None).ToString());
				storedEvent = Site.GetZoneProperty(SecondChronicleProperty, null);
			}
			if (storedEvent != chronicleEvent)
			{
				throw new InvalidOperationException(
					"The second-founding chronicle belongs to another transaction.");
			}
			int ChronicleStage()
			{
				string raw = Site.GetZoneProperty(SecondChronicleStageProperty, null);
				if (!int.TryParse(raw, out var stage) || stage < 0 || stage > 2)
				{
					throw new InvalidOperationException(
						"The second-founding chronicle stage is malformed.");
				}
				return stage;
			}
			int restored = 0;
			string restoredRaw = Site.GetZoneProperty(SecondRestoredProperty, null);
			if (string.IsNullOrEmpty(restoredRaw))
			{
				bool isRuin = KingdomRules.IsRuinSite(system.FoundingTerrainBlueprint);
				if (isRuin && !KingdomFounding.TryRestoreRuinStructures(Site,
					Basin.PendingTransactionID, out restored))
				{
					throw new InvalidOperationException(
						"The ruin-restoration object receipts could not settle exactly.");
				}
				if (!isRuin) restored = 0;
				Site.SetZoneProperty(SecondRestoredProperty, restored.ToString());
			}
			else if (!int.TryParse(restoredRaw, out restored) || restored < 0)
			{
				throw new InvalidOperationException(
					"The second-founding restoration count is malformed.");
			}
			bool ruin = KingdomRules.IsRuinSite(system.FoundingTerrainBlueprint);
			string verb = ruin ? "reclaimed" : "founded";
			RecordChronicleOnce(system, chronicleEvent, "you poured again on " +
				KingdomFounding.StyleGroundClause(system.Style) + ", and " +
				KingdomPresentation.Rich(Basin.PendingName) + " was " + verb + " as " +
				KingdomSettlement.VocationClause(system.Vocation) + ", the second city of " +
				KingdomPresentation.Rich(system.KingdomDisplayName) +
				KingdomRules.RuinRestorationClause(restored),
				Accomplishment: true, MuralText: null,
				ReadStage: ChronicleStage,
				WriteStage: stage => Site.SetZoneProperty(
					SecondChronicleStageProperty, stage.ToString()),
				ReadDisposition: () => Site.HasZoneProperty(
					SecondChronicleDispositionProperty)
					? (int?)int.Parse(Site.GetZoneProperty(
						SecondChronicleDispositionProperty, null))
					: null,
				WriteDisposition: disposition => Site.SetZoneProperty(
					SecondChronicleDispositionProperty, disposition.ToString()),
				ValidateAuthority: () => FoundingAuthorityStillExact(
					Basin.PendingAuthority, Site));
			if (ChronicleStage() != 2)
			{
				throw new InvalidOperationException(
					"The second-founding chronicle outbox remains incomplete.");
			}
			Basin.PendingChronicleDisposition =
				(KingdomChronicleDisposition)int.Parse(Site.GetZoneProperty(
					SecondChronicleDispositionProperty, null));
			Basin.PendingChronicleStage = 2;
			Basin.PendingChronicleRecorded = true;
			if (Basin.PendingChronicleStage != 2 ||
				!ChronicleAccomplishmentObserved(chronicleEvent,
					Basin.PendingChronicleDisposition))
				{
					throw new InvalidOperationException(
						"The basin did not retain its chronicle completion stage.");
				}
			string sealFailure;
			if (!KingdomSeal.TryStageSemanticSnapshot("second founding", out sealFailure))
			{
				throw new InvalidOperationException("The second-city seal remains pending: " + sealFailure);
			}
			Projection = KingdomFoundingProjection.Seal;
		}

	}
}
