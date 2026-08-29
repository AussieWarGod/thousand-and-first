using System;
using System.Collections.Generic;

using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private sealed class RoutedInputRaw
		{
			internal KingdomConstructionInputKind Kind;
			internal string Classification;
			internal RoutedInputZone Authority;
			internal string HolderId;
			internal string SourceObjectId;
			internal KingdomConstructionInputTopology Topology;
			internal int X, Y, Count, DedicationOrdinal;
			internal string Blueprint;
			internal bool AlwaysStack, ProtectedCargo, Leased;
		}

		private sealed class RoutedInputAggregate
		{
			internal int Stock, Prior, Floor;
		}

		private static bool TryScanInputCandidates(KingdomSystem system,
			IList<RoutedInputZone> zones, KingdomConstructionInputLeaseSet leases,
			KingdomConstructionJob job, IList<string> requiredObjectIds,
			string targetZoneId, int targetX, int targetY,
			long now, out List<KingdomConstructionInputCandidate> candidates,
			out string failure)
		{
			candidates = null;
			failure = null;
			List<RoutedInputRaw> raw = new List<RoutedInputRaw>();
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < zones.Count; i++)
			{
				if (!ScanInputWater(zones[i], leases, raw, identities, out failure)
					|| !ScanInputMaterials(zones[i], leases, job, requiredObjectIds,
						raw, identities, out failure))
					return false;
				if (raw.Count > KingdomConstructionInputPlanRules.MaxScannedCandidates)
				{
					failure = "The routed-input candidate bound was exceeded; nothing was truncated.";
					return false;
				}
			}
			Dictionary<string, RoutedInputAggregate> groups =
				new Dictionary<string, RoutedInputAggregate>(StringComparer.Ordinal);
			for (int i = 0; i < raw.Count; i++)
			{
				RoutedInputRaw row = raw[i];
				string key = InputGroupKey(row);
				RoutedInputAggregate group;
				if (!groups.TryGetValue(key, out group))
				{
					int floor = 0;
					if (row.Kind == KingdomConstructionInputKind.Water
						&& !KingdomConstructionInputRules.TryWaterReserveFloor(
							row.Authority.DailyWaterUpkeep, out floor))
					{
						failure = "A settlement water reserve exceeds the exact accounting range.";
						return false;
					}
					group = new RoutedInputAggregate { Floor = floor };
					groups.Add(key, group);
				}
				if (group.Stock > int.MaxValue - row.Count
					|| (row.Leased && group.Prior > int.MaxValue - row.Count))
				{
					failure = "A routed-input source aggregate overflowed.";
					return false;
				}
				group.Stock += row.Count;
				if (row.Leased) group.Prior += row.Count;
			}

			List<KingdomConstructionInputCandidate> found =
				new List<KingdomConstructionInputCandidate>();
			Dictionary<string, int> routeCosts = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < raw.Count; i++)
			{
				RoutedInputRaw row = raw[i];
				if (row.Leased) continue;
				string holderId = row.HolderId;
				string sourceId = row.SourceObjectId;
				if (string.IsNullOrEmpty(holderId) || string.IsNullOrEmpty(sourceId))
				{
					failure = "A routed-input candidate lost its assigned identity.";
					return false;
				}
				string endpoint = row.Authority.ZoneId + "\0" + holderId;
				int routeCost;
				if (!routeCosts.TryGetValue(endpoint, out routeCost))
				{
					long arrival;
					KingdomCityFault routeFault;
					if (!KingdomCentralLogistics.TryPreviewObservedManifestRoute(system,
						row.Authority.Observation, job.Id, holderId,
						row.Authority.ZoneId, row.X, row.Y, null, targetZoneId,
						targetX, targetY, now, out arrival, out routeFault))
					{
						routeCosts.Add(endpoint, -1);
						routeCost = -1;
					}
					else if (!TryInputRouteCost(now, arrival, out routeCost))
					{
						failure = "A routed-input itinerary exceeds the exact cost range.";
						return false;
					}
					else routeCosts.Add(endpoint, routeCost);
				}
				if (routeCost < 0) continue;
				RoutedInputAggregate group = groups[InputGroupKey(row)];
				found.Add(new KingdomConstructionInputCandidate(row.Kind, row.Classification,
					row.Authority.SettlementId, row.Authority.ZoneId, holderId,
					sourceId, row.Topology, row.X, row.Y, row.Blueprint, row.Count,
					group.Stock, group.Prior, group.Floor, routeCost,
					row.DedicationOrdinal, row.AlwaysStack));
			}
			candidates = found;
			return true;
		}

		private static bool ScanInputWater(RoutedInputZone authority,
			KingdomConstructionInputLeaseSet leases, List<RoutedInputRaw> into,
			HashSet<string> identities, out string failure)
		{
			failure = null;
			KingdomConstructionInputZoneObservation observation = authority.Observation;
			for (int i = 0; observation != null && i < observation.LineCount; i++)
			{
				KingdomConstructionInputObservationLine line = observation.LineAt(i);
				if (line.Kind != KingdomConstructionInputKind.Water) continue;
				if (!AddInputIdentity(authority.ZoneId, line.HolderId,
					line.SourceObjectId, identities, out failure)) return false;
				into.Add(new RoutedInputRaw { Kind = KingdomConstructionInputKind.Water,
					Classification = KingdomConstructionInputRules.WaterClassification,
					Authority = authority, HolderId = line.HolderId,
					SourceObjectId = line.SourceObjectId,
					Topology = KingdomConstructionInputTopology.LiquidVessel,
					X = line.X, Y = line.Y, Count = line.Count,
					DedicationOrdinal = line.DedicationOrdinal, Blueprint = line.Blueprint,
					Leased = leases.Contains(authority.ZoneId, line.HolderId,
						line.SourceObjectId) });
			}
			return true;
		}

		private static bool ScanInputMaterials(RoutedInputZone authority,
			KingdomConstructionInputLeaseSet leases, KingdomConstructionJob job,
			IList<string> requiredObjectIds, List<RoutedInputRaw> into,
			HashSet<string> identities, out string failure)
		{
			failure = null;
			KingdomConstructionInputZoneObservation observation = authority.Observation;
			for (int i = 0; observation != null && i < observation.LineCount; i++)
			{
				KingdomConstructionInputObservationLine line = observation.LineAt(i);
				if (line.Kind == KingdomConstructionInputKind.Water
					|| line.ProtectedCargo && !RequiredPurposeCargo(job,
						requiredObjectIds, line.SourceObjectId)) continue;
				if (!AddInputIdentity(authority.ZoneId, line.HolderId,
					line.SourceObjectId, identities, out failure)) return false;
				into.Add(new RoutedInputRaw { Kind = line.Kind,
					Classification = line.Classification, Authority = authority,
					HolderId = line.HolderId, SourceObjectId = line.SourceObjectId,
					Topology = line.Topology, X = line.X, Y = line.Y, Count = line.Count,
					DedicationOrdinal = line.DedicationOrdinal, Blueprint = line.Blueprint,
					AlwaysStack = line.AlwaysStack, ProtectedCargo = line.ProtectedCargo,
					Leased = leases.Contains(authority.ZoneId, line.HolderId,
						line.SourceObjectId) });
			}
			return true;
		}

		/// <summary>Durable observation may nominate only an id named once by this exact frozen
		/// purpose commitment. Full object/receipt reproval still gates attended pickup.</summary>
		private static bool RequiredPurposeCargo(KingdomConstructionJob job,
			IList<string> requiredObjectIds, string objectId)
		{
			if (!KingdomPurpose.RequiredFundingObjectsMatch(job, requiredObjectIds)
				|| string.IsNullOrEmpty(objectId)) return false;
			int count = 0;
			for (int i = 0; i < requiredObjectIds.Count; i++)
				if (requiredObjectIds[i] == objectId) count++;
			return count == 1;
		}

		internal static bool TryInputClassification(GameObject item,
			out KingdomConstructionInputKind kind, out string classification)
		{
			KingdomMaterial material;
			if (KingdomMaterials.TryMaterialOf(item, out material))
				return KingdomConstructionInputPlanRules.TryUnitClassification(
					KingdomMaterialDebitSourceKind.Material, (int)material, null,
					out kind, out classification);
			KingdomExotic exotic;
			if (KingdomMaterials.TryExoticOf(item, out exotic))
				return KingdomConstructionInputPlanRules.TryUnitClassification(
					KingdomMaterialDebitSourceKind.Exotic, (int)exotic, null,
					out kind, out classification);
			return KingdomConstructionInputPlanRules.TryUnitClassification(
				KingdomMaterialDebitSourceKind.BitStock, 0, KingdomMaterials.UnitBits(item),
				out kind, out classification);
		}

		private static string InputGroupKey(RoutedInputRaw row)
		{
			return row.Kind == KingdomConstructionInputKind.Water
				? row.Authority.SettlementId + "\0water"
				: row.Authority.SettlementId + "\0" + row.Authority.ZoneId + "\0"
					+ row.HolderId + "\0" + (int)row.Kind + "\0" + row.Classification;
		}

		private static bool AddInputIdentity(string zone, string holder, string source,
			HashSet<string> identities, out string failure)
		{
			failure = null;
			if (string.IsNullOrEmpty(holder) || string.IsNullOrEmpty(source)
				|| !identities.Add(KingdomConstructionInputLeaseSet.Key(zone, holder, source)))
			{
				failure = "A dedicated routed-input source has ambiguous identity.";
				return false;
			}
			return true;
		}

		private static bool TryInputRouteCost(long now, long arrival, out int cost)
		{
			cost = 0;
			long ticks = arrival - now;
			int pace = KingdomItineraryRules.WalkTicksPerCellDefault;
			if (ticks < 0L || pace <= 0) return false;
			long cells = ticks / pace + (ticks % pace == 0L ? 0L : 1L);
			if (cells > int.MaxValue) return false;
			cost = (int)cells;
			return true;
		}
	}
}
