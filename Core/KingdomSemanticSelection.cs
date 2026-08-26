using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Frozen person payload produced before any dependent object is created.</summary>
	internal sealed class KingdomSemanticPersonPlan
	{
		internal int RulesVersion;
		internal long Sequence;
		internal string StreamId;
		internal uint EventKind;
		internal string Blueprint;
		internal string Origin;
		internal string Creed;
		internal string Name;
		internal string Title;
		internal string Arrived;
		internal int X = -1;
		internal int Y = -1;
	}

	/// <summary>
	/// Qud-facing semantic catalogue adapter. It reads the already-merged population graph but
	/// never asks Qud to roll or generate it: only direct, fixed-count PopulationObject rows are
	/// admitted, then the engine-free rules perform the canonical weighted draw.
	/// </summary>
	internal static class KingdomSemanticSelection
	{
		internal const string NoArrivalGroundFailure = "no passable empty arrival cell exists";
		internal const string GrowthArrivalStream = "taf:semantic:growth-arrival:v1";
		internal const string PlainGuestStream = "taf:semantic:plain-guest:v1";
		internal const string NotableGuestStream = "taf:semantic:notable-guest:v1";
		internal const string CausalPilgrimStream = "taf:semantic:causal-pilgrim:v1";
		internal const string NotableLodgeStream = "taf:semantic:notable-lodge:v1";

		internal const uint PersonEventKind = 1U;
		internal const uint LodgeEventKind = 2U;
		internal const uint FurnishEventKind = 3U;
		internal const uint HookEventKind = 4U;

		private const uint BlueprintDraw = 0U;
		private const uint OriginDraw = 1U;
		private const uint NameDraw = 2U;
		private const uint TitleDraw = 4U;
		private const uint CreedDraw = 5U;
		private const uint CellDraw = 6U;

		internal static bool TryPreparePerson(KingdomSystem system, string table,
			string fallbackBlueprint, string streamId, uint eventKind, long sequence,
			bool allowMerchantTitle, out KingdomSemanticPersonPlan plan, out string failure)
		{
			plan = null;
			failure = null;
			string settlementId = system == null ? null : system.CurrentSettlementId;
			if (system == null || sequence <= 0L || string.IsNullOrEmpty(settlementId))
			{
				failure = "semantic person identity is absent";
				return false;
			}
			List<KingdomSemanticWeightedEntry> catalogue;
			if (!TryLoadSimpleCatalogue(table, fallbackBlueprint, out catalogue, out failure))
				return false;
			string blueprint;
			KingdomSemanticSelectionFault selectionFault;
			if (!KingdomSemanticSelectionRules.TryChoose(system.SimulationSeed,
				KingdomSemanticSelectionRules.RulesVersion, settlementId, streamId, eventKind,
				(ulong)sequence, BlueprintDraw, catalogue, out blueprint, out selectionFault))
			{
				failure = "semantic blueprint draw refused: " + selectionFault;
				return false;
			}
			SemanticEventKey key;
			KernelFaultCode kernelFault;
			if (!SemanticEventKey.TryCreate(KingdomSemanticSelectionRules.RulesVersion,
				settlementId, streamId, eventKind, (ulong)sequence, out key, out kernelFault))
			{
				failure = "semantic person event key refused";
				return false;
			}
			int originIndex;
			if (!KingdomSemanticSelectionRules.TryChooseIndex(system.SimulationSeed, key,
				OriginDraw, KingdomRules.Origins.Length, out originIndex, out selectionFault))
			{
				failure = "semantic origin draw refused: " + selectionFault;
				return false;
			}
			string name;
			if (!KingdomSemanticSelectionRules.TryName(system.SimulationSeed, key, NameDraw,
				out name, out selectionFault))
			{
				failure = "semantic name draw refused: " + selectionFault;
				return false;
			}
			string title = null;
			GameObjectBlueprint objectBlueprint;
			bool merchant = allowMerchantTitle
				&& GameObjectFactory.Factory.Blueprints.TryGetValue(blueprint, out objectBlueprint)
				&& objectBlueprint.HasTag(KingdomGuestbook.LegendaryTraderTag);
			if (merchant && !KingdomSemanticSelectionRules.TryMerchantTitle(
				system.SimulationSeed, key, TitleDraw, out title, out selectionFault))
			{
				failure = "semantic merchant title draw refused: " + selectionFault;
				return false;
			}
			plan = new KingdomSemanticPersonPlan
			{
				RulesVersion = KingdomSemanticSelectionRules.RulesVersion,
				Sequence = sequence,
				StreamId = streamId,
				EventKind = eventKind,
				Blueprint = blueprint,
				Origin = KingdomRules.Origins[originIndex],
				Name = name,
				Title = title
			};
			return true;
		}

		internal static bool TryPrepareGrowthArrival(KingdomSystem system, Zone zone,
			long sequence, long createdTick, out KingdomSemanticPersonPlan plan,
			out string failure)
		{
			if (!TryPreparePerson(system, "r_KingdomSettlers", "r_KingdomSettler",
				GrowthArrivalStream, PersonEventKind, sequence, false, out plan, out failure))
				return false;
			SemanticEventKey key;
			KernelFaultCode kernelFault;
			if (!SemanticEventKey.TryCreate(plan.RulesVersion, system.CurrentSettlementId,
				plan.StreamId, plan.EventKind, (ulong)sequence, out key, out kernelFault))
			{
				failure = "growth arrival event key refused";
				return false;
			}
			if (!KingdomCreed.TryDraw(system, system.SimulationSeed, key, CreedDraw,
				out plan.Creed))
			{
				failure = "growth arrival creed draw refused";
				return false;
			}
			Cell cell;
			if (!TryProbeArrivalCell(zone, system.SimulationSeed, key, CellDraw, out cell,
				out failure)) return false;
			plan.X = cell.X;
			plan.Y = cell.Y;
			plan.Arrived = XRL.World.Calendar.GetDay(createdTick) + " of "
				+ XRL.World.Calendar.GetMonth(createdTick) + ", "
				+ XRL.World.Calendar.GetYear(createdTick) + " AR";
			return true;
		}

		internal static bool TryPrepareGrowthArrivalForFrozenBlueprint(KingdomSystem system,
			Zone zone, long sequence, long createdTick, string blueprint,
			out KingdomSemanticPersonPlan plan, out string failure)
		{
			plan = null;
			failure = null;
			SemanticEventKey key;
			KernelFaultCode kernelFault;
			if (system == null || sequence <= 0L || !ValidBlueprint(blueprint)
				|| !SemanticEventKey.TryCreate(KingdomSemanticSelectionRules.RulesVersion,
					system.CurrentSettlementId, GrowthArrivalStream, PersonEventKind,
					(ulong)sequence, out key, out kernelFault))
			{
				failure = "legacy growth arrival semantic identity is absent";
				return false;
			}
			KingdomSemanticSelectionFault semanticFault;
			int originIndex;
			string name;
			if (!KingdomSemanticSelectionRules.TryChooseIndex(system.SimulationSeed, key,
				OriginDraw, KingdomRules.Origins.Length, out originIndex, out semanticFault)
				|| !KingdomSemanticSelectionRules.TryName(system.SimulationSeed, key, NameDraw,
					out name, out semanticFault))
			{
				failure = "legacy growth arrival person draw refused: " + semanticFault;
				return false;
			}
			plan = new KingdomSemanticPersonPlan
			{
				RulesVersion = KingdomSemanticSelectionRules.RulesVersion,
				Sequence = sequence, StreamId = GrowthArrivalStream,
				EventKind = PersonEventKind, Blueprint = blueprint,
				Origin = KingdomRules.Origins[originIndex], Name = name
			};
			if (!KingdomCreed.TryDraw(system, system.SimulationSeed, key, CreedDraw,
				out plan.Creed))
			{
				failure = "legacy growth arrival creed draw refused";
				return false;
			}
			Cell cell;
			if (!TryProbeArrivalCell(zone, system.SimulationSeed, key, CellDraw, out cell,
				out failure)) return false;
			plan.X = cell.X; plan.Y = cell.Y;
			plan.Arrived = XRL.World.Calendar.GetDay(createdTick) + " of "
				+ XRL.World.Calendar.GetMonth(createdTick) + ", "
				+ XRL.World.Calendar.GetYear(createdTick) + " AR";
			return true;
		}

		internal static bool TryNameOnly(KingdomSystem system, string streamId,
			uint eventKind, long sequence, out string name, out string failure)
		{
			name = null;
			failure = null;
			SemanticEventKey key;
			KernelFaultCode kernelFault;
			if (system == null || sequence <= 0L
				|| !SemanticEventKey.TryCreate(KingdomSemanticSelectionRules.RulesVersion,
					system.CurrentSettlementId, streamId, eventKind, (ulong)sequence,
					out key, out kernelFault))
			{
				failure = "semantic naming identity is absent";
				return false;
			}
			KingdomSemanticSelectionFault semanticFault;
			if (!KingdomSemanticSelectionRules.TryName(system.SimulationSeed, key, NameDraw,
				out name, out semanticFault))
			{
				failure = "semantic name draw refused: " + semanticFault;
				return false;
			}
			return true;
		}

		internal static bool TryChoosePopulationBlueprint(KingdomSystem system, string table,
			string fallbackBlueprint, string streamId, uint eventKind, ulong ordinal,
			uint drawIndex, out string blueprint, out string failure)
		{
			blueprint = null;
			failure = null;
			List<KingdomSemanticWeightedEntry> catalogue;
			if (system == null || string.IsNullOrEmpty(system.CurrentSettlementId)
				|| !TryLoadSimpleCatalogue(table, fallbackBlueprint, out catalogue, out failure))
				return false;
			KingdomSemanticSelectionFault selectionFault;
			if (!KingdomSemanticSelectionRules.TryChoose(system.SimulationSeed,
				KingdomSemanticSelectionRules.RulesVersion, system.CurrentSettlementId,
				streamId, eventKind, ordinal, drawIndex, catalogue, out blueprint,
				out selectionFault))
			{
				failure = "semantic population draw refused: " + selectionFault;
				return false;
			}
			return true;
		}

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
