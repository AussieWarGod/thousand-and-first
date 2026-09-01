using XRL.World;
using XRL.World.Parts;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	/// <summary>Ordered system-owned mutation gate for paid hosted-floor realization.</summary>
	public static partial class KingdomHostedArcology
	{
		internal static bool ReconcileActiveInterior(Zone Z, out string Failure)
		{
			Failure = null;
			InteriorZone interior = Z as InteriorZone;
			string lot = interior == null ? "" : KingdomHostedArcologyTopology.HostedLotAt(
				interior.X, interior.Y, interior.Z);
			if (string.IsNullOrEmpty(lot)) return true;
			if (!TryLoadedInteriorRoot(interior, out GameObject shell, out Failure)) return false;
			if (!IsOperationalPure(shell))
				return ContextFail("hosted floor lacks current operational authority", out Failure);
			r_KingdomArcology root = shell.GetPart<r_KingdomArcology>();
			if (!TryReceipt(root, lot, out KingdomHostedLotReceipt receipt, out Failure)) return false;
			if (receipt == null || receipt.Phase == KingdomHostedLotPhase.Working) return true;
			if (receipt.Phase != KingdomHostedLotPhase.Active)
				return ContextFail("hosted floor receipt is not active", out Failure);
			if (!TryLiveContext(Z, false, out KingdomHostedLiveContext context, out Failure))
				return false;
			return KingdomHostedArcologyVisual.Reconcile(Z, context.Anchor)
				|| ContextFail(root.QuarantineReason
					?? "hosted fixture realization failed", out Failure);
		}
	}
}
