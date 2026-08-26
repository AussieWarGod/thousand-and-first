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

	}
}
