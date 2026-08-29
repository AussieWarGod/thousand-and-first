using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static bool DriveFoundingHeartWorks(KingdomSystem System, Zone Z,
			FoundingHeartContext Context)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			int slot = KingdomFoundingHeartRules.WorksSlot;
			if (System == null || Z == null || !KingdomFoundingHeartRules.Valid(plan)
				|| !ExactFoundingHeartMarkerRoster(Z, plan, false)
				|| !ExactFoundingHeartWorksRoster(Z, Context, true)) return false;
			if (plan.States[slot] == 0)
			{
				if (TryFoundingHeartRoot(plan, slot, out GameObject rooted))
				{
					if (!PreparedFoundingHeartWorks(rooted, Context)
						&& !ExactFoundingHeartWorks(rooted, Z, Context)) return false;
					if (!AdvanceFoundingHeart(Z, Context, slot, 0, 1)) return false;
				}
				else
				{
					if (!FoundingHeartRootAbsent(plan, slot)
						|| FindGlobalFoundingHeartId(KingdomFoundingHeartRules.SlotId(plan, slot),
							out _, out _) != KingdomPhysicalLookupState.Absent) return false;
					FoundingHeartPlacement placement = new FoundingHeartPlacement
						{ Zone = Z, Context = Context, Slot = slot };
					if (!GameObject.Validate(StakeFirstHeartPrepared(System, Z, Context,
						placement))) return false;
				}
			}
			if (plan.States[slot] == 1 && !PlaceOrSettleFoundingHeartWorks(Z, Context))
				return false;
			if (plan.States[slot] != 2) return false;
			GameObject exact;
			if (FindGlobalFoundingHeartId(KingdomFoundingHeartRules.SlotId(plan, slot),
				out exact, out bool graveyard) != KingdomPhysicalLookupState.Exact
				|| graveyard || !ExactFoundingHeartWorks(exact, Z, Context)) return false;
			return RetireFoundingHeartRoot(plan, slot, exact)
				&& ExactFoundingHeartWorksRoster(Z, Context, false);
		}

		private static bool PrepareFoundingHeartWorksAdd(FoundingHeartPlacement Placement,
			GameObject Works)
		{
			FoundingHeartContext context = Placement?.Context;
			KingdomFoundingHeartPlan plan = context?.Plan;
			int slot = Placement == null ? -1 : Placement.Slot;
			if (Placement?.Zone == null || slot != KingdomFoundingHeartRules.WorksSlot
				|| plan?.States == null || plan.States[slot] != 0
				|| !PreparedFoundingHeartWorks(Works, context)
				|| !RootFoundingHeartOutput(plan, slot, Works)) return false;
			return AdvanceFoundingHeart(Placement.Zone, context, slot, 0, 1);
		}

		private static bool SettleFoundingHeartWorksAdd(FoundingHeartPlacement Placement,
			GameObject Works, GameObject Accepted, bool CallbackThrew)
		{
			FoundingHeartContext context = Placement?.Context;
			KingdomFoundingHeartPlan plan = context?.Plan;
			int slot = KingdomFoundingHeartRules.WorksSlot;
			if (Placement?.Zone == null || plan?.States == null
				|| (!CallbackThrew && !ReferenceEquals(Accepted, Works))
				|| !ExactFoundingHeartWorks(Works, Placement.Zone, context)) return false;
			if (plan.States[slot] == 1
				&& !AdvanceFoundingHeart(Placement.Zone, context, slot, 1, 2)) return false;
			return plan.States[slot] == 2 && RetireFoundingHeartRoot(plan, slot, Works);
		}

		private static bool PlaceOrSettleFoundingHeartWorks(Zone Z,
			FoundingHeartContext Context)
		{
			int slot = KingdomFoundingHeartRules.WorksSlot;
			KingdomFoundingHeartPlan plan = Context.Plan;
			if (!TryFoundingHeartRoot(plan, slot, out GameObject output)) return false;
			FoundingHeartPlacement placement = new FoundingHeartPlacement
				{ Zone = Z, Context = Context, Slot = slot };
			if (ExactFoundingHeartWorks(output, Z, Context))
				return SettleFoundingHeartWorksAdd(placement, output, output, true);
			if (!PreparedFoundingHeartWorks(output, Context)) return false;
			Cell cell = Z.GetCell(Context.Architecture.MainWorldX,
				Context.Architecture.MainWorldY);
			if (cell == null) return false;
			GameObject accepted = null;
			bool threw = false;
			try { accepted = cell.AddObject(output); }
			catch { threw = true; }
			finally { KingdomSurvey.ObserveAddResultInActive(Z, output, accepted); }
			return SettleFoundingHeartWorksAdd(placement, output, accepted, threw);
		}

		private static bool PreparedFoundingHeartWorks(GameObject Works,
			FoundingHeartContext Context)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			return FoundingHeartIdentity(Works, plan, KingdomFoundingHeartRules.WorksSlot)
				&& PreparedFoundingHeartWorksShape(Works, Context);
		}

		private static bool PreparedFoundingHeartWorksShape(GameObject Works,
			FoundingHeartContext Context)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			r_KingdomPlotWorks part = GameObject.Validate(Works)
				? Works.GetPart<r_KingdomPlotWorks>() : null;
				if (part == null || Works.CurrentCell != null
					|| Works.CurrentZone != null || Works.Blueprint != WorksBlueprint
					|| (Works.Physics != null && Works.Physics.InInventory != null)
					|| FoundingHeartLoadedReferenceCount(Works) != 0
				|| part.DesignKey != Context.Entry.Key || part.StartTick != plan.StartedTick
				|| part.TotalTicks != plan.TotalTicks || Works.GetIntProperty(HeartPlotProperty) != 1
				|| Works.GetStringProperty(PlotIdProperty) != plan.PlotId
					|| Works.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Context.Entry.Key
					|| !ExactFoundingHeartStakeTruth(Works, Context)
					|| !TryReadRect(Works, out KingdomPlotRules.PlotRect rect)
				|| !SameRect(rect, Context.Rect)
				|| !KingdomArchitectureRuntime.TryRead(Works,
					out KingdomArchitectureIntent frozen, out _)
				|| !SameIntent(frozen, Context.Architecture)) return false;
			return KingdomArchitectureStamper.TryReadOwner(Works, out _, out _,
				out string lotId, out _) && lotId == plan.PlotId;
		}

		private static bool ExactFoundingHeartWorks(GameObject Works, Zone Z,
			FoundingHeartContext Context)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			Cell cell = Z?.GetCell(Context.Architecture.MainWorldX,
				Context.Architecture.MainWorldY);
			if (!FoundingHeartIdentity(Works, plan, KingdomFoundingHeartRules.WorksSlot)
				|| Works.Blueprint != WorksBlueprint || Works.GetIntProperty(HeartPlotProperty) != 1
					|| Works.GetStringProperty(PlotIdProperty) != plan.PlotId
					|| !ExactFoundingHeartStakeTruth(Works, Context)
					|| !ExpectedWorks(Works, cell, Context.Entry.Key, Context.Architecture, false)
					|| ReferenceCountInCell(cell, Works) != 1
					|| FoundingHeartLoadedReferenceCount(Works) != 1) return false;
			r_KingdomPlotWorks part = Works.GetPart<r_KingdomPlotWorks>();
			if (part == null || part.StartTick != plan.StartedTick
				|| part.TotalTicks != plan.TotalTicks) return false;
			KingdomPhysicalLookupState state = FindGlobalFoundingHeartId(
				KingdomFoundingHeartRules.SlotId(plan, KingdomFoundingHeartRules.WorksSlot),
				out GameObject global, out bool graveyard);
			return state == KingdomPhysicalLookupState.Exact && !graveyard
				&& ReferenceEquals(global, Works);
		}

		private static bool ExactFoundingHeartWorksRoster(Zone Z,
			FoundingHeartContext Context, bool AllowPending)
		{
			int count = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				r_KingdomPlotWorks works = item?.GetPart<r_KingdomPlotWorks>();
				if (item == null || (item.GetIntProperty(HeartPlotProperty) != 1
					&& works?.DesignKey != "heartbasin"
					&& item.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != "heartbasin"))
					continue;
				if (!ExactFoundingHeartWorks(item, Z, Context)) return false;
				count++;
			}
			int state = Context.Plan.States[KingdomFoundingHeartRules.WorksSlot];
			return state == 2 ? count == 1 : state == 0 ? count == 0
				: AllowPending && count <= 1;
		}

		private static bool ExactFoundingHeartWorld(Zone Z, FoundingHeartContext Context)
		{
			return KingdomFoundingHeartRules.Complete(Context?.Plan)
					&& ExactFoundingHeartReceipt(Z, Context.Plan)
					&& ExactFoundingHeartZoneTruth(Z, Context.Plan)
					&& ExactFoundingHeartFinalCustody(Context.Plan)
					&& ExactFoundingHeartMarkerRoster(Z, Context.Plan, false)
				&& ExactFoundingHeartWorksRoster(Z, Context, false);
		}

		/// <summary>Read-only whole-envelope proof. No slot may mutate until all six current
		/// custody states agree with the receipt. Only the first open zero slot may carry the
		/// exact durable root left by a cut between root publication and cursor publication.</summary>
		private static bool PreflightFoundingHeartWorld(Zone Z, FoundingHeartContext Context)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			if (Z == null || !KingdomFoundingHeartRules.Valid(plan)
				|| Z.GetZoneProperty(FoundingHeartReceiptProperty, null) != Context.Receipt
				|| !FoundingHeartSealAbsent(Z)
					|| !ExactFoundingHeartOwnedRoster(plan)
					|| !ExactFoundingHeartMarkerRoster(Z, plan, true)
				|| !ExactFoundingHeartWorksRoster(Z, Context, true)) return false;
			bool open = false;
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
			{
				int state = plan.States[slot];
				bool firstOpen = !open && state < 2;
				if (state < 2) open = true;
				KingdomPhysicalLookupState found = FindGlobalFoundingHeartId(
					KingdomFoundingHeartRules.SlotId(plan, slot), out GameObject exact,
					out bool graveyard);
					bool rooted = TryFoundingHeartRoot(plan, slot, out GameObject root);
					bool rootAbsent = FoundingHeartRootAbsent(plan, slot);
					if (!ExactFoundingHeartObjectGameState(plan, slot, exact, rooted)) return false;
				if (state == 0 && found == KingdomPhysicalLookupState.Absent)
				{
					if (!rootAbsent) return false;
					continue;
				}
				if (found != KingdomPhysicalLookupState.Exact || graveyard) return false;
				bool works = slot == KingdomFoundingHeartRules.WorksSlot;
				bool prepared = works ? PreparedFoundingHeartWorks(exact, Context)
					: PreparedFoundingHeartMark(exact, plan, slot);
				bool placed = works ? ExactFoundingHeartWorks(exact, Z, Context)
					: ExactFoundingHeartMark(exact, Z, plan, slot);
				if (state == 2)
				{
					if (!placed || !rootAbsent
						&& (!rooted || !ReferenceEquals(root, exact))) return false;
					continue;
				}
				if (!rooted || !ReferenceEquals(root, exact)) return false;
				if (state == 0)
				{
					if (!firstOpen || !prepared && !placed) return false;
				}
				else if (!prepared && !placed) return false;
			}
			return true;
		}
	}
}
