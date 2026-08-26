using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	/// <summary>Pure host for API-v3 behaviour results: owner qualification, row caps, atomic
	/// resource changes, deterministic itinerary timing, bounded network solve, frozen work state,
	/// and the one canonical durable wire string.</summary>
	internal static partial class KingdomBehaviourRules
	{
		internal const long TicksPerDay = 1200L;
		internal const long MaxResourceQuantity = 1000000000L;
		internal const int MaxOwedObjectsPerWork = 1000;
		private const int WireMagic = 0x33464154; // TAF3, little-endian
		private const int LegacyWireVersion = 1;
		private const int WireVersion = 2;

		internal static KingdomBehaviourReading Reading(KingdomBehaviourState state)
		{
			return (state ?? KingdomBehaviourState.Empty).Reading();
		}

		internal static bool TryApplyResources(KingdomBehaviourState state, string owner,
			KingdomResourceDefinition[] candidates, out KingdomBehaviourState next, out int kept)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; kept = 0;
			KingdomResourceReading[] rows = state.Resources();
			List<string> seen = new List<string>();
			int owned = CountOwner(rows, owner);
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxResourceKindsPerOwner; i++)
			{
				KingdomResourceDefinition candidate = candidates[i];
				string key = KingdomApiRules.ExtensionKey(owner, candidate.Key);
				string unit = KingdomApiRules.BehaviourIdentifier(candidate.Unit, true);
				string property = KingdomApiRules.BehaviourIdentifier(candidate.ContainerProperty, false);
				string liquid = KingdomApiRules.BehaviourIdentifier(candidate.LiquidId, false);
				string network = string.IsNullOrWhiteSpace(candidate.NetworkKey) ? ""
					: KingdomApiRules.ExtensionKey(owner, candidate.NetworkKey);
				if (key == null || unit == null || property == null || liquid == null || network == null
					|| candidate.Capacity < 0L || candidate.Capacity > MaxResourceQuantity
					|| candidate.InitialLevel < 0L || candidate.InitialLevel > candidate.Capacity
					|| seen.Contains(key)) continue;
				seen.Add(key);
				int at = ResourceIndex(rows, key);
				if (at < 0)
				{
					if (owned >= KingdomApiRules.MaxResourceKindsPerOwner
						|| rows.Length >= KingdomApiRules.MaxResourceKindsPerCity) continue;
					rows = Append(rows, new KingdomResourceReading(key, unit, property, network, liquid,
						candidate.InitialLevel, candidate.Capacity));
					owned++;
				}
				else
				{
					long level = rows[at].Level > candidate.Capacity ? candidate.Capacity : rows[at].Level;
					rows[at] = new KingdomResourceReading(key, unit, property, network, liquid,
						level, candidate.Capacity);
				}
				kept++;
			}
			if (kept > 0) next = new KingdomBehaviourState(rows, state.Jobs(), state.Networks(), state.Works());
			return true;
		}

		internal static KingdomCarrierKindRow[] NormalizeCarriers(string owner,
			KingdomCarrierDefinition[] candidates, out int kept)
		{
			List<KingdomCarrierKindRow> rows = new List<KingdomCarrierKindRow>();
			kept = 0;
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxCarrierKindsPerOwner; i++)
			{
				KingdomCarrierDefinition candidate = candidates[i];
				string key = KingdomApiRules.ExtensionKey(owner, candidate.Key);
				string blueprint = KingdomApiRules.BehaviourIdentifier(candidate.Blueprint, true);
				if (key == null || blueprint == null || candidate.WalkTicksPerCell <= 0
					|| candidate.WalkTicksPerCell > 100000 || candidate.Capacity <= 0
					|| candidate.Capacity > 1000000 || CarrierIndex(rows, key) >= 0) continue;
				rows.Add(new KingdomCarrierKindRow(key, blueprint, candidate.WalkTicksPerCell,
					candidate.Capacity));
				kept++;
			}
			return rows.ToArray();
		}

	}
}
