namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomLogisticsRules
	{
		// ==================================================================================
		// Internals
		// ==================================================================================

		private static bool Eligible(KingdomHolderRow holder, int distance, KingdomStockKind kind)
		{
			return holder.Holds == kind && holder.Amount > 0L && distance >= 0 && distance < NoRoute;
		}

		private static bool SharesRoutePrefix(KingdomLogisticsRequest left,
			KingdomLogisticsRequest right)
		{
			return left.SourceEndpointId == right.SourceEndpointId
				&& left.SourceZoneIndex == right.SourceZoneIndex
				&& left.Cargo == right.Cargo
				&& left.CargoAuthority == right.CargoAuthority
				&& (left.CargoAuthority != KingdomDeliveryCargoAuthority.CarryBookManifest
					|| string.Equals(left.OwnerOperationId, right.OwnerOperationId,
						System.StringComparison.Ordinal))
				&& left.ZoneRoute != null && right.ZoneRoute != null
				&& left.ZoneRouteCount >= 2 && right.ZoneRouteCount >= 2
				&& left.ZoneRoute[0] == right.ZoneRoute[0]
				&& left.ZoneRoute[1] == right.ZoneRoute[1];
		}

		/// <summary>The frozen order of &sect;3.10(1): distance, then holder id, then dedication.
		/// Written out rather than delegated to a comparer, because the comparison IS the
		/// invariant.</summary>
		private static bool Nearer(KingdomHolderRow candidate, int candidateCells, KingdomHolderRow standing, int standingCells)
		{
			if (candidateCells != standingCells)
			{
				return candidateCells < standingCells;
			}
			if (candidate.HolderId != standing.HolderId)
			{
				return candidate.HolderId < standing.HolderId;
			}
			return candidate.DedicationOrdinal < standing.DedicationOrdinal;
		}

		private static int Length(int[] between, int nodes, int[] order, int count)
		{
			int total = 0;
			int at = 0;
			for (int i = 0; i < count; i++)
			{
				total += between[(at * nodes) + order[i] + 1];
				at = order[i] + 1;
			}
			return total;
		}

		/// <summary>What reversing <c>order[i..j]</c> would cost, as the two edges it replaces
		/// against the two it lays. A closed form rather than a re-measure of the whole route,
		/// which is what keeps a swap test at four operations.</summary>
		private static int Delta(int[] between, int nodes, int[] order, int count, int i, int j)
		{
			int before = (i == 0) ? 0 : (order[i - 1] + 1);
			int after = (j == count - 1) ? -1 : (order[j + 1] + 1);
			int head = order[i] + 1;
			int tail = order[j] + 1;
			int was = between[(before * nodes) + head];
			int now = between[(before * nodes) + tail];
			if (after >= 0)
			{
				was += between[(tail * nodes) + after];
				now += between[(head * nodes) + after];
			}
			return now - was;
		}

		private static void Reverse(int[] order, int i, int j)
		{
			while (i < j)
			{
				int swap = order[i];
				order[i] = order[j];
				order[j] = swap;
				i++;
				j--;
			}
		}
	}
}
