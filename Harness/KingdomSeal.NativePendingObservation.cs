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

		/// <summary>#283: one capture-only reading of the live world, journaled as evidence:
		/// whether it captured, its spatial result and reason, and whether the settlement pass
		/// would call it a fault. False only when the observation itself changed the staged
		/// record.</summary>
		internal bool NativeSpatialCaptureReading(out bool Captured,
			out KingdomInheritanceSpatialCaptureResult Spatial, out string Reason)
		{
			string before = NativePendingStageEvidence();
			Captured = TryCapture(The.Game.GetSystem<KingdomSystem>(), LegacyId, Generation,
				Revision, The.Game.TimeTicks, out var record, out Reason, out Spatial);
			return NativePendingStageEvidence() == before;
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
