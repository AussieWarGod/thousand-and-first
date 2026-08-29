using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Engine-free four-bay identity, transition, and recovery law.</summary>
	public static partial class KingdomStasisVaultRules
	{
		public const int CurrentReceiptVersion = 1;
		public const int MaxSlots = 4;
		public const int MaxIdentityChars = 1024;
		public const int MaxNameChars = 192;
		public const int MaxFaultChars = 512;

		public static bool TryPrepare(int Slot, int Generation, string RealmId,
			string SettlementId, string ZoneId, string VaultId, string LotId,
			string CradleId, string BodyId, string SubjectId, string BodyBlueprint,
			string BodyName, string InventoryFingerprint, string EquipmentFingerprint,
			string EffectFingerprint, long Tick, out KingdomStasisCustodyReceipt Receipt,
			out string Failure)
		{
			Receipt = null;
			Failure = "";
			string realm = Clean(RealmId, MaxIdentityChars);
			string settlement = Clean(SettlementId, MaxIdentityChars);
			string zone = Clean(ZoneId, MaxIdentityChars);
			string vault = Clean(VaultId, MaxIdentityChars);
			string lot = Clean(LotId, MaxIdentityChars);
			string cradle = Clean(CradleId, MaxIdentityChars);
			string body = Clean(BodyId, MaxIdentityChars);
			string subject = Clean(SubjectId, MaxIdentityChars);
			string blueprint = Clean(BodyBlueprint, MaxIdentityChars);
			string name = Clean(BodyName, MaxNameChars);
			if (Slot < 0 || Slot >= MaxSlots || Generation <= 0 || Tick < 0L
				|| string.IsNullOrEmpty(realm) || string.IsNullOrEmpty(settlement)
				|| string.IsNullOrEmpty(zone) || string.IsNullOrEmpty(vault)
				|| string.IsNullOrEmpty(lot) || string.IsNullOrEmpty(cradle)
				|| string.IsNullOrEmpty(body) || string.IsNullOrEmpty(subject)
				|| string.IsNullOrEmpty(blueprint) || string.IsNullOrEmpty(name)
				|| !DigestShape(InventoryFingerprint) || !DigestShape(EquipmentFingerprint)
				|| !DigestShape(EffectFingerprint))
				return Fail("stasis preparation lacks bounded exact evidence", out Failure);
			string custody = CustodyId(realm, settlement, vault, body, Generation);
			Receipt = new KingdomStasisCustodyReceipt
			{
				Version = CurrentReceiptVersion,
				Phase = KingdomStasisCustodyPhase.Prepared,
				Slot = Slot,
				Generation = Generation,
				CustodyId = custody,
				RealmId = realm,
				SettlementId = settlement,
				ZoneId = zone,
				VaultObjectId = vault,
				LotId = lot,
				CradleObjectId = cradle,
				FieldObjectId = custody + ":field",
				BodyObjectId = body,
				SubjectObjectId = subject,
				BodyBlueprint = blueprint,
				BodyName = name,
				InventoryFingerprint = InventoryFingerprint,
				EquipmentFingerprint = EquipmentFingerprint,
				EffectFingerprint = EffectFingerprint,
				EnteredTick = Tick
			};
			return Validate(Receipt, out Failure);
		}

		public static bool Validate(KingdomStasisCustodyReceipt Receipt,
			out string Failure)
		{
			Failure = "";
			if (Receipt == null || Receipt.Version != CurrentReceiptVersion
				|| !Enum.IsDefined(typeof(KingdomStasisCustodyPhase), Receipt.Phase))
				return Fail("unknown stasis receipt version or phase", out Failure);
			if (Receipt.Phase == KingdomStasisCustodyPhase.Quarantined)
				return QuarantineShape(Receipt)
					|| Fail("quarantined stasis evidence is unbounded", out Failure);
			if (Receipt.Slot < 0 || Receipt.Slot >= MaxSlots || Receipt.Generation <= 0
				|| Receipt.EnteredTick < 0L || Receipt.ReleasedTick < 0L
				|| !Bounded(Receipt.CustodyId, MaxIdentityChars)
				|| !Bounded(Receipt.RealmId, MaxIdentityChars)
				|| !Bounded(Receipt.SettlementId, MaxIdentityChars)
				|| !Bounded(Receipt.ZoneId, MaxIdentityChars)
				|| !Bounded(Receipt.VaultObjectId, MaxIdentityChars)
				|| !Bounded(Receipt.LotId, MaxIdentityChars)
				|| !Bounded(Receipt.CradleObjectId, MaxIdentityChars)
				|| !Bounded(Receipt.FieldObjectId, MaxIdentityChars)
				|| !Bounded(Receipt.BodyObjectId, MaxIdentityChars)
				|| !Bounded(Receipt.SubjectObjectId, MaxIdentityChars)
				|| !Bounded(Receipt.BodyBlueprint, MaxIdentityChars)
				|| !Bounded(Receipt.BodyName, MaxNameChars)
				|| !DigestShape(Receipt.InventoryFingerprint)
				|| !DigestShape(Receipt.EquipmentFingerprint)
				|| !DigestShape(Receipt.EffectFingerprint)
				|| (Receipt.Phase == KingdomStasisCustodyPhase.Released
					? !OptionalBounded(Receipt.Fault, MaxFaultChars)
					: !string.IsNullOrEmpty(Receipt.Fault)))
				return Fail("stasis receipt has malformed bounded evidence", out Failure);
			if (Receipt.CustodyId != CustodyId(Receipt.RealmId, Receipt.SettlementId,
				Receipt.VaultObjectId, Receipt.BodyObjectId, Receipt.Generation))
				return Fail("stasis custody identity diverged", out Failure);
			if (Receipt.FieldObjectId != Receipt.CustodyId + ":field")
				return Fail("stasis field identity diverged", out Failure);
			if (Receipt.Phase == KingdomStasisCustodyPhase.Released)
				return Receipt.ReleasedTick >= Receipt.EnteredTick
					|| Fail("released stasis receipt lacks terminal time", out Failure);
			return Receipt.ReleasedTick == 0L
				|| Fail("open stasis receipt carries terminal time", out Failure);
		}

		public static bool SameAuthority(KingdomStasisCustodyReceipt Left,
			KingdomStasisCustodyReceipt Right)
		{
			if (Left == null || Right == null) return false;
			return Left.Version == Right.Version && Left.Slot == Right.Slot
				&& Left.Generation == Right.Generation && Left.CustodyId == Right.CustodyId
				&& Left.RealmId == Right.RealmId && Left.SettlementId == Right.SettlementId
				&& Left.ZoneId == Right.ZoneId && Left.VaultObjectId == Right.VaultObjectId
				&& Left.LotId == Right.LotId && Left.CradleObjectId == Right.CradleObjectId
				&& Left.FieldObjectId == Right.FieldObjectId
				&& Left.BodyObjectId == Right.BodyObjectId
				&& Left.SubjectObjectId == Right.SubjectObjectId;
		}

		public static string Fingerprint(params string[] Rows)
		{
			return Digest("TAF-STASIS-MANIFEST-V1", Rows);
		}

		private static string CustodyId(string Realm, string Settlement, string Vault,
			string Body, int Generation)
		{
			return "taf:stasis:v1:" + Digest("TAF-STASIS-CUSTODY-V1", new[] { Realm,
				Settlement, Vault, Body, Generation.ToString(CultureInfo.InvariantCulture) });
		}

		private static bool QuarantineShape(KingdomStasisCustodyReceipt R)
		{
			return R.Slot >= 0 && R.Slot < MaxSlots && R.Generation >= 0
				&& R.EnteredTick >= 0L && R.ReleasedTick >= 0L
				&& OptionalBounded(R.CustodyId, MaxIdentityChars)
				&& OptionalBounded(R.RealmId, MaxIdentityChars)
				&& OptionalBounded(R.SettlementId, MaxIdentityChars)
				&& OptionalBounded(R.ZoneId, MaxIdentityChars)
				&& OptionalBounded(R.VaultObjectId, MaxIdentityChars)
				&& OptionalBounded(R.LotId, MaxIdentityChars)
				&& OptionalBounded(R.CradleObjectId, MaxIdentityChars)
				&& OptionalBounded(R.FieldObjectId, MaxIdentityChars)
				&& OptionalBounded(R.BodyObjectId, MaxIdentityChars)
				&& OptionalBounded(R.SubjectObjectId, MaxIdentityChars)
				&& OptionalBounded(R.BodyBlueprint, MaxIdentityChars)
				&& OptionalBounded(R.BodyName, MaxNameChars)
				&& OptionalDigest(R.InventoryFingerprint)
				&& OptionalDigest(R.EquipmentFingerprint)
				&& OptionalDigest(R.EffectFingerprint)
				&& Bounded(R.Fault, MaxFaultChars);
		}

		private static bool OptionalDigest(string Value)
		{
			return string.IsNullOrEmpty(Value) || DigestShape(Value);
		}

		private static bool DigestShape(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static string Digest(string Domain, string[] Rows)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					writer.Write(Domain ?? "");
					for (int i = 0; i < Rows.Length; i++) writer.Write(Rows[i] ?? "");
					writer.Flush();
					using (SHA256 sha = SHA256.Create())
					{
						byte[] hash = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2",
							CultureInfo.InvariantCulture));
						return text.ToString();
					}
				}
			}
			catch { return ""; }
		}

		private static string Clean(string Value, int Limit)
		{
			if (string.IsNullOrWhiteSpace(Value) || Limit < 1) return "";
			StringBuilder text = new StringBuilder(Math.Min(Value.Length, Limit));
			bool space = false;
			for (int i = 0; i < Value.Length && text.Length < Limit; i++)
			{
				char c = Value[i];
				if (char.IsControl(c) || char.IsWhiteSpace(c)) { space = text.Length > 0; continue; }
				if (space && text.Length < Limit) text.Append(' ');
				space = false;
				if (text.Length < Limit) text.Append(c);
			}
			return text.ToString().Trim();
		}

		private static bool Bounded(string Value, int Limit)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= Limit
				&& Value == Clean(Value, Limit);
		}

		private static bool OptionalBounded(string Value, int Limit)
		{
			return string.IsNullOrEmpty(Value) || Bounded(Value, Limit);
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
