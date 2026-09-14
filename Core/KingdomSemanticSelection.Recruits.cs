using ThousandAndFirst.Simulation.Kernel;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSemanticSelection
	{
		private static bool TryPrepareSettlerPayload(KingdomSystem System, ulong Ordinal,
			long DueTick, bool FirstGuest, out KingdomSemanticPersonPlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (System == null || Ordinal == 0UL || Ordinal > (ulong)long.MaxValue || DueTick < 0L
				|| !SemanticEventKey.TryCreate(KingdomSemanticSelectionRules.RulesVersion,
					System.CurrentSettlementId, GrowthArrivalStream, PersonEventKind, Ordinal,
					out var key, out _))
			{
				Failure = "settler semantic identity is absent"; return false;
			}
			if (!KingdomRecruitment.TryChoose(System, key, Ordinal, FirstGuest,
				out string blueprint, out string origin, out string name, out Failure)) return false;
			if (!KingdomCreed.TryDraw(System, System.SimulationSeed, key, CreedDraw, out string creed))
			{
				Failure = "settler creed draw refused"; return false;
			}
			Plan = new KingdomSemanticPersonPlan
			{
				RulesVersion = KingdomSemanticSelectionRules.RulesVersion, Sequence = (long)Ordinal,
				StreamId = GrowthArrivalStream, EventKind = PersonEventKind, Blueprint = blueprint,
				Origin = origin, Name = name, Creed = creed,
				Arrived = Calendar.GetDay(DueTick) + " of " + Calendar.GetMonth(DueTick)
					+ ", " + Calendar.GetYear(DueTick) + " AR"
			};
			return true;
		}
	}
}
