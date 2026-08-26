using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static bool ValidCount(int Value)
		{
			return Value >= 0 && Value <= MaxPhysicalCount;
		}

		private static bool PristineLifecycleBook(KingdomLifecycleBook book)
		{
			return book != null && book.FormatVersion == CurrentFormatVersion
				&& !book.WireRejected && !book.Quarantined && string.IsNullOrEmpty(book.Fault)
				&& string.IsNullOrEmpty(book.SettlementId) && !book.IdentityBound
				&& string.IsNullOrEmpty(book.IdentityProof) && !book.LegacyIdentity
				&& string.IsNullOrEmpty(book.LegacyMigrationKey)
				&& book.PlainGuestNextSequence == 1L && book.PlainGuestRetiredThrough == 0L
				&& book.NotableGuestNextSequence == 1L && book.NotableGuestRetiredThrough == 0L
				&& book.RaidNextSequence == 1L && book.RaidRetiredThrough == 0L
				&& book.PetitionNextSequence == 1L && book.PetitionRetiredThrough == 0L
				&& book.LocusOption == KingdomLifecycleOptionState.Unknown
				&& book.NotableOption == KingdomLifecycleOptionState.Unknown
				&& book.RaidOption == KingdomLifecycleOptionState.Unknown
				&& book.PetitionOption == KingdomLifecycleOptionState.Unknown
				&& book.LocusOptionTick == 0L && book.NotableOptionTick == 0L
				&& book.RaidOptionTick == 0L && book.PetitionOptionTick == 0L
				&& book.PlainGuest == null && book.NotableGuest == null
				&& book.Raid == null && book.Petition == null
				&& book.Resources != null && book.Resources.Count == 0
				&& book.RecentProofs != null && book.RecentProofs.Count == 0
				&& KingdomRaidIncidentRules.ValidLedger(book.RaidLedger)
				&& book.RaidLedger.StateRevision == 0L
				&& book.RaidLedger.ScheduleRevision == 0L
				&& book.RaidLedger.Grievances.Count == 0
				&& book.RaidLedger.Incidents.Count == 0
				&& book.RaidLedger.ActiveIncidentId == null
				&& !book.RaidLedger.LegacyEvidenceArchived
				&& PristineGrowthBook(book.Growth);
		}

		private static bool CanonicalLifecycleQuarantine(KingdomLifecycleBook book)
		{
			if (book == null || book.FormatVersion != CurrentFormatVersion || book.WireRejected
				|| !book.Quarantined || string.IsNullOrEmpty(book.Fault)
				|| TooLong(book.Fault, MaxTextChars)) return false;
			bool identity = book.IdentityBound
				? ValidRootId(book.SettlementId) && ExactSettlementIdentityProof(book)
				: string.IsNullOrEmpty(book.SettlementId)
					&& string.IsNullOrEmpty(book.IdentityProof) && !book.LegacyIdentity
					&& string.IsNullOrEmpty(book.LegacyMigrationKey);
			return identity && LifecycleBookShape(book);
		}

		private static bool PristineCarryBook(KingdomCarryBook book)
		{
			return book != null && book.FormatVersion == CurrentCarryFormatVersion
				&& !book.WireRejected && !book.Quarantined && string.IsNullOrEmpty(book.Fault)
				&& book.OpaqueWireVersion == 0 && book.OpaquePayload == null
				&& string.IsNullOrEmpty(book.RealmId) && !book.IdentityBound
				&& string.IsNullOrEmpty(book.IdentityProof) && !book.LegacyIdentity
				&& string.IsNullOrEmpty(book.LegacyMigrationKey)
				&& book.SettlementIds != null && book.SettlementIds.Count == 0
				&& book.NextSequence == 1L && book.RetiredThrough == 0L && book.Open == null
				&& book.Resources != null && book.Resources.Count == 0
				&& book.RecentProofs != null && book.RecentProofs.Count == 0;
		}

		private static string SettlementIdentityProof(string settlementId, bool legacy,
			string migrationKey)
		{
			return HashId("lifecycle-binding", delegate(BinaryWriter w)
			{
				CanonicalString(w, settlementId);
				w.Write(legacy);
				CanonicalString(w, legacy ? migrationKey : null);
			});
		}

		private static bool ExactSettlementIdentityProof(KingdomLifecycleBook book)
		{
			return book != null && book.IdentityBound && ValidRootId(book.SettlementId)
				&& string.Equals(book.IdentityProof, SettlementIdentityProof(book.SettlementId,
					book.LegacyIdentity, book.LegacyMigrationKey), StringComparison.Ordinal);
		}

		private static string CarryIdentityProof(string realmId, List<string> settlementIds,
			bool legacy, string migrationKey)
		{
			return HashId("carry-binding", delegate(BinaryWriter w)
			{
				CanonicalString(w, realmId);
				w.Write(settlementIds == null ? -1 : settlementIds.Count);
				if (settlementIds != null) for (int i = 0; i < settlementIds.Count; i++)
					CanonicalString(w, settlementIds[i]);
				w.Write(legacy);
				CanonicalString(w, legacy ? migrationKey : null);
			});
		}

		private static string RealmTopologyDigest(string realmId, List<string> settlementIds)
		{
			return HashId("carry-realm-topology", delegate(BinaryWriter w)
			{
				CanonicalString(w, realmId);
				w.Write(settlementIds == null ? -1 : settlementIds.Count);
				if (settlementIds != null) for (int i = 0; i < settlementIds.Count; i++)
					CanonicalString(w, settlementIds[i]);
			});
		}

		private static bool ExactCarryIdentityProof(KingdomCarryBook book)
		{
			return book != null && book.IdentityBound && ValidRootId(book.RealmId)
				&& FrozenSettlementSetValid(book.SettlementIds)
				&& string.Equals(book.IdentityProof, CarryIdentityProof(book.RealmId,
					book.SettlementIds, book.LegacyIdentity, book.LegacyMigrationKey),
					StringComparison.Ordinal);
		}

		private static bool TryFrozenSettlementSet(ICollection<string> source,
			out List<string> frozen)
		{
			frozen = null;
			try
			{
				if (source == null || source.Count <= 0 || source.Count > MaxSettlementIds)
					return false;
				List<string> value = new List<string>(source.Count);
				HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
				foreach (string id in source)
					if (!ValidRootId(id) || !seen.Add(id)) return false; else value.Add(id);
				if (value.Count != source.Count) return false;
				value.Sort(StringComparer.Ordinal);
				frozen = value;
				return true;
			}
			catch (Exception)
			{
				frozen = null;
				return false;
			}
		}

		private static bool TryFrozenPositiveIds(ICollection<int> source, int maximum,
			out List<int> frozen)
		{
			frozen = null;
			try
			{
				if (source == null || source.Count <= 0 || source.Count > maximum) return false;
				List<int> value = new List<int>(source.Count);
				int prior = 0;
				foreach (int id in source)
				{
					if (id <= prior) return false;
					value.Add(id);
					prior = id;
				}
				if (value.Count != source.Count) return false;
				frozen = value;
				return true;
			}
			catch (Exception)
			{
				frozen = null;
				return false;
			}
		}

		private static bool FrozenPositiveIdsValid(List<int> ids, int maximum)
		{
			if (ids == null || ids.Count <= 0 || ids.Count > maximum) return false;
			for (int i = 0; i < ids.Count; i++)
				if (ids[i] <= 0 || (i > 0 && ids[i - 1] >= ids[i])) return false;
			return true;
		}

		private static bool ExistingIdsExclude(ICollection<string> source, string exactId)
		{
			if (source == null) return false;
			try
			{
				if (source.Count > MaxLifecycleCollisionIds) return false;
				HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
				int count = 0;
				foreach (string id in source)
				{
					count++;
					if (count > MaxLifecycleCollisionIds || !ValidRootId(id) || !seen.Add(id)
						|| string.Equals(id, exactId, StringComparison.Ordinal)) return false;
				}
				return count == source.Count;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool FrozenSettlementSetValid(List<string> ids)
		{
			if (ids == null || ids.Count <= 0 || ids.Count > MaxSettlementIds) return false;
			for (int i = 0; i < ids.Count; i++)
				if (!ValidRootId(ids[i]) || (i > 0 && string.CompareOrdinal(ids[i - 1], ids[i]) >= 0))
					return false;
			return true;
		}

		private static bool CarrySettlementSetShape(KingdomCarryBook book)
		{
			return book != null && FrozenSettlementSetValid(book.SettlementIds);
		}

		private static bool SettlementMember(KingdomCarryBook book, string id)
		{
			if (book == null || !ValidRootId(id) || book.SettlementIds == null) return false;
			for (int i = 0; i < book.SettlementIds.Count; i++)
				if (string.Equals(book.SettlementIds[i], id, StringComparison.Ordinal)) return true;
			return false;
		}

		private static bool ExactStringList(List<string> a, List<string> b)
		{
			if (a == null || b == null || a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
				if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ValidRootId(string Value)
		{
			return !string.IsNullOrEmpty(Value) && !TooLong(Value, MaxIdChars);
		}

		private static bool ValidName(string Value)
		{
			return !string.IsNullOrEmpty(Value) && !TooLong(Value, MaxNameChars);
		}

		private static bool TooLong(string Value, int Limit)
		{
			if (Value == null) return false;
			if (Limit < 0 || Value.Length > Limit) return true;
			try
			{
				return StrictUtf8.GetByteCount(Value) > (long)Limit * 4L;
			}
			catch (EncoderFallbackException) { return true; }
		}

		private static string SafeFault(string Value)
		{
			return Value != null && Value.Length <= MaxTextChars ? Value
				: "lifecycle authority quarantined";
		}

		private static void Deny(KingdomLifecycleBook Book, string Fault)
		{
			Book.Quarantined = true;
			Book.Fault = Fault;
		}

		private static void Deny(KingdomCarryBook Book, string Fault)
		{
			Book.Quarantined = true;
			Book.Fault = Fault;
		}

	}
}
