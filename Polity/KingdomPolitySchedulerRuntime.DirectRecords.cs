using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolitySchedulerRuntime
	{
		/// <summary>Explicit read surface for a Charter/conversation endpoint. Never pushes prose.</summary>
		internal static List<KingdomPolityDirectRecord> ReadDirectRecordsOnDemand(
			KingdomSystem System, string SettlementId, bool IncludeAcknowledged)
		{
			return KingdomPolityDispatchRules.ReadableDirectRecords(System?.PolityDispatch,
				SettlementId, IncludeAcknowledged);
		}

		internal static bool TryAcknowledgeDirectRecordOnDemand(KingdomSystem System,
			string SettlementId, string RecordId, long Tick, out string Failure)
		{
			KingdomPolityDispatchState state = System?.PolityDispatch;
			return KingdomPolityDispatchRules.TryAcknowledgeDirectRecord(state,
				state?.Revision ?? -1L, RecordId, SettlementId, Tick, out Failure);
		}
	}
}
