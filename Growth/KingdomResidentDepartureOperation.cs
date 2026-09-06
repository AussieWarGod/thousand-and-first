using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	internal enum KingdomResidentDeparturePhase : byte
	{
		None = 0,
		Prepared = 1,
		RolesPrepared = 2,
		CitizenshipRemoved = 3,
		CarriersRemoved = 4,
		RolesClosed = 5,
		EffectsPublished = 6
	}

	/// <summary>Realm-global write-ahead record for the one resident body whose destruction is
	/// in flight. Every identity and pre-role snapshot is frozen before the first role mutation.</summary>
	[Serializable]
	public sealed class KingdomResidentDepartureOperation
#if !TAF_TESTS
		: IComposite
#endif
	{
		public const int CurrentVersion = 1;
		public int Version;
		public int Phase;
		public long Revision;
		public string OperationId = "";
		public string RealmId = "";
		public string SettlementId = "";
		public int ResidentId;
		public string BodyObjectId = "";
		public string ZoneId = "";
		public string ResidentName = "";
		public string Origin = "";
		public long PreparedTick;
		public int DeparturesBefore;
		public bool Chronicled;
		public string ChronicleLine = "";
		public string LedgerLine = "";
		public string Cause = "";
		public KingdomNamedCookReceipt PriorCook;
		public KingdomCivicOfficeReceipt PriorOffice;
		public KingdomPolityNamedFigureRecord PriorPolity;
		public string PolityConclusionRef = "";
		public int AuthorizationKind;
		public string AuthorizationEventId = "";
		public string AuthorizationOwnerObjectId = "";
		public string AuthorizationCauseDigest = "";

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomResidentDepartureOperation));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomResidentDepartureOperation));
		}
#endif

		public KingdomResidentDepartureOperation Copy()
		{
			return new KingdomResidentDepartureOperation
			{
				Version = Version, Phase = Phase, Revision = Revision,
				OperationId = OperationId, RealmId = RealmId,
				SettlementId = SettlementId, ResidentId = ResidentId,
				BodyObjectId = BodyObjectId, ZoneId = ZoneId,
				ResidentName = ResidentName, Origin = Origin,
				PreparedTick = PreparedTick, DeparturesBefore = DeparturesBefore,
				Chronicled = Chronicled, ChronicleLine = ChronicleLine,
				LedgerLine = LedgerLine, Cause = Cause,
				PriorCook = PriorCook?.Copy(),
				PriorOffice = PriorOffice == null ? null
					: KingdomExperienceRules.CopyOffice(PriorOffice),
				PriorPolity = KingdomPolityRules.CopyResidentTransitionFigure(PriorPolity),
				PolityConclusionRef = PolityConclusionRef,
				AuthorizationKind = AuthorizationKind,
				AuthorizationEventId = AuthorizationEventId,
				AuthorizationOwnerObjectId = AuthorizationOwnerObjectId,
				AuthorizationCauseDigest = AuthorizationCauseDigest
			};
		}
	}
}
