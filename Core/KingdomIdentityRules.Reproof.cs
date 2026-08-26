using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomIdentityRules
	{
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
