using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDispatchRules
	{
		internal static bool TerminalizeOpenIntents(KingdomPolityDispatchState State,
			out string Failure)
		{
			Failure = null; List<KingdomPolityDirectRecord> intents =
				new List<KingdomPolityDirectRecord>();
			for (int i = 0; i < State.DirectRecords.Count; i++)
				if (IsKind(State.DirectRecords[i], IntentPrefix)) intents.Add(State.DirectRecords[i]);
			intents.Sort((a, b) => a.AcknowledgedTick.CompareTo(b.AcknowledgedTick));
			for (int i = 0; i < intents.Count; i++)
			{
				int ordinal = (int)(-intents[i].AcknowledgedTick - 1L);
				if (!AddDetailed(State, intents[i], out KingdomPolityDirectRecord _, out Failure))
					return false;
				State.DirectRecords.Remove(intents[i]);
				if (ordinal >= 0 && ordinal < State.EndpointCount)
					State.CompletedMask |= 1 << ordinal;
			}
			return true;
		}

		internal static bool SuppressOpenIntents(KingdomPolityDispatchState State,
			out string Failure)
		{
			Failure = null;
			for (int i = State.DirectRecords.Count - 1; i >= 0; i--)
				if (IsKind(State.DirectRecords[i], IntentPrefix))
					State.DirectRecords.RemoveAt(i);
			State.CompletedMask = State.HasWindow
				? (1 << State.EndpointCount) - 1 : 0;
			return true;
		}

		internal static bool AddDetailed(KingdomPolityDispatchState State,
			KingdomPolityDirectRecord Intent, out KingdomPolityDirectRecord Added,
			out string Failure)
		{
			Added = null; Failure = null;
			KingdomPolityDirectRecord row = new KingdomPolityDirectRecord
			{
				SourceRef = Intent.SourceRef, SettlementId = Intent.SettlementId,
				Purpose = Intent.Purpose, WindowOrdinal = Intent.WindowOrdinal,
				CauseTick = Intent.CauseTick, EndpointVerb = Intent.EndpointVerb
			};
			row.RecordId = StoredId(DirectPrefix, "polity-direct-record-v2", row);
			KingdomPolityDirectRecord existing = FindRecord(State.DirectRecords, row.RecordId);
			if (existing != null)
			{
				if (!SameStoredRecord(existing, row, false))
					return Fail("direct polity fact identity is divergent", out Failure);
				Added = existing.Copy(); return true;
			}
			while (DetailedCount(State.DirectRecords) >= MaximumDirectRecords)
				if (!FoldOldestDetailed(State.DirectRecords, out Failure)) return false;
			State.DirectRecords.Add(row); Added = row.Copy(); return true;
		}

		private static KingdomPolityDirectRecord BuildDirectRecord(KingdomPolityDueWork Work)
		{
			KingdomPolityDirectRecord row = new KingdomPolityDirectRecord
			{
				SourceRef = Work.CohortId, SettlementId = Work.SettlementId,
				Purpose = Work.Purpose, WindowOrdinal = Work.WindowOrdinal,
				CauseTick = Work.CauseTick, EndpointVerb = Work.DueFacts
			};
			row.RecordId = StoredId(DirectPrefix, "polity-direct-record-v2", row); return row;
		}

		private static int DetailedCount(IList<KingdomPolityDirectRecord> Rows)
		{
			int count = 0;
			for (int i = 0; i < Rows.Count; i++) if (IsKind(Rows[i], DirectPrefix)) count++;
			return count;
		}

		private static bool FoldOldestDetailed(List<KingdomPolityDirectRecord> Rows,
			out string Failure)
		{
			Failure = null; KingdomPolityDirectRecord oldest = null;
			KingdomPolityDirectRecord prior = null;
			for (int i = 0; i < Rows.Count; i++)
			{
				if (IsKind(Rows[i], AggregatePrefix)) prior = Rows[i];
				if (!IsKind(Rows[i], DirectPrefix)) continue;
				if (oldest == null || Rows[i].CauseTick < oldest.CauseTick
					|| Rows[i].CauseTick == oldest.CauseTick
					&& string.CompareOrdinal(Rows[i].RecordId, oldest.RecordId) < 0)
					oldest = Rows[i];
			}
			if (oldest == null) return Fail("direct polity cap has no foldable fact", out Failure);
			ulong count = prior == null ? 1UL : prior.WindowOrdinal + 1UL;
			if (count == 0UL) return Fail("direct polity aggregate count is exhausted", out Failure);
			string priorSource = prior?.SourceRef ?? "taf:event:polity-direct-aggregate:origin";
			string countText = count.ToString(CultureInfo.InvariantCulture);
			string purpose = ((byte)oldest.Purpose).ToString(CultureInfo.InvariantCulture);
			string source = KingdomPolityRules.ActivationId(
				"taf:event:polity-direct-aggregate:v1:", "polity-direct-supersession-v1",
				priorSource, oldest.RecordId, countText, purpose);
			KingdomPolityDirectRecord aggregate = new KingdomPolityDirectRecord
			{
				SourceRef = source, Purpose = oldest.Purpose, WindowOrdinal = count,
				CauseTick = Math.Max(prior?.CauseTick ?? 0L, oldest.CauseTick),
				EndpointVerb = "supersession count=" + countText + "; prior=" + priorSource
					+ "; folded=" + oldest.RecordId + "; authority=" + source
			};
			aggregate.RecordId = StoredId(AggregatePrefix,
				"polity-direct-aggregate-v1", aggregate);
			Rows.Remove(oldest); if (prior != null) Rows.Remove(prior); Rows.Add(aggregate);
			return true;
		}

		private static bool ExactAggregate(KingdomPolityDirectRecord Record)
		{
			const string start = "supersession count=";
			const string priorMark = "; prior=";
			const string foldedMark = "; folded=";
			const string authorityMark = "; authority=";
			string value = Record?.EndpointVerb;
			if (value == null || !value.StartsWith(start, StringComparison.Ordinal)) return false;
			int priorAt = value.IndexOf(priorMark, start.Length, StringComparison.Ordinal);
			int foldedAt = value.IndexOf(foldedMark, priorAt + priorMark.Length,
				StringComparison.Ordinal);
			int authorityAt = value.IndexOf(authorityMark, foldedAt + foldedMark.Length,
				StringComparison.Ordinal);
			if (priorAt < 0 || foldedAt < 0 || authorityAt < 0
				|| !ulong.TryParse(value.Substring(start.Length, priorAt - start.Length),
					NumberStyles.None, CultureInfo.InvariantCulture, out ulong count)
				|| count != Record.WindowOrdinal) return false;
			string prior = value.Substring(priorAt + priorMark.Length,
				foldedAt - priorAt - priorMark.Length);
			string folded = value.Substring(foldedAt + foldedMark.Length,
				authorityAt - foldedAt - foldedMark.Length);
			string authority = value.Substring(authorityAt + authorityMark.Length);
			string expected = KingdomPolityRules.ActivationId(
				"taf:event:polity-direct-aggregate:v1:", "polity-direct-supersession-v1",
				prior, folded, count.ToString(CultureInfo.InvariantCulture),
				((byte)Record.Purpose).ToString(CultureInfo.InvariantCulture));
			return KingdomPolityRules.SemanticId(prior)
				&& KingdomPolityRules.TypedId(folded, DirectPrefix) && authority == expected
				&& Record.SourceRef == expected;
		}

		internal static string DirectAuthorityDigest(KingdomPolityDispatchState State)
		{
			List<string> rows = new List<string>();
			for (int i = 0; i < (State?.DirectRecords?.Count ?? 0); i++)
			{
				KingdomPolityDirectRecord r = State.DirectRecords[i];
				if (IsKind(r, RetirementPrefix)) continue;
				rows.Add(string.Join("|", r.RecordId, r.SourceRef ?? "", r.SettlementId ?? "",
					((byte)r.Purpose).ToString(CultureInfo.InvariantCulture),
					r.WindowOrdinal.ToString(CultureInfo.InvariantCulture),
					r.CauseTick.ToString(CultureInfo.InvariantCulture), r.EndpointVerb ?? "",
					r.AcknowledgedTick.ToString(CultureInfo.InvariantCulture)));
			}
			rows.Sort(StringComparer.Ordinal);
			return KingdomPolityRules.ActivationDigest("polity-direct-authority-v1", rows);
		}

		internal static KingdomPolityDirectRecord FindRecord(
			IList<KingdomPolityDirectRecord> Rows, string Id)
		{
			for (int i = 0; i < (Rows?.Count ?? 0); i++) if (Rows[i].RecordId == Id) return Rows[i];
			return null;
		}

		internal static bool SameStoredRecord(KingdomPolityDirectRecord A,
			KingdomPolityDirectRecord B, bool IncludeAcknowledgement)
		{
			return A != null && B != null && A.RecordId == B.RecordId && A.SourceRef == B.SourceRef
				&& A.SettlementId == B.SettlementId && A.Purpose == B.Purpose
				&& A.WindowOrdinal == B.WindowOrdinal && A.CauseTick == B.CauseTick
				&& A.EndpointVerb == B.EndpointVerb
				&& (!IncludeAcknowledgement || A.AcknowledgedTick == B.AcknowledgedTick);
		}

		internal static bool IsKind(KingdomPolityDirectRecord R, string Prefix)
		{
			return R?.RecordId != null && R.RecordId.StartsWith(Prefix, StringComparison.Ordinal);
		}

		internal static void SortRecords(List<KingdomPolityDirectRecord> Rows)
		{
			Rows.Sort((a, b) => string.CompareOrdinal(a.RecordId, b.RecordId));
		}
	}
}
