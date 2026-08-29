using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomVocationServiceRules
	{
		internal static bool TryInspect(KingdomVocationServiceBook book,
			KingdomVocationServiceOffer offer, out KingdomVocationServiceStatus status,
			out string failure)
		{
			status = null; failure = null;
			if (!TryValidate(book, out failure) || !TryValidateOffer(offer, out failure) ||
				offer.State != KingdomVocationServiceOfferState.Available)
				return Fail(failure ?? "only an available vocation source has a record state", out failure);
			int series = 0;
			KingdomVocationServiceReceipt existing = null;
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomVocationServiceReceipt row = book.Rows[i];
				if (row.Request.SettlementId == offer.SettlementId &&
					row.Request.Vocation == offer.Vocation) series++;
				if (!SameSource(row.Request, offer)) continue;
				if (!OfferMatchesRequest(offer, row.Request))
					return Fail("the exact source receipt conflicts with durable history", out failure);
				existing = row;
			}
			KingdomVocationServiceActionState state = existing != null
				? KingdomVocationServiceActionState.AlreadyRecorded :
				series >= MaxRowsPerSeries || book.Rows.Count >= MaxRows
					? KingdomVocationServiceActionState.CapacityClosed :
					KingdomVocationServiceActionState.Available;
			status = new KingdomVocationServiceStatus(state, series, book.Rows.Count,
				existing?.OutputText);
			return true;
		}

		internal static bool TryDescribeRealmResults(KingdomVocationServiceBook book,
			out string text, out string failure)
		{
			return TryDescribeRealmResults(book, 0, out text, out int _, out failure);
		}

		internal static bool TryDescribeRealmResults(KingdomVocationServiceBook book,
			int offset, out string text, out int nextOffset, out string failure)
		{
			text = null; nextOffset = -1; failure = null;
			if (!TryValidate(book, out failure)) return false;
			if (book.Rows.Count == 0)
			{
				text = "No durable vocation-service result is recorded in this realm.";
				return true;
			}
			List<KingdomVocationServiceReceipt> ordered =
				new List<KingdomVocationServiceReceipt>(book.Rows);
			ordered.Sort(CompareNewest);
			if (offset < 0 || offset >= ordered.Count)
				return Fail("realm vocation result page is outside durable history", out failure);
			StringBuilder value = new StringBuilder("Realm vocation-service results (" +
				book.Rows.Count.ToString(CultureInfo.InvariantCulture) + "/" +
				MaxRows.ToString(CultureInfo.InvariantCulture) + "), rows " +
				(offset + 1).ToString(CultureInfo.InvariantCulture) + " onward: ");
			int shown = 0;
			for (int i = offset; i < ordered.Count; i++)
			{
				KingdomVocationServiceReceipt row = ordered[i];
				string addition = (shown == 0 ? "" : " | ") + row.Request.SettlementId +
					" / " + row.Request.Vocation + " / " + row.Request.SourceReceiptId +
					": " + row.Request.ResultText;
				if (!Utf8(value.ToString() + addition, MaxOfferTextBytes - 96)) break;
				value.Append(addition); shown++;
			}
			if (shown == 0) return Fail("one realm vocation result exceeds its view cap", out failure);
			int next = offset + shown;
			if (next < ordered.Count)
			{
				nextOffset = next;
				value.Append(" | More durable result(s) continue on the next page.");
			}
			text = value.ToString();
			return OfferText(text) || Fail("realm vocation results exceed their view cap", out failure);
		}

		private static int CompareNewest(KingdomVocationServiceReceipt left,
			KingdomVocationServiceReceipt right)
		{
			int order = right.CompletedTick.CompareTo(left.CompletedTick);
			return order != 0 ? order : string.CompareOrdinal(right.ServiceId, left.ServiceId);
		}
	}
}
