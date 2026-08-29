using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolitySchedulerRuntime
	{
		private static bool TryOrderFair(KingdomSystem System,
			List<KingdomPolityDueWork> Work, out string Failure)
		{
			Failure = null; List<KingdomExperienceAdmissionCandidate> requests =
				new List<KingdomExperienceAdmissionCandidate>();
			for (int i = 0; i < Work.Count; i++)
			{
				if (!KingdomExperienceRules.TryReadBodyLease(System.Experience,
					KingdomPolityExperienceRuntime.BodyReservationId(Work[i].CohortId),
					out KingdomExperienceBodyReservation lease,
					out KingdomExperienceLeaseState leaseState, out Failure)) return false;
				requests.Add(new KingdomExperienceAdmissionCandidate
				{
					Lane = KingdomExperienceLane.PolityCohort,
					SettlementId = Work[i].SettlementId, SourceId = Work[i].SourceRef,
					CauseTick = Work[i].CauseTick, WindowOrdinal = Work[i].WindowOrdinal,
					BodyCount = Work[i].MemberCount, HasDirectFallback = true,
					ExactRetry = leaseState == KingdomExperienceLeaseState.Active
						&& lease?.SourceId == Work[i].CohortId
				});
			}
			if (!KingdomExperienceFairnessRules.TryOrder(requests,
				out List<KingdomExperienceAdmissionCandidate> ordered, out Failure)) return false;
			Dictionary<string, KingdomPolityDueWork> bySource =
				new Dictionary<string, KingdomPolityDueWork>();
			for (int i = 0; i < Work.Count; i++) bySource[Work[i].SourceRef] = Work[i];
			Work.Clear();
			for (int i = 0; i < ordered.Count; i++) Work.Add(bySource[ordered[i].SourceId]);
			return true;
		}
	}
}
