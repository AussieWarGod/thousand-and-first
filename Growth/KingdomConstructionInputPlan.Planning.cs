using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputPlanRules
	{
		public static bool TryPlan(string OperationId, int WaterRequested,
			string MaterialRequestedClaim, string RequiredObjectId,
			IList<KingdomConstructionInputCandidate> Candidates,
			out KingdomConstructionInputPlan Plan,
			out KingdomConstructionInputPlanFault Fault)
		{
			return TryPlanWithRequiredObjects(OperationId, WaterRequested, MaterialRequestedClaim,
				string.IsNullOrEmpty(RequiredObjectId) ? new string[0]
					: new[] { RequiredObjectId }, Candidates, out Plan, out Fault);
		}

		public static bool TryPlanWithRequiredObjects(string OperationId, int WaterRequested,
			string MaterialRequestedClaim, IList<string> RequiredObjectIds,
			IList<KingdomConstructionInputCandidate> Candidates,
			out KingdomConstructionInputPlan Plan,
			out KingdomConstructionInputPlanFault Fault)
		{
			Plan = null;
			string[] required;
			if (!KingdomConstructionInputRules.TryRequiredObjectIds(
				RequiredObjectIds, out required))
				return Refuse(KingdomConstructionInputPlanFault.RequiredObject, out Fault);
			KingdomMaterialDebitCost requested;
			if (WaterRequested < 0 || !KingdomMaterialDebitCost.TryParseClaim(
				MaterialRequestedClaim, out requested)
				|| MaterialRequestedClaim != requested.ToClaimString())
				return Refuse(KingdomConstructionInputPlanFault.Claim, out Fault);
			List<KingdomConstructionInputCandidate> ordered;
			if (!TryOrderedCandidates(OperationId, required, Candidates,
				out ordered, out Fault)) return false;

			Dictionary<int, int> materialTake;
			if (!TryMaterialSelection(requested, required, ordered,
				out materialTake, out Fault)) return false;
			Dictionary<KingdomConstructionInputCandidate, List<int>> waterTake;
			int dailyWater;
			if (!TryWaterSelection(WaterRequested, ordered, out waterTake,
				out dailyWater, out Fault)) return false;

			List<KingdomConstructionInputPlannedLine> lines =
				new List<KingdomConstructionInputPlannedLine>();
			for (int i = 0; i < ordered.Count; i++)
			{
				KingdomConstructionInputCandidate candidate = ordered[i];
				int take;
				if (candidate.Kind != KingdomConstructionInputKind.Water
					&& materialTake.TryGetValue(i, out take))
				{
					if (candidate.AlwaysStack && take < candidate.Count)
						return Refuse(KingdomConstructionInputPlanFault.UnsafeStack, out Fault);
					AddLine(lines, candidate, candidate.Count, take, OperationId);
				}
				List<int> water;
				if (candidate.Kind == KingdomConstructionInputKind.Water
					&& waterTake.TryGetValue(candidate, out water))
				{
					int before = candidate.Count;
					for (int j = 0; j < water.Count; j++)
					{
						AddLine(lines, candidate, before, water[j], OperationId);
						before -= water[j];
					}
				}
			}
			if (lines.Count < 1 || lines.Count > KingdomConstructionInputRules.MaxSourceLines)
				return Refuse(KingdomConstructionInputPlanFault.Bounds, out Fault);
			List<KingdomConstructionInputPlannedChild> children;
			if (!TryPackChildren(lines, out children, out Fault)) return false;
			Plan = new KingdomConstructionInputPlan(OperationId, WaterRequested,
				MaterialRequestedClaim, required, dailyWater, lines.ToArray(),
				children.ToArray());
			Fault = KingdomConstructionInputPlanFault.None;
			return true;
		}

		private static bool TryMaterialSelection(KingdomMaterialDebitCost requested,
			string[] requiredObjectIds, List<KingdomConstructionInputCandidate> ordered,
			out Dictionary<int, int> selected, out KingdomConstructionInputPlanFault fault)
		{
			selected = new Dictionary<int, int>();
			List<KingdomMaterialDebitSource> sources = new List<KingdomMaterialDebitSource>();
			Dictionary<string, int> required = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < requiredObjectIds.Length; i++) required.Add(requiredObjectIds[i], -1);
			for (int i = 0; i < ordered.Count; i++)
			{
				KingdomConstructionInputCandidate candidate = ordered[i];
				if (required.ContainsKey(candidate.SourceObjectId))
					required[candidate.SourceObjectId] = i;
				if (candidate.Kind == KingdomConstructionInputKind.Water) continue;
				KingdomMaterialDebitSource source;
				if (!TryDebitSource(candidate, i, out source))
					return Refuse(KingdomConstructionInputPlanFault.Claim, out fault);
				sources.Add(source);
			}
			for (int i = 0; i < requiredObjectIds.Length; i++)
			{
				int index = required[requiredObjectIds[i]];
				if (index < 0 || ordered[index].Kind != KingdomConstructionInputKind.Material
					|| ordered[index].Count != 1)
					return Refuse(KingdomConstructionInputPlanFault.RequiredObject, out fault);
			}
			KingdomMaterialDebitPlan materialPlan;
			KingdomMaterialDebitFault materialFault;
			if (!KingdomMaterialDebitRules.TryPlan(requested, sources,
				out materialPlan, out materialFault))
				return Refuse(KingdomConstructionInputPlanFault.InsufficientMaterial, out fault);
			for (int i = 0; i < materialPlan.Steps.Count; i++)
			{
				KingdomMaterialDebitStep step = materialPlan.Steps[i];
				if (selected.ContainsKey(step.Source))
					return Refuse(KingdomConstructionInputPlanFault.Duplicate, out fault);
				selected.Add(step.Source, step.Taken);
			}
			for (int i = 0; i < requiredObjectIds.Length; i++)
			{
				int requiredTake;
				int index = required[requiredObjectIds[i]];
				if (!selected.TryGetValue(index, out requiredTake) || requiredTake != 1)
					return Refuse(KingdomConstructionInputPlanFault.RequiredObject, out fault);
			}
			fault = KingdomConstructionInputPlanFault.None;
			return true;
		}

		private static bool TryWaterSelection(int requested,
			List<KingdomConstructionInputCandidate> ordered,
			out Dictionary<KingdomConstructionInputCandidate, List<int>> selected,
			out int dailyWater, out KingdomConstructionInputPlanFault fault)
		{
			selected = new Dictionary<KingdomConstructionInputCandidate, List<int>>();
			dailyWater = 0;
			Dictionary<string, int> available = new Dictionary<string, int>(StringComparer.Ordinal);
			Dictionary<string, int> floors = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < ordered.Count; i++)
			{
				KingdomConstructionInputCandidate row = ordered[i];
				if (row.Kind != KingdomConstructionInputKind.Water
					|| available.ContainsKey(row.SourceSettlementId)) continue;
				available.Add(row.SourceSettlementId,
					row.HolderStockBefore - row.PriorReserved - row.ReserveFloor);
				floors.Add(row.SourceSettlementId, row.ReserveFloor);
			}
			int remaining = requested;
			HashSet<string> usedSettlements = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < ordered.Count && remaining > 0; i++)
			{
				KingdomConstructionInputCandidate row = ordered[i];
				if (row.Kind != KingdomConstructionInputKind.Water) continue;
				int group = available[row.SourceSettlementId];
				int take = Math.Min(row.Count, Math.Min(group, remaining));
				if (take <= 0) continue;
				List<int> legs = new List<int>();
				int left = take;
				while (left > 0)
				{
					int leg = Math.Min(left, KingdomConstructionInputRules.WaterCargoCapacity);
					legs.Add(leg); left -= leg;
				}
				selected.Add(row, legs);
				available[row.SourceSettlementId] = group - take;
				remaining -= take;
				usedSettlements.Add(row.SourceSettlementId);
			}
			if (remaining != 0)
				return Refuse(KingdomConstructionInputPlanFault.InsufficientWater, out fault);
			long daily = 0L;
			foreach (string settlement in usedSettlements)
			{
				int floor = floors[settlement];
				if (floor % KingdomConstructionInputRules.WaterReserveDays != 0)
					return Refuse(KingdomConstructionInputPlanFault.Claim, out fault);
				daily += floor / KingdomConstructionInputRules.WaterReserveDays;
			}
			if (daily > int.MaxValue)
				return Refuse(KingdomConstructionInputPlanFault.Bounds, out fault);
			dailyWater = (int)daily;
			fault = KingdomConstructionInputPlanFault.None;
			return true;
		}

		private static void AddLine(List<KingdomConstructionInputPlannedLine> lines,
			KingdomConstructionInputCandidate candidate, int before, int take,
			string operationId)
		{
			int ordinal = lines.Count;
			lines.Add(new KingdomConstructionInputPlannedLine(candidate, ordinal,
				before, take, Token(operationId, ordinal)));
		}

		private static bool TryPackChildren(List<KingdomConstructionInputPlannedLine> lines,
			out List<KingdomConstructionInputPlannedChild> children,
			out KingdomConstructionInputPlanFault fault)
		{
			children = new List<KingdomConstructionInputPlannedChild>();
			int start = 0;
			while (start < lines.Count)
			{
				KingdomConstructionInputCandidate source = lines[start].Candidate;
				int count = 1;
				while (start + count < lines.Count
					&& count < KingdomConstructionInputRules.MaxCargoPerChild
					&& SameEndpoint(source, lines[start + count].Candidate)) count++;
				children.Add(new KingdomConstructionInputPlannedChild(children.Count,
					start, count, source));
				start += count;
			}
			if (children.Count < 1 || children.Count > KingdomConstructionInputRules.MaxChildren)
				return Refuse(KingdomConstructionInputPlanFault.Child, out fault);
			fault = KingdomConstructionInputPlanFault.None;
			return true;
		}

		private static bool SameEndpoint(KingdomConstructionInputCandidate left,
			KingdomConstructionInputCandidate right)
		{
			return left.SourceZoneId == right.SourceZoneId && left.HolderId == right.HolderId
				&& left.X == right.X && left.Y == right.Y;
		}
	}
}
