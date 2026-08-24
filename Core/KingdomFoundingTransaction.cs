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
	/// <summary>
	/// Live, same-basin founding transaction. The receipt is serialized on the basin part; engine
	/// projections are idempotent and verified before success is returned. Before an irreversible
	/// publication, failure restores the exact liquid snapshot. Afterwards it retains one paid
	/// receipt and a named recovery point instead of lying that an engine publication was undone.
	/// </summary>
	public static class KingdomFoundingTransaction
	{
		private const string PendingFactionProperty = "TAFFoundingPending";
		internal const string PendingFactionTransactionProperty = "TAFFoundingTransaction";
		internal const string PendingFactionAuthorityProperty = "TAFFoundingAuthority";
		internal const string RealmReservationProperty = "TAFFoundingRealmReservation";
		internal const string VillageReservationProperty = "TAFFoundingVillageReservation";
		internal const string GlobalReservationState = "r_TAF_FoundingGlobalReservation_v1";
		internal const string SiteReservationProperty = "r_TAF_FoundingSiteAuthority_v1";
		private const string SiteReservationNameProperty = "r_TAF_FoundingSiteName_v1";
		private const string SiteReservationVocationProperty = "r_TAF_FoundingSiteVocation_v1";
		private const string SiteReservationVillageProperty = "r_TAF_FoundingSiteVillage_v1";
		private const string SiteReservationDisplayProperty = "r_TAF_FoundingSiteDisplay_v1";
		private const string SiteReservationTickProperty = "r_TAF_FoundingSiteTick_v1";
		private const string SecondChronicleProperty = "r_TAF_SecondFoundingChronicle";
		private const string SecondChronicleStageProperty = "r_TAF_SecondFoundingChronicleStage";
		private const string SecondChronicleDispositionProperty =
			"r_TAF_SecondFoundingChronicleDisposition_v1";
		private const string SecondRestoredProperty = "r_TAF_SecondFoundingRestored_v1";
		private const string SecondPublicationAuthorityProperty =
			"r_TAF_SecondFoundingPublicationAuthority_v1";
		private const string SecondIdentityTransactionProperty =
			"r_TAF_SecondFoundingIdentityTransaction_v1";
		private const string SecondIdentityRealmProperty =
			"r_TAF_SecondFoundingIdentityRealm_v1";
		private const string SecondIdentitySettlementProperty =
			"r_TAF_SecondFoundingIdentitySettlement_v1";
		private const string SecondIdentityVersionProperty =
			"r_TAF_SecondFoundingIdentityVersion_v1";
		private const string SecondIdentityOriginProperty =
			"r_TAF_SecondFoundingIdentityOrigin_v1";
		internal const string ClaimChronicleEventProperty = "r_TAF_ClaimChronicleEvent_v1";
		internal const string ClaimChronicleStageProperty = "r_TAF_ClaimChronicleStage_v1";
		internal const string ClaimChronicleDispositionProperty =
			"r_TAF_ClaimChronicleDisposition_v1";
		internal const string ClaimFoundingProperty = "r_TAF_ClaimWasFounding_v1";
		private const string DirectRecoveryNameProperty = "r_TAF_SecondFoundingRecoveryName";
		private const string DirectRecoveryVocationProperty = "r_TAF_SecondFoundingRecoveryVocation";
		private const string DirectRecoveryRiteXProperty = "r_TAF_SecondFoundingRecoveryRiteX";
		private const string DirectRecoveryRiteYProperty = "r_TAF_SecondFoundingRecoveryRiteY";
		private const string DirectRecoveryTickProperty = "r_TAF_SecondFoundingRecoveryTick";
		private const string DirectRecoveryRealmProperty = "r_TAF_SecondFoundingRecoveryRealm";
		private const string DirectRecoveryTransactionProperty = "r_TAF_SecondFoundingRecoveryTransaction";
		private static readonly FieldInfo FactionListField = typeof(Factions).GetField(
			"FactionList", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly object InFlightSync = new object();
		private static FoundingLease InFlight;

		/// <summary>Process-local guard for synchronous engine callbacks. Founding is single-threaded,
		/// but JournalAPI invokes listeners before it returns. A nested route must see the guard before
		/// RequireSystem, reservation, receipt, liquid, faction, or journal mutation.</summary>
		private sealed class FoundingLease : IDisposable
		{
			internal string Authority;
			internal r_FounderBasin Basin;

			internal bool Bind(string Value, r_FounderBasin Receipt)
			{
				if (string.IsNullOrEmpty(Value) ||
					!KingdomFoundingTransactionRules.TryParseAuthority(Value, out var parsed))
				{
					return false;
				}
				lock (InFlightSync)
				{
					if (!ReferenceEquals(InFlight, this) ||
						(!string.IsNullOrEmpty(Authority) && Authority != Value))
					{
						return false;
					}
					Authority = Value;
					Basin = Receipt;
					return true;
				}
			}

			public void Dispose()
			{
				lock (InFlightSync)
				{
					if (ReferenceEquals(InFlight, this))
					{
						InFlight = null;
					}
				}
			}
		}

		private static bool TryEnterFounding(string AuthorityHint, r_FounderBasin Basin,
			out FoundingLease Lease)
		{
			lock (InFlightSync)
			{
				if (InFlight != null)
				{
					Lease = null;
					return false;
				}
				Lease = new FoundingLease { Authority = AuthorityHint, Basin = Basin };
				InFlight = Lease;
				return true;
			}
		}

		private static KingdomFoundingResult ReentryRefusal()
		{
			return Result(KingdomFoundingOutcome.Refused,
				KingdomFoundingWaterDisposition.Untouched,
				KingdomFoundingProjection.None,
				"Another founding callback is already in flight; this nested attempt changed nothing.");
		}

		internal static bool AuthorityIsSynchronouslyInFlight(string Authority,
			r_FounderBasin Basin = null)
		{
			lock (InFlightSync)
			{
				return InFlight != null && !string.IsNullOrEmpty(Authority) &&
					InFlight.Authority == Authority &&
					(Basin == null || ReferenceEquals(InFlight.Basin, Basin));
			}
		}

		/// <summary>
		/// Makes the debug reset safe to run. Paid basin authority is never discarded: reset
		/// refuses before mutation while one is in flight or retained anywhere it can own the
		/// current transaction. Stable realm-owned claim/direct markers on the current zone are
		/// removed exactly and verified so a later debug founding can use that ground again.
		/// </summary>
		internal static bool TryPrepareDebugReset(KingdomSystem System, GameObject Actor,
			Zone Site, out string Failure)
		{
			Failure = "";
			if (System == null || Actor == null)
			{
				Failure = "The founder or kingdom system is unavailable.";
				return false;
			}
			lock (InFlightSync)
			{
				if (InFlight != null)
				{
					Failure = "A founding callback is in flight.";
					return false;
				}
			}
			if (ObjectTreeHasPaidReceipt(Actor))
			{
				Failure = "A founder's basin retains a paid or staged founding receipt.";
				return false;
			}
			if (Site != null)
			{
				foreach (GameObject root in Site.GetObjects())
				{
					if (!ReferenceEquals(root, Actor) && ObjectTreeHasPaidReceipt(root))
					{
						Failure = "A basin on this ground retains a paid or staged founding receipt.";
						return false;
					}
				}
			}

			HashSet<string> realms = new HashSet<string>(StringComparer.Ordinal);
			if (!string.IsNullOrEmpty(System.KingdomFactionName))
			{
				realms.Add(System.KingdomFactionName);
			}
			if (!string.IsNullOrEmpty(System.ExiledFactionName))
			{
				realms.Add(System.ExiledFactionName);
			}
			bool realmListsSite = Site != null &&
				(System.ClaimedZones.Contains(Site.ZoneID) ||
				 (System.Away != null && System.Away.ClaimedZones.Contains(Site.ZoneID)) ||
				 (System.ExiledSeat != null &&
				  System.ExiledSeat.ClaimedZones.Contains(Site.ZoneID)) ||
				 (System.ExiledAway != null &&
				  System.ExiledAway.ClaimedZones.Contains(Site.ZoneID)));
			string zoneFaction = Site?.GetZoneProperty("faction", null);
			if (realmListsSite && !string.IsNullOrEmpty(zoneFaction) &&
				!realms.Contains(zoneFaction))
			{
				Failure = "The current zone carries a foreign faction claim.";
				return false;
			}
			bool realmOwnsSite = Site != null &&
				(realmListsSite || (!string.IsNullOrEmpty(zoneFaction) &&
				 realms.Contains(zoneFaction)));

			string liveAuthority = null;
			string global = The.Game?.GetStringGameState(GlobalReservationState, null);
			if (!string.IsNullOrEmpty(global) &&
				!AcceptDirectResetAuthority(global, realms, Site,
					ref liveAuthority, out Failure))
			{
				return false;
			}
			foreach (string realmName in realms)
			{
				Faction realm = Factions.GetIfExists(realmName);
				string bound = realm?.GetStringProperty(RealmReservationProperty, null);
				if (!string.IsNullOrEmpty(bound) &&
					!AcceptDirectResetAuthority(bound, realms, Site,
						ref liveAuthority, out Failure))
				{
					return false;
				}
				if (realm != null && realm.GetIntProperty(PendingFactionProperty) == 1)
				{
					string pending = realm.GetStringProperty(
						PendingFactionAuthorityProperty, null);
					if (!AcceptDirectResetAuthority(pending, realms, Site,
							ref liveAuthority, out Failure) ||
						realm.GetStringProperty(PendingFactionTransactionProperty, null) !=
							ParseTransaction(pending))
					{
						Failure = "The realm faction retains a paid or malformed pending founding.";
						return false;
					}
				}
			}

			bool clearLegacyDirect = false;
			if (Site != null && HasSiteReservation(Site))
			{
				string siteAuthority = Site.GetZoneProperty(SiteReservationProperty, null);
				if (!string.IsNullOrEmpty(siteAuthority))
				{
					if (!TryReadSiteReservation(Site, out var parsedSite,
						out var _, out var _, out var _, out var _, out var _) ||
						!AcceptDirectResetAuthority(siteAuthority, realms, Site,
							ref liveAuthority, out Failure))
					{
						Failure = "The current zone retains a paid, foreign, or malformed founding reservation.";
						return false;
					}
				}
				else if (!realmOwnsSite || !LegacyDirectResetMarkersAreExact(
					Site, realms))
				{
					Failure = "The current zone carries partial or foreign legacy founding markers.";
					return false;
				}
				else
				{
					clearLegacyDirect = true;
				}
			}

			string published = Site?.GetZoneProperty(
				SecondPublicationAuthorityProperty, null);
			if (!string.IsNullOrEmpty(published) &&
				(!realmOwnsSite ||
				 !KingdomFoundingTransactionRules.TryParseAuthority(published,
					out var publishedAuthority) ||
				 publishedAuthority.Kind != KingdomFoundingKind.SecondCity ||
				 publishedAuthority.ZoneID != Site.ZoneID ||
				 !realms.Contains(publishedAuthority.RealmFaction)))
			{
				Failure = "The current zone carries a foreign or malformed second-founding publication.";
				return false;
			}

			// Everything above is read-only. From here on no paid or foreign authority is touched.
			if (!string.IsNullOrEmpty(liveAuthority))
			{
				if (Site != null && SiteReservationMatches(Site, liveAuthority) &&
					!ReleaseSiteReservation(Site, liveAuthority))
				{
					Failure = "The exact direct site reservation could not be cleared.";
					return false;
				}
				if (!KingdomFoundingTransactionRules.TryParseAuthority(liveAuthority,
						out var parsedLive) ||
					!ReleaseGlobalReservation(liveAuthority,
						parsedLive.RealmFaction, null))
				{
					Failure = "The exact direct global/faction reservation could not be cleared.";
					return false;
				}
			}
			if (clearLegacyDirect)
			{
				ClearLegacyDirectRecovery(Site);
			}
			if (Site != null && HasSiteReservation(Site))
			{
				Failure = "A site founding marker remains after exact cleanup.";
				return false;
			}

			if (realmOwnsSite)
			{
				Site.RemoveZoneProperty("faction");
				Site.RemoveZoneProperty(ClaimChronicleEventProperty);
				Site.RemoveZoneProperty(ClaimChronicleStageProperty);
				Site.RemoveZoneProperty(ClaimChronicleDispositionProperty);
				Site.RemoveZoneProperty(ClaimFoundingProperty);
				Site.RemoveZoneProperty(SecondChronicleProperty);
				Site.RemoveZoneProperty(SecondChronicleStageProperty);
				Site.RemoveZoneProperty(SecondChronicleDispositionProperty);
				Site.RemoveZoneProperty(SecondRestoredProperty);
				Site.RemoveZoneProperty(SecondPublicationAuthorityProperty);
			}
			foreach (string realmName in realms)
			{
				Faction realm = Factions.GetIfExists(realmName);
				if (realm != null)
				{
					realm.HolyPlaces.Remove(Site?.ZoneID);
					if (!KingdomFounding.ClearDebugFoundingMarkers(realm))
					{
						Failure = "The exact realm founding markers could not be cleared.";
						return false;
					}
					if (!string.IsNullOrEmpty(realm.GetStringProperty(
						RealmReservationProperty, null)))
					{
						Failure = "A realm faction reservation remains after exact cleanup.";
						return false;
					}
				}
			}
			if (!string.IsNullOrEmpty(The.Game?.GetStringGameState(
					GlobalReservationState, null)) ||
				(Site != null && HasSiteReservation(Site)) ||
				(realmOwnsSite && (!string.IsNullOrEmpty(
					Site.GetZoneProperty("faction", null)) ||
					Site.HasZoneProperty(ClaimChronicleEventProperty) ||
					Site.HasZoneProperty(ClaimChronicleStageProperty) ||
					Site.HasZoneProperty(ClaimChronicleDispositionProperty) ||
					Site.HasZoneProperty(ClaimFoundingProperty) ||
					Site.HasZoneProperty(SecondPublicationAuthorityProperty))))
			{
				Failure = "Founding or claim cleanup did not retain an empty exact state.";
				return false;
			}
			return true;
		}

		private static bool ObjectTreeHasPaidReceipt(GameObject Root)
		{
			if (Root == null)
			{
				return false;
			}
			r_FounderBasin basin = Root.GetPart<r_FounderBasin>();
			if (basin != null && basin.HasPendingRite)
			{
				return true;
			}
			foreach (GameObject item in Root.GetContents(new List<GameObject>()))
			{
				basin = item.GetPart<r_FounderBasin>();
				if (basin != null && basin.HasPendingRite)
				{
					return true;
				}
			}
			return false;
		}

		private static bool AcceptDirectResetAuthority(string Raw,
			HashSet<string> Realms, Zone Site, ref string Expected, out string Failure)
		{
			Failure = "";
			if (string.IsNullOrEmpty(Raw) || Site == null ||
				!KingdomFoundingTransactionRules.TryParseAuthority(Raw, out var parsed) ||
				parsed.OwnerKind != KingdomFoundingOwnerKind.Direct ||
				(parsed.Kind != KingdomFoundingKind.FirstCity &&
				 parsed.Kind != KingdomFoundingKind.SecondCity) ||
				parsed.ZoneID != Site.ZoneID || !Realms.Contains(parsed.RealmFaction))
			{
				Failure = "A paid, foreign, or malformed founding authority is still reserved.";
				return false;
			}
			if (!string.IsNullOrEmpty(Expected) && Expected != Raw)
			{
				Failure = "More than one direct founding authority is reserved.";
				return false;
			}
			Expected = Raw;
			return true;
		}

		private static string ParseTransaction(string Authority)
		{
			return KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed)
				? parsed.TransactionID : null;
		}

		private static bool LegacyDirectResetMarkersAreExact(Zone Site,
			HashSet<string> Realms)
		{
			return Site != null &&
				!string.IsNullOrEmpty(Site.GetZoneProperty(
					DirectRecoveryNameProperty, null)) &&
				KingdomSettlement.IsKnownVocation(Site.GetZoneProperty(
					DirectRecoveryVocationProperty, null)) &&
				int.TryParse(Site.GetZoneProperty(DirectRecoveryRiteXProperty, null),
					out var riteX) && riteX >= 0 && riteX < Site.Width &&
				int.TryParse(Site.GetZoneProperty(DirectRecoveryRiteYProperty, null),
					out var riteY) && riteY >= 0 && riteY < Site.Height &&
				long.TryParse(Site.GetZoneProperty(DirectRecoveryTickProperty, null),
					out var tick) && tick >= 0L &&
				Realms.Contains(Site.GetZoneProperty(DirectRecoveryRealmProperty, null)) &&
				!string.IsNullOrEmpty(Site.GetZoneProperty(
					DirectRecoveryTransactionProperty, null));
		}

		internal static bool FoundingAuthorityStillExact(string Authority, Zone Site)
		{
			r_FounderBasin basin;
			lock (InFlightSync)
			{
				if (InFlight == null || InFlight.Authority != Authority || InFlight.Basin == null)
				{
					return false;
				}
				basin = InFlight.Basin;
			}
			if (!KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed) ||
				Site == null || Site.ZoneID != parsed.ZoneID ||
				basin.PendingAuthority != Authority || basin.PendingKind != parsed.Kind ||
				basin.PendingTransactionID != parsed.TransactionID ||
				basin.PendingOwnerKind != parsed.OwnerKind ||
				basin.PendingOwnerNonce != parsed.OwnerNonce ||
				basin.PendingRealmFaction != parsed.RealmFaction ||
				basin.PendingZoneID != parsed.ZoneID || basin.PendingRiteX != parsed.RiteX ||
				basin.PendingRiteY != parsed.RiteY ||
				basin.PendingPayloadDigest != parsed.PayloadDigest ||
				!GlobalReservationMatches(Authority) || !SiteReservationMatches(Site, Authority))
			{
				return false;
			}
			if (!FoundingReceiptStillExact(basin, parsed))
			{
				return false;
			}
			Faction realm = Factions.GetIfExists(parsed.RealmFaction);
			if (realm == null || realm.GetStringProperty(RealmReservationProperty, null) != Authority)
			{
				return false;
			}
			if (parsed.Kind == KingdomFoundingKind.VillageCharter)
			{
				Faction village = Factions.GetIfExists(basin.PendingVillageFaction);
				return village != null && village.GetStringProperty(
					VillageReservationProperty, null) == Authority;
			}
			return true;
		}

		private static bool FoundingReceiptStillExact(r_FounderBasin Basin,
			KingdomFoundingAuthority Authority)
		{
			if (Basin == null ||
				(Basin.PendingPhase != KingdomFoundingPhase.WaterCommitted &&
				 Basin.PendingPhase != KingdomFoundingPhase.PublicationCommitted) ||
				Basin.PendingChronicleEventID != FoundingEventID(Authority.Kind,
					Authority.TransactionID, "chronicle"))
			{
				return false;
			}
			if (Authority.OwnerKind == KingdomFoundingOwnerKind.Direct)
			{
				return Basin.ParentObject == null && Authority.PayloadDigest ==
					DirectPayloadDigest(Authority.Kind, Basin.PendingName,
						Basin.PendingVocation, Basin.PendingVillageFaction,
						Basin.PendingVillageDisplayName);
			}
			if (Authority.OwnerKind != KingdomFoundingOwnerKind.Basin ||
				Basin.ParentObject == null || !Basin.HasCompleteReceiptSchema ||
				Basin.PendingBasinID != Basin.ParentObject.ID ||
				!Basin.TryGetOriginalComponents(out var original, out var originalEncoded) ||
				!Basin.TryGetCommittedComponents(out var committed, out var committedEncoded) ||
				EncodeComponents(original) != originalEncoded ||
				EncodeComponents(committed) != committedEncoded ||
				!KingdomFoundingTransactionRules.WaterAlgebraValid(
					Basin.PendingOriginalVolume, Basin.PendingOriginalMaxVolume,
					Basin.PendingCommittedVolume, Basin.PendingCommittedMaxVolume,
					KingdomRules.FoundingCostDrams,
					KingdomFoundingTransactionRules.ComponentsDescribePureWater(
						original, Basin.PendingOriginalVolume),
					KingdomFoundingTransactionRules.ComponentsDescribePureWater(
						committed, Basin.PendingCommittedVolume)) ||
				!CommittedSnapshotStillExact(Basin,
					Basin.ParentObject.GetPart<LiquidVolume>()))
			{
				return false;
			}
			return Authority.PayloadDigest == KingdomFoundingTransactionRules.PayloadDigest(
				Authority.Kind, Basin.PendingName, Basin.PendingVocation,
				Basin.PendingVillageFaction, Basin.PendingVillageDisplayName,
				Basin.PendingOriginalVolume, Basin.PendingOriginalMaxVolume,
				Basin.PendingCommittedVolume, Basin.PendingCommittedMaxVolume,
				originalEncoded, committedEncoded);
		}

		/// <summary>
		/// Debug/script route for a second city. It uses the same verified, retryable projection as
		/// the basin but has no water receipt to debit. A false result may be resumed by calling the
		/// same name/site again; it never permits Force to overwrite foreign ground.
		/// </summary>
		internal static bool TryFoundSecondWithoutWater(string Name, string Vocation, Zone Site,
			bool Force, out string Failure)
		{
			Failure = "";
			if (!TryEnterFounding(null, null, out var lease))
			{
				Failure = "Another founding callback is already in flight; this nested attempt changed nothing.";
				return false;
			}
			using (lease)
			{
				return TryFoundSecondWithoutWaterCore(Name, Vocation, Site, Force,
					lease, out Failure);
			}
		}

		private static bool TryFoundSecondWithoutWaterCore(string Name, string Vocation,
			Zone Site, bool Force, FoundingLease Lease, out string Failure)
		{
			Failure = "";
			if (string.IsNullOrEmpty(Name) || Site == null)
			{
				Failure = "The second city needs a name and a site.";
				return false;
			}
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Faction realm = Factions.GetIfExists(system.KingdomFactionName);
			if (!system.Founded || !FactionRegistryCoherent(system.KingdomFactionName, realm) ||
				realm.GetIntProperty("PlayerKingdom") != 1 ||
				realm.GetIntProperty("Village") != 1)
			{
				Failure = "The realm faction is not registered coherently.";
				return false;
			}
			string vocation = KingdomSettlement.IsKnownVocation(Vocation)
				? Vocation : KingdomSettlement.NeutralVocation;
			Cell rite = The.Player?.CurrentCell;
			bool riteIsHere = rite != null && rite.ParentZone == Site;
			int riteX = riteIsHere ? rite.X : (Site.Width / 2);
			int riteY = riteIsHere ? rite.Y : (Site.Height / 2);
			KingdomFoundingAuthority authority;
			bool hasSite = HasSiteReservation(Site);
			string publishedAuthority = Site.GetZoneProperty(
				SecondPublicationAuthorityProperty, null);
			if (!string.IsNullOrEmpty(publishedAuthority) && !hasSite)
			{
				Failure = "This ground already carries a completed second-city publication.";
				return false;
			}
			if (hasSite)
			{
				if (!TryReadSiteReservation(Site, out authority, out var storedName,
					out var storedVocation, out var storedVillage, out var storedDisplay,
					out var storedTick) || authority.OwnerKind != KingdomFoundingOwnerKind.Direct ||
					authority.Kind != KingdomFoundingKind.SecondCity || storedName != Name ||
					storedVocation != vocation || !string.IsNullOrEmpty(storedVillage) ||
					!string.IsNullOrEmpty(storedDisplay) || authority.RealmFaction !=
						system.KingdomFactionName || authority.ZoneID != Site.ZoneID ||
					authority.PayloadDigest != DirectPayloadDigest(authority.Kind, storedName,
						storedVocation, null, null))
				{
					Failure = "This ground carries a quarantined or foreign founding reservation.";
					return false;
				}
				riteX = authority.RiteX;
				riteY = authority.RiteY;
			}
			else
			{
				authority = NewAuthority(KingdomFoundingKind.SecondCity,
					KingdomFoundingOwnerKind.Direct, Guid.NewGuid().ToString("N"),
					Guid.NewGuid().ToString("N"), system.KingdomFactionName, Site.ZoneID,
					riteX, riteY, DirectPayloadDigest(KingdomFoundingKind.SecondCity,
						Name, vocation, null, null));
			}
			string encodedAuthority = KingdomFoundingTransactionRules.FormatAuthority(authority);
			if (!string.IsNullOrEmpty(publishedAuthority) &&
				publishedAuthority != encodedAuthority)
			{
				Failure = "This ground carries another transaction's city publication.";
				return false;
			}
			if (encodedAuthority == null || !Lease.Bind(encodedAuthority, null))
			{
				Failure = "Another founding already holds this realm or site reservation.";
				return false;
			}
			// Site is durable recovery authority for direct founding. Publish it before broad
			// realm/global locks; an interrupted cleanup that already released those locks can
			// therefore reacquire the same exact authority instead of minting a replacement.
			if (!StageSiteReservation(Site, encodedAuthority, Name, vocation, null, null))
			{
				if (hasSite)
				{
					Failure = "The exact direct second-founding reservation is malformed and remains quarantined.";
					return false;
				}
				bool cleanedSite = ClearStagedSiteSubset(Site, encodedAuthority, Name,
					vocation, null, null);
				Failure = cleanedSite
					? "Another founding already holds this realm or site reservation."
					: "The direct second-founding reservation remains staged for exact cleanup.";
				return false;
			}
			if (!AcquireGlobalReservation(encodedAuthority,
				system.KingdomFactionName, null))
			{
				// Existing exact site is retry receipt. New site also remains exact: clearing it
				// here would recreate site-last cleanup and lose authority across callback cuts.
				Failure = "Another founding holds the realm lock; this exact site receipt can retry.";
				return false;
			}
			bool published = SecondPublished(system, Name, Site.ZoneID,
				authority.TransactionID) &&
				PublishedSecondAuthorityMatches(Site, encodedAuthority) &&
				SiteReservationMatches(Site, encodedAuthority);
			bool targetIsExactSeat = SecondIsExactSeat(system, Name, Site.ZoneID,
				authority.TransactionID);
			bool targetIsExactAway = SecondIsExactAway(system, Name, Site.ZoneID,
				authority.TransactionID);
			if (!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				system.SettlementCount, KingdomSettlement.MaxSettlements,
				system.Away == null, targetIsExactSeat, targetIsExactAway, published))
			{
				bool forwardRedo = DirectSecondHasForwardRedo(system, Site, authority,
					encodedAuthority);
				if (!forwardRedo)
				{
					if (!ClearExactReservationSet(Site, encodedAuthority,
						system.KingdomFactionName, null))
					{
						Failure = "The invalid direct second-founding reservation remains staged for exact cleanup.";
						return false;
					}
				}
				Failure = forwardRedo
					? "The realm's city seats changed, but this exact forward-recovery receipt remains."
					: "The realm's city seats no longer match this stale transaction; its exact reservations were cleared.";
				return false;
			}
			bool partialClaim = Site.GetZoneProperty("faction", null) ==
				system.KingdomFactionName || realm.HolyPlaces.Contains(Site.ZoneID);
			r_FounderBasin carrier = new r_FounderBasin
			{
				PendingKind = KingdomFoundingKind.SecondCity,
				PendingOwnerKind = KingdomFoundingOwnerKind.Direct,
				PendingPhase = partialClaim || published
					? KingdomFoundingPhase.PublicationCommitted
					: KingdomFoundingPhase.WaterCommitted,
				PendingTransactionID = authority.TransactionID,
				PendingBasinID = "direct:" + authority.TransactionID,
				PendingOwnerNonce = authority.OwnerNonce,
				PendingPayloadDigest = authority.PayloadDigest,
				PendingAuthority = encodedAuthority,
				PendingRealmFaction = system.KingdomFactionName,
				PendingName = Name,
				PendingVocation = vocation,
				PendingZoneID = Site.ZoneID,
				PendingRiteX = riteX,
				PendingRiteY = riteY,
				PendingChronicleEventID = FoundingEventID(
					authority.Kind, authority.TransactionID, "chronicle"),
				PendingChronicleDisposition = KingdomChronicleDisposition.None
			};
				if (!Lease.Bind(encodedAuthority, carrier))
				{
					bool cleared = ClearExactReservationSet(Site, encodedAuthority,
						system.KingdomFactionName, null);
					Failure = "The exact direct founding authority left its synchronous guard." +
						(cleared ? "" : " Its exact reservation remains pending cleanup.");
					return false;
			}
			try
			{
				KingdomFoundingProjection projection = KingdomFoundingProjection.Water;
				PublishSecond(carrier, The.Player, Site, ref projection, Force);
				carrier.PendingPhase = KingdomFoundingPhase.Complete;
				if (!ClearExactReservationSet(Site, encodedAuthority,
					system.KingdomFactionName, null))
				{
					throw new InvalidOperationException(
						"The completed second founding could not clear its exact reservation.");
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = Describe(ex);
				if (!DetectPublication(carrier, Site) &&
					carrier.PendingPhase != KingdomFoundingPhase.PublicationCommitted)
				{
					if (!ClearExactReservationSet(Site, encodedAuthority,
						system.KingdomFactionName, null))
					{
						Failure += " Exact direct reservation cleanup remains pending.";
					}
				}
				KingdomLog.Log("second founding remains recoverable: " + Failure);
				return false;
			}
		}

		/// <summary>Debug/script first founding with same authority and verification as basin flow,
		/// but no liquid debit.</summary>
		internal static bool TryFoundFirstWithoutWater(string Name, Zone Site,
			out Faction Faction, out string Failure)
		{
			Faction = null;
			Failure = "";
			if (!TryEnterFounding(null, null, out var lease))
			{
				Failure = "Another founding callback is already in flight; this nested attempt changed nothing.";
				return false;
			}
			using (lease)
			{
				return TryFoundFirstWithoutWaterCore(Name, Site, lease, out Faction,
					out Failure);
			}
		}

		private static bool TryFoundFirstWithoutWaterCore(string Name, Zone Site,
			FoundingLease Lease, out Faction Faction, out string Failure)
		{
			Faction = null;
			Failure = "";
			if (string.IsNullOrEmpty(Name) || Site == null)
			{
				Failure = "The first city needs a name and a site.";
				return false;
			}
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Cell rite = The.Player?.CurrentCell;
			int riteX = rite != null && rite.ParentZone == Site ? rite.X : Site.Width / 2;
			int riteY = rite != null && rite.ParentZone == Site ? rite.Y : Site.Height / 2;
			KingdomFoundingAuthority authority;
			bool hasSite = HasSiteReservation(Site);
			Faction reservedFaction = Factions.GetIfExists(Name);
			if (hasSite)
			{
				if (!TryReadSiteReservation(Site, out authority, out var storedName,
					out var storedVocation, out var storedVillage, out var storedDisplay,
					out var storedTick) || authority.OwnerKind != KingdomFoundingOwnerKind.Direct ||
					authority.Kind != KingdomFoundingKind.FirstCity || storedName != Name ||
					!string.IsNullOrEmpty(storedVocation) || !string.IsNullOrEmpty(storedVillage) ||
					!string.IsNullOrEmpty(storedDisplay) || authority.RealmFaction != Name ||
					authority.ZoneID != Site.ZoneID || authority.PayloadDigest !=
						DirectPayloadDigest(authority.Kind, storedName, null, null, null))
				{
					Failure = "This ground carries a quarantined or foreign founding reservation.";
					return false;
				}
				riteX = authority.RiteX;
				riteY = authority.RiteY;
			}
			else if (reservedFaction != null &&
				reservedFaction.GetIntProperty(PendingFactionProperty) == 1 &&
				reservedFaction.GetIntProperty("PlayerKingdom") == 1 &&
				reservedFaction.GetIntProperty("Village") == 1 &&
				KingdomFoundingTransactionRules.TryParseAuthority(
					reservedFaction.GetStringProperty(
						PendingFactionAuthorityProperty, null), out authority) &&
				authority.OwnerKind == KingdomFoundingOwnerKind.Direct &&
				authority.Kind == KingdomFoundingKind.FirstCity &&
				authority.RealmFaction == Name && authority.ZoneID == Site.ZoneID &&
				authority.TransactionID == reservedFaction.GetStringProperty(
					PendingFactionTransactionProperty, null) &&
				authority.PayloadDigest == DirectPayloadDigest(
					KingdomFoundingKind.FirstCity, Name, null, null, null) &&
				reservedFaction.GetStringProperty(RealmReservationProperty, null) ==
					KingdomFoundingTransactionRules.FormatAuthority(authority))
			{
				// Faction registration is irreversible. If site cleanup was interrupted after
				// that exact publication, its own durable faction tuple is sufficient to
				// recreate only the missing direct reservation and resume the same name/site.
				riteX = authority.RiteX;
				riteY = authority.RiteY;
			}
			else
			{
				if (system.Founded)
				{
					Failure = "A realm already stands; only its exact pending first transaction may resume.";
					return false;
				}
				authority = NewAuthority(KingdomFoundingKind.FirstCity,
					KingdomFoundingOwnerKind.Direct, Guid.NewGuid().ToString("N"),
					Guid.NewGuid().ToString("N"), Name, Site.ZoneID, riteX, riteY,
					DirectPayloadDigest(KingdomFoundingKind.FirstCity, Name, null, null, null));
			}
			if (system.Founded && (system.KingdomFactionName != Name ||
				reservedFaction == null ||
				reservedFaction.GetIntProperty(PendingFactionProperty) != 1))
			{
				Failure = "A realm already stands; this name or site is not its exact pending first transaction.";
				return false;
			}
			string encoded = KingdomFoundingTransactionRules.FormatAuthority(authority);
			if ((hasSite || reservedFaction != null) && reservedFaction != null &&
				reservedFaction.GetStringProperty(RealmReservationProperty, null) != encoded)
			{
				Failure = "The reserved first faction was removed or replaced.";
				return false;
			}
			if (encoded == null || !Lease.Bind(encoded, null) ||
				!AcquireGlobalReservation(encoded, Name, null))
			{
				Failure = "Another founding already holds this realm or site reservation.";
				return false;
			}
			if (!StageSiteReservation(Site, encoded, Name, null, null, null))
			{
				if (hasSite)
				{
					Failure = "The exact direct first-founding reservation is malformed and remains quarantined.";
					return false;
				}
				bool cleanedSite = ClearStagedSiteSubset(Site, encoded, Name,
					null, null, null);
				bool cleanedGlobal = ReleaseGlobalReservation(encoded, Name, null);
				Failure = cleanedSite && cleanedGlobal
					? "Another founding already holds this realm or site reservation."
					: "The direct first-founding reservation remains staged for exact cleanup.";
				return false;
			}
			r_FounderBasin carrier = new r_FounderBasin
			{
				PendingKind = KingdomFoundingKind.FirstCity,
				PendingOwnerKind = KingdomFoundingOwnerKind.Direct,
				PendingPhase = KingdomFoundingPhase.WaterCommitted,
				PendingTransactionID = authority.TransactionID,
				PendingOwnerNonce = authority.OwnerNonce,
				PendingPayloadDigest = authority.PayloadDigest,
				PendingAuthority = encoded,
				PendingRealmFaction = Name,
				PendingName = Name,
				PendingZoneID = Site.ZoneID,
				PendingRiteX = riteX,
				PendingRiteY = riteY,
				PendingChronicleEventID = FoundingEventID(
					authority.Kind, authority.TransactionID, "chronicle"),
				PendingChronicleDisposition = KingdomChronicleDisposition.None
			};
				if (!Lease.Bind(encoded, carrier))
				{
					bool cleared = ClearExactReservationSet(Site, encoded, Name, null);
					Failure = "The exact direct founding authority left its synchronous guard." +
						(cleared ? "" : " Its exact reservation remains pending cleanup.");
					return false;
			}
			try
			{
				KingdomFoundingProjection projection = KingdomFoundingProjection.Water;
				PublishFirst(carrier, The.Player, Site, ref projection);
				carrier.PendingPhase = KingdomFoundingPhase.Complete;
				Faction = Factions.GetIfExists(Name);
				if (!ClearExactReservationSet(Site, encoded, Name, null))
				{
					throw new InvalidOperationException(
						"The completed first founding could not clear its exact reservation.");
				}
				return Faction != null;
			}
			catch (Exception ex)
			{
				Failure = Describe(ex);
				if (!DetectPublication(carrier, Site))
				{
					if (!ClearExactReservationSet(Site, encoded, Name, null))
					{
						Failure += " Exact direct reservation cleanup remains pending.";
					}
				}
				KingdomLog.Log("first founding remains recoverable: " + Failure);
				return false;
			}
		}

		private static void ClearDirectRecovery(Zone Site, string ExpectedTransaction = null)
		{
			if (Site == null)
			{
				return;
			}
			if (!string.IsNullOrEmpty(ExpectedTransaction) &&
				Site.GetZoneProperty(DirectRecoveryTransactionProperty, null) !=
					ExpectedTransaction)
			{
				return;
			}
			Site.RemoveZoneProperty(DirectRecoveryNameProperty);
			Site.RemoveZoneProperty(DirectRecoveryVocationProperty);
			Site.RemoveZoneProperty(DirectRecoveryRiteXProperty);
			Site.RemoveZoneProperty(DirectRecoveryRiteYProperty);
			Site.RemoveZoneProperty(DirectRecoveryTickProperty);
			Site.RemoveZoneProperty(DirectRecoveryRealmProperty);
			Site.RemoveZoneProperty(DirectRecoveryTransactionProperty);
		}

		private static bool HasDirectRecovery(Zone Site)
		{
			return HasSiteReservation(Site);
		}

		private static bool DirectRecoveryMatches(Zone Site, string Name, string Realm,
			string Transaction)
		{
			if (!TryReadSiteReservation(Site, out var authority, out var storedName,
				out var vocation, out var village, out var display, out var tick))
			{
				return false;
			}
			return storedName == Name && authority.RealmFaction == Realm &&
				authority.TransactionID == Transaction;
		}

		private static bool StageDirectRecovery(Zone Site, string Name, string Vocation,
			int RiteX, int RiteY, string Realm, string Transaction)
		{
			if (Site == null || HasDirectRecovery(Site) || string.IsNullOrEmpty(Transaction))
			{
				return false;
			}
			string digest = DirectPayloadDigest(KingdomFoundingKind.SecondCity, Name,
				Vocation, null, null);
			KingdomFoundingAuthority authority = NewAuthority(
				KingdomFoundingKind.SecondCity, KingdomFoundingOwnerKind.Direct,
				Transaction, Guid.NewGuid().ToString("N"), Realm, Site.ZoneID,
				RiteX, RiteY, digest);
			string encoded = KingdomFoundingTransactionRules.FormatAuthority(authority);
			return encoded != null && StageSiteReservation(Site, encoded, Name,
				Vocation, null, null);
		}

		private static KingdomFoundingAuthority NewAuthority(KingdomFoundingKind Kind,
			KingdomFoundingOwnerKind OwnerKind, string Transaction, string OwnerNonce,
			string Realm, string ZoneID, int RiteX, int RiteY, string PayloadDigest)
		{
			return new KingdomFoundingAuthority
			{
				Kind = Kind,
				TransactionID = Transaction,
				OwnerKind = OwnerKind,
				OwnerNonce = OwnerNonce,
				RealmFaction = Realm,
				ZoneID = ZoneID,
				RiteX = RiteX,
				RiteY = RiteY,
				PayloadDigest = PayloadDigest
			};
		}

		private static string DirectPayloadDigest(KingdomFoundingKind Kind, string Name,
			string Vocation, string VillageFaction, string VillageDisplay)
		{
			return KingdomFoundingTransactionRules.PayloadDigest(Kind, Name, Vocation,
				VillageFaction, VillageDisplay, -1, -1, -1, -1, "", "");
		}

		internal static bool HasGlobalReservation()
		{
			return The.Game != null && The.Game.HasStringGameState(GlobalReservationState) &&
				!string.IsNullOrEmpty(The.Game.GetStringGameState(GlobalReservationState, null));
		}

		internal static bool GlobalReservationMatches(string Authority)
		{
			return !string.IsNullOrEmpty(Authority) && The.Game != null &&
				The.Game.GetStringGameState(GlobalReservationState, null) == Authority &&
				KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed);
		}

		private static bool ReservationAbsentOrExact(string Authority, string Realm,
			string VillageFaction)
		{
			if (string.IsNullOrEmpty(Authority))
			{
				return false;
			}
			string global = The.Game?.GetStringGameState(GlobalReservationState, null);
			Faction realm = Factions.GetIfExists(Realm);
			Faction village = string.IsNullOrEmpty(VillageFaction)
				? null : Factions.GetIfExists(VillageFaction);
			string realmBound = realm?.GetStringProperty(RealmReservationProperty, null);
			string villageBound = village?.GetStringProperty(VillageReservationProperty, null);
			return (string.IsNullOrEmpty(global) || global == Authority) &&
				(string.IsNullOrEmpty(realmBound) || realmBound == Authority) &&
				(string.IsNullOrEmpty(villageBound) || villageBound == Authority);
		}

		private static bool AcquireGlobalReservation(string Authority, string Realm,
			string VillageFaction)
		{
			if (The.Game == null || string.IsNullOrEmpty(Authority) ||
				!KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed) ||
				parsed.RealmFaction != Realm)
			{
				return false;
			}
			string current = The.Game.GetStringGameState(GlobalReservationState, null);
			bool wroteGlobal = string.IsNullOrEmpty(current);
			if (!wroteGlobal && current != Authority)
			{
				return false;
			}
			Faction realmFaction = Factions.GetIfExists(Realm);
			Faction village = string.IsNullOrEmpty(VillageFaction)
				? null : Factions.GetIfExists(VillageFaction);
			bool wroteRealm = false;
			bool wroteVillage = false;
			try
			{
				if (wroteGlobal)
				{
					The.Game.SetStringGameState(GlobalReservationState, Authority);
				}
				if (The.Game.GetStringGameState(GlobalReservationState, null) != Authority)
				{
					throw new InvalidOperationException("The global founding reservation was not retained.");
				}
				if (realmFaction != null)
				{
					string bound = realmFaction.GetStringProperty(RealmReservationProperty, null);
					if (!string.IsNullOrEmpty(bound) && bound != Authority)
					{
						throw new InvalidOperationException("The realm faction carries another founding reservation.");
					}
					wroteRealm = string.IsNullOrEmpty(bound);
					realmFaction.SetProperty(RealmReservationProperty, Authority);
					if (realmFaction.GetStringProperty(RealmReservationProperty, null) != Authority)
					{
						throw new InvalidOperationException("The faction-global reservation was not retained.");
					}
				}
				if (village != null)
				{
					string bound = village.GetStringProperty(VillageReservationProperty, null);
					if (!string.IsNullOrEmpty(bound) && bound != Authority)
					{
						throw new InvalidOperationException("The village carries another covenant reservation.");
					}
					wroteVillage = string.IsNullOrEmpty(bound);
					village.SetProperty(VillageReservationProperty, Authority);
					if (village.GetStringProperty(VillageReservationProperty, null) != Authority)
					{
						throw new InvalidOperationException("The village reservation was not retained.");
					}
				}
				return true;
			}
			catch
			{
				if (wroteVillage && village?.GetStringProperty(
					VillageReservationProperty, null) == Authority)
				{
					village.RemoveProperty(VillageReservationProperty);
				}
				if (wroteRealm && realmFaction?.GetStringProperty(
					RealmReservationProperty, null) == Authority)
				{
					realmFaction.RemoveProperty(RealmReservationProperty);
				}
				if (wroteGlobal && The.Game.GetStringGameState(
					GlobalReservationState, null) == Authority)
				{
					The.Game.RemoveStringGameState(GlobalReservationState);
				}
				return false;
			}
		}

		private static bool ReleaseGlobalReservation(string Authority, string Realm,
			string VillageFaction)
		{
			if (string.IsNullOrEmpty(Authority))
			{
				return false;
			}
			Faction realm = Factions.GetIfExists(Realm);
			Faction village = string.IsNullOrEmpty(VillageFaction)
				? null : Factions.GetIfExists(VillageFaction);
			string globalBound = The.Game?.GetStringGameState(GlobalReservationState, null);
			string realmBound = realm?.GetStringProperty(RealmReservationProperty, null);
			string villageBound = village?.GetStringProperty(VillageReservationProperty, null);
			// Cleanup may remove only this transaction's exact markers. A missing marker is
			// idempotent; a foreign marker is never treated as successful cleanup.
			if ((!string.IsNullOrEmpty(globalBound) && globalBound != Authority) ||
				(!string.IsNullOrEmpty(realmBound) && realmBound != Authority) ||
				(!string.IsNullOrEmpty(villageBound) && villageBound != Authority))
			{
				return false;
			}
			if (realm?.GetStringProperty(RealmReservationProperty, null) == Authority)
			{
				realm.RemoveProperty(RealmReservationProperty);
			}
			if (village?.GetStringProperty(VillageReservationProperty, null) == Authority)
			{
				village.RemoveProperty(VillageReservationProperty);
			}
			if (The.Game?.GetStringGameState(GlobalReservationState, null) == Authority)
			{
				The.Game.RemoveStringGameState(GlobalReservationState);
			}
			return string.IsNullOrEmpty(The.Game?.GetStringGameState(
					GlobalReservationState, null)) &&
				string.IsNullOrEmpty(realm?.GetStringProperty(
					RealmReservationProperty, null)) &&
				string.IsNullOrEmpty(village?.GetStringProperty(
					VillageReservationProperty, null));
		}

		private static bool GlobalReservationMarkersAbsent(string Realm,
			string VillageFaction)
		{
			Faction realm = Factions.GetIfExists(Realm);
			Faction village = string.IsNullOrEmpty(VillageFaction)
				? null : Factions.GetIfExists(VillageFaction);
			return string.IsNullOrEmpty(The.Game?.GetStringGameState(
					GlobalReservationState, null)) &&
				string.IsNullOrEmpty(realm?.GetStringProperty(
					RealmReservationProperty, null)) &&
				string.IsNullOrEmpty(village?.GetStringProperty(
					VillageReservationProperty, null));
		}

		private static bool ExactAuthorityMarkersAbsent(string Authority, string Realm,
			string VillageFaction, Zone Site = null)
		{
			Faction realm = Factions.GetIfExists(Realm);
			Faction village = string.IsNullOrEmpty(VillageFaction)
				? null : Factions.GetIfExists(VillageFaction);
			return The.Game?.GetStringGameState(GlobalReservationState, null) != Authority &&
				realm?.GetStringProperty(RealmReservationProperty, null) != Authority &&
				village?.GetStringProperty(VillageReservationProperty, null) != Authority &&
				(Site == null || Site.GetZoneProperty(
					SiteReservationProperty, null) != Authority);
		}

		private static bool ClearExactReservationSet(Zone Site, string Authority,
			string Realm, string VillageFaction)
		{
			if (Site == null || string.IsNullOrEmpty(Authority))
			{
				return false;
			}
			if (KingdomFoundingTransactionRules.TryParseAuthority(Authority,
				out var parsedAuthority) && parsedAuthority.Kind == KingdomFoundingKind.SecondCity)
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				if (system == null || !system.Founded ||
					system.KingdomFactionName != parsedAuthority.RealmFaction ||
					!system.TryAbortPendingSettlementIdentity(
						parsedAuthority.TransactionID, parsedAuthority.ZoneID, Authority,
						out string pendingFailure))
					return false;
			}
			// Clear broad reservation before site evidence. A save cut therefore leaves the
			// exact site marker available to reacquire authority; never an ownerless global lock.
			if (!ReleaseGlobalReservation(Authority, Realm, VillageFaction))
			{
				return false;
			}
			if (HasSiteReservation(Site) &&
				(!SiteReservationMatches(Site, Authority) ||
				 !ReleaseSiteReservation(Site, Authority)))
			{
				return false;
			}
			return !HasSiteReservation(Site) &&
				GlobalReservationMarkersAbsent(Realm, VillageFaction);
		}

		internal static bool HasSiteReservation(Zone Site)
		{
			return Site != null && (Site.HasZoneProperty(SiteReservationProperty) ||
				Site.HasZoneProperty(SiteReservationNameProperty) ||
				Site.HasZoneProperty(SiteReservationVocationProperty) ||
				Site.HasZoneProperty(SiteReservationVillageProperty) ||
				Site.HasZoneProperty(SiteReservationDisplayProperty) ||
				Site.HasZoneProperty(SiteReservationTickProperty) ||
				Site.HasZoneProperty(DirectRecoveryNameProperty) ||
				Site.HasZoneProperty(DirectRecoveryVocationProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteXProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteYProperty) ||
				Site.HasZoneProperty(DirectRecoveryTickProperty) ||
				Site.HasZoneProperty(DirectRecoveryRealmProperty) ||
				Site.HasZoneProperty(DirectRecoveryTransactionProperty));
		}

		private static bool CompletedSiteReservationSubsetMatches(Zone Site,
			r_FounderBasin Basin)
		{
			if (Site == null || Basin == null || Site.ZoneID != Basin.PendingZoneID ||
				Site.HasZoneProperty(DirectRecoveryNameProperty) ||
				Site.HasZoneProperty(DirectRecoveryVocationProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteXProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteYProperty) ||
				Site.HasZoneProperty(DirectRecoveryTickProperty) ||
				Site.HasZoneProperty(DirectRecoveryRealmProperty) ||
				Site.HasZoneProperty(DirectRecoveryTransactionProperty))
			{
				return false;
			}
			return (!Site.HasZoneProperty(SiteReservationProperty) ||
					Site.GetZoneProperty(SiteReservationProperty, null) == Basin.PendingAuthority) &&
				(!Site.HasZoneProperty(SiteReservationNameProperty) ||
					Site.GetZoneProperty(SiteReservationNameProperty, null) == Basin.PendingName) &&
				(!Site.HasZoneProperty(SiteReservationVocationProperty) ||
					Site.GetZoneProperty(SiteReservationVocationProperty, null) ==
						Basin.PendingVocation) &&
				(!Site.HasZoneProperty(SiteReservationVillageProperty) ||
					Site.GetZoneProperty(SiteReservationVillageProperty, null) ==
						Basin.PendingVillageFaction) &&
				(!Site.HasZoneProperty(SiteReservationDisplayProperty) ||
					Site.GetZoneProperty(SiteReservationDisplayProperty, null) ==
						Basin.PendingVillageDisplayName) &&
				(!Site.HasZoneProperty(SiteReservationTickProperty) ||
					(long.TryParse(Site.GetZoneProperty(SiteReservationTickProperty, null),
						out var tick) && tick >= 0L));
		}

		private static bool ClearCompletedSiteReservation(Zone Site, r_FounderBasin Basin)
		{
			if (!CompletedSiteReservationSubsetMatches(Site, Basin))
			{
				return false;
			}
			if (!ClearFrozenSecondIdentity(Site, Basin.PendingAuthority)) return false;
			Site.RemoveZoneProperty(SiteReservationNameProperty);
			Site.RemoveZoneProperty(SiteReservationVocationProperty);
			Site.RemoveZoneProperty(SiteReservationVillageProperty);
			Site.RemoveZoneProperty(SiteReservationDisplayProperty);
			Site.RemoveZoneProperty(SiteReservationTickProperty);
			Site.RemoveZoneProperty(SiteReservationProperty);
			return !HasSiteReservation(Site);
		}

		private static bool ClearStagedSiteSubset(Zone Site, string Authority, string Name,
			string Vocation, string VillageFaction, string VillageDisplay)
		{
			if (Site == null || string.IsNullOrEmpty(Authority) ||
				Site.HasZoneProperty(DirectRecoveryNameProperty) ||
				Site.HasZoneProperty(DirectRecoveryVocationProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteXProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteYProperty) ||
				Site.HasZoneProperty(DirectRecoveryTickProperty) ||
				Site.HasZoneProperty(DirectRecoveryRealmProperty) ||
				Site.HasZoneProperty(DirectRecoveryTransactionProperty) ||
				(Site.HasZoneProperty(SiteReservationProperty) &&
				 Site.GetZoneProperty(SiteReservationProperty, null) != Authority) ||
				(Site.HasZoneProperty(SiteReservationNameProperty) &&
				 Site.GetZoneProperty(SiteReservationNameProperty, null) != Name) ||
				(Site.HasZoneProperty(SiteReservationVocationProperty) &&
				 Site.GetZoneProperty(SiteReservationVocationProperty, null) != Vocation) ||
				(Site.HasZoneProperty(SiteReservationVillageProperty) &&
				 Site.GetZoneProperty(SiteReservationVillageProperty, null) != VillageFaction) ||
				(Site.HasZoneProperty(SiteReservationDisplayProperty) &&
				 Site.GetZoneProperty(SiteReservationDisplayProperty, null) != VillageDisplay) ||
				(Site.HasZoneProperty(SiteReservationTickProperty) &&
				 (!long.TryParse(Site.GetZoneProperty(SiteReservationTickProperty, null),
					out var tick) || tick < 0L)))
			{
				return false;
			}
			if (!ClearFrozenSecondIdentity(Site, Authority)) return false;
			Site.RemoveZoneProperty(SiteReservationNameProperty);
			Site.RemoveZoneProperty(SiteReservationVocationProperty);
			Site.RemoveZoneProperty(SiteReservationVillageProperty);
			Site.RemoveZoneProperty(SiteReservationDisplayProperty);
			Site.RemoveZoneProperty(SiteReservationTickProperty);
			Site.RemoveZoneProperty(SiteReservationProperty);
			return !HasSiteReservation(Site);
		}

		internal static bool SiteReservationMatches(Zone Site, string Authority)
		{
			return Site != null && !string.IsNullOrEmpty(Authority) &&
				Site.GetZoneProperty(SiteReservationProperty, null) == Authority &&
				KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed) &&
				parsed.ZoneID == Site.ZoneID;
		}

		private static bool StageSiteReservation(Zone Site, string Authority, string Name,
			string Vocation, string VillageFaction, string VillageDisplay)
		{
			if (Site == null || string.IsNullOrEmpty(Name) ||
				!KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed) ||
				parsed.ZoneID != Site.ZoneID)
			{
				return false;
			}
			string published = Site.GetZoneProperty(
				SecondPublicationAuthorityProperty, null);
			if (parsed.Kind == KingdomFoundingKind.SecondCity &&
				!string.IsNullOrEmpty(published) && published != Authority)
			{
				return false;
			}
			string existing = Site.GetZoneProperty(SiteReservationProperty, null);
			bool hasExisting = HasSiteReservation(Site);
			if (hasExisting && existing != Authority)
			{
				return false;
			}
			if (hasExisting)
			{
				return TryReadSiteReservation(Site, out var existingAuthority,
					out var existingName, out var existingVocation,
					out var existingVillage, out var existingDisplay,
					out var existingTick) &&
					KingdomFoundingTransactionRules.FormatAuthority(existingAuthority) ==
						Authority && existingName == Name &&
					existingVocation == Vocation &&
					existingVillage == VillageFaction &&
					existingDisplay == VillageDisplay;
			}
			try
			{
				Site.SetZoneProperty(SiteReservationProperty, Authority);
				Site.SetZoneProperty(SiteReservationNameProperty, Name);
				SetOrRemoveZoneProperty(Site, SiteReservationVocationProperty, Vocation);
				SetOrRemoveZoneProperty(Site, SiteReservationVillageProperty, VillageFaction);
				SetOrRemoveZoneProperty(Site, SiteReservationDisplayProperty, VillageDisplay);
				Site.SetZoneProperty(SiteReservationTickProperty,
					The.Game.TimeTicks.ToString());
				return TryReadSiteReservation(Site, out var read, out var readName,
					out var readVocation, out var readVillage, out var readDisplay,
					out var readTick) && KingdomFoundingTransactionRules.FormatAuthority(read) ==
						Authority && readName == Name && readVocation == Vocation &&
						readVillage == VillageFaction && readDisplay == VillageDisplay;
			}
			catch
			{
				return false;
			}
		}

		private static void SetOrRemoveZoneProperty(Zone Site, string Property,
			string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				Site.RemoveZoneProperty(Property);
			}
			else
			{
				Site.SetZoneProperty(Property, Value);
			}
		}

		private static bool TryReadSiteReservation(Zone Site,
			out KingdomFoundingAuthority Authority, out string Name, out string Vocation,
			out string VillageFaction, out string VillageDisplay, out long Tick)
		{
			Authority = default(KingdomFoundingAuthority);
			Name = Vocation = VillageFaction = VillageDisplay = null;
			Tick = -1L;
			if (Site == null || !KingdomFoundingTransactionRules.TryParseAuthority(
				Site.GetZoneProperty(SiteReservationProperty, null), out Authority) ||
				Authority.ZoneID != Site.ZoneID)
			{
				return false;
			}
			Name = Site.GetZoneProperty(SiteReservationNameProperty, null);
			Vocation = Site.GetZoneProperty(SiteReservationVocationProperty, null);
			VillageFaction = Site.GetZoneProperty(SiteReservationVillageProperty, null);
			VillageDisplay = Site.GetZoneProperty(SiteReservationDisplayProperty, null);
			return !string.IsNullOrEmpty(Name) && Name.Length <= 256 &&
				(Vocation == null || Vocation.Length <= 64) &&
				(VillageFaction == null || VillageFaction.Length <= 256) &&
				(VillageDisplay == null || VillageDisplay.Length <= 256) &&
				long.TryParse(Site.GetZoneProperty(SiteReservationTickProperty, null), out Tick) &&
				Tick >= 0L;
		}

		private static bool ReleaseSiteReservation(Zone Site, string Authority)
		{
			if (!SiteReservationMatches(Site, Authority))
			{
				return false;
			}
			if (!ClearFrozenSecondIdentity(Site, Authority)) return false;
			Site.RemoveZoneProperty(SiteReservationProperty);
			Site.RemoveZoneProperty(SiteReservationNameProperty);
			Site.RemoveZoneProperty(SiteReservationVocationProperty);
			Site.RemoveZoneProperty(SiteReservationVillageProperty);
			Site.RemoveZoneProperty(SiteReservationDisplayProperty);
			Site.RemoveZoneProperty(SiteReservationTickProperty);
			ClearLegacyDirectRecovery(Site);
			return !HasSiteReservation(Site);
		}

		private static bool ClearFrozenSecondIdentity(Zone Site, string Authority)
		{
			if (Site == null || !KingdomFoundingTransactionRules.TryParseAuthority(
				Authority, out var parsed) || parsed.ZoneID != Site.ZoneID)
				return false;
			KingdomSystem currentSystem = null;
			if (parsed.Kind == KingdomFoundingKind.SecondCity)
			{
				currentSystem = The.Game?.GetSystem<KingdomSystem>();
				if (currentSystem == null || !currentSystem.Founded ||
					currentSystem.KingdomFactionName != parsed.RealmFaction) return false;
			}
			bool any = Site.HasZoneProperty(SecondIdentityTransactionProperty) ||
				Site.HasZoneProperty(SecondIdentityRealmProperty) ||
				Site.HasZoneProperty(SecondIdentitySettlementProperty) ||
				Site.HasZoneProperty(SecondIdentityVersionProperty) ||
				Site.HasZoneProperty(SecondIdentityOriginProperty);
			if (!any) return true;
			string transaction = Site.GetZoneProperty(
				SecondIdentityTransactionProperty, null);
			string realm = Site.GetZoneProperty(SecondIdentityRealmProperty, null);
			string settlement = Site.GetZoneProperty(
				SecondIdentitySettlementProperty, null);
			if ((transaction != null && transaction != parsed.TransactionID) ||
				(realm != null && (currentSystem == null ||
				 realm != currentSystem.RealmId)) ||
				(Site.HasZoneProperty(SecondIdentityVersionProperty) &&
				 Site.GetZoneProperty(SecondIdentityVersionProperty, null) !=
					KingdomIdentityRules.RulesVersion.ToString()) ||
				(Site.HasZoneProperty(SecondIdentityOriginProperty) &&
				 Site.GetZoneProperty(SecondIdentityOriginProperty, null) !=
					((int)KingdomIdentityOrigin.FoundingTransaction).ToString()))
				return false;
			if (settlement != null)
			{
				string expected;
				KingdomIdentityFault fault;
				if (realm == null || !KingdomIdentityRules.TryMintSettlement(realm,
					parsed.TransactionID, out expected, out fault) || expected != settlement)
					return false;
			}
			Site.RemoveZoneProperty(SecondIdentityTransactionProperty);
			Site.RemoveZoneProperty(SecondIdentityRealmProperty);
			Site.RemoveZoneProperty(SecondIdentitySettlementProperty);
			Site.RemoveZoneProperty(SecondIdentityVersionProperty);
			Site.RemoveZoneProperty(SecondIdentityOriginProperty);
			return !Site.HasZoneProperty(SecondIdentityTransactionProperty) &&
				!Site.HasZoneProperty(SecondIdentityRealmProperty) &&
				!Site.HasZoneProperty(SecondIdentitySettlementProperty) &&
				!Site.HasZoneProperty(SecondIdentityVersionProperty) &&
				!Site.HasZoneProperty(SecondIdentityOriginProperty);
		}

		private static void ClearLegacyDirectRecovery(Zone Site)
		{
			if (Site == null)
			{
				return;
			}
			Site.RemoveZoneProperty(DirectRecoveryNameProperty);
			Site.RemoveZoneProperty(DirectRecoveryVocationProperty);
			Site.RemoveZoneProperty(DirectRecoveryRiteXProperty);
			Site.RemoveZoneProperty(DirectRecoveryRiteYProperty);
			Site.RemoveZoneProperty(DirectRecoveryTickProperty);
			Site.RemoveZoneProperty(DirectRecoveryRealmProperty);
			Site.RemoveZoneProperty(DirectRecoveryTransactionProperty);
		}

		public static KingdomFoundingResult BeginFirst(r_FounderBasin Basin,
			GameObject Actor, Zone Site, string Name)
		{
			if (!TryEnterFounding(null, Basin, out var lease))
			{
				return ReentryRefusal();
			}
			using (lease)
			{
				KingdomFoundingResult start = Begin(Basin, Actor, Site,
					KingdomFoundingKind.FirstCity, Name, null, null, null);
				if (start.Outcome != KingdomFoundingOutcome.Committed)
				{
					return start;
				}
				if (!lease.Bind(Basin.PendingAuthority, Basin))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Water,
						"The exact paid authority could not enter its synchronous guard.");
				}
				return Run(Basin, Actor, Site);
			}
		}

		public static KingdomFoundingResult BeginSecond(r_FounderBasin Basin,
			GameObject Actor, Zone Site, string Name, string Vocation)
		{
			if (!TryEnterFounding(null, Basin, out var lease))
			{
				return ReentryRefusal();
			}
			using (lease)
			{
				KingdomFoundingResult start = Begin(Basin, Actor, Site,
					KingdomFoundingKind.SecondCity, Name, Vocation, null, null);
				if (start.Outcome != KingdomFoundingOutcome.Committed)
				{
					return start;
				}
				if (!lease.Bind(Basin.PendingAuthority, Basin))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Water,
						"The exact paid authority could not enter its synchronous guard.");
				}
				return Run(Basin, Actor, Site);
			}
		}

		public static KingdomFoundingResult BeginVillageCharter(r_FounderBasin Basin,
			GameObject Actor, Zone Site, string FactionName, string DisplayName)
		{
			if (!TryEnterFounding(null, Basin, out var lease))
			{
				return ReentryRefusal();
			}
			using (lease)
			{
				KingdomFoundingResult start = Begin(Basin, Actor, Site,
					KingdomFoundingKind.VillageCharter, DisplayName, null,
					FactionName, DisplayName);
				if (start.Outcome != KingdomFoundingOutcome.Committed)
				{
					return start;
				}
				if (!lease.Bind(Basin.PendingAuthority, Basin))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Water,
						"The exact paid authority could not enter its synchronous guard.");
				}
				return Run(Basin, Actor, Site);
			}
		}

		/// <summary>Resumes the one serialized receipt carried by <paramref name="Basin"/>.</summary>
		public static KingdomFoundingResult Resume(r_FounderBasin Basin,
			GameObject Actor, Zone Site)
		{
			if (!TryEnterFounding(Basin?.PendingAuthority, Basin, out var lease))
			{
				return ReentryRefusal();
			}
			using (lease)
			{
				return ResumeGuarded(Basin, Actor, Site, lease);
			}
		}

		private static KingdomFoundingResult ResumeGuarded(r_FounderBasin Basin,
			GameObject Actor, Zone Site, FoundingLease Lease)
		{
			KingdomFoundingReceiptNormalization normalization = NormalizeReceipt(Basin);
			if (normalization == KingdomFoundingReceiptNormalization.ClearStaged)
			{
				if (TryClearStagedReceipt(Basin, Site))
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"The interrupted staging ended before any water was spent; its exact reservation was cleared.");
				}
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"The unpaid staged receipt no longer matches its exact basin, site, or reservation and was quarantined.");
			}
			if (normalization == KingdomFoundingReceiptNormalization.Clean)
			{
				if (!SafeClearReceipt(Basin))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.RestorationFailed,
						KingdomFoundingProjection.None,
						"The empty founding receipt could not prove every receipt field absent.");
				}
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None, "There is no interrupted rite to resume.");
			}
			if (normalization == KingdomFoundingReceiptNormalization.Quarantine)
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"The founding receipt header is malformed and has been quarantined without another debit.");
			}
			if (!Lease.Bind(Basin.PendingAuthority, Basin))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.None,
					"The exact pending authority could not enter its synchronous guard.");
			}
			return Run(Basin, Actor, Site);
		}

		/// <summary>
		/// A temporary Committed result means only that Begin established the exact water receipt;
		/// the public Begin* methods immediately continue into Run and never expose it to UI.
		/// </summary>
		private static KingdomFoundingResult Begin(r_FounderBasin Basin, GameObject Actor,
			Zone Site, KingdomFoundingKind Kind, string Name, string Vocation,
			string VillageFaction, string VillageDisplayName)
		{
			if (Basin == null || Basin.ParentObject == null || Actor == null || Site == null ||
				Actor.CurrentZone != Site || Actor.CurrentCell == null ||
				Actor.CurrentCell.ParentZone != Site ||
				!KingdomFoundingTransactionRules.IsKnownKind(Kind) ||
				Kind == KingdomFoundingKind.None || string.IsNullOrEmpty(Name) ||
				Name.Length > 256 ||
				(Kind == KingdomFoundingKind.FirstCity &&
					(!string.IsNullOrEmpty(Vocation) || !string.IsNullOrEmpty(VillageFaction) ||
					 !string.IsNullOrEmpty(VillageDisplayName))) ||
				(Kind == KingdomFoundingKind.SecondCity &&
					(!KingdomSettlement.IsKnownVocation(Vocation) ||
					 !string.IsNullOrEmpty(VillageFaction) ||
					 !string.IsNullOrEmpty(VillageDisplayName))) ||
				(Kind == KingdomFoundingKind.VillageCharter &&
					(string.IsNullOrEmpty(VillageFaction) ||
					 VillageFaction.Length > 256 ||
					 string.IsNullOrEmpty(VillageDisplayName) ||
					 !string.IsNullOrEmpty(Vocation))))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The founder, basin, site, and name must all still be present.");
			}
			KingdomFoundingReceiptNormalization normalization = NormalizeReceipt(Basin);
			if (normalization == KingdomFoundingReceiptNormalization.ClearStaged)
			{
				if (!TryClearStagedReceipt(Basin, Site))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.RestorationFailed,
						KingdomFoundingProjection.None,
						"This basin carries an unpaid but malformed staged receipt; it was quarantined.");
				}
				normalization = NormalizeReceipt(Basin);
			}
			if (normalization == KingdomFoundingReceiptNormalization.Pending)
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.None,
					"This basin already carries an interrupted founding receipt.");
			}
			if (normalization == KingdomFoundingReceiptNormalization.Quarantine)
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"This basin carries a malformed founding receipt; it was quarantined without another debit.");
			}

			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			string realmFaction = Kind == KingdomFoundingKind.FirstCity
				? Name : system.KingdomFactionName;
			if (Kind == KingdomFoundingKind.FirstCity && system.Founded)
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"A realm already stands; this pour cannot replace it.");
			}
			if (Kind == KingdomFoundingKind.FirstCity && !FactionNameAvailable(Name))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"That faction name already belongs to something in this world.");
			}
			if (Kind == KingdomFoundingKind.SecondCity)
			{
				KingdomSettlement.SecondFoundingVerdict verdict =
					KingdomFounding.JudgeSite(system, Site);
				if (verdict != KingdomSettlement.SecondFoundingVerdict.Allowed ||
					!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
						system.SettlementCount, KingdomSettlement.MaxSettlements,
						system.Away == null, TargetIsExactSeat: false,
						AlreadyPublished: false))
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"The realm no longer has one open, eligible second-city seat.");
				}
			}
			if (Kind != KingdomFoundingKind.FirstCity &&
				(string.IsNullOrEmpty(realmFaction) || !system.Founded))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"No coherent realm exists for this rite.");
			}
			if (Kind != KingdomFoundingKind.FirstCity)
			{
				Faction realm = Factions.GetIfExists(realmFaction);
				if (!FactionRegistryCoherent(realmFaction, realm) ||
					realm.GetIntProperty("PlayerKingdom") != 1 ||
					realm.GetIntProperty("Village") != 1)
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"The realm faction table and list do not agree; nothing was poured.");
				}
			}
			if (Kind == KingdomFoundingKind.VillageCharter)
			{
				Faction village = Factions.GetIfExists(VillageFaction);
				if (!FactionRegistryCoherent(VillageFaction, village) ||
					village.GetIntProperty("Village") != 1 || village.DisplayName != VillageDisplayName ||
					Site.GetZoneProperty("faction", null) != VillageFaction)
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"The village faction is no longer one coherent living village.");
				}
			}

			LiquidVolume vessel = Basin.ParentObject.GetPart<LiquidVolume>();
			int committedVolume;
			if (vessel == null || vessel.ParentObject != Basin.ParentObject ||
				vessel.MaxVolume < vessel.Volume || vessel.MaxVolume < 0 ||
				!KingdomLiquids.HasFreshWater(vessel) ||
				!KingdomFoundingTransactionRules.TryCommittedVolume(vessel.Volume,
					KingdomRules.FoundingCostDrams, out committedVolume))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The basin does not hold the exact fresh-water cost.");
			}

			string ownerNonce = Basin.EnsureOwnerNonce();
			string transaction = Guid.NewGuid().ToString("N");
			Dictionary<string, int> originalComponents = Copy(vessel.ComponentLiquids);
			Dictionary<string, int> committedComponents = committedVolume == 0
				? new Dictionary<string, int>() : Copy(vessel.ComponentLiquids);
			string originalEncoding = EncodeComponents(originalComponents);
			string committedEncoding = EncodeComponents(committedComponents);
			string payloadDigest = KingdomFoundingTransactionRules.PayloadDigest(Kind, Name,
				Vocation, VillageFaction, VillageDisplayName, vessel.Volume, vessel.MaxVolume,
				committedVolume, vessel.MaxVolume, originalEncoding, committedEncoding);
			KingdomFoundingAuthority authority = NewAuthority(Kind,
				KingdomFoundingOwnerKind.Basin, transaction, ownerNonce, realmFaction,
				Site.ZoneID, Actor.CurrentCell.X, Actor.CurrentCell.Y, payloadDigest);
			string encodedAuthority = KingdomFoundingTransactionRules.FormatAuthority(authority);
			if (encodedAuthority == null)
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The founding authority could not be encoded exactly.");
			}
			if (Kind == KingdomFoundingKind.SecondCity)
			{
				if (!system.TryPrepareLaterSettlementIdentity(transaction, Site.ZoneID,
						out string preflightSettlementId, out string topologyFailure) ||
					!system.TryPrepareSecondCityTopology(preflightSettlementId,
						out KingdomSecondCityTopologyPlan ignoredPlan, out topologyFailure))
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"Trade or Carry cannot admit another city before this pour: " +
							topologyFailure);
				}
			}

			try
			{
				Basin.PendingKind = Kind;
				Basin.PendingPhase = KingdomFoundingPhase.None;
				Basin.PendingOwnerKind = KingdomFoundingOwnerKind.Basin;
				Basin.PendingTransactionID = transaction;
				Basin.PendingBasinID = Basin.ParentObject.ID;
				Basin.PendingOwnerNonce = ownerNonce;
				Basin.PendingPayloadDigest = payloadDigest;
				Basin.PendingAuthority = encodedAuthority;
				Basin.PendingRealmFaction = realmFaction;
				Basin.PendingName = Name;
				Basin.PendingVocation = Vocation;
				Basin.PendingVillageFaction = VillageFaction;
				Basin.PendingVillageDisplayName = VillageDisplayName;
				Basin.PendingZoneID = Site.ZoneID;
				Basin.PendingRiteX = Actor.CurrentCell.X;
				Basin.PendingRiteY = Actor.CurrentCell.Y;
				Basin.PendingOriginalVolume = vessel.Volume;
				Basin.PendingOriginalMaxVolume = vessel.MaxVolume;
				Basin.PendingOriginalComponents = originalComponents;
				Basin.PendingCommittedVolume = committedVolume;
				Basin.PendingCommittedMaxVolume = vessel.MaxVolume;
				Basin.PendingCommittedComponents = committedComponents;
				Basin.PendingChronicleRecorded = false;
				Basin.PendingChronicleStage = 0;
				Basin.PendingChronicleDisposition = KingdomChronicleDisposition.None;
				Basin.PendingChronicleEventID = FoundingEventID(Kind,
					transaction, "chronicle");
			}
			catch (Exception ex)
			{
				bool cleared = ExactAuthorityMarkersAbsent(encodedAuthority,
					realmFaction, VillageFaction, Site) && SafeClearReceipt(Basin);
				return Result(cleared ? KingdomFoundingOutcome.Refused :
						KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The founding receipt could not be staged before the pour: " + Describe(ex));
			}
			if (!ValidateReceiptPayload(Basin, null, vessel, out var stagedFailure) ||
				!OriginalSnapshotStillExact(Basin, vessel))
			{
				bool cleared = ExactAuthorityMarkersAbsent(encodedAuthority,
					realmFaction, VillageFaction, Site) && SafeClearReceipt(Basin);
				return Result(cleared ? KingdomFoundingOutcome.Refused :
						KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The staged founding receipt failed its own strict readback: " +
					stagedFailure);
			}
			bool siteWasReserved = HasSiteReservation(Site);
			if (!StageSiteReservation(Site, encodedAuthority, Name, Vocation,
					VillageFaction, VillageDisplayName) ||
				!ValidateReceiptPayload(Basin, Site, vessel, out stagedFailure) ||
				!OriginalSnapshotStillExact(Basin, vessel))
			{
				if (!siteWasReserved)
				{
					ClearStagedSiteSubset(Site, encodedAuthority, Name, Vocation,
						VillageFaction, VillageDisplayName);
				}
				bool cleared = ExactAuthorityMarkersAbsent(encodedAuthority,
					realmFaction, VillageFaction, Site) &&
					(siteWasReserved || !HasSiteReservation(Site)) &&
					SafeClearReceipt(Basin);
				return Result(cleared ? KingdomFoundingOutcome.Refused :
						KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The exact site reservation could not be staged and read back before the pour: " +
						stagedFailure);
			}
			string reservationVillage = Kind == KingdomFoundingKind.VillageCharter
				? VillageFaction : null;
			if (!AcquireGlobalReservation(encodedAuthority, realmFaction,
				reservationVillage))
			{
				if (!siteWasReserved)
					ClearStagedSiteSubset(Site, encodedAuthority, Name, Vocation,
						VillageFaction, VillageDisplayName);
				bool cleared = ExactAuthorityMarkersAbsent(encodedAuthority,
					realmFaction, reservationVillage, Site) &&
					(siteWasReserved || !HasSiteReservation(Site)) && SafeClearReceipt(Basin);
				return Result(cleared ? KingdomFoundingOutcome.Refused :
						KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"Another founding already reserves this realm or ground.");
			}

			try
			{
				// Durable forward-recovery intent precedes liquid removal. A save from inside
				// Drain can never look like an unpaid staged receipt.
				Basin.PendingPhase = KingdomFoundingPhase.WaterCommitted;
				int removed = KingdomLiquids.Drain(vessel, KingdomRules.FoundingCostDrams);
				if (removed != KingdomRules.FoundingCostDrams || vessel.Volume != committedVolume)
				{
					bool restored = RestorePrePublication(Basin, vessel);
					bool cleaned = false;
					if (restored)
					{
						Basin.PendingPhase = KingdomFoundingPhase.None;
						cleaned = ClearExactReservationSet(Site, encodedAuthority,
							realmFaction, Kind == KingdomFoundingKind.VillageCharter
								? VillageFaction : null) && SafeClearReceipt(Basin);
					}
					else
					{
						PoisonReceipt(Basin, vessel);
					}
					return Result(restored && cleaned
							? KingdomFoundingOutcome.CompensatedFailure
							: KingdomFoundingOutcome.RecoverableFailure,
						restored
							? KingdomFoundingWaterDisposition.RestoredExactly
							: KingdomFoundingWaterDisposition.RestorationFailed,
						KingdomFoundingProjection.Water,
						"The basin did not yield the measured amount exactly.");
				}
				if (!CommittedSnapshotStillExact(Basin, vessel))
				{
					throw new InvalidOperationException(
						"The basin's committed liquid snapshot did not match its staged algebra.");
				}
				return Result(KingdomFoundingOutcome.Committed,
					KingdomFoundingWaterDisposition.Spent,
					KingdomFoundingProjection.Water);
			}
				catch (Exception ex)
				{
					bool restored = RestorePrePublication(Basin, vessel);
					bool cleaned = false;
					if (restored)
					{
						Basin.PendingPhase = KingdomFoundingPhase.None;
						cleaned = ClearExactReservationSet(Site, encodedAuthority,
							realmFaction, Kind == KingdomFoundingKind.VillageCharter
								? VillageFaction : null) && SafeClearReceipt(Basin);
				}
				else
				{
					PoisonReceipt(Basin, vessel);
				}
					return Result(restored && cleaned
						? KingdomFoundingOutcome.CompensatedFailure
						: KingdomFoundingOutcome.RecoverableFailure,
					restored
						? KingdomFoundingWaterDisposition.RestoredExactly
						: KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.Water, Describe(ex));
			}
		}

		private static KingdomFoundingResult Run(r_FounderBasin Basin,
			GameObject Actor, Zone Site)
		{
			KingdomFoundingReceiptNormalization normalization = NormalizeReceipt(Basin);
			if (normalization != KingdomFoundingReceiptNormalization.Pending)
			{
				return Result(normalization == KingdomFoundingReceiptNormalization.Quarantine
						? KingdomFoundingOutcome.RecoverableFailure
						: KingdomFoundingOutcome.Refused,
					normalization == KingdomFoundingReceiptNormalization.Quarantine
						? KingdomFoundingWaterDisposition.RestorationFailed
						: KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The founding receipt header is not resumable.");
			}
			LiquidVolume vessel = Basin?.ParentObject?.GetPart<LiquidVolume>();
			if (!ValidateReceiptPayload(Basin, null, vessel, out var payloadFailure))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"The founding receipt payload is malformed and was quarantined before identity lookup: " +
					payloadFailure);
			}
			if (Basin.PendingOwnerNonce != Basin.OwnerNonce ||
				Basin.PendingOwnerKind != KingdomFoundingOwnerKind.Basin)
			{
				// Ordinary copies are stripped during FinalizeCopy. A surviving mismatch cannot be
				// proven to be a clone rather than corruption of the paid original, so quarantine
				// without projecting, restoring, clearing, or touching global authority.
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"This basin does not own the founding receipt; its authority was quarantined.");
			}
			if (Basin.PendingPhase == KingdomFoundingPhase.WaterCommitted &&
				OriginalSnapshotStillExact(Basin, vessel) &&
				!TryFinishWaterCommit(Basin, vessel, out string waterFailure))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					Basin.PendingPhase == KingdomFoundingPhase.RecoveryRequired
						? KingdomFoundingWaterDisposition.RestorationFailed
						: KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.Water, waterFailure);
			}
			if (Basin.PendingPhase == KingdomFoundingPhase.RecoveryRequired ||
				!CommittedSnapshotStillExact(Basin, vessel))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.Water,
					"The receipt-bound basin is not the exact committed liquid snapshot; it is quarantined.");
			}
			if (Basin == null || Actor == null || Site == null || Actor.CurrentZone != Site ||
				Site.ZoneID != Basin.PendingZoneID)
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.Water,
					"Return with this basin to the ground where the interrupted rite began.");
			}
			if (!ValidateReceiptPayload(Basin, Site, vessel, out payloadFailure))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.Water,
					"The founding site no longer matches the bounded receipt: " + payloadFailure);
			}
			string authority = Basin.PendingAuthority;
			bool complete = Basin.PendingPhase == KingdomFoundingPhase.Complete;
			string reservationVillage = Basin.PendingKind ==
				KingdomFoundingKind.VillageCharter ? Basin.PendingVillageFaction : null;
			if (!complete && !GlobalReservationMatches(authority) &&
				ReservationAbsentOrExact(authority, Basin.PendingRealmFaction,
					reservationVillage) && SiteReservationMatches(Site, authority) &&
				ExistingReservationOwnersMatch(Basin))
			{
				AcquireGlobalReservation(authority, Basin.PendingRealmFaction,
					reservationVillage);
			}
			if ((!complete && (!GlobalReservationMatches(authority) ||
					!SiteReservationMatches(Site, authority))) ||
				(complete && (!ReservationAbsentOrExact(authority, Basin.PendingRealmFaction,
					reservationVillage) ||
					(HasSiteReservation(Site) &&
					 !CompletedSiteReservationSubsetMatches(Site, Basin)))))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.Water,
					"The exact global or site founding reservation is missing or belongs to another transaction.");
			}

			// Structural receipt, water algebra, site, digest and authority are proved before any
			// live realm/faction identity is consulted.
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!KingdomFoundingTransactionRules.ReceiptBindingMatches(
				Basin.PendingOwnerNonce, Basin.OwnerNonce, Basin.PendingOwnerKind,
				Basin.PendingTransactionID, Basin.PendingRealmFaction, Basin.PendingName,
				system.KingdomFactionName, Basin.PendingKind))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"The founding receipt no longer binds its exact transaction and realm; it is quarantined.");
			}
			if (!ReceiptFactionCoherent(Basin, system))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.Identity,
					"The exact faction registry publication is incomplete or was replaced; recovery retained it.");
			}
			if (complete)
			{
				if (!CompletionObserved(Basin, Actor, Site, system))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Seal,
						"A Complete receipt did not have every live projection and seal; it was quarantined.");
				}
				if (!FinishReceipt(Basin, Site))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Seal,
						"The completed rite could not clear its exact reservation yet.");
				}
				return Result(KingdomFoundingOutcome.Committed,
					KingdomFoundingWaterDisposition.Spent, KingdomFoundingProjection.Seal);
			}
			if (Basin.PendingKind == KingdomFoundingKind.SecondCity)
			{
				bool publishedSecond = SecondPublished(system, Basin.PendingName, Site.ZoneID,
					Basin.PendingTransactionID) &&
					PublishedSecondAuthorityMatches(Site, authority) &&
					SiteReservationMatches(Site, authority);
				bool exactSecondSeat = SecondIsExactSeat(system, Basin.PendingName,
					Site.ZoneID, Basin.PendingTransactionID);
				bool exactSecondAway = SecondIsExactAway(system, Basin.PendingName,
					Site.ZoneID, Basin.PendingTransactionID);
				if (!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
					system.SettlementCount, KingdomSettlement.MaxSettlements,
					system.Away == null, exactSecondSeat, exactSecondAway, publishedSecond))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Water,
						"Another city now occupies this receipt's seat; the stale rite was quarantined without projection.");
				}
			}
			KingdomFoundingProjection projection = KingdomFoundingProjection.Water;
			try
			{
				switch (Basin.PendingKind)
				{
				case KingdomFoundingKind.FirstCity:
					PublishFirst(Basin, Actor, Site, ref projection);
					break;
				case KingdomFoundingKind.SecondCity:
					PublishSecond(Basin, Actor, Site, ref projection, Force: false);
					break;
				case KingdomFoundingKind.VillageCharter:
					PublishVillageCharter(Basin, Site, ref projection);
					break;
				default:
					throw new InvalidOperationException("The pending rite kind is not readable.");
				}
				Basin.PendingPhase = KingdomFoundingPhase.Complete;
				if (!CompletionObserved(Basin, Actor, Site, system) ||
					!FinishReceipt(Basin, Site))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Seal,
						"The rite is sealed, but its exact completion receipt remains for cleanup.");
				}
				return Result(KingdomFoundingOutcome.Committed,
					KingdomFoundingWaterDisposition.Spent, KingdomFoundingProjection.Seal);
			}
			catch (Exception ex)
			{
				bool published = DetectPublication(Basin, Site);
				if (published ||
					Basin.PendingPhase == KingdomFoundingPhase.PublicationCommitted)
				{
					Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery, projection, Describe(ex));
					}
					bool restored = RestoreOriginal(Basin, vessel, TrustCurrent: false);
					bool cleaned = false;
					if (restored)
					{
						Basin.PendingPhase = KingdomFoundingPhase.None;
						cleaned = ClearExactReservationSet(Site,
							Basin.PendingAuthority, Basin.PendingRealmFaction,
							Basin.PendingKind == KingdomFoundingKind.VillageCharter
								? Basin.PendingVillageFaction : null) &&
							SafeClearReceipt(Basin);
				}
				else
				{
					PoisonReceipt(Basin, vessel);
				}
					return Result(restored && cleaned
						? KingdomFoundingOutcome.CompensatedFailure
						: KingdomFoundingOutcome.RecoverableFailure,
					restored
						? KingdomFoundingWaterDisposition.RestoredExactly
						: KingdomFoundingWaterDisposition.RestorationFailed,
					projection, Describe(ex));
			}
		}

		/// <summary>Completes the one safe save cut between WaterCommitted intent and Drain.
		/// Only the exact original snapshot may retry removal; every third water graph poisons.</summary>
		private static bool TryFinishWaterCommit(r_FounderBasin Basin,
			LiquidVolume Vessel, out string Failure)
		{
			Failure = null;
			if (CommittedSnapshotStillExact(Basin, Vessel)) return true;
			if (Basin == null || Vessel == null ||
				Basin.PendingPhase != KingdomFoundingPhase.WaterCommitted ||
				!OriginalSnapshotStillExact(Basin, Vessel))
			{
				Failure = "The water-commit barrier does not retain its exact original snapshot.";
				return false;
			}
			try
			{
				int removed = KingdomLiquids.Drain(Vessel, KingdomRules.FoundingCostDrams);
				if (removed == KingdomRules.FoundingCostDrams &&
					CommittedSnapshotStillExact(Basin, Vessel)) return true;
			}
			catch (Exception ex)
			{
				if (CommittedSnapshotStillExact(Basin, Vessel)) return true;
				if (OriginalSnapshotStillExact(Basin, Vessel))
				{
					Failure = "The deferred water commit can retry: " + Describe(ex);
					return false;
				}
			}
			if (!OriginalSnapshotStillExact(Basin, Vessel)) PoisonReceipt(Basin, Vessel);
			Failure = "The deferred water commit did not yield the exact measured amount.";
			return false;
		}

		private static void PublishFirst(r_FounderBasin Basin, GameObject Actor, Zone Site,
			ref KingdomFoundingProjection Projection)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Faction faction = KingdomFounding.Found(Basin.PendingName, Site,
				Site.GetCell(Basin.PendingRiteX, Basin.PendingRiteY),
				Basin.PendingTransactionID, Basin.PendingAuthority);
			if (faction == null || !system.Founded ||
				system.KingdomFactionName != Basin.PendingName ||
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
			if (system.Away != null || system.SettlementCount != 1 ||
				system.SettlementName != Basin.PendingName)
			{
				throw new InvalidOperationException("The first settlement is not the realm's exact seat.");
			}
			Projection = KingdomFoundingProjection.Seat;

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
			bool targetIsExactAway = SecondIsExactAway(system, Basin.PendingName,
				Site.ZoneID, Basin.PendingTransactionID);
			if (!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				system.SettlementCount, KingdomSettlement.MaxSettlements,
				system.Away == null, targetIsExactSeat, targetIsExactAway, published) ||
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
			if (system.Away == null || system.SettlementCount != 2 ||
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
				Basin.PendingName + " was " + verb + " as " +
				KingdomSettlement.VocationClause(system.Vocation) + ", the second city of " +
				system.KingdomDisplayName + KingdomRules.RuinRestorationClause(restored),
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
			bool targetIsExactAway = SecondIsExactAway(System, Basin.PendingName,
				Site.ZoneID, Basin.PendingTransactionID);
			if (!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				System.SettlementCount, KingdomSettlement.MaxSettlements,
				System.Away == null, targetIsExactSeat, targetIsExactAway, published))
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

				bool awayIsNew = SecondIsExactAway(System, Basin.PendingName, Site.ZoneID,
					Basin.PendingTransactionID);
				if (!awayIsNew)
				{
					if (System.Away != null)
					{
						throw new InvalidOperationException("An unrelated Away city appeared before the second seat could publish.");
					}
					// Publish the new city into the open Away slot first. TrySeat then captures the
					// old seat and restores only this exact transaction city. If seat exchange is
					// interrupted, no old city was overwritten and zone activation can retry it.
					System.Away = founded;
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
			if (system.GetStanding(Basin.PendingVillageFaction) <
				KingdomRules.VillageCharterSealedStanding)
			{
				system.SetStanding(Basin.PendingVillageFaction,
					KingdomRules.VillageCharterSealedStanding);
			}
			if (system.GetStanding(Basin.PendingVillageFaction) <
				KingdomRules.VillageCharterSealedStanding)
			{
				throw new InvalidOperationException("The covenant standing projection was refused.");
			}
			Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;
			Projection = KingdomFoundingProjection.Identity;
			RecordChronicleOnce(system, Basin.PendingChronicleEventID,
				"you asked, and " +
				(Basin.PendingVillageDisplayName ?? Basin.PendingVillageFaction) +
				" agreed: their ground stays theirs, and a covenant now stands between them and " +
				system.KingdomDisplayName, Accomplishment: true, MuralText: null,
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
				Faction pendingFaction = Factions.GetIfExists(Basin.PendingName);
				return system.FirstIdentityMatches(Basin.PendingTransactionID, Site.ZoneID) ||
					(pendingFaction != null &&
					pendingFaction.GetIntProperty("PlayerKingdom") == 1 &&
					pendingFaction.GetIntProperty("Village") == 1 &&
					pendingFaction.GetStringProperty(PendingFactionTransactionProperty, null) ==
						Basin.PendingTransactionID &&
					pendingFaction.GetStringProperty(PendingFactionAuthorityProperty, null) ==
						Basin.PendingAuthority &&
					((system.Founded && system.KingdomFactionName == Basin.PendingName) ||
					 pendingFaction.GetIntProperty(PendingFactionProperty) == 1));
			case KingdomFoundingKind.SecondCity:
				return SiteReservationMatches(Site, Basin.PendingAuthority) &&
					PublishedSecondAuthorityMatches(Site, Basin.PendingAuthority);
			case KingdomFoundingKind.VillageCharter:
				Faction village = Factions.GetIfExists(Basin.PendingVillageFaction);
				return SiteReservationMatches(Site, Basin.PendingAuthority) &&
					system.Founded && !string.IsNullOrEmpty(Basin.PendingVillageFaction) &&
					FactionRegistryCoherent(Basin.PendingVillageFaction, village) &&
					village.GetIntProperty("Village") == 1 &&
					village.GetStringProperty(VillageReservationProperty, null) ==
						Basin.PendingAuthority &&
					system.GetStanding(Basin.PendingVillageFaction) >=
						KingdomRules.VillageCharterSealedStanding;
			default:
				return false;
			}
		}

		private static bool SecondPublished(KingdomSystem System, string Name, string ZoneID,
			string TransactionId)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(Name) ||
				string.IsNullOrEmpty(ZoneID) || System.SettlementCount < 2 ||
				!KingdomIdentityRules.TryMintSettlement(System.RealmId, TransactionId,
					out string expectedId, out KingdomIdentityFault identityFault))
			{
				return false;
			}
			if (System.SettlementName == Name && System.ClaimedZones.Contains(ZoneID) &&
				System.SeatedLaterIdentityMatches(expectedId, TransactionId, ZoneID))
			{
				return true;
			}
			return System.Away != null && System.Away.SettlementName == Name &&
				System.Away.ClaimedZones.Contains(ZoneID) &&
				System.LaterSettlementIdentityMatches(System.Away, expectedId,
					TransactionId, ZoneID);
		}

		/// <summary>A direct contender may retain locks after terminal seat loss only when
		/// immutable forward work exists for this exact transaction. Site reservation alone is
		/// reversible staging and never qualifies.</summary>
		private static bool DirectSecondHasForwardRedo(KingdomSystem System, Zone Site,
			KingdomFoundingAuthority Authority, string EncodedAuthority)
		{
			if (System == null || Site == null || string.IsNullOrEmpty(EncodedAuthority) ||
				Authority.Kind != KingdomFoundingKind.SecondCity ||
				Authority.ZoneID != Site.ZoneID ||
				Authority.RealmFaction != System.KingdomFactionName ||
				!KingdomIdentityRules.TryMintSettlement(System.RealmId,
					Authority.TransactionID, out string settlementId,
					out KingdomIdentityFault fault)) return false;
			if (PublishedSecondAuthorityMatches(Site, EncodedAuthority)) return true;
			bool pending = System.PendingSettlementId == settlementId &&
				System.PendingSettlementTransactionId == Authority.TransactionID &&
				System.PendingSettlementZoneId == Site.ZoneID &&
				System.PendingSettlementAuthority == EncodedAuthority;
			if (pending) return true;
			bool trade = KingdomTradeRules.BookUsable(System.TradeBook) &&
				System.TradeBook.RealmId == System.RealmId &&
				System.TradeBook.SettlementIds != null &&
				System.TradeBook.SettlementIds.Contains(settlementId);
			bool carry = KingdomLifecycleRules.CanOwnAuthority(System.CarryBook) &&
				System.CarryBook.RealmId == System.RealmId &&
				System.CarryBook.SettlementIds != null &&
				System.CarryBook.SettlementIds.Contains(settlementId);
			return trade || carry;
		}

		private static bool PublishedSecondAuthorityMatches(Zone Site, string Authority)
		{
			return Site != null && !string.IsNullOrEmpty(Authority) &&
				Site.GetZoneProperty(SecondPublicationAuthorityProperty, null) == Authority;
		}

		private static bool SecondIsExactSeat(KingdomSystem System, string Name, string ZoneID,
			string TransactionId)
		{
			string expected;
			KingdomIdentityFault fault;
			return System != null && !string.IsNullOrEmpty(Name) &&
				!string.IsNullOrEmpty(ZoneID) && System.SettlementName == Name &&
				System.ClaimedZones.Contains(ZoneID) &&
				KingdomIdentityRules.TryMintSettlement(System.RealmId, TransactionId,
					out expected, out fault) &&
				System.SeatedLaterIdentityMatches(expected, TransactionId, ZoneID);
		}

		private static bool SecondIsExactAway(KingdomSystem System, string Name, string ZoneID,
			string TransactionId)
		{
			string expected;
			KingdomIdentityFault fault;
			return System != null && System.Away != null && !string.IsNullOrEmpty(Name) &&
				!string.IsNullOrEmpty(ZoneID) && System.Away.SettlementName == Name &&
				System.Away.ClaimedZones != null && System.Away.ClaimedZones.Contains(ZoneID) &&
				KingdomIdentityRules.TryMintSettlement(System.RealmId, TransactionId,
					out expected, out fault) &&
				System.LaterSettlementIdentityMatches(System.Away, expected,
					TransactionId, ZoneID);
		}

		private static bool SeatSecond(KingdomSystem System, string Name, Zone Site,
			string Authority, string TransactionId)
		{
			if (System == null || Site == null ||
				!SiteReservationMatches(Site, Authority) ||
				!PublishedSecondAuthorityMatches(Site, Authority))
			{
				return false;
			}
			if (SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId))
			{
				return true;
			}
			if (System.ClaimedZones != null &&
				System.ClaimedZones.Contains(Site.ZoneID))
			{
				// Another seated city claiming the target ground is not this transaction and
				// cannot be displaced merely because the exact target waits in Away.
				return false;
			}
			if (!SecondIsExactAway(System, Name, Site.ZoneID, TransactionId))
			{
				return false;
			}
			KingdomSettlement exactAway = System.Away;
			KingdomSettlement preSeat;
			try
			{
				preSeat = System.Capture();
			}
			catch (Exception ex)
			{
				KingdomLog.Log("second founding pre-Capture retry failed: " + Describe(ex));
				return false;
			}
			try
			{
				if (System.TrySeat(Site) && SecondIsExactSeat(System, Name, Site.ZoneID,
					TransactionId))
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				KingdomLog.Log("second founding TrySeat retry: " + Describe(ex));
				if (SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId) &&
					ReferenceEquals(System.Away, exactAway))
				{
					System.Away = preSeat;
					return true;
				}
			}
			if (SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId))
			{
				if (ReferenceEquals(System.Away, exactAway))
				{
					System.Away = preSeat;
				}
				return System.Away != null && !ReferenceEquals(System.Away, exactAway);
			}
			// TrySeat may be fault-injected at Capture or Restore. Retry those exact operations
			// only while Away is still the same transaction city; never overwrite a newcomer.
			if (!ReferenceEquals(System.Away, exactAway) ||
				!SecondIsExactAway(System, Name, Site.ZoneID, TransactionId))
			{
				return false;
			}
			KingdomSettlement oldSeat = preSeat;
			if (!ReferenceEquals(System.Away, exactAway) ||
				!SecondIsExactAway(System, Name, Site.ZoneID, TransactionId))
			{
				return false;
			}
			try
			{
				System.Restore(exactAway);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("second founding Restore retry failed: " + Describe(ex));
				if (SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId) &&
					ReferenceEquals(System.Away, exactAway))
				{
					System.Away = oldSeat;
					return true;
				}
				return false;
			}
			// Restore is all-or-nothing by KingdomSettlement.WriteTo contract. Only after exact
			// city is seated do we publish captured old seat into Away.
			if (!SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId) ||
				!ReferenceEquals(System.Away, exactAway))
			{
				return false;
			}
			System.Away = oldSeat;
			return SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId) &&
				System.Away == oldSeat;
		}

		private static KingdomFoundingReceiptNormalization NormalizeReceipt(
			r_FounderBasin Basin)
		{
			if (Basin == null)
			{
				return KingdomFoundingReceiptNormalization.Clean;
			}
			Basin.TryReadRawHeader(out var rawKind, out var rawPhase,
				out var kindPresent, out var phasePresent);
			return KingdomFoundingTransactionRules.NormalizeRaw(kindPresent, rawKind,
				phasePresent, rawPhase, Basin.HasReceiptPayloadBeyondHeader);
		}

		private static bool ValidateReceiptPayload(r_FounderBasin Basin, Zone Site,
			LiquidVolume Vessel, out string Failure)
		{
			Failure = "";
			if (Basin == null || Basin.ParentObject == null ||
				!Basin.HasCompleteReceiptSchema)
			{
				Failure = "required receipt keys are missing";
				return false;
			}
			Basin.TryReadRawHeader(out var rawKind, out var rawPhase,
				out var kindPresent, out var phasePresent);
			bool chronicleRawPresent = Basin.TryReadRawChronicle(out var chronicleRaw);
			bool dispositionRawPresent = Basin.TryReadRawChronicleDisposition(
				out var dispositionRaw);
			if (!kindPresent || !phasePresent ||
				!KingdomFoundingTransactionRules.TryParseKind(rawKind, out var kind) ||
				!KingdomFoundingTransactionRules.TryParsePhase(rawPhase, out var phase) ||
				kind == KingdomFoundingKind.None ||
				!KingdomFoundingTransactionRules.IsValidPair(kind, phase) ||
				Basin.PendingKind != kind || Basin.PendingPhase != phase)
			{
				Failure = "kind/phase is unknown or contradictory";
				return false;
			}
			if (!KingdomFoundingTransactionRules.IsNonce(Basin.PendingTransactionID) ||
				!KingdomFoundingTransactionRules.IsNonce(Basin.PendingOwnerNonce) ||
				Basin.PendingOwnerKind != KingdomFoundingOwnerKind.Basin ||
				string.IsNullOrEmpty(Basin.PendingBasinID) ||
				Basin.PendingBasinID.Length > 256 ||
				Basin.PendingBasinID != Basin.ParentObject.ID ||
				string.IsNullOrEmpty(Basin.PendingRealmFaction) ||
				Basin.PendingRealmFaction.Length > 256 ||
				string.IsNullOrEmpty(Basin.PendingName) || Basin.PendingName.Length > 256 ||
				string.IsNullOrEmpty(Basin.PendingZoneID) || Basin.PendingZoneID.Length > 512 ||
				Basin.PendingRiteX < 0 || Basin.PendingRiteX > 255 ||
				Basin.PendingRiteY < 0 || Basin.PendingRiteY > 255 ||
				!KingdomFoundingTransactionRules.IsLowerHex(
					Basin.PendingPayloadDigest, 64) ||
				!chronicleRawPresent || (chronicleRaw != 0 && chronicleRaw != 1) ||
				Basin.PendingChronicleStage < 0 || Basin.PendingChronicleStage > 2 ||
				Basin.PendingChronicleRecorded !=
					(Basin.PendingChronicleStage == 2) ||
				((phase == KingdomFoundingPhase.None ||
				  phase == KingdomFoundingPhase.WaterCommitted) &&
				 Basin.PendingChronicleStage != 0) ||
				(phase == KingdomFoundingPhase.Complete &&
				 Basin.PendingChronicleStage != 2) ||
				Basin.PendingChronicleEventID != FoundingEventID(kind,
					Basin.PendingTransactionID, "chronicle"))
			{
				Failure = "identity, bounds, or outbox fields are malformed";
				return false;
			}
			int accomplishmentCount = CountAccomplishments(
				Basin.PendingChronicleEventID);
			if (!KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				Basin.PendingChronicleStage, dispositionRawPresent, dispositionRaw,
				accomplishmentCount,
				Options.GetOption("r_TAF_OptionChronicle") == "No",
				out var disposition, out var writeDisposition))
			{
				Failure = "chronicle journal disposition is malformed or ambiguous";
				return false;
			}
			if (!writeDisposition && Basin.PendingChronicleDisposition != disposition)
			{
				Failure = "chronicle journal disposition readback differs";
				return false;
			}
			if ((kind == KingdomFoundingKind.FirstCity &&
					(Basin.HasVocationField || Basin.HasVillageFactionField ||
					 Basin.HasVillageDisplayField ||
					 Basin.PendingRealmFaction != Basin.PendingName)) ||
				(kind == KingdomFoundingKind.SecondCity &&
					(!Basin.HasVocationField ||
					 !KingdomSettlement.IsKnownVocation(Basin.PendingVocation) ||
					 Basin.HasVillageFactionField || Basin.HasVillageDisplayField)) ||
				(kind == KingdomFoundingKind.VillageCharter &&
					(Basin.HasVocationField || !Basin.HasVillageFactionField ||
					 !Basin.HasVillageDisplayField ||
					 string.IsNullOrEmpty(Basin.PendingVillageFaction) ||
					 Basin.PendingVillageFaction.Length > 256 ||
					 string.IsNullOrEmpty(Basin.PendingVillageDisplayName) ||
					 Basin.PendingVillageDisplayName.Length > 256)))
			{
				Failure = "kind-specific payload fields are malformed";
				return false;
			}
			if (!Basin.TryGetOriginalComponents(out var original, out var originalEncoded) ||
				!Basin.TryGetCommittedComponents(out var committed, out var committedEncoded) ||
				EncodeComponents(original) != originalEncoded ||
				EncodeComponents(committed) != committedEncoded ||
				!KingdomFoundingTransactionRules.WaterAlgebraValid(
					Basin.PendingOriginalVolume, Basin.PendingOriginalMaxVolume,
					Basin.PendingCommittedVolume, Basin.PendingCommittedMaxVolume,
					KingdomRules.FoundingCostDrams,
					KingdomFoundingTransactionRules.ComponentsDescribePureWater(original,
						Basin.PendingOriginalVolume),
					KingdomFoundingTransactionRules.ComponentsDescribePureWater(committed,
						Basin.PendingCommittedVolume)))
			{
				Failure = "liquid components or volume algebra is malformed";
				return false;
			}
			string digest = KingdomFoundingTransactionRules.PayloadDigest(kind,
				Basin.PendingName, Basin.PendingVocation, Basin.PendingVillageFaction,
				Basin.PendingVillageDisplayName, Basin.PendingOriginalVolume,
				Basin.PendingOriginalMaxVolume, Basin.PendingCommittedVolume,
				Basin.PendingCommittedMaxVolume, originalEncoded, committedEncoded);
			if (digest != Basin.PendingPayloadDigest ||
				!KingdomFoundingTransactionRules.TryParseAuthority(
					Basin.PendingAuthority, out var authority) ||
				authority.Kind != kind || authority.TransactionID != Basin.PendingTransactionID ||
				authority.OwnerKind != Basin.PendingOwnerKind ||
				authority.OwnerNonce != Basin.PendingOwnerNonce ||
				authority.RealmFaction != Basin.PendingRealmFaction ||
				authority.ZoneID != Basin.PendingZoneID ||
				authority.RiteX != Basin.PendingRiteX ||
				authority.RiteY != Basin.PendingRiteY ||
				authority.PayloadDigest != digest)
			{
				Failure = "payload digest or authority tuple does not match";
				return false;
			}
			if (Site != null)
			{
				if (Site.ZoneID != Basin.PendingZoneID ||
					Site.GetCell(Basin.PendingRiteX, Basin.PendingRiteY) == null)
				{
					Failure = "site, rite coordinates, or site payload differs";
					return false;
				}
				bool siteReservationPresent = HasSiteReservation(Site);
				if (phase == KingdomFoundingPhase.Complete && siteReservationPresent &&
					!CompletedSiteReservationSubsetMatches(Site, Basin))
				{
					Failure = "completed site cleanup contains a foreign marker";
					return false;
				}
				if (phase != KingdomFoundingPhase.Complete &&
					(!TryReadSiteReservation(Site, out var siteAuthority, out var siteName,
						out var siteVocation, out var siteVillage, out var siteDisplay,
						out var siteTick) ||
					 KingdomFoundingTransactionRules.FormatAuthority(siteAuthority) !=
						Basin.PendingAuthority || siteName != Basin.PendingName ||
					 siteVocation != Basin.PendingVocation ||
					 siteVillage != Basin.PendingVillageFaction ||
					 siteDisplay != Basin.PendingVillageDisplayName))
				{
					Failure = "site reservation payload differs";
					return false;
				}
			}
			if (Vessel == null || Vessel.ParentObject != Basin.ParentObject)
			{
				Failure = "receipt vessel is missing or belongs to another object";
				return false;
			}
			// Legacy migration is itself a mutation. Defer it until every identity, water, site,
			// and vessel check has succeeded so a malformed receipt remains byte-for-byte intact.
			if (writeDisposition)
			{
				Basin.PendingChronicleDisposition = disposition;
				if (!Basin.TryReadRawChronicleDisposition(out var writtenDisposition) ||
					writtenDisposition != (int)disposition)
				{
					Failure = "chronicle journal disposition migration was not retained";
					return false;
				}
			}
			return true;
		}

		private static bool TryClearStagedReceipt(r_FounderBasin Basin, Zone Site)
		{
			LiquidVolume vessel = Basin?.ParentObject?.GetPart<LiquidVolume>();
			if (!ValidateReceiptPayload(Basin, null, vessel, out var failure) ||
				Basin.PendingPhase != KingdomFoundingPhase.None ||
				!OriginalSnapshotStillExact(Basin, vessel) ||
				Site == null || Site.ZoneID != Basin.PendingZoneID ||
				Site.GetCell(Basin.PendingRiteX, Basin.PendingRiteY) == null ||
				DetectPublication(Basin, Site) ||
				!ReservationAbsentOrExact(Basin.PendingAuthority,
					Basin.PendingRealmFaction,
					Basin.PendingKind == KingdomFoundingKind.VillageCharter
						? Basin.PendingVillageFaction : null) ||
				(HasSiteReservation(Site) &&
				 !SiteReservationMatches(Site, Basin.PendingAuthority)))
			{
				return false;
			}
			string authority = Basin.PendingAuthority;
			string village = Basin.PendingKind == KingdomFoundingKind.VillageCharter
				? Basin.PendingVillageFaction : null;
			if (!ClearExactReservationSet(Site, authority,
				Basin.PendingRealmFaction, village))
			{
				return false;
			}
			return SafeClearReceipt(Basin);
		}

		private static bool CompletionObserved(r_FounderBasin Basin, GameObject Actor,
			Zone Site, KingdomSystem System)
		{
			if (Basin == null || Site == null || System == null ||
				Basin.PendingPhase != KingdomFoundingPhase.Complete ||
				Site.ZoneID != Basin.PendingZoneID ||
				Basin.PendingChronicleStage != 2 ||
				Basin.PendingChronicleEventID != FoundingEventID(Basin.PendingKind,
					Basin.PendingTransactionID, "chronicle") ||
				!ChronicleAccomplishmentObserved(Basin.PendingChronicleEventID,
					Basin.PendingChronicleDisposition))
			{
				return false;
			}
			Faction realm = Factions.GetIfExists(Basin.PendingRealmFaction);
			if (!FactionRegistryCoherent(Basin.PendingRealmFaction, realm) ||
				realm.GetIntProperty("PlayerKingdom") != 1 ||
				realm.GetIntProperty("Village") != 1 || !System.Founded ||
				System.KingdomFactionName != Basin.PendingRealmFaction)
			{
				return false;
			}
			bool projected;
			switch (Basin.PendingKind)
			{
			case KingdomFoundingKind.FirstCity:
				projected = System.SettlementCount == 1 && System.Away == null &&
					System.FirstIdentityMatches(Basin.PendingTransactionID, Site.ZoneID) &&
					System.SettlementName == Basin.PendingName &&
					System.ClaimedZones.Contains(Site.ZoneID) &&
					Site.GetZoneProperty("faction", null) == Basin.PendingRealmFaction &&
					realm.HolyPlaces.Contains(Site.ZoneID) &&
					realm.GetIntProperty(PendingFactionProperty) == 0 &&
					realm.GetStringProperty(PendingFactionTransactionProperty, null) ==
						Basin.PendingTransactionID &&
					realm.GetStringProperty(PendingFactionAuthorityProperty, null) ==
						Basin.PendingAuthority &&
					Site.GetZoneProperty(ClaimChronicleEventProperty, null) ==
						FoundingEventID(KingdomFoundingKind.FirstCity,
							Basin.PendingTransactionID, "claim") &&
					Site.GetZoneProperty(ClaimChronicleStageProperty, null) == "2" &&
					Site.GetZoneProperty(ClaimChronicleDispositionProperty, null) ==
						((int)KingdomChronicleDisposition.Skipped).ToString() &&
					Site.GetZoneProperty(ClaimFoundingProperty, null) == "1";
				break;
			case KingdomFoundingKind.SecondCity:
				projected = System.SettlementCount == 2 &&
					System.TryProveSettledSecondCityTopology(out string _) &&
					PublishedSecondAuthorityMatches(Site, Basin.PendingAuthority) &&
					SecondIsExactSeat(System, Basin.PendingName, Site.ZoneID,
						Basin.PendingTransactionID) &&
					System.Away != null &&
					Site.GetZoneProperty(SecondChronicleDispositionProperty, null) ==
						((int)Basin.PendingChronicleDisposition).ToString() &&
					Site.GetZoneProperty("faction", null) == Basin.PendingRealmFaction &&
					realm.HolyPlaces.Contains(Site.ZoneID);
				break;
			case KingdomFoundingKind.VillageCharter:
				Faction village = Factions.GetIfExists(Basin.PendingVillageFaction);
				projected = FactionRegistryCoherent(Basin.PendingVillageFaction, village) &&
					village.GetIntProperty("Village") == 1 &&
					village.DisplayName == Basin.PendingVillageDisplayName &&
					Site.GetZoneProperty("faction", null) == Basin.PendingVillageFaction &&
					System.GetStanding(Basin.PendingVillageFaction) >=
						KingdomRules.VillageCharterSealedStanding;
				break;
			default:
				return false;
			}
			if (!projected || !EnsureAbility(Actor) ||
				(Basin.PendingKind != KingdomFoundingKind.VillageCharter &&
				 !EnsurePlacement(System, Site, Basin.PendingRiteX, Basin.PendingRiteY)))
			{
				return false;
			}
			string failure;
			return Basin.PendingKind == KingdomFoundingKind.FirstCity
				? KingdomSeal.TryFoundingCompleted(out failure)
				: KingdomSeal.TryStageSemanticSnapshot(
					Basin.PendingKind == KingdomFoundingKind.SecondCity
						? "second founding completion observation"
						: "village charter completion observation", out failure);
		}

		private static bool FinishReceipt(r_FounderBasin Basin, Zone Site)
		{
			if (Basin == null || Site == null ||
				Basin.PendingPhase != KingdomFoundingPhase.Complete)
			{
				return false;
			}
			string authority = Basin.PendingAuthority;
			string village = Basin.PendingKind == KingdomFoundingKind.VillageCharter
				? Basin.PendingVillageFaction : null;
			if (!ReservationAbsentOrExact(authority, Basin.PendingRealmFaction, village) ||
				(HasSiteReservation(Site) &&
				 !CompletedSiteReservationSubsetMatches(Site, Basin)))
			{
				return false;
			}
			if (KingdomFoundingTransactionRules.TryParseAuthority(authority,
				out var parsed) && parsed.Kind == KingdomFoundingKind.SecondCity)
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				if (system == null || !system.Founded ||
					system.KingdomFactionName != parsed.RealmFaction ||
					!system.TryProveSettledSecondCityTopology(out string _)) return false;
			}
			if (!ReleaseGlobalReservation(authority, Basin.PendingRealmFaction, village))
			{
				return false;
			}
			if (HasSiteReservation(Site) &&
				!ClearCompletedSiteReservation(Site, Basin))
			{
				return false;
			}
			return !HasSiteReservation(Site) &&
				GlobalReservationMarkersAbsent(Basin.PendingRealmFaction, village) &&
				SafeClearReceipt(Basin);
		}

		private static bool SafeClearReceipt(r_FounderBasin Basin)
		{
			try
			{
				Basin?.ClearPendingRite();
				if (Basin == null)
				{
					return true;
				}
				Basin.TryReadRawHeader(out var rawKind, out var rawPhase,
					out var kindPresent, out var phasePresent);
				return !kindPresent && !phasePresent && !Basin.HasAnyReceiptState &&
					!Basin.HasReceiptPayloadBeyondHeader &&
					Basin.PendingKind == KingdomFoundingKind.None &&
					Basin.PendingPhase == KingdomFoundingPhase.None;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("founding receipt cleanup failed: " + Describe(ex));
				return false;
			}
		}

		/// <summary>Both engine registries must name the same faction exactly once.</summary>
		internal static bool FactionRegistryCoherent(string Name, Faction Faction)
		{
			if (string.IsNullOrEmpty(Name) || Faction == null || Faction.Name != Name ||
				!ReferenceEquals(Factions.GetIfExists(Name), Faction))
			{
				return false;
			}
			int exactReferences = 0;
			int matchingNames = 0;
			foreach (Faction listed in Factions.GetList())
			{
				if (ReferenceEquals(listed, Faction))
				{
					exactReferences++;
				}
				if (listed != null && listed.Name == Name)
				{
					matchingNames++;
				}
			}
			return exactReferences == 1 && matchingNames == 1;
		}

		internal static bool FactionNameAvailable(string Name)
		{
			if (string.IsNullOrEmpty(Name) || Factions.Exists(Name))
			{
				return false;
			}
			foreach (Faction listed in Factions.GetList())
			{
				if (listed != null && listed.Name == Name)
				{
					return false;
				}
			}
			return true;
		}

		private static bool ReceiptFactionCoherent(r_FounderBasin Basin,
			KingdomSystem System)
		{
			if (Basin == null || System == null)
			{
				return false;
			}
			Faction faction = Factions.GetIfExists(Basin.PendingRealmFaction);
			if (Basin.PendingKind == KingdomFoundingKind.FirstCity && faction == null)
			{
				return !System.Founded;
			}
			if (Basin.PendingKind == KingdomFoundingKind.FirstCity &&
				!FactionRegistryCoherent(Basin.PendingRealmFaction, faction) &&
				!RepairPendingFactionRegistry(Basin.PendingRealmFaction,
					Basin.PendingTransactionID, Basin.PendingAuthority))
			{
				return false;
			}
			faction = Factions.GetIfExists(Basin.PendingRealmFaction);
			if (!FactionRegistryCoherent(Basin.PendingRealmFaction, faction) ||
				faction.GetIntProperty("PlayerKingdom") != 1 ||
				faction.GetIntProperty("Village") != 1)
			{
				return false;
			}
			if (Basin.PendingKind == KingdomFoundingKind.FirstCity)
			{
				return faction.GetStringProperty(PendingFactionTransactionProperty, null) ==
						Basin.PendingTransactionID &&
					faction.GetStringProperty(PendingFactionAuthorityProperty, null) ==
						Basin.PendingAuthority &&
					(faction.GetIntProperty(PendingFactionProperty) == 1 ||
					 (System.Founded && System.KingdomFactionName == Basin.PendingRealmFaction));
			}
			string realmReservation = faction.GetStringProperty(
				RealmReservationProperty, null);
			if (!System.Founded || System.KingdomFactionName != Basin.PendingRealmFaction ||
				(realmReservation != Basin.PendingAuthority &&
				 !(Basin.PendingPhase == KingdomFoundingPhase.Complete &&
				   string.IsNullOrEmpty(realmReservation))))
			{
				return false;
			}
			if (Basin.PendingKind == KingdomFoundingKind.VillageCharter)
			{
				Faction village = Factions.GetIfExists(Basin.PendingVillageFaction);
				string villageReservation = village?.GetStringProperty(
					VillageReservationProperty, null);
				return FactionRegistryCoherent(Basin.PendingVillageFaction, village) &&
					village.GetIntProperty("Village") == 1 &&
					village.DisplayName == Basin.PendingVillageDisplayName &&
					(villageReservation == Basin.PendingAuthority ||
					 (Basin.PendingPhase == KingdomFoundingPhase.Complete &&
					  string.IsNullOrEmpty(villageReservation)));
			}
			return true;
		}

		private static bool ExistingReservationOwnersMatch(r_FounderBasin Basin)
		{
			if (Basin == null)
			{
				return false;
			}
			Faction realm = Factions.GetIfExists(Basin.PendingRealmFaction);
			if (Basin.PendingKind == KingdomFoundingKind.FirstCity)
			{
				return realm == null ||
					(realm.GetIntProperty("PlayerKingdom") == 1 &&
					 realm.GetIntProperty("Village") == 1 &&
					 realm.GetStringProperty(PendingFactionTransactionProperty, null) ==
						Basin.PendingTransactionID &&
					 realm.GetStringProperty(PendingFactionAuthorityProperty, null) ==
						Basin.PendingAuthority &&
					 realm.GetStringProperty(RealmReservationProperty, null) ==
						Basin.PendingAuthority);
			}
			if (!FactionRegistryCoherent(Basin.PendingRealmFaction, realm) ||
				realm.GetIntProperty("PlayerKingdom") != 1 ||
				realm.GetIntProperty("Village") != 1 ||
				realm.GetStringProperty(RealmReservationProperty, null) !=
					Basin.PendingAuthority)
			{
				return false;
			}
			if (Basin.PendingKind != KingdomFoundingKind.VillageCharter)
			{
				return true;
			}
			Faction village = Factions.GetIfExists(Basin.PendingVillageFaction);
			return FactionRegistryCoherent(Basin.PendingVillageFaction, village) &&
				village.GetIntProperty("Village") == 1 &&
				village.DisplayName == Basin.PendingVillageDisplayName &&
				village.GetStringProperty(VillageReservationProperty, null) ==
					Basin.PendingAuthority;
		}

		internal static bool RepairPendingFactionRegistry(string Name, string Transaction,
			string Authority)
		{
			Faction faction = Factions.GetIfExists(Name);
			if (faction == null || string.IsNullOrEmpty(Transaction) ||
				string.IsNullOrEmpty(Authority) || faction.Name != Name ||
				faction.GetIntProperty("PlayerKingdom") != 1 ||
				faction.GetIntProperty("Village") != 1 ||
				faction.GetIntProperty(PendingFactionProperty) != 1 ||
				faction.GetStringProperty(PendingFactionTransactionProperty, null) != Transaction ||
				faction.GetStringProperty(PendingFactionAuthorityProperty, null) != Authority)
			{
				return false;
			}
			int exact = 0;
			int sameName = 0;
			foreach (Faction listed in Factions.GetList())
			{
				if (ReferenceEquals(listed, faction))
				{
					exact++;
				}
				if (listed != null && listed.Name == Name)
				{
					sameName++;
				}
			}
			if (exact == 1 && sameName == 1)
			{
				return true;
			}
			if (exact != 0 || sameName != 0 || FactionListField == null)
			{
				return false;
			}
			try
			{
				List<Faction> list = FactionListField.GetValue(null) as List<Faction>;
				if (list == null || list.Contains(faction))
				{
					return false;
				}
				list.Add(faction);
				return FactionRegistryCoherent(Name, faction);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("pending faction list repair failed: " + Describe(ex));
				return false;
			}
		}

		/// <summary>Durable founding/claim outbox. Official and outsider registers are locally
		/// compensated together. Journal accomplishments use the event id as secretId, so a throw
		/// after insertion is observed and never inserted twice on retry.</summary>
		internal static void RecordChronicleOnce(KingdomSystem System, string EventID,
			string Text, bool Accomplishment, string MuralText,
			Func<int> ReadStage, Action<int> WriteStage,
			Func<int?> ReadDisposition, Action<int> WriteDisposition,
			Func<bool> ValidateAuthority = null)
		{
			if (System == null || string.IsNullOrEmpty(EventID) || EventID.Length > 160 ||
				string.IsNullOrEmpty(Text) || ReadStage == null || WriteStage == null ||
				ReadDisposition == null || WriteDisposition == null)
			{
				throw new InvalidOperationException("The chronicle outbox identity is malformed.");
			}
			int stage = ReadStage();
			int existing = CountAccomplishments(EventID);
			int? rawDisposition = ReadDisposition();
			if (!KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(stage,
				rawDisposition.HasValue, rawDisposition.GetValueOrDefault(), existing,
				!Accomplishment || Options.GetOption("r_TAF_OptionChronicle") == "No",
				out var disposition, out var needsDispositionWrite))
			{
				throw new InvalidOperationException(
					"The chronicle outbox stage or journal disposition is malformed.");
			}
			if (needsDispositionWrite)
			{
				WriteDisposition((int)disposition);
				if (ReadDisposition() != (int)disposition)
				{
					throw new InvalidOperationException(
						"The migrated chronicle disposition was not retained.");
				}
			}
			if (stage == 0)
			{
				List<string> official = System.ChronicleEntries == null
					? null : new List<string>(System.ChronicleEntries);
				List<string> outsider = System.OutsiderEntries == null
					? null : new List<string>(System.OutsiderEntries);
				try
				{
					KingdomChronicle.Record(System, Text, Accomplishment: false,
						MuralText: null);
					WriteStage(1);
					if (ReadStage() != 1)
					{
						throw new InvalidOperationException(
							"The chronicle register outbox stage was not retained.");
					}
					stage = 1;
				}
				catch
				{
					RestoreList(System.ChronicleEntries, official);
					RestoreList(System.OutsiderEntries, outsider);
					throw;
				}
			}
			if (stage == 1)
			{
				existing = CountAccomplishments(EventID);
				rawDisposition = ReadDisposition();
				if (!KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(stage,
					rawDisposition.HasValue, rawDisposition.GetValueOrDefault(), existing,
					!Accomplishment || Options.GetOption("r_TAF_OptionChronicle") == "No",
					out disposition, out needsDispositionWrite))
				{
					throw new InvalidOperationException(
						"The chronicle outbox disposition changed incompatibly.");
				}
				if (needsDispositionWrite)
				{
					WriteDisposition((int)disposition);
				}
				if (disposition == KingdomChronicleDisposition.None)
				{
					disposition = Accomplishment &&
						Options.GetOption("r_TAF_OptionChronicle") != "No"
						? KingdomChronicleDisposition.Required
						: KingdomChronicleDisposition.Skipped;
					WriteDisposition((int)disposition);
					if (ReadDisposition() != (int)disposition)
					{
						throw new InvalidOperationException(
							"The chronicle journal decision was not retained before callback.");
					}
				}
				if (disposition == KingdomChronicleDisposition.Required)
				{
					existing = CountAccomplishments(EventID);
					if (existing > 1)
					{
						throw new InvalidOperationException(
							"The founding journal event id already appears more than once.");
					}
					if (existing == 0)
					{
						bool wantsMural = !string.IsNullOrEmpty(MuralText);
						int callbackStage = ReadStage();
						int? callbackDisposition = ReadDisposition();
						if (ValidateAuthority != null && !ValidateAuthority())
						{
							throw new InvalidOperationException(
								"The founding authority changed before the journal callback.");
						}
						try
						{
							JournalAPI.AddAccomplishment(Text.Capitalize() + ".",
								wantsMural ? MuralText : null, null, null, "general",
								MuralCategory.CreatesSomething,
								wantsMural ? MuralWeight.Medium : MuralWeight.Nil,
								EventID, -1L);
						}
						catch
						{
							if ((ValidateAuthority != null && !ValidateAuthority()) ||
								CountAccomplishments(EventID) != 1)
							{
								throw;
							}
						}
						if (ReadStage() != callbackStage ||
							ReadDisposition() != callbackDisposition)
						{
							throw new InvalidOperationException(
								"The chronicle receipt changed during the journal callback.");
						}
						if (ValidateAuthority != null && !ValidateAuthority())
						{
							throw new InvalidOperationException(
								"The founding authority changed during the journal callback.");
						}
					}
					if (CountAccomplishments(EventID) != 1)
					{
						throw new InvalidOperationException(
							"The founding journal event was not retained exactly once.");
					}
					disposition = KingdomChronicleDisposition.Inserted;
					WriteDisposition((int)disposition);
					if (ReadDisposition() != (int)disposition)
					{
						throw new InvalidOperationException(
							"The inserted journal disposition was not retained.");
					}
				}
				else if (disposition == KingdomChronicleDisposition.Inserted)
				{
					if (CountAccomplishments(EventID) != 1)
					{
						throw new InvalidOperationException(
							"The inserted journal disposition lost its exact row.");
					}
				}
				else if (disposition == KingdomChronicleDisposition.Skipped)
				{
					if (CountAccomplishments(EventID) != 0)
					{
						throw new InvalidOperationException(
							"A skipped journal disposition unexpectedly has a row.");
					}
				}
				else
				{
					throw new InvalidOperationException(
						"The chronicle journal disposition is not terminal.");
				}
				if (ValidateAuthority != null && !ValidateAuthority())
				{
					throw new InvalidOperationException(
						"The founding authority changed before chronicle completion.");
				}
				WriteStage(2);
				if (ReadStage() != 2)
				{
					throw new InvalidOperationException(
						"The completed chronicle outbox stage was not retained.");
				}
			}
		}

		private static int CountAccomplishments(string EventID)
		{
			int count = 0;
			if (JournalAPI.Accomplishments == null)
			{
				return 0;
			}
			foreach (JournalAccomplishment accomplishment in JournalAPI.Accomplishments)
			{
				if (accomplishment != null && accomplishment.ID == EventID)
				{
					count++;
				}
			}
			return count;
		}

		private static bool ChronicleAccomplishmentObserved(string EventID,
			KingdomChronicleDisposition Disposition)
		{
			int count = CountAccomplishments(EventID);
			return KingdomFoundingTransactionRules.ChronicleDispositionValid(2,
				Disposition, count);
		}

		/// <summary>Atomic compatibility helper for non-transaction civic events.</summary>
		internal static void RecordChronicleAtomically(KingdomSystem System, string Text,
			bool Accomplishment = false, string MuralText = null)
		{
			if (System == null)
			{
				throw new InvalidOperationException("No kingdom chronicle exists for this founding.");
			}
			List<string> official = System.ChronicleEntries == null
				? null : new List<string>(System.ChronicleEntries);
			List<string> outsider = System.OutsiderEntries == null
				? null : new List<string>(System.OutsiderEntries);
			try
			{
				KingdomChronicle.Record(System, Text, Accomplishment, MuralText);
			}
			catch
			{
				RestoreList(System.ChronicleEntries, official);
				RestoreList(System.OutsiderEntries, outsider);
				throw;
			}
		}

		private static void RestoreList(List<string> Target, List<string> Snapshot)
		{
			if (Target == null || Snapshot == null)
			{
				return;
			}
			Target.Clear();
			Target.AddRange(Snapshot);
		}

		private static bool EnsureAbility(GameObject Actor)
		{
			if (Actor == null || !GameObject.Validate(Actor))
			{
				return false;
			}
			KingdomCharterPart charter = Actor.RequirePart<KingdomCharterPart>();
			charter.EnsureAbility();
			return Actor.GetActivatedAbilityByCommand(KingdomCharterPart.COMMAND) != null;
		}

		private static bool EnsurePlacement(KingdomSystem System, Zone Site, int RiteX, int RiteY)
		{
			Cell rite = Site?.GetCell(RiteX, RiteY);
			if (System == null || Site == null || rite == null)
			{
				return false;
			}
			Site.SetZoneProperty(KingdomPlots.RiteXProperty, RiteX.ToString());
			Site.SetZoneProperty(KingdomPlots.RiteYProperty, RiteY.ToString());
			if (!KingdomPlots.TryRiteGround(Site, out var readX, out var readY) ||
				readX != RiteX || readY != RiteY)
			{
				return false;
			}
			if (!KingdomPlots.TrySurveyedHeart(Site, out var survey))
			{
				if (!KingdomPlots.SurveyHeart(System, Site, RiteX, RiteY) ||
					!KingdomPlots.TrySurveyedHeart(Site, out survey))
				{
					return false;
				}
			}
			return EnsureMark(rite, KingdomPlots.HeartRelicBlueprint,
					KingdomPlots.HeartRelicProperty) &&
				EnsureMark(Site.GetCell(survey.X1, survey.Y1), KingdomPlots.SurveyStakeBlueprint,
					KingdomPlots.HeartStakeProperty) &&
				EnsureMark(Site.GetCell(survey.X2, survey.Y1), KingdomPlots.SurveyStakeBlueprint,
					KingdomPlots.HeartStakeProperty) &&
				EnsureMark(Site.GetCell(survey.X1, survey.Y2), KingdomPlots.SurveyStakeBlueprint,
					KingdomPlots.HeartStakeProperty) &&
				EnsureMark(Site.GetCell(survey.X2, survey.Y2), KingdomPlots.SurveyStakeBlueprint,
					KingdomPlots.HeartStakeProperty);
		}

		private static bool EnsureMark(Cell Cell, string Blueprint, string Property)
		{
			if (Cell == null)
			{
				return false;
			}
			foreach (GameObject item in Cell.Objects)
			{
				if (item.GetIntProperty(Property) == 1 && item.CurrentCell == Cell)
				{
					return true;
				}
			}
			GameObject placed = GameObject.Create(Blueprint);
			if (placed == null)
			{
				return false;
			}
			placed.SetIntProperty(Property, 1);
			Cell.AddObject(placed);
			if (placed.CurrentCell == Cell && Cell.Objects.Contains(placed))
			{
				return true;
			}
			try
			{
				placed.Obliterate();
			}
			catch
			{
			}
			return false;
		}

		internal static string FoundingEventID(KingdomFoundingKind Kind,
			string TransactionID, string Lane)
		{
			if (!KingdomFoundingTransactionRules.IsKnownKind(Kind) ||
				Kind == KingdomFoundingKind.None ||
				!KingdomFoundingTransactionRules.IsNonce(TransactionID) ||
				string.IsNullOrEmpty(Lane) || Lane.Length > 32)
			{
				return null;
			}
			for (int i = 0; i < Lane.Length; i++)
			{
				char c = Lane[i];
				if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
				{
					return null;
				}
			}
			return "taf:founding:v1:" + ((int)Kind) + ":" + TransactionID + ":" + Lane;
		}

		private static string EncodeComponents(Dictionary<string, int> Components)
		{
			if (Components == null)
			{
				return null;
			}
			List<string> keys = new List<string>(Components.Keys);
			keys.Sort(StringComparer.Ordinal);
			System.Text.StringBuilder encoded = new System.Text.StringBuilder();
			foreach (string key in keys)
			{
				if (encoded.Length > 0)
				{
					encoded.Append(';');
				}
				encoded.Append(Convert.ToBase64String(
					System.Text.Encoding.UTF8.GetBytes(key ?? "")))
					.Append(':').Append(Components[key]);
			}
			return encoded.ToString();
		}

		private static bool OriginalSnapshotStillExact(r_FounderBasin Basin,
			LiquidVolume Vessel)
		{
			return Basin != null && Vessel != null &&
				Vessel.Volume == Basin.PendingOriginalVolume &&
				Vessel.MaxVolume == Basin.PendingOriginalMaxVolume &&
				Same(Vessel.ComponentLiquids, Basin.PendingOriginalComponents);
		}

		private static bool CommittedSnapshotStillExact(r_FounderBasin Basin,
			LiquidVolume Vessel)
		{
			return Basin != null && Vessel != null &&
				Vessel.Volume == Basin.PendingCommittedVolume &&
				Vessel.MaxVolume == Basin.PendingCommittedMaxVolume &&
				Same(Vessel.ComponentLiquids, Basin.PendingCommittedComponents);
		}

		private static void PoisonReceipt(r_FounderBasin Basin, LiquidVolume Vessel)
		{
			if (Basin == null)
			{
				return;
			}
			// Never rewrite the paid snapshot to fit corrupt live water. Its strict algebra is
			// the only evidence a later recovery may trust.
			Basin.PendingPhase = KingdomFoundingPhase.RecoveryRequired;
		}

		private static bool RestorePrePublication(r_FounderBasin Basin,
			LiquidVolume Vessel)
		{
			if (OriginalSnapshotStillExact(Basin, Vessel))
			{
				return true;
			}
			return CommittedSnapshotStillExact(Basin, Vessel) &&
				RestoreOriginal(Basin, Vessel, TrustCurrent: false);
		}

		private static bool RestoreOriginal(r_FounderBasin Basin, LiquidVolume Vessel,
			bool TrustCurrent)
		{
			if (Basin == null || Vessel == null)
			{
				return false;
			}
			if (!TrustCurrent && !CommittedSnapshotStillExact(Basin, Vessel))
			{
				return false;
			}
			try
			{
				Vessel.MaxVolume = Basin.PendingOriginalMaxVolume;
				Vessel.Volume = Basin.PendingOriginalVolume;
				Vessel.ComponentLiquids = Copy(Basin.PendingOriginalComponents);
				Vessel.Update();
				return Vessel.MaxVolume == Basin.PendingOriginalMaxVolume &&
					Vessel.Volume == Basin.PendingOriginalVolume &&
					Same(Vessel.ComponentLiquids, Basin.PendingOriginalComponents);
			}
			catch
			{
				return Vessel.MaxVolume == Basin.PendingOriginalMaxVolume &&
					Vessel.Volume == Basin.PendingOriginalVolume &&
					Same(Vessel.ComponentLiquids, Basin.PendingOriginalComponents);
			}
		}

		private static Dictionary<string, int> Copy(Dictionary<string, int> Source)
		{
			return Source == null
				? new Dictionary<string, int>()
				: new Dictionary<string, int>(Source);
		}

		private static bool Same(Dictionary<string, int> A, Dictionary<string, int> B)
		{
			if (ReferenceEquals(A, B))
			{
				return true;
			}
			if (A == null || B == null || A.Count != B.Count)
			{
				return false;
			}
			foreach (KeyValuePair<string, int> item in A)
			{
				if (!B.TryGetValue(item.Key, out var value) || value != item.Value)
				{
					return false;
				}
			}
			return true;
		}

		private static KingdomFoundingResult Result(KingdomFoundingOutcome Outcome,
			KingdomFoundingWaterDisposition Water, KingdomFoundingProjection Projection,
			string Failure = null)
		{
			return KingdomFoundingResult.From(Outcome, Water, Projection, Failure);
		}

		private static string Describe(Exception Exception)
		{
			return Exception == null || string.IsNullOrEmpty(Exception.Message)
				? "The engine refused the founding projection."
				: Exception.Message;
		}
	}
}
