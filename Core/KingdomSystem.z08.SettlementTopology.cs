using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>Number of owned cities not occupying the flat active seat.</summary>
		public int NonSeatSettlementCount => SettlementTopology?.Count ?? 0;

		/// <summary>Immutable-id-ordered snapshot of the non-seat topology.</summary>
		public List<KingdomSettlement> NonSeatSettlements()
		{
			return SettlementTopology?.Snapshot() ?? new List<KingdomSettlement>();
		}

		public KingdomSettlement NonSeatSettlementAt(int Index)
		{
			return SettlementTopology?.Get(Index);
		}

		public KingdomSettlement FindNonSeatSettlementByZone(string ZoneId)
		{
			return SettlementTopology?.FindByZone(ZoneId);
		}

		internal KingdomSettlement FindNonSeatSettlementById(string SettlementId)
		{
			return SettlementTopology?.FindById(SettlementId);
		}

		internal KingdomSettlement FindNonSeatSettlementByBook(
			Simulation.City.KingdomCityBook Book)
		{
			if (Book == null) return null;
			List<KingdomSettlement> rows = NonSeatSettlements();
			KingdomSettlement found = null;
			for (int i = 0; i < rows.Count; i++)
			{
				if (!ReferenceEquals(rows[i]?.City, Book)) continue;
				if (found != null) return null;
				found = rows[i];
			}
			return found;
		}

		/// <summary>Resolves one exact owned city book. A null row with <paramref name="Seated"/>
		/// true denotes the flat active seat; non-seat results return their mutable record.</summary>
		internal bool TryFindSettlement(Simulation.City.KingdomCityBook Book,
			out bool Seated, out KingdomSettlement Settlement)
		{
			Seated = Book != null && ReferenceEquals(Book, City);
			Settlement = Seated ? null : FindNonSeatSettlementByBook(Book);
			return Seated != (Settlement != null);
		}

		internal bool TryFindSettlement(string SettlementId, out bool Seated,
			out KingdomSettlement Settlement)
		{
			Seated = !string.IsNullOrEmpty(SettlementId) && string.Equals(
				City?.SettlementId, SettlementId, StringComparison.Ordinal);
			Settlement = Seated ? null : FindNonSeatSettlementById(SettlementId);
			return Seated != (Settlement != null);
		}

		internal List<Simulation.City.KingdomCityBook> OwnedCityBooks()
		{
			List<Simulation.City.KingdomCityBook> books =
				new List<Simulation.City.KingdomCityBook>();
			if (City != null) books.Add(City);
			List<KingdomSettlement> rows = NonSeatSettlements();
			for (int i = 0; i < rows.Count; i++)
				if (rows[i]?.City != null) books.Add(rows[i].City);
			return books;
		}

		internal bool TryAddNonSeatSettlement(KingdomSettlement Settlement,
			out string Failure)
		{
			if (SettlementTopology == null) SettlementTopology = new KingdomSettlementTopology();
			if (Settlement?.City == null || string.Equals(Settlement.City.SettlementId,
				City?.SettlementId, StringComparison.Ordinal))
			{
				Failure = "Non-seat publication would duplicate or omit the seated identity.";
				return false;
			}
			if (!SettlementTopology.TryAdd(Settlement, out Failure)) return false;
			SynchronizeLegacySettlementProjection();
			return true;
		}

		internal bool TryRemoveNonSeatSettlement(KingdomSettlement Settlement,
			out string Failure)
		{
			Failure = null;
			if (SettlementTopology == null ||
				!SettlementTopology.TryRemoveReference(Settlement, out Failure)) return false;
			SynchronizeLegacySettlementProjection();
			return true;
		}

		internal bool TryReplaceNonSeatSettlement(KingdomSettlement Expected,
			KingdomSettlement Replacement, out string Failure)
		{
			Failure = null;
			if (SettlementTopology == null ||
				!SettlementTopology.TryReplaceReference(Expected, Replacement, out Failure))
				return false;
			SynchronizeLegacySettlementProjection();
			return true;
		}

		internal bool OwnedZone(string ZoneId)
		{
			if (string.IsNullOrEmpty(ZoneId)) return false;
			bool seat = ClaimedZones != null && ClaimedZones.Contains(ZoneId);
			KingdomSettlement other = FindNonSeatSettlementByZone(ZoneId);
			return seat != (other != null);
		}

		internal string SettlementIdForOwnedZone(string ZoneId)
		{
			if (string.IsNullOrEmpty(ZoneId)) return null;
			bool seat = ClaimedZones != null && ClaimedZones.Contains(ZoneId);
			KingdomSettlement other = FindNonSeatSettlementByZone(ZoneId);
			if (seat == (other != null)) return null;
			return seat ? City?.SettlementId : other.City?.SettlementId;
		}

		private void StageLegacySettlementTopology()
		{
			if (SettlementTopology == null) SettlementTopology = new KingdomSettlementTopology();
#pragma warning disable 618
			KingdomSettlement legacy = Away;
#pragma warning restore 618
			if (SettlementTopology.Count == 0 && legacy != null)
			{
				if (!SettlementTopology.TryAdoptLegacy(legacy, out string failure))
					QuarantineIdentity(failure);
			}
			else if (legacy != null && SettlementTopology.Count > 0)
			{
				KingdomSettlement canonical = SettlementTopology.Get(0);
				string failure = null;
				if (!ReferenceEquals(legacy, canonical) &&
					!KingdomArchivedSettlementCodec.ExactGraph(legacy, canonical,
						out failure)) QuarantineIdentity(
						"legacy Away projection differs from exact topology: " + failure);
			}
			SettlementTopology.NormalizeMembers();
			SynchronizeLegacySettlementProjection();
		}

		private void ValidateSettlementTopology()
		{
			string failure = null;
			if (SettlementTopology == null ||
				!SettlementTopology.NormalizeCurrent(out failure))
			{
				QuarantineIdentity(failure ?? "settlement topology is absent");
				return;
			}
			List<string> ids = new List<string>();
			for (int i = 0; i < SettlementTopology.Count; i++)
				ids.Add(SettlementTopology.Get(i)?.City?.SettlementId);
			if (Founded && !KingdomSettlementTopologyRules.TryCanonicalize(City?.SettlementId,
				ids, out List<string> canonical, out failure)) QuarantineIdentity(failure);
			SynchronizeLegacySettlementProjection();
		}

		private void SynchronizeLegacySettlementProjection()
		{
#pragma warning disable 618
			Away = SettlementTopology?.Get(0);
#pragma warning restore 618
		}

		private void SynchronizeLegacyExiledProjection()
		{
#pragma warning disable 618
			ExiledAway = ExiledSettlementTopology?.Get(0);
#pragma warning restore 618
		}
	}
}
