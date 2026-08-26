using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSemanticSelection
	{
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

	}
}
