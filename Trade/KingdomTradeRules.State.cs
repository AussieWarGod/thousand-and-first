using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		public static void QuarantineBook(KingdomTradeBook Book, string Fault)
		{
			if (Book == null || Book.FormatVersion != CurrentFormatVersion) return;
			Book.SchemaState = KingdomTradeSchemaState.Quarantined;
			Book.SchemaFault = AppendFault(Book.SchemaFault, Fault);
			if (Book.Charters != null)
			{
				for (int i = 0; i < Book.Charters.Count; i++)
				{
					KingdomTradeCharter row = Book.Charters[i];
					if (row == null) continue;
					row.Quarantined = true;
					row.Fault = AppendFault(row.Fault, Fault);
				}
			}
			if (Book.Manifest != null)
			{
				Book.Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Book.Manifest.Fault = AppendFault(Book.Manifest.Fault, Fault);
			}
			if (Book.OpenOperation != null)
			{
				Book.OpenOperation.Phase = KingdomTradePhase.Quarantined;
				Book.OpenOperation.Fault = AppendFault(Book.OpenOperation.Fault, Fault);
			}
		}

		public static bool SinkSettled(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Delivered
				|| State == KingdomTradeSinkState.Skipped
				|| State == KingdomTradeSinkState.Lost;
		}

		public static KingdomTradeSinkState ResumeSink(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Intent ? KingdomTradeSinkState.Lost : State;
		}

		public static KingdomTradeOptionAction ObserveOption(
			KingdomTradeOptionState Prior, bool Enabled)
		{
			if (!Enabled)
			{
				return Prior == KingdomTradeOptionState.Disabled
					? KingdomTradeOptionAction.StayDisabled : KingdomTradeOptionAction.Disable;
			}
			return Prior == KingdomTradeOptionState.Enabled
				? KingdomTradeOptionAction.None : KingdomTradeOptionAction.EnableAndRestamp;
		}

		public static long SaturatingAdd(long Left, long Right)
		{
			if (Right > 0L && Left > long.MaxValue - Right) return long.MaxValue;
			if (Right < 0L && Left < long.MinValue - Right) return long.MinValue;
			return Left + Right;
		}

		public static int SaturatingMultiply(int Left, int Right)
		{
			if (Left <= 0 || Right <= 0) return 0;
			long value = (long)Left * Right;
			return value >= int.MaxValue ? int.MaxValue : (int)value;
		}

		public static int SaturatingAdd(int Left, int Right)
		{
			long value = (long)Left + Right;
			if (value <= 0L) return 0;
			return value >= int.MaxValue ? int.MaxValue : (int)value;
		}

		public static bool RecordIncident(KingdomTradeBook Book, long Tick, string Fault,
			KingdomTradeBook Evidence = null)
		{
			if (Book == null || Book.Incidents == null || Book.Incidents.Count >= MaxIncidents) return false;
			KingdomTradeOperation operation = Evidence?.OpenOperation;
			Book.Incidents.Add(new KingdomTradeIncident
			{
				RealmId = ValidId(Evidence?.RealmId) ? Evidence.RealmId
					: (ValidId(Book.RealmId) ? Book.RealmId : "unbound-trade-incident"),
				Sequence = operation?.Sequence ?? 0L,
				OperationId = operation?.Id,
				EvidenceHash = EvidenceDigest(Evidence ?? Book),
				Tick = Tick < 0L ? 0L : Tick,
				Fault = Bound(Fault, MaxTextChars)
			});
			return true;
		}

		public static string EvidenceDigest(KingdomTradeBook Book)
		{
			try
			{
				byte[] bytes = KingdomTradeCodec.EncodePayload(Book);
				using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
			}
			catch
			{
				using (SHA256 sha = SHA256.Create())
				{
					byte[] bytes = Encoding.UTF8.GetBytes(DigestField(Book?.RealmId) + "\n"
						+ DigestField(Book?.OpenOperation?.Id) + "\n"
						+ (Book?.OpenOperation?.Sequence ?? 0L).ToString(CultureInfo.InvariantCulture));
					return Hex(sha.ComputeHash(bytes));
				}
			}
		}

		/// <summary>Only canonical lowercase SHA-256 text may carry authority evidence.</summary>
		public static bool CanonicalSha256(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if ((Value[i] < '0' || Value[i] > '9')
					&& (Value[i] < 'a' || Value[i] > 'f')) return false;
			return true;
		}

		/// <summary>
		/// Recomputable receipt commitment. ReceiptEvidenceHash itself is excluded to avoid
		/// recursion; every other persisted archive field is length- or width-delimited.
		/// </summary>
		private static string ArchiveReceiptDigest(KingdomTradeArchive Row)
		{
			try
			{
				if (Row == null || Row.SettlementIds == null
					|| Row.SettlementIds.Count > MaxSettlementIds) return null;
				using (MemoryStream canonical = new MemoryStream())
				{
					if (!WriteCanonicalField(canonical, "taf.trade.archive-receipt.v1")
						|| !WriteCanonicalNullableField(canonical, Row.RealmId)) return null;
					WriteInt32(canonical, Row.SettlementIds.Count);
					for (int i = 0; i < Row.SettlementIds.Count; i++)
						if (!WriteCanonicalNullableField(canonical, Row.SettlementIds[i])) return null;
					WriteInt64(canonical, Row.RetainedEscrowDrams);
					WriteInt32(canonical, Row.ManifestEscrowDrams);
					if (!WriteCanonicalNullableField(canonical, Row.ManifestId)) return null;
					WriteInt32(canonical, (int)Row.ManifestStatus);
					WriteInt32(canonical, Row.CharterCount);
					WriteInt32(canonical, Row.ProjectionCount);
					WriteInt32(canonical, Row.ProofCount);
					if (!WriteCanonicalNullableField(canonical, Row.OpenOperationId)
						|| !WriteCanonicalNullableField(canonical, Row.PendingRetirementId)) return null;
					WriteInt32(canonical, Row.OpenRequestedWater);
					WriteInt32(canonical, Row.OpenProvedWater);
					WriteInt32(canonical, Row.OpenAmbiguousWater);
					WriteInt64(canonical, Row.RetiredThrough);
					if (!WriteCanonicalNullableField(canonical, Row.AuthorityEvidenceHash)) return null;
					WriteInt64(canonical, Row.ClosedTick);
					using (SHA256 sha = SHA256.Create())
						return sha == null ? null : Hex(sha.ComputeHash(canonical.ToArray()));
				}
			}
			catch { return null; }
		}

		private static string DigestField(string Value)
		{
			if (Value == null) return "-1:";
			int take = Math.Min(Value.Length, MaxIdChars);
			return Value.Length.ToString(CultureInfo.InvariantCulture) + ":"
				+ (take == Value.Length ? Value : Value.Substring(0, take));
		}

		private static string Hex(byte[] Digest)
		{
			char[] hex = new char[Digest.Length * 2];
			const string alphabet = "0123456789abcdef";
			for (int i = 0; i < Digest.Length; i++)
			{
				hex[i * 2] = alphabet[Digest[i] >> 4];
				hex[i * 2 + 1] = alphabet[Digest[i] & 15];
			}
			return new string(hex);
		}

		/// <summary>Name-derived identity exists only to preserve legacy positional rows.
		/// Live trade must bind the city's already-minted SettlementId.</summary>
	}
}
