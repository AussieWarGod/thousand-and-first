using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocationCodec
	{
		private static void Write(BinaryWriter W, KingdomRelocationReceipt R)
		{
			W.Write(Magic); W.Write(R.Schema); WriteText(W, R.PlanId);
			WriteText(W, R.ZoneId); WriteText(W, R.RealmId); WriteText(W, R.HeartId);
			WriteText(W, R.SuccessorKey); WriteRect(W, R.HeartGround);
			W.Write(R.CreatedTick); W.Write(R.Generation); W.Write(R.CurrentMove);
			W.Write(R.Held); W.Write(R.ObstructionAnnounced);
			W.Write((byte)R.Phase); WriteOptional(W, R.Failure); W.Write(R.Moves.Count);
			for (int i = 0; i < R.Moves.Count; i++) WriteMove(W, R.Moves[i]);
		}

		private static void WriteMove(BinaryWriter W, KingdomRelocationMove M)
		{
			WriteText(W, M.RootId); WriteText(W, M.PlotId); WriteText(W, M.BuildKey);
			WriteOptional(W, M.DisplayName); WriteRect(W, M.Source);
			WriteRect(W, M.Destination); WriteRect(W, M.Footprint); W.Write(M.Roof);
			W.Write(M.StartedTick); W.Write(M.LastTick); W.Write(M.RequiredTicks);
			W.Write(M.RemainingTicks); W.Write(M.CompletionTick); W.Write((byte)M.Phase);
			WriteText(W, M.FrameId); W.Write(M.StakeIds.Length);
			for (int i = 0; i < M.StakeIds.Length; i++) WriteText(W, M.StakeIds[i]);
			W.Write(M.Architecture != null);
			if (M.Architecture != null) WriteArchitecture(W, M.Architecture);
			W.Write(M.Rows.Count);
			for (int i = 0; i < M.Rows.Count; i++) WriteRow(W, M.Rows[i]);
			W.Write(M.Clearance.Count);
			for (int i = 0; i < M.Clearance.Count; i++) WriteClear(W, M.Clearance[i]);
		}

		private static void WriteArchitecture(BinaryWriter W,
			KingdomRelocationArchitecture A)
		{
			W.Write(A.Schema); WriteText(W, A.BuildKey); WriteText(W, A.PlanKey);
			WriteText(W, A.BindingKey); WriteText(W, A.TierKey);
			WriteText(W, A.VariantKey); WriteText(W, A.PaletteKey);
			WriteText(W, A.LotType); W.Write(A.LotSize); W.Write(A.Facing);
			WriteText(W, A.Snapshot); WriteText(W, A.Hash); W.Write(A.MainX); W.Write(A.MainY);
		}

		private static void WriteRow(BinaryWriter W, KingdomRelocationRow R)
		{
			WriteText(W, R.ObjectId); WriteText(W, R.Blueprint);
			W.Write(R.OffsetX); W.Write(R.OffsetY); W.Write(R.Root); W.Write((byte)R.State);
		}

		private static void WriteClear(BinaryWriter W, KingdomRelocationClearRow R)
		{
			WriteText(W, R.ObjectId); WriteText(W, R.Blueprint);
			W.Write(R.X); W.Write(R.Y); W.Write((byte)R.State);
		}
	}
}
