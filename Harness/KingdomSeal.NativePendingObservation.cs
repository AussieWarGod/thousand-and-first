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

		internal bool NativeSpatialCapturePreflight(out string Result, out string Failure)
		{
			string before = NativePendingStageEvidence();
			bool captured = TryCapture(The.Game.GetSystem<KingdomSystem>(), LegacyId, Generation,
				Revision, The.Game.TimeTicks, out var record, out Failure, out var spatial);
			Result = "captured=" + captured + "; spatial=" + spatial + "; reason=" + Failure;
			if (NativePendingStageEvidence() != before)
			{
				Failure = "capture-only observation changed the staged record"; return false;
			}
			return captured && record != null && spatial == KingdomInheritanceSpatialCaptureResult.Captured
				|| !captured && record == null && spatial == KingdomInheritanceSpatialCaptureResult.Pending
					&& Failure == "the public entrance has no witnessed street connection to the zone edge yet";
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
