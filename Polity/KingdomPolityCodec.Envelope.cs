using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private const int Magic = 0x54415032; // TAP2
		public const int CurrentWireVersion = 6;
		public const int ImmediatePriorWireVersion = 5;
		public const int PriorWireVersion = 4;
		public const int OlderWireVersion = 3;
		public const int OldestWireVersion = 2;
		public const int LegacyWireVersion = 1;
		public const int MaxEnvelopeBytes = 1024 * 1024;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static byte[] EncodeEnvelope(KingdomPolityLedger Ledger)
		{
			if (Ledger == null) throw new ArgumentNullException(nameof(Ledger));
			int wire = CurrentWireVersion; byte[] payload;
			if (Ledger.SchemaState == KingdomPolitySchemaState.Unknown)
			{
				wire = Ledger.OpaqueWireVersion;
				if (wire <= CurrentWireVersion || Ledger.OpaqueFuturePayload == null ||
					Ledger.OpaqueFuturePayload.Length > MaxEnvelopeBytes - 12)
					throw new InvalidDataException("Opaque polity wire evidence is invalid.");
				payload = (byte[])Ledger.OpaqueFuturePayload.Clone();
			}
			else
			{
				RequireEncodable(Ledger);
				payload = EncodePayloadV6(Ledger);
			}
			return Frame(wire, payload);
		}

		public static KingdomPolityLedger DecodeEnvelope(byte[] Envelope)
		{
			KingdomPolityLedger ledger = DecodeEnvelopeRaw(Envelope);
			KingdomPolityRules.Normalize(ledger);
			return ledger;
		}

		public static KingdomPolityLedger DecodeEnvelopeRaw(byte[] Envelope)
		{
			if (Envelope == null || Envelope.Length < 12 || Envelope.Length > MaxEnvelopeBytes)
				throw new InvalidDataException("Polity envelope length is invalid.");
			using (MemoryStream stream = new MemoryStream(Envelope, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				if (reader.ReadInt32() != Magic) throw new InvalidDataException("Polity marker is invalid.");
				int wire = reader.ReadInt32();
				int length = ReadCount(reader, MaxEnvelopeBytes - 12, "payload bytes");
				if (length != stream.Length - stream.Position)
					throw new InvalidDataException("Polity payload length mismatch.");
				byte[] payload = reader.ReadBytes(length);
				if (payload.Length != length) throw new EndOfStreamException("Truncated polity payload.");
				if (wire == LegacyWireVersion) return DecodePayloadV1(payload);
				if (wire == OldestWireVersion) return DecodePayloadV2(payload);
				if (wire == OlderWireVersion) return DecodePayloadV3(payload);
				if (wire == PriorWireVersion) return DecodePayloadV4(payload);
				if (wire == ImmediatePriorWireVersion) return DecodePayloadV5(payload);
				if (wire == CurrentWireVersion) return DecodePayloadV6(payload);
				if (wire <= 0) throw new InvalidDataException("Polity wire version is invalid.");
				return new KingdomPolityLedger
				{
					SchemaState = KingdomPolitySchemaState.Unknown,
					SchemaFault = "Unsupported bounded polity wire preserved as opaque evidence.",
					OpaqueWireVersion = wire,
					OpaqueFuturePayload = payload,
					Options = DisabledDefaultOptions()
				};
			}
		}

#if TAF_TESTS
		public static byte[] EncodeEnvelopeV1Fixture(KingdomPolityLedger Ledger)
		{
			if (!KingdomPolityRules.TryValidate(Ledger, out string failure))
				throw new InvalidDataException(failure);
			RequireNoResidentBridges(Ledger, "Wire-v1");
			if (Ledger.MigratedFromVersion > KingdomPolityRules.LegacyFormatVersion)
				throw new InvalidDataException("Wire-v1 fixture cannot carry later migration provenance.");
			if (Ledger.Projections.Count != 0 || Ledger.Compactions.Count != 0 ||
				Ledger.FoldedCompactionCount != 0L)
				throw new InvalidDataException("Wire-v1 fixture cannot carry v2-only rows.");
			return Frame(LegacyWireVersion, EncodePayloadV1(Ledger));
		}

		public static byte[] EncodeEnvelopeV2Fixture(KingdomPolityLedger Ledger)
		{
			if (!KingdomPolityRules.TryValidate(Ledger, out string failure))
				throw new InvalidDataException(failure);
			RequireNoResidentBridges(Ledger, "Wire-v2");
			RequireNoAbandonedCohorts(Ledger, "Wire-v2");
			return Frame(OldestWireVersion, EncodePayloadV2(Ledger));
		}

		public static byte[] EncodeEnvelopeV3Fixture(KingdomPolityLedger Ledger)
		{
			if (!KingdomPolityRules.TryValidate(Ledger, out string failure))
				throw new InvalidDataException(failure);
			RequireNoAbandonedCohorts(Ledger, "Wire-v3");
			return Frame(OlderWireVersion, EncodePayloadV3(Ledger));
		}

		public static byte[] EncodeEnvelopeV4Fixture(KingdomPolityLedger Ledger)
		{
			if (!KingdomPolityRules.TryValidate(Ledger, out string failure))
				throw new InvalidDataException(failure);
			RequireNoV5IncidentTransactions(Ledger, "Wire-v4");
			RequireNoAbandonedCohorts(Ledger, "Wire-v4");
			return Frame(PriorWireVersion, EncodePayloadV4(Ledger));
		}

		public static byte[] EncodeEnvelopeV5Fixture(KingdomPolityLedger Ledger)
		{
			if (!KingdomPolityRules.TryValidate(Ledger, out string failure))
				throw new InvalidDataException(failure);
			RequireNoAbandonedCohorts(Ledger, "Wire-v5");
			return Frame(ImmediatePriorWireVersion, EncodePayloadV5(Ledger));
		}

		private static void RequireNoAbandonedCohorts(KingdomPolityLedger Ledger, string Wire)
		{
			for (int i = 0; i < Ledger.Cohorts.Count; i++)
				if (Ledger.Cohorts[i].Phase == KingdomPolityCohortPhase.Abandoned)
					throw new InvalidDataException(Wire + " fixture cannot carry phase 6.");
		}

		private static void RequireNoV5IncidentTransactions(KingdomPolityLedger Ledger,
			string Wire)
		{
			for (int i = 0; i < Ledger.Grievances.Count; i++)
				if (Ledger.Grievances[i].Cause == KingdomPolityGrievanceCause.ResourceRefusal)
					throw new InvalidDataException(Wire +
						" fixture cannot carry post-v4 grievance values.");
			for (int i = 0; i < Ledger.Projections.Count; i++)
				if (Ledger.Projections[i].Kind == KingdomPolityProjectionKind.ConsentedEscrow)
					throw new InvalidDataException(Wire +
						" fixture cannot carry post-v4 projection values.");
			for (int i = 0; i < Ledger.Incidents.Count; i++)
				if (Ledger.Incidents[i].Hospitality != null ||
					Ledger.Incidents[i].Intervention != null ||
					Ledger.Incidents[i].Aftermath != null)
					throw new InvalidDataException(Wire +
						" fixture cannot carry v5 incident transactions.");
		}

		private static void RequireNoResidentBridges(KingdomPolityLedger Ledger, string Wire)
		{
			for (int i = 0; i < Ledger.NamedFigures.Count; i++)
				if (Ledger.NamedFigures[i].ResidentId != 0 ||
					!string.IsNullOrEmpty(Ledger.NamedFigures[i].ResidentSettlementId))
					throw new InvalidDataException(Wire + " fixture cannot carry v3 resident bridges.");
		}
#endif

		private static byte[] Frame(int Wire, byte[] Payload)
		{
			if (Payload == null || Payload.Length > MaxEnvelopeBytes - 12)
				throw new InvalidDataException("Polity payload exceeds hard bound.");
			using (MemoryStream stream = new MemoryStream(12 + Payload.Length))
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Magic); writer.Write(Wire); writer.Write(Payload.Length);
				writer.Write(Payload, 0, Payload.Length); return stream.ToArray();
			}
		}

		private static void RequireEncodable(KingdomPolityLedger L)
		{
			if (L.SchemaState == KingdomPolitySchemaState.Compatible)
			{
				if (!KingdomPolityRules.TryValidate(L, out string failure))
					throw new InvalidDataException("Invalid polity authority: " + failure);
				return;
			}
			if (L.SchemaState != KingdomPolitySchemaState.Quarantined ||
				!KingdomPolityRules.Text(L.SchemaFault, true) ||
				!BoundedTop(L)) throw new InvalidDataException("Polity quarantine shape is invalid.");
		}

		private static bool BoundedTop(KingdomPolityLedger L)
		{
			return L.Options != null && KingdomPolityRules.Count(L.Polities, KingdomPolityRules.MaxPolities) &&
				KingdomPolityRules.Count(L.Relations, KingdomPolityRules.MaxRelations) &&
				KingdomPolityRules.Count(L.Profiles, KingdomPolityRules.MaxProfiles) &&
				KingdomPolityRules.Count(L.Routes, KingdomPolityRules.MaxRoutes) &&
				KingdomPolityRules.Count(L.Grievances, KingdomPolityRules.MaxGrievances) &&
				KingdomPolityRules.Count(L.Fronts, KingdomPolityRules.MaxFronts) &&
				KingdomPolityRules.Count(L.Cohorts, KingdomPolityRules.MaxCohorts) &&
				KingdomPolityRules.Count(L.NamedFigures, KingdomPolityRules.MaxNamedFigures) &&
				KingdomPolityRules.Count(L.Incidents, KingdomPolityRules.MaxIncidents) &&
				KingdomPolityRules.Count(L.Projections, KingdomPolityRules.MaxProjections) &&
				KingdomPolityRules.Count(L.Compactions, KingdomPolityRules.MaxCompactions);
		}
	}
}
