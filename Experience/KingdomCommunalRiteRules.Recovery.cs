using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCommunalRiteRules
	{
		public static bool TryRecoverReady(KingdomCommunalRiteBook book,
			long expectedRevision, string practiceId, string eventId, long projectionTick,
			out KingdomCommunalRiteReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!TryValidate(book, out failure)) return false;
			int index = PracticeIndex(book, practiceId);
			if (index < 0 || book.Rows[index].EventId != eventId
				|| book.Rows[index].Phase != KingdomCommunalRitePhase.Suppressed
				|| expectedRevision != book.Revision || book.Revision == long.MaxValue
				|| projectionTick < book.Rows[index].EventTick)
				return Fail("ready communal-rite recovery CAS refused", out failure);
			KingdomCommunalRiteBook next = Clone(book);
			next.Rows[index].Phase = KingdomCommunalRitePhase.Attended;
			next.Rows[index].ProjectionTick = projectionTick; next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); receipt = Copy(next.Rows[index]); return true;
		}
	}
}
