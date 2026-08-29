using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomVocationServiceRules
	{
		internal static bool TryMigrateLegacy(KingdomVocationServiceBook book,
			out string failure)
		{
			failure = null;
			if (!TryValidateLegacy(book, out failure)) return false;
			List<KingdomVocationServiceReceipt> ordered = new List<KingdomVocationServiceReceipt>();
			for (int i = 0; i < book.Rows.Count; i++) ordered.Add(CopyReceipt(book.Rows[i]));
			ordered.Sort(CompareLegacyCadence);
			KingdomVocationServiceBook migrated = new KingdomVocationServiceBook
				{ Revision = book.Revision };
			for (int i = 0; i < ordered.Count; i++)
			{
				KingdomVocationServiceRequest old = ordered[i].Request;
				long cadence = CountSeries(migrated.Rows, old.SettlementId, old.Vocation);
				KingdomVocationServiceRequest request = new KingdomVocationServiceRequest
				{
					SettlementId = old.SettlementId, Vocation = old.Vocation, Kind = old.Kind,
					SourceReceiptId = old.SourceReceiptId,
					SourceDescription = old.SourceDescription,
					ResultText = LegacyResult(ordered[i]),
					InputUnits = 0, CadenceOrdinal = cadence,
					RequestedTick = old.RequestedTick
				};
				request.SinkReceiptId = SinkId(request);
				request.Digest = RequestDigest(request);
				migrated.Rows.Add(new KingdomVocationServiceReceipt
				{
					Version = KingdomVocationServiceReceipt.CurrentVersion,
					ServiceId = ServiceId(request), Request = request,
					Verb = OfferVerb(request.Kind), OutputText = ReceiptOutput(request),
					OutputUnits = 0, CompletedTick = ordered[i].CompletedTick
				});
			}
			migrated.Rows.Sort((left, right) => string.CompareOrdinal(left.ServiceId, right.ServiceId));
			if (!TryValidate(migrated, out failure)) return false;
			book.Rows.Clear();
			for (int i = 0; i < migrated.Rows.Count; i++) book.Rows.Add(migrated.Rows[i]);
			return true;
		}

		internal static bool TryMigratePrior(KingdomVocationServiceBook book,
			out string failure)
		{
			failure = null;
			if (!TryValidatePrior(book, out failure)) return false;
			KingdomVocationServiceBook migrated = new KingdomVocationServiceBook
				{ Revision = book.Revision };
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomVocationServiceReceipt oldRow = book.Rows[i];
				KingdomVocationServiceRequest old = oldRow.Request;
				KingdomVocationServiceRequest request = new KingdomVocationServiceRequest
				{
					SettlementId = old.SettlementId, Vocation = old.Vocation, Kind = old.Kind,
					SourceReceiptId = old.SourceReceiptId,
					SourceDescription = old.SourceDescription,
					ResultText = PriorResult(old), InputUnits = 0,
					CadenceOrdinal = old.CadenceOrdinal, RequestedTick = old.RequestedTick
				};
				request.SinkReceiptId = SinkId(request);
				request.Digest = RequestDigest(request);
				migrated.Rows.Add(new KingdomVocationServiceReceipt
				{
					Version = KingdomVocationServiceReceipt.CurrentVersion,
					ServiceId = ServiceId(request), Request = request,
					Verb = OfferVerb(request.Kind), OutputText = ReceiptOutput(request),
					OutputUnits = 0, CompletedTick = oldRow.CompletedTick
				});
			}
			migrated.Rows.Sort((left, right) => string.CompareOrdinal(left.ServiceId, right.ServiceId));
			if (!TryValidate(migrated, out failure)) return false;
			book.Rows.Clear();
			for (int i = 0; i < migrated.Rows.Count; i++) book.Rows.Add(migrated.Rows[i]);
			return true;
		}

