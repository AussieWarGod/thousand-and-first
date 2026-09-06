namespace ThousandAndFirst
{
	/// <summary>Read-only execution admission over the actual world clock and exact saved book.
	/// Success neither advances a checkpoint nor authorizes departure, release, report or retirement.</summary>
	internal static class KingdomSubsidenceExecutionClockRules
	{
		internal static bool TryValidate(long actualNow, long rawLastTick,
			KingdomSubsidenceStepBook book, out string issue)
		{
			issue = null;
			if (actualNow < 0)
				return Refuse("The world's subsidence clock is negative.", out issue);
			if (rawLastTick < 0)
				return Refuse("The saved subsidence checkpoint is negative.", out issue);
			if (rawLastTick > actualNow)
				return Refuse("The saved subsidence checkpoint is ahead of the world's clock.", out issue);
			if (!KingdomSubsidenceStepRules.Valid(book))
				return Refuse("The saved subsidence book is invalid.", out issue);
			if (book.LastRetiredTick > actualNow)
				return Refuse("The retired subsidence receipt is ahead of the world's clock.", out issue);
			if (!KingdomSubsidenceAnnouncementCodec.TryDecode(book.AnnouncementModel, out var announcement)
				|| announcement.LastTick > actualNow || announcement.Active != null && announcement.Active.AtTick > actualNow)
				return Refuse("The saved subsidence announcement is ahead of the world's clock.", out issue);
			KingdomSubsidenceOptionIntent intent = null;
			if (book.OptionModel != KingdomSubsidenceStepRules.NoOption)
			{
				if (!KingdomSubsidenceOptionIntentRules.TryDecode(book.OptionModel, out intent)
					|| !KingdomSubsidenceOptionIntentRules.TrySnapshot(intent,
						out KingdomSubsidenceOptionRules.Snapshot snapshot))
					return Refuse("The frozen subsidence option cannot be proved.", out issue);
				if (snapshot.Decision.Record.ObservedTick > actualNow)
					return Refuse("The frozen subsidence option is ahead of the world's clock.", out issue);
			}
			KingdomSubsidenceStepOperation active = book.Active;
			if (active == null)
			{
				if (intent != null)
					return KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, book, rawLastTick, out _)
						|| Refuse("The saved checkpoint is not this frozen option's before, retired or target tick.", out issue);
				if (rawLastTick < book.LastRetiredTick)
					return Refuse("The saved checkpoint precedes its retired subsidence receipt.", out issue);
				return true;
			}
			if (active.DueTick > actualNow)
				return Refuse("The active subsidence due tick is ahead of the world's clock.", out issue);
			if (active.LastActivityTick > actualNow)
				return Refuse("The active subsidence activity tick is ahead of the world's clock.", out issue);
			if (active.CancelRequested && active.CancelTick > actualNow)
				return Refuse("The active subsidence cancellation is ahead of the world's clock.", out issue);
			if (active.Phase == KingdomSubsidenceStepPhase.Quarantined)
				return Refuse("The active subsidence step is quarantined.", out issue);
			if (active.RungModel != KingdomSubsidenceStepRules.NoRungs
				&& active.RungModel != KingdomSubsidenceStepRules.UnplannedRungs)
			{
				if (!KingdomSubsidenceRungCodec.TryDecode(active.RungModel, out KingdomSubsidenceRungPlan rung))
					return Refuse("The frozen subsidence rung cannot be proved.", out issue);
				if (rung.PreparedTick > actualNow)
					return Refuse("The frozen subsidence rung preparation is ahead of the world's clock.", out issue);
			}
			if (intent != null && !StepClock(book, intent.BeforeTick))
				return Refuse("The frozen option claims an unproved active-step checkpoint.", out issue);
			if (!StepClock(book, rawLastTick))
				return Refuse("The saved checkpoint is not the active step's anchor or proved terminal tick.", out issue);
			return true;
		}

		private static bool StepClock(KingdomSubsidenceStepBook book, long observed)
		{
			return observed == book.Active.AnchorTick
				|| KingdomSubsidenceStepRules.TryCheckpoint(book, observed, out long target) && target == observed;
		}

		private static bool Refuse(string reason, out string issue)
		{
			issue = reason + " Saved evidence is retained.";
			return false;
		}
	}
}
