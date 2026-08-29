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
		private static bool TryFreezeSecondIdentity(r_FounderBasin Basin,
			KingdomSystem System, Zone Site, out string SettlementId, out string Failure)
		{
			SettlementId = null;
			Failure = null;
			if (Basin == null || System == null || Site == null ||
				!SiteReservationMatches(Site, Basin.PendingAuthority) ||
				!System.TryPrepareLaterSettlementIdentity(Basin.PendingTransactionID,
					Site.ZoneID, out SettlementId, out Failure)) return false;
			string transaction = Site.GetZoneProperty(
				SecondIdentityTransactionProperty, null);
			string realm = Site.GetZoneProperty(SecondIdentityRealmProperty, null);
			string settlement = Site.GetZoneProperty(
				SecondIdentitySettlementProperty, null);
			string version = Site.GetZoneProperty(SecondIdentityVersionProperty, null);
			string origin = Site.GetZoneProperty(SecondIdentityOriginProperty, null);
			if ((transaction != null && transaction != Basin.PendingTransactionID) ||
				(realm != null && realm != System.RealmId) ||
				(settlement != null && settlement != SettlementId) ||
				(version != null && version != KingdomIdentityRules.RulesVersion.ToString()) ||
				(origin != null && origin !=
					((int)KingdomIdentityOrigin.FoundingTransaction).ToString()))
			{
				Failure = "the site carries a third-value immutable identity field";
				return false;
			}
			if (!System.TryPrepareSecondCityTopology(SettlementId,
				out KingdomSecondCityTopologyPlan topologyPlan, out Failure)) return false;
			try
			{
				Site.SetZoneProperty(SecondIdentityTransactionProperty,
					Basin.PendingTransactionID);
				if (!System.TryPrepareLaterSettlementIdentity(Basin.PendingTransactionID,
					Site.ZoneID, out SettlementId, out Failure)) return false;
				Site.SetZoneProperty(SecondIdentityRealmProperty, System.RealmId);
				if (!System.TryPrepareLaterSettlementIdentity(Basin.PendingTransactionID,
					Site.ZoneID, out SettlementId, out Failure)) return false;
				Site.SetZoneProperty(SecondIdentityVersionProperty,
					KingdomIdentityRules.RulesVersion.ToString());
				Site.SetZoneProperty(SecondIdentityOriginProperty,
					((int)KingdomIdentityOrigin.FoundingTransaction).ToString());
				Site.SetZoneProperty(SecondIdentitySettlementProperty, SettlementId);
			}
			catch (Exception ex)
			{
				Failure = "site identity callback failed: " + Describe(ex);
				return false;
			}
			if (Site.GetZoneProperty(SecondIdentityTransactionProperty, null) !=
					Basin.PendingTransactionID ||
				Site.GetZoneProperty(SecondIdentityRealmProperty, null) != System.RealmId ||
				Site.GetZoneProperty(SecondIdentitySettlementProperty, null) != SettlementId ||
				Site.GetZoneProperty(SecondIdentityVersionProperty, null) !=
					KingdomIdentityRules.RulesVersion.ToString() ||
				Site.GetZoneProperty(SecondIdentityOriginProperty, null) !=
					((int)KingdomIdentityOrigin.FoundingTransaction).ToString() ||
				!System.TryStagePendingSettlementIdentity(SettlementId,
					Basin.PendingTransactionID, Site.ZoneID, Basin.PendingAuthority,
					out Failure))
			{
				System.TryAbortPendingSettlementIdentity(Basin.PendingTransactionID,
					Site.ZoneID, Basin.PendingAuthority, out string ignoredAbortFailure);
				if (string.IsNullOrEmpty(Failure))
					Failure = "site identity readback or pending topology staging failed";
				return false;
			}
			// Paired expansion is irreversible. Publish durable redo barrier first; any later
			// failure retains authenticated site+system tuple for forward recovery.
			Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;
			if (!System.TryCommitSecondCityTopology(topologyPlan,
				Basin.PendingTransactionID, Site.ZoneID, Basin.PendingAuthority,
				out Failure)) return false;
			return SiteReservationMatches(Site, Basin.PendingAuthority) &&
				System.TryPrepareLaterSettlementIdentity(Basin.PendingTransactionID,
					Site.ZoneID, out string reproved, out Failure) && reproved == SettlementId;
		}

		private static void PublishVillageCharter(r_FounderBasin Basin, Zone Site,
			ref KingdomFoundingProjection Projection)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Faction village = Factions.GetIfExists(Basin.PendingVillageFaction);
			if (string.IsNullOrEmpty(Basin.PendingVillageFaction) || !system.Founded ||
				!FactionRegistryCoherent(Basin.PendingVillageFaction, village) ||
				village.GetIntProperty("Village") != 1 ||
				village.DisplayName != Basin.PendingVillageDisplayName ||
				village.GetStringProperty(VillageReservationProperty, null) !=
					Basin.PendingAuthority ||
				!SiteReservationMatches(Site, Basin.PendingAuthority) ||
				Site.GetZoneProperty("faction", null) != Basin.PendingVillageFaction)
			{
				throw new InvalidOperationException("The village covenant no longer names this ground.");
			}
			EnsureVillageStandingEffectApplied(Basin, Site, system);
			Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;
			Projection = KingdomFoundingProjection.Identity;
			RecordChronicleOnce(system, Basin.PendingChronicleEventID,
				"you asked, and " +
				KingdomPresentation.Rich(Basin.PendingVillageDisplayName ??
					Basin.PendingVillageFaction) +
				" agreed: their ground stays theirs, and a covenant now stands between them and " +
				KingdomPresentation.Rich(system.KingdomDisplayName),
				Accomplishment: true, MuralText: null,
				ReadStage: () => Basin.PendingChronicleStage,
				WriteStage: stage => Basin.PendingChronicleStage = stage,
				ReadDisposition: () => Basin.TryReadRawChronicleDisposition(out var raw)
					? (int?)raw : null,
				WriteDisposition: disposition => Basin.PendingChronicleDisposition =
					(KingdomChronicleDisposition)disposition,
				ValidateAuthority: () => FoundingAuthorityStillExact(
					Basin.PendingAuthority, Site));
			Basin.PendingChronicleRecorded = Basin.PendingChronicleStage == 2;
			if (!Basin.PendingChronicleRecorded ||
				!ChronicleAccomplishmentObserved(Basin.PendingChronicleEventID,
					Basin.PendingChronicleDisposition))
			{
				throw new InvalidOperationException("The village chronicle outbox remains incomplete.");
			}
			// The durable cut, and its position is the whole of its argument. The covenant's
			// standing is exact and its chronicle entry is terminal, so there is something true to
			// record; the seal, the completion and the reservation cleanup have not run, so the
			// receipt that paid for this is still on the basin. A failure here therefore retains
			// that receipt for forward recovery instead of leaving a sealed covenant with nothing
			// written down.
			string covenantFailure = "the exact site reservation and its tick are unreadable";
			int sealedStanding = Basin.PendingVillageEffectState ==
				KingdomFoundingTransactionRules.VillageStandingEffectApplied
				? Basin.PendingVillageEffectAfter
				: system.GetRegardForRealm(Basin.PendingVillageFaction); // legacy archived retry only
			if (!TryReadSiteReservation(Site, out _, out _, out _, out _, out _,
					out long reservationTick) ||
				!KingdomVillageCovenantRuntime.TryRecord(system, Basin.PendingTransactionID,
					Basin.PendingAuthority, Basin.PendingVillageFaction,
					Basin.PendingVillageDisplayName, Site.ZoneID, Basin.PendingChronicleEventID,
					sealedStanding, reservationTick,
					out covenantFailure))
			{
				throw new InvalidOperationException(
					"The village covenant was not durably archived: " + covenantFailure);
			}
			string failure;
			if (!KingdomSeal.TryStageSemanticSnapshot("village charter", out failure))
			{
				throw new InvalidOperationException("The village covenant seal remains pending: " + failure);
			}
			Projection = KingdomFoundingProjection.Seal;
		}

		private static bool DetectPublication(r_FounderBasin Basin, Zone Site)
		{
			if (Basin == null || Site == null)
			{
				return false;
			}
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null)
			{
				return false;
			}
			switch (Basin.PendingKind)
			{
			case KingdomFoundingKind.FirstCity:
				Faction pendingFaction = Factions.GetIfExists(Basin.PendingRealmFaction);
				return system.FirstIdentityMatches(Basin.PendingTransactionID, Site.ZoneID) ||
					(pendingFaction != null &&
					pendingFaction.GetIntProperty("PlayerKingdom") == 1 &&
					pendingFaction.GetIntProperty("Village") == 1 &&
					pendingFaction.GetStringProperty(PendingFactionTransactionProperty, null) ==
						Basin.PendingTransactionID &&
					pendingFaction.GetStringProperty(PendingFactionAuthorityProperty, null) ==
						Basin.PendingAuthority &&
					((system.Founded && system.KingdomFactionName ==
						Basin.PendingRealmFaction) ||
					 pendingFaction.GetIntProperty(PendingFactionProperty) == 1));
			case KingdomFoundingKind.SecondCity:
				return SiteReservationMatches(Site, Basin.PendingAuthority) &&
					PublishedSecondAuthorityMatches(Site, Basin.PendingAuthority);
			case KingdomFoundingKind.VillageCharter:
				return VillageStandingEffectProvesPublication(Basin, Site, system);
			default:
				return false;
			}
		}

	}
}
