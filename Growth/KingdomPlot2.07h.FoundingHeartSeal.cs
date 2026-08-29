using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static bool FoundingHeartSealAbsent(Zone Z)
		{
			return string.IsNullOrEmpty(Z?.GetZoneProperty(FoundingHeartSealProperty, null));
		}

		private static bool ExactFoundingHeartSeal(Zone Z, KingdomFoundingHeartPlan Plan)
		{
			string expected = KingdomFoundingHeartRules.CompletionSeal(Plan);
			return expected != null && Z?.GetZoneProperty(FoundingHeartSealProperty, null) == expected
				&& ExactFoundingHeartReceipt(Z, Plan)
				&& ExactFoundingHeartZoneTruth(Z, Plan);
		}

		private static bool SealFoundingHeart(Zone Z, FoundingHeartContext Context)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			string expected = KingdomFoundingHeartRules.CompletionSeal(plan);
			if (Z == null || expected == null || !FoundingHeartSealAbsent(Z)
				|| !ExactFoundingHeartWorld(Z, Context)) return false;
			Z.SetZoneProperty(FoundingHeartSealProperty, expected);
			return ExactFoundingHeartSeal(Z, plan);
		}

		private static bool FoundingHeartWorkIdentityEvidence(GameObject Work)
		{
			if (Work == null) return false;
			if (Work.HasStringProperty(FoundingHeartOwnerProperty)
				|| Work.HasIntProperty(FoundingHeartOwnerProperty)
				|| Work.HasStringProperty(FoundingHeartSlotProperty)
				|| Work.HasIntProperty(FoundingHeartSlotProperty)) return true;
			Zone zone = Work.CurrentZone;
			string raw = zone?.GetZoneProperty(FoundingHeartReceiptProperty, null);
			return Work.GetPart<XRL.World.Parts.r_KingdomPlotWorks>() != null
				&& KingdomFoundingHeartRules.TryDecode(raw, out KingdomFoundingHeartPlan plan)
				&& KingdomFoundingHeartRules.Complete(plan) && plan.ZoneId == zone.ZoneID
				&& ExactFoundingHeartSeal(zone, plan)
				&& (Work.IDIfAssigned == KingdomFoundingHeartRules.SlotId(plan,
					KingdomFoundingHeartRules.WorksSlot)
					|| Work.GetIntProperty(HeartPlotProperty) == 1
					|| Work.GetPart<XRL.World.Parts.r_KingdomPlotWorks>()?.DesignKey == "heartbasin");
		}

		private static bool TryReadFoundingHeartWorkAuthority(Zone Z, GameObject Work,
			out FoundingHeartContext Context)
		{
			Context = null;
			string raw = Z?.GetZoneProperty(FoundingHeartReceiptProperty, null);
			if (!KingdomFoundingHeartRules.TryDecode(raw, out KingdomFoundingHeartPlan plan)
				|| !KingdomFoundingHeartRules.Complete(plan) || plan.ZoneId != Z.ZoneID
				|| !TryReadFoundingHeartContext(Z, plan, out Context)
				|| !ExactFoundingHeartReservations(plan)
				|| !ExactFoundingHeartSeal(Z, plan)
				|| !ExactFoundingHeartFinalCustody(plan)
				|| !ExactFoundingHeartMarkerRoster(Z, plan, false)
				|| !FoundingHeartIdentity(Work, plan, KingdomFoundingHeartRules.WorksSlot)
				|| !ExactFoundingHeartStakeTruth(Work, Context, false)
				|| !ExactFoundingHeartFinalIntent(Z, Context, Work)
				|| Work.CurrentZone != Z
				|| Work.CurrentCell != Z.GetCell(Context.Architecture.MainWorldX,
					Context.Architecture.MainWorldY)
				|| !ExpectedArchitectureReceipt(Work, Work.CurrentCell,
					Context.Stake.BuildKey, Context.Architecture, false)
				|| FindGlobalFoundingHeartId(KingdomFoundingHeartRules.SlotId(plan,
					KingdomFoundingHeartRules.WorksSlot), out GameObject exact, out bool graveyard)
					!= KingdomPhysicalLookupState.Exact
				|| graveyard || !object.ReferenceEquals(exact, Work)
				|| FoundingHeartLoadedReferenceCount(Work) != 1
				|| !ExactFoundingHeartObjectGameState(plan,
					KingdomFoundingHeartRules.WorksSlot, Work, false))
			{
				Context = null;
				return false;
			}
			return true;
		}

		private static bool ExactFoundingHeartFinalIntent(Zone Z, FoundingHeartContext Context,
			GameObject Work)
		{
			string raw = Z?.GetZoneProperty(FoundingHeartTerminalProperty, null);
			if (string.IsNullOrEmpty(raw))
				return FoundingHeartPropertyAbsent(Work, FinalOutputIdProperty)
					&& FindGlobalFoundingHeartId(FoundingHeartFinalId(Context.Plan), out _, out _)
						== KingdomPhysicalLookupState.Absent
					&& The.Game?.ObjectGameState.ContainsKey(
						FoundingHeartFinalRootKey(Context.Plan)) != true;
			return KingdomFoundingHeartTerminalRules.TryDecode(raw, out var terminal)
				&& FoundingHeartTerminalBinding(Context, terminal)
				&& ExactFoundingHeartString(Work, FinalOutputIdProperty, terminal.FinalId);
		}

		private static bool ExactFoundingHeartRetiredAuthority(Zone Z, string PredecessorId,
			out FoundingHeartContext Context)
		{
			Context = null;
			string raw = Z?.GetZoneProperty(FoundingHeartReceiptProperty, null);
			return KingdomFoundingHeartRules.TryDecode(raw, out KingdomFoundingHeartPlan plan)
				&& KingdomFoundingHeartRules.Complete(plan) && plan.ZoneId == Z.ZoneID
				&& KingdomFoundingHeartRules.SlotId(plan,
					KingdomFoundingHeartRules.WorksSlot) == PredecessorId
					&& ExactFoundingHeartSeal(Z, plan)
					&& ExactFoundingHeartReservations(plan)
					&& TryReadFoundingHeartContext(Z, plan, out Context)
					&& ExactFoundingHeartMarkerRoster(Z, plan, false)
					&& ExactFoundingHeartRetiredCustody(plan)
					&& ExactFoundingHeartGraveyardTombstone(plan,
						KingdomFoundingHeartRules.WorksSlot, out _);
		}
	}
}
