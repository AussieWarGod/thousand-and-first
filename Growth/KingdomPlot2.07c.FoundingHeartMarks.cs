using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static bool DriveFoundingHeartMark(Zone Z, FoundingHeartContext Context,
			int Slot)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			if (Z == null || !KingdomFoundingHeartRules.Valid(plan)
				|| Slot < 0 || Slot >= KingdomFoundingHeartRules.WorksSlot
				|| !ExactFoundingHeartMarkerRoster(Z, plan, true)) return false;
			if (plan.States[Slot] == 0)
			{
				GameObject staged;
				if (TryFoundingHeartRoot(plan, Slot, out staged))
				{
					if (!PreparedFoundingHeartMark(staged, plan, Slot)
						&& !ExactFoundingHeartMark(staged, Z, plan, Slot)) return false;
						if (!AdvanceFoundingHeart(Z, Context, Slot, 0, 1)) return false;
				}
				else
				{
					string id = KingdomFoundingHeartRules.SlotId(plan, Slot);
					if (!FoundingHeartRootAbsent(plan, Slot)
						|| FindGlobalFoundingHeartId(id, out _, out _)
							!= KingdomPhysicalLookupState.Absent) return false;
					GameObject created;
					try { created = GameObject.Create(FoundingHeartSlotBlueprint(Slot)); }
					catch { return false; }
					if (!GameObject.Validate(created)) return false;
					created.SetIntProperty(FoundingHeartSlotMark(Slot), 1);
					if (!PreparedFoundingHeartMarkShape(created, Slot)) return false;
					if (!StageFoundingHeartIdentity(created, plan, Slot)
						|| !PreparedFoundingHeartMark(created, plan, Slot)
						|| !RootFoundingHeartOutput(plan, Slot, created)
							|| !AdvanceFoundingHeart(Z, Context, Slot, 0, 1)) return false;
				}
			}
			if (plan.States[Slot] == 1 && !PlaceOrSettleFoundingHeartMark(Z, Context, Slot))
				return false;
			if (plan.States[Slot] != 2) return false;
			GameObject exact;
			if (FindGlobalFoundingHeartId(KingdomFoundingHeartRules.SlotId(plan, Slot),
				out exact, out bool graveyard) != KingdomPhysicalLookupState.Exact
				|| graveyard || !ExactFoundingHeartMark(exact, Z, plan, Slot)) return false;
			return RetireFoundingHeartRoot(plan, Slot, exact)
				&& ExactFoundingHeartMarkerRoster(Z, plan, true);
		}

		private static bool PlaceOrSettleFoundingHeartMark(Zone Z,
			FoundingHeartContext Context, int Slot)
		{
			KingdomFoundingHeartPlan Plan = Context?.Plan;
			if (!TryFoundingHeartRoot(Plan, Slot, out GameObject output)) return false;
			if (ExactFoundingHeartMark(output, Z, Plan, Slot))
				return SettleFoundingHeartMark(Z, Context, Slot, output);
			if (!PreparedFoundingHeartMark(output, Plan, Slot)) return false;
			FoundingHeartSlotGround(Plan, Slot, out int x, out int y);
			Cell cell = Z.GetCell(x, y);
			if (cell == null) return false;
			GameObject accepted = null;
			bool threw = false;
			try { accepted = cell.AddObject(output, NoStack: true); }
			catch { threw = true; }
			finally { KingdomSurvey.ObserveAddResultInActive(Z, output, accepted); }
			return (threw || ReferenceEquals(accepted, output))
				&& ExactFoundingHeartMark(output, Z, Plan, Slot)
				&& SettleFoundingHeartMark(Z, Context, Slot, output);
		}

		private static bool SettleFoundingHeartMark(Zone Z, FoundingHeartContext Context,
			int Slot, GameObject Output)
		{
			KingdomFoundingHeartPlan Plan = Context?.Plan;
			return ExactFoundingHeartMark(Output, Z, Plan, Slot)
				&& AdvanceFoundingHeart(Z, Context, Slot, 1, 2)
				&& RetireFoundingHeartRoot(Plan, Slot, Output);
		}

		private static bool PreparedFoundingHeartMark(GameObject Object,
			KingdomFoundingHeartPlan Plan, int Slot)
		{
			return FoundingHeartIdentity(Object, Plan, Slot)
				&& PreparedFoundingHeartMarkShape(Object, Slot);
		}

		private static bool PreparedFoundingHeartMarkShape(GameObject Object, int Slot)
		{
			return GameObject.Validate(Object)
				&& Object.Blueprint == FoundingHeartSlotBlueprint(Slot)
				&& ExactFoundingHeartInt(Object, FoundingHeartSlotMark(Slot), 1)
				&& FoundingHeartPropertyAbsent(Object, Slot == KingdomFoundingHeartRules.RelicSlot
					? HeartStakeProperty : HeartRelicProperty)
				&& Object.CurrentCell == null && Object.CurrentZone == null
				&& (Object.Physics == null || Object.Physics.InInventory == null)
				&& FoundingHeartLoadedReferenceCount(Object) == 0;
		}

		private static bool ExactFoundingHeartMark(GameObject Object, Zone Z,
			KingdomFoundingHeartPlan Plan, int Slot)
		{
			FoundingHeartSlotGround(Plan, Slot, out int x, out int y);
			Cell cell = Z?.GetCell(x, y);
			if (!FoundingHeartIdentity(Object, Plan, Slot) || cell == null
				|| Object.Blueprint != FoundingHeartSlotBlueprint(Slot)
					|| !ExactFoundingHeartInt(Object, FoundingHeartSlotMark(Slot), 1)
					|| !FoundingHeartPropertyAbsent(Object,
						Slot == KingdomFoundingHeartRules.RelicSlot
							? HeartStakeProperty : HeartRelicProperty)
					|| Object.CurrentZone != Z || Object.CurrentCell != cell
				|| (Object.Physics != null && Object.Physics.InInventory != null)
					|| ReferenceCountInCell(cell, Object) != 1
					|| FoundingHeartLoadedReferenceCount(Object) != 1) return false;
			KingdomPhysicalLookupState state = FindGlobalFoundingHeartId(
				KingdomFoundingHeartRules.SlotId(Plan, Slot), out GameObject global,
				out bool graveyard);
			return state == KingdomPhysicalLookupState.Exact && !graveyard
				&& ReferenceEquals(global, Object);
		}

		private static bool ExactFoundingHeartMarkerRoster(Zone Z,
			KingdomFoundingHeartPlan Plan, bool AllowPending)
		{
			if (Z == null || !KingdomFoundingHeartRules.Valid(Plan)) return false;
			int[] counts = new int[KingdomFoundingHeartRules.WorksSlot];
			foreach (GameObject item in Z.GetObjects())
			{
				if (!GameObject.Validate(item)) continue;
				bool relic = item.GetIntProperty(HeartRelicProperty) == 1;
				bool stake = item.GetIntProperty(HeartStakeProperty) == 1;
				if (!relic && !stake) continue;
				int slot = -1;
				for (int i = 0; i < KingdomFoundingHeartRules.WorksSlot; i++)
					if (item.IDIfAssigned == KingdomFoundingHeartRules.SlotId(Plan, i))
					{
						slot = i;
						break;
					}
				if (slot < 0 || relic != (slot == KingdomFoundingHeartRules.RelicSlot)
					|| stake == relic || !ExactFoundingHeartMark(item, Z, Plan, slot)) return false;
				counts[slot]++;
			}
			for (int slot = 0; slot < KingdomFoundingHeartRules.WorksSlot; slot++)
			{
				int state = Plan.States[slot];
				if (state == 2 && counts[slot] != 1) return false;
				if (state == 0 && counts[slot] != 0) return false;
				if (state == 1 && (!AllowPending || counts[slot] > 1)) return false;
			}
			return true;
		}
	}
}
