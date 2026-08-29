namespace ThousandAndFirst
{
	/// <summary>One exact source leg. Immutable plan facts never change after publication.</summary>
	public sealed class KingdomConstructionInputSourceLine
	{
		public readonly int Ordinal;
		public readonly string LineId;
		public readonly KingdomConstructionInputKind Kind;
		public readonly string Classification;
		public readonly string SourceSettlementId;
		public readonly string SourceZoneId;
		public readonly string HolderId;
		public readonly string SourceObjectId;
		public readonly KingdomConstructionInputTopology Topology;
		public readonly int X;
		public readonly int Y;
		public readonly string Blueprint;
		public readonly int Before;
		public readonly int Take;
		public readonly int ResidualAfter;
		public readonly int HolderStockBefore;
		public readonly int PriorReserved;
		public readonly int ReserveFloor;
		public readonly int CargoOrdinal;
		public readonly int RouteCost;
		public readonly int DedicationOrdinal;
		public readonly string RemainderMarker;

		public readonly KingdomConstructionInputSourcePhase Phase;
		public readonly string RemainderObjectId;
		public readonly string BeforeWitnessHash;
		public readonly string AfterWitnessHash;
		public readonly int ProvedLost;

		public KingdomConstructionInputSourceLine(int Ordinal, string LineId,
			KingdomConstructionInputKind Kind, string Classification,
			string SourceSettlementId, string SourceZoneId, string HolderId,
			string SourceObjectId, KingdomConstructionInputTopology Topology,
			int X, int Y, string Blueprint, int Before, int Take, int ResidualAfter,
			int HolderStockBefore, int PriorReserved, int ReserveFloor, int CargoOrdinal,
			int RouteCost, int DedicationOrdinal, string RemainderMarker,
			KingdomConstructionInputSourcePhase Phase, string RemainderObjectId,
			string BeforeWitnessHash, string AfterWitnessHash, int ProvedLost)
		{
			this.Ordinal = Ordinal;
			this.LineId = LineId;
			this.Kind = Kind;
			this.Classification = Classification;
			this.SourceSettlementId = SourceSettlementId;
			this.SourceZoneId = SourceZoneId;
			this.HolderId = HolderId;
			this.SourceObjectId = SourceObjectId;
			this.Topology = Topology;
			this.X = X;
			this.Y = Y;
			this.Blueprint = Blueprint;
			this.Before = Before;
			this.Take = Take;
			this.ResidualAfter = ResidualAfter;
			this.HolderStockBefore = HolderStockBefore;
			this.PriorReserved = PriorReserved;
			this.ReserveFloor = ReserveFloor;
			this.CargoOrdinal = CargoOrdinal;
			this.RouteCost = RouteCost;
			this.DedicationOrdinal = DedicationOrdinal;
			this.RemainderMarker = RemainderMarker;
			this.Phase = Phase;
			this.RemainderObjectId = RemainderObjectId;
			this.BeforeWitnessHash = BeforeWitnessHash;
			this.AfterWitnessHash = AfterWitnessHash;
			this.ProvedLost = ProvedLost;
		}

		internal KingdomConstructionInputSourceLine WithPhase(
			KingdomConstructionInputSourcePhase phase)
		{
			return Copy(phase, RemainderObjectId, BeforeWitnessHash, AfterWitnessHash, ProvedLost);
		}

		internal KingdomConstructionInputSourceLine WithEvidence(string remainderObjectId,
			string beforeWitnessHash, string afterWitnessHash, int provedLost)
		{
			return Copy(Phase, remainderObjectId, beforeWitnessHash, afterWitnessHash, provedLost);
		}

		private KingdomConstructionInputSourceLine Copy(KingdomConstructionInputSourcePhase phase,
			string remainderObjectId, string beforeWitnessHash, string afterWitnessHash, int provedLost)
		{
			return new KingdomConstructionInputSourceLine(Ordinal, LineId, Kind, Classification,
				SourceSettlementId, SourceZoneId, HolderId, SourceObjectId, Topology, X, Y,
				Blueprint, Before, Take, ResidualAfter, HolderStockBefore, PriorReserved,
				ReserveFloor, CargoOrdinal, RouteCost, DedicationOrdinal, RemainderMarker,
				phase, remainderObjectId, beforeWitnessHash, afterWitnessHash, provedLost);
		}
	}
}
