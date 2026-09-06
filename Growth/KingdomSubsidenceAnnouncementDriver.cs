using System;

namespace ThousandAndFirst
{
	internal interface IKingdomSubsidenceAnnouncementPort
	{
		KingdomSubsidenceStepBook Book { get; }
		bool Exact { get; }
		bool Announced { get; }
		bool Publish(KingdomSubsidenceStepBook expected, KingdomSubsidenceStepBook next);
		bool WriteFlag(bool before, bool after);
		void Message(string text);
		bool Report(KingdomSubsidenceReportPlan report, Func<KingdomSubsidenceReportPlan, bool> save, out string refusal);
	}

	/// <summary>Production-used announcement coordinator. Every external attempt follows a
	/// saved intent; an uninspectable queue attempt is retained, never retried or called seen.</summary>
	internal static class KingdomSubsidenceAnnouncementDriver
	{
		internal static bool Resume(IKingdomSubsidenceAnnouncementPort port, out string refusal)
		{
			bool complete = ResumeCore(port, out refusal);
			if (!complete && string.IsNullOrEmpty(refusal))
				refusal = "Subsidence cannot prove its saved announcement; evidence is retained.";
			return complete;
		}
		private static bool ResumeCore(IKingdomSubsidenceAnnouncementPort port, out string refusal)
		{
			refusal = "Subsidence waits for its saved begin or arrest telling; evidence is retained.";
			try
			{
				if (port == null || !port.Exact || !Read(port, out var value)) return false;
				if (value.Active == null) { refusal = null; return true; }
				var op = value.Active;
				if (op.FlagProved)
				{
					if (port.Announced != op.After || !port.Exact) return false;
				}
				else
				{
					if (port.Announced != op.Before && port.Announced != op.After || !port.Exact
						|| !port.WriteFlag(op.Before, op.After) || !port.Exact || port.Announced != op.After) return false;
					var before = port.Book;
					if (!KingdomSubsidenceAnnouncementRules.TryProveFlag(before, port.Announced, out var proved)
						|| !Save(port, before, proved) || !Read(port, out value)) return false;
					op = value.Active;
				}
				if (op.Notice == KingdomSubsidenceNoticePhase.Pending)
				{
					if (!Notice(port, KingdomSubsidenceNoticePhase.Intent)) return false;
					bool returned = false;
					try { if (!port.Exact) return false; port.Message(op.Message); returned = true; }
					catch (Exception) { }
					if (!port.Exact || port.Announced != op.After || !Notice(port, returned
						? KingdomSubsidenceNoticePhase.Returned : KingdomSubsidenceNoticePhase.Unconfirmed)) return false;
				}
				else if (op.Notice == KingdomSubsidenceNoticePhase.Intent)
				{
					if (!Notice(port, KingdomSubsidenceNoticePhase.Unconfirmed)) return false;
				}
				if (!Read(port, out value) || value.Active == null || !port.Exact || port.Announced != value.Active.After
					|| !KingdomSubsidenceReportCodec.TryDecode(value.Active.ReportModel, out var report)) return false;
				if (!KingdomSubsidenceReportRules.Settled(report))
				{
					if (!port.Report(report, next =>
					{
						var before = port.Book;
						return KingdomSubsidenceAnnouncementRules.TryReport(before, next, out var saved) && Save(port, before, saved);
					}, out refusal)) return false;
				}
				if (!port.Exact || !Read(port, out value) || value.Active == null || port.Announced != value.Active.After) return false;
				if (value.Active.Notice == KingdomSubsidenceNoticePhase.Unconfirmed)
				{
					refusal = "A subsidence notice had an interrupted queue attempt. Read the homecoming report to inspect and acknowledge it; the saved telling is retained.";
					return false;
				}
				var prior = port.Book;
				refusal = "Subsidence cannot retire its telling until every failed report is retained. Read homecoming warnings if their archive is full.";
				if (!KingdomSubsidenceAnnouncementRules.TryRetire(prior, out var retired) || !Save(port, prior, retired)) return false;
				refusal = null; return true;
			}
			catch (Exception)
			{ refusal = "Subsidence stopped while checking its saved announcement; no receipt was discarded."; return false; }
		}
		private static bool Notice(IKingdomSubsidenceAnnouncementPort port, KingdomSubsidenceNoticePhase phase)
		{
			var prior = port.Book;
			return KingdomSubsidenceAnnouncementRules.TryNotice(prior, phase, out var next) && Save(port, prior, next);
		}
		private static bool Read(IKingdomSubsidenceAnnouncementPort port, out KingdomSubsidenceAnnouncement value)
		{
			value = null;
			return port.Exact && KingdomSubsidenceStepRules.Valid(port.Book)
				&& KingdomSubsidenceAnnouncementCodec.TryDecode(port.Book.AnnouncementModel, out value);
		}
		private static bool Save(IKingdomSubsidenceAnnouncementPort port, KingdomSubsidenceStepBook prior, KingdomSubsidenceStepBook next)
		{
			return port.Exact && ReferenceEquals(port.Book, prior) && KingdomSubsidenceStepRules.Valid(next)
				&& port.Publish(prior, next) && port.Exact && ReferenceEquals(port.Book, next);
		}
	}
}
