using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceCodec
	{
		private static byte[] EncodePayload(KingdomExperienceLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				w.Write(L.FormatVersion); w.Write((byte)L.SchemaState); WriteString(w, L.SchemaFault);
				w.Write(L.MigratedFromVersion); WriteString(w, L.RealmId);
				WriteBool(w, L.IdentityBound); w.Write(L.Revision);
				WriteOption(w, L.Story); WriteOption(w, L.Knowledge); WriteOption(w, L.Ambient);
				WriteList(w, L.Audiences, KingdomExperienceRules.MaxAudienceReceipts,
					WriteAudienceCompact);
				WriteList(w, L.BodyReservations, KingdomExperienceRules.MaxBodyReservations,
					WriteBodyCompact);
				WriteList(w, L.Offices, KingdomExperienceRules.MaxOfficeReceipts, WriteOffice);
				WriteList(w, L.Remembrances, KingdomExperienceRules.MaxRemembranceReceipts,
					WriteRemembrance);
				WriteList(w, L.Voices, KingdomExperienceRules.MaxVoiceReceipts, WriteVoice);
				WriteList(w, L.FirstFeasts, KingdomExperienceRules.MaxFirstFeastReceipts,
					WriteFirstFeast);
				w.Flush();
				if (stream.Length > KingdomExperienceRules.MaxDeclaredPayloadBytes)
					throw new InvalidDataException("Experience payload exceeds declared byte budget.");
				return stream.ToArray();
			}
		}

		private static KingdomExperienceLedger DecodePayload(byte[] Payload)
		{
			return DecodePayload(Payload, HasCivicRows: true, HasVoices: true,
				HasFirstFeasts: true, CompactRealm: true, SourceWire: CurrentWireVersion);
		}

		private static KingdomExperienceLedger DecodePayloadV3(byte[] Payload)
		{
			return DecodePayload(Payload, HasCivicRows: true, HasVoices: true,
				HasFirstFeasts: false, CompactRealm: true, SourceWire: 3);
		}

		private static KingdomExperienceLedger DecodePayloadV2(byte[] Payload)
		{
			return DecodePayload(Payload, HasCivicRows: true, HasVoices: false,
				HasFirstFeasts: false, CompactRealm: false, SourceWire: 2);
		}

		private static KingdomExperienceLedger DecodePayloadV1(byte[] Payload)
		{
			return DecodePayload(Payload, HasCivicRows: false, HasVoices: false,
				HasFirstFeasts: false, CompactRealm: false, SourceWire: 1);
		}

		private static KingdomExperienceLedger DecodePayload(byte[] Payload, bool HasCivicRows,
			bool HasVoices, bool HasFirstFeasts, bool CompactRealm, int SourceWire)
		{
			if (Payload.Length > KingdomExperienceRules.MaxDeclaredPayloadBytes)
				throw new InvalidDataException("Experience payload exceeds declared byte budget.");
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				int format = r.ReadInt32();
				if (format != SourceWire)
					throw new InvalidDataException("Experience payload format is invalid.");
				KingdomExperienceSchemaState schema = (KingdomExperienceSchemaState)r.ReadByte();
				string schemaFault = ReadString(r); int migrated = r.ReadInt32();
				string realm = ReadString(r); bool bound = ReadBool(r); long revision = r.ReadInt64();
				KingdomExperienceLedger l = new KingdomExperienceLedger
				{
					FormatVersion = KingdomExperienceRules.CurrentFormatVersion,
					SchemaState = schema, SchemaFault = schemaFault,
					MigratedFromVersion = SourceWire == CurrentWireVersion ? migrated : 0,
					RealmId = realm,
					IdentityBound = bound, Revision = revision,
					Story = ReadOption(r), Knowledge = ReadOption(r), Ambient = ReadOption(r),
					Audiences = ReadList(r, KingdomExperienceRules.MaxAudienceReceipts,
						CompactRealm ? (RowReader<KingdomExperienceAudienceReceipt>)
							(R => ReadAudienceCompact(R, realm)) : ReadAudience),
					BodyReservations = ReadList(r, KingdomExperienceRules.MaxBodyReservations,
						CompactRealm ? (RowReader<KingdomExperienceBodyReservation>)
							(R => ReadBodyCompact(R, realm)) : ReadBody),
					Offices = HasCivicRows
						? ReadList(r, KingdomExperienceRules.MaxOfficeReceipts, ReadOffice)
						: new System.Collections.Generic.List<KingdomCivicOfficeReceipt>(),
					Remembrances = HasCivicRows
						? ReadList(r, KingdomExperienceRules.MaxRemembranceReceipts, ReadRemembrance)
						: new System.Collections.Generic.List<KingdomRemembranceReceipt>(),
					Voices = HasVoices
						? ReadList(r, KingdomExperienceRules.MaxVoiceReceipts, ReadVoice)
						: new System.Collections.Generic.List<KingdomCivicVoiceReceipt>(),
					FirstFeasts = HasFirstFeasts
						? ReadList(r, KingdomExperienceRules.MaxFirstFeastReceipts, ReadFirstFeast)
						: new System.Collections.Generic.List<KingdomFirstFeastReceipt>()
				};
				RequireEnd(stream);
				if (KingdomExperienceRules.TryValidate(l, out string failure)) return l;
				return new KingdomExperienceLedger
				{
					SchemaState = KingdomExperienceSchemaState.Quarantined,
					SchemaFault = KingdomExperienceRules.Text(failure, true) ? failure
						: "Experience authority decoded with invalid bounded evidence.",
					OpaqueWireVersion = SourceWire,
					OpaqueFuturePayload = (byte[])Payload.Clone()
				};
			}
		}

