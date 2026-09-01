using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Pure pricing and exact-evidence law for <c>Goods="Yes"</c> yard trades.</summary>
	public static class KingdomYardGoodsRules
	{
		public sealed class FoodHouseholdEvidence
		{
			public string PlotId;
			public string YardKey;
			public string ExpectedBlueprint;
			public int FoodCap;
			public bool Built;
			public bool Eligible;
			public bool Registered;
		}

		public sealed class FoodFixtureEvidence
		{
			public string PlotId;
			public string YardKey;
			public string Blueprint;
			public bool Standing;
			public bool InYard;
			public bool Unbroken;
		}

		/// <summary>One household sideline exchanges one bundle for one dram on each due cycle.</summary>
		public const int DramsPerHousehold = 1;

		/// <summary>A household sideline may improve a caravan, never replace the charter itself.</summary>
		public const int MaxHouseholdsPerCaravan = 4;

		public sealed class HouseholdEvidence
		{
			public string PlotId;
			public string YardKey;
			public bool Built;
			public bool Eligible;
			public bool FeedsGoods;
			public bool Working;
		}

		public sealed class FixtureEvidence
		{
			public string PlotId;
			public string YardKey;
			public bool Standing;
			public bool InYard;
			public bool FeedsGoods;
		}

		private struct Pair : IEquatable<Pair>
		{
			internal readonly string PlotId;
			internal readonly string YardKey;

			internal Pair(string PlotId, string YardKey)
			{
				this.PlotId = PlotId;
				this.YardKey = YardKey;
			}

			public bool Equals(Pair Other)
			{
				return string.Equals(PlotId, Other.PlotId, StringComparison.Ordinal)
					&& string.Equals(YardKey, Other.YardKey, StringComparison.Ordinal);
			}

			public override bool Equals(object Object)
			{
				return Object is Pair && Equals((Pair)Object);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return ((PlotId == null ? 0 : StringComparer.Ordinal.GetHashCode(PlotId)) * 397)
						^ (YardKey == null ? 0 : StringComparer.Ordinal.GetHashCode(YardKey));
				}
			}
		}

		/// <summary>
		/// Counts only one eligible built household paired to one standing fixture on its own yard.
		/// Missing, moved, mismatched, or duplicate authority yields nothing. Result is hard-capped.
		/// </summary>
		public static int ExactStandingHouseholds(IList<HouseholdEvidence> Households,
			IList<FixtureEvidence> Fixtures)
		{
			if (Households == null || Fixtures == null) return 0;
			Dictionary<Pair, int> houses = new Dictionary<Pair, int>();
			Dictionary<Pair, int> fixtures = new Dictionary<Pair, int>();
			for (int i = 0; i < Households.Count; i++)
			{
				HouseholdEvidence row = Households[i];
				if (row == null || !row.Built || !row.Eligible || !row.FeedsGoods
					|| !row.Working || string.IsNullOrEmpty(row.PlotId)
					|| string.IsNullOrEmpty(row.YardKey)) continue;
				Increment(houses, new Pair(row.PlotId, row.YardKey));
			}
			for (int i = 0; i < Fixtures.Count; i++)
			{
				FixtureEvidence row = Fixtures[i];
				if (row == null || !row.Standing || !row.InYard || !row.FeedsGoods
					|| string.IsNullOrEmpty(row.PlotId)
					|| string.IsNullOrEmpty(row.YardKey)) continue;
				Increment(fixtures, new Pair(row.PlotId, row.YardKey));
			}
			int exact = 0;
			foreach (KeyValuePair<Pair, int> row in houses)
			{
				int fixtureCount;
				if (row.Value != 1 || !fixtures.TryGetValue(row.Key, out fixtureCount)
					|| fixtureCount != 1) continue;
				exact++;
				if (exact == MaxHouseholdsPerCaravan) break;
			}
			return exact;
		}

		/// <summary>One registered food sideline needs exactly one matching live fixture.</summary>
		public static int ExactPhysicalFood(FoodHouseholdEvidence House,
			IList<FoodFixtureEvidence> Fixtures)
		{
			if (House == null || Fixtures == null || !House.Built || !House.Eligible
				|| !House.Registered || House.FoodCap <= 0
				|| House.FoodCap > KingdomYardRules.MaxShadePerWork
				|| string.IsNullOrEmpty(House.PlotId) || string.IsNullOrEmpty(House.YardKey)
				|| string.IsNullOrEmpty(House.ExpectedBlueprint)) return 0;
			FoodFixtureEvidence exact = null;
			int matches = 0;
			for (int i = 0; i < Fixtures.Count; i++)
			{
				FoodFixtureEvidence row = Fixtures[i];
				if (row == null || !string.Equals(row.PlotId, House.PlotId,
					StringComparison.Ordinal) || !string.Equals(row.YardKey, House.YardKey,
					StringComparison.Ordinal)) continue;
				matches++;
				if (matches == 1) exact = row;
			}
			return matches == 1 && exact.Standing && exact.InYard && exact.Unbroken
				&& string.Equals(exact.Blueprint, House.ExpectedBlueprint,
					StringComparison.Ordinal) ? House.FoodCap : 0;
		}

		/// <summary>Freezes adjusted per-cycle charter income without overflow.</summary>
		public static int IncomePerCycle(int BaseIncome, int ExactHouseholds)
		{
			if (BaseIncome < 0) return -1;
			int households = ExactHouseholds;
			if (households < 0) households = 0;
			if (households > MaxHouseholdsPerCaravan)
				households = MaxHouseholdsPerCaravan;
			long total = (long)BaseIncome + households * DramsPerHousehold;
			return total > int.MaxValue ? int.MaxValue : (int)total;
		}

		public static string EffectSummary()
		{
			return "adds 1 dram to each chartered caravan per exact standing household, up to 4 drams per caravan";
		}

		private static void Increment(Dictionary<Pair, int> Counts, Pair Key)
		{
			int current;
			if (!Counts.TryGetValue(Key, out current)) current = 0;
			Counts[Key] = current == int.MaxValue ? int.MaxValue : current + 1;
		}
	}
}
