namespace ThousandAndFirst
{
	/// <summary>Exact parent construction facts authenticated before routed planning.</summary>
	public sealed class KingdomConstructionInputIntent
	{
		public readonly string ConstructionJobId;
		public readonly string OwnerKey;
		public readonly string ZoneId;
		public readonly int Route;
		public readonly int Projection;
		public readonly int X;
		public readonly int Y;
		public readonly string SubjectId;
		public readonly string SourceId;
		public readonly string TargetKey;
		public readonly string PayloadDigest;
		public readonly string BuildTruthDigest;
		public readonly int WaterRequested;
		public readonly string MaterialRequestedClaim;
		public readonly long CreatedTick;
		public readonly long StartedTick;
		public readonly long DueTick;

		public KingdomConstructionInputIntent(string ConstructionJobId, string OwnerKey,
			string ZoneId, int Route, int Projection, int X, int Y, string SubjectId,
			string SourceId, string TargetKey, string PayloadDigest, string BuildTruthDigest,
			int WaterRequested, string MaterialRequestedClaim, long CreatedTick,
			long StartedTick, long DueTick)
		{
			this.ConstructionJobId = ConstructionJobId;
			this.OwnerKey = OwnerKey;
			this.ZoneId = ZoneId;
			this.Route = Route;
			this.Projection = Projection;
			this.X = X;
			this.Y = Y;
			this.SubjectId = SubjectId;
			this.SourceId = SourceId;
			this.TargetKey = TargetKey;
			this.PayloadDigest = PayloadDigest;
			this.BuildTruthDigest = BuildTruthDigest;
			this.WaterRequested = WaterRequested;
			this.MaterialRequestedClaim = MaterialRequestedClaim;
			this.CreatedTick = CreatedTick;
			this.StartedTick = StartedTick;
			this.DueTick = DueTick;
		}
	}
}
