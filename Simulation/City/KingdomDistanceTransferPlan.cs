namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One exact nearest-holder decision handed to the engine edge.</summary>
	internal readonly struct KingdomDistanceTransferPlan
	{
		internal readonly int SourceZoneIndex;

		internal readonly int HolderId;

		internal readonly string HolderObjectId;

		internal readonly int TargetId;

		internal readonly string TargetObjectId;

		internal readonly short SourceX;

		internal readonly short SourceY;

		internal readonly short TargetX;

		internal readonly short TargetY;

		internal readonly int Cells;

		internal readonly long Amount;

		internal KingdomDistanceTransferPlan(int sourceZoneIndex, int holderId,
			string holderObjectId, int targetId, string targetObjectId,
			short sourceX, short sourceY, short targetX, short targetY,
			int cells, long amount)
		{
			SourceZoneIndex = sourceZoneIndex;
			HolderId = holderId;
			HolderObjectId = holderObjectId;
			TargetId = targetId;
			TargetObjectId = targetObjectId;
			SourceX = sourceX;
			SourceY = sourceY;
			TargetX = targetX;
			TargetY = targetY;
			Cells = cells;
			Amount = amount;
		}
	}
}
