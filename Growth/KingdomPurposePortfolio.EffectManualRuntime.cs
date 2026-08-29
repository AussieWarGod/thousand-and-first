namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static KingdomPurposeBodyDriveState DriveManualPurposeEffect(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeOperationReceipt Operation, out int NextStep, out string Failure)
		{
			NextStep = Operation == null ? KingdomPurposePortfolioRules.PurposeEffectNone
				: Operation.EffectStep;
			Failure = null;
			if (Context == null || Operation == null || Context.Kind != Operation.SourceKind)
				return InvalidEffect("The bounded purpose kind changed after consent.", out Failure);
			if (Operation.SourceKind == KingdomPurposeKind.Deep
				|| Operation.SourceKind == KingdomPurposeKind.Forge)
				return DriveRefinePurposeEffect(Context, Operation, out NextStep, out Failure);
			if (Operation.SourceKind == KingdomPurposeKind.Harvest)
				return DriveHarvestPurposeEffect(Context, Operation, out NextStep, out Failure);
			return InvalidEffect("This purpose kind has no manual bounded effect.", out Failure);
		}

		private static KingdomPurposeBodyDriveState DriveRefinePurposeEffect(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeOperationReceipt Operation, out int NextStep, out string Failure)
		{
			NextStep = Operation.EffectStep;
			Failure = null;
			if (Operation.EffectStep == (int)KingdomPurposeEffectRefineStep.None
				|| Operation.EffectStep == (int)KingdomPurposeEffectRefineStep.FirstRawSpent)
				return DrivePurposeEffectDebit(Context, Operation,
					KingdomPurposeEffectCallbackKind.RefineRaw, out NextStep, out Failure);
			if (Operation.EffectStep == (int)KingdomPurposeEffectRefineStep.SecondRawSpent)
			{
				KingdomPurposeBodyDriveState state = DrivePurposeEffectProductBatch(Context,
					Operation, KingdomPurposeEffectProductRole.Refined,
					KingdomPurposePortfolioRules.PurposeEffectRefinedUnits,
					out bool complete, out Failure);
				if (state == KingdomPurposeBodyDriveState.Applied && complete)
					NextStep = (int)KingdomPurposeEffectRefineStep.Made;
				return state;
			}
			return InvalidEffect("The refine effect entered a foreign step.", out Failure);
		}

		private static KingdomPurposeBodyDriveState DriveHarvestPurposeEffect(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeOperationReceipt Operation, out int NextStep, out string Failure)
		{
			NextStep = Operation.EffectStep;
			Failure = null;
			if (Operation.EffectStep >= (int)KingdomPurposeEffectHarvestStep.None
				&& Operation.EffectStep < (int)KingdomPurposeEffectHarvestStep.ThirdCropSpent)
				return DrivePurposeEffectDebit(Context, Operation,
					KingdomPurposeEffectCallbackKind.HarvestCrop, out NextStep, out Failure);
			if (Operation.EffectStep == (int)KingdomPurposeEffectHarvestStep.ThirdCropSpent)
			{
				KingdomPurposeBodyDriveState seed = DrivePurposeEffectProductBatch(Context,
					Operation, KingdomPurposeEffectProductRole.Seed,
					KingdomPurposePortfolioRules.PurposeEffectSeedUnits,
					out bool complete, out Failure);
				if (seed == KingdomPurposeBodyDriveState.Applied && complete)
					NextStep = (int)KingdomPurposeEffectHarvestStep.SeedMade;
				return seed;
			}
			if (Operation.EffectStep == (int)KingdomPurposeEffectHarvestStep.SeedMade)
			{
				KingdomPurposeBodyDriveState staple = DrivePurposeEffectProductBatch(Context,
					Operation, KingdomPurposeEffectProductRole.Staple,
					KingdomPurposePortfolioRules.PurposeEffectStapleUnits,
					out bool complete, out Failure);
				if (staple == KingdomPurposeBodyDriveState.Applied && complete)
					NextStep = (int)KingdomPurposeEffectHarvestStep.Milled;
				return staple;
			}
			return InvalidEffect("The Harvest effect entered a foreign step.", out Failure);
		}
	}
}
