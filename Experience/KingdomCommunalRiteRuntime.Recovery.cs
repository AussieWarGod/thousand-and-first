#if !TAF_TESTS
using System;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomCommunalRiteRuntime
	{
		internal static void ReconcileBestEffort(KingdomSystem system)
		{
			GameObject founder = The.Player;
			if (!KingdomFirstFeastRuntime.TryCurrentCity(system, founder,
				out KingdomFirstFeastRuntime.CityContext context, out string _)
				|| !KingdomExperienceRules.TryGetFirstFeast(system.Experience,
					context.SettlementId, out KingdomFirstFeastReceipt practice, out string failure)
				|| !KingdomFirstFeastRules.IsAffirmative(practice)) return;
			long now = The.Game?.TimeTicks ?? 0L;
			if (!TryEnsureBound(system, out failure) || !TryRead(system,
				out KingdomCommunalRiteBook book, out failure)
				|| !KingdomCommunalRiteRules.TryFind(book, context.SettlementId,
					out KingdomCommunalRiteReceipt row)) return;
			if (row != null && !TryResume(system, context, practice, now, out failure))
				KingdomLog.Log("communal expression: load/option recovery retained (" + failure + ")");
		}

		private static bool TryStart(KingdomSystem system,
			KingdomFirstFeastRuntime.CityContext context, KingdomFirstFeastReceipt practice,
			long now, out string failure)
		{
			failure = null;
			if (!TryEnsureBound(system, out failure)
				|| !TryRead(system, out KingdomCommunalRiteBook book, out failure)
				|| !KingdomCommunalRiteRules.TryFind(book, context.SettlementId,
					out KingdomCommunalRiteReceipt standing)) return false;
			if (standing != null)
				return standing.PracticeId == practice.PracticeId
					? TryResume(system, context, practice, now, out failure)
					: Fail("this city already names another communal expression", out failure);
			if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(system, now, out failure)
				|| !KingdomExperienceRules.TryGetEnableEpoch(system.Experience,
					KingdomExperienceOptionKind.CivicStory, now, out long epoch, out failure)
				|| !KingdomCommunalRiteRules.TryPracticeSubject(practice.PracticeId,
					out int subject)) return false;
			string eventId = KingdomCommunalRiteRules.EventId(
				context.SettlementId, now, subject);
			KingdomCommunalRiteBook prepared = KingdomCommunalRiteRules.Clone(book);
			if (!KingdomCommunalRiteRules.TryPrepare(prepared, prepared.Revision, practice,
				eventId, now, epoch, out _, out failure)
				|| !TryPublish(system, prepared, out failure)) return false;
			KingdomExperienceRuntime.TryRecord(system,
				KingdomExperienceExperiment.FirstFeastPractice,
				KingdomExperienceTrialArm.Projected,
				KingdomExperienceObservationKind.Exposed, 1);
			// The Prepared C18 cut above and Committed C18 cut below both precede Queue.
			return TryCommitThenDrive(system, context, practice, now, out failure);
		}

		private static bool TryResume(KingdomSystem system,
			KingdomFirstFeastRuntime.CityContext context, KingdomFirstFeastReceipt practice,
			long now, out string failure)
		{
			failure = null;
			if (!TryRead(system, out KingdomCommunalRiteBook book, out failure)
				|| !KingdomCommunalRiteRules.TryFind(book, context.SettlementId,
					out KingdomCommunalRiteReceipt row) || row == null
				|| row.PracticeId != practice.PracticeId)
				return Fail(failure ?? "exact communal expression is absent", out failure);
			if (row.Phase == KingdomCommunalRitePhase.Prepared)
				return TryCommitThenDrive(system, context, practice, now, out failure);
			return TryDrive(system, context, row, now, out failure);
		}

		private static bool TryCommitThenDrive(KingdomSystem system,
			KingdomFirstFeastRuntime.CityContext context, KingdomFirstFeastReceipt practice,
			long now, out string failure)
		{
			failure = null;
			if (!TryRead(system, out KingdomCommunalRiteBook book, out failure)
				|| !KingdomCommunalRiteRules.TryFind(book, context.SettlementId,
					out KingdomCommunalRiteReceipt row) || row == null
				|| row.PracticeId != practice.PracticeId)
				return Fail(failure ?? "prepared communal expression is absent", out failure);
			if (row.Phase == KingdomCommunalRitePhase.Prepared)
			{
				KingdomCommunalRiteOptionDisposition option = ObserveOption(
					system, row, now, out failure);
				if (option == KingdomCommunalRiteOptionDisposition.Unreadable) return false;
				if (option != KingdomCommunalRiteOptionDisposition.Current)
				{
					if (!KingdomCommunalRiteRules.TryPracticeSubject(row.PracticeId,
						out int preparedSubject)
						|| !TryPhysical(context.Book, row, preparedSubject, now,
							out KingdomCommunalRitePhysicalState preparedPhysical,
							out failure)) return false;
					return TrySuppressThenClear(system, context.Book, row,
						preparedSubject, preparedPhysical, now, out failure);
				}
				KingdomCommunalRiteBook committed = KingdomCommunalRiteRules.Clone(book);
				if (!KingdomCommunalRiteRules.TryCommit(committed, committed.Revision,
					row.PracticeId, row.EventId, out _, out failure)
					|| !TryPublish(system, committed, out failure)) return false;
				if (!TryRead(system, out book, out failure)
					|| !KingdomCommunalRiteRules.TryFind(book, context.SettlementId, out row)
					|| row == null || row.Phase != KingdomCommunalRitePhase.Committed)
					return Fail(failure ?? "communal expression commit was not reproved", out failure);
			}
			return TryDrive(system, context, row, now, out failure);
		}

		private static bool TryDrive(KingdomSystem system,
			KingdomFirstFeastRuntime.CityContext context, KingdomCommunalRiteReceipt row,
			long now, out string failure)
		{
			failure = null;
			if (!KingdomCommunalRiteRules.TryPracticeSubject(row.PracticeId, out int subject))
				return Fail("communal expression practice identity is invalid", out failure);
			if (row.Phase == KingdomCommunalRitePhase.Attended)
			{
				if (!TryPhysical(context.Book, row, subject, now,
					out KingdomCommunalRitePhysicalState attendedPhysical,
					out failure)) return false;
				return TryAcknowledgeAttended(system, context.Book, row, subject,
					attendedPhysical,
					now, out failure);
			}
			if (row.Phase == KingdomCommunalRitePhase.Suppressed)
			{
				if (!TryPhysical(context.Book, row, subject, now,
					out KingdomCommunalRitePhysicalState suppressedPhysical,
					out failure)) return false;
				if (suppressedPhysical == KingdomCommunalRitePhysicalState.Ready)
					return TryPublishTerminalThenAcknowledge(system, context.Book, row,
						subject, now, out failure);
				return TryClearSuppressed(system, context.Book, row, subject,
					suppressedPhysical,
					now, out failure);
			}
			if (row.Phase != KingdomCommunalRitePhase.Committed)
				return Fail("communal expression is not committed", out failure);
			if (!TryPhysical(context.Book, row, subject, now,
				out KingdomCommunalRitePhysicalState physical, out failure)) return false;
			if (physical == KingdomCommunalRitePhysicalState.Ready)
				return TryPublishTerminalThenAcknowledge(system, context.Book, row, subject,
					now, out failure);
			if (physical == KingdomCommunalRitePhysicalState.Restoring)
				return Fail("communal attendance is restoring without a terminal C18 cut",
					out failure);
			KingdomCommunalRiteOptionDisposition option = ObserveOption(
				system, row, now, out failure);
			if (option == KingdomCommunalRiteOptionDisposition.Unreadable) return false;
			if (option != KingdomCommunalRiteOptionDisposition.Current)
				return TrySuppressThenClear(system, context.Book, row, subject, physical,
					now, out failure);
			if (!TryEnsureBodyLease(system, row, out failure)) return false;
			KingdomPhysicalQueueResult result = KingdomPhysicalHappenings.QueueCommunalRite(
				system, context.Book, row.PracticeId, subject, row.EventTick,
				row.EnableEpoch, The.Player?.CurrentZone, now, out string eventId,
				out string[] _);
			if (eventId != row.EventId)
				return Fail("physical communal expression returned another event", out failure);
			if (result == KingdomPhysicalQueueResult.Refused
				|| result == KingdomPhysicalQueueResult.Busy
				|| result == KingdomPhysicalQueueResult.AlreadyCompleted)
			{
				TryReleaseBodyLease(system, row, out string _);
				return Fail("physical communal expression refused exact recovery (" + result + ")",
					out failure);
			}
			if (!TryPhysical(context.Book, row, subject, now, out physical, out failure))
				return false;
			return physical == KingdomCommunalRitePhysicalState.Ready
				? TryPublishTerminalThenAcknowledge(system, context.Book, row, subject,
					now, out failure) : true;
		}

		private static KingdomCommunalRiteOptionDisposition ObserveOption(
			KingdomSystem system, KingdomCommunalRiteReceipt row, long now,
			out string failure)
		{
			failure = null;
			if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(system, now, out failure))
				return KingdomCommunalRiteOptionDisposition.Unreadable;
			bool enabled = KingdomExperienceRules.CanEmit(system.Experience,
				KingdomExperienceOptionKind.CivicStory, now);
			long epoch = 0L;
			if (enabled && !KingdomExperienceRules.TryGetEnableEpoch(system.Experience,
				KingdomExperienceOptionKind.CivicStory, now, out epoch, out failure))
				return KingdomCommunalRiteOptionDisposition.Unreadable;
			return KingdomCommunalRiteRules.OptionDisposition(
				true, enabled, epoch, row.EnableEpoch);
		}
	}
}
#endif