#if TAF_TESTS
		internal static bool TryDowngradeLegacy(KingdomVocationServiceBook book,
			out KingdomVocationServiceBook legacy, out string failure)
		{
			legacy = null; failure = null;
			if (!TryValidate(book, out failure)) return false;
			KingdomVocationServiceBook candidate = new KingdomVocationServiceBook
				{ Revision = book.Revision };
			for (int i = 0; i < book.Rows.Count; i++)
				{
					KingdomVocationServiceReceipt row = book.Rows[i];
					KingdomVocationServiceRequest request = CopyRequest(row.Request);
					request.ResultText = null;
					request.SinkReceiptId = SinkIdPrior(request);
					request.InputUnits = 1;
					request.Digest = RequestDigestPrior(request);
				candidate.Rows.Add(new KingdomVocationServiceReceipt
				{
					Version = KingdomVocationServiceReceipt.LegacyVersion,
					ServiceId = ServiceId(request), Request = request,
					Verb = LegacyVerb(request.Kind), OutputText = LegacyOutput(request),
					OutputUnits = 1, CompletedTick = row.CompletedTick
				});
			}
			candidate.Rows.Sort((left, right) => string.CompareOrdinal(left.ServiceId, right.ServiceId));
			if (!TryValidateLegacy(candidate, out failure)) return false;
			legacy = candidate;
			return true;
		}

		internal static bool TryDowngradePrior(KingdomVocationServiceBook book,
			out KingdomVocationServiceBook prior, out string failure)
		{
			prior = null; failure = null;
			if (!TryValidate(book, out failure)) return false;
			KingdomVocationServiceBook candidate = new KingdomVocationServiceBook
				{ Revision = book.Revision };
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomVocationServiceReceipt row = book.Rows[i];
				KingdomVocationServiceRequest request = CopyRequest(row.Request);
				request.ResultText = null; request.SinkReceiptId = SinkIdPrior(request);
				request.Digest = RequestDigestPrior(request);
				candidate.Rows.Add(new KingdomVocationServiceReceipt
				{
					Version = KingdomVocationServiceReceipt.PriorVersion,
					ServiceId = ServiceId(request), Request = request,
					Verb = OfferVerb(request.Kind), OutputText = PriorReceiptOutput(request),
					OutputUnits = 0, CompletedTick = row.CompletedTick
				});
			}
			candidate.Rows.Sort((left, right) => string.CompareOrdinal(left.ServiceId, right.ServiceId));
			if (!TryValidatePrior(candidate, out failure)) return false;
			prior = candidate; return true;
		}
