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

		internal static string DirectRecordText(KingdomPolityDirectRecord Row)
		{
			if (Row == null) return "an unreadable company report is held for inspection.";
			if (Row.RecordId != null && Row.RecordId.StartsWith(
				KingdomPolityDispatchRules.AggregatePrefix,
				global::System.StringComparison.Ordinal)) return Row.EndpointVerb + ".";
			switch (Row.Purpose)
			{
			case KingdomPolityCohortPurpose.Guard:
				return "the watch retained its exact gate duty: " + Row.EndpointVerb + ".";
			case KingdomPolityCohortPurpose.Patrol:
				return "the road watch retained its exact boundary report: " + Row.EndpointVerb + ".";
			case KingdomPolityCohortPurpose.Courier:
				return "the current deed was retained as correspondence: " + Row.EndpointVerb + ".";
			case KingdomPolityCohortPurpose.Trader:
				return "the route manifest was retained with no wares or trade: " + Row.EndpointVerb + ".";
			case KingdomPolityCohortPurpose.Migrant:
				return "the request was retained without admitting a resident: " + Row.EndpointVerb + ".";
			default: return "an exact company report was retained without projecting a body.";
			}
		}
	}
}
