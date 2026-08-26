using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	internal static partial class KingdomBehaviourRules
	{
		internal static bool TryApplyNetworks(KingdomBehaviourState state, string owner,
			KingdomNetworkPlan[] candidates, KingdomCityReading city, long nowTick,
			out KingdomBehaviourState next, out int kept)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; kept = 0;
			if (city == null || nowTick < 0L) return false;
			KingdomResourceReading[] resources = state.Resources();
			KingdomExtensionNetworkReading[] rows = state.Networks();
			int owned = CountOwner(rows, owner);
			List<string> seen = new List<string>();
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxNetworksPerOwner; i++)
			{
				KingdomNetworkPlan plan = candidates[i];
				if (plan == null || plan.NodeCount <= 0
					|| plan.NodeCount > KingdomApiRules.MaxNodesPerNetwork
					|| plan.EdgeCount > KingdomApiRules.MaxEdgesPerNetwork) continue;
				string key = KingdomApiRules.ExtensionKey(owner, plan.Key);
				string resourceKey = KingdomApiRules.ExtensionKey(owner, plan.ResourceKey);
				int resourceAt = ResourceIndex(resources, resourceKey);
				if (key == null || resourceKey == null || resourceAt < 0 || seen.Contains(key)) continue;
				if (!string.IsNullOrEmpty(resources[resourceAt].NetworkKey)
					&& resources[resourceAt].NetworkKey != key) continue;
				seen.Add(key);
				int at = NetworkIndex(rows, key);
				if (at < 0 && (owned >= KingdomApiRules.MaxNetworksPerOwner
					|| rows.Length >= KingdomApiRules.MaxNetworksPerCity)) continue;
				int flow, brownout, supply;
				if (!TrySolve(plan, city, out flow, out brownout, out supply)) continue;
				long from = at < 0 ? nowTick : rows[at].ProcessedThroughTick;
				if (from < 0L || nowTick < from) continue;
				long days = nowTick / TicksPerDay - from / TicksPerDay;
				long surplus = supply - flow;
				if (days > 0L && surplus > 0L)
				{
					if (surplus > long.MaxValue / days) continue;
					long made = surplus * days;
					long room = resources[resourceAt].Room;
					if (made > room) made = room;
					resources[resourceAt] = WithLevel(resources[resourceAt],
						resources[resourceAt].Level + made);
				}
				KingdomExtensionNetworkReading reading = new KingdomExtensionNetworkReading(
					key, resourceKey, nowTick, flow, brownout);
				if (at < 0) { rows = Append(rows, reading); owned++; }
				else rows[at] = reading;
				kept++;
			}
			if (kept > 0) next = new KingdomBehaviourState(resources, state.Jobs(), rows, state.Works());
			return true;
		}

		internal static bool TryApplyWorks(KingdomBehaviourState state, string owner,
			KingdomWorkAdvance[] candidates, KingdomCityReading city, long nowTick,
			out KingdomBehaviourState next, out int kept)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; kept = 0;
			if (city == null || nowTick < 0L) return false;
			KingdomResourceReading[] resources = state.Resources();
			KingdomWorkBehaviourReading[] rows = state.Works();
			int owned = CountOwner(rows, owner);
			List<string> seen = new List<string>();
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxWorkBehavioursPerOwner; i++)
			{
				KingdomWorkAdvance result = candidates[i];
				if (result == null || result.WorkId <= 0 || result.NextTick <= nowTick
					|| result.ChangeCount > KingdomApiRules.MaxChangesPerResult
					|| result.MaterialisationCount > KingdomApiRules.MaxMaterialisationsPerAdvance
					|| !HasWork(city, result.WorkId)) continue;
				string key = KingdomApiRules.ExtensionKey(owner, result.BehaviourKey);
				string rowKey = key == null ? null : key + "#" + result.WorkId;
				if (key == null || seen.Contains(rowKey)) continue;
				seen.Add(rowKey);
				int at = WorkIndex(rows, key, result.WorkId);
				if (at >= 0 && rows[at].NextTick > nowTick) continue;
				if (at < 0 && (owned >= KingdomApiRules.MaxWorkBehavioursPerOwner
					|| rows.Length >= KingdomApiRules.MaxWorkBehavioursPerCity)) continue;
				KingdomResourceChange[] changes = WorkChanges(result);
				KingdomResourceReading[] posted;
				if (!TryApplyOwnedChanges(resources, owner, changes, out posted)) continue;
				string owedBlueprint = at < 0 ? "" : rows[at].OwedBlueprint;
				int owedCount = at < 0 ? 0 : rows[at].OwedCount;
				long materialisationSequence = at < 0 ? 0L : rows[at].MaterialisationSequence;
				if (result.MaterialisationCount > 0)
				{
					KingdomMaterialisation materialisation;
					if (!result.TryMaterialisation(0, out materialisation)) continue;
					string blueprint = KingdomApiRules.BehaviourIdentifier(materialisation.Blueprint, true);
					if (blueprint == null || materialisation.Count <= 0
						|| materialisation.Count > MaxOwedObjectsPerWork
						|| (owedCount > 0 && owedBlueprint != blueprint)
						|| owedCount > MaxOwedObjectsPerWork - materialisation.Count
						|| materialisationSequence == long.MaxValue) continue;
					owedBlueprint = blueprint;
					owedCount += materialisation.Count;
					materialisationSequence++;
				}
				resources = posted;
				KingdomWorkBehaviourReading row = new KingdomWorkBehaviourReading(key, result.WorkId,
					result.NextState, result.NextTick, owedBlueprint, owedCount, materialisationSequence);
				if (at < 0) { rows = Append(rows, row); owned++; }
				else rows[at] = row;
				kept++;
			}
			if (kept > 0) next = new KingdomBehaviourState(resources, state.Jobs(), state.Networks(), rows);
			return true;
		}

		/// <summary>Removes landed physical debt from one exact behaviour/work row. Used only after
		/// the engine edge has successfully placed the exact blueprint.</summary>
		internal static bool TryAcknowledgeMaterialisation(KingdomBehaviourState state,
			string behaviourKey, int workId, string blueprint, int count, out KingdomBehaviourState next)
		{
			state = state ?? KingdomBehaviourState.Empty; next = state;
			if (count <= 0) return false;
			KingdomWorkBehaviourReading[] rows = state.Works();
			int at = WorkIndex(rows, behaviourKey, workId);
			if (at < 0 || rows[at].OwedBlueprint != blueprint || rows[at].OwedCount < count) return false;
			int left = rows[at].OwedCount - count;
			rows[at] = new KingdomWorkBehaviourReading(rows[at].BehaviourKey, workId,
				rows[at].State, rows[at].NextTick, left == 0 ? "" : blueprint, left,
				rows[at].MaterialisationSequence);
			next = new KingdomBehaviourState(state.Resources(), state.Jobs(), state.Networks(), rows);
			return true;
		}

		/// <summary>Canonical exact-ground receipt. Generation separates later output from a stale
		/// marker left after acknowledgement; owed count separates each unit within one generation.</summary>
		internal static string MaterialisationReceipt(KingdomWorkBehaviourReading owed)
		{
			return owed.BehaviourKey + "|" + owed.WorkId.ToString(CultureInfo.InvariantCulture)
				+ "|" + owed.MaterialisationSequence.ToString(CultureInfo.InvariantCulture)
				+ "|" + owed.OwedCount.ToString(CultureInfo.InvariantCulture);
		}

	}
}
