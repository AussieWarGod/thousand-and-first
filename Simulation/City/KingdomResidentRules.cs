using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The resident row's own rules: how a standing changes, who labours, and what the roll must
	/// add up to.
	/// <para>
	/// Pure and engine-free. LIVING-CITY-ARCHITECTURE &sect;8.3 sequences this wave explicitly:
	/// <b>W2 ships the rows and the binding but not the placement.</b> So everything here is a
	/// verdict about a row, and nothing here moves a body &mdash; placement is W3, and doing
	/// identity and movement in one wave is how a settler ends up in two places.
	/// </para>
	/// </summary>
	internal static partial class KingdomResidentRules
	{
		/// <summary>An origin the row does not name. Zero is "no origin", which is what a settler
		/// the ground never told us about actually has.</summary>
		internal const int NoOrigin = 0;

		internal static KingdomAccessionCarrierState AccessionCarriers(bool CityOriginal,
			bool CityAdvanced, bool BindingOriginal, bool BindingAdvanced)
		{
			if (CityOriginal && !CityAdvanced && BindingOriginal && !BindingAdvanced)
			{
				return KingdomAccessionCarrierState.Original;
			}
			if (!CityOriginal && CityAdvanced && !BindingOriginal && BindingAdvanced)
			{
				return KingdomAccessionCarrierState.Committed;
			}
			if (!CityOriginal && CityAdvanced && BindingOriginal && !BindingAdvanced)
			{
				return KingdomAccessionCarrierState.CityAdvanced;
			}
			if (CityOriginal && !CityAdvanced && !BindingOriginal && BindingAdvanced)
			{
				return KingdomAccessionCarrierState.BindingAdvanced;
			}
			return KingdomAccessionCarrierState.Unknown;
		}

		/// <summary>
		/// Exact semantic equality for the whole serialized city carrier. Accession uses
		/// this after every publish attempt because <c>KingdomCityBook.TryPublish</c>
		/// rewrites every column; matching residents alone cannot prove a torn book clean.
		/// </summary>
		internal static bool SameCity(KingdomCityState A, KingdomCityState B)
		{
			if (A == null || B == null || A.SchemaVersion != B.SchemaVersion
				|| A.RulesVersion != B.RulesVersion || A.SettlementId != B.SettlementId
				|| A.ProcessedThroughTick != B.ProcessedThroughTick
				|| !SameStocks(A.Stocks, B.Stocks) || A.ZoneCount != B.ZoneCount
				|| A.WorkCount != B.WorkCount || A.ResidentCount != B.ResidentCount
				|| A.ClockCount != B.ClockCount || A.ToldCount != B.ToldCount)
			{
				return false;
			}
			for (int i = 0; i < A.ZoneCount; i++)
			{
				KingdomZoneRow a;
				KingdomZoneRow b;
				if (!A.TryZone(i, out a) || !B.TryZone(i, out b) || !SameZone(a, b)) return false;
			}
			for (int i = 0; i < A.WorkCount; i++)
			{
				KingdomWorkRow a;
				KingdomWorkRow b;
				if (!A.TryWork(i, out a) || !B.TryWork(i, out b) || !SameWork(a, b)) return false;
			}
			for (int i = 0; i < A.ResidentCount; i++)
			{
				KingdomResidentRow a;
				KingdomResidentRow b;
				if (!A.TryResident(i, out a) || !B.TryResident(i, out b) || !SameResident(a, b)) return false;
			}
			for (int i = 0; i < A.ClockCount; i++)
			{
				KingdomClockRow a;
				KingdomClockRow b;
				if (!A.TryClock(i, out a) || !B.TryClock(i, out b)
					|| a.Kind != b.Kind || a.NextDueTick != b.NextDueTick || a.Ordinal != b.Ordinal)
				{
					return false;
				}
			}
			for (int i = 0; i < A.ToldCount; i++)
			{
				KingdomToldRow a;
				KingdomToldRow b;
				if (!A.TryTold(i, out a) || !B.TryTold(i, out b) || a.Kind != b.Kind
					|| a.Tick != b.Tick || a.SubjectA != b.SubjectA || a.SubjectB != b.SubjectB
					|| a.PlaceZoneId != b.PlaceZoneId || a.Outcome != b.Outcome)
				{
					return false;
				}
			}
			return true;
		}

		private static bool SameStocks(KingdomStocks A, KingdomStocks B)
		{
			return A.Water.Level == B.Water.Level && A.Water.Capacity == B.Water.Capacity
				&& A.Food.Level == B.Food.Level && A.Food.Capacity == B.Food.Capacity
				&& A.Materials.Level == B.Materials.Level
				&& A.Materials.Capacity == B.Materials.Capacity;
		}

		private static bool SameZone(KingdomZoneRow A, KingdomZoneRow B)
		{
			return A.ZoneId == B.ZoneId && A.DistrictCode == B.DistrictCode
				&& A.LastReadTick == B.LastReadTick && SameStocks(A.Stocks, B.Stocks)
				&& A.Roofs == B.Roofs && A.Defence == B.Defence
				&& A.WaterCarry == B.WaterCarry && A.FoodCarry == B.FoodCarry
				&& A.OwedWater == B.OwedWater && A.OwedFood == B.OwedFood
				&& A.OwedMaterials == B.OwedMaterials;
		}

		private static bool SameWork(KingdomWorkRow A, KingdomWorkRow B)
		{
			return A.WorkId == B.WorkId && A.ZoneId == B.ZoneId && A.AnchorX == B.AnchorX
				&& A.AnchorY == B.AnchorY && A.DesignKey == B.DesignKey
				&& A.ConditionPercent == B.ConditionPercent && A.CrewAssigned == B.CrewAssigned
				&& A.RanThroughTick == B.RanThroughTick && A.RunState.Kind == B.RunState.Kind
				&& A.RunState.Stage == B.RunState.Stage && A.RunState.Progress == B.RunState.Progress
				&& A.RunState.NextTick == B.RunState.NextTick;
		}

		private static bool SameResident(KingdomResidentRow A, KingdomResidentRow B)
		{
			return A.ResidentId == B.ResidentId && A.Name == B.Name && A.Origin == B.Origin
				&& A.OriginCode == B.OriginCode && A.CreedCode == B.CreedCode
				&& A.ArrivedTick == B.ArrivedTick && A.Arrived == B.Arrived
				&& A.HomeWorkId == B.HomeWorkId
				&& A.JobWorkId == B.JobWorkId && A.JobRole == B.JobRole
				&& A.DayShape == B.DayShape && A.Standing == B.Standing && A.Cause == B.Cause
				&& A.BoundZoneId == B.BoundZoneId && SameBrink(A.RoofBrink, B.RoofBrink)
				&& SameBrink(A.CreedBrink, B.CreedBrink) && A.CreedToward == B.CreedToward
				&& A.CreedChannel == B.CreedChannel && A.KeptCreeds == B.KeptCreeds;
		}

		private static bool SameBrink(KingdomBrinkWindow A, KingdomBrinkWindow B)
		{
			return A.Stands == B.Stands && A.ReachedTick == B.ReachedTick
				&& A.WarnedTick == B.WarnedTick;
		}

	}
}
