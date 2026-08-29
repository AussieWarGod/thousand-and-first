using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static bool ExactFoundingHeartStakeTruth(GameObject Works,
			FoundingHeartContext Context, bool RequireStaked = true)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			KingdomFoundingHeartStakeTruth truth = Context?.Stake;
			r_KingdomPlotWorks part = GameObject.Validate(Works)
				? Works.GetPart<r_KingdomPlotWorks>() : null;
			if (!KingdomFoundingHeartRules.Valid(plan)
				|| !KingdomFoundingHeartStakeRules.Valid(truth) || part == null
				|| part.DesignKey != truth.BuildKey || part.DisplayName != truth.DisplayName
				|| part.X1 != plan.RectX1 || part.Y1 != plan.RectY1
				|| part.X2 != plan.RectX2 || part.Y2 != plan.RectY2
				|| part.StartTick != plan.StartedTick || part.TotalTicks != plan.TotalTicks
				|| part.StageApplied < (int)KingdomPlotRules.PlotStage.Staked
				|| part.StageApplied > (int)KingdomPlotRules.PlotStage.Done
				|| RequireStaked && part.StageApplied != (int)KingdomPlotRules.PlotStage.Staked
				|| part.Open != truth.Open || part.Carved != truth.Carved
				|| part.WallBlueprint != truth.WallBlueprint
				|| part.ContentsTable != truth.Contents || part.StaffNeeded != truth.Staff
				|| part.ThresholdManning != truth.ThresholdManning
				|| part.DefencePending != truth.Defence || part.HasDoor != truth.HasDoor
				|| part.DoorX != truth.DoorX || part.DoorY != truth.DoorY
				|| Works.DisplayName != "plot: " + truth.DisplayName) return false;
			return ExactFoundingHeartInt(Works, HeartPlotProperty, 1)
				&& ExactFoundingHeartString(Works, PlotIdProperty, plan.PlotId)
				&& ExactFoundingHeartString(Works, KingdomUpgrade.BuildKeyProperty, truth.BuildKey)
				&& ExactFoundingHeartInt(Works, PlotX1Property, plan.RectX1)
				&& ExactFoundingHeartInt(Works, PlotY1Property, plan.RectY1)
				&& ExactFoundingHeartInt(Works, PlotX2Property, plan.RectX2)
				&& ExactFoundingHeartInt(Works, PlotY2Property, plan.RectY2)
				&& ExactFoundingHeartInt(Works, FootX1Property, truth.FootprintX1)
				&& ExactFoundingHeartInt(Works, FootY1Property, truth.FootprintY1)
				&& ExactFoundingHeartInt(Works, FootX2Property, truth.FootprintX2)
				&& ExactFoundingHeartInt(Works, FootY2Property, truth.FootprintY2)
				&& ExactFoundingHeartInt(Works, PlotRoofProperty, truth.Roof)
				&& ExactFoundingHeartPurpose(Works, truth.PurposeLegacy)
				&& FoundingHeartPropertyAbsent(Works, KingdomConstruction.ReceiptProperty)
				&& FoundingHeartWorkSchemaAbsent(Works)
				&& FoundingHeartSkinAbsent(Works);
		}

		private static bool ExactFoundingHeartPurpose(GameObject Works, bool Legacy)
		{
			if (Works.HasStringProperty(KingdomPurpose.CommitmentProperty)
				|| Works.HasIntProperty(KingdomPurpose.CommitmentProperty)) return false;
			return Legacy
				? ExactFoundingHeartInt(Works, KingdomPurpose.CommitmentLegacyProperty, 1)
				: FoundingHeartPropertyAbsent(Works, KingdomPurpose.CommitmentLegacyProperty);
		}

		private static bool FoundingHeartWorkSchemaAbsent(GameObject Works)
		{
			return FoundingHeartPropertyAbsent(Works, PlotWorkSchemaProperty)
				&& FoundingHeartPropertyAbsent(Works, PlotWorkRequiredProperty)
				&& FoundingHeartPropertyAbsent(Works, PlotWorkRemainingProperty)
				&& FoundingHeartPropertyAbsent(Works, PlotWorkLastTickProperty);
		}

		private static bool FoundingHeartSkinAbsent(GameObject Works)
		{
			return FoundingHeartPropertyAbsent(Works, KingdomDesign.StagedColorStringProperty)
				&& FoundingHeartPropertyAbsent(Works, KingdomDesign.StagedDetailColorProperty)
				&& FoundingHeartPropertyAbsent(Works, KingdomDesign.StagedRenderStringProperty)
				&& FoundingHeartPropertyAbsent(Works, KingdomDesign.StagedTileProperty);
		}

		private static bool ExactFoundingHeartFinalTruth(GameObject Building,
			KingdomFoundingHeartStakeTruth Truth)
		{
			if (!ExactFoundingHeartFinalShape(Building, Truth)) return false;
			return FindGlobalFoundingHeartId(Building.IDIfAssigned, out GameObject exact,
				out bool graveyard) == KingdomPhysicalLookupState.Exact
				&& !graveyard && object.ReferenceEquals(exact, Building)
				&& FoundingHeartLoadedReferenceCount(Building) == 1;
		}

		private static bool ExactFoundingHeartFinalShape(GameObject Building,
			KingdomFoundingHeartStakeTruth Truth)
		{
			if (!GameObject.Validate(Building) || !KingdomFoundingHeartStakeRules.Valid(Truth)
				|| Building.Blueprint != Truth.Blueprint) return false;
			return ExactFoundingHeartInt(Building, "KingdomBuilt", 1)
				&& ExactFoundingHeartString(Building,
					r_KingdomScaffold.CompletionNameProperty, "plot: " + Truth.DisplayName)
				&& ExactPositiveFoundingHeartInt(Building, "KingdomDefence", Truth.Defence)
				&& ExactPositiveFoundingHeartInt(Building, "KingdomStaffNeeded", Truth.Staff)
				&& (Truth.Staff > 0 && Truth.ThresholdManning
					? ExactFoundingHeartInt(Building, "KingdomThresholdManning", 1)
					: FoundingHeartPropertyAbsent(Building, "KingdomThresholdManning"))
				&& ExactFoundingHeartPurpose(Building, Truth.PurposeLegacy)
				&& FoundingHeartPropertyAbsent(Building, FoundingHeartOwnerProperty)
				&& FoundingHeartPropertyAbsent(Building, FoundingHeartSlotProperty);
		}

		private static bool ExactPositiveFoundingHeartInt(GameObject Object, string Key,
			int Expected)
		{
			return Expected > 0 ? ExactFoundingHeartInt(Object, Key, Expected)
				: FoundingHeartPropertyAbsent(Object, Key);
		}

		private static bool ExactFoundingHeartInt(GameObject Object, string Key, int Expected)
		{
			return Object.HasIntProperty(Key) && !Object.HasStringProperty(Key)
				&& Object.GetIntProperty(Key) == Expected;
		}

		private static bool ExactFoundingHeartString(GameObject Object, string Key, string Expected)
		{
			return Object.HasStringProperty(Key) && !Object.HasIntProperty(Key)
				&& Object.GetStringProperty(Key) == Expected;
		}

		private static bool FoundingHeartPropertyAbsent(GameObject Object, string Key)
		{
			return !Object.HasStringProperty(Key) && !Object.HasIntProperty(Key);
		}
	}
}
