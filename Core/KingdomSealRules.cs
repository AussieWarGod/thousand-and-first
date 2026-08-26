using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The rules that turn a living settlement into a sealed record, judge whether one may cross,
	/// and draw the one fortune between lives. Engine-free by design: everything here is testable
	/// without a game, which is the only way the exploit class this guards against
	/// (<c>DECISIONS.md:167-172</c>) stays caught.
	/// </summary>
	internal static partial class KingdomSealRules
	{
		/// <summary>The alphabet an identifier may use. Deliberately no slash, no backslash, no
		/// dollar, no brace, no space: an id from a file must never be able to become a path
		/// fragment, a format template, or a markup tag.</summary>
		public const string TokenAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.:";

		/// <summary>What a settler's name may not be longer than once sanitized.</summary>
		public const int MaxNameChars = KingdomSealRecord.MaxNameChars;

		/// <summary>
		/// True when a string is one this build will accept as an identifier: the token alphabet,
		/// nothing else, and never empty at a site that requires one.
		/// </summary>
		public static bool IsToken(string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				if (TokenAlphabet.IndexOf(Value[i]) < 0)
				{
					return false;
				}
			}
			return true;
		}

		public static bool ExactIdentity(KingdomSealIdentity Identity,
			KingdomSettlement Seat)
		{
			if (Identity == null || Seat?.City == null || Identity.SettlementIds == null ||
				!string.Equals(Seat.City.SettlementId, Identity.SettlementId,
					StringComparison.Ordinal)) return false;
			KingdomIdentityFault fault;
			return KingdomIdentityRules.ReproveRealm(Identity.RealmId,
				Identity.RealmIdentityVersion, Identity.RealmIdentityOrigin,
				Identity.RealmIdentityTransactionId, Identity.RealmIdentityLegacyFaction,
				Identity.RealmIdentityFoundedTick, Identity.RealmIdentitySeedHigh,
				Identity.RealmIdentitySeedLow, Identity.RealmIdentityFirstClaimedZone,
				out fault) && KingdomIdentityRules.ValidateRealmTopology(Identity.RealmId,
					Identity.SettlementIds, out fault) &&
				Identity.SettlementIds.Contains(Identity.SettlementId) &&
				ExactTopologyProvenance(Identity.RealmId, Identity.SettlementIds,
					Identity.SettlementProvenanceRows, Identity.SettlementId,
					Identity.SettlementIdentityVersion, Identity.SettlementIdentityOrigin,
					Identity.SettlementIdentityTransactionId,
					Identity.SettlementIdentityFoundedTick,
					Identity.SettlementIdentityFirstClaimedZone,
					Identity.SettlementIdentityLegacyId) &&
				KingdomIdentityRules.ReproveSettlement(Identity.SettlementId,
					Identity.RealmId, Identity.SettlementIdentityVersion,
					Identity.SettlementIdentityOrigin,
					Identity.SettlementIdentityTransactionId,
					Identity.SettlementIdentityFoundedTick,
					Identity.SettlementIdentityFirstClaimedZone, out fault);
		}

		internal static bool TryBuildSettlementProvenance(string SettlementId, int Version,
			KingdomIdentityOrigin Origin, string TransactionId, long FoundedTick,
			string FirstClaimedZone, string LegacyId, out string Row)
		{
			Row = null;
			if (!KingdomIdentityRules.IsSettlementId(SettlementId) || Version < 0 || Version > 32 ||
				Origin < KingdomIdentityOrigin.None || Origin > KingdomIdentityOrigin.LegacyMigration ||
				FoundedTick < 0L) return false;
			if (!TryHex(TransactionId ?? "", 1024, out string transaction) ||
				!TryHex(FirstClaimedZone ?? "", 1024, out string zone) ||
				!TryHex(LegacyId ?? "", 1024, out string legacy)) return false;
			Row = SettlementId + "." + Version.ToString(CultureInfo.InvariantCulture) + "." +
				((int)Origin).ToString(CultureInfo.InvariantCulture) + "." +
				FoundedTick.ToString(CultureInfo.InvariantCulture) +
				"." + transaction + "." + zone + "." + legacy;
			return Row.Length <= 4300 && IsToken(Row);
		}

		internal static bool ExactTopologyProvenance(string RealmId, IList<string> SettlementIds,
			IList<string> Rows, string SeatedId = null, int SeatedVersion = 0,
			KingdomIdentityOrigin SeatedOrigin = KingdomIdentityOrigin.None,
			string SeatedTransaction = null, long SeatedFounded = 0L,
			string SeatedZone = null, string SeatedLegacy = null)
		{
			KingdomIdentityFault topologyFault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out topologyFault) || SettlementIds == null || Rows == null ||
				SettlementIds.Count != Rows.Count) return false;
			for (int i = 0; i < SettlementIds.Count; i++)
			{
				if (i > 0 && string.CompareOrdinal(SettlementIds[i - 1], SettlementIds[i]) >= 0)
					return false;
				if (!TryParseSettlementProvenance(Rows[i], out string id, out int version,
					out KingdomIdentityOrigin origin, out string transaction, out long founded,
					out string zone, out string legacy) || id != SettlementIds[i] ||
					!KingdomIdentityRules.ReproveSettlement(id, RealmId, version, origin,
						transaction, founded, zone, out topologyFault)) return false;
				if (id == SeatedId && (version != SeatedVersion || origin != SeatedOrigin ||
					transaction != (SeatedTransaction ?? "") || founded != SeatedFounded ||
					zone != (SeatedZone ?? "") || legacy != (SeatedLegacy ?? ""))) return false;
			}
			return SeatedId == null || SettlementIds.Contains(SeatedId);
		}

		private static bool TryParseSettlementProvenance(string Row, out string SettlementId,
			out int Version, out KingdomIdentityOrigin Origin, out string TransactionId,
			out long FoundedTick, out string FirstClaimedZone, out string LegacyId)
		{
			SettlementId = null; Version = 0; Origin = KingdomIdentityOrigin.None;
			TransactionId = null; FoundedTick = 0L; FirstClaimedZone = null; LegacyId = null;
			if (string.IsNullOrEmpty(Row) || Row.Length > 4300 || !IsToken(Row)) return false;
			string[] parts = Row.Split(new char[] { '.' }, StringSplitOptions.None);
			if (parts.Length != 7 || !KingdomIdentityRules.IsSettlementId(parts[0]) ||
				!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture,
					out Version) || Version < 0 || Version > 32 ||
				!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture,
					out int origin) || origin < 0 || origin > 2 ||
				!long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture,
					out FoundedTick) || FoundedTick < 0L ||
				!TryUnhex(parts[4], 1024, out TransactionId) ||
				!TryUnhex(parts[5], 1024, out FirstClaimedZone) ||
				!TryUnhex(parts[6], 1024, out LegacyId)) return false;
			SettlementId = parts[0]; Origin = (KingdomIdentityOrigin)origin;
			return true;
		}

		private static bool TryHex(string Value, int MaxBytes, out string Hex)
		{
			Hex = null;
			try
			{
				byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value ?? "");
				if (bytes.Length > MaxBytes) return false;
				StringBuilder text = new StringBuilder(bytes.Length * 2);
				for (int i = 0; i < bytes.Length; i++) text.Append(bytes[i].ToString("x2"));
				Hex = text.ToString();
				return true;
			}
			catch { return false; }
		}

		private static bool TryUnhex(string Hex, int MaxBytes, out string Value)
		{
			Value = null;
			if (Hex == null || (Hex.Length & 1) != 0 || Hex.Length > MaxBytes * 2) return false;
			try
			{
				byte[] bytes = new byte[Hex.Length / 2];
				for (int i = 0; i < bytes.Length; i++)
				{
					int high = HexNibble(Hex[i * 2]); int low = HexNibble(Hex[i * 2 + 1]);
					if (high < 0 || low < 0) return false;
					bytes[i] = (byte)((high << 4) | low);
				}
				Value = new UTF8Encoding(false, true).GetString(bytes);
				return true;
			}
			catch { return false; }
		}

		private static int HexNibble(char Value)
		{
			if (Value >= '0' && Value <= '9') return Value - '0';
			if (Value >= 'a' && Value <= 'f') return Value - 'a' + 10;
			return -1;
		}
	}
}
