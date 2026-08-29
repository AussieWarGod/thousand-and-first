using System;

namespace ThousandAndFirst
{
	/// <summary>Append-only lifecycle vocabulary for witnessed cook loss and explicit handoff.</summary>
	public static partial class KingdomNamedCookRules
	{
		public static KingdomNamedCookReceipt BeginVacancy(KingdomNamedCookReceipt Receipt,
			KingdomNamedCookVacancyCause Cause)
		{
			if (Receipt == null || !Enum.IsDefined(typeof(KingdomNamedCookVacancyCause), Cause)
				|| Cause == KingdomNamedCookVacancyCause.None) return null;
			KingdomNamedCookPhase target = PreparedVacancyPhase(Cause);
			if (Receipt.Phase == target) return Receipt.Copy();
			if (Receipt.Phase != KingdomNamedCookPhase.Applied
				&& Receipt.Phase != KingdomNamedCookPhase.Prepared) return null;
			KingdomNamedCookReceipt copy = Receipt.Copy();
			copy.Phase = target; copy.ReleasedTick = 0L; copy.Fault = "";
			return Validate(copy, out string _) ? copy : null;
		}

		public static KingdomNamedCookReceipt CompleteVacancy(KingdomNamedCookReceipt Receipt,
			long Tick)
		{
			if (Receipt == null || !IsVacancyPrepared(Receipt.Phase)) return null;
			KingdomNamedCookReceipt copy = Receipt.Copy();
			copy.Phase = VacantPhase(VacancyCause(Receipt.Phase));
			copy.ReleasedTick = Tick; copy.Fault = "";
			return Validate(copy, out string _) ? copy : null;
		}

		/// <summary>Exact rollback for a staged cause whose owning transaction did not publish.</summary>
		public static KingdomNamedCookReceipt CancelVacancy(KingdomNamedCookReceipt Receipt,
			KingdomNamedCookVacancyCause Cause)
		{
			if (Receipt == null || !IsVacancyPrepared(Receipt.Phase)
				|| VacancyCause(Receipt.Phase) != Cause) return null;
			KingdomNamedCookReceipt copy = Receipt.Copy();
			copy.Phase = KingdomNamedCookPhase.Applied;
			copy.ReleasedTick = 0L; copy.Fault = "";
			return Validate(copy, out string _) ? copy : null;
		}

		public static bool IsVacancyPrepared(KingdomNamedCookPhase Phase)
		{
			return Phase == KingdomNamedCookPhase.ReleasePrepared
				|| Phase == KingdomNamedCookPhase.DeathVacancyPrepared
				|| Phase == KingdomNamedCookPhase.DepartureVacancyPrepared
				|| Phase == KingdomNamedCookPhase.RetirementVacancyPrepared
				|| Phase == KingdomNamedCookPhase.HandoffVacancyPrepared;
		}

		public static bool IsVacant(KingdomNamedCookPhase Phase)
		{
			return Phase == KingdomNamedCookPhase.Released
				|| Phase == KingdomNamedCookPhase.DeathVacant
				|| Phase == KingdomNamedCookPhase.DepartureVacant
				|| Phase == KingdomNamedCookPhase.RetirementVacant
				|| Phase == KingdomNamedCookPhase.HandoffVacant;
		}

		public static KingdomNamedCookVacancyCause VacancyCause(KingdomNamedCookPhase Phase)
		{
			switch (Phase)
			{
			case KingdomNamedCookPhase.ReleasePrepared:
			case KingdomNamedCookPhase.Released: return KingdomNamedCookVacancyCause.Released;
			case KingdomNamedCookPhase.DeathVacancyPrepared:
			case KingdomNamedCookPhase.DeathVacant: return KingdomNamedCookVacancyCause.Death;
			case KingdomNamedCookPhase.DepartureVacancyPrepared:
			case KingdomNamedCookPhase.DepartureVacant: return KingdomNamedCookVacancyCause.Departure;
			case KingdomNamedCookPhase.RetirementVacancyPrepared:
			case KingdomNamedCookPhase.RetirementVacant:
				return KingdomNamedCookVacancyCause.VoluntaryRetirement;
			case KingdomNamedCookPhase.HandoffVacancyPrepared:
			case KingdomNamedCookPhase.HandoffVacant: return KingdomNamedCookVacancyCause.Handoff;
			default: return KingdomNamedCookVacancyCause.None;
			}
		}

		public static KingdomNamedCookServiceState ServiceState(KingdomNamedCookReceipt Receipt)
		{
			if (Receipt == null || Receipt.Phase == KingdomNamedCookPhase.None
				|| IsVacant(Receipt.Phase)) return KingdomNamedCookServiceState.Vacant;
			if (Receipt.Phase == KingdomNamedCookPhase.Applied)
				return KingdomNamedCookServiceState.Available;
			if (Receipt.Phase == KingdomNamedCookPhase.Quarantined)
				return KingdomNamedCookServiceState.Quarantined;
			return KingdomNamedCookServiceState.RecoveryPending;
		}

		public static string VacancyClause(KingdomNamedCookReceipt Receipt)
		{
			switch (VacancyCause(Receipt == null ? KingdomNamedCookPhase.None : Receipt.Phase))
			{
			case KingdomNamedCookVacancyCause.Death: return "died";
			case KingdomNamedCookVacancyCause.Departure: return "departed";
			case KingdomNamedCookVacancyCause.VoluntaryRetirement: return "retired from the hearth";
			case KingdomNamedCookVacancyCause.Handoff: return "offered a deliberate handoff";
			default: return "was released from the hearth";
			}
		}

		private static KingdomNamedCookPhase PreparedVacancyPhase(
			KingdomNamedCookVacancyCause Cause)
		{
			switch (Cause)
			{
			case KingdomNamedCookVacancyCause.Death:
				return KingdomNamedCookPhase.DeathVacancyPrepared;
			case KingdomNamedCookVacancyCause.Departure:
				return KingdomNamedCookPhase.DepartureVacancyPrepared;
			case KingdomNamedCookVacancyCause.VoluntaryRetirement:
				return KingdomNamedCookPhase.RetirementVacancyPrepared;
			case KingdomNamedCookVacancyCause.Handoff:
				return KingdomNamedCookPhase.HandoffVacancyPrepared;
			default: return KingdomNamedCookPhase.ReleasePrepared;
			}
		}

		private static KingdomNamedCookPhase VacantPhase(KingdomNamedCookVacancyCause Cause)
		{
			switch (Cause)
			{
			case KingdomNamedCookVacancyCause.Death: return KingdomNamedCookPhase.DeathVacant;
			case KingdomNamedCookVacancyCause.Departure:
				return KingdomNamedCookPhase.DepartureVacant;
			case KingdomNamedCookVacancyCause.VoluntaryRetirement:
				return KingdomNamedCookPhase.RetirementVacant;
			case KingdomNamedCookVacancyCause.Handoff:
				return KingdomNamedCookPhase.HandoffVacant;
			default: return KingdomNamedCookPhase.Released;
			}
		}
	}
}
