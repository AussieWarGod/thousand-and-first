using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		private static bool ExactArchivedSettlements(string RealmId,
			KingdomSettlement Seat, KingdomSettlementTopology Topology,
			IList<string> ExpectedIds)
		{
			if (!TryArchivedSettlementIds(RealmId, Seat, Topology,
				out List<string> ids)) return false;
			if (ExpectedIds == null || ids.Count != ExpectedIds.Count) return false;
			for (int i = 0; i < ids.Count; i++)
				if (!string.Equals(ids[i], ExpectedIds[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool TryArchivedRetainedIds(string RealmId,
			KingdomSettlement Seat, KingdomSettlementTopology Topology,
			KingdomSettlement Seceded, out List<string> Ids)
		{
			if (!TryArchivedSettlementIds(RealmId, Seat, Topology, out Ids)) return false;
			if (Seceded != null)
			{
				if (!ArchivedSettlementMatches(RealmId, Seceded, out string secededId) ||
					Ids.Contains(secededId)) return false;
				Ids.Add(secededId);
				Ids.Sort(StringComparer.Ordinal);
			}
			return KingdomIdentityRules.ValidateRealmTopology(RealmId, Ids,
				out KingdomIdentityFault _);
		}

		private static bool TryArchivedSettlementIds(string RealmId,
			KingdomSettlement Seat, KingdomSettlementTopology Topology,
			out List<string> Ids)
		{
			Ids = new List<string>();
			if (!ArchivedSettlementMatches(RealmId, Seat, out string seatId) ||
				Topology == null || Topology.HasOpaqueEvidence ||
				Topology.Count > KingdomSettlementTopologyRules.MaxNonSeatSettlements)
				return false;
			Ids.Add(seatId);
			for (int i = 0; i < Topology.Count; i++)
			{
				if (!ArchivedSettlementMatches(RealmId, Topology.Get(i), out string id) ||
					Ids.Contains(id)) return false;
				Ids.Add(id);
			}
			Ids.Sort(StringComparer.Ordinal);
			return KingdomIdentityRules.ValidateRealmTopology(RealmId, Ids,
				out KingdomIdentityFault _);
		}

		private static bool CanonicalTopologyReferences(KingdomSettlement Seat,
			KingdomSettlementTopology Topology, KingdomSettlement Projection,
			KingdomSettlement Seceded)
		{
			if (Topology == null || Topology.HasOpaqueEvidence ||
				Topology.Count > KingdomSettlementTopologyRules.MaxNonSeatSettlements ||
				!ReferenceEquals(Projection, Topology.Get(0))) return false;
			string prior = null;
			for (int i = 0; i < Topology.Count; i++)
			{
				KingdomSettlement row = Topology.Get(i);
				string id = row?.City?.SettlementId;
				if (row == null || ReferenceEquals(row, Seat) || ReferenceEquals(row, Seceded) ||
					(i > 0 && string.CompareOrdinal(prior, id) >= 0)) return false;
				for (int j = 0; j < i; j++)
					if (ReferenceEquals(row, Topology.Get(j))) return false;
				prior = id;
			}
			return !ReferenceEquals(Seat, Seceded);
		}

		private KingdomSettlement FindArchivedSettlement(string SettlementId)
		{
			if (string.Equals(Seat?.City?.SettlementId, SettlementId,
				StringComparison.Ordinal)) return Seat;
			return SettlementTopology?.FindById(SettlementId);
		}

		private bool ExactLiveSettlementTopology(KingdomSystem System, out string Failure)
		{
			Failure = null;
			if (System?.SettlementTopology == null || SettlementTopology == null ||
				System.SettlementTopology.HasOpaqueEvidence || SettlementTopology.HasOpaqueEvidence ||
				System.NonSeatSettlementCount != SettlementTopology.Count)
				return Refuse("current non-seat topology differs from archive", out Failure);
			KingdomSettlement currentSeat;
			try { currentSeat = System.Capture(); }
			catch (Exception ex) { return Refuse(Bound(ex.Message, 512), out Failure); }
			KingdomSettlement expectedSeat = FindArchivedSettlement(
				currentSeat?.City?.SettlementId);
			if (expectedSeat == null || !KingdomArchivedSettlementCodec.ExactGraph(
				expectedSeat, currentSeat, out Failure)) return false;
			List<KingdomSettlement> current = System.NonSeatSettlements();
			for (int i = 0; i < current.Count; i++)
			{
				KingdomSettlement expected = FindArchivedSettlement(
					current[i]?.City?.SettlementId);
				if (expected == null || !KingdomArchivedSettlementCodec.ExactGraph(
					expected, current[i], out Failure)) return false;
			}
			return true;
		}

		private static bool ExactTopologyGraphs(KingdomSettlementTopology Left,
			KingdomSettlementTopology Right, out string Failure)
		{
			Failure = null;
			if (Left == null || Right == null || ReferenceEquals(Left, Right) ||
				Left.HasOpaqueEvidence || Right.HasOpaqueEvidence || Left.Count != Right.Count)
				return false;
			for (int i = 0; i < Left.Count; i++)
			{
				KingdomSettlement left = Left.Get(i);
				KingdomSettlement right = Right.FindById(left?.City?.SettlementId);
				if (right == null || !KingdomArchivedSettlementCodec.ExactGraph(left, right,
					out Failure)) return false;
			}
			return true;
		}

		private static void WriteTopologyGraph(BinaryWriter Writer,
			KingdomSettlementTopology Topology)
		{
			if (Topology == null || Topology.HasOpaqueEvidence ||
				Topology.Count > KingdomSettlementTopologyRules.MaxNonSeatSettlements)
				throw new InvalidDataException("Realm settlement topology is not hashable.");
			Writer.Write(Topology.Count);
			for (int i = 0; i < Topology.Count; i++)
			{
				if (!KingdomArchivedSettlementCodec.TryEncode(Topology.Get(i),
					out byte[] payload, out string failure))
					throw new InvalidDataException(failure);
				WriteGraphBytes(Writer, payload);
			}
		}

		private static object[] SettlementRoots(KingdomSettlement Seat,
			KingdomSettlementTopology Topology, KingdomSettlement Seceded,
			params object[] Tail)
		{
			List<object> roots = new List<object> { Seat };
			if (Topology != null)
				for (int i = 0; i < Topology.Count; i++) roots.Add(Topology.Get(i));
			roots.Add(Seceded);
			if (Tail != null) roots.AddRange(Tail);
			return roots.ToArray();
		}
	}
}
