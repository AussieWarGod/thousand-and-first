using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSemanticSelection
	{
		/// <summary>First-guest candidates use an exact TAF-owned, one-body catalogue. Merged
		/// population rows remain useful elsewhere but cannot smuggle a group or foreign actor into
		/// Growth-owned citizenship authority.</summary>
		internal static bool TryPrepareGrowthFirstGuest(KingdomSystem system, Zone zone,
			long sequence, long createdTick, out KingdomSemanticPersonPlan plan,
			out string failure)
		{
			if (!TryPrepareGrowthFirstGuestPayload(system, (ulong)sequence, createdTick,
				out plan, out failure)) return false;
			Cell cell;
			if (!TryLocateGrowthArrival(system, zone, plan.RulesVersion, (ulong)sequence,
				out cell, out failure)) return false;
			plan.X = cell.X; plan.Y = cell.Y;
			return true;
		}

		internal static bool TryPrepareGrowthFirstGuestPayload(KingdomSystem system,
			ulong ordinal, long dueTick, out KingdomSemanticPersonPlan plan, out string failure)
		{
			plan = null; failure = null;
			if (system == null || ordinal == 0UL || ordinal > (ulong)long.MaxValue
				|| dueTick < 0L || string.IsNullOrEmpty(
				system.CurrentSettlementId))
			{
				failure = "first-guest semantic identity is absent"; return false;
			}
			List<KingdomSemanticWeightedEntry> catalogue = FirstGuestCatalogue();
			string blueprint; KingdomSemanticSelectionFault semanticFault;
			if (!KingdomSemanticSelectionRules.TryChoose(system.SimulationSeed,
				KingdomSemanticSelectionRules.RulesVersion, system.CurrentSettlementId,
				GrowthArrivalStream, PersonEventKind, ordinal, BlueprintDraw,
				catalogue, out blueprint, out semanticFault)
				|| !KingdomLifecycleRules.GrowthFirstGuestBlueprintAllowed(blueprint))
			{
				failure = "first-guest owned blueprint draw refused: " + semanticFault;
				return false;
			}
			SemanticEventKey key; KernelFaultCode kernelFault;
			if (!SemanticEventKey.TryCreate(KingdomSemanticSelectionRules.RulesVersion,
				system.CurrentSettlementId, GrowthArrivalStream, PersonEventKind,
				ordinal, out key, out kernelFault))
			{
				failure = "first-guest event key refused"; return false;
			}
			int originIndex; string name;
			if (!KingdomSemanticSelectionRules.TryChooseIndex(system.SimulationSeed, key,
				OriginDraw, KingdomRules.Origins.Length, out originIndex, out semanticFault)
				|| !KingdomSemanticSelectionRules.TryName(system.SimulationSeed, key, NameDraw,
					out name, out semanticFault))
			{
				failure = "first-guest person facts refused: " + semanticFault; return false;
			}
			string creed;
			if (!KingdomCreed.TryDraw(system, system.SimulationSeed, key, CreedDraw, out creed))
			{
				failure = "first-guest creed draw refused"; return false;
			}
			plan = new KingdomSemanticPersonPlan
			{
				RulesVersion = KingdomSemanticSelectionRules.RulesVersion,
				Sequence = (long)ordinal, StreamId = GrowthArrivalStream,
				EventKind = PersonEventKind, Blueprint = blueprint,
				Origin = KingdomRules.Origins[originIndex], Creed = creed, Name = name,
				Arrived = Calendar.GetDay(dueTick) + " of " + Calendar.GetMonth(dueTick)
					+ ", " + Calendar.GetYear(dueTick) + " AR"
			};
			return true;
		}

		private static List<KingdomSemanticWeightedEntry> FirstGuestCatalogue()
		{
			return new List<KingdomSemanticWeightedEntry>
			{
				new KingdomSemanticWeightedEntry("r_KingdomSettler", 25UL),
				new KingdomSemanticWeightedEntry("r_KingdomSettlerHand", 20UL),
				new KingdomSemanticWeightedEntry("r_KingdomSettlerDrifter", 14UL),
				new KingdomSemanticWeightedEntry("r_KingdomSettlerTinker", 10UL),
				new KingdomSemanticWeightedEntry("r_KingdomSettlerScribe", 8UL),
				new KingdomSemanticWeightedEntry("r_KingdomSettlerYoung", 8UL),
				new KingdomSemanticWeightedEntry("r_KingdomSettlerPhysicker", 5UL),
				new KingdomSemanticWeightedEntry("r_KingdomSettlerMechanimist", 5UL),
				new KingdomSemanticWeightedEntry("r_KingdomSettlerSnapjaw", 5UL)
			};
		}
	}
}
