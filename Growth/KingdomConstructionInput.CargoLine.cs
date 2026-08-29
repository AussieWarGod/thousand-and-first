namespace ThousandAndFirst
{
	/// <summary>One exact physical freight object; generated identity is adopted, never predicted.</summary>
	public sealed class KingdomConstructionInputCargoLine
	{
		public readonly int Ordinal;
		public readonly string CargoKey;
		public readonly string CreationMarker;
		public readonly KingdomConstructionInputKind Kind;
		public readonly string Classification;
		public readonly int Amount;
		public readonly string Blueprint;
		public readonly int Capacity;
		public readonly int SourceLineOrdinal;
		public readonly string ExpectedObjectId;
		public readonly int ChildJobId;
		public readonly int ChildTripId;

		public readonly string ObjectId;
		public readonly KingdomConstructionInputCargoPhase Phase;
		public readonly KingdomConstructionInputTopology CustodyTopology;
		public readonly string CustodyOwnerId;
		public readonly string CustodyZoneId;
		public readonly int CustodyX;
		public readonly int CustodyY;
		public readonly string BeforeWitnessHash;
		public readonly string AfterWitnessHash;
		public readonly int Spent;
		public readonly int Lost;

		public KingdomConstructionInputCargoLine(int Ordinal, string CargoKey,
			string CreationMarker, KingdomConstructionInputKind Kind, string Classification,
			int Amount, string Blueprint, int Capacity, int SourceLineOrdinal,
			string ExpectedObjectId, int ChildJobId, int ChildTripId, string ObjectId,
			KingdomConstructionInputCargoPhase Phase,
			KingdomConstructionInputTopology CustodyTopology, string CustodyOwnerId,
			string CustodyZoneId, int CustodyX, int CustodyY, string BeforeWitnessHash,
			string AfterWitnessHash, int Spent, int Lost)
		{
			this.Ordinal = Ordinal;
			this.CargoKey = CargoKey;
			this.CreationMarker = CreationMarker;
			this.Kind = Kind;
			this.Classification = Classification;
			this.Amount = Amount;
			this.Blueprint = Blueprint;
			this.Capacity = Capacity;
			this.SourceLineOrdinal = SourceLineOrdinal;
			this.ExpectedObjectId = ExpectedObjectId;
			this.ChildJobId = ChildJobId;
			this.ChildTripId = ChildTripId;
			this.ObjectId = ObjectId;
			this.Phase = Phase;
			this.CustodyTopology = CustodyTopology;
			this.CustodyOwnerId = CustodyOwnerId;
			this.CustodyZoneId = CustodyZoneId;
			this.CustodyX = CustodyX;
			this.CustodyY = CustodyY;
			this.BeforeWitnessHash = BeforeWitnessHash;
			this.AfterWitnessHash = AfterWitnessHash;
			this.Spent = Spent;
			this.Lost = Lost;
		}

		internal KingdomConstructionInputCargoLine WithPhase(
			KingdomConstructionInputCargoPhase phase)
		{
			return Copy(ObjectId, phase, CustodyTopology, CustodyOwnerId, CustodyZoneId,
				CustodyX, CustodyY, BeforeWitnessHash, AfterWitnessHash, Spent, Lost);
		}

		internal KingdomConstructionInputCargoLine WithEvidence(string objectId,
			KingdomConstructionInputTopology topology, string ownerId, string zoneId,
			int x, int y, string beforeWitnessHash, string afterWitnessHash, int spent, int lost)
		{
			return Copy(objectId, Phase, topology, ownerId, zoneId, x, y,
				beforeWitnessHash, afterWitnessHash, spent, lost);
		}

		private KingdomConstructionInputCargoLine Copy(string objectId,
			KingdomConstructionInputCargoPhase phase, KingdomConstructionInputTopology topology,
			string ownerId, string zoneId, int x, int y, string beforeWitnessHash,
			string afterWitnessHash, int spent, int lost)
		{
			return new KingdomConstructionInputCargoLine(Ordinal, CargoKey, CreationMarker,
				Kind, Classification, Amount, Blueprint, Capacity, SourceLineOrdinal,
				ExpectedObjectId, ChildJobId, ChildTripId, objectId, phase, topology, ownerId,
				zoneId, x, y, beforeWitnessHash, afterWitnessHash, spent, lost);
		}
	}
}
