using XRL;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSeal
	{
		internal string NativePendingStageEvidence()
		{
			KingdomSealRecord record = GetStore().ReadStage(OriginGameId);
			return Revision + ":" + (record == null ? "absent" : record.Compose());
		}

		internal bool NativeSpatialCaptureWaits(out string Failure)
		{
			string before = NativePendingStageEvidence();
			bool captured = TryCapture(The.Game.GetSystem<KingdomSystem>(), LegacyId, Generation,
				Revision, The.Game.TimeTicks, out var record, out Failure, out var spatial);
			if (NativePendingStageEvidence() != before)
			{
				Failure = "capture-only observation changed the staged record"; return false;
			}
			if (!captured && record == null && spatial == KingdomInheritanceSpatialCaptureResult.Pending)
				return true;
			Failure = "captured=" + captured + "; spatial=" + spatial + "; reason=" + Failure;
			return false;
		}
	}
}
