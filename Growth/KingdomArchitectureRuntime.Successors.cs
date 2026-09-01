using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		public static bool TryPrepareSuccessor(KingdomSystem System, Zone Z,
			KingdomArchitectureIntent Before, string SuccessorBuildKey,
			out KingdomArchitectureIntent Intent, out string Failure)
		{
			Intent = null;
			Failure = null;
			ArchitectureLayoutSnapshot before;
			if (!TryValidateIntent(Before, out before, out Failure)) return false;
			if (!KingdomArchitectureRules.IsLatestSnapshotEncoding(Before.EncodedSnapshot))
				return Fail("legacy architecture has no authored in-place tier transition", out Failure);
			if (System == null || !System.Founded || Z == null || !ValidRectInZone(Before.Rect, Z))
				return Fail("authored successor needs its founded settlement and exact loaded lot",
					out Failure);
			ArchitectureSelectionContext context;
			if (!TrySelectionContext(System, Z, out context, out Failure)) return false;
			ArchitectureLayoutSnapshot after;
			if (!KingdomArchitecture.TryResolveSuccessor(before.BuildKey, before.VariantKey,
				SuccessorBuildKey, before.PlanKey,
				before.BindingKey, before.LotType, before.LotSize, context, before.Facing,
				out after, out Failure)) return false;
			ArchitectureLayoutDelta delta;
			if (!KingdomArchitectureRules.TryBuildDelta(before, after,
				after.IncomingTransitionMode, out delta, out Failure))
				return false;
			KingdomPlotRules.PlotRect successorRect = Before.Rect;
			int beforeRung = KingdomPlotRules.HeartRungOf(before.BuildKey);
			int afterRung = KingdomPlotRules.HeartRungOf(after.BuildKey);
			bool heartAccretion = beforeRung > 0 && afterRung == beforeRung + 1
				&& before.PlanKey == "civic-heart" && after.PlanKey == "civic-heart";
			if (heartAccretion)
			{
				KingdomPlotRules.PlotRect standingRect;
				if (KingdomPlots.HeartRung(Z) != beforeRung
					|| !KingdomPlots.TryHeartRectFor(Z, beforeRung, out standingRect)
					|| !SameRect(Before.Rect, standingRect)
					|| !KingdomPlots.TryHeartRectFor(Z, afterRung, out successorRect)
					|| !ValidRectInZone(successorRect, Z))
					return Fail("founding-heart successor does not accrete from its exact standing rung",
						out Failure);
				if (!TryHeartBasinInvariant(before, Before.Rect, Z, out Failure)
					|| !TryHeartBasinInvariant(after, successorRect, Z, out Failure)) return false;
			}
			string encoded;
			string hash;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(after, out encoded, out Failure)
				|| !KingdomArchitectureRules.TrySnapshotHash(after, out hash, out Failure)) return false;
			int mainX;
			int mainY;
			if (!TryWorldCoordinate(after, successorRect, after.MainX, after.MainY,
				out mainX, out mainY, out Failure)) return false;
			if (mainX != Before.MainWorldX || mainY != Before.MainWorldY)
				return Fail("authored successor moves the frozen main behavior root", out Failure);
			KingdomArchitectureIntent prepared = KingdomArchitectureIntent.Create(after, encoded,
				hash, successorRect, mainX, mainY);
			ArchitectureLayoutSnapshot checkedSnapshot;
			if (!TryValidateIntent(prepared, out checkedSnapshot, out Failure)) return false;
			Intent = prepared;
			return true;
		}

		/// <summary>
		/// Resolves one explicitly declared same-set plan change. Unlike a tier successor this may
		/// cross plan and binding keys, but it may not change typed lot, rectangle, pose, or main
		/// behavior-root cell. Declaration is checked before debit; its endpoint hashes are then
		/// frozen on the predecessor so retries never consult a mutable catalogue.
		/// </summary>
		public static bool TryPreparePlanTransition(KingdomSystem System, Zone Z,
			KingdomArchitectureIntent Before, string SuccessorBuildKey,
			KingdomSocketTransition Transition, out KingdomArchitectureIntent Intent,
			out string Failure)
		{
			Intent = null;
			Failure = null;
			ArchitectureLayoutSnapshot before;
			if (!TryValidateIntent(Before, out before, out Failure)
				|| !KingdomArchitectureRules.IsLatestSnapshotEncoding(Before.EncodedSnapshot))
				return Failure != null ? false : Fail(
					"legacy architecture has no authored same-set transition", out Failure);
			if (System == null || !System.Founded || Z == null ||
				!KingdomSocketTransitions.TryResolveCurrent(Transition, before.BuildKey,
					SuccessorBuildKey, before.LotType, before.LotSize,
					out KingdomSocketTransition declared) || !ValidRectInZone(Before.Rect, Z))
				return Fail("same-set transition declaration does not match the standing typed lot",
					out Failure);
			ArchitectureSelectionContext context;
			ArchitectureLayoutSnapshot after;
			if (!TrySelectionContext(System, Z, out context, out Failure)
				|| !KingdomArchitecture.TryResolve(SuccessorBuildKey, before.LotType,
					before.LotSize, context, before.Facing, out after, out Failure)) return false;
			if (after.LotType != before.LotType || after.LotSize != before.LotSize
				|| after.Facing != before.Facing)
				return Fail("same-set transition changes the frozen lot binding or pose", out Failure);
			// A socket route owns its incoming edge. Freeze that mode into the target snapshot so
			// paid retries cannot reinterpret the physical work through later catalogue changes.
			after.IncomingTransitionMode = declared.Mode;
			ArchitectureLayoutDelta delta;
			if (!KingdomArchitectureRules.TryBuildDelta(before, after, declared.Mode,
				out delta, out Failure))
				return false;
			string encoded;
			string hash;
			int mainX;
			int mainY;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(after, out encoded, out Failure)
				|| !KingdomArchitectureRules.TrySnapshotHash(after, out hash, out Failure)
				|| !TryWorldCoordinate(after, Before.Rect, after.MainX, after.MainY,
					out mainX, out mainY, out Failure)) return false;
			if (mainX != Before.MainWorldX || mainY != Before.MainWorldY)
				return Fail("same-set transition moves the frozen main behavior root", out Failure);
			KingdomArchitectureIntent prepared = KingdomArchitectureIntent.Create(after, encoded,
				hash, Before.Rect, mainX, mainY);
			ArchitectureLayoutSnapshot checkedSnapshot;
			if (!TryValidateIntent(prepared, out checkedSnapshot, out Failure)) return false;
			Intent = prepared;
			return true;
		}
	}
}
