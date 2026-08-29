using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomHostedArcologyAuthority
	{
		public int Version = 1;
		public KingdomHostedAuthorityPhase Phase;
		public string RealmId;
		public string SettlementId;
		public string ZoneId;
		public string CarrierId;
		public string ConstructionJobId;
		public string Fault;

		public bool Valid()
		{
			return Version == 1 && Enum.IsDefined(typeof(KingdomHostedAuthorityPhase), Phase)
				&& Bounded(RealmId) && Bounded(SettlementId) && Bounded(ZoneId)
				&& Bounded(CarrierId)
				&& (Phase == KingdomHostedAuthorityPhase.Quarantined
					? Optional(ConstructionJobId) : Bounded(ConstructionJobId))
				&& (Phase == KingdomHostedAuthorityPhase.Quarantined
					? Bounded(Fault) : string.IsNullOrEmpty(Fault));
		}

		private static bool Bounded(string V) { return !string.IsNullOrEmpty(V) && V.Length <= 512; }
		private static bool Optional(string V) { return string.IsNullOrEmpty(V) || V.Length <= 512; }
	}

	[Serializable]
	public sealed class KingdomHostedLotReceipt
	{
		public int Version = 1;
		public KingdomHostedLotPhase Phase;
		public string LotKey;
		public string JobId;
		public string RootId;
		public string Supports;
		public int Remaining;
		public long LastTick;
		public int StaffingBasis;
		public bool RequiresWater;
		public string Fault;

		public bool Valid()
		{
			bool identity = Version == 1 && Phase != KingdomHostedLotPhase.Dormant
				&& Enum.IsDefined(typeof(KingdomHostedLotPhase), Phase)
				&& !string.IsNullOrEmpty(LotKey) && LotKey.Length <= 64
				&& Bounded(JobId, 128) && Bounded(RootId, 512) && Bounded(Supports, 512)
				&& Remaining >= 0 && LastTick >= 0L && StaffingBasis >= 0 && StaffingBasis <= 100;
			return identity && (Phase == KingdomHostedLotPhase.Working ? Remaining >= 0
				: Remaining == 0) && (Phase == KingdomHostedLotPhase.Quarantined
				? Bounded(Fault, 512) : string.IsNullOrEmpty(Fault));
		}

		private static bool Bounded(string V, int Max)
		{
			return !string.IsNullOrEmpty(V) && V.Length <= Max;
		}
	}

	/// <summary>Bounded versioned binary envelope; delimiters inside engine IDs stay harmless.</summary>
	public static class KingdomHostedArcologyReceiptCodec
	{
		private const string AuthorityMagic = "TAF-HOSTED-AUTHORITY-V1";
		private const string LotMagic = "TAF-HOSTED-LOT-V1";

		public static string EncodeAuthority(KingdomHostedArcologyAuthority R)
		{
			if (R == null || !R.Valid()) return "";
			return Write(delegate(BinaryWriter w) { w.Write(AuthorityMagic); w.Write(R.Version);
				w.Write((byte)R.Phase); S(w, R.RealmId); S(w, R.SettlementId); S(w, R.ZoneId);
				S(w, R.CarrierId); S(w, R.ConstructionJobId); S(w, R.Fault); });
		}

		public static bool TryDecodeAuthority(string Encoded, out KingdomHostedArcologyAuthority R)
		{
			R = null; try { using (BinaryReader b = Read(Encoded)) { if (b == null || b.ReadString() != AuthorityMagic) return false;
				R = new KingdomHostedArcologyAuthority { Version = b.ReadInt32(), Phase = (KingdomHostedAuthorityPhase)b.ReadByte(),
					RealmId = G(b), SettlementId = G(b), ZoneId = G(b), CarrierId = G(b), ConstructionJobId = G(b), Fault = G(b) };
				return End(b) && R.Valid(); } } catch { R = null; return false; }
		}

		public static string EncodeLot(KingdomHostedLotReceipt R)
		{
			if (R == null || !R.Valid()) return "";
			return Write(delegate(BinaryWriter w) { w.Write(LotMagic); w.Write(R.Version); w.Write((byte)R.Phase);
				S(w, R.LotKey); S(w, R.JobId); S(w, R.RootId); S(w, R.Supports); w.Write(R.Remaining);
				w.Write(R.LastTick); w.Write(R.StaffingBasis); w.Write(R.RequiresWater); S(w, R.Fault); });
		}

		public static bool TryDecodeLot(string Encoded, out KingdomHostedLotReceipt R)
		{
			R = null; try { using (BinaryReader b = Read(Encoded)) { if (b == null || b.ReadString() != LotMagic) return false;
				R = new KingdomHostedLotReceipt { Version = b.ReadInt32(), Phase = (KingdomHostedLotPhase)b.ReadByte(),
					LotKey = G(b), JobId = G(b), RootId = G(b), Supports = G(b), Remaining = b.ReadInt32(),
					LastTick = b.ReadInt64(), StaffingBasis = b.ReadInt32(), RequiresWater = b.ReadBoolean(), Fault = G(b) };
				return End(b) && R.Valid(); } } catch { R = null; return false; }
		}

		private static string Write(Action<BinaryWriter> Body) { using (MemoryStream s = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(s, new UTF8Encoding(false, true), true)) { Body(w); w.Flush(); return Convert.ToBase64String(s.ToArray()); } }
		private static BinaryReader Read(string E) { if (string.IsNullOrEmpty(E) || E.Length > 8192) return null;
			return new BinaryReader(new MemoryStream(Convert.FromBase64String(E)), new UTF8Encoding(false, true), false); }
		private static void S(BinaryWriter W, string V) { W.Write(V ?? ""); }
		private static string G(BinaryReader R) { string v = R.ReadString(); if (v.Length > 512) throw new InvalidDataException(); return v; }
		private static bool End(BinaryReader R) { return R.BaseStream.Position == R.BaseStream.Length; }
	}
}
