using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		// --- Full-merge materialisation -----------------------------------------------------

		private static void Materialise(LoadState State)
		{
			List<string> poseKeys = OrderedKeys(State.RawPoses);
			List<ArchitecturePoseDraft> poseDrafts = new List<ArchitecturePoseDraft>();
			HashSet<string> poisonedPoses = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < poseKeys.Count; i++)
			{
				RawPose raw = State.RawPoses[poseKeys[i]];
				if (TryPose(State, raw, out ArchitecturePoseDraft pose))
					poseDrafts.Add(pose);
				else if (ValidBlueprint(raw.Key) && raw.Key[0] != '$')
					poisonedPoses.Add(raw.Key);
			}
			if (!KingdomArchitectureRules.TryCreatePoseRegistry(poseDrafts, poisonedPoses,
				out State.PoseRegistry, out string poseFailure))
			{
				AddFault(State, "poses", poseFailure);
				poisonedPoses.UnionWith(poseKeys);
				KingdomArchitectureRules.TryCreatePoseRegistry(
					new List<ArchitecturePoseDraft>(), poisonedPoses,
					out State.PoseRegistry, out poseFailure);
			}

			List<string> paletteKeys = OrderedKeys(State.RawPalettes);
			for (int i = 0; i < paletteKeys.Count; i++)
			{
				RawPalette raw = State.RawPalettes[paletteKeys[i]];
				ArchitecturePaletteDraft draft;
				if (TryPalette(State, raw, out draft)) State.Palettes.Add(draft.Key, draft);
			}

			List<string> mapKeys = OrderedKeys(State.RawMaps);
			for (int i = 0; i < mapKeys.Count; i++)
			{
				RawMap raw = State.RawMaps[mapKeys[i]];
				ArchitectureMapDraft draft;
				if (TryMap(State, raw, out draft)) State.Maps.Add(draft.Key, draft);
			}

			List<string> planKeys = OrderedKeys(State.RawPlans);
			for (int i = 0; i < planKeys.Count; i++)
			{
				RawPlan raw = State.RawPlans[planKeys[i]];
				ArchitecturePlanDraft draft;
				if (TryPlan(State, raw, out draft)) State.Plans.Add(draft.Key, draft);
			}

			Dictionary<string, int> exactCounts =
				new Dictionary<string, int>(StringComparer.Ordinal);
			bool mappingOverflow = false;
			int mappingDeclarations = 0;
			List<string> convertedPlans = OrderedKeys(State.Plans);
			for (int p = 0; p < convertedPlans.Count; p++)
			{
				ArchitecturePlanDraft plan = State.Plans[convertedPlans[p]];
				for (int b = 0; b < plan.Bindings.Count; b++)
					for (int t = 0; t < plan.Bindings[b].Tiers.Count; t++)
					{
						mappingDeclarations++;
						if (mappingDeclarations > MaxMappings)
						{
							mappingOverflow = true;
							continue;
						}
						ArchitectureBindingDraft binding = plan.Bindings[b];
						string key = ExactRecordKey(binding.Tiers[t].BuildKey,
							Fold(binding.TypeKey), binding.Size);
						int count;
						exactCounts.TryGetValue(key, out count);
						exactCounts[key] = count + 1;
					}
			}
			if (mappingOverflow)
				AddFault(State, "catalogue", "architecture mapping bound exceeded " + MaxMappings);

			HashSet<string> usedMaps = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> usedPalettes = new HashSet<string>(StringComparer.Ordinal);
			for (int p = 0; p < convertedPlans.Count; p++)
			{
				ArchitecturePlanDraft plan = State.Plans[convertedPlans[p]];
				string failure;
				if (!KingdomArchitectureRules.TryValidatePlan(plan, out failure))
				{
					AddFault(State, "plan " + plan.Key, failure);
					continue;
				}
				for (int b = 0; b < plan.Bindings.Count; b++)
				{
					ArchitectureBindingDraft binding = plan.Bindings[b];
					for (int t = 0; t < binding.Tiers.Count; t++)
					{
						ArchitectureTierDraft tier = binding.Tiers[t];
						if (mappingOverflow) continue;
						string exactKey = ExactRecordKey(tier.BuildKey,
							Fold(binding.TypeKey), binding.Size);
						if (exactCounts[exactKey] != 1)
						{
							AddFault(State, "building " + tier.BuildKey + " typed lot "
								+ Fold(binding.TypeKey) + "/" + binding.Size,
								"BuildKey and typed actual lot are declared by more than one architecture tier");
							continue;
						}
						ResolvedRecord record;
						if (TryRecord(State, plan.Key, binding, tier, usedMaps,
							usedPalettes, out record)) IndexRecord(State, record);
					}
				}
			}

			for (int i = 0; i < mapKeys.Count; i++)
				if (State.Maps.ContainsKey(mapKeys[i]) && !usedMaps.Contains(mapKeys[i]))
					AddFault(State, "map " + mapKeys[i], "map is not resolved by any valid tier variant");
			for (int i = 0; i < paletteKeys.Count; i++)
				if (State.Palettes.ContainsKey(paletteKeys[i]) && !usedPalettes.Contains(paletteKeys[i]))
					AddFault(State, "palette " + paletteKeys[i], "palette is not resolved by any valid tier variant");

			List<string> buildingKeys = OrderedKeys(State.Buildings);
			for (int i = 0; i < buildingKeys.Count; i++)
			{
				FrozenBuilding building = State.Buildings[buildingKeys[i]];
				if (building.HasPlot && !State.RecordsByBuild.ContainsKey(building.Key))
					AddFault(State, "building " + building.Key,
						"plot design has no valid authored architecture mapping");
				if (!building.HasPlot || KingdomPlotRules.HeartRungOf(building.Key) > 0) continue;
				// Plot is the minimum footprint, not authority to synthesize every larger size.
				// The picker already exposes only exact records. Require the minimum mapping so the
				// design remains commissionable; any authored larger records are optional exact offers.
				if (!State.Records.ContainsKey(ExactRecordKey(
					building.Key, Fold(building.Category), building.LotSize)))
					AddFault(State, "building " + building.Key + " typed lot "
						+ Fold(building.Category) + "/" + building.LotSize,
						"declared minimum lot has no exact valid authored architecture mapping");
			}
		}

	}
}
