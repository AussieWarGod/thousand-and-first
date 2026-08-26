using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		private static bool TryRecord(LoadState State, string PlanKey,
			ArchitectureBindingDraft Binding, ArchitectureTierDraft Tier,
			HashSet<string> UsedMaps, HashSet<string> UsedPalettes,
			out ResolvedRecord Record)
		{
			Record = null;
			FrozenBuilding building;
			if (!State.Buildings.TryGetValue(Tier.BuildKey, out building))
				return Fault(State, "tier " + Tier.Key,
					"BuildKey " + Tier.BuildKey + " does not exist in the frozen KingdomBuildings view");
			if (!building.HasPlot)
				return Fault(State, "building " + Tier.BuildKey,
					"architecture tier points at a design with no plot");
			if (building.LotSize > Binding.Size)
				return Fault(State, "building " + Tier.BuildKey,
					"authored binding size is smaller than its merged Plot minimum");
			if (building.Category == null
				|| !string.Equals(building.Category, Fold(Binding.TypeKey), StringComparison.Ordinal))
				return Fault(State, "building " + Tier.BuildKey,
					"architecture Type does not match its merged Category");
			if (!ValidBlueprint(building.Blueprint) || !BlueprintExists(building.Blueprint))
				return Fault(State, "building " + Tier.BuildKey,
					"behavior Blueprint is absent from Qud: " + (building.Blueprint ?? "<null>"));

			for (int v = 0; v < Tier.Variants.Count; v++)
			{
				ArchitectureVariantDraft variant = Tier.Variants[v];
				string mapKey = string.IsNullOrEmpty(variant.MapKey) ? Tier.MapKey : variant.MapKey;
				string paletteKey = string.IsNullOrEmpty(variant.PaletteKey)
					? Tier.PaletteKey : variant.PaletteKey;
				UsedMaps.Add(mapKey);
				UsedPalettes.Add(paletteKey);
				ArchitectureMapDraft map;
				ArchitecturePaletteDraft palette;
				if (!State.Maps.TryGetValue(mapKey, out map))
					return Fault(State, "building " + Tier.BuildKey + " variant " + variant.Key,
						"unresolved map " + mapKey);
				if (!State.Palettes.TryGetValue(paletteKey, out palette))
					return Fault(State, "building " + Tier.BuildKey + " variant " + variant.Key,
						"unresolved palette " + paletteKey);
				for (int facing = (int)ArchitectureFacing.North;
					facing <= (int)ArchitectureFacing.West; facing++)
				{
					ArchitectureCompileRequest request = new ArchitectureCompileRequest
					{
						PlanKey = PlanKey, Binding = Binding, Tier = Tier, Variant = variant,
						Map = map, Palette = palette, BuildingBlueprint = building.Blueprint,
						Facing = (ArchitectureFacing)facing
					};
					ArchitectureLayoutSnapshot snapshot;
					string failure;
					if (!KingdomArchitectureRules.TryCompile(request, out snapshot, out failure))
						return Fault(State, "building " + Tier.BuildKey + " variant " + variant.Key,
							((ArchitectureFacing)facing) + " compile failed: " + failure);
				}
			}
			Record = new ResolvedRecord
			{
				Binding = Binding,
				Tier = Tier,
				View = new KingdomArchitectureMapping(building.Blueprint, building.Category,
					PlanKey, Binding, Tier)
			};
			return true;
		}

		private static void IndexRecord(LoadState State, ResolvedRecord Record)
		{
			KingdomArchitectureMapping view = Record.View;
			State.Records.Add(ExactRecordKey(view.BuildKey, Fold(view.TypeKey), view.LotSize), Record);
			List<ResolvedRecord> byBuild;
			if (!State.RecordsByBuild.TryGetValue(view.BuildKey, out byBuild))
			{
				byBuild = new List<ResolvedRecord>();
				State.RecordsByBuild.Add(view.BuildKey, byBuild);
			}
			byBuild.Add(Record);

			string bindingKey = BindingRecordKey(view.PlanKey, view.BindingKey,
				Fold(view.TypeKey), view.LotSize);
			Dictionary<string, ResolvedRecord> byBinding;
			if (!State.RecordsByBinding.TryGetValue(bindingKey, out byBinding))
			{
				byBinding = new Dictionary<string, ResolvedRecord>(StringComparer.Ordinal);
				State.RecordsByBinding.Add(bindingKey, byBinding);
			}
			byBinding.Add(view.BuildKey, Record);
		}

	}
}
