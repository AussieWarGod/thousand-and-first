using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputPlanRules
	{
		public const int MaxScannedCandidates = 4096;
		public const string WaterCargoBlueprint = "EmptyWaterskin";

		public static bool TryUnitClassification(KingdomMaterialDebitSourceKind Kind,
			int KindIndex, KingdomBitTally UnitBits, out KingdomConstructionInputKind InputKind,
			out string Classification)
		{
			InputKind = KingdomConstructionInputKind.Invalid;
			Classification = null;
			KingdomMaterialTally materials = new KingdomMaterialTally();
			KingdomBitTally bits = new KingdomBitTally();
			KingdomExoticTally exotics = new KingdomExoticTally();
			switch (Kind)
			{
			case KingdomMaterialDebitSourceKind.Material:
				if (KindIndex < 0 || KindIndex >= KingdomMaterialRules.MaterialCount) return false;
				materials.Set((KingdomMaterial)KindIndex, 1);
				InputKind = KingdomConstructionInputKind.Material;
				break;
			case KingdomMaterialDebitSourceKind.Exotic:
				if (KindIndex < 0 || KindIndex >= KingdomMaterialRules.ExoticCount) return false;
				exotics.Set((KingdomExotic)KindIndex, 1);
				InputKind = KingdomConstructionInputKind.Exotic;
				break;
			case KingdomMaterialDebitSourceKind.BitStock:
				if (UnitBits == null || UnitBits.IsEmpty()) return false;
				bits = UnitBits.Copy();
				InputKind = KingdomConstructionInputKind.Bit;
				break;
			default:
				return false;
			}
			Classification = new KingdomMaterialDebitCost(materials, bits, exotics).ToClaimString();
			return true;
		}

		private static bool TryOrderedCandidates(string operationId, string[] requiredObjectIds,
			IList<KingdomConstructionInputCandidate> candidates,
			out List<KingdomConstructionInputCandidate> ordered,
			out KingdomConstructionInputPlanFault fault)
		{
			ordered = null;
			if (!KingdomConstructionInputRules.ValidText(operationId,
				KingdomConstructionInputRules.MaxIdentityChars, false) || candidates == null)
				return Refuse(KingdomConstructionInputPlanFault.Null, out fault);
			if (requiredObjectIds == null
				|| requiredObjectIds.Length > KingdomConstructionInputRules.MaxRequiredObjects
				|| candidates.Count > MaxScannedCandidates)
				return Refuse(KingdomConstructionInputPlanFault.Bounds, out fault);
			Dictionary<string, int> requiredOrder = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < requiredObjectIds.Length; i++)
			{
				if (!KingdomConstructionInputRules.ValidText(requiredObjectIds[i],
					KingdomConstructionInputRules.MaxIdentityChars, false)
					|| requiredOrder.ContainsKey(requiredObjectIds[i]))
					return Refuse(KingdomConstructionInputPlanFault.RequiredObject, out fault);
				requiredOrder.Add(requiredObjectIds[i], i);
			}
			ordered = new List<KingdomConstructionInputCandidate>(candidates.Count);
			HashSet<string> physical = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> objectIds = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<string, CandidateGroup> groups =
				new Dictionary<string, CandidateGroup>(StringComparer.Ordinal);
			for (int i = 0; i < candidates.Count; i++)
			{
				KingdomConstructionInputCandidate row = candidates[i];
				if (!ValidCandidate(row))
					return Refuse(KingdomConstructionInputPlanFault.Identity, out fault);
				string key = KingdomConstructionInputLeaseSet.Key(row.SourceZoneId,
					row.HolderId, row.SourceObjectId);
				if (!physical.Add(key) || !objectIds.Add(row.SourceObjectId))
					return Refuse(KingdomConstructionInputPlanFault.Duplicate, out fault);
				string groupKey = GroupKey(row);
				CandidateGroup group;
				if (!groups.TryGetValue(groupKey, out group))
				{
					group = new CandidateGroup(row.HolderStockBefore,
						row.PriorReserved, row.ReserveFloor);
					groups.Add(groupKey, group);
				}
				if (!group.Add(row))
					return Refuse(KingdomConstructionInputPlanFault.Claim, out fault);
				ordered.Add(row);
			}
			foreach (CandidateGroup group in groups.Values)
				if (!group.Valid()) return Refuse(KingdomConstructionInputPlanFault.Claim, out fault);
			ordered.Sort(delegate(KingdomConstructionInputCandidate left,
				KingdomConstructionInputCandidate right)
			{
				int leftRequired, rightRequired;
				bool lr = requiredOrder.TryGetValue(left.SourceObjectId, out leftRequired);
				bool rr = requiredOrder.TryGetValue(right.SourceObjectId, out rightRequired);
				if (lr != rr) return lr ? -1 : 1;
				if (lr && leftRequired != rightRequired)
					return leftRequired.CompareTo(rightRequired);
				int compare = left.RouteCost.CompareTo(right.RouteCost);
				if (compare == 0) compare = string.CompareOrdinal(left.SourceSettlementId,
					right.SourceSettlementId);
				if (compare == 0) compare = string.CompareOrdinal(left.SourceZoneId, right.SourceZoneId);
				if (compare == 0) compare = string.CompareOrdinal(left.HolderId, right.HolderId);
				if (compare == 0) compare = left.DedicationOrdinal.CompareTo(right.DedicationOrdinal);
				if (compare == 0) compare = string.CompareOrdinal(left.SourceObjectId,
					right.SourceObjectId);
				return compare;
			});
			fault = KingdomConstructionInputPlanFault.None;
			return true;
		}

		private static bool ValidCandidate(KingdomConstructionInputCandidate row)
		{
			if (row == null || !KingdomConstructionInputRules.Defined(row.Kind)
				|| !KingdomConstructionInputRules.ValidText(row.Classification,
					KingdomConstructionInputRules.MaxClaimChars, false)
				|| !KingdomConstructionInputRules.ValidText(row.SourceSettlementId,
					KingdomConstructionInputRules.MaxIdentityChars, false)
				|| !KingdomConstructionInputRules.ValidText(row.SourceZoneId,
					KingdomConstructionInputRules.MaxIdentityChars, false)
				|| !KingdomConstructionInputRules.ValidText(row.HolderId,
					KingdomConstructionInputRules.MaxIdentityChars, false)
				|| !KingdomConstructionInputRules.ValidText(row.SourceObjectId,
					KingdomConstructionInputRules.MaxIdentityChars, false)
				|| !KingdomConstructionInputRules.ValidText(row.Blueprint,
					KingdomConstructionInputRules.MaxBlueprintChars, false)
				|| row.X < 0 || row.X > KingdomConstructionInputRules.MaxCoordinate
				|| row.Y < 0 || row.Y > KingdomConstructionInputRules.MaxCoordinate
				|| row.Count <= 0 || row.HolderStockBefore < row.Count
				|| row.PriorReserved < 0 || row.ReserveFloor < 0 || row.RouteCost < 0
				|| row.DedicationOrdinal < 0) return false;
			if (row.Kind == KingdomConstructionInputKind.Water)
				return row.Classification == KingdomConstructionInputRules.WaterClassification
					&& row.Topology == KingdomConstructionInputTopology.LiquidVessel;
			if (row.Topology != KingdomConstructionInputTopology.ContainerInventory
				|| row.ReserveFloor != 0) return false;
			KingdomMaterialDebitSource source;
			return TryDebitSource(row, 0, out source);
		}

		private static bool TryDebitSource(KingdomConstructionInputCandidate row, int sourceId,
			out KingdomMaterialDebitSource source)
		{
			source = null;
			KingdomMaterialDebitCost unit;
			if (row == null || !KingdomMaterialDebitCost.TryParseClaim(row.Classification,
				out unit) || row.Classification != unit.ToClaimString()) return false;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				if (unit.Materials.Get((KingdomMaterial)i) == 1 && unit.Bits.IsEmpty()
					&& unit.Exotics.IsEmpty())
				{
					for (int j = 0; j < KingdomMaterialRules.MaterialCount; j++)
						if (j != i && unit.Materials.Get((KingdomMaterial)j) != 0) return false;
					source = new KingdomMaterialDebitSource(sourceId,
						KingdomMaterialDebitSourceKind.Material, i, row.Count); break;
				}
			if (source == null)
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
				if (unit.Exotics.Get((KingdomExotic)i) == 1 && unit.Materials.IsEmpty()
					&& unit.Bits.IsEmpty())
				{
					for (int j = 0; j < KingdomMaterialRules.ExoticCount; j++)
						if (j != i && unit.Exotics.Get((KingdomExotic)j) != 0) return false;
					source = new KingdomMaterialDebitSource(sourceId,
						KingdomMaterialDebitSourceKind.Exotic, i, row.Count); break;
				}
			if (source == null && unit.Materials.IsEmpty() && unit.Exotics.IsEmpty()
				&& !unit.Bits.IsEmpty()) source = new KingdomMaterialDebitSource(sourceId,
					KingdomMaterialDebitSourceKind.BitStock, 0, row.Count, unit.Bits);
			return source != null && ((row.Kind == KingdomConstructionInputKind.Material
				&& source.Kind == KingdomMaterialDebitSourceKind.Material)
				|| (row.Kind == KingdomConstructionInputKind.Exotic
					&& source.Kind == KingdomMaterialDebitSourceKind.Exotic)
				|| (row.Kind == KingdomConstructionInputKind.Bit
					&& source.Kind == KingdomMaterialDebitSourceKind.BitStock));
		}

		private static string GroupKey(KingdomConstructionInputCandidate row)
		{
			return row.Kind == KingdomConstructionInputKind.Water
				? row.SourceSettlementId + "\0water\0" + row.Classification
				: row.SourceSettlementId + "\0" + row.SourceZoneId + "\0" + row.HolderId
					+ "\0" + (int)row.Kind + "\0" + row.Classification;
		}

		private static string Token(string operationId, int ordinal)
		{
			string value = operationId + "\0" + ordinal.ToString();
			return KingdomConstructionInputRules.HashBytes(
				KingdomConstructionInputRules.StrictUtf8.GetBytes(value)).Substring(0, 32);
		}

		internal static bool Refuse(KingdomConstructionInputPlanFault value,
			out KingdomConstructionInputPlanFault fault)
		{
			fault = value;
			return false;
		}

		private sealed class CandidateGroup
		{
			private readonly int Stock, Prior, Floor;
			private long Count;
			internal CandidateGroup(int stock, int prior, int floor)
			{ Stock = stock; Prior = prior; Floor = floor; }
			internal bool Add(KingdomConstructionInputCandidate row)
			{
				if (row.HolderStockBefore != Stock || row.PriorReserved != Prior
					|| row.ReserveFloor != Floor) return false;
				Count += row.Count; return Count <= int.MaxValue;
			}
			internal bool Valid()
			{ return Prior <= Stock && Floor <= Stock - Prior && Count <= (long)Stock - Prior; }
		}
	}
}
