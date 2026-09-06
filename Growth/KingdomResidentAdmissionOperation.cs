using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	internal enum KingdomResidentAdmissionPhase : byte
	{
		None = 0,
		Prepared = 1,
		ReceiptPrepared = 2,
		CitizenshipApplied = 3,
		ResidentPublished = 4,
		FigureLinked = 5,
		CustodyReleased = 6,
		TransferIntent = 7,
		Transferred = 8,
		ReceiptCommitted = 9
	}

	[Serializable]
	public sealed class KingdomResidentAdmissionOperation
#if !TAF_TESTS
		: IComposite
#endif
	{
		public const int CurrentVersion = 1;
		public int Version;
		public int Phase;
		public long Revision;
		public string OperationId = "";
		public string HandoffId = "";
		public string RealmId = "";
		public string SourcePolityId = "";
		public string CohortId = "";
		public string MemberId = "";
		public string SettlementId = "";
		public string BodyObjectId = "";
		public string SourceZoneId = "";
		public string ProjectionId = "";
		public string BodyBlueprint = "";
		public string ProposedName = "";
		public string Origin = "";
		public string Creed = "";
		public string Arrived = "";
		public string LodgingProof = "";
		public string FigureId = "";
		public long PreparedTick;
		public int ResidentCounterBefore;
		public int ResidentId;
		public bool Rejected;
		public int RejectionReason;
		public string Fault = "";

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomResidentAdmissionOperation));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomResidentAdmissionOperation));
		}
#endif

		public KingdomResidentAdmissionOperation Copy()
		{
			return (KingdomResidentAdmissionOperation)MemberwiseClone();
		}
	}
}
