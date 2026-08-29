using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static void WriteGrowthFirstGuestPlan(BinaryWriter w,
			KingdomGrowthFirstGuestOpportunity x)
		{
			if (x == null) throw new InvalidDataException("first-guest plan is absent");
			CanonicalString(w, x.RulesVersion == 1 ? "first-guest-opportunity-v1"
				: "first-guest-opportunity-v2"); w.Write(x.RulesVersion);
			CanonicalString(w, x.OpportunityId); CanonicalString(w, x.CauseId);
			w.Write(x.CauseTick); w.Write(x.OfferedTick); w.Write(x.CadenceTicks);
			w.Write((byte)x.FactsState); w.Write(x.CohortSize);
			w.Write(x.PopulationBefore); w.Write(x.PopulationCap);
			w.Write(x.SupportedLevel); w.Write(x.SupportCap);
			w.Write(x.WaterAvailable); w.Write(x.WaterRequired);
			w.Write((byte)x.ChoiceState); w.Write(x.DeferredTick);
			CanonicalString(w, x.DeferredReceiptId); w.Write(x.DecisionTick);
			CanonicalString(w, x.DecisionReceiptId);
			CanonicalString(w, x.BodyReservationId); CanonicalString(w, x.BodyRealmId);
			w.Write((byte)x.BodyOptionKind); w.Write(x.BodyEnableEpoch);
			w.Write(x.BodyReservedTick);
			// LeaseState is intentionally omitted: exact W0 release may advance it after the
			// physical callback proof has bound this immutable request.
			if (x.RulesVersion >= 2)
			{
				w.Write((byte)x.GuestPhase); w.Write((byte)x.GuestTerminalState);
				w.Write(x.GuestActionTick); CanonicalString(w, x.GuestActionReceiptId);
				w.Write(x.GuestTerminalTick); CanonicalString(w, x.GuestTerminalReceiptId);
			}
		}
	}
}
