using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Engine boundary for exact raw per-zone Reach observations.</summary>
	internal static class KingdomReachObservationRuntime
	{
		// Frozen storage key: changing it would hide rp1 receipts before their exact v2 migration.
		// SourceRevision and payload prefix, not this property name, identify the receipt schema.
		internal const string PropertyName = "r_TAF_ReachObservation_v1";
		private const int MaxRevocationZones = 4096;

		internal static int Amount(KingdomSystem System, string ZoneId, string SettlementId,
			string Kind, bool RealmBand, long CurrentTick)
		{
			if (!TryBinding(System, ZoneId, SettlementId, out string realm, out string owner)
				|| !TryRaw(ZoneId, out bool present, out object raw) || !present
				|| !TryReadReceipt(raw, realm, SettlementId, ZoneId, owner, CurrentTick,
					out KingdomZoneObservationReceipt receipt)
				|| !KingdomReachObservationRules.TryDecodeVersionedPayload(
					receipt.SourceRevision, receipt.Payload,
					out int[] _, out int[] _, out string _)) return 0;
			return KingdomReachObservationRules.Amount(receipt.Payload, Kind, RealmBand);
		}

		internal static bool TryWrite(KingdomSystem System, Zone Zone,
			List<KindAmount> CityLifts, List<KindAmount> RealmLifts,
			IList<string> AuthorityRows, long Tick, out string Failure)
		{
			Failure = null;
			string zoneId = Zone?.ZoneID;
			string settlementId = System?.SettlementIdForOwnedZone(zoneId);
			if (The.Game == null || Zone == null || The.ZoneManager == null
				|| !ReferenceEquals(Zone, The.ZoneManager.ActiveZone) || Tick != The.Game.TimeTicks
				|| !TryBinding(System, zoneId, settlementId, out string realm, out string owner))
				return Fail("active Reach observation lacks exact realm-ground authority", out Failure);
			if (!KingdomReachObservationRules.SameKindOrder(KingdomReachRules.LiftOrder)
				|| !KingdomReachObservationRules.TryAuthorityDigest(AuthorityRows,
					out string authorityDigest))
				return Fail("Reach source authority is malformed or over-bound", out Failure);
			int[] city = Values(CityLifts), other = Values(RealmLifts);
			if (city == null || other == null
				|| !KingdomReachObservationRules.TryEncodePayload(city, other,
					authorityDigest, out string payload)
				|| !KingdomZoneObservationRules.TryCreate(KingdomReachObservationRules.Purpose,
					realm, settlementId, zoneId, owner,
					KingdomReachObservationRules.SourceRevision, Tick, payload,
					out KingdomZoneObservationReceipt receipt)
				|| !KingdomZoneObservationCodec.TryEncode(receipt, out string wire))
				return Fail("Reach observation could not form a canonical receipt", out Failure);
			// Activation removed the former receipt before physical work. Revoke again here so a
			// direct caller and every write/setter fault also leave no stale authority.
			if (!TryRevokeZone(zoneId, out Failure)) return false;
			try { Zone.SetZoneProperty(PropertyName, wire); }
			catch (Exception) { TryRemoveRaw(zoneId); return Fail(
				"Reach observation setter failed closed", out Failure); }
			if (!TryRaw(zoneId, out bool present, out object raw) || !present
				|| raw?.GetType() != typeof(string) || !string.Equals((string)raw, wire,
					StringComparison.Ordinal)
				|| !KingdomZoneObservationRules.TryReadExact(raw,
					KingdomReachObservationRules.Purpose, realm, settlementId, zoneId, owner,
					KingdomReachObservationRules.SourceRevision, Tick, out _))
			{
				TryRemoveRaw(zoneId);
				return Fail("Reach observation did not survive exact raw readback", out Failure);
			}
			return true;
		}

		internal static bool TryRevokeOwned(KingdomSystem System, out string Failure)
		{
			Failure = null;
			if (System == null || !System.TryExactSettlementIds(true,
				out List<string> _, out Failure)) return false;
			List<string> zones = new List<string>();
			for (int i = 0; i < (System.ClaimedZones?.Count ?? 0); i++)
			{
				if (zones.Count == MaxRevocationZones)
					return Fail("Reach revocation exceeds the zone bound", out Failure);
				zones.Add(System.ClaimedZones[i]);
			}
			List<KingdomSettlement> others = System.NonSeatSettlements();
			for (int s = 0; s < others.Count; s++)
				for (int i = 0; i < (others[s]?.ClaimedZones?.Count ?? 0); i++)
				{
					if (zones.Count == MaxRevocationZones)
						return Fail("Reach revocation exceeds the zone bound", out Failure);
					zones.Add(others[s].ClaimedZones[i]);
				}
			return TryRevokeZones(zones, out Failure);
		}

		internal static bool TryRevokeZones(IList<string> ZoneIds, out string Failure)
		{
			Failure = null;
			if (The.Game == null || The.ZoneManager?.ZoneProperties == null || ZoneIds == null
				|| ZoneIds.Count > MaxRevocationZones)
				return Fail("Reach revocation cannot bound the zone registry", out Failure);
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < ZoneIds.Count; i++)
			{
				string zone = ZoneIds[i];
				if (!KingdomZoneObservationRules.Text(zone,
					KingdomZoneObservationRules.MaxIdentityChars) || !seen.Add(zone))
					return Fail("Reach revocation found malformed or duplicate ground", out Failure);
				if (!TryRevokeZone(zone, out Failure)) return false;
			}
			return true;
		}

		internal static bool TryRevokeZone(string ZoneId, out string Failure)
		{
			Failure = null;
			if (The.Game == null || !KingdomZoneObservationRules.Text(ZoneId,
				KingdomZoneObservationRules.MaxIdentityChars)
				|| !KingdomReachObservationRules.SameKindOrder(KingdomReachRules.LiftOrder))
				return Fail("Reach revocation has no exact zone", out Failure);
			if (!TryRemoveRaw(ZoneId))
				return Fail("Reach receipt could not be removed exactly", out Failure);
			for (int i = 0; i < KingdomReachRules.LiftOrder.Length; i++)
			{
				string kind = KingdomReachRules.LiftOrder[i];
				string city = KingdomReach.CityStatePrefix + ZoneId + "_" + kind;
				string realm = KingdomReach.RealmStatePrefix + ZoneId + "_" + kind;
				try
				{
					The.Game.SetIntGameState(city, 0); The.Game.SetIntGameState(realm, 0);
					if (The.Game.GetIntGameState(city) != 0
						|| The.Game.GetIntGameState(realm) != 0)
						return Fail("legacy Reach state did not remain zero", out Failure);
				}
				catch (Exception)
				{
					return Fail("legacy Reach state could not be retired", out Failure);
				}
			}
			return true;
		}

		private static bool TryBinding(KingdomSystem System, string ZoneId,
			string SettlementId, out string RealmId, out string OwnerId)
		{
			RealmId = System?.RealmId; OwnerId = System?.KingdomFactionName;
			if (System == null || !System.Founded || !string.IsNullOrEmpty(System.IdentityFault)
				|| !KingdomZoneObservationRules.Text(RealmId, 512)
				|| !KingdomZoneObservationRules.Text(OwnerId, 512)
				|| !KingdomZoneObservationRules.Text(SettlementId, 512)
				|| !KingdomZoneObservationRules.Text(ZoneId, 512)
				|| !System.TryExactSettlementIds(true, out List<string> settlements, out string _)
				|| !settlements.Contains(SettlementId) || !System.OwnedZone(ZoneId)
				|| !string.Equals(System.SettlementIdForOwnedZone(ZoneId), SettlementId,
					StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool TryReadReceipt(object Raw, string RealmId, string SettlementId,
			string ZoneId, string OwnerId, long CurrentTick,
			out KingdomZoneObservationReceipt Receipt)
		{
			if (KingdomZoneObservationRules.TryReadExact(Raw,
				KingdomReachObservationRules.Purpose, RealmId, SettlementId, ZoneId, OwnerId,
				KingdomReachObservationRules.SourceRevision, CurrentTick, out Receipt)) return true;
			return KingdomZoneObservationRules.TryReadExact(Raw,
				KingdomReachObservationRules.Purpose, RealmId, SettlementId, ZoneId, OwnerId,
				KingdomReachObservationRules.LegacySourceRevision, CurrentTick, out Receipt);
		}

		private static int[] Values(List<KindAmount> Lifts)
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts);
			int[] values = new int[KingdomReachObservationRules.KindCount];
			for (int i = 0; i < values.Length; i++)
			{
				string kind = KingdomReachObservationRules.KindAt(i);
				for (int row = 0; row < character.Lifts.Count; row++)
					if (character.Lifts[row].Kind == kind)
					{ values[i] = character.Lifts[row].Amount; break; }
			}
			return values;
		}

		private static bool TryRaw(string ZoneId, out bool Present, out object Raw)
		{
			Present = false; Raw = null;
			Dictionary<string, Dictionary<string, object>> all = The.ZoneManager?.ZoneProperties;
			if (all == null) return false;
			if (!all.TryGetValue(ZoneId, out Dictionary<string, object> properties)) return true;
			if (properties == null) return false;
			Present = properties.TryGetValue(PropertyName, out Raw); return true;
		}

		private static bool TryRemoveRaw(string ZoneId)
		{
			Dictionary<string, Dictionary<string, object>> all = The.ZoneManager?.ZoneProperties;
			if (all == null) return false;
			if (!all.TryGetValue(ZoneId, out Dictionary<string, object> properties)) return true;
			if (properties == null) return false;
			properties.Remove(PropertyName); return !properties.ContainsKey(PropertyName);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
