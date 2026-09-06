using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private static bool ResumeRungReport(DriverFrame frame, out string refusal)
		{
			refusal = "The settlement's lost rung awaits its exact dated telling.";
			KingdomSubsidenceStepBook book = frame.Owner.Owner.Step;
			KingdomSubsidenceStepOperation op = book.Active;
			if (op.RungModel == KingdomSubsidenceStepRules.NoRungs) { refusal = null; return true; }
			if (!KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan rung)
				|| !KingdomSubsidenceRungRules.ReleasedComplete(rung)) return false;
			KingdomSubsidenceReportPlan report;
			if (op.RungReportModel == KingdomSubsidenceBatchRules.PendingReport)
			{
				string name;
				if (book.BatchModel == KingdomSubsidenceBatchRules.None)
					name = KingdomPresentation.Rich(frame.Owner.System.KingdomDisplayName);
				else
				{
					if (!KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch)) return false;
					name = batch.Name;
				}
				List<KingdomSubsidenceReportEntry> entries = new List<KingdomSubsidenceReportEntry>();
				entries.Add(ReportEntry(name + " ceased to be a " + rung.From.ToString().ToLowerInvariant()
					+ " and became a " + rung.To.ToString().ToLowerInvariant() + " again", rung.DueTick, false));
				int ruined = 0, named = 0, deepest = 0;
				foreach (KingdomSubsidenceRungWork work in rung.Works)
				{
					if (work.AfterWear <= work.BeforeWear) continue;
					if (KingdomSubsidenceRules.TellsRuin(ruined++))
					{
						entries.Add(ReportEntry(KingdomSubsidenceRules.RuinedWorkLine(work.Name, name), rung.DueTick, true));
						named++;
					}
					deepest = Math.Max(deepest, work.AfterWear);
				}
				string summary = KingdomSubsidenceRules.RuinSummary(name, ruined, named, deepest);
				if (summary != null) entries.Add(ReportEntry(summary, rung.DueTick, true));
				report = new KingdomSubsidenceReportPlan(op.Id, book.RealmId, book.SettlementId, entries);
				if (!SaveRungReport(frame, report)) return false;
			}
			else if (!KingdomSubsidenceReportCodec.TryDecode(op.RungReportModel, out report)) return false;
			return ResumeReport(frame, report, next => SaveRungReport(frame, next), out refusal);
		}

		private static bool ResumeBatchReport(DriverFrame frame, KingdomSubsidenceBatch batch, out string refusal)
		{
			refusal = "The settlement's departures await their saved summary.";
			if (!DriverExact(frame) || !batch.Closing || frame.Owner.Owner.Step.Active != null) return false;
			if (batch.ReportModel == KingdomSubsidenceBatchRules.PendingReport)
			{
				string text = KingdomSubsidenceRules.SlideDepartureSummary(batch.Name, batch.Departed,
					KingdomSubsidenceBatchRules.Named(batch), KingdomSubsidenceRules.DepartureCause(batch.Binding));
				string wire = KingdomSubsidenceBatchRules.NoReport;
				if (text != null && !KingdomSubsidenceReportCodec.TryEncode(new KingdomSubsidenceReportPlan(
					batch.Id, batch.RealmId, batch.SettlementId,
					new[] { ReportEntry(text, batch.ClosedTick, true) }), out wire)) return false;
				batch = batch.Copy(reportModel: wire);
				if (!SaveBatch(frame, batch)) return false;
			}
			if (batch.ReportModel != KingdomSubsidenceBatchRules.NoReport)
			{
				if (!KingdomSubsidenceReportCodec.TryDecode(batch.ReportModel, out KingdomSubsidenceReportPlan report)
					|| !ResumeReport(frame, report, next =>
					{
						if (!KingdomSubsidenceReportCodec.TryEncode(next, out string wire)) return false;
						KingdomSubsidenceBatch changed = batch.Copy(reportModel: wire);
						if (!SaveBatch(frame, changed)) return false;
						batch = changed; return true;
					}, out refusal)) return false;
			}
			refusal = "Failed subsidence reports could not be retained. Read the homecoming report to acknowledge existing warnings; saved evidence is retained.";
			if (!KingdomSubsidenceReportArchive.TryRetain(frame.Owner.Owner.Step, batch.ReportModel,
				out KingdomSubsidenceStepBook retained)
				|| !SaveDriver(frame, retained.WithBatch(KingdomSubsidenceBatchRules.None))) return false;
			refusal = null; return true;
		}

		private static KingdomSubsidenceReportEntry ReportEntry(string text, long atTick, bool ledger)
		{
			return new KingdomSubsidenceReportEntry(text,
				ledger ? "{{r|" + XRL.Language.Grammar.InitCap(text) + ".}}" : "", atTick);
		}

		private static bool SaveRungReport(DriverFrame frame, KingdomSubsidenceReportPlan report)
		{
			KingdomSubsidenceStepBook book = frame.Owner.Owner.Step;
			return book.Active != null && KingdomSubsidenceReportCodec.TryEncode(report, out string wire)
				&& SaveDriver(frame, book.With(book.Active.Copy(rungReportModel: wire), book.Sequence));
		}
	}
}
