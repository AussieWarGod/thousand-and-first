namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRules
	{
		internal static bool TryFreezeOption(KingdomSubsidenceStepBook prior,
			KingdomSubsidenceOptionIntent intent, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Valid(prior) || prior.OptionModel != NoOption
				|| !KingdomSubsidenceOptionIntentRules.Matches(intent, prior)
				|| !KingdomSubsidenceOptionIntentRules.TryEncode(intent, out string wire)) return false;
			next = prior.WithOption(wire);
			return Valid(next);
		}

		internal static bool TryFinishOption(KingdomSubsidenceStepBook prior, long checkpoint,
			KingdomDurableKeyObservation observed, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Valid(prior)
				|| !KingdomSubsidenceOptionIntentRules.TryDecode(prior.OptionModel,
					out KingdomSubsidenceOptionIntent intent)
				|| !KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, prior, checkpoint, out long target)
				|| checkpoint != target || !KingdomSubsidenceOptionIntentRules.TrySnapshot(intent,
					out KingdomSubsidenceOptionRules.Snapshot snapshot)
				|| !KingdomSubsidenceOptionRules.ProvesPublished(snapshot, observed)) return false;
			next = prior.WithOption(NoOption);
			return Valid(next);
		}
	}
}
