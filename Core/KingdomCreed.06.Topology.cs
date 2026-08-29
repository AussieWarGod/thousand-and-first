using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal sealed class KingdomCreedPairCity
	{
		internal string Id;
		internal string Name;
		internal string Creed;
		internal int Population;
		internal bool Seated;
		internal KingdomSettlement Settlement;
	}

	public static partial class KingdomCreed
	{
		private static bool EnsureDissentPair(KingdomSystem System,
			out KingdomCreedPairCity A, out KingdomCreedPairCity B)
		{
			if (!TryDissentPair(System, out A, out B)) return false;
			System.DissentSettlementAId = A.Id;
			System.DissentSettlementBId = B.Id;
			return true;
		}

		private static bool TryDissentPair(KingdomSystem System,
			out KingdomCreedPairCity A, out KingdomCreedPairCity B)
		{
			A = null;
			B = null;
			if (System == null || !System.Founded || System.SettlementCount < 2) return false;
			bool hasA = !string.IsNullOrEmpty(System.DissentSettlementAId);
			bool hasB = !string.IsNullOrEmpty(System.DissentSettlementBId);
			if (hasA || hasB)
			{
				if (!hasA || !hasB || !TryCity(System, System.DissentSettlementAId, out A)
					|| !TryCity(System, System.DissentSettlementBId, out B) ||
					string.CompareOrdinal(A.Id, B.Id) >= 0)
				{
					A = null;
					B = null;
					return false;
				}
				return true;
			}
			List<KingdomCreedPairCity> cities = Cities(System);
			int bestHostility = int.MinValue;
			for (int i = 0; i < cities.Count; i++)
				for (int j = i + 1; j < cities.Count; j++)
				{
					KingdomCreedPairCity left = cities[i];
					KingdomCreedPairCity right = cities[j];
					int hostility = HostilityBetween(left.Creed, right.Creed);
					if (A != null && hostility < bestHostility) continue;
					if (A != null && hostility == bestHostility &&
						ComparePair(left, right, A, B) >= 0) continue;
					A = left;
					B = right;
					bestHostility = hostility;
				}
			return A != null;
		}

		private static List<KingdomCreedPairCity> Cities(KingdomSystem System)
		{
			List<KingdomCreedPairCity> cities = new List<KingdomCreedPairCity>();
			cities.Add(new KingdomCreedPairCity
			{
				Id = System.City?.SettlementId,
				Name = System.SeatName,
				Creed = SeatCreed(System),
				Population = System.Population,
				Seated = true
			});
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				cities.Add(FromSettlement(nonSeat[i]));
			cities.Sort(delegate(KingdomCreedPairCity left, KingdomCreedPairCity right)
			{
				return string.CompareOrdinal(left.Id, right.Id);
			});
			return cities;
		}

		private static bool TryCity(KingdomSystem System, string Id,
			out KingdomCreedPairCity City)
		{
			City = null;
			if (!System.TryFindSettlement(Id, out bool seated, out KingdomSettlement row))
				return false;
			City = seated ? new KingdomCreedPairCity
			{
				Id = System.City?.SettlementId,
				Name = System.SeatName,
				Creed = SeatCreed(System),
				Population = System.Population,
				Seated = true
			} : FromSettlement(row);
			return KingdomIdentityRules.IsSettlementId(City.Id);
		}

		private static KingdomCreedPairCity FromSettlement(KingdomSettlement Row)
		{
			return new KingdomCreedPairCity
			{
				Id = Row?.City?.SettlementId,
				Name = Row?.SettlementName,
				Creed = CreedOf(Row),
				Population = Row?.Population ?? 0,
				Settlement = Row
			};
		}

		private static int ComparePair(KingdomCreedPairCity A1, KingdomCreedPairCity B1,
			KingdomCreedPairCity A2, KingdomCreedPairCity B2)
		{
			int first = string.CompareOrdinal(A1.Id, A2.Id);
			return first != 0 ? first : string.CompareOrdinal(B1.Id, B2.Id);
		}

		private static void ClearDissentPair(KingdomSystem System)
		{
			System.DissentSettlementAId = null;
			System.DissentSettlementBId = null;
		}
	}
}
