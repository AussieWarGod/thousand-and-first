using System;
using System.Collections.Generic;
#if TAF_TESTS
using System.IO;
using System.Text;
#endif

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomJobRegistry
	{
		/// <summary>The next job id, minted off the realm's counter.</summary>
		public int MintJobId()
		{
			JobCounter++;
			return JobCounter;
		}

		private static KingdomJobKind KindOf(int stored)
		{
			if (stored == (int)KingdomJobKind.Delivery) { return KingdomJobKind.Delivery; }
			if (stored == (int)KingdomJobKind.Expedition) { return KingdomJobKind.Expedition; }
			return KingdomJobKind.None;
		}

		private static KingdomStockKind CargoOf(int stored)
		{
			if (stored == (int)KingdomStockKind.Food) { return KingdomStockKind.Food; }
			if (stored == (int)KingdomStockKind.Materials) { return KingdomStockKind.Materials; }
			if (stored == (int)KingdomStockKind.OpaqueManifest) { return KingdomStockKind.OpaqueManifest; }
			return KingdomStockKind.Water;
		}

		private static KingdomJobStatus StatusOf(int stored)
		{
			if (stored == (int)KingdomJobStatus.Delivered) { return KingdomJobStatus.Delivered; }
			if (stored == (int)KingdomJobStatus.Failed) { return KingdomJobStatus.Failed; }
			return KingdomJobStatus.Open;
		}

		private bool Duplicated(int index)
		{
			for (int i = 0; i < index; i++)
			{
				if (JobIds[i] == JobIds[index])
				{
					return true;
				}
			}
			return false;
		}

		private void DropOverCap()
		{
			int consumed = 0;
			for (int i = 0; i < JobIds.Count; i++)
			{
				if (i < KingdomJobRules.MaxOpenJobs)
				{
					consumed += LegCounts[i];
					continue;
				}
				RemoveLegs(consumed, LegCounts[i]);
				RemoveJob(i);
				i--;
			}
		}

		private void RemoveJob(int index)
		{
			JobIds.RemoveAt(index);
			Kinds.RemoveAt(index);
			Cargos.RemoveAt(index);
			CargoAmounts.RemoveAt(index);
			SourceZoneIds.RemoveAt(index);
			DestZoneIds.RemoveAt(index);
			StartTicks.RemoveAt(index);
			WalkTicksPerCell.RemoveAt(index);
			Statuses.RemoveAt(index);
			OriginCodes.RemoveAt(index);
			DepositLegIndexes.RemoveAt(index);
			SubjectIds.RemoveAt(index);
			SubjectNames.RemoveAt(index);
			TargetNames.RemoveAt(index);
			DueTicks.RemoveAt(index);
			WaterCosts.RemoveAt(index);
			ProvisionCosts.RemoveAt(index);
			OutcomeCodes.RemoveAt(index);
			DeliverySourceEndpointIds.RemoveAt(index);
			DeliverySourceObjectIds.RemoveAt(index);
			DeliverySourceXs.RemoveAt(index);
			DeliverySourceYs.RemoveAt(index);
			DeliveryTargetEndpointIds.RemoveAt(index);
			DeliveryTargetObjectIds.RemoveAt(index);
			DeliveryTargetXs.RemoveAt(index);
			DeliveryTargetYs.RemoveAt(index);
			DeliverySourceBeforeAmounts.RemoveAt(index);
			DeliveryTripIds.RemoveAt(index);
			DeliveryStopOrdinals.RemoveAt(index);
			DeliveryPhases.RemoveAt(index);
			DeliveryCargoAuthorityKinds.RemoveAt(index);
			DeliveryOwnerOperationIds.RemoveAt(index);
			DeliveryOwnerManifestVersions.RemoveAt(index);
			DeliveryOwnerManifestDigests.RemoveAt(index);
			DeliveryOwnerManifestRevisions.RemoveAt(index);
			DeliveryManifestSourceStarts.RemoveAt(index);
			DeliveryManifestSourceCounts.RemoveAt(index);
			DeliveryTargetBeforeAmounts.RemoveAt(index);
			DeliveryTargetReceiptStates.RemoveAt(index);
			LegCounts.RemoveAt(index);
		}

		private void RemoveLegs(int from, int count)
		{
			if (count <= 0 || from < 0 || from >= LegZoneIds.Count)
			{
				return;
			}
			int take = (from + count > LegZoneIds.Count) ? (LegZoneIds.Count - from) : count;
			LegZoneIds.RemoveRange(from, take);
			LegEnterX.RemoveRange(from, take);
			LegEnterY.RemoveRange(from, take);
			LegExitX.RemoveRange(from, take);
			LegExitY.RemoveRange(from, take);
			LegLengths.RemoveRange(from, take);
			LegDepartTicks.RemoveRange(from, take);
			LegArriveTicks.RemoveRange(from, take);
		}

		private static List<T> Repair<T>(List<T> column)
		{
			return column ?? new List<T>();
		}

		private static int Shortest(int[] counts)
		{
			int shortest = int.MaxValue;
			for (int i = 0; i < counts.Length; i++)
			{
				if (counts[i] < shortest)
				{
					shortest = counts[i];
				}
			}
			return (shortest == int.MaxValue) ? 0 : shortest;
		}

		private static void Trim<T>(List<T> column, int count)
		{
			if (column.Count > count)
			{
				column.RemoveRange(count, column.Count - count);
			}
		}

		private static void Pad<T>(List<T> column, int count, T value)
		{
			while (column.Count < count)
			{
				column.Add(value);
			}
		}
	}
}
