using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static KingdomPurposeBodyDriveState WaitingEffect(string Message,
			out string Failure)
		{
			Failure = string.IsNullOrEmpty(Message)
				? "The bounded purpose effect waits on its exact local ground." : Message;
			return KingdomPurposeBodyDriveState.Waiting;
		}

		private static KingdomPurposeBodyDriveState InvalidEffect(string Message,
			out string Failure)
		{
			Failure = string.IsNullOrEmpty(Message)
				? "The bounded purpose effect cannot prove its exact state." : Message;
			return KingdomPurposeBodyDriveState.Invalid;
		}

		private static KingdomPurposeBodyDriveState FaultedEffect(GameObject Work,
			string Receipt, int Step, string Observation, string Message, out string Failure)
		{
			Failure = string.IsNullOrEmpty(Message)
				? "The bounded purpose effect reached an unknown physical aftermath." : Message;
			if (!StampPurposeEffectFault(Work, Receipt, Step, Observation))
				Failure += " Its durable fault witness could not be reproved.";
			return KingdomPurposeBodyDriveState.Invalid;
		}

		private static string MissingDebit(KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeEffectCallbackKind Callback)
		{
			if (Context == null)
				return "The bounded purpose effect has no exact local input to spend.";
			if (Callback == KingdomPurposeEffectCallbackKind.RefineRaw)
				return "This work's exact input store holds no unreserved "
					+ KingdomMaterialRules.MaterialName(Context.RawMaterial) + " to refine.";
			if (Callback == KingdomPurposeEffectCallbackKind.HarvestCrop)
				return "The Granary-Colossus holds no unreserved crop of its frozen kind to mill.";
			return "The bounded purpose effect has no exact local input for this step.";
		}

		private static bool TryExpectedEffectCallback(KingdomPurposeKind Kind, int Step,
			out KingdomPurposeEffectCallbackKind Callback)
		{
			Callback = KingdomPurposeEffectCallbackKind.Invalid;
			if (Kind == KingdomPurposeKind.Deep || Kind == KingdomPurposeKind.Forge)
			{
				if (Step == (int)KingdomPurposeEffectRefineStep.None
					|| Step == (int)KingdomPurposeEffectRefineStep.FirstRawSpent)
					Callback = KingdomPurposeEffectCallbackKind.RefineRaw;
				else if (Step == (int)KingdomPurposeEffectRefineStep.SecondRawSpent)
					Callback = KingdomPurposeEffectCallbackKind.RefinedProduct;
			}
			else if (Kind == KingdomPurposeKind.Harvest)
			{
				if (Step >= (int)KingdomPurposeEffectHarvestStep.None
					&& Step < (int)KingdomPurposeEffectHarvestStep.ThirdCropSpent)
					Callback = KingdomPurposeEffectCallbackKind.HarvestCrop;
				else if (Step == (int)KingdomPurposeEffectHarvestStep.ThirdCropSpent)
					Callback = KingdomPurposeEffectCallbackKind.HarvestSeed;
				else if (Step == (int)KingdomPurposeEffectHarvestStep.SeedMade)
					Callback = KingdomPurposeEffectCallbackKind.HarvestStaple;
			}
			return Callback != KingdomPurposeEffectCallbackKind.Invalid;
		}
	}
}
