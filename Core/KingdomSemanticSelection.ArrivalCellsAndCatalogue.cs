using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSemanticSelection
	{
		internal static bool TryProbeArrivalCell(Zone zone, KernelSeed128 seed,
			SemanticEventKey key, uint drawIndex, out Cell chosen, out string failure)
		{
			chosen = null;
			failure = null;
			if (zone == null)
			{
				failure = "arrival zone is absent";
				return false;
			}
			int start;
			KingdomSemanticSelectionFault fault;
			if (!KingdomSemanticSelectionRules.TryProbeStart(seed, key, drawIndex,
				zone.Width, zone.Height, out start, out fault))
			{
				failure = "arrival coordinate draw refused: " + fault;
				return false;
			}
			int count = zone.Width * zone.Height;
			for (int offset = 0; offset < count; offset++)
			{
				int at = KingdomSemanticSelectionRules.ProbeIndex(start, offset, count);
				Cell cell = zone.GetCell(at % zone.Width, at / zone.Width);
				if (cell != null && cell.IsEmpty() && cell.IsPassable()
					&& !cell.HasObjectWithPart("LiquidVolume"))
				{
					chosen = cell;
					return true;
				}
			}
			failure = NoArrivalGroundFailure;
			return false;
		}

		internal static bool TryLoadSimpleCatalogue(string table, string fallbackBlueprint,
			out List<KingdomSemanticWeightedEntry> catalogue, out string failure)
		{
			catalogue = new List<KingdomSemanticWeightedEntry>();
			failure = null;
			if (string.IsNullOrEmpty(table)
				|| table.StartsWith("Dynamic", StringComparison.OrdinalIgnoreCase))
			{
				failure = "semantic population table identity is dynamic or absent";
				return false;
			}
			PopulationInfo population;
			if (!PopulationManager.TryResolvePopulation(table, out population))
			{
				if (ValidBlueprint(fallbackBlueprint))
				{
					catalogue.Add(new KingdomSemanticWeightedEntry(fallbackBlueprint, 1UL));
					return true;
				}
				failure = "semantic population table is missing: " + table;
				return false;
			}
			if (!string.Equals(population.Style, "pickone",
				StringComparison.OrdinalIgnoreCase))
			{
				failure = "semantic population must use Style=pickone: " + table;
				return false;
			}
			if (population.Items == null || population.Items.Count == 0
				|| population.Items.Count > KingdomSemanticSelectionRules.MaxCatalogueEntries)
			{
				failure = "semantic population row count is outside its bound: " + table;
				return false;
			}
			for (int i = 0; i < population.Items.Count; i++)
			{
				PopulationObject row = population.Items[i] as PopulationObject;
				string blueprint = row == null ? null : row.Blueprint;
				if (row == null || !SimpleOne(row.Number) || !string.IsNullOrEmpty(row.Chance)
					|| !string.IsNullOrEmpty(row.Builder) || row.Weight == 0U
					|| string.IsNullOrEmpty(blueprint) || blueprint.IndexOf('{') >= 0
					|| blueprint.IndexOf('}') >= 0
					|| blueprint.StartsWith("$CALL", StringComparison.OrdinalIgnoreCase)
					|| !ValidBlueprint(blueprint))
				{
					failure = "semantic population contains an unsupported dynamic, grouped, "
						+ "conditional, counted, built, or missing object row: " + table;
					catalogue = null;
					return false;
				}
				catalogue.Add(new KingdomSemanticWeightedEntry(blueprint, row.Weight));
			}
			List<KingdomSemanticWeightedEntry> canonical;
			ulong total;
			KingdomSemanticSelectionFault semanticFault;
			if (!KingdomSemanticSelectionRules.TryCanonicalize(catalogue, out canonical,
				out total, out semanticFault))
			{
				failure = "semantic population canonicalization refused: " + semanticFault;
				catalogue = null;
				return false;
			}
			catalogue = canonical;
			return true;
		}

		private static bool SimpleOne(string number)
		{
			return string.IsNullOrEmpty(number) || string.Equals(number, "1",
				StringComparison.Ordinal);
		}

		private static bool ValidBlueprint(string blueprint)
		{
			return !string.IsNullOrEmpty(blueprint) && blueprint.Length <= 256
				&& GameObjectFactory.Factory.HasBlueprint(blueprint);
		}
	}
}
