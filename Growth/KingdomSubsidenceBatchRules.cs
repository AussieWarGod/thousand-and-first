using System;
using System.Globalization;

namespace ThousandAndFirst
{
	internal static class KingdomSubsidenceBatchRules
	{
		internal const string None = "sb1:none";
		internal const string NoReport = "st1:none";
		internal const string PendingReport = "st1:pending";
		internal const string Prefix = "taf:subsidence-batch:v1:";

		internal static bool Valid(KingdomSubsidenceBatch batch)
		{
			if (batch == null || !KingdomIdentityRules.IsRealmId(batch.RealmId)
				|| !KingdomIdentityRules.IsSettlementId(batch.SettlementId) || !Text(batch.Name, 512)
				|| batch.Name.Length == 0 || batch.Binding != "water" && batch.Binding != "roof"
				|| batch.FirstSequence <= 0 || batch.AnchorTick < 0 || batch.ThroughTick <= batch.AnchorTick
				|| (batch.ThroughTick - batch.AnchorTick) % KingdomSubsidenceStepRules.StepTicks != 0
				|| (batch.ThroughTick - batch.AnchorTick) / KingdomSubsidenceStepRules.StepTicks
					> KingdomSubsidenceRules.MaxSteps
				|| batch.Wanted < 1 || batch.Wanted > KingdomRules.MaxPopulation
				|| batch.Departed < 0 || batch.Departed > batch.Wanted
				|| !batch.Closing && batch.Departed == batch.Wanted
				|| !batch.Closing && (batch.ClosedTick != 0 || batch.ReportModel != PendingReport)
				|| batch.Closing && batch.ClosedTick < batch.AnchorTick) return false;
			try
			{
				if (batch.Id != Id(batch)) return false;
				if (!batch.Closing || batch.ReportModel == PendingReport) return true;
				if (batch.ReportModel == NoReport) return batch.Departed == Named(batch);
				return KingdomSubsidenceReportCodec.TryDecode(batch.ReportModel, out KingdomSubsidenceReportPlan report)
					&& report.OwnerId == batch.Id && report.RealmId == batch.RealmId
					&& report.SettlementId == batch.SettlementId && batch.Departed > Named(batch)
					&& report.Entries.Count == 1 && KingdomSubsidenceStepRules.ReportDates(report, batch.ClosedTick);
			}
			catch { return false; }
		}

		internal static bool TryBegin(KingdomSubsidenceStepBook book, long anchor, long through,
			int wanted, string name, string binding, out KingdomSubsidenceBatch batch)
		{
			batch = null;
			if (!KingdomSubsidenceStepRules.Valid(book) || book.Active != null
				|| book.Admission != KingdomSubsidenceAdmission.Admitted || book.Sequence == long.MaxValue
				|| book.OptionModel != KingdomSubsidenceStepRules.NoOption || book.BatchModel != None
				|| anchor < book.LastRetiredTick) return false;
			KingdomSubsidenceBatch value = new KingdomSubsidenceBatch("", book.RealmId, book.SettlementId,
				name, binding, book.Sequence + 1, anchor, through, wanted, 0, false, 0, PendingReport);
			try
			{
				value = new KingdomSubsidenceBatch(Id(value), value.RealmId, value.SettlementId,
					name, binding, value.FirstSequence, anchor, through, wanted, 0, false, 0, PendingReport);
				if (!Valid(value)) return false;
				batch = value; return true;
			}
			catch { return false; }
		}

