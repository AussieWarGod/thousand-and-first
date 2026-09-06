using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private sealed class HomecomingFrame
		{
			internal OptionFrame Owner;
			internal KingdomLedger Ledger;
			internal List<string> Notes, Brinks, Expeditions;
			internal string[] BrinkValues, ExpeditionValues;
			internal int[] Counts;
			internal int Days, NoteCount;
			internal string NoteHash;
			internal string DepartureWarnings;
			internal bool Announced;
		}

		internal static bool TryReadHomecoming(KingdomSystem system, Action<string> show,
			out string refusal)
		{
			refusal = "The homecoming report changed or its saved account could not be proved. Its news and evidence are retained.";
			try
			{
				if (!KingdomResidentDeathRuntime.TryRecoverPending(system, out string deathFailure))
				{ refusal = deathFailure; return false; }
				if (show == null || !TryOptionFrame(system, out OptionFrame owner)) return false;
				KingdomLedger ledger = system.Ledger;
				if (ledger == null || ledger.BrinkLines == null || ledger.ExpeditionLines == null
					|| ledger.BrinkLines.Count > KingdomLedger.MaxBrinkLines
					|| ledger.ExpeditionLines.Count > Simulation.City.KingdomJobRules.MaxOpenJobs
					|| system.HomecomingDays < 0
					|| !KingdomSubsidenceReportRules.TryHash(ledger.Notes, out string noteHash)) return false;
				HomecomingFrame frame = new HomecomingFrame
				{
					Owner = owner, Ledger = ledger, Notes = ledger.Notes, NoteCount = ledger.Notes.Count,
					NoteHash = noteHash, Days = system.HomecomingDays, Counts = HomecomingCounts(ledger), Announced = system.SubsidenceAnnounced,
					DepartureWarnings = system.ResidentDepartureCapacityWarnings,
					Brinks = ledger.BrinkLines, BrinkValues = ledger.BrinkLines.ToArray(),
					Expeditions = ledger.ExpeditionLines, ExpeditionValues = ledger.ExpeditionLines.ToArray()
				};
				if (!HomecomingExact(frame)) return false;
				string failures = owner.Owner.Step.FailureModel;
				string warnings = KingdomSubsidenceReportArchive.Digest(owner.Owner.Step);
				string noticeWarning = AnnouncementWarning(owner.Owner.Step);
				if (!KingdomResidentDepartureCapacityArchive.TryPrepareRead(frame.DepartureWarnings,
					owner.Realm, owner.Settlement, out string departureWarnings, out string acknowledgedDepartures)) return false;
				if (!HomecomingExact(frame) || warnings == null || noticeWarning == null
					|| (failures == KingdomSubsidenceReportArchive.None) != (warnings.Length == 0)) return false;
				if (!ledger.Any && failures == KingdomSubsidenceReportArchive.None && noticeWarning.Length == 0 && departureWarnings.Length == 0)
				{
					show("Nothing has happened here since you last stood on this ground.");
					if (!HomecomingExact(frame)) return false;
					refusal = null; return true;
				}
				string digest = ledger.Digest(system.SeatName, frame.Days);
				digest += warnings + noticeWarning + departureWarnings;
				if (!HomecomingExact(frame)) return false;
				show(digest);
				if (!HomecomingExact(frame)
					|| !TryPrepareHomecoming(owner.Owner.Step, frame.Notes, out KingdomSubsidenceStepBook next)) return false;
				if (failures != KingdomSubsidenceReportArchive.None)
					next = next.WithFailures(KingdomSubsidenceReportArchive.None);
				// Both report lanes and the exact acknowledged archive enter one guarded publication.
				if (!KingdomSubsidenceStepRules.Valid(next)
					|| !KingdomSubsidenceStepCodec.TryEncode(next, out string wire)
					|| !HomecomingExact(frame)) return false;
				if (wire != owner.Owner.Wire && !SaveOption(owner, next)) return false;
				if (!HomecomingExact(frame)) return false;
				// Reset is non-virtual BCL bookkeeping; no external callback follows this barrier.
				system.ResidentDepartureCapacityWarnings = acknowledgedDepartures;
				ledger.Reset();
				system.HomecomingDays = 0;
				refusal = null; return true;
			}
			catch (Exception) { return false; }
		}

		private static bool HomecomingExact(HomecomingFrame frame)
		{
			if (frame == null || !OptionExact(frame.Owner)
				|| !string.Equals(frame.Owner.System.ResidentDepartureCapacityWarnings, frame.DepartureWarnings, StringComparison.Ordinal)
				|| frame.Owner.System.SubsidenceAnnounced != frame.Announced
				|| !AnnouncementFlagExact(frame.Owner.System, frame.Owner.Owner.Step)
				// The realm's departure journal can still own a reset counter, even after parent credit.
				|| !KingdomResidentDepartureRules.IsEmpty(frame.Owner.System.ResidentDeparture)
				|| !ReferenceEquals(frame.Owner.System.Ledger, frame.Ledger)
				|| !ReferenceEquals(frame.Ledger.Notes, frame.Notes)
				|| !ReferenceEquals(frame.Ledger.BrinkLines, frame.Brinks)
				|| !ReferenceEquals(frame.Ledger.ExpeditionLines, frame.Expeditions)
				|| frame.Owner.System.HomecomingDays != frame.Days || frame.Notes.Count != frame.NoteCount
				|| !KingdomSubsidenceReportRules.TryHash(frame.Notes, out string hash)
				|| !string.Equals(hash, frame.NoteHash, StringComparison.Ordinal)
				|| !HomecomingListExact(frame.Brinks, frame.BrinkValues)
				|| !HomecomingListExact(frame.Expeditions, frame.ExpeditionValues)) return false;
			int[] counts = HomecomingCounts(frame.Ledger);
			for (int i = 0; i < counts.Length; i++) if (counts[i] != frame.Counts[i]) return false;
			return true;
		}

		private static int[] HomecomingCounts(KingdomLedger ledger)
		{
			return new[] { ledger.Fetched, ledger.UpkeepDrawn, ledger.ArrivalCost, ledger.Delivered,
				ledger.Harvested, ledger.Milled, ledger.Foraged, ledger.RationsDrawn, ledger.HarvestLost,
				ledger.Plundered, ledger.Arrivals, ledger.Departures };
		}

		private static bool HomecomingListExact(List<string> current, string[] prior)
		{
			if (current.Count != prior.Length) return false;
			for (int i = 0; i < prior.Length; i++)
				if (!string.Equals(current[i], prior[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool TryPrepareHomecoming(KingdomSubsidenceStepBook book, IList<string> notes,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!KingdomSubsidenceStepRules.Valid(book)) return false;
			KingdomSubsidenceStepBook value = book;
			if (book.Active != null)
			{
				if (!TrySettleHomecomingReport(book.Active.RungReportModel, book.Active.Id, book,
					notes, out string rung)) return false;
				if (rung != book.Active.RungReportModel)
					value = value.With(book.Active.Copy(rungReportModel: rung), book.Sequence);
			}
			if (book.BatchModel != KingdomSubsidenceBatchRules.None)
			{
				if (!KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch)) return false;
				if (batch.Closing)
				{
					if (!TrySettleHomecomingReport(batch.ReportModel, batch.Id, book, notes, out string report)) return false;
					if (report != batch.ReportModel)
					{
						if (!KingdomSubsidenceBatchCodec.TryEncode(batch.Copy(reportModel: report), out string wire)) return false;
						value = value.WithBatch(wire);
					}
				}
			}
			if (!KingdomSubsidenceAnnouncementRules.TryAcknowledgeHomecoming(value, notes, out value)
				|| !KingdomSubsidenceStepRules.Valid(value)) return false;
			next = value; return true;
		}

		private static bool TrySettleHomecomingReport(string wire, string ownerId, KingdomSubsidenceStepBook book,
			IList<string> notes, out string next)
		{
			next = wire;
			if (wire == KingdomSubsidenceBatchRules.NoReport || wire == KingdomSubsidenceBatchRules.PendingReport) return true;
			if (!KingdomSubsidenceReportCodec.TryDecode(wire, out KingdomSubsidenceReportPlan report)
				|| report.OwnerId != ownerId || report.RealmId != book.RealmId
				|| report.SettlementId != book.SettlementId) return false;
			int intent = -1;
			for (int i = 0; i < report.Entries.Count; i++)
				if (report.Entries[i].LedgerPhase == ReportLedgerPhase.Intent)
				{
					if (intent != -1) return false;
					intent = i;
				}
			if (intent == -1) return true;
			KingdomSubsidenceReportPlan settled;
			bool ready = KingdomSubsidenceReportRules.LedgerAction(report, intent, notes) == KingdomSubsidenceEffectAction.Confirm
				? KingdomSubsidenceReportRules.TryProveLedger(report, intent, notes, out settled)
				: KingdomSubsidenceReportRules.TryLoseLedger(report, intent, notes, true, out settled);
			return ready && KingdomSubsidenceReportCodec.TryEncode(settled, out next);
		}
	}
}
