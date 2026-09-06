namespace ThousandAndFirst
{
	/// <summary>Classifies exact receipt values. The runtime separately proves live authority
	/// and persists the parent intent before applying any admitted local write.</summary>
	internal static class KingdomSubsidenceReleaseRules
	{
		internal static bool Same(KingdomSubsidenceWearReceipt a, KingdomSubsidenceWearReceipt b)
		{
			if (ReferenceEquals(a, b)) return true;
			return a != null && b != null && a.Phase == b.Phase && a.Id == b.Id
				&& a.Cause == b.Cause && a.BeforeWear == b.BeforeWear && a.AfterWear == b.AfterWear
				&& a.Wear == b.Wear && a.LastCause == b.LastCause
				&& a.LastCompletedId == b.LastCompletedId && a.Line == b.Line
				&& a.MessageState == b.MessageState;
		}

		internal static bool TryPlan(string stepId, int beforeWear, int afterWear,
			KingdomSubsidenceWearReceipt observed, out KingdomSubsidenceWearReceipt target)
		{
			target = null;
			if (!KingdomSubsidenceStepRules.IsStepId(stepId) || !ValidValues(observed)
				|| observed.Cause != (int)KingdomWearRules.WearCause.Subsidence
				|| observed.BeforeWear != beforeWear || observed.AfterWear != afterWear
				|| observed.Wear != afterWear) return false;
			bool mutated = observed.Phase == (int)KingdomWearIncidentPhase.Mutated
				&& observed.Id == stepId;
			bool cleared = observed.Phase == (int)KingdomWearIncidentPhase.None
				&& observed.Id == null && observed.Line == null && observed.LastCompletedId == stepId;
			if (!mutated && !cleared) return false;
			target = new KingdomSubsidenceWearReceipt((int)KingdomWearIncidentPhase.None, null,
				observed.Cause, observed.BeforeWear, observed.AfterWear, observed.Wear,
				observed.LastCause, stepId, null, observed.MessageState);
			return true;
		}

		internal static bool ValidProof(string stepId, int beforeWear, int afterWear,
			KingdomSubsidenceWearReceipt before, KingdomSubsidenceWearReceipt target)
		{
			return TryPlan(stepId, beforeWear, afterWear, before,
				out KingdomSubsidenceWearReceipt expected) && Same(expected, target);
		}

		internal static KingdomSubsidenceWearReceipt AfterWrite(KingdomSubsidenceWearReceipt before,
			KingdomSubsidenceWearReceipt target, int cut)
		{
			if (cut < 0 || cut > 4 || !ValidPair(before, target)) return null;
			return Prefix(before, target, cut);
		}

		internal static bool TryNextWrite(KingdomSubsidenceWearReceipt before,
			KingdomSubsidenceWearReceipt target, KingdomSubsidenceWearReceipt observed, out int field)
		{
			field = -1;
			if (observed == null || !ValidPair(before, target)) return false;
			for (int cut = 4; cut >= 0; cut--)
				if (Same(Prefix(before, target, cut), observed))
				{
					field = cut;
					return true;
				}
			return false;
		}

		private static bool ValidPair(KingdomSubsidenceWearReceipt before,
			KingdomSubsidenceWearReceipt target)
		{
			return before != null && target != null
				&& ValidProof(target.LastCompletedId, before.BeforeWear, before.AfterWear, before, target);
		}

		private static KingdomSubsidenceWearReceipt Prefix(KingdomSubsidenceWearReceipt before,
			KingdomSubsidenceWearReceipt target, int cut)
		{
			return new KingdomSubsidenceWearReceipt(cut >= 2 ? target.Phase : before.Phase,
				cut >= 3 ? target.Id : before.Id, before.Cause, before.BeforeWear, before.AfterWear,
				before.Wear, before.LastCause, cut >= 1 ? target.LastCompletedId : before.LastCompletedId,
				cut >= 4 ? target.Line : before.Line, before.MessageState);
		}

		private static bool ValidValues(KingdomSubsidenceWearReceipt receipt)
		{
			return receipt != null && receipt.BeforeWear >= 0
				&& receipt.BeforeWear <= KingdomMaterialRules.MaxWearPercent
				&& receipt.AfterWear >= receipt.BeforeWear
				&& receipt.AfterWear <= KingdomMaterialRules.MaxWearPercent
				&& receipt.Wear >= 0 && receipt.Wear <= KingdomMaterialRules.MaxWearPercent
				&& receipt.LastCause >= (int)KingdomWearRules.WearCause.None
				&& receipt.LastCause <= (int)KingdomWearRules.WearCause.Subsidence
				&& receipt.MessageState >= (int)KingdomWearSinkDisposition.None
				&& receipt.MessageState <= (int)KingdomWearSinkDisposition.Lost
				&& Text(receipt.Id) && Text(receipt.LastCompletedId) && Text(receipt.Line);
		}

		private static bool Text(string value)
		{
			if (value == null) return true;
			if (value.Length > KingdomWearRules.MaxSavedTextChars) return false;
			for (int i = 0; i < value.Length; i++)
			{
				if (char.IsControl(value[i])) return false;
				if (!char.IsSurrogate(value[i])) continue;
				if (!char.IsHighSurrogate(value[i]) || i + 1 >= value.Length
					|| !char.IsLowSurrogate(value[++i])) return false;
			}
			return true;
		}
	}
}
