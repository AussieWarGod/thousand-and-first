using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free bounds and ordering for the live seat plus non-seat roster.</summary>
	public static class KingdomSettlementTopologyRules
	{
		public const int MaxOwnedSettlements = 3;
		public const int MaxNonSeatSettlements = MaxOwnedSettlements - 1;

		/// <summary>Proves one seat plus a bounded, distinct non-seat identity set and returns
		/// the non-seat ids in the sole persisted order.</summary>
		public static bool TryCanonicalize(string SeatId, IList<string> NonSeatIds,
			out List<string> Canonical, out string Failure)
		{
			Canonical = new List<string>();
			Failure = null;
			if (!KingdomIdentityRules.IsSettlementId(SeatId) || NonSeatIds == null ||
				NonSeatIds.Count > MaxNonSeatSettlements)
			{
				Failure = "Settlement topology has no exact seat or exceeds its bound.";
				return false;
			}
			for (int i = 0; i < NonSeatIds.Count; i++)
			{
				string id = NonSeatIds[i];
				if (!KingdomIdentityRules.IsSettlementId(id) ||
					string.Equals(id, SeatId, StringComparison.Ordinal) || Canonical.Contains(id))
				{
					Failure = "Settlement topology contains an invalid or duplicate identity.";
					Canonical.Clear();
					return false;
				}
				Canonical.Add(id);
			}
			Canonical.Sort(StringComparer.Ordinal);
			return true;
		}

		/// <summary>Returns the unique row claiming a zone, or -1 when none or ambiguous.</summary>
		public static int UniqueClaimOwner(IList<bool> Claims)
		{
			if (Claims == null || Claims.Count > MaxOwnedSettlements) return -1;
			int owner = -1;
			for (int i = 0; i < Claims.Count; i++)
			{
				if (!Claims[i]) continue;
				if (owner >= 0) return -1;
				owner = i;
			}
			return owner;
		}
	}
}
