using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomVocationServiceRules
	{
		internal static bool TryPrepareRequest(KingdomVocationServiceBook book,
			KingdomVocationServiceOffer offer, long tick,
			out KingdomVocationServiceRequest request, out string failure)
		{
			request = null; failure = null;
			if (!TryValidate(book, out failure) || !TryValidateOffer(offer, out failure))
				return false;
			if (offer.State != KingdomVocationServiceOfferState.Available)
				return Fail("this vocation report offers no executable service", out failure);
			if (tick < 0L) return Fail("vocation service tick is invalid", out failure);
			if (!TryInspect(book, offer, out KingdomVocationServiceStatus status, out failure))
				return false;
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomVocationServiceReceipt existing = book.Rows[i];
				if (!SameSource(existing.Request, offer)) continue;
				if (!OfferMatchesRequest(offer, existing.Request))
					return Fail("the exact source receipt conflicts with durable history", out failure);
				request = CopyRequest(existing.Request);
				return true;
			}
			if (status.State == KingdomVocationServiceActionState.CapacityClosed)
				return Fail(status.SeriesCount >= MaxRowsPerSeries
					? "this city and vocation are at their 16-receipt capacity"
					: "realm vocation-service history is at its 48-receipt capacity", out failure);
			long cadence = 0L;
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].Request.SettlementId == offer.SettlementId &&
					book.Rows[i].Request.Vocation == offer.Vocation) cadence++;
			request = new KingdomVocationServiceRequest
			{
				SettlementId = offer.SettlementId,
				Vocation = offer.Vocation,
					Kind = offer.Kind,
					SourceReceiptId = offer.SourceReceiptId,
					SourceDescription = offer.SourceDescription,
					ResultText = offer.ResultText,
				InputUnits = 0,
				CadenceOrdinal = cadence,
				RequestedTick = tick
			};
			request.SinkReceiptId = SinkId(request);
			request.Digest = RequestDigest(request);
			if (!ValidCurrentRequest(request))
				return Fail("prepared vocation service request is invalid", out failure);
			return true;
		}

		public static bool TryServe(KingdomVocationServiceBook book, long expectedRevision,
			KingdomVocationServiceRequest request, long tick,
			out KingdomVocationServiceReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!TryValidate(book, out failure) || !ValidCurrentRequest(request))
				return Fail(failure ?? "vocation service request is invalid", out failure);
			if (tick < 0L) return Fail("vocation service completion tick is invalid", out failure);
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomVocationServiceReceipt existing = book.Rows[i];
				if (!SameSource(existing.Request, request)) continue;
				if (!SameRequest(existing.Request, request))
					return Fail("the exact source receipt conflicts with durable history", out failure);
				receipt = existing;
				return true;
			}
			if (book.Revision != expectedRevision)
				return Fail("vocation service revision changed before commit", out failure);
			if (tick < request.RequestedTick)
				return Fail("vocation service completion predates its request", out failure);
			if (book.Rows.Count >= MaxRows)
				return Fail("realm vocation-service history is at capacity", out failure);
			if (book.Revision == long.MaxValue)
				return Fail("vocation service revision cannot advance", out failure);
			long expectedCadence = 0L;
			for (int i = 0; i < book.Rows.Count; i++)
				if (SameSeries(book.Rows[i].Request, request)) expectedCadence++;
			if (expectedCadence >= MaxRowsPerSeries)
				return Fail("this city and vocation are at capacity", out failure);
			if (request.CadenceOrdinal != expectedCadence)
				return Fail("vocation service cadence changed before commit", out failure);
			KingdomVocationServiceReceipt appended = new KingdomVocationServiceReceipt
			{
				ServiceId = ServiceId(request),
				Request = CopyRequest(request),
				Verb = OfferVerb(request.Kind),
				OutputText = ReceiptOutput(request),
				OutputUnits = 0,
				CompletedTick = tick
			};
			KingdomVocationServiceBook candidate = CopyBook(book);
			candidate.Rows.Add(appended);
			candidate.Rows.Sort((left, right) =>
				string.CompareOrdinal(left.ServiceId, right.ServiceId));
			candidate.Revision++;
			if (!TryValidate(candidate, out failure)) return false;
			book.Rows.Clear();
			for (int i = 0; i < candidate.Rows.Count; i++) book.Rows.Add(candidate.Rows[i]);
			book.Revision = candidate.Revision;
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].ServiceId == appended.ServiceId) receipt = book.Rows[i];
			return receipt != null || Fail("vocation service commit lost its receipt", out failure);
		}

		internal static bool TryMatchAvailableOffers(KingdomVocationServiceOffer opened,
			KingdomVocationServiceOffer fresh, out string failure)
		{
			failure = null;
			if (!TryValidateOffer(opened, out failure) || !TryValidateOffer(fresh, out failure) ||
				opened.State != KingdomVocationServiceOfferState.Available ||
				fresh.State != KingdomVocationServiceOfferState.Available)
				return Fail(failure ?? "the vocation service is no longer available", out failure);
			return opened.SettlementId == fresh.SettlementId && opened.Vocation == fresh.Vocation &&
				opened.Kind == fresh.Kind && opened.Authority == fresh.Authority &&
				opened.SourceReceiptId == fresh.SourceReceiptId &&
				opened.SourceDescription == fresh.SourceDescription &&
				opened.ResultText == fresh.ResultText ||
				Fail("the exact vocation source changed; read the fresh report", out failure);
		}

		internal static bool TryDescribeHistory(KingdomVocationServiceBook book,
			string settlementId, string vocation, out string text, out string failure)
		{
			text = null; failure = null;
			if (!TryValidate(book, out failure) || !Id(settlementId)) return false;
			if (vocation == "holding")
			{
				text = "No vocation-service history applies to this holding.";
				return true;
			}
			if (KindFor(vocation) == KingdomVocationServiceKind.None)
				return Fail("vocation service history has an unknown vocation", out failure);
			int count = 0;
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].Request.SettlementId == settlementId &&
					book.Rows[i].Request.Vocation == vocation) count++;
			if (count == 0) { text = "No durable vocation-service receipt is recorded here."; return true; }
			StringBuilder value = new StringBuilder("Durable vocation-service history (" +
				count.ToString(CultureInfo.InvariantCulture) + "): ");
			int shown = 0;
			for (long cadence = count - 1L; cadence >= 0L; cadence--)
			{
				KingdomVocationServiceReceipt row = FindCadence(book, settlementId, vocation, cadence);
				if (row == null) return Fail("vocation history cadence is unreadable", out failure);
				string addition = (value[value.Length - 1] == ' ' ? "" : " | ") + row.OutputText;
				if (!Utf8(value.ToString() + addition, MaxOfferTextBytes - 96)) break;
				value.Append(addition);
				shown++;
			}
			if (shown < count) value.Append(" | ").Append(
				(count - shown).ToString(CultureInfo.InvariantCulture))
				.Append(" older receipt(s) remain durably retained.");
			text = value.ToString();
			return OfferText(text) || Fail("vocation service history exceeds its view cap", out failure);
		}

		internal static string ReceiptOutput(KingdomVocationServiceRequest request) =>
			request.ResultText + " Source " + request.SourceReceiptId + ": " +
			request.SourceDescription + ". Sink " + request.SinkReceiptId +
			". Occurrence " +
			(request.CadenceOrdinal + 1L).ToString(CultureInfo.InvariantCulture) +
			"/16 for this city and vocation. Realm retention is bounded at 48. " +
			"Closure: durable information/title only; 0 material input, 0 material output, " +
			"no passive effect.";

		private static KingdomVocationServiceReceipt FindCadence(KingdomVocationServiceBook book,
			string settlement, string vocation, long cadence)
		{
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].Request.SettlementId == settlement &&
					book.Rows[i].Request.Vocation == vocation &&
					book.Rows[i].Request.CadenceOrdinal == cadence) return book.Rows[i];
			return null;
		}

		private static bool OfferMatchesRequest(KingdomVocationServiceOffer offer,
			KingdomVocationServiceRequest request) => offer.SettlementId == request.SettlementId &&
			offer.Vocation == request.Vocation && offer.Kind == request.Kind &&
			offer.SourceReceiptId == request.SourceReceiptId &&
			offer.SourceDescription == request.SourceDescription &&
			offer.ResultText == request.ResultText;

		private static bool SameRequest(KingdomVocationServiceRequest left,
			KingdomVocationServiceRequest right) => left != null && right != null &&
			left.SettlementId == right.SettlementId && left.Vocation == right.Vocation &&
			left.Kind == right.Kind && left.SourceReceiptId == right.SourceReceiptId &&
			left.SourceDescription == right.SourceDescription && left.ResultText == right.ResultText &&
			left.SinkReceiptId == right.SinkReceiptId &&
			left.InputUnits == right.InputUnits && left.CadenceOrdinal == right.CadenceOrdinal &&
			left.RequestedTick == right.RequestedTick && left.Digest == right.Digest;

		private static bool SameSeries(KingdomVocationServiceRequest left,
			KingdomVocationServiceRequest right) => left.SettlementId == right.SettlementId &&
			left.Vocation == right.Vocation;
		private static bool SameCadence(KingdomVocationServiceRequest left,
			KingdomVocationServiceRequest right) => SameSeries(left, right) &&
			left.CadenceOrdinal == right.CadenceOrdinal;
		private static bool SameSource(KingdomVocationServiceRequest left,
			KingdomVocationServiceRequest right) => left != null && right != null &&
			left.SettlementId == right.SettlementId && left.Vocation == right.Vocation &&
			left.Kind == right.Kind && left.SourceReceiptId == right.SourceReceiptId;
		private static bool SameSource(KingdomVocationServiceRequest left,
			KingdomVocationServiceOffer right) => left != null && right != null &&
			left.SettlementId == right.SettlementId && left.Vocation == right.Vocation &&
			left.Kind == right.Kind && left.SourceReceiptId == right.SourceReceiptId;

		private static string RequestDigest(KingdomVocationServiceRequest request) =>
			Hash(request?.SettlementId, request?.Vocation,
				Number((byte)(request == null ? KingdomVocationServiceKind.None : request.Kind)),
				request?.SourceReceiptId, request?.SourceDescription, request?.ResultText,
				request?.SinkReceiptId,
				Number(request?.InputUnits), Number(request?.CadenceOrdinal),
				Number(request?.RequestedTick));

		private static string SinkId(KingdomVocationServiceRequest request) =>
			"taf:vocation-sink:" + Hash(request?.SettlementId, request?.Vocation,
				Number((byte)(request == null ? KingdomVocationServiceKind.None : request.Kind)),
				request?.SourceReceiptId, request?.SourceDescription, request?.ResultText,
				Number(request?.CadenceOrdinal));
		private static string ServiceId(KingdomVocationServiceRequest request) =>
			"taf:vocation-service:" + Hash(request?.Digest);

		private static KingdomVocationServiceRequest CopyRequest(KingdomVocationServiceRequest value) =>
			value == null ? null : new KingdomVocationServiceRequest
			{
					SettlementId = value.SettlementId, Vocation = value.Vocation,
					SourceReceiptId = value.SourceReceiptId, SourceDescription = value.SourceDescription,
					ResultText = value.ResultText,
				SinkReceiptId = value.SinkReceiptId, Kind = value.Kind, InputUnits = value.InputUnits,
				CadenceOrdinal = value.CadenceOrdinal, RequestedTick = value.RequestedTick,
				Digest = value.Digest
			};

		private static KingdomVocationServiceBook CopyBook(KingdomVocationServiceBook value)
		{
			KingdomVocationServiceBook copy = new KingdomVocationServiceBook { Revision = value.Revision };
			for (int i = 0; i < value.Rows.Count; i++) copy.Rows.Add(CopyReceipt(value.Rows[i]));
			return copy;
		}

		private static KingdomVocationServiceReceipt CopyReceipt(KingdomVocationServiceReceipt value) =>
			new KingdomVocationServiceReceipt { Version = value.Version, ServiceId = value.ServiceId,
				Request = CopyRequest(value.Request), Verb = value.Verb, OutputText = value.OutputText,
				OutputUnits = value.OutputUnits, CompletedTick = value.CompletedTick };

		private static string Number(long? value) => value.HasValue
			? value.Value.ToString(CultureInfo.InvariantCulture) : "";
		private static string Hash(params string[] parts)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{
					for (int i = 0; i < parts.Length; i++) writer.Write(parts[i] ?? "");
					writer.Flush();
					using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(
						sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
				}
			}
			catch (EncoderFallbackException) { return null; }
		}

		private static bool Utf8(string value, int maxBytes)
		{
			try { return value != null && value.IndexOf('\0') < 0 &&
				new UTF8Encoding(false, true).GetByteCount(value) <= maxBytes; }
			catch (EncoderFallbackException) { return false; }
		}
		private static bool Id(string value) => value != null &&
			value.StartsWith("taf:", StringComparison.Ordinal) && Utf8(value, MaxIdBytes);
		private static bool SourceText(string value) => !string.IsNullOrWhiteSpace(value) &&
			Utf8(value, MaxSourceTextBytes);
		internal static bool ResultText(string value) => !string.IsNullOrWhiteSpace(value) &&
			Utf8(value, MaxResultTextBytes);
		private static bool Text(string value) => !string.IsNullOrWhiteSpace(value) &&
			Utf8(value, MaxTextBytes);
		private static bool OfferText(string value) => !string.IsNullOrWhiteSpace(value) &&
			Utf8(value, MaxOfferTextBytes);
		private static bool Digest(string value) => value != null && value.Length == 64 &&
			Array.TrueForAll(value.ToCharArray(), c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
		private static bool Fail(string text, out string failure) { failure = text; return false; }
	}
}
