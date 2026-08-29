using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Pure selection of one loaded owned polity endpoint from bounded city claims.</summary>
	internal static class KingdomPolityLoadedEndpointRules
	{
		internal static bool TryResolve(string ZoneId, string SeatSettlementId,
			IList<string> SeatClaims, IList<string> NonSeatSettlementIds,
			IList<IList<string>> NonSeatClaims, out string SettlementId,
			out bool Owned, out string Failure)
		{
			SettlementId = null; Owned = false; Failure = null;
			if (string.IsNullOrEmpty(ZoneId) || SeatClaims == null ||
				NonSeatSettlementIds == null || NonSeatClaims == null ||
				NonSeatSettlementIds.Count != NonSeatClaims.Count ||
				!KingdomSettlementTopologyRules.TryCanonicalize(SeatSettlementId,
					NonSeatSettlementIds, out List<string> _, out Failure))
			{
				Failure = Failure ?? "loaded polity endpoint topology is incomplete";
				return false;
			}
			List<string> ids = new List<string> { SeatSettlementId };
			List<bool> claims = new List<bool> { SeatClaims.Contains(ZoneId) };
			for (int i = 0; i < NonSeatSettlementIds.Count; i++)
			{
				if (NonSeatClaims[i] == null)
				{
					Failure = "loaded polity endpoint claim set is absent"; return false;
				}
				ids.Add(NonSeatSettlementIds[i]);
				claims.Add(NonSeatClaims[i].Contains(ZoneId));
			}
			int count = 0;
			for (int i = 0; i < claims.Count; i++) if (claims[i]) count++;
			if (count == 0) return true;
			if (count != 1)
			{
				Failure = "loaded polity endpoint claim is ambiguous"; return false;
			}
			int owner = KingdomSettlementTopologyRules.UniqueClaimOwner(claims);
			if (owner < 0 || owner >= ids.Count)
			{
				Failure = "loaded polity endpoint has no unique owner"; return false;
			}
			SettlementId = ids[owner]; Owned = true; return true;
		}
	}
}
