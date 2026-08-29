namespace ThousandAndFirst
{
	/// <summary>Stable v1 rendering. Every variable word is frozen in the receipt before display.</summary>
	public static partial class KingdomFirstFeastRules
	{
		public static string RenderOffer(KingdomFirstFeastReceipt Row,
			bool ProposerAvailable, bool WitnessAvailable)
		{
			if (!Valid(Row) || Row.Phase != KingdomFirstFeastPhase.Offered) return "";
			string facts = Row.ProposerName + " proposes " + Row.DishName + " at "
				+ Row.SettlementName + ", made in the telling from " + Row.Ingredients
				+ ". The exact deed is that " + Row.DeedText + ". The proposed dedication is to "
				+ Row.OfferedDedication + ".";
			if (!ProposerAvailable || !WitnessAvailable) return facts;
			return facts + "\n\n" + Row.ProposerName
				+ ": \"Keep the founding water in the telling; the food grants nothing.\"\n"
				+ Row.WitnessName
				+ ": \"Change the dedication if you wish; no meal or recipe follows.\"";
		}

		public static string DecisionDisclosure(KingdomFirstFeastChoice Choice,
			string Adaptation)
		{
			if (Choice == KingdomFirstFeastChoice.Adopt)
				return "Adopt records one private civic practice, attributed to its proposer and deed. "
					+ "It grants no meal, ingredient, recipe, note, buff, reputation, or calendar event.";
			if (Choice == KingdomFirstFeastChoice.Adapt && IsAdaptation(Adaptation))
				return "Adapt records the same one private practice, dedicated instead to "
					+ Adaptation + ". It grants no meal, recipe, creed change, or other benefit.";
			if (Choice == KingdomFirstFeastChoice.Refuse)
				return "Refuse closes this finite proposal for free. It records no practice or reward.";
			if (Choice == KingdomFirstFeastChoice.Defer)
				return "Defer is free and changes nothing. The proposal remains open without a deadline.";
			return "";
		}

		public static string RenderOutcome(KingdomFirstFeastReceipt Row)
		{
			if (!Valid(Row)) return "The First Feast record is unavailable.";
			if (Row.Phase == KingdomFirstFeastPhase.Offered)
				return "The proposal by " + Row.ProposerName + " remains open without a deadline.";
			if (Row.Phase == KingdomFirstFeastPhase.Refused)
				return "The proposal by " + Row.ProposerName
					+ " was refused. No civic practice or reward was created.";
			if (Row.Phase == KingdomFirstFeastPhase.Archived)
				return "The unaccepted proposal by " + Row.ProposerName
					+ " was archived when civic stories were disabled. Re-enabling them "
					+ "does not recreate a backlog.";
			if (Row.Phase == KingdomFirstFeastPhase.Quarantined)
				return "The First Feast record is quarantined: " + Row.Fault;
			return Row.ProposerName + " proposed " + Row.DishName + " after " + Row.DeedText
				+ ". " + Row.SettlementName + " keeps it as a private practice dedicated to "
				+ EffectiveDedication(Row) + ". It creates no recurring feast or mechanical benefit.";
		}

		public static string ChronicleClause(KingdomFirstFeastReceipt Row)
		{
			if (!IsAffirmative(Row)) return null;
			return Row.ProposerName + " proposed " + Row.DishName + " after " + Row.DeedText
				+ ", and " + Row.SettlementName + " adopted the private practice in dedication to "
				+ EffectiveDedication(Row);
		}

		public static string ChronicleEventId(KingdomFirstFeastReceipt Row)
		{
			return IsAffirmative(Row) ? Row.PracticeId + ":history" : null;
		}

		public static string RecipePolicyText(bool NamedCookAvailable, string CookName,
			string RecipeName)
		{
			if (!NamedCookAvailable)
				return "This practice grants no recipe. The named hearth is vacant; the practice remains.";
			return "This practice grants no recipe. Separately, named cook " + CookName
				+ " offers " + RecipeName + " through Qud's ordinary paid water ritual.";
		}
	}
}
