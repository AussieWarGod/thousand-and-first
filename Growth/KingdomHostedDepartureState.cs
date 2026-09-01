using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public enum KingdomHostedDeparturePhase : byte
	{
		Pending = 1,
		Settled = 2
	}

	/// <summary>One fixed-slot fence and cross-zone hosted-lift snapshot. Pending is zero by
	/// construction; only an exact final physical-observation CAS may publish Settled.</summary>
	[Serializable]
	public sealed class KingdomHostedDepartureState
	{
		public int Version = 1;
		public KingdomHostedDeparturePhase Phase;
		public int AuthoritySlot;
		public string RealmId;
		public string SettlementId;
		public string ExteriorZoneId;
		public string CarrierId;
		public string AuthorityJobId;
		public string LotKey;
		public string InteriorZoneId;
		public string ReceiptRevision;
		public long ObservedTick;
		public int Roof;
		public int Luxury;
		public int Food;
		public bool FreshWater;
		public ReachBand Band;
		public bool Headed;

		public bool Valid()
		{
			bool identity = Version == 1 && AuthoritySlot >= 0 && AuthoritySlot < 2
				&& Enum.IsDefined(typeof(KingdomHostedDeparturePhase), Phase)
				&& Token(RealmId, 512) && Token(SettlementId, 512)
				&& Token(ExteriorZoneId, 512) && Token(CarrierId, 512)
				&& Token(AuthorityJobId, 512) && Token(LotKey, 64)
				&& Token(InteriorZoneId, 512) && ObservedTick >= 0L
				&& Roof >= 0 && Luxury >= 0 && Food >= 0
				&& Enum.IsDefined(typeof(ReachBand), Band);
			if (!identity) return false;
			if (Phase == KingdomHostedDeparturePhase.Pending)
				return string.IsNullOrEmpty(ReceiptRevision)
					&& Roof == 0 && Luxury == 0 && Food == 0;
			return Token(ReceiptRevision, 128);
		}

		public KingdomHostedDepartureState Copy()
		{
			return (KingdomHostedDepartureState)MemberwiseClone();
		}

		private static bool Token(string Value, int Maximum)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= Maximum
				&& Value.IndexOf('\0') < 0;
		}
	}

	/// <summary>Engine-free identity and reach decisions for the fixed departure store.</summary>
	public static class KingdomHostedDepartureRules
	{
		public const string SlotPrefix = "r_TAF_HostedDepartureV1:";

		/// <summary>Proves that a decoded record occupies its one canonical fixed slot.</summary>
		public static bool SlotKeyMatches(string Key, KingdomHostedDepartureState State)
		{
			if (State == null || !State.Valid()) return false;
			string suffix = State.LotKey == KingdomHostedArcologyTopology.WardLotKey
				? ":ward" : State.LotKey == KingdomHostedArcologyTopology.TerraceLotKey
				? ":terrace" : null;
			return suffix != null && string.Equals(Key,
				SlotPrefix + State.AuthoritySlot + suffix, StringComparison.Ordinal);
		}

		public static bool Matches(KingdomHostedDepartureState State, int Slot,
			KingdomHostedArcologyAuthority Authority, string LotKey)
		{
			return State != null && State.Valid() && Authority != null && Authority.Valid()
				&& State.AuthoritySlot == Slot && State.RealmId == Authority.RealmId
				&& State.SettlementId == Authority.SettlementId
				&& State.ExteriorZoneId == Authority.ZoneId
				&& State.CarrierId == Authority.CarrierId
				&& State.AuthorityJobId == Authority.ConstructionJobId
				&& State.LotKey == LotKey;
		}

		public static ReachBand EffectiveBand(KingdomHostedDepartureState State)
		{
			if (State == null || !State.Valid()) return ReachBand.Plot;
			return !KingdomReachRules.RequiresSeat(State.Band) || State.Headed
				? State.Band : KingdomReachRules.Unheaded(State.Band);
		}

		public static int LuxuryFor(KingdomHostedDepartureState State,
			string SettlementId, string ExceptZoneId)
		{
			if (State == null || !State.Valid()
				|| State.Phase != KingdomHostedDeparturePhase.Settled
				|| State.Luxury <= 0 || string.IsNullOrEmpty(SettlementId)
				|| State.ExteriorZoneId == ExceptZoneId
				|| State.InteriorZoneId == ExceptZoneId) return 0;
			ReachBand band = EffectiveBand(State);
			if (band < ReachBand.City) return 0;
			return band == ReachBand.Realm || State.SettlementId == SettlementId
				? State.Luxury : 0;
		}

		public static int BindingFor(KingdomHostedDepartureState State, string Kind,
			string SettlementId, string ExceptZoneId)
		{
			if (State == null || !State.Valid()
				|| State.Phase != KingdomHostedDeparturePhase.Settled
				|| State.SettlementId != SettlementId
				|| State.ExteriorZoneId == ExceptZoneId
				|| State.InteriorZoneId == ExceptZoneId)
				return 0;
			if (Kind == KingdomCatalogueRules.SupportRoof) return State.Roof;
			if (Kind == KingdomCatalogueRules.SupportFood) return State.Food;
			return 0;
		}
	}

	/// <summary>Canonical bounded wire form for a save-persistent fixed slot.</summary>
	public static class KingdomHostedDepartureCodec
	{
		private const string Magic = "TAF-HOSTED-DEPARTURE-V1";
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		public static string Encode(KingdomHostedDepartureState State)
		{
			if (State == null || !State.Valid()) return "";
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
			{
				writer.Write(Magic); writer.Write(State.Version); writer.Write((byte)State.Phase);
				writer.Write(State.AuthoritySlot); Text(writer, State.RealmId);
				Text(writer, State.SettlementId); Text(writer, State.ExteriorZoneId);
				Text(writer, State.CarrierId); Text(writer, State.AuthorityJobId);
				Text(writer, State.LotKey); Text(writer, State.InteriorZoneId);
				Text(writer, State.ReceiptRevision); writer.Write(State.ObservedTick);
				writer.Write(State.Roof); writer.Write(State.Luxury); writer.Write(State.Food);
				writer.Write(State.FreshWater); writer.Write((byte)State.Band);
				writer.Write(State.Headed); writer.Flush();
				return Convert.ToBase64String(stream.ToArray());
			}
		}

		public static bool TryDecode(string Encoded, out KingdomHostedDepartureState State)
		{
			State = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > 8192) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded);
				using (BinaryReader reader = new BinaryReader(new MemoryStream(bytes), Utf8, false))
				{
					if (reader.ReadString() != Magic) return false;
					State = new KingdomHostedDepartureState {
						Version = reader.ReadInt32(),
						Phase = (KingdomHostedDeparturePhase)reader.ReadByte(),
						AuthoritySlot = reader.ReadInt32(), RealmId = Text(reader, 512),
						SettlementId = Text(reader, 512), ExteriorZoneId = Text(reader, 512),
						CarrierId = Text(reader, 512), AuthorityJobId = Text(reader, 512),
						LotKey = Text(reader, 64), InteriorZoneId = Text(reader, 512),
						ReceiptRevision = Text(reader, 128), ObservedTick = reader.ReadInt64(),
						Roof = reader.ReadInt32(), Luxury = reader.ReadInt32(),
						Food = reader.ReadInt32(), FreshWater = reader.ReadBoolean(),
						Band = (ReachBand)reader.ReadByte(),
						Headed = reader.ReadBoolean() };
					return reader.BaseStream.Position == reader.BaseStream.Length && State.Valid();
				}
			}
			catch { State = null; return false; }
		}

		private static void Text(BinaryWriter Writer, string Value)
		{
			Writer.Write(Value ?? "");
		}

		private static string Text(BinaryReader Reader, int Maximum)
		{
			string value = Reader.ReadString();
			if (value.Length > Maximum) throw new InvalidDataException();
			return value;
		}
	}
}
