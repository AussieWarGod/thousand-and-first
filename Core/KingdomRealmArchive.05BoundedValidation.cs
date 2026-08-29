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
				if (string.IsNullOrEmpty(row.Key) || !BoundedUtf8(row.Key, 512, 2048)) return false;
			return true;
		}

		private static bool BoundedRemainders(Dictionary<string, int> Value)
		{
			if (!BoundedStandings(Value)) return false;
			foreach (KeyValuePair<string, int> row in Value)
				if (!KingdomStandingRules.ValidRemainder(row.Value)) return false;
			return true;
		}

		private static bool ValidDirectionalStandings(string RealmFaction,
			Dictionary<string, int> Regard, Dictionary<string, int> Policy,
			Dictionary<string, int> Remainders, Dictionary<string, int> Observed)
		{
			if (!BoundedStandings(Regard) || !BoundedStandings(Policy) ||
				!BoundedRemainders(Remainders) || !BoundedStandings(Observed) ||
				!KingdomStandingRules.CanonicalPairs(Regard, Remainders)) return false;
			foreach (string key in Regard.Keys)
				if (!KingdomStandingRules.EligibleForeignFaction(key, RealmFaction)) return false;
			foreach (string key in Policy.Keys)
				if (!KingdomStandingRules.EligibleForeignFaction(key, RealmFaction)) return false;
			foreach (string key in Remainders.Keys)
				if (!KingdomStandingRules.EligibleForeignFaction(key, RealmFaction)) return false;
			foreach (string key in Observed.Keys)
				if (!KingdomStandingRules.EligibleForeignFaction(key, RealmFaction)) return false;
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
