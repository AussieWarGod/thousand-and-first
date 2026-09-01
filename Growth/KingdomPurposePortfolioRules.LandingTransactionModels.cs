namespace ThousandAndFirst
{
	/// <summary>Executable shape of the landing fields carried by one exact cargo. This is not a
	/// second object reader: runtime classifies the physical fields, then this pure seam composes
	/// that classification with cleanup and publication state.</summary>
	internal enum KingdomPurposeLandingCargoRecordShape : byte
	{
		Invalid = 0,
		CleanLegacy = 1,
		WholeCurrent = 2,
		PartialCurrent = 3,
		TornOrForeign = 4
	}

	/// <summary>Completeness of one fresh recursive loaded-custody walk.</summary>
	internal enum KingdomPurposeLandingCustodyProof : byte
	{
		Invalid = 0,
		Complete = 1,
		NullZoneRootIndex = 2,
		NullNestedInventoryIndex = 3,
		InvalidListedObject = 4
	}

	/// <summary>Inverse membership of the frozen destination store in its exact cell rack.</summary>
	internal enum KingdomPurposeLandingStoreRackProof : byte
	{
		Invalid = 0,
		Exact = 1,
		MissingFromCellList = 2
	}

	/// <summary>Farthest physical cleanup completed before semantic pair publication. Values are
	/// ordered because each step conserves all evidence needed by the next one.</summary>
	internal enum KingdomPurposeLandingCleanupStep : byte
	{
		None = 0,
		Prevalidated = 1,
		AttemptRetired = 2,
		MarksRetired = 3,
		CargoRecordRetired = 4,
		RootRetired = 5,
		PairPublished = 6
	}

	/// <summary>One pure drive result. A refused semantic CAS is recoverable only through evidence
	/// already represented in the returned state; it never rewinds physical cleanup or a fault.</summary>
	internal enum KingdomPurposeLandingTransactionVerdict : byte
	{
		Refused = 0,
		SemanticCasRefused = 1,
		PairPublished = 2,
		Quarantined = 3,
		EntryProved = 4,
		OperationAdmitted = 5
	}

	/// <summary>Small executable composition of physical proof and semantic receipt state. No Qud
	/// object, callback, identifier, or mutation authority crosses this boundary.</summary>
	internal struct KingdomPurposeLandingTransactionState
	{
		public KingdomPurposePairPhase PairPhase;
		public KingdomPurposePairPhase ResumePhase;
		public KingdomPurposeOperationPhase OperationPhase;
		public KingdomPurposeLandingCargoRecordShape CargoRecord;
		public KingdomPurposeLandingAttemptState Attempt;
		public KingdomPurposeLandingCustodyProof EntryCustody;
		public KingdomPurposeLandingCustodyProof Custody;
		public KingdomPurposeLandingStoreRackProof StoreRack;
		public KingdomPurposeLandingCleanupStep Cleanup;
		public int PairRevision;
		public int OperationRevision;
		public int NextOperationOrdinal;
		public int Carried;
		public int ExactServingMarks;
		public bool RootPresent;
		public bool MeasuredRosterExact;
		public bool MalformedCoResidentEvidence;
		public bool FaultPresent;

		/// <summary>Builds one complete observed state. Every physical and semantic fact is explicit;
		/// no proof silently inherits a value-type default because a caller omitted it.</summary>
		public KingdomPurposeLandingTransactionState(
			KingdomPurposePairPhase PairPhase, KingdomPurposePairPhase ResumePhase,
			KingdomPurposeOperationPhase OperationPhase,
			KingdomPurposeLandingCargoRecordShape CargoRecord,
			KingdomPurposeLandingAttemptState Attempt,
			KingdomPurposeLandingCustodyProof EntryCustody,
			KingdomPurposeLandingCustodyProof Custody,
			KingdomPurposeLandingStoreRackProof StoreRack,
			KingdomPurposeLandingCleanupStep Cleanup, int PairRevision,
			int OperationRevision, int NextOperationOrdinal, int Carried,
			int ExactServingMarks, bool RootPresent, bool MeasuredRosterExact,
			bool MalformedCoResidentEvidence, bool FaultPresent)
		{
			this.PairPhase = PairPhase;
			this.ResumePhase = ResumePhase;
			this.OperationPhase = OperationPhase;
			this.CargoRecord = CargoRecord;
			this.Attempt = Attempt;
			this.EntryCustody = EntryCustody;
			this.Custody = Custody;
			this.StoreRack = StoreRack;
			this.Cleanup = Cleanup;
			this.PairRevision = PairRevision;
			this.OperationRevision = OperationRevision;
			this.NextOperationOrdinal = NextOperationOrdinal;
			this.Carried = Carried;
			this.ExactServingMarks = ExactServingMarks;
			this.RootPresent = RootPresent;
			this.MeasuredRosterExact = MeasuredRosterExact;
			this.MalformedCoResidentEvidence = MalformedCoResidentEvidence;
			this.FaultPresent = FaultPresent;
		}
	}
}