		internal static bool MatchesShape(KingdomSubsidenceBatch batch, KingdomSubsidenceStepBook book)
		{
			if (!Valid(batch) || book == null || book.Admission != KingdomSubsidenceAdmission.Admitted
				|| book.RealmId != batch.RealmId || book.SettlementId != batch.SettlementId
				|| book.Sequence < batch.FirstSequence - 1
				|| book.Sequence - (batch.FirstSequence - 1) > KingdomSubsidenceRules.MaxSteps) return false;
			KingdomSubsidenceStepOperation op = book.Active;
			long started = book.Sequence - (batch.FirstSequence - 1);
			long retired = started - (op == null ? 0 : 1);
			if (retired < 0 || batch.Departed > retired * 5
				|| !batch.Closing && batch.Departed < retired) return false;
			long offset = retired * KingdomSubsidenceStepRules.StepTicks;
			if (batch.AnchorTick > long.MaxValue - offset) return false;
			long checkpoint = batch.AnchorTick + offset;
			if (retired == 0 ? book.LastRetiredTick > batch.AnchorTick
				: batch.Closing ? book.LastRetiredTick < checkpoint || batch.ClosedTick != book.LastRetiredTick
					: book.LastRetiredTick != checkpoint) return false;
			if (op == null) return !batch.Closing || batch.ClosedTick ==
				(retired == 0 ? batch.AnchorTick : book.LastRetiredTick);
			return !batch.Closing && book.Sequence >= batch.FirstSequence
				&& op.AnchorTick == checkpoint && op.DueTick <= batch.ThroughTick
				&& op.BindingSupport == batch.Binding
				&& op.Completed <= batch.Wanted - batch.Departed
				&& op.Quota <= batch.Wanted - batch.Departed;
		}

		internal static bool TryRetire(KingdomSubsidenceBatch batch, KingdomSubsidenceStepBook prior,
			long target, out KingdomSubsidenceBatch next)
		{
			next = null;
			if (!MatchesShape(batch, prior) || prior.Active == null
				|| !KingdomSubsidenceStepRules.TryCheckpoint(prior, target, out long expected)
				|| expected != target) return false;
			KingdomSubsidenceStepOperation op = prior.Active;
			int departed = batch.Departed + op.Completed;
			bool closing = op.CancelRequested || target >= batch.ThroughTick || departed >= batch.Wanted;
			next = batch.Copy(departed: departed, closing: closing, closedTick: closing ? target : 0);
			return Valid(next);
		}

		internal static bool TryClose(KingdomSubsidenceBatch batch, KingdomSubsidenceStepBook book,
			long checkpoint, out KingdomSubsidenceBatch next)
		{
			next = null;
			if (!MatchesShape(batch, book) || book.Active != null || checkpoint < batch.AnchorTick
				|| checkpoint != (book.Sequence < batch.FirstSequence ? batch.AnchorTick : book.LastRetiredTick))
				return false;
			if (batch.Closing)
			{
				if (batch.ClosedTick != checkpoint) return false;
				next = batch; return true;
			}
			next = batch.Copy(closing: true, closedTick: checkpoint);
			return Valid(next);
		}

		internal static int Named(KingdomSubsidenceBatch batch)
		{
			int count = 0;
			for (int i = 0; i < batch.Departed; i++)
				if (KingdomSubsidenceRules.TellsDeparture(i, batch.Wanted)) count++;
			return count;
		}

		private static string Id(KingdomSubsidenceBatch batch)
		{
			return KingdomPolityRules.ActivationId(Prefix, "subsidence-batch-v1", batch.RealmId,
				batch.SettlementId, batch.Name, batch.Binding,
				batch.FirstSequence.ToString(CultureInfo.InvariantCulture),
				batch.AnchorTick.ToString(CultureInfo.InvariantCulture),
				batch.ThroughTick.ToString(CultureInfo.InvariantCulture), batch.Wanted.ToString(CultureInfo.InvariantCulture));
		}

		private static bool Text(string value, int max)
		{
			if (value == null || value.Length > max) return false;
			for (int i = 0; i < value.Length; i++)
			{
				if (char.IsControl(value[i])) return false;
				if (char.IsSurrogate(value[i]))
				{
					if (!char.IsHighSurrogate(value[i]) || i + 1 >= value.Length
						|| !char.IsLowSurrogate(value[i + 1])) return false;
					i++;
				}
			}
			return true;
		}
	}
}
