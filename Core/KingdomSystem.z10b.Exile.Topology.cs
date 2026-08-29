using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private static bool ExactTopologyMirror(KingdomSettlementTopology Expected,
			KingdomSettlementTopology Current, out string Failure)
		{
			Failure = null;
			if (Expected == null || Current == null || ReferenceEquals(Expected, Current) ||
				Expected.HasOpaqueEvidence || Current.HasOpaqueEvidence ||
				Expected.Count != Current.Count) return false;
			for (int i = 0; i < Expected.Count; i++)
			{
				KingdomSettlement expected = Expected.Get(i);
				KingdomSettlement current = Current.FindById(expected?.City?.SettlementId);
				if (current == null || !KingdomArchivedSettlementCodec.ExactGraph(
					expected, current, out Failure)) return false;
			}
			return true;
		}

		private static bool ExactArchivedSettlements(string RealmId,
			KingdomSettlement Seat, KingdomSettlementTopology Topology,
			IList<string> ExpectedIds = null)
		{
			List<string> ids = new List<string>();
			if (!ArchivedSettlementMatches(RealmId, Seat, out string seatId)) return false;
			ids.Add(seatId);
			if (Topology == null || Topology.HasOpaqueEvidence) return false;
			for (int i = 0; i < Topology.Count; i++)
			{
				if (!ArchivedSettlementMatches(RealmId, Topology.Get(i), out string otherId) ||
					ids.Contains(otherId)) return false;
				ids.Add(otherId);
			}
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, ids,
				out KingdomIdentityFault _)) return false;
			ids.Sort(StringComparer.Ordinal);
			if (ExpectedIds == null || ids.Count != ExpectedIds.Count)
				return ExpectedIds == null;
			for (int i = 0; i < ids.Count; i++)
				if (!string.Equals(ids[i], ExpectedIds[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ArchivedSettlementMatches(string RealmId,
			KingdomSettlement Settlement, out string SettlementId)
		{
			SettlementId = Settlement?.City?.SettlementId;
			return Settlement != null && Settlement.ClaimedZones != null &&
				Settlement.ClaimedZones.Contains(Settlement.SettlementIdentityFirstClaimedZone) &&
				KingdomIdentityRules.ReproveSettlement(SettlementId, RealmId,
					Settlement.SettlementIdentityVersion, Settlement.SettlementIdentityOrigin,
					Settlement.SettlementIdentityTransactionId,
					Settlement.SettlementIdentityFoundedTick,
					Settlement.SettlementIdentityFirstClaimedZone, out KingdomIdentityFault _) &&
				Settlement.LifecycleBook != null && !Settlement.LifecycleBook.LegacyIdentity &&
				string.Equals(Settlement.LifecycleBook.SettlementId, SettlementId,
					StringComparison.Ordinal) &&
				KingdomLifecycleRules.CanOwnAuthority(Settlement.LifecycleBook);
		}
	}
}
