using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomCommunalRitePhase : byte
	{
		None = 0,
		Committed = 1,
		Attended = 2,
		Suppressed = 3,
		// Appended to keep every already-frozen v1 terminal byte stable. Prepared is the
		// durable semantic intent cut; no physical happening may be queued from it.
		Prepared = 4
	}

	public enum KingdomCommunalRiteOptionDisposition : byte
	{
		Unreadable = 0,
		Current = 1,
		Disabled = 2,
		SupersededEpoch = 3
	}

	[Serializable]
	public sealed class KingdomCommunalRiteReceipt
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public KingdomCommunalRitePhase Phase;
		public string SettlementId;
		public string PracticeId;
		public string EventId;
		public long EventTick;
		public long EnableEpoch;
		public long ProjectionTick;
	}

	/// <summary>Exact D8 authority. Persistence is owned by the authenticated civic-memory
	/// section host; this book never registers another game system or root field.</summary>
	[Serializable]
	public sealed class KingdomCommunalRiteBook
	{
		public KingdomExperienceSchemaState SchemaState =
			KingdomExperienceSchemaState.Compatible;
		public string SchemaFault;
		public string RealmId;
		public bool IdentityBound;
		public long Revision;
		public List<KingdomCommunalRiteReceipt> Rows =
			new List<KingdomCommunalRiteReceipt>();
		public int OpaqueWireVersion;
		public byte[] OpaqueFuturePayload;
		public byte[] OpaqueEnvelope;
	}
}

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
