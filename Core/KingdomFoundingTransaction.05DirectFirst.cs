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
			if (!KingdomPresentationRules.TryNormalizeName(Name, out Name,
					out string presentationFailure) || Site == null)
			{
				Failure = Site == null ? "The first city needs a site." : presentationFailure;
				return false;
			}
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Cell rite = The.Player?.CurrentCell;
			int riteX = rite != null && rite.ParentZone == Site ? rite.X : Site.Width / 2;
			int riteY = rite != null && rite.ParentZone == Site ? rite.Y : Site.Height / 2;
			KingdomFoundingAuthority authority;
			bool hasSite = HasSiteReservation(Site);
			Faction reservedFaction = null;
			if (hasSite)
			{
				if (!TryReadSiteReservation(Site, out authority, out var storedName,
					out var storedVocation, out var storedVillage, out var storedDisplay,
					out var storedTick) || authority.OwnerKind != KingdomFoundingOwnerKind.Direct ||
					authority.Kind != KingdomFoundingKind.FirstCity || storedName != Name ||
					!string.IsNullOrEmpty(storedVocation) || !string.IsNullOrEmpty(storedVillage) ||
					!string.IsNullOrEmpty(storedDisplay) ||
					!KingdomIdentityRules.FirstFactionKeyMatches(authority.RealmFaction,
						authority.TransactionID, Name, AllowLegacy: true) ||
					authority.ZoneID != Site.ZoneID || authority.PayloadDigest !=
						DirectPayloadDigest(authority.Kind, storedName, null, null, null))
				{
					Failure = "This ground carries a quarantined or foreign founding reservation.";
					return false;
				}
				reservedFaction = Factions.GetIfExists(authority.RealmFaction);
				riteX = authority.RiteX;
				riteY = authority.RiteY;
			}
			else if (TryFindDirectPendingFirstFaction(Name, Site, out reservedFaction,
				out authority))
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
				string transaction = Guid.NewGuid().ToString("N");
				if (!KingdomIdentityRules.TryMintRealm(transaction, out string factionId,
						out KingdomIdentityFault identityFault) ||
					!FactionNameAvailable(factionId))
				{
					Failure = "The direct founding could not reserve one unique internal realm key (" +
						identityFault + ").";
					return false;
				}
				authority = NewAuthority(KingdomFoundingKind.FirstCity,
					KingdomFoundingOwnerKind.Direct, transaction,
					Guid.NewGuid().ToString("N"), factionId, Site.ZoneID, riteX, riteY,
					DirectPayloadDigest(KingdomFoundingKind.FirstCity, Name, null, null, null));
			}
			string realmFaction = authority.RealmFaction;
			if (system.Founded && (system.KingdomFactionName != realmFaction ||
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
				!AcquireGlobalReservation(encoded, realmFaction, null))
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
				bool cleanedGlobal = ReleaseGlobalReservation(encoded, realmFaction, null);
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
				PendingRealmFaction = realmFaction,
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
					bool cleared = ClearExactReservationSet(Site, encoded, realmFaction, null);
					Failure = "The exact direct founding authority left its synchronous guard." +
						(cleared ? "" : " Its exact reservation remains pending cleanup.");
					return false;
			}
			try
			{
				KingdomFoundingProjection projection = KingdomFoundingProjection.Water;
				PublishFirst(carrier, The.Player, Site, ref projection);
				carrier.PendingPhase = KingdomFoundingPhase.Complete;
				Faction = Factions.GetIfExists(realmFaction);
				if (!ClearExactReservationSet(Site, encoded, realmFaction, null))
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
					if (!ClearExactReservationSet(Site, encoded, realmFaction, null))
					{
						Failure += " Exact direct reservation cleanup remains pending.";
					}
				}
				KingdomLog.Log("first founding remains recoverable: " + Failure);
				return false;
			}
		}

		/// <summary>Finds the one irreversible direct publication when its site marker was the
		/// interrupted write. Current factions are keyed by realm id, so display-name lookup is
		/// never authority; every candidate must re-prove its complete transaction tuple.</summary>
		private static bool TryFindDirectPendingFirstFaction(string DisplayName, Zone Site,
			out Faction Found, out KingdomFoundingAuthority Authority)
		{
			Found = null;
			Authority = default(KingdomFoundingAuthority);
			if (string.IsNullOrEmpty(DisplayName) || Site == null) return false;
			foreach (Faction candidate in Factions.GetList())
			{
				if (candidate == null || candidate.DisplayName != DisplayName ||
					candidate.GetIntProperty(PendingFactionProperty) != 1 ||
					candidate.GetIntProperty("PlayerKingdom") != 1 ||
					candidate.GetIntProperty("Village") != 1 ||
					!KingdomFoundingTransactionRules.TryParseAuthority(
						candidate.GetStringProperty(PendingFactionAuthorityProperty, null),
						out KingdomFoundingAuthority parsed) ||
					parsed.OwnerKind != KingdomFoundingOwnerKind.Direct ||
					parsed.Kind != KingdomFoundingKind.FirstCity ||
					parsed.RealmFaction != candidate.Name || parsed.ZoneID != Site.ZoneID ||
					!KingdomIdentityRules.FirstFactionKeyMatches(candidate.Name,
						parsed.TransactionID, DisplayName, AllowLegacy: true) ||
					parsed.TransactionID != candidate.GetStringProperty(
						PendingFactionTransactionProperty, null) ||
					parsed.PayloadDigest != DirectPayloadDigest(
						KingdomFoundingKind.FirstCity, DisplayName, null, null, null) ||
					candidate.GetStringProperty(RealmReservationProperty, null) !=
						KingdomFoundingTransactionRules.FormatAuthority(parsed) ||
					!FactionRegistryCoherent(candidate.Name, candidate)) continue;
				if (Found != null) return false;
				Found = candidate;
				Authority = parsed;
			}
			return Found != null;
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

	}
}
