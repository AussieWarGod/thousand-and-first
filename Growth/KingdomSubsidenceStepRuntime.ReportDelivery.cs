using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private static bool ResumeReport(DriverFrame frame, KingdomSubsidenceReportPlan report,
			Func<KingdomSubsidenceReportPlan, bool> save, out string refusal)
		{
			if (frame == null) { refusal = "Subsidence telling has no exact settlement owner."; return false; }
			return ResumeReport(frame.Owner, () => DriverExact(frame), report, save, out refusal);
		}

		private static bool ResumeReport(OptionFrame owner, Func<bool> ownerExact, KingdomSubsidenceReportPlan report,
			Func<KingdomSubsidenceReportPlan, bool> save, out string refusal)
		{
			refusal = "Subsidence telling paused: its exact ledger or Chronicle delivery cannot be proved. Saved evidence is retained.";
			if (owner == null || ownerExact == null || save == null || !ownerExact() || !KingdomSubsidenceReportRules.Valid(report)) return false;
			KingdomLedger ledger = owner.System.Ledger;
			List<string> notes = ledger?.Notes;
			if (notes == null) return false;
			Func<bool> exact = () => ownerExact()
				&& ReferenceEquals(owner.System.Ledger, ledger) && ReferenceEquals(ledger.Notes, notes);
			for (int i = 0; i < report.Entries.Count; i++)
			{
				KingdomSubsidenceReportEntry entry = report.Entries[i];
				if (entry.ChronicleProved || entry.ChronicleLost || entry.CapacityRefused) continue;
				if (!exact()) return false;
				if (entry.LedgerPhase == ReportLedgerPhase.Prepared)
				{
					if (!KingdomSubsidenceReportRules.TryArmLedger(report, i, notes,
						out KingdomSubsidenceReportPlan armed) || !save(armed)) return false;
					report = armed; entry = report.Entries[i];
				}
				if (entry.LedgerPhase == ReportLedgerPhase.Intent)
				{
					KingdomSubsidenceEffectAction action = KingdomSubsidenceReportRules.LedgerAction(report, i, notes);
					if (!exact()) return false;
					KingdomSubsidenceReportPlan proved;
					if (action == KingdomSubsidenceEffectAction.Refuse)
					{
						if (!KingdomSubsidenceReportRules.TryLoseLedger(report, i, notes, false, out proved)) return false;
					}
					else
					{
						if (action == KingdomSubsidenceEffectAction.Apply) ledger.Note(entry.LedgerText);
						if (!exact() || !KingdomSubsidenceReportRules.TryProveLedger(report, i, notes, out proved)) return false;
					}
					if (!exact() || !save(proved)) return false;
					report = proved; entry = report.Entries[i];
				}
				string eventId = KingdomSubsidenceReportRules.EventId(report, i);
				if (!exact()) return false;
				if (KingdomChronicle.TryObserveCapacityRefusalAt(owner.System, eventId,
					entry.Text, entry.AtTick, exact, out KingdomChronicle.CapacityObservation capacity))
				{
					if (!KingdomSubsidenceReportRules.TryPublishCapacity(report, i, capacity.Witness,
						() => exact() && KingdomChronicle.ReproveCapacityRefusal(capacity, exact), save,
						out KingdomSubsidenceReportPlan refused)) return false;
					report = refused;
					continue;
				}
				KingdomChronicle.RecordOnceAt(owner.System, eventId, entry.Text, entry.AtTick, exact);
				if (!exact()) return false;
				KingdomSubsidenceReportPlan told;
				if (KingdomChronicle.TryProveOnceAt(owner.System, eventId, entry.Text, entry.AtTick))
				{
					if (!KingdomSubsidenceReportRules.TryProveChronicle(report, i, out told)) return false;
				}
				else if (!KingdomChronicle.TryProveLostOnceAt(owner.System, eventId, entry.Text, entry.AtTick)
					|| !KingdomSubsidenceReportRules.TryLoseChronicle(report, i, out told)) return false;
				if (!exact() || !save(told)) return false;
				report = told;
			}
			if (!exact() || !KingdomSubsidenceReportRules.Settled(report)) return false;
			refusal = null; return true;
		}
	}
}
