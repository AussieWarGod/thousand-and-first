using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Qud-facing semantic catalogue adapter. It reads the already-merged population graph but
	/// never asks Qud to roll or generate it: only direct, fixed-count PopulationObject rows are
	/// admitted, then the engine-free rules perform the canonical weighted draw.
	/// </summary>
	internal static partial class KingdomSemanticSelection
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
			if (!TryPrepareGrowthArrivalPayload(system, (ulong)sequence, createdTick, false,
				out plan, out failure))
				return false;
			Cell cell;
			if (!TryLocateGrowthArrival(system, zone, plan.RulesVersion, (ulong)sequence,
				out cell, out failure)) return false;
			plan.X = cell.X; plan.Y = cell.Y;
			return true;
		}

		internal static bool TryPrepareGrowthArrivalPayload(KingdomSystem system,
			ulong ordinal, long dueTick, bool firstGuest, out KingdomSemanticPersonPlan plan,
			out string failure)
		{
			plan = null; failure = null;
			if (ordinal == 0UL || ordinal > (ulong)long.MaxValue || dueTick < 0L)
			{
				failure = "growth arrival semantic identity is absent"; return false;
			}
			if (firstGuest) return TryPrepareGrowthFirstGuestPayload(system, ordinal, dueTick,
				out plan, out failure);
			long sequence = (long)ordinal;
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
			plan.Arrived = XRL.World.Calendar.GetDay(dueTick) + " of "
				+ XRL.World.Calendar.GetMonth(dueTick) + ", "
				+ XRL.World.Calendar.GetYear(dueTick) + " AR";
			return true;
		}

		internal static bool TryLocateGrowthArrival(KingdomSystem system, Zone zone,
			int rulesVersion, ulong ordinal, out Cell cell, out string failure)
		{
			cell = null; failure = null;
			SemanticEventKey key; KernelFaultCode kernelFault;
			if (system == null || ordinal == 0UL || !SemanticEventKey.TryCreate(rulesVersion,
				system.CurrentSettlementId, GrowthArrivalStream, PersonEventKind, ordinal,
				out key, out kernelFault))
			{
				failure = "growth arrival placement identity is absent"; return false;
			}
			return TryProbeArrivalCell(zone, system.SimulationSeed, key, CellDraw,
				out cell, out failure);
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

	}
}