#if TAF_TESTS
		/// <summary>Produces an authentic W0 wire-v1 envelope so migration and quarantine tests do
		/// not counterfeit framing bytes. Civic rows cannot be represented and are refused.</summary>
		public static byte[] EncodeLegacyV1Fixture(KingdomExperienceLedger L)
		{
			if (L == null || L.Offices == null || L.Offices.Count != 0
				|| L.Remembrances == null || L.Remembrances.Count != 0
				|| L.Voices == null || L.Voices.Count != 0
				|| L.FirstFeasts == null || L.FirstFeasts.Count != 0)
				throw new InvalidDataException("Legacy experience fixtures cannot carry civic rows.");
			if (!KingdomExperienceRules.TryValidate(L, out string failure))
				throw new InvalidDataException("Invalid legacy fixture source: " + failure);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				w.Write(1); w.Write((byte)L.SchemaState); WriteString(w, L.SchemaFault);
				w.Write(L.MigratedFromVersion); WriteString(w, L.RealmId);
				WriteBool(w, L.IdentityBound); w.Write(L.Revision);
				WriteOption(w, L.Story); WriteOption(w, L.Knowledge); WriteOption(w, L.Ambient);
				WriteList(w, L.Audiences, KingdomExperienceRules.MaxAudienceReceipts,
					WriteAudience);
				WriteList(w, L.BodyReservations, KingdomExperienceRules.MaxBodyReservations,
					WriteBody);
				w.Flush(); return Frame(1, stream.ToArray());
			}
		}

		/// <summary>Authentic full-row wire-v2 fixture for compact-v3 migration tests.</summary>
		public static byte[] EncodeLegacyV2Fixture(KingdomExperienceLedger L)
		{
			if (L == null || L.Voices == null || L.Voices.Count != 0
				|| L.FirstFeasts == null || L.FirstFeasts.Count != 0)
				throw new InvalidDataException("Wire-v2 fixtures cannot carry civic voices.");
			if (!KingdomExperienceRules.TryValidate(L, out string failure))
				throw new InvalidDataException("Invalid wire-v2 fixture source: " + failure);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				w.Write(2); w.Write((byte)L.SchemaState); WriteString(w, L.SchemaFault);
				w.Write(L.MigratedFromVersion); WriteString(w, L.RealmId);
				WriteBool(w, L.IdentityBound); w.Write(L.Revision);
				WriteOption(w, L.Story); WriteOption(w, L.Knowledge); WriteOption(w, L.Ambient);
				WriteList(w, L.Audiences, KingdomExperienceRules.MaxAudienceReceipts, WriteAudience);
				WriteList(w, L.BodyReservations, KingdomExperienceRules.MaxBodyReservations, WriteBody);
				WriteList(w, L.Offices, KingdomExperienceRules.MaxOfficeReceipts, WriteOffice);
				WriteList(w, L.Remembrances, KingdomExperienceRules.MaxRemembranceReceipts,
					WriteRemembrance);
				w.Flush(); return Frame(2, stream.ToArray());
			}
		}

		/// <summary>Authentic compact wire-v3 fixture. First Feast did not exist.</summary>
		public static byte[] EncodeLegacyV3Fixture(KingdomExperienceLedger L)
		{
			if (L == null || L.FirstFeasts == null || L.FirstFeasts.Count != 0)
				throw new InvalidDataException("Wire-v3 fixtures cannot carry first-feast rows.");
			if (!KingdomExperienceRules.TryValidate(L, out string failure))
				throw new InvalidDataException("Invalid wire-v3 fixture source: " + failure);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				w.Write(3); w.Write((byte)L.SchemaState); WriteString(w, L.SchemaFault);
				w.Write(L.MigratedFromVersion); WriteString(w, L.RealmId);
				WriteBool(w, L.IdentityBound); w.Write(L.Revision);
				WriteOption(w, L.Story); WriteOption(w, L.Knowledge); WriteOption(w, L.Ambient);
				WriteList(w, L.Audiences, KingdomExperienceRules.MaxAudienceReceipts,
					WriteAudienceCompact);
				WriteList(w, L.BodyReservations, KingdomExperienceRules.MaxBodyReservations,
					WriteBodyCompact);
				WriteList(w, L.Offices, KingdomExperienceRules.MaxOfficeReceipts, WriteOffice);
				WriteList(w, L.Remembrances, KingdomExperienceRules.MaxRemembranceReceipts,
					WriteRemembrance);
				WriteList(w, L.Voices, KingdomExperienceRules.MaxVoiceReceipts, WriteVoice);
				w.Flush(); return Frame(3, stream.ToArray());
			}
		}
