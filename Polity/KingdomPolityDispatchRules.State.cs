using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDispatchRules
	{
		internal static KingdomPolityDispatchState CloneState(KingdomPolityDispatchState Source)
		{
			if (Source == null) return null;
			KingdomPolityDispatchState copy = new KingdomPolityDispatchState();
			CopyState(copy, Source); return copy;
		}

		internal static void CopyState(KingdomPolityDispatchState Target,
			KingdomPolityDispatchState Source)
		{
			Target.Version = Source.Version; Target.RealmId = Source.RealmId;
			Target.Revision = Source.Revision; Target.HasWindow = Source.HasWindow;
			Target.LastWindowOrdinal = Source.LastWindowOrdinal;
			Target.WindowCauseTick = Source.WindowCauseTick;
			Target.FutureCauseFloorTick = Source.FutureCauseFloorTick;
			Target.EndpointDigest = Source.EndpointDigest;
			Target.EndpointCount = Source.EndpointCount;
			Target.CompletedMask = Source.CompletedMask;
			Target.DirectRecords = new List<KingdomPolityDirectRecord>();
			for (int i = 0; i < (Source.DirectRecords?.Count ?? 0); i++)
				Target.DirectRecords.Add(Source.DirectRecords[i].Copy());
			Target.Fault = Source.Fault;
		}

		internal static bool SameState(KingdomPolityDispatchState A,
			KingdomPolityDispatchState B)
		{
			if (A == null || B == null || A.Version != B.Version || A.RealmId != B.RealmId
				|| A.Revision != B.Revision || A.HasWindow != B.HasWindow
				|| A.LastWindowOrdinal != B.LastWindowOrdinal
				|| A.WindowCauseTick != B.WindowCauseTick
				|| A.FutureCauseFloorTick != B.FutureCauseFloorTick
				|| A.EndpointDigest != B.EndpointDigest || A.EndpointCount != B.EndpointCount
				|| A.CompletedMask != B.CompletedMask || A.Fault != B.Fault
				|| (A.DirectRecords?.Count ?? -1) != (B.DirectRecords?.Count ?? -1)) return false;
			for (int i = 0; i < A.DirectRecords.Count; i++)
				if (!SameStoredRecord(A.DirectRecords[i], B.DirectRecords[i], true)) return false;
			return true;
		}

		internal static bool TryCommitState(KingdomPolityDispatchState State,
			KingdomPolityDispatchState Candidate, long ExpectedRevision, out string Failure)
		{
			Failure = null;
			if (State == null || Candidate == null || State.Revision != ExpectedRevision)
				return Fail("polity dispatch lost its compare-and-swap", out Failure);
			if (!ValidState(Candidate, out Failure)) return false;
			CopyState(State, Candidate); return true;
		}
	}
}
