namespace ThousandAndFirst
{
	internal sealed class KingdomSubsidenceBatch
	{
		internal readonly string Id, RealmId, SettlementId, Name, Binding, ReportModel;
		internal readonly long FirstSequence, AnchorTick, ThroughTick, ClosedTick;
		internal readonly int Wanted, Departed;
		internal readonly bool Closing;

		internal KingdomSubsidenceBatch(string id, string realmId, string settlementId,
			string name, string binding, long firstSequence, long anchorTick, long throughTick,
			int wanted, int departed, bool closing, long closedTick, string reportModel)
		{
			Id = id; RealmId = realmId; SettlementId = settlementId; Name = name; Binding = binding;
			FirstSequence = firstSequence; AnchorTick = anchorTick; ThroughTick = throughTick;
			Wanted = wanted; Departed = departed; Closing = closing; ClosedTick = closedTick;
			ReportModel = reportModel;
		}

		internal KingdomSubsidenceBatch Copy(int? departed = null, bool? closing = null,
			long? closedTick = null, string reportModel = null)
		{
			return new KingdomSubsidenceBatch(Id, RealmId, SettlementId, Name, Binding, FirstSequence,
				AnchorTick, ThroughTick, Wanted, departed ?? Departed, closing ?? Closing,
				closedTick ?? ClosedTick, reportModel ?? ReportModel);
		}
	}
}
