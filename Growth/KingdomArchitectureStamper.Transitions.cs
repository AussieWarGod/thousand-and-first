using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private static bool TryAuthorizedTransition(GameObject Owner, Zone Z,
			KingdomArchitectureIntent BeforeIntent, ArchitectureLayoutSnapshot Before,
			KingdomArchitectureIntent AfterIntent, ArchitectureLayoutSnapshot After,
			bool AllowPlanChange, out bool HeartAccretion, out string Failure)
		{
			HeartAccretion = false;
			Failure = null;
			if (Owner == null || Z == null || BeforeIntent == null || AfterIntent == null
				|| Before == null || After == null)
				return Fail("authored transition lacks exact frozen endpoints", out Failure);
			int beforeRung = KingdomPlotRules.HeartRungOf(Before.BuildKey);
			int afterRung = KingdomPlotRules.HeartRungOf(After.BuildKey);
			if (beforeRung == 0 && afterRung == 0)
			{
				if (!SameRect(BeforeIntent.Rect, AfterIntent.Rect))
					return TryAuthorizedEnvelopeExpansion(Owner, Z, BeforeIntent, Before,
						AfterIntent, After, out Failure);
				bool samePlan = Before.PlanKey == After.PlanKey;
				bool sameBinding = Before.BindingKey == After.BindingKey;
				bool needsRouteAuthority = !samePlan || !sameBinding;
				bool durableRouteAuthority = needsRouteAuthority && !AllowPlanChange &&
					KingdomSocketTransitions.Authorizes(Owner, BeforeIntent, AfterIntent);
				if (KingdomSocketTransitionRules.AuthorizesFixedLotTransition(samePlan,
					sameBinding, Before.LotType == After.LotType,
					Before.LotSize == After.LotSize,
					SameRect(BeforeIntent.Rect, AfterIntent.Rect),
					Before.Facing == After.Facing,
					BeforeIntent.MainWorldX == AfterIntent.MainWorldX &&
						BeforeIntent.MainWorldY == AfterIntent.MainWorldY,
					ValidLotId(Owner.GetStringProperty(LotIdProperty)), AllowPlanChange,
					durableRouteAuthority)) return true;
				return Fail("authored fixed-lot transition changes frozen identity without " +
					"an explicit same-set authority", out Failure);
			}

			KingdomPlotRules.PlotRect expectedBefore;
			KingdomPlotRules.PlotRect expectedAfter;
			if (beforeRung < 1 || afterRung != beforeRung + 1
				|| Before.PlanKey != "civic-heart" || After.PlanKey != "civic-heart"
				|| Before.LotType != "civic" || After.LotType != "civic"
				|| Before.Facing != After.Facing
				|| BeforeIntent.MainWorldX != AfterIntent.MainWorldX
				|| BeforeIntent.MainWorldY != AfterIntent.MainWorldY
				|| (int)Before.LotSize != beforeRung || (int)After.LotSize != afterRung
				|| Owner.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1
				|| KingdomPlots.HeartRung(Z) != beforeRung
				|| !KingdomPlots.TryHeartRectFor(Z, beforeRung, out expectedBefore)
				|| !KingdomPlots.TryHeartRectFor(Z, afterRung, out expectedAfter)
				|| !SameRect(BeforeIntent.Rect, expectedBefore)
				|| !SameRect(AfterIntent.Rect, expectedAfter)
				|| Owner.GetStringProperty(KingdomPlots.PlotIdProperty)
					!= Owner.GetStringProperty(LotIdProperty)
				|| !TryExactHeartBasin(Owner, Z, BeforeIntent, Before, out Failure)
				|| !TryHeartSnapshotBasin(AfterIntent, After, Z, out Failure))
				return Failure != null ? false : Fail(
					"cross-size authored transition is not adjacent founding-heart accretion",
					out Failure);
			HeartAccretion = true;
			return true;
		}

		private static bool TryExactHeartBasin(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Failure = null;
			ArchitecturePlacement basin;
			if (!TryHeartBasinPlacement(Intent, Snapshot, Z, out basin, out Failure)) return false;
			string lot = Owner.GetStringProperty(LotIdProperty);
			GameObject exact;
			return TryExactOutput(Owner, Z, Intent, lot, basin, out exact, out Failure)
				&& exact.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1;
		}

		private static bool TryHeartSnapshotBasin(KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, Zone Z, out string Failure)
		{
			ArchitecturePlacement ignored;
			return TryHeartBasinPlacement(Intent, Snapshot, Z, out ignored, out Failure);
		}

		private static bool TryHeartBasinPlacement(KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, Zone Z, out ArchitecturePlacement Basin,
			out string Failure)
		{
			Basin = null;
			Failure = null;
			int riteX;
			int riteY;
			if (!KingdomPlots.TryRiteGround(Z, out riteX, out riteY))
				return Fail("founding-heart transition has no recorded rite", out Failure);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (!placement.ExistingAuthority) continue;
				if (Basin != null || placement.Blueprint != KingdomPlots.HeartRelicBlueprint
					|| placement.StatefulAnchor != "fixture:first-basin")
					return Fail("founding-heart snapshot has malformed existing authority", out Failure);
				Basin = placement;
			}
			int x;
			int y;
			if (Basin == null || !KingdomArchitectureRuntime.TryWorldPlacement(Snapshot,
				Intent.Rect, Basin, out x, out y, out Failure))
				return Failure != null ? false : Fail(
					"founding-heart snapshot has no immutable basin", out Failure);
			if (x != riteX || y != riteY)
				return Fail("founding-heart snapshot moves the immutable basin", out Failure);
			return true;
		}

	}
}