#endif

		private static bool TryValidateLegacy(KingdomVocationServiceBook book,
			out string failure)
		{
			failure = null;
			if (book == null || book.Revision < 0L || book.Rows == null ||
				book.Rows.Count > PriorMaxRows)
				return Fail("legacy vocation service book is invalid", out failure);
			string prior = null;
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomVocationServiceReceipt row = book.Rows[i];
				if (row == null || row.Version != KingdomVocationServiceReceipt.LegacyVersion ||
					!ValidLegacyRequest(row.Request) || row.ServiceId != ServiceId(row.Request) ||
					prior != null && string.CompareOrdinal(prior, row.ServiceId) >= 0 ||
					row.Verb != LegacyVerb(row.Request.Kind) ||
					row.OutputText != LegacyOutput(row.Request) || row.OutputUnits != 1 ||
					row.CompletedTick < row.Request.RequestedTick)
					return Fail("legacy vocation service row is invalid", out failure);
				prior = row.ServiceId;
			}
			return true;
		}

		private static bool ValidLegacyRequest(KingdomVocationServiceRequest request) =>
			request != null && Id(request.SettlementId) && request.Kind == KindFor(request.Vocation) &&
			request.Kind != KingdomVocationServiceKind.None && Id(request.SourceReceiptId) &&
			SourceText(request.SourceDescription) && Id(request.SinkReceiptId) &&
			request.InputUnits == 1 && request.CadenceOrdinal >= 0L &&
			request.RequestedTick >= 0L && Digest(request.Digest) &&
			request.ResultText == null && request.Digest == RequestDigestPrior(request);

		private static bool TryValidatePrior(KingdomVocationServiceBook book,
			out string failure)
		{
			failure = null;
			if (book == null || book.Revision < 0L || book.Rows == null ||
				book.Rows.Count > PriorMaxRows)
				return Fail("prior vocation service book is invalid", out failure);
			string prior = null;
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomVocationServiceReceipt row = book.Rows[i];
				if (row == null || row.Version != KingdomVocationServiceReceipt.PriorVersion ||
					!ValidPriorRequest(row.Request) || row.ServiceId != ServiceId(row.Request) ||
					prior != null && string.CompareOrdinal(prior, row.ServiceId) >= 0 ||
					row.Verb != OfferVerb(row.Request.Kind) ||
					row.OutputText != PriorReceiptOutput(row.Request) || row.OutputUnits != 0 ||
					row.CompletedTick < row.Request.RequestedTick)
					return Fail("prior vocation service row is invalid", out failure);
				prior = row.ServiceId;
				for (int j = 0; j < i; j++)
				{
					if (SameSource(book.Rows[j].Request, row.Request))
						return Fail("prior vocation source was recorded twice", out failure);
					if (SameCadence(book.Rows[j].Request, row.Request))
						return Fail("prior vocation cadence was recorded twice", out failure);
				}
				int earlier = 0;
				for (int j = 0; j < book.Rows.Count; j++)
					if (SameSeries(book.Rows[j].Request, row.Request) &&
						book.Rows[j].Request.CadenceOrdinal < row.Request.CadenceOrdinal) earlier++;
				if (earlier != row.Request.CadenceOrdinal)
					return Fail("prior vocation cadence contains a gap", out failure);
			}
			return true;
		}

		private static bool ValidPriorRequest(KingdomVocationServiceRequest request) =>
			request != null && Id(request.SettlementId) && request.Kind == KindFor(request.Vocation) &&
			request.Kind != KingdomVocationServiceKind.None && Id(request.SourceReceiptId) &&
			SourceText(request.SourceDescription) && request.ResultText == null &&
			Id(request.SinkReceiptId) && request.SinkReceiptId == SinkIdPrior(request) &&
			request.InputUnits == 0 && request.CadenceOrdinal >= 0L &&
			request.CadenceOrdinal < PriorMaxRows && request.RequestedTick >= 0L &&
			Digest(request.Digest) && request.Digest == RequestDigestPrior(request);

		private static int CompareLegacyCadence(KingdomVocationServiceReceipt left,
			KingdomVocationServiceReceipt right)
		{
			int order = string.CompareOrdinal(left.Request.SettlementId, right.Request.SettlementId);
			if (order != 0) return order;
			order = string.CompareOrdinal(left.Request.Vocation, right.Request.Vocation);
			if (order != 0) return order;
			order = left.Request.RequestedTick.CompareTo(right.Request.RequestedTick);
			return order != 0 ? order : string.CompareOrdinal(left.ServiceId, right.ServiceId);
		}

		private static long CountSeries(IList<KingdomVocationServiceReceipt> rows,
			string settlement, string vocation)
		{
			long count = 0L;
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].Request.SettlementId == settlement &&
					rows[i].Request.Vocation == vocation) count++;
			return count;
		}

		private static string LegacyVerb(KingdomVocationServiceKind kind) =>
			kind == KingdomVocationServiceKind.RouteBrief ? "Ask for a route brief" :
			kind == KingdomVocationServiceKind.SanctuaryTitle ? "Register sanctuary" :
			kind == KingdomVocationServiceKind.ProvenanceReading ?
				"Request a provenance reading" : null;

		private static string LegacyOutput(KingdomVocationServiceRequest request) =>
			request.Kind == KingdomVocationServiceKind.RouteBrief ?
				"Route brief: " + request.SourceDescription :
			request.Kind == KingdomVocationServiceKind.SanctuaryTitle ?
				"Sanctuary registered: " + request.SourceDescription :
				"Provenance reading: " + request.SourceDescription;

		private static string LegacyResult(KingdomVocationServiceReceipt row) =>
			row.OutputText;

		private static string PriorResult(KingdomVocationServiceRequest request) =>
			request.Kind == KingdomVocationServiceKind.RouteBrief
				? "Legacy route brief: " + request.SourceDescription + "." :
			request.Kind == KingdomVocationServiceKind.SanctuaryTitle
				? "Legacy sanctuary title: " + request.SourceDescription + "." :
			"Legacy provenance reading: " + request.SourceDescription + ".";

		private static string PriorReceiptOutput(KingdomVocationServiceRequest request) =>
			"Source " + request.SourceReceiptId + ": " + request.SourceDescription +
			". Sink " + request.SinkReceiptId + ". Cadence " +
			(request.CadenceOrdinal + 1L).ToString(global::System.Globalization.CultureInfo.InvariantCulture) +
			"/16. Closure: durable history only; 0 input, 0 output, no continuing effect.";

		private static string RequestDigestPrior(KingdomVocationServiceRequest request) =>
			Hash(request?.SettlementId, request?.Vocation,
				Number((byte)(request == null ? KingdomVocationServiceKind.None : request.Kind)),
				request?.SourceReceiptId, request?.SourceDescription, request?.SinkReceiptId,
				Number(request?.InputUnits), Number(request?.CadenceOrdinal),
				Number(request?.RequestedTick));

		private static string SinkIdPrior(KingdomVocationServiceRequest request) =>
			"taf:vocation-sink:" + Hash(request?.SettlementId, request?.Vocation,
				Number((byte)(request == null ? KingdomVocationServiceKind.None : request.Kind)),
				request?.SourceReceiptId, request?.SourceDescription,
				Number(request?.CadenceOrdinal));
	}
}
