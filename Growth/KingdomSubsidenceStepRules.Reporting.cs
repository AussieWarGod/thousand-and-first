namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRules
	{
		private static bool ValidRungReport(KingdomSubsidenceStepBook book)
		{
			KingdomSubsidenceStepOperation op = book.Active;
			if (op == null) return true;
			if (op.ReachedStage == op.FromStage) return op.RungReportModel == KingdomSubsidenceBatchRules.NoReport;
			if (op.RungReportModel == KingdomSubsidenceBatchRules.PendingReport) return true;
			return op.Phase == KingdomSubsidenceStepPhase.Settling
				&& KingdomSubsidenceRungCodec.TryDecode(op.RungModel, out KingdomSubsidenceRungPlan rung)
				&& KingdomSubsidenceRungRules.PhysicalComplete(rung)
				&& KingdomSubsidenceReportCodec.TryDecode(op.RungReportModel, out KingdomSubsidenceReportPlan report)
				&& report.OwnerId == op.Id && report.RealmId == book.RealmId
				&& report.SettlementId == book.SettlementId && report.Entries.Count >= 1
				&& ReportDates(report, op.DueTick);
		}

		internal static bool ReportDates(KingdomSubsidenceReportPlan report, long tick)
		{
			foreach (KingdomSubsidenceReportEntry entry in report.Entries)
				if (entry.AtTick != tick) return false;
			return true;
		}

		private static bool RungComplete(KingdomSubsidenceStepBook book)
		{
			KingdomSubsidenceStepOperation op = book.Active;
			if (op.RungModel == NoRungs) return op.RungReportModel == KingdomSubsidenceBatchRules.NoReport;
			return TryReadRungPlan(book, out KingdomSubsidenceRungPlan plan)
				&& KingdomSubsidenceRungRules.ReleasedComplete(plan)
				&& KingdomSubsidenceReportCodec.TryDecode(op.RungReportModel, out KingdomSubsidenceReportPlan report)
				&& KingdomSubsidenceReportRules.Settled(report)
				&& KingdomSubsidenceReportArchive.TryRetain(book, op.RungReportModel, out _);
		}
	}
}
