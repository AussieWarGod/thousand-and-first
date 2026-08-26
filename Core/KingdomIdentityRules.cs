using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Why a realm/settlement identity set was refused.</summary>
	public enum KingdomIdentityFault : byte
	{
		None = 0,
		InvalidTransaction = 1,
		InvalidRealm = 2,
		InvalidEvidence = 3,
		NullSet = 4,
		TooManySettlements = 5,
		InvalidSettlement = 6,
		DuplicateSettlement = 7,
		CryptographicFailure = 8,
		InvalidOrigin = 9,
		InvalidVersion = 10,
		IdentityMismatch = 11,
		EmptySettlementSet = 12,
		RaggedSettlementNames = 13,
		AmbiguousSettlementName = 14
	}

	/// <summary>The one durable provenance lane permitted to justify an immutable id.</summary>
	public enum KingdomIdentityOrigin : byte
	{
		None = 0,
		FoundingTransaction = 1,
		LegacyMigration = 2,
		Quarantined = 3
	}

	/// <summary>
	/// Mints the immutable subjects every civic receipt and deterministic draw hangs from.
	/// Display names, faction names, capital status, seat order, and current membership never
	/// enter a live identity. They are admitted only by the one explicit legacy migration.
	/// </summary>
	public static class KingdomIdentityRules
	{
		public const int RulesVersion = 1;
		public const int MaxSettlements = 4;
		public const int HashHexChars = 64;
		public const string RealmPrefix = "taf:realm:v1:";
		public const string SettlementPrefix = "taf:settlement:v1:";

		private const int NonceChars = 32;
		private const int MaxEvidenceChars = 512;
		private const int MaxEvidenceUtf8Bytes = 1024;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
		private static readonly char[] Hex = "0123456789abcdef".ToCharArray();
#if TAF_TESTS
		internal static Func<SHA256> TestProviderFactory = SHA256.Create;
#endif

		public static bool IsRealmId(string Value)
		{
			return IsHashedId(Value, RealmPrefix);
		}

		public static bool IsSettlementId(string Value)
		{
			return IsHashedId(Value, SettlementPrefix);
		}

		/// <summary>
		/// Proves the engine faction key carried by a first-founding receipt. Current receipts use
		/// the realm id itself: a namespaced immutable key derived only from the transaction. The
		/// display-name equality is admitted solely for interrupted pre-contract receipts and old
		/// saves, whose registered faction key can never be renamed or removed safely.
		/// </summary>
		public static bool FirstFactionKeyMatches(string FactionKey, string TransactionId,
			string LegacyDisplayName, bool AllowLegacy)
		{
			if (TryMintRealm(TransactionId, out string expected, out KingdomIdentityFault fault) &&
				string.Equals(FactionKey, expected, StringComparison.Ordinal)) return true;
			return AllowLegacy && !string.IsNullOrEmpty(LegacyDisplayName) &&
				string.Equals(FactionKey, LegacyDisplayName, StringComparison.Ordinal);
		}

		public static bool IsFoundingTransaction(string Value)
		{
			if (Value == null || Value.Length != NonceChars) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
			}
			return true;
		}

		/// <summary>Mints the realm once, from the first founding transaction.</summary>
		public static bool TryMintRealm(string TransactionId, out string RealmId,
			out KingdomIdentityFault Fault)
		{
			RealmId = null;
			if (!IsFoundingTransaction(TransactionId))
			{
				Fault = KingdomIdentityFault.InvalidTransaction;
				return false;
			}
			return TryHash(RealmPrefix, "realm-founding", delegate(BinaryWriter writer)
			{
				WriteCanonical(writer, TransactionId);
			}, out RealmId, out Fault);
		}

		/// <summary>Mints one city from its own founding transaction under an existing realm.</summary>
		public static bool TryMintSettlement(string RealmId, string TransactionId,
			out string SettlementId, out KingdomIdentityFault Fault)
		{
			SettlementId = null;
			Fault = KingdomIdentityFault.None;
			if (!IsRealmId(RealmId))
			{
				Fault = KingdomIdentityFault.InvalidRealm;
				return false;
			}
			if (!IsFoundingTransaction(TransactionId))
			{
				Fault = KingdomIdentityFault.InvalidTransaction;
				return false;
			}
			return TryHash(SettlementPrefix, "settlement-founding",
				delegate(BinaryWriter writer)
				{
					WriteCanonical(writer, RealmId);
					WriteCanonical(writer, TransactionId);
				}, out SettlementId, out Fault);
		}

		/// <summary>
		/// One deterministic migration for a realm written before immutable identity existed.
		/// These mutable-looking fields are evidence read once; the result is persisted and never
		/// recomputed after a rename, re-seat, or relationship change.
		/// </summary>
		public static bool TryMigrateRealm(string RealmFactionAtMigration, long FoundedTick,
			ulong SeedHigh, ulong SeedLow, string FirstClaimedZone, out string RealmId,
			out KingdomIdentityFault Fault)
		{
			RealmId = null;
			string realmEvidence;
			string zoneEvidence;
			if (FoundedTick < 0L
				|| !TryNormalizeEvidence(RealmFactionAtMigration, out realmEvidence)
				|| !TryNormalizeEvidence(FirstClaimedZone, out zoneEvidence))
			{
				Fault = KingdomIdentityFault.InvalidEvidence;
				return false;
			}
			return TryHash(RealmPrefix, "realm-legacy-migration",
				delegate(BinaryWriter writer)
				{
					WriteCanonical(writer, realmEvidence);
					writer.Write(FoundedTick);
					writer.Write(SeedHigh);
					writer.Write(SeedLow);
					WriteCanonical(writer, zoneEvidence);
				}, out RealmId, out Fault);
		}

		/// <summary>One deterministic migration for a city under an already migrated realm.</summary>
		public static bool TryMigrateSettlement(string RealmId, long FoundedTick,
			string FirstClaimedZone, out string SettlementId, out KingdomIdentityFault Fault)
		{
			SettlementId = null;
			if (!IsRealmId(RealmId))
			{
				Fault = KingdomIdentityFault.InvalidRealm;
				return false;
			}
			string zoneEvidence;
			if (FoundedTick < 0L
				|| !TryNormalizeEvidence(FirstClaimedZone, out zoneEvidence))
			{
				Fault = KingdomIdentityFault.InvalidEvidence;
				return false;
			}
			return TryHash(SettlementPrefix, "settlement-legacy-migration",
				delegate(BinaryWriter writer)
				{
					WriteCanonical(writer, RealmId);
					writer.Write(FoundedTick);
					WriteCanonical(writer, zoneEvidence);
				}, out SettlementId, out Fault);
		}

		/// <summary>
		/// Validates the complete bounded city identity set. Duplicate rows invalidate the whole
		/// set; no caller may keep the first and silently discard the other claimant.
		/// </summary>
		public static bool ValidateSettlementSet(IList<string> SettlementIds,
			out KingdomIdentityFault Fault)
		{
			if (SettlementIds == null)
			{
				Fault = KingdomIdentityFault.NullSet;
				return false;
			}
			if (SettlementIds.Count > MaxSettlements)
			{
				Fault = KingdomIdentityFault.TooManySettlements;
				return false;
			}
			for (int i = 0; i < SettlementIds.Count; i++)
			{
				if (!IsSettlementId(SettlementIds[i]))
				{
					Fault = KingdomIdentityFault.InvalidSettlement;
					return false;
				}
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(SettlementIds[i], SettlementIds[j],
						StringComparison.Ordinal))
					{
						Fault = KingdomIdentityFault.DuplicateSettlement;
						return false;
					}
				}
			}
			Fault = KingdomIdentityFault.None;
			return true;
		}

		/// <summary>Validates the complete live topology. Unlike the lower-level set helper,
		/// a founded realm must carry at least one city and a valid immutable realm id.</summary>
		public static bool ValidateRealmTopology(string RealmId, IList<string> SettlementIds,
			out KingdomIdentityFault Fault)
		{
			if (!IsRealmId(RealmId))
			{
				Fault = KingdomIdentityFault.InvalidRealm;
				return false;
			}
			if (SettlementIds == null)
			{
				Fault = KingdomIdentityFault.NullSet;
				return false;
			}
			if (SettlementIds.Count == 0)
			{
				Fault = KingdomIdentityFault.EmptySettlementSet;
				return false;
			}
			return ValidateSettlementSet(SettlementIds, out Fault);
		}

		/// <summary>Resolves prose to authority only when a complete already-proved topology has
		/// exactly one ordinal match. Duplicate names refuse whole; no first-row promotion.</summary>
		public static bool TryResolveUniqueSettlementName(IList<string> Names,
			IList<string> SettlementIds, string Name, out string SettlementId,
			out KingdomIdentityFault Fault)
		{
			SettlementId = null;
			if (Names == null || SettlementIds == null || Names.Count != SettlementIds.Count ||
				Names.Count == 0 || Names.Count > MaxSettlements)
			{
				Fault = KingdomIdentityFault.RaggedSettlementNames;
				return false;
			}
			if (string.IsNullOrEmpty(Name))
			{
				Fault = KingdomIdentityFault.InvalidEvidence;
				return false;
			}
			if (!ValidateSettlementSet(SettlementIds, out Fault)) return false;
			int found = -1;
			for (int i = 0; i < Names.Count; i++)
			{
				if (!string.Equals(Names[i], Name, StringComparison.Ordinal)) continue;
				if (found >= 0)
				{
					Fault = KingdomIdentityFault.AmbiguousSettlementName;
					return false;
				}
				found = i;
			}
			if (found < 0)
			{
				Fault = KingdomIdentityFault.InvalidEvidence;
				return false;
			}
			SettlementId = SettlementIds[found];
			Fault = KingdomIdentityFault.None;
			return true;
		}

		/// <summary>Recomputes a persisted realm id from its frozen provenance. No live name is
		/// consulted on the founding-transaction lane; the legacy name is admitted only when the
		/// stored origin explicitly says this is the one migration.</summary>
		public static bool ReproveRealm(string PersistedId, int PersistedVersion,
			KingdomIdentityOrigin Origin, string TransactionId, string LegacyFaction,
			long FoundedTick, ulong SeedHigh, ulong SeedLow, string FirstClaimedZone,
			out KingdomIdentityFault Fault)
		{
			if (PersistedVersion != RulesVersion)
			{
				Fault = KingdomIdentityFault.InvalidVersion;
				return false;
			}
			string expected = null;
			bool proved = false;
			Fault = KingdomIdentityFault.None;
			switch (Origin)
			{
			case KingdomIdentityOrigin.FoundingTransaction:
				if (!string.IsNullOrEmpty(LegacyFaction))
				{
					Fault = KingdomIdentityFault.InvalidEvidence;
					return false;
				}
				proved = TryMintRealm(TransactionId, out expected, out Fault);
				break;
			case KingdomIdentityOrigin.LegacyMigration:
				if (!string.IsNullOrEmpty(TransactionId))
				{
					Fault = KingdomIdentityFault.InvalidEvidence;
					return false;
				}
				proved = TryMigrateRealm(LegacyFaction, FoundedTick, SeedHigh, SeedLow,
					FirstClaimedZone, out expected, out Fault);
				break;
			default:
				Fault = KingdomIdentityFault.InvalidOrigin;
				return false;
			}
			if (!proved) return false;
			if (!string.Equals(PersistedId, expected, StringComparison.Ordinal))
			{
				Fault = KingdomIdentityFault.IdentityMismatch;
				return false;
			}
			Fault = KingdomIdentityFault.None;
			return true;
		}

		/// <summary>Recomputes one persisted city id under the exact persisted realm id.</summary>
		public static bool ReproveSettlement(string PersistedId, string RealmId,
			int PersistedVersion, KingdomIdentityOrigin Origin, string TransactionId,
			long FoundedTick, string FirstClaimedZone, out KingdomIdentityFault Fault)
		{
			if (PersistedVersion != RulesVersion)
			{
				Fault = KingdomIdentityFault.InvalidVersion;
				return false;
			}
			string expected;
			bool proved;
			switch (Origin)
			{
			case KingdomIdentityOrigin.FoundingTransaction:
				proved = TryMintSettlement(RealmId, TransactionId, out expected, out Fault);
				break;
			case KingdomIdentityOrigin.LegacyMigration:
				if (!string.IsNullOrEmpty(TransactionId))
				{
					Fault = KingdomIdentityFault.InvalidEvidence;
					return false;
				}
				proved = TryMigrateSettlement(RealmId, FoundedTick, FirstClaimedZone,
					out expected, out Fault);
				break;
			default:
				Fault = KingdomIdentityFault.InvalidOrigin;
				return false;
			}
			if (!proved) return false;
			if (!string.Equals(PersistedId, expected, StringComparison.Ordinal))
			{
				Fault = KingdomIdentityFault.IdentityMismatch;
				return false;
			}
			Fault = KingdomIdentityFault.None;
			return true;
		}

		private static bool IsHashedId(string Value, string Prefix)
		{
			if (Value == null || Value.Length != Prefix.Length + HashHexChars
				|| !Value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			for (int i = Prefix.Length; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
			}
			return true;
		}

		private static bool TryNormalizeEvidence(string Value, out string Normalized)
		{
			Normalized = null;
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxEvidenceChars) return false;
			try
			{
				string normalized = Value.Normalize(NormalizationForm.FormC);
				if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxEvidenceChars
					|| StrictUtf8.GetByteCount(normalized) > MaxEvidenceUtf8Bytes) return false;
				Normalized = normalized;
				return true;
			}
			catch (Exception exception)
			{
				if (!(exception is EncoderFallbackException)
					&& !(exception is ArgumentException)) throw;
				return false;
			}
		}

		private static bool TryHash(string Prefix, string Lane, Action<BinaryWriter> Payload,
			out string Id, out KingdomIdentityFault Fault)
		{
			Id = null;
			try
			{
				byte[] preimage;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8))
				{
					WriteCanonical(writer, "taf.identity");
					writer.Write(RulesVersion);
					WriteCanonical(writer, Lane);
					Payload(writer);
					writer.Flush();
					preimage = stream.ToArray();
				}
				byte[] digest;
#if TAF_TESTS
				SHA256 createdProvider = TestProviderFactory();
#else
				SHA256 createdProvider = SHA256.Create();
#endif
				using (SHA256 provider = createdProvider)
				{
					if (provider == null)
					{
						Fault = KingdomIdentityFault.CryptographicFailure;
						return false;
					}
					digest = provider.ComputeHash(preimage);
				}
				char[] value = new char[Prefix.Length + digest.Length * 2];
				Prefix.CopyTo(0, value, 0, Prefix.Length);
				int at = Prefix.Length;
				for (int i = 0; i < digest.Length; i++)
				{
					value[at++] = Hex[digest[i] >> 4];
					value[at++] = Hex[digest[i] & 15];
				}
				Id = new string(value);
				Fault = KingdomIdentityFault.None;
				return true;
			}
			catch (Exception exception)
			{
				if (!(exception is IOException) && !(exception is EncoderFallbackException)
					&& !(exception is CryptographicException)
					&& !(exception is NotSupportedException)) throw;
				Fault = KingdomIdentityFault.CryptographicFailure;
				return false;
			}
		}

		private static void WriteCanonical(BinaryWriter Writer, string Value)
		{
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(bytes.Length);
			Writer.Write(bytes);
		}
	}
}
