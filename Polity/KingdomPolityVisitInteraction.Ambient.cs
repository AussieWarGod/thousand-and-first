using System;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityVisitInteraction
	{
		internal static string ActionLabel(KingdomPolityCohortPurpose Purpose)
		{
			switch (Purpose)
			{
			case KingdomPolityCohortPurpose.Guard: return "Hear the witnessed watch report";
			case KingdomPolityCohortPurpose.Patrol: return "Hear the condition report";
			case KingdomPolityCohortPurpose.Courier: return "Receive the frozen message";
			case KingdomPolityCohortPurpose.Trader: return "Hear the market notice";
			case KingdomPolityCohortPurpose.Migrant: return "Answer the petition";
			case KingdomPolityCohortPurpose.Warband: return "Address confrontation";
			default: return "Hear delegation";
			}
		}

		internal static string ActionVerb(KingdomPolityCohortPurpose Purpose)
		{
			switch (Purpose)
			{
			case KingdomPolityCohortPurpose.Guard: return "hear the witnessed watch report";
			case KingdomPolityCohortPurpose.Patrol: return "hear the condition report";
			case KingdomPolityCohortPurpose.Courier: return "receive the frozen message";
			case KingdomPolityCohortPurpose.Trader: return "hear the market notice";
			case KingdomPolityCohortPurpose.Migrant: return "answer the petition";
			case KingdomPolityCohortPurpose.Warband: return "address the confrontation";
			default: return "hear the delegation";
			}
		}

		private static bool CanAnswerAmbient(KingdomSystem System,
			KingdomPolityCohortPlan Cohort)
		{
			if (System == null || !IsAmbient(Cohort?.Purpose ??
				KingdomPolityCohortPurpose.None) ||
				(Cohort.Phase != KingdomPolityCohortPhase.Materialized &&
				 Cohort.Phase != KingdomPolityCohortPhase.Concluded) ||
				KingdomPolityDispatchRules.Expired(Cohort, Now())) return false;
			return KingdomPolityAmbientTransactionRules.Valid(Cohort.AmbientTransaction,
				Cohort.CohortId, out _);
		}

		private static void AnswerAmbient(KingdomSystem System, GameObject Body,
			KingdomPolityCohortPlan Cohort)
		{
			KingdomPolityAmbientTransaction transaction = Cohort.AmbientTransaction;
			if (!KingdomPolityAmbientTransactionRules.Valid(transaction, Cohort.CohortId,
				out string failure))
			{
				Popup.Show("This pre-schema visit has no exact semantic transaction. Nothing changes.");
				return;
			}
			string report = AmbientReport(transaction);
			if (transaction.TerminalChoice != KingdomPolityAmbientTerminalChoice.None)
			{
				Popup.Show(report + "\n\nThe exact answer is already recorded: " +
					TerminalLabel(transaction.TerminalChoice) + "."); return;
			}
			string[] options = Options(transaction.Purpose);
			int picked = Popup.PickOption(Title: ActionLabel(transaction.Purpose),
				Intro: report, Options: options, AllowEscape: true);
			if (picked < 0 || picked >= options.Length - 1) return;
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Popup.Show("Settlement simulation is paused. No answer is recorded."); return;
			}
			long tick = Now(); KingdomPolityAdmissionHandoff handoff = null;
			KingdomPolityAmbientTerminalChoice choice = Choice(transaction.Purpose, picked);
			if (transaction.Purpose == KingdomPolityCohortPurpose.Migrant &&
				!KingdomPolityAmbientTransactionRules.TryPrepareAdmissionHandoff(System.RealmId,
					Cohort, Cohort.ResolvedMembers[0].MemberKey, Body.ID, Body.CurrentZone.ZoneID,
					Body.BaseDisplayNameStripped, tick, out handoff, out failure))
			{
				Popup.Show("The petition handoff cannot be prepared: " + failure); return;
			}
			if (!KingdomPolityAmbientTransactionRules.TryRecordTerminal(System.PolityLedger,
				System.PolityLedger.Revision, Cohort.CohortId, choice, tick, handoff,
				out KingdomPolityPublicationResult _, out failure))
			{
				Popup.Show("The answer is not recorded: " + failure); return;
			}
			Popup.Show(TerminalResult(choice));
		}

		private static string AmbientReport(KingdomPolityAmbientTransaction T)
		{
			string from = KingdomPresentation.Rich(T.SourceSettlementName);
			string to = KingdomPresentation.Rich(T.DestinationSettlementName);
			string detail = KingdomPresentation.Rich(T.SafeDetail);
			switch (T.Purpose)
			{
			case KingdomPolityCohortPurpose.Guard:
				return "At " + to + ", the watch reports one witnessed local matter:\n\n" + detail;
			case KingdomPolityCohortPurpose.Patrol:
				return "At " + to + ", the patrol reports one caused local condition:\n\n" + detail +
					"\n\nNo unseen safety, road, journey, or offscreen outcome is claimed.";
			case KingdomPolityCohortPurpose.Courier:
				return "A message frozen at " + from + " is delivered to " + to + ":\n\n" + detail;
			case KingdomPolityCohortPurpose.Trader:
				return "A market visit from " + from + " addresses " + to + ":\n\n" + detail;
			default:
				return "A petitioner from " + from + " asks to enter " + to + ":\n\n" + detail;
			}
		}

		private static string[] Options(KingdomPolityCohortPurpose Purpose)
		{
			if (Purpose == KingdomPolityCohortPurpose.Migrant)
				return new[] { "Accept the petition", "Reject the petition", "Answer later" };
			return new[] { Purpose == KingdomPolityCohortPurpose.Trader
				? "Acknowledge the no-trade visit" : "Acknowledge the report", "Answer later" };
		}

		private static KingdomPolityAmbientTerminalChoice Choice(
			KingdomPolityCohortPurpose Purpose, int Pick)
		{
			if (Purpose == KingdomPolityCohortPurpose.Migrant) return Pick == 0
				? KingdomPolityAmbientTerminalChoice.PetitionAccepted
				: KingdomPolityAmbientTerminalChoice.PetitionRejected;
			return Purpose == KingdomPolityCohortPurpose.Trader
				? KingdomPolityAmbientTerminalChoice.AcknowledgedNoTrade
				: KingdomPolityAmbientTerminalChoice.Acknowledged;
		}

		private static string TerminalResult(KingdomPolityAmbientTerminalChoice Choice)
		{
			if (Choice == KingdomPolityAmbientTerminalChoice.PetitionAccepted)
				return "The petition is accepted and handed to resident-arrival authority. " +
					"No resident, citizenship, row, or body binding is created by this answer.";
			if (Choice == KingdomPolityAmbientTerminalChoice.PetitionRejected)
				return "The petition is rejected. No resident authority changes.";
			return "The exact frozen matter is acknowledged. No journey or offscreen result is inferred.";
		}

		private static string TerminalLabel(KingdomPolityAmbientTerminalChoice Choice)
		{
			return Choice == KingdomPolityAmbientTerminalChoice.PetitionAccepted ? "accepted" :
				Choice == KingdomPolityAmbientTerminalChoice.PetitionRejected ? "rejected" :
				Choice == KingdomPolityAmbientTerminalChoice.AcknowledgedNoTrade ?
				"acknowledged without trade" : "acknowledged";
		}

		private static bool IsAmbient(KingdomPolityCohortPurpose Purpose)
		{
			return KingdomPolityDispatchRules.AmbientPurpose(Purpose);
		}
	}
}
