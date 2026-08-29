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
		private static void PublishFirst(r_FounderBasin Basin, GameObject Actor, Zone Site,
			ref KingdomFoundingProjection Projection)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!CommitExternalBinding(Basin, Site, out string externalFailure))
			{
				throw new InvalidOperationException(
					"External ownership changed before realm publication: " + externalFailure);
			}
			Faction faction = KingdomFounding.Found(Basin.PendingName, Site,
				Site.GetCell(Basin.PendingRiteX, Basin.PendingRiteY),
				Basin.PendingTransactionID, Basin.PendingAuthority);
			if (faction == null || !system.Founded ||
				system.KingdomFactionName != Basin.PendingRealmFaction ||
				faction.GetIntProperty("PlayerKingdom") != 1 ||
				faction.GetStringProperty(PendingFactionTransactionProperty, null) !=
					Basin.PendingTransactionID ||
				faction.GetStringProperty(PendingFactionAuthorityProperty, null) !=
					Basin.PendingAuthority || faction.GetIntProperty("Village") != 1 ||
				!FactionRegistryCoherent(Basin.PendingRealmFaction, faction) ||
				!FoundingAuthorityStillExact(Basin.PendingAuthority, Site))
			{
				throw new InvalidOperationException("The realm identity could not be published exactly.");
			}
			Basin.PendingChronicleDisposition =
				KingdomFounding.FirstChronicleDisposition(faction);
			if (!ChronicleAccomplishmentObserved(Basin.PendingChronicleEventID,
				Basin.PendingChronicleDisposition))
			{
				throw new InvalidOperationException(
					"The first-founding chronicle disposition is not terminal.");
			}
			Basin.PendingChronicleStage = 2;
			Basin.PendingChronicleRecorded = true;
			Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;
			Projection = KingdomFoundingProjection.Identity;

			if (!KingdomFounding.ClaimZone(Site, Force: false, StageSnapshot: false,
				Authority: Basin.PendingAuthority) ||
				!system.ClaimedZones.Contains(Site.ZoneID) ||
				Site.GetZoneProperty("faction", null) != system.KingdomFactionName ||
				!faction.HolyPlaces.Contains(Site.ZoneID))
			{
				throw new InvalidOperationException("The founding ground did not retain every claim projection.");
			}
			string tradeIdentityFailure = null;
			if (!system.FirstIdentityMatches(Basin.PendingTransactionID, Site.ZoneID) ||
				!system.TryBindTradeIdentity(out tradeIdentityFailure))
			{
				throw new InvalidOperationException("The exact founding identity could not bind Trade: " +
					tradeIdentityFailure);
			}
			Projection = KingdomFoundingProjection.Claim;
			if (system.NonSeatSettlementCount != 0 || system.SettlementCount != 1 ||
				system.SettlementName != Basin.PendingName)
			{
				throw new InvalidOperationException("The first settlement is not the realm's exact seat.");
			}
			Projection = KingdomFoundingProjection.Seat;
			if (!KingdomPolityRuntime.TryEnsureFoundation(system, faction,
				The.Game.TimeTicks, out string polityFailure))
			{
				throw new InvalidOperationException(
					"The realm's polity authority could not publish: " + polityFailure);
			}

			if (!EnsureAbility(Actor))
			{
				throw new InvalidOperationException("The Charter ability could not be projected onto the founder.");
			}
			Projection = KingdomFoundingProjection.Ability;
			if (!EnsurePlacement(system, Site, Basin.PendingRiteX, Basin.PendingRiteY))
			{
				throw new InvalidOperationException("The rite ground and surveyed heart could not be placed exactly.");
			}
			Projection = KingdomFoundingProjection.Placement;
			string failure;
			if (!KingdomSeal.TryFoundingCompleted(out failure))
			{
				throw new InvalidOperationException("The founding seal remains pending: " + failure);
			}
			faction.SetProperty(PendingFactionProperty, 0);
			Projection = KingdomFoundingProjection.Seal;
		}

	}
}
