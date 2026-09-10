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
			bool staged = TryFlushLiving("native roadless witness", true, out Failure, out var spatial);
			return !staged && spatial == KingdomInheritanceSpatialCaptureResult.Pending;
		}
	}
}