#endif

		private static void WriteOption(BinaryWriter W, KingdomExperienceOptionReceipt O)
		{
			W.Write((byte)O.Kind); W.Write((byte)O.State); W.Write(O.ObservedTick);
			W.Write(O.EnableEpoch); W.Write(O.FutureCauseFloorTick);
		}

		private static KingdomExperienceOptionReceipt ReadOption(BinaryReader R)
		{
			return new KingdomExperienceOptionReceipt
			{
				Kind = (KingdomExperienceOptionKind)R.ReadByte(),
				State = (KingdomExperienceOptionState)R.ReadByte(),
				ObservedTick = R.ReadInt64(), EnableEpoch = R.ReadInt64(),
				FutureCauseFloorTick = R.ReadInt64()
			};
		}

		private static void WriteAudience(BinaryWriter W, KingdomExperienceAudienceReceipt R)
		{
			WriteString(W, R.ReservationId); WriteString(W, R.RealmId);
			WriteString(W, R.SettlementId);
			WriteString(W, R.SourceId); W.Write((byte)R.Lane); W.Write((byte)R.OptionKind);
			W.Write(R.CauseTick); W.Write(R.ReservedTick); W.Write(R.EnableEpoch);
		}

		private static KingdomExperienceAudienceReceipt ReadAudience(BinaryReader R)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = ReadString(R), RealmId = ReadString(R),
				SettlementId = ReadString(R), SourceId = ReadString(R),
				Lane = (KingdomExperienceLane)R.ReadByte(),
				OptionKind = (KingdomExperienceOptionKind)R.ReadByte(),
				CauseTick = R.ReadInt64(), ReservedTick = R.ReadInt64(),
				EnableEpoch = R.ReadInt64()
			};
		}

		private static void WriteAudienceCompact(BinaryWriter W,
			KingdomExperienceAudienceReceipt R)
		{
			WriteString(W, R.ReservationId); WriteString(W, R.SettlementId);
			WriteString(W, R.SourceId); W.Write((byte)R.Lane); W.Write((byte)R.OptionKind);
			W.Write(R.CauseTick); W.Write(R.ReservedTick); W.Write(R.EnableEpoch);
		}

		private static KingdomExperienceAudienceReceipt ReadAudienceCompact(BinaryReader R,
			string RealmId)
		{
			KingdomExperienceAudienceReceipt row = ReadAudienceWithoutRealm(R);
			row.RealmId = RealmId; return row;
		}

		private static KingdomExperienceAudienceReceipt ReadAudienceWithoutRealm(BinaryReader R)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = ReadString(R), SettlementId = ReadString(R), SourceId = ReadString(R),
				Lane = (KingdomExperienceLane)R.ReadByte(),
				OptionKind = (KingdomExperienceOptionKind)R.ReadByte(), CauseTick = R.ReadInt64(),
				ReservedTick = R.ReadInt64(), EnableEpoch = R.ReadInt64()
			};
		}

		private static void WriteBody(BinaryWriter W, KingdomExperienceBodyReservation R)
		{
			WriteString(W, R.ReservationId); WriteString(W, R.RealmId);
			WriteString(W, R.SettlementId);
			WriteString(W, R.SourceId); W.Write((byte)R.Lane); W.Write((byte)R.OptionKind);
			W.Write(R.CauseTick); W.Write(R.ReservedTick); W.Write(R.EnableEpoch);
			W.Write(R.BodyCount);
		}

		private static KingdomExperienceBodyReservation ReadBody(BinaryReader R)
		{
			return new KingdomExperienceBodyReservation
			{
				ReservationId = ReadString(R), RealmId = ReadString(R),
				SettlementId = ReadString(R), SourceId = ReadString(R),
				Lane = (KingdomExperienceLane)R.ReadByte(),
				OptionKind = (KingdomExperienceOptionKind)R.ReadByte(),
				CauseTick = R.ReadInt64(), ReservedTick = R.ReadInt64(),
				EnableEpoch = R.ReadInt64(), BodyCount = R.ReadInt32()
			};
		}

		private static void WriteBodyCompact(BinaryWriter W, KingdomExperienceBodyReservation R)
		{
			WriteString(W, R.ReservationId); WriteString(W, R.SettlementId);
			WriteString(W, R.SourceId); W.Write((byte)R.Lane); W.Write((byte)R.OptionKind);
			W.Write(R.CauseTick); W.Write(R.ReservedTick); W.Write(R.EnableEpoch); W.Write(R.BodyCount);
		}

		private static KingdomExperienceBodyReservation ReadBodyCompact(BinaryReader R,
			string RealmId)
		{
			return new KingdomExperienceBodyReservation
			{
				ReservationId = ReadString(R), RealmId = RealmId, SettlementId = ReadString(R),
				SourceId = ReadString(R), Lane = (KingdomExperienceLane)R.ReadByte(),
				OptionKind = (KingdomExperienceOptionKind)R.ReadByte(), CauseTick = R.ReadInt64(),
				ReservedTick = R.ReadInt64(), EnableEpoch = R.ReadInt64(), BodyCount = R.ReadInt32()
			};
		}
	}
}
