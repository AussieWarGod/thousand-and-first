using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		private static bool TryExactScalarAmount(KingdomSurvey survey, KingdomJobRow row,
			bool source, out long amount)
		{
			amount = 0L;
			GameObject target;
			LiquidVolume water;
			return TryExactScalar(survey, row, source, out target, out water, out amount);
		}

		private static bool TryExactScalarTarget(KingdomSurvey survey, KingdomJobRow row,
			out GameObject target, out LiquidVolume water, out long amount)
		{
			return TryExactScalar(survey, row, source: false, out target, out water, out amount);
		}

		private static bool TryExactScalar(KingdomSurvey survey, KingdomJobRow row, bool source,
			out GameObject target, out LiquidVolume water, out long amount)
		{
			target = null; water = null; amount = 0L;
			if (survey == null) return false;
			int endpoint = source ? row.DeliverySourceEndpointId : row.DeliveryTargetEndpointId;
			string objectId = source ? row.DeliverySourceObjectId : row.DeliveryTargetObjectId;
			if (row.Cargo == KingdomStockKind.Water)
			{
				for (int i = 0; i < survey.Stores.Count; i++)
				{
					LiquidVolume candidate = survey.Stores[i];
					GameObject owner = candidate == null ? null : candidate.ParentObject;
					if (!GameObject.Validate(owner) || !string.Equals(owner.IDIfAssigned, objectId,
						StringComparison.Ordinal)
						|| KingdomCityRules.StableId(owner.IDIfAssigned) != endpoint)
						continue;
					target = owner; water = candidate;
					amount = KingdomLiquids.HasFreshWater(candidate) ? candidate.Volume : 0L;
					return true;
				}
				return false;
			}
			if (row.Cargo == KingdomStockKind.Food)
			{
				for (int i = 0; i < survey.Larders.Count; i++)
				{
					GameObject candidate = survey.Larders[i];
					if (!GameObject.Validate(candidate)
						|| !string.Equals(candidate.IDIfAssigned, objectId, StringComparison.Ordinal)
						|| KingdomCityRules.StableId(candidate.IDIfAssigned) != endpoint)
						continue;
					target = candidate;
					if (!source) amount = KingdomSurvey.HeldIn(candidate);
					else
					{
						KingdomConstructionInputLeaseSnapshot leases;
						string failure;
						if (!KingdomOrdinaryFoodAuthority.TryCapture(out leases, out failure))
							return false;
						amount = KingdomOrdinaryFoodAuthority.AvailableIn(candidate, leases);
					}
					return true;
				}
			}
			return false;
		}

		private static bool TryDebitScalar(KingdomSurvey survey, KingdomJobRow row,
			int amount, out int debited)
		{
			debited = 0;
			GameObject target;
			LiquidVolume water;
			long before;
			if (amount <= 0 || !TryExactScalar(survey, row, source: true,
				out target, out water, out before) || before < amount) return false;
			return row.Cargo == KingdomStockKind.Water
				? survey.TryLeakFromExact(water, amount, out debited)
				: survey.TrySpoilFromExact(target, amount, out debited);
		}

		private static int AddMarkedFood(KingdomSurvey survey, GameObject target, int jobId,
			int amount, string blueprint)
		{
			if (survey == null || !GameObject.Validate(target) || target.Inventory == null
				|| amount <= 0 || string.IsNullOrEmpty(blueprint)) return 0;
			int before = MarkedFood(target, jobId);
			try
			{
				for (int i = 0; i < amount; i++)
				{
					GameObject food = GameObject.Create(blueprint);
					if (!GameObject.Validate(food)
						|| food.Count != 1 || !KingdomOrdinaryFoodAuthority.IsEdible(food))
					{
						string cleanupFailure;
						if (GameObject.Validate(food) && KingdomOrdinaryFoodAuthority.TryObjectNow(
							food, out cleanupFailure)) food.Obliterate();
						break;
					}
					string foodId = food.ID;
					food.SetIntProperty(KingdomPorters.StockProperty, 1);
					food.SetIntProperty(FoodReceiptJobProperty, jobId);
					target.Inventory.AddObject(food, Silent: true, NoStack: true);
					string failure;
					if (!GameObject.Validate(food) || food.InInventory != target
						|| food.CurrentCell != null || food.Count != 1 || food.IDIfAssigned != foodId
						|| food.Blueprint != blueprint
						|| food.GetIntProperty(KingdomPorters.StockProperty) != 1
						|| !KingdomOrdinaryFoodAuthority.TrySpendNow(food,
							FoodReceiptJobProperty, jobId, out failure)) break;
				}
			}
			catch
			{
				// Inventory callbacks may land one or more marked units before throwing.
				// Publish the measured receipt delta before the module guard sees the fault.
				PublishMarkedFoodDelta(survey, target, jobId, before);
				throw;
			}
			return PublishMarkedFoodDelta(survey, target, jobId, before);
		}

		private static int PublishMarkedFoodDelta(KingdomSurvey survey, GameObject target,
			int jobId, int before)
		{
			int added = MarkedFood(target, jobId) - before;
			survey.RefreshFoodTopology();
			if (added > 0)
			{
				survey.SynchronizeReceiptObject(target);
			}
			return added;
		}

		private static int MarkedFood(GameObject target, int jobId)
		{
			int count = 0;
			KingdomConstructionInputLeaseSnapshot leases;
			string failure;
			if (!KingdomOrdinaryFoodAuthority.TryCapture(out leases, out failure)) return 0;
			List<GameObject> items = !GameObject.Validate(target) || target.Inventory == null
				? null : target.Inventory.GetObjects();
			for (int i = 0; items != null && i < items.Count; i++)
			{
				GameObject item = items[i];
				if (GameObject.Validate(item) && item.InInventory == target
					&& item.GetIntProperty(FoodReceiptJobProperty) == jobId
					&& item.GetIntProperty(KingdomPorters.StockProperty) == 1
					&& KingdomOrdinaryFoodAuthority.CanSpend(leases, item,
						FoodReceiptJobProperty, jobId))
					count += item.Count;
			}
			return count;
		}

		private static List<KingdomJobRow> TripRows(KingdomJobTable table, int tripId)
		{
			List<KingdomJobRow> rows = new List<KingdomJobRow>();
			for (int i = 0; table != null && i < table.Count; i++)
			{
				KingdomJobRow row;
				if (table.TryAt(i, out row) && row.DeliveryTripId == tripId) rows.Add(row);
			}
			rows.Sort(delegate(KingdomJobRow a, KingdomJobRow b)
			{
				return a.DeliveryStopOrdinal.CompareTo(b.DeliveryStopOrdinal);
			});
			return rows;
		}

		private static bool PriorStopsLanded(KingdomJobTable table, KingdomJobRow row)
		{
			List<KingdomJobRow> group = TripRows(table, row.DeliveryTripId);
			for (int i = 0; i < group.Count; i++)
				if (group[i].DeliveryStopOrdinal < row.DeliveryStopOrdinal
					&& group[i].CargoAmount > 0) return false;
			return true;
		}

		private static bool TripLanded(KingdomJobTable table, int tripId)
		{
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count == 0) return false;
			for (int i = 0; i < rows.Count; i++) if (rows[i].CargoAmount > 0) return false;
			return true;
		}

		private static string Receipt(KingdomJobRow row)
		{
			return "taf:delivery:" + row.DeliveryTripId + ":" + row.JobId;
		}

		private static void SweepTarget(KingdomJobTable table, GameObject target)
		{
			if (!GameObject.Validate(target)) return;
			string marker = target.GetStringProperty(TargetReceiptProperty);
			bool active = false;
			for (int i = 0; !string.IsNullOrEmpty(marker) && table != null && i < table.Count; i++)
			{
				KingdomJobRow row;
				if (table.TryAt(i, out row) && string.Equals(row.DeliveryTargetObjectId,
					target.IDIfAssigned, StringComparison.Ordinal) && string.Equals(Receipt(row), marker,
					StringComparison.Ordinal)) { active = true; break; }
			}
			if (!active && !string.IsNullOrEmpty(marker)) target.RemoveStringProperty(TargetReceiptProperty);
			List<GameObject> items = target.Inventory == null ? null : target.Inventory.GetObjects();
			for (int i = 0; items != null && i < items.Count; i++)
			{
				int jobId = items[i].GetIntProperty(FoodReceiptJobProperty);
				if (jobId > 0 && (table == null || !table.Holds(jobId)))
					items[i].RemoveIntProperty(FoodReceiptJobProperty);
			}
		}
	}
}
