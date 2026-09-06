using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	internal static class KingdomSubsidenceAnnouncementRules
	{
		internal const string Prefix = "taf:subsidence-announcement:v1:";
		internal static bool IsId(string value)
		{ return value != null && value.StartsWith(Prefix, StringComparison.Ordinal)
			&& value.Length == Prefix.Length + 64 && KingdomChronicleReceiptRules.IsSha256(value.Substring(Prefix.Length)); }

		internal static bool Valid(KingdomSubsidenceAnnouncement value)
		{
			if (value == null || value.Ordinal < 0 || value.LastTick < 0) return false;
			if (value.Ordinal == 0) return value.RealmId == "" && value.SettlementId == "" && value.LastTick == 0 && value.Active == null;
			if (!KingdomIdentityRules.IsRealmId(value.RealmId) || !KingdomIdentityRules.IsSettlementId(value.SettlementId)) return false;
			var op = value.Active;
			if (op == null) return true;
			if (op.AtTick < value.LastTick || op.Before == op.After || op.Notice > KingdomSubsidenceNoticePhase.Acknowledged
				|| !KingdomSubsidenceRungRules.Text(op.Message, KingdomChronicleReceiptRules.MaxEntryChars, false)
				|| !KingdomSubsidenceReportCodec.TryDecode(op.ReportModel, out var report)
				|| report.OwnerId != op.Id || report.RealmId != value.RealmId || report.SettlementId != value.SettlementId
				|| report.Entries.Count != 1 || report.Entries[0].AtTick != op.AtTick || report.Entries[0].LedgerText != op.Message) return false;
			if (!op.FlagProved && op.Notice != KingdomSubsidenceNoticePhase.Pending) return false;
			if ((!op.FlagProved || op.Notice == KingdomSubsidenceNoticePhase.Pending || op.Notice == KingdomSubsidenceNoticePhase.Intent)
				&& report.Entries[0].LedgerPhase != ReportLedgerPhase.Prepared) return false;
			try { return op.Id == Id(value.RealmId, value.SettlementId, value.Ordinal, op.After, op.AtTick); }
			catch { return false; }
		}

		internal static bool MatchesShape(KingdomSubsidenceStepBook book)
		{
			if (book == null || !KingdomSubsidenceAnnouncementCodec.TryDecode(book.AnnouncementModel, out var value)) return false;
			if (value.Ordinal == 0) return true;
			return book.Admission == KingdomSubsidenceAdmission.Admitted && value.RealmId == book.RealmId
				&& value.SettlementId == book.SettlementId && (value.Active == null || book.Active == null
					&& book.BatchModel == KingdomSubsidenceBatchRules.None && book.OptionModel == KingdomSubsidenceStepRules.NoOption);
		}
		internal static bool Pending(KingdomSubsidenceStepBook book)
		{ return book != null && KingdomSubsidenceAnnouncementCodec.TryDecode(book.AnnouncementModel, out var value) && value.Active != null; }

		internal static bool TryPrepare(KingdomSubsidenceStepBook book, bool before, bool after, long tick,
			string message, string chronicle, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!KingdomSubsidenceStepRules.Valid(book) || book.Admission != KingdomSubsidenceAdmission.Admitted
				|| before == after || tick < 0 || book.Active != null || book.BatchModel != KingdomSubsidenceBatchRules.None
				|| book.OptionModel != KingdomSubsidenceStepRules.NoOption
				|| !KingdomSubsidenceAnnouncementCodec.TryDecode(book.AnnouncementModel, out var prior)
				|| prior.Active != null || prior.Ordinal == long.MaxValue || tick < prior.LastTick) return false;
			try
			{
				long ordinal = prior.Ordinal + 1; string id = Id(book.RealmId, book.SettlementId, ordinal, after, tick);
				var report = new KingdomSubsidenceReportPlan(id, book.RealmId, book.SettlementId,
					new[] { new KingdomSubsidenceReportEntry(chronicle, message, tick) });
				if (!KingdomSubsidenceReportCodec.TryEncode(report, out string reportWire)) return false;
				var op = new KingdomSubsidenceAnnouncementOperation(id, tick, before, after, message,
					reportWire, false, KingdomSubsidenceNoticePhase.Pending);
				return Store(book, new KingdomSubsidenceAnnouncement(book.RealmId, book.SettlementId, ordinal, prior.LastTick, op), out next);
			}
			catch { return false; }
		}

		internal static bool TryProveFlag(KingdomSubsidenceStepBook book, bool observed, out KingdomSubsidenceStepBook next)
		{
			next = null;
			return ReadActive(book, out var value) && observed == value.Active.After
				&& Store(book, value.With(value.Active.Copy(flagProved: true)), out next);
		}
		internal static bool TryNotice(KingdomSubsidenceStepBook book, KingdomSubsidenceNoticePhase target,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!ReadActive(book, out var value) || !value.Active.FlagProved) return false;
			var prior = value.Active.Notice;
			bool valid = prior == KingdomSubsidenceNoticePhase.Pending && target == KingdomSubsidenceNoticePhase.Intent
				|| prior == KingdomSubsidenceNoticePhase.Intent && (target == KingdomSubsidenceNoticePhase.Returned
					|| target == KingdomSubsidenceNoticePhase.Unconfirmed)
				|| (prior == KingdomSubsidenceNoticePhase.Intent || prior == KingdomSubsidenceNoticePhase.Unconfirmed)
					&& target == KingdomSubsidenceNoticePhase.Acknowledged;
			return valid && Store(book, value.With(value.Active.Copy(notice: target)), out next);
		}

		internal static bool TryReport(KingdomSubsidenceStepBook book, KingdomSubsidenceReportPlan report,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!ReadActive(book, out var value) || !value.Active.FlagProved
				|| value.Active.Notice < KingdomSubsidenceNoticePhase.Returned
				|| !KingdomSubsidenceReportCodec.TryDecode(value.Active.ReportModel, out var prior)
				|| !KingdomSubsidenceReportRules.Valid(report) || report.OwnerId != prior.OwnerId
				|| report.RealmId != prior.RealmId || report.SettlementId != prior.SettlementId || report.Entries.Count != 1
				|| !ReportProgress(prior.Entries[0], report.Entries[0])
				|| !KingdomSubsidenceReportCodec.TryEncode(report, out string wire)) return false;
			return Store(book, value.With(value.Active.Copy(report: wire)), out next);
		}

		internal static bool TryRetire(KingdomSubsidenceStepBook book, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!ReadActive(book, out var value) || !value.Active.FlagProved
				|| value.Active.Notice != KingdomSubsidenceNoticePhase.Returned && value.Active.Notice != KingdomSubsidenceNoticePhase.Acknowledged
				|| !KingdomSubsidenceReportArchive.TryRetain(book, value.Active.ReportModel, out var retained)) return false;
			return Store(retained, new KingdomSubsidenceAnnouncement(value.RealmId, value.SettlementId,
				value.Ordinal, value.Active.AtTick, null), out next);
		}

		// Caller must first show the frozen warning and reprove the same ledger and parent.
		// This settles only reset-sensitive intent; no publication, retirement or archive acknowledgement.
		internal static bool TryAcknowledgeHomecoming(KingdomSubsidenceStepBook book, IList<string> notes,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!KingdomSubsidenceStepRules.Valid(book) || !KingdomSubsidenceReportRules.TryHash(notes, out _)
				|| !KingdomSubsidenceAnnouncementCodec.TryDecode(book.AnnouncementModel, out var value)) return false;
			if (value.Active == null) { next = book; return true; }
			var op = value.Active;
			if (op.Notice == KingdomSubsidenceNoticePhase.Intent || op.Notice == KingdomSubsidenceNoticePhase.Unconfirmed)
				if (!TryNotice(book, KingdomSubsidenceNoticePhase.Acknowledged, out book)) return false;
			if (!KingdomSubsidenceReportCodec.TryDecode(op.ReportModel, out var report)) return false;
			if (report.Entries[0].LedgerPhase == ReportLedgerPhase.Intent)
			{
				KingdomSubsidenceReportPlan settled;
				bool ready = KingdomSubsidenceReportRules.LedgerAction(report, 0, notes) == KingdomSubsidenceEffectAction.Confirm
					? KingdomSubsidenceReportRules.TryProveLedger(report, 0, notes, out settled)
					: KingdomSubsidenceReportRules.TryLoseLedger(report, 0, notes, true, out settled);
				if (!ready || !TryReport(book, settled, out book)) return false;
			}
			next = book; return true;
		}

		private static bool ReportProgress(KingdomSubsidenceReportEntry before, KingdomSubsidenceReportEntry after)
		{
			if (after.Text != before.Text || after.LedgerText != before.LedgerText || after.AtTick != before.AtTick
				|| before.ChronicleProved && !after.ChronicleProved || before.ChronicleLost && !after.ChronicleLost
				|| before.CapacityRefused && (!after.CapacityRefused || after.CapacityCount != before.CapacityCount
					|| after.CapacityHash != before.CapacityHash || after.CapacityFingerprint != before.CapacityFingerprint)) return false;
			if (before.LedgerPhase == ReportLedgerPhase.Prepared) return true;
			if (after.LedgerPhase == ReportLedgerPhase.Prepared || before.BeforeCount != after.BeforeCount || before.BeforeHash != after.BeforeHash) return false;
			return before.LedgerPhase == ReportLedgerPhase.Intent || before.LedgerPhase == after.LedgerPhase
				&& before.AfterCount == after.AfterCount && before.AfterHash == after.AfterHash && before.LedgerLoss == after.LedgerLoss;
		}
		private static bool ReadActive(KingdomSubsidenceStepBook book, out KingdomSubsidenceAnnouncement value)
		{
			value = null;
			return KingdomSubsidenceStepRules.Valid(book)
				&& KingdomSubsidenceAnnouncementCodec.TryDecode(book.AnnouncementModel, out value) && value.Active != null;
		}
		private static bool Store(KingdomSubsidenceStepBook book, KingdomSubsidenceAnnouncement value, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!KingdomSubsidenceAnnouncementCodec.TryEncode(value, out string wire)) return false;
			var candidate = book.WithAnnouncement(wire);
			if (!KingdomSubsidenceStepRules.Valid(candidate)) return false;
			next = candidate; return true;
		}
		private static string Id(string realm, string settlement, long ordinal, bool target, long tick)
		{ return KingdomPolityRules.ActivationId(Prefix, "subsidence-announcement-v1", realm, settlement,
			ordinal.ToString(CultureInfo.InvariantCulture), target ? "begin" : "arrest", tick.ToString(CultureInfo.InvariantCulture)); }
	}
}
