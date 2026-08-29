using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryReconcilePortfolioTopology(
			ref KingdomPurposePairReceipt Pair, out string Failure)
		{
			Failure = null;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null)
				return Fail("No live realm can reprove the purpose-pair topology.", out Failure);
			if (!system.TryExactSettlementIds(RequirePublishedClaims: true,
				out List<string> active, out string identityFailure))
				return Fail("Purpose-pair topology could not be reproved: "
					+ identityFailure, out Failure);
			if (!KingdomPurposePortfolioRules.TryReconcileTopology(Pair, active,
				out KingdomPurposePairReceipt reconciled, out KingdomPurposePairFault fault))
				return Fail("Purpose-pair topology reconciliation refused (" + fault + ").",
					out Failure);
			if (reconciled.Revision == Pair.Revision) return true;
			if (!TryPublishPortfolioPair(Pair, reconciled, out Failure)) return false;
			Pair = reconciled;
			return true;
		}
	}
}
