using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Pure container-level catch-up. Visibility is the first key; stored dedication and stable id
	/// follow. Debt leaves only after the callback reports a measured physical delta.
	/// </summary>
	internal static class KingdomContainerCatchUpRules
	{
		internal static bool TryMeasure(
			KingdomContainerCatchUpRow[] rows,
			int count,
			int owedWater,
			int owedFood,
			int owedMaterials,
			out KingdomContainerDemandReceipt receipt,
			out KingdomCityFault fault)
		{
			receipt = default(KingdomContainerDemandReceipt);
			int[] order;
			if (!TryOrder(rows, count, out order, out fault)) return false;
			int water = Magnitude(owedWater);
			int food = Magnitude(owedFood);
			int materials = Magnitude(owedMaterials);
			if (water < 0 || food < 0 || materials < 0)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			int waterBefore = water;
			int foodBefore = food;
			int materialsBefore = materials;
			int visible = 0;
			int rest = 0;
			for (int i = 0; i < count; i++)
			{
				KingdomContainerCatchUpRow row = rows[order[i]];
				int remaining = Remaining(row.Kind, water, food, materials);
				int signed = Owed(row.Kind, owedWater, owedFood, owedMaterials);
				int available = (signed > 0) ? row.Room : ((signed < 0) ? row.Contents : 0);
				if (remaining <= 0 || available <= 0) continue;
				int moved = (available < remaining) ? available : remaining;
				Reduce(row.Kind, moved, ref water, ref food, ref materials);
				if (row.Visible) visible++; else rest++;
			}
			receipt = new KingdomContainerDemandReceipt(
				visible, rest,
				waterBefore - water, foodBefore - food, materialsBefore - materials,
				water, food, materials);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Applies up to the visible/rest allowances. A callback refusal stops the ordered walk:
		/// a later reserve may not leapfrog the oldest eligible container. Any measured partial delta
		/// still clears exactly that much debt and costs exactly one medium unit.
		/// </summary>
		internal static bool TrySettle(
			KingdomContainerCatchUpRow[] rows,
			int count,
			int owedWater,
			int owedFood,
			int owedMaterials,
			int visibleAllowance,
			int restAllowance,
			KingdomContainerSettlement settle,
			out KingdomContainerSettlementReceipt receipt,
			out KingdomCityFault fault)
		{
			receipt = new KingdomContainerSettlementReceipt(
				owedWater, owedFood, owedMaterials, 0, 0, false);
			if (settle == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (visibleAllowance < 0 || restAllowance < 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int[] order;
			if (!TryOrder(rows, count, out order, out fault)) return false;
			int water = owedWater;
			int food = owedFood;
			int materials = owedMaterials;
			int units = 0;
			int visibleSpent = 0;
			bool failed = false;
			for (int i = 0; i < count; i++)
			{
				int source = order[i];
				KingdomContainerCatchUpRow row = rows[source];
				if (row.Visible)
				{
					if (visibleAllowance <= 0) continue;
				}
				else if (restAllowance <= 0) continue;
				int signed = Owed(row.Kind, water, food, materials);
				int magnitude = Magnitude(signed);
				if (magnitude < 0)
				{
					fault = KingdomCityFault.ArithmeticOverflow;
					return false;
				}
				int available = (signed > 0) ? row.Room : ((signed < 0) ? row.Contents : 0);
				if (magnitude <= 0 || available <= 0) continue;
				int offered = (available < magnitude) ? available : magnitude;
				int applied = 0;
				bool accepted;
				try
				{
					accepted = settle(source, row.Kind,
						(signed > 0) ? KingdomUnitDirection.Land : KingdomUnitDirection.Draw,
						offered, out applied);
				}
				catch
				{
					accepted = false;
					applied = 0;
				}
				if (applied < 0 || applied > offered)
				{
					fault = KingdomCityFault.InvalidCapacity;
					return false;
				}
				if (applied > 0)
				{
					Apply(row.Kind, signed, applied, ref water, ref food, ref materials);
					units++;
					if (row.Visible) { visibleAllowance--; visibleSpent++; }
					else restAllowance--;
				}
				if (!accepted || applied != offered)
				{
					failed = true;
					break;
				}
			}
			receipt = new KingdomContainerSettlementReceipt(
				water, food, materials, units, visibleSpent, failed);
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool TryOrder(KingdomContainerCatchUpRow[] rows, int count,
			out int[] order, out KingdomCityFault fault)
		{
			order = null;
			if (rows == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > rows.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			order = new int[count];
			for (int i = 0; i < count; i++)
			{
				if (rows[i].Room < 0 || rows[i].Contents < 0
					|| (int)rows[i].Kind < (int)KingdomStockKind.Water
					|| (int)rows[i].Kind > (int)KingdomStockKind.Materials)
				{
					fault = KingdomCityFault.InvalidCapacity;
					return false;
				}
				order[i] = i;
			}
			for (int i = 1; i < count; i++)
			{
				int value = order[i];
				int j = i - 1;
				while (j >= 0 && Precedes(rows[value], value, rows[order[j]], order[j]))
				{
					order[j + 1] = order[j];
					j--;
				}
				order[j + 1] = value;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool Precedes(KingdomContainerCatchUpRow left, int leftIndex,
			KingdomContainerCatchUpRow right, int rightIndex)
		{
			if (left.Visible != right.Visible) return left.Visible;
			if (left.DedicationOrdinal != right.DedicationOrdinal)
				return left.DedicationOrdinal < right.DedicationOrdinal;
			if (left.ContainerId != right.ContainerId) return left.ContainerId < right.ContainerId;
			return leftIndex < rightIndex;
		}

		private static int Owed(KingdomStockKind kind, int water, int food, int materials)
		{
			return (kind == KingdomStockKind.Water) ? water
				: ((kind == KingdomStockKind.Food) ? food : materials);
		}

		private static int Remaining(KingdomStockKind kind, int water, int food, int materials)
		{
			return Magnitude(Owed(kind, water, food, materials));
		}

		private static int Magnitude(int value)
		{
			return (value == int.MinValue) ? -1 : Math.Abs(value);
		}

		private static void Reduce(KingdomStockKind kind, int amount,
			ref int water, ref int food, ref int materials)
		{
			if (kind == KingdomStockKind.Water) water -= amount;
			else if (kind == KingdomStockKind.Food) food -= amount;
			else materials -= amount;
		}

		private static void Apply(KingdomStockKind kind, int signed, int amount,
			ref int water, ref int food, ref int materials)
		{
			int delta = (signed > 0) ? -amount : amount;
			if (kind == KingdomStockKind.Water) water += delta;
			else if (kind == KingdomStockKind.Food) food += delta;
			else materials += delta;
		}
	}
}
