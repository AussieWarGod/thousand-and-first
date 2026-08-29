using System;
using System.Collections.Generic;
using System.IO;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomExperienceOptionReceipt
	{
		public KingdomExperienceOptionKind Kind;
		public KingdomExperienceOptionState State;
		public long ObservedTick;
		public long EnableEpoch;
		public long FutureCauseFloorTick = long.MaxValue;
	}

	[Serializable]
	public sealed class KingdomExperienceAudienceReceipt
	{
		public string ReservationId;
		public string RealmId;
		public string SettlementId;
		public string SourceId;
		public KingdomExperienceLane Lane;
		public KingdomExperienceOptionKind OptionKind;
		public long CauseTick;
		public long ReservedTick;
		public long EnableEpoch;
	}

	[Serializable]
	public sealed class KingdomExperienceBodyReservation
	{
		public string ReservationId;
		public string RealmId;
		public string SettlementId;
		public string SourceId;
		public KingdomExperienceLane Lane;
		public KingdomExperienceOptionKind OptionKind;
		public long CauseTick;
		public long ReservedTick;
		public long EnableEpoch;
		public int BodyCount;
	}

	/// <summary>
	/// Realm-scoped experience authority. Capacity rows remain source-owned leases; bounded civic
	/// rows own only their exact office/remembrance decisions and projection receipts. The explicit
	/// composite codec rejects oversize input before a CLR collection can be populated.
	/// </summary>
	[Serializable]
	public sealed partial class KingdomExperienceLedger
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int FormatVersion = KingdomExperienceRules.CurrentFormatVersion;
		public KingdomExperienceSchemaState SchemaState = KingdomExperienceSchemaState.Compatible;
		public string SchemaFault;
		public int MigratedFromVersion;
		public string RealmId;
		public bool IdentityBound;
		public long Revision;
		public KingdomExperienceOptionReceipt Story =
			KingdomExperienceRules.UnobservedOption(KingdomExperienceOptionKind.CivicStory);
		public KingdomExperienceOptionReceipt Knowledge =
			KingdomExperienceRules.UnobservedOption(KingdomExperienceOptionKind.CivicKnowledge);
		public KingdomExperienceOptionReceipt Ambient =
			KingdomExperienceRules.UnobservedOption(KingdomExperienceOptionKind.AmbientUse);
		public List<KingdomExperienceAudienceReceipt> Audiences =
			new List<KingdomExperienceAudienceReceipt>();
		public List<KingdomExperienceBodyReservation> BodyReservations =
			new List<KingdomExperienceBodyReservation>();
		public List<KingdomCivicOfficeReceipt> Offices =
			new List<KingdomCivicOfficeReceipt>();
		public List<KingdomRemembranceReceipt> Remembrances =
			new List<KingdomRemembranceReceipt>();
		public List<KingdomCivicVoiceReceipt> Voices =
			new List<KingdomCivicVoiceReceipt>();
		public int OpaqueWireVersion;
		public byte[] OpaqueFuturePayload;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			byte[] envelope = KingdomExperienceCodec.EncodeEnvelope(this);
			Writer.Write(envelope.Length);
			Writer.Write(envelope, 0, envelope.Length);
		}

		public void Read(SerializationReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length < 0 || length > KingdomExperienceCodec.MaxEnvelopeBytes)
				throw new InvalidDataException("Experience envelope length exceeds hard bound.");
			byte[] envelope = Reader.ReadBytesDirect(length);
			if (envelope.Length != length)
				throw new EndOfStreamException("Truncated experience envelope.");
			CopyFrom(KingdomExperienceCodec.DecodeEnvelopeRaw(envelope));
		}
#endif

		internal void CopyFrom(KingdomExperienceLedger Source)
		{
			if (Source == null) throw new ArgumentNullException(nameof(Source));
			FormatVersion = Source.FormatVersion; SchemaState = Source.SchemaState;
			SchemaFault = Source.SchemaFault; MigratedFromVersion = Source.MigratedFromVersion;
			RealmId = Source.RealmId; IdentityBound = Source.IdentityBound;
			Revision = Source.Revision; Story = Source.Story; Knowledge = Source.Knowledge;
			Ambient = Source.Ambient; Audiences = Source.Audiences;
			BodyReservations = Source.BodyReservations;
			Offices = Source.Offices; Remembrances = Source.Remembrances;
			Voices = Source.Voices; FirstFeasts = Source.FirstFeasts;
			OpaqueWireVersion = Source.OpaqueWireVersion;
			OpaqueFuturePayload = Source.OpaqueFuturePayload;
		}
	}

	public static partial class KingdomExperienceRules { }
	public static partial class KingdomExperienceCodec { }
}
