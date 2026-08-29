namespace ThousandAndFirst
{
	/// <summary>Frozen central route/body child. Cargo type and economics remain parent-owned.</summary>
	public sealed class KingdomConstructionInputChild
	{
		public readonly int Ordinal;
		public readonly int JobId;
		public readonly int TripId;
		public readonly int CargoStart;
		public readonly int CargoCount;
		public readonly KingdomConstructionInputCargoShape CargoShape;
		public readonly int SourceEndpointId;
		public readonly string SourceObjectId;
		public readonly string SourceZoneId;
		public readonly int SourceX;
		public readonly int SourceY;
		public readonly int TargetEndpointId;
		public readonly string TargetObjectId;
		public readonly string TargetZoneId;
		public readonly int TargetX;
		public readonly int TargetY;
		public readonly long ArrivalTick;
		public readonly string RouteDigest;
		public readonly int CentralPhase;
		public readonly long CentralRevision;

		public KingdomConstructionInputChild(int Ordinal, int JobId, int TripId,
			int CargoStart, int CargoCount, KingdomConstructionInputCargoShape CargoShape,
			int SourceEndpointId, string SourceObjectId, string SourceZoneId,
			int SourceX, int SourceY, int TargetEndpointId, string TargetObjectId,
			string TargetZoneId, int TargetX, int TargetY, long ArrivalTick,
			string RouteDigest, int CentralPhase, long CentralRevision)
		{
			this.Ordinal = Ordinal;
			this.JobId = JobId;
			this.TripId = TripId;
			this.CargoStart = CargoStart;
			this.CargoCount = CargoCount;
			this.CargoShape = CargoShape;
			this.SourceEndpointId = SourceEndpointId;
			this.SourceObjectId = SourceObjectId;
			this.SourceZoneId = SourceZoneId;
			this.SourceX = SourceX;
			this.SourceY = SourceY;
			this.TargetEndpointId = TargetEndpointId;
			this.TargetObjectId = TargetObjectId;
			this.TargetZoneId = TargetZoneId;
			this.TargetX = TargetX;
			this.TargetY = TargetY;
			this.ArrivalTick = ArrivalTick;
			this.RouteDigest = RouteDigest;
			this.CentralPhase = CentralPhase;
			this.CentralRevision = CentralRevision;
		}

		internal KingdomConstructionInputChild WithCentral(int phase, long revision)
		{
			return new KingdomConstructionInputChild(Ordinal, JobId, TripId, CargoStart,
				CargoCount, CargoShape, SourceEndpointId, SourceObjectId, SourceZoneId,
				SourceX, SourceY, TargetEndpointId, TargetObjectId, TargetZoneId, TargetX,
				TargetY, ArrivalTick, RouteDigest, phase, revision);
		}
	}
}
