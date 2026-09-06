namespace ThousandAndFirst
{
	/// <summary>Returned means the queue call returned, not that a player saw the notice.
	/// An interrupted attempt is never replayed; only a guarded homecoming may acknowledge it.</summary>
	internal enum KingdomSubsidenceNoticePhase : byte { Pending, Intent, Returned, Unconfirmed, Acknowledged }

	internal sealed class KingdomSubsidenceAnnouncement
	{
		internal readonly string RealmId, SettlementId;
		internal readonly long Ordinal, LastTick;
		internal readonly KingdomSubsidenceAnnouncementOperation Active;
		internal KingdomSubsidenceAnnouncement(string realm, string settlement, long ordinal, long lastTick,
			KingdomSubsidenceAnnouncementOperation active)
		{ RealmId = realm; SettlementId = settlement; Ordinal = ordinal; LastTick = lastTick; Active = active; }
		internal KingdomSubsidenceAnnouncement With(KingdomSubsidenceAnnouncementOperation active)
		{ return new KingdomSubsidenceAnnouncement(RealmId, SettlementId, Ordinal, LastTick, active); }
	}

	internal sealed class KingdomSubsidenceAnnouncementOperation
	{
		internal readonly string Id, Message, ReportModel;
		internal readonly long AtTick;
		internal readonly bool Before, After, FlagProved;
		internal readonly KingdomSubsidenceNoticePhase Notice;
		internal KingdomSubsidenceAnnouncementOperation(string id, long tick, bool before, bool after,
			string message, string report, bool flagProved, KingdomSubsidenceNoticePhase notice)
		{
			Id = id; AtTick = tick; Before = before; After = after; Message = message;
			ReportModel = report; FlagProved = flagProved; Notice = notice;
		}
		internal KingdomSubsidenceAnnouncementOperation Copy(string report = null, bool? flagProved = null,
			KingdomSubsidenceNoticePhase? notice = null)
		{ return new KingdomSubsidenceAnnouncementOperation(Id, AtTick, Before, After, Message,
			report ?? ReportModel, flagProved ?? FlagProved, notice ?? Notice); }
	}
}
