using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		private bool Refuse(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}

		private static bool BoundedText(string Value)
		{
			return BoundedUtf8(Value, 4096, MaxTextBytes);
		}

		private static bool BoundedOpaque(byte[] Value)
		{
			return Value == null || Value.Length <= KingdomArchivedSettlementCodec.MaxPayloadBytes;
		}

		private static bool BoundedStandings(Dictionary<string, int> Value)
		{
			if (Value == null || Value.Count > 512) return false;
			foreach (KeyValuePair<string, int> row in Value)
				if (!BoundedUtf8(row.Key, 512, 2048)) return false;
			return true;
		}

		private static bool ValidCallback(KingdomRealmCallbackReceipt Value)
		{
			return Value != null && Value.Validate();
		}

		private static bool ValidCallbackEnvelope(KingdomRealmCallbackReceipt Value)
		{
			return Value != null &&
				Enum.IsDefined(typeof(KingdomRealmCallbackPhase), Value.Phase) &&
				Enum.IsDefined(typeof(KingdomRealmCallbackDisposition), Value.Disposition) &&
				Enum.IsDefined(typeof(KingdomRealmCallbackScope), Value.Scope) &&
				BoundedUtf8(Value.BeforeGraph, 64, 64) &&
				BoundedUtf8(Value.AfterGraph, 64, 64) &&
				BoundedUtf8(Value.BeforeArchiveGraph, 64, 64) &&
				BoundedUtf8(Value.AfterArchiveGraph, 64, 64) &&
				BoundedUtf8(Value.BeforeEffect, KingdomRealmCallbackReceipt.MaxEffectChars,
					KingdomRealmCallbackReceipt.MaxEffectChars * 4) &&
				BoundedUtf8(Value.AfterEffect, KingdomRealmCallbackReceipt.MaxEffectChars,
					KingdomRealmCallbackReceipt.MaxEffectChars * 4) &&
				BoundedUtf8(Value.ObservedEffect, KingdomRealmCallbackReceipt.MaxEffectChars,
					KingdomRealmCallbackReceipt.MaxEffectChars * 4);
		}

		private static bool ExactArchivedSettlements(string RealmId,
			KingdomSettlement Seat, KingdomSettlement Away, IList<string> ExpectedIds)
		{
			List<string> ids = new List<string>();
			if (!ArchivedSettlementMatches(RealmId, Seat, out string seatId)) return false;
			ids.Add(seatId);
			if (Away != null)
			{
				if (!ArchivedSettlementMatches(RealmId, Away, out string awayId)) return false;
				ids.Add(awayId);
			}
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, ids, out fault)) return false;
			ids.Sort(StringComparer.Ordinal);
			if (ExpectedIds == null || ids.Count != ExpectedIds.Count) return false;
			for (int i = 0; i < ids.Count; i++)
				if (!string.Equals(ids[i], ExpectedIds[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool TryArchivedRetainedIds(string RealmId,
			KingdomSettlement Seat, KingdomSettlement Away, KingdomSettlement Seceded,
			out List<string> Ids)
		{
			Ids = new List<string>();
			if (!ArchivedSettlementMatches(RealmId, Seat, out string seatId)) return false;
			Ids.Add(seatId);
			if (Away != null)
			{
				if (!ArchivedSettlementMatches(RealmId, Away, out string awayId)) return false;
				Ids.Add(awayId);
			}
			if (Seceded != null)
			{
				if (!ArchivedSettlementMatches(RealmId, Seceded, out string secededId))
					return false;
				Ids.Add(secededId);
			}
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, Ids, out fault)) return false;
			Ids.Sort(StringComparer.Ordinal);
			return true;
		}

		private static bool ExactCarrySettlementIds(KingdomCarryBook Book,
			IList<string> Expected)
		{
			if (Book?.SettlementIds == null || Expected == null ||
				Book.SettlementIds.Count != Expected.Count) return false;
			for (int i = 0; i < Expected.Count; i++)
				if (!string.Equals(Book.SettlementIds[i], Expected[i],
					StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ArchivedSettlementMatches(string RealmId,
			KingdomSettlement Settlement, out string SettlementId)
		{
			SettlementId = Settlement?.City?.SettlementId;
			KingdomIdentityFault fault;
			return Settlement != null && Settlement.ClaimedZones != null &&
				Settlement.ClaimedZones.Contains(Settlement.SettlementIdentityFirstClaimedZone) &&
				KingdomIdentityRules.ReproveSettlement(SettlementId, RealmId,
					Settlement.SettlementIdentityVersion, Settlement.SettlementIdentityOrigin,
					Settlement.SettlementIdentityTransactionId,
					Settlement.SettlementIdentityFoundedTick,
					Settlement.SettlementIdentityFirstClaimedZone, out fault) &&
				Settlement.LifecycleBook != null && !Settlement.LifecycleBook.LegacyIdentity &&
				string.Equals(Settlement.LifecycleBook.SettlementId, SettlementId,
					StringComparison.Ordinal) &&
				KingdomLifecycleRules.CanOwnAuthority(Settlement.LifecycleBook);
		}

		private static bool BoundedUtf8(string Value, int MaxChars, int MaxBytes)
		{
			if (Value == null) return true;
			try
			{
				return Value.Length <= MaxChars && StrictUtf8.GetByteCount(Value) <= MaxBytes;
			}
			catch (EncoderFallbackException)
			{
				return false;
			}
		}

		private static bool BoundedHaul(KingdomCarryHaul Value)
		{
			return Value == null ||
				(BoundedUtf8(Value.OriginZoneID, 512, 2048) &&
				 BoundedUtf8(Value.DestinationSettlementId, 256, 1024) &&
				 BoundedUtf8(Value.DestinationSettlementName, 512, 2048));
		}

		private static bool ValidHaulAuthority(KingdomCarryHaul Value)
		{
			if (Value == null) return true;
			return KingdomIdentityRules.IsSettlementId(Value.DestinationSettlementId) &&
				Value.PlantedTick >= 0L && Value.DueTick >= Value.PlantedTick &&
				Value.Mud >= 0 && Value.Brush >= 0 && Value.Timber >= 0 &&
				Value.Stone >= 0 && Value.Marble >= 0 && Value.Scrap >= 0;
		}

		private static bool StrictlySorted(IList<string> Values)
		{
			if (Values == null) return false;
			for (int i = 1; i < Values.Count; i++)
				if (string.CompareOrdinal(Values[i - 1], Values[i]) >= 0) return false;
			return true;
		}

		private static bool BoundedStrings(List<string> Values, int MaxChars)
		{
			if (Values == null) return false;
			for (int i = 0; i < Values.Count; i++)
			{
				if (Values[i] == null || Values[i].Length > MaxChars) return false;
				try
				{
					if (StrictUtf8.GetByteCount(Values[i]) > MaxTextBytes * 4) return false;
				}
				catch (EncoderFallbackException)
				{
					return false;
				}
			}
			return true;
		}

		private static bool ValidBindings(Simulation.City.KingdomBindingRegistry Value)
		{
			if (Value == null || Value.Keys == null || Value.Kinds == null || Value.ZoneIds == null
				|| Value.ObjectIds == null || Value.MintedTicks == null) return false;
			int count = Value.Keys.Count;
			return count <= MaxBindings && Value.Kinds.Count == count && Value.ZoneIds.Count == count
				&& Value.ObjectIds.Count == count && Value.MintedTicks.Count == count
				&& BoundedStrings(Value.ZoneIds, 512) && BoundedStrings(Value.ObjectIds, 512);
		}

	}
}
