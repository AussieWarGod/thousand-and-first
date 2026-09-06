using System;
using System.Globalization;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private sealed class AnnouncementPort : IKingdomSubsidenceAnnouncementPort
		{
			private readonly OptionFrame Owner;
			private readonly long Now;
			private readonly Func<bool> Additional;
			private readonly object[] Tables;
			internal AnnouncementPort(OptionFrame owner, long now, Func<bool> additional = null)
			{
				Owner = owner; Now = now; Additional = additional;
				Tables = new object[] { owner.Game.StringGameState, owner.Game.IntGameState,
					owner.Game.Int64GameState, owner.Game.ObjectGameState, owner.Game.BooleanGameState };
			}
			public KingdomSubsidenceStepBook Book { get { return Owner.Owner.Step; } }
			public bool Announced { get { return Owner.System.SubsidenceAnnounced; } }
			public bool Exact
			{
				get
				{
					foreach (object table in Tables) if (table == null) return false;
					return ExecutionExact(Owner, Now) && (Additional == null || Additional())
						&& AnnouncementFlagExact(Owner.System, Book)
						&& ReferenceEquals(Tables[0], Owner.Game.StringGameState) && ReferenceEquals(Tables[1], Owner.Game.IntGameState)
						&& ReferenceEquals(Tables[2], Owner.Game.Int64GameState) && ReferenceEquals(Tables[3], Owner.Game.ObjectGameState)
						&& ReferenceEquals(Tables[4], Owner.Game.BooleanGameState);
				}
			}
			public bool Publish(KingdomSubsidenceStepBook expected, KingdomSubsidenceStepBook next)
			{ return Exact && ReferenceEquals(Book, expected) && SaveExecutingOption(Owner, next, Now) && Exact; }
			public bool WriteFlag(bool before, bool after)
			{
				if (!Exact || !KingdomSubsidenceAnnouncementCodec.TryDecode(Book.AnnouncementModel, out var value)
					|| value.Active == null || value.Active.Before != before || value.Active.After != after) return false;
				if (Announced != before && Announced != after) return false;
				Owner.System.SubsidenceAnnounced = after;
				return Exact && Announced == after;
			}
			public void Message(string text)
			{
				if (!Exact || !KingdomSubsidenceAnnouncementCodec.TryDecode(Book.AnnouncementModel, out var value)
					|| value.Active == null || value.Active.Notice != KingdomSubsidenceNoticePhase.Intent
					|| text != value.Active.Message) throw new InvalidOperationException("The saved subsidence notice changed.");
				MessageQueue.AddPlayerMessage(text);
			}
			public bool Report(KingdomSubsidenceReportPlan report, Func<KingdomSubsidenceReportPlan, bool> save, out string refusal)
			{ return ResumeReport(Owner, () => Exact, report, save, out refusal); }
		}

		internal static bool TryResumeAnnouncement(KingdomSystem system, long now, out string refusal)
		{
			refusal = "Subsidence waits for its saved begin or arrest telling.";
			try
			{
				return TryExecutionFrame(system, now, out OptionFrame owner, out refusal)
					&& KingdomSubsidenceAnnouncementDriver.Resume(new AnnouncementPort(owner, now), out refusal);
			}
			catch (Exception) { refusal = "The saved subsidence telling could not be proved; evidence is retained."; return false; }
		}

		internal static bool TryTell(KingdomSystem system, Zone zone, KingdomSurvey survey, long now,
			bool announced, string binding, int level, out string refusal)
		{
			bool complete = TryTellCore(system, zone, survey, now, announced, binding, level, out refusal);
			if (!complete && string.IsNullOrEmpty(refusal))
				refusal = "Subsidence cannot prove its saved transition telling; evidence is retained.";
			return complete;
		}
		private static bool TryTellCore(KingdomSystem system, Zone zone, KingdomSurvey survey, long now,
			bool announced, string binding, int level, out string refusal)
		{
			refusal = "Subsidence waits to freeze its exact begin or arrest telling.";
			try
			{
				if (!TryDriverFrame(system, zone, survey, now, out DriverFrame frame, out refusal)) return false;
				var port = new AnnouncementPort(frame.Owner, now, () => DriverExact(frame));
				if (!port.Exact) return false;
				if (KingdomSubsidenceAnnouncementRules.Pending(port.Book)
					&& !KingdomSubsidenceAnnouncementDriver.Resume(port, out refusal)) return false;
				if (system.SubsidenceAnnounced == announced) { refusal = null; return true; }
				if (port.Book.Admission != KingdomSubsidenceAdmission.Admitted)
				{
					if (!KingdomSubsidenceStepRules.TryAdmit(port.Book, frame.Owner.Realm, frame.Owner.Settlement, out var admitted)
						|| !SaveExecutingOption(frame.Owner, admitted, now, true) || !port.Exact) return false;
				}
				bool before = system.SubsidenceAnnounced; int population = system.Population;
				if (system.SupportedLevel != level || system.SubsidenceBinding != binding) return false;
				string realm = KingdomPresentation.Rich(system.KingdomDisplayName);
				if (!port.Exact || system.Population != population || system.SubsidenceAnnounced != before
					|| system.SupportedLevel != level || system.SubsidenceBinding != binding) return false;
				string note = announced ? KingdomSubsidenceRules.BeganNote(realm, binding, level, population)
					: KingdomSubsidenceRules.ArrestedNote(realm, level, population);
				string text = announced ? KingdomSubsidenceRules.BeganChronicle(realm, binding, level)
					: KingdomSubsidenceRules.ArrestedChronicle(realm, level);
				string message = (announced ? "{{r|" : "{{G|") + note + "}}";
				KingdomSubsidenceStepBook prior = port.Book;
				if (!KingdomSubsidenceAnnouncementRules.TryPrepare(prior, before, announced, now, message, text, out var pending)
					|| !port.Exact || !port.Publish(prior, pending)) return false;
				return KingdomSubsidenceAnnouncementDriver.Resume(port, out refusal);
			}
			catch (Exception) { refusal = "Subsidence could not freeze its telling; saved evidence is retained."; return false; }
		}

		private static bool AnnouncementFlagExact(KingdomSystem system, KingdomSubsidenceStepBook book)
		{
			if (system == null || !KingdomSubsidenceAnnouncementCodec.TryDecode(book.AnnouncementModel, out var value)) return false;
			var op = value.Active;
			return op == null || !op.FlagProved || system.SubsidenceAnnounced == op.After;
		}

		private static string AnnouncementWarning(KingdomSubsidenceStepBook book)
		{
			if (!KingdomSubsidenceStepRules.Valid(book)
				|| !KingdomSubsidenceAnnouncementCodec.TryDecode(book.AnnouncementModel, out var value)) return null;
			var op = value.Active;
			if (op == null || op.Notice != KingdomSubsidenceNoticePhase.Intent && op.Notice != KingdomSubsidenceNoticePhase.Unconfirmed) return "";
			return "\n\n{{W|Unconfirmed subsidence notice}}\nThe queue attempt was interrupted. Reading acknowledges this saved warning,"
				+ " not proof that the notice was displayed.\n" + op.Message + "\nSaved tick: " + op.AtTick.ToString(CultureInfo.InvariantCulture)
				+ ". Evidence: " + op.Id + ".\n";
		}

	}
}
