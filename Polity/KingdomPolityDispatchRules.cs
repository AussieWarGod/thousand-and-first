using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One current-window dispatch. Missed windows become bounded direct facts.</summary>
	public static partial class KingdomPolityDispatchRules
	{
		public const long CalendarDayTicks = 1200L;
		public const int MaximumEndpoints = 3;
		public const int PurposeCount = 5;
		public const long PeriodTicks = CalendarDayTicks * 7L;
		public const long StayTicks = CalendarDayTicks * 2L;

		public static bool TryOpen(KingdomPolityDispatchState State,
			KingdomPolityDispatchOffer Offer, out List<KingdomPolityDueWork> Work,
			out string Failure)
		{
			return TryOpen(State, State?.Revision ?? -1L, Offer, true, out Work, out Failure);
		}

		public static bool TryOpen(KingdomPolityDispatchState State, long ExpectedRevision,
			KingdomPolityDispatchOffer Offer, out List<KingdomPolityDueWork> Work,
			out string Failure)
		{
			return TryOpen(State, ExpectedRevision, Offer, true, out Work, out Failure);
		}

		internal static bool TryOpen(KingdomPolityDispatchState State, long ExpectedRevision,
			KingdomPolityDispatchOffer Offer, bool AdmitNewCauses,
			out List<KingdomPolityDueWork> Work, out string Failure)
		{
			Work = new List<KingdomPolityDueWork>(); Failure = null;
			if (!ValidState(State, out Failure) || !ValidOffer(Offer, out Failure)) return false;
			if (State.RealmId != null && State.RealmId != Offer.RealmId)
				return Fail("polity dispatch belongs to another realm", out Failure);
			if (RetirementRecord(State) != null) return true;
			ulong window = (ulong)(Offer.Tick / PeriodTicks);
			if (State.HasWindow && window < State.LastWindowOrdinal)
				return Fail("polity dispatch clock regressed", out Failure);
			string digest = EndpointDigest(Offer.Endpoints);
			bool admit = AdmitNewCauses && WindowStart(window) >= State.FutureCauseFloorTick;
			if (State.HasWindow && window == State.LastWindowOrdinal
				&& State.EndpointDigest != digest)
				return Fail("open polity topology differs from its frozen facts", out Failure);

			KingdomPolityDispatchState candidate = CloneState(State);
			bool changed = !candidate.HasWindow || window != candidate.LastWindowOrdinal;
			if (changed)
			{
				if (candidate.Revision == long.MaxValue)
					return Fail("polity dispatch revision is exhausted", out Failure);
				if (!(admit ? TerminalizeOpenIntents(candidate, out Failure)
					: SuppressOpenIntents(candidate, out Failure))) return false;
				candidate.RealmId = Offer.RealmId; candidate.HasWindow = true;
				candidate.LastWindowOrdinal = window;
				candidate.WindowCauseTick = WindowStart(window);
				candidate.EndpointDigest = digest;
				candidate.EndpointCount = Offer.Endpoints.Count; candidate.CompletedMask = 0;
				for (int i = 0; admit && i < Offer.Endpoints.Count; i++)
				{
					if (!TryChoose(Offer.RealmId, Offer.Endpoints[i], Offer.Endpoints.Count,
						window, i, candidate.WindowCauseTick, digest,
						out KingdomPolityDueWork row))
					{
						candidate.CompletedMask |= 1 << i; continue;
					}
					candidate.DirectRecords.Add(BuildIntent(row));
				}
				if (!admit) candidate.CompletedMask = (1 << candidate.EndpointCount) - 1;
				SortRecords(candidate.DirectRecords); candidate.Revision++;
				if (!TryCommitState(State, candidate, ExpectedRevision, out Failure)) return false;
			}
			else if (!admit && State.CompletedMask != (1 << State.EndpointCount) - 1)
			{
				if (State.Revision == long.MaxValue)
					return Fail("polity dispatch revision is exhausted", out Failure);
				if (!SuppressOpenIntents(candidate, out Failure)) return false;
				candidate.Revision++; SortRecords(candidate.DirectRecords);
				if (!TryCommitState(State, candidate, ExpectedRevision, out Failure)) return false;
			}
			if (!admit) return ValidState(State, out Failure);
			for (int i = 0; i < Offer.Endpoints.Count; i++)
			{
				if ((State.CompletedMask & 1 << i) != 0) continue;
				if (!TryChoose(Offer.RealmId, Offer.Endpoints[i], Offer.Endpoints.Count,
					window, i, State.WindowCauseTick, digest,
					out KingdomPolityDueWork row) || !ExactOpenWork(State, row))
					return Fail("open polity intent no longer matches its source facts", out Failure);
				Work.Add(row);
			}
			return ValidState(State, out Failure);
		}

		public static bool TryComplete(KingdomPolityDispatchState State, ulong Window,
			int EndpointOrdinal, out string Failure)
		{
			return TryComplete(State, State?.Revision ?? -1L, Window, EndpointOrdinal,
				out Failure);
		}

		public static bool TryComplete(KingdomPolityDispatchState State, long ExpectedRevision,
			ulong Window, int EndpointOrdinal, out string Failure)
		{
			Failure = null;
			if (!ValidState(State, out Failure) || !State.HasWindow
				|| State.LastWindowOrdinal != Window || EndpointOrdinal < 0
				|| EndpointOrdinal >= State.EndpointCount)
				return Fail("polity dispatch completion does not match the open window", out Failure);
			int bit = 1 << EndpointOrdinal;
			if ((State.CompletedMask & bit) != 0) return true;
			if (State.Revision == long.MaxValue)
				return Fail("polity dispatch revision is exhausted", out Failure);
			KingdomPolityDispatchState candidate = CloneState(State);
			KingdomPolityDirectRecord intent = FindIntent(candidate, EndpointOrdinal);
			if (intent == null) return Fail("polity completion lacks its exact intent", out Failure);
			candidate.DirectRecords.Remove(intent); candidate.CompletedMask |= bit;
			candidate.Revision++; SortRecords(candidate.DirectRecords);
			return TryCommitState(State, candidate, ExpectedRevision, out Failure);
		}

		public static bool TryCreateForPurpose(string RealmId, KingdomPolityEndpointFacts Endpoint,
			int EndpointCount, ulong Window, long CauseTick, KingdomPolityCohortPurpose Purpose,
			out KingdomPolityDueWork Work, out string Failure)
		{
			Work = null; Failure = null;
			if (!KingdomPolityRules.TypedId(RealmId, "taf:realm:") || !ValidEndpoint(Endpoint)
				|| EndpointCount < 1 || EndpointCount > MaximumEndpoints || CauseTick < 0L
				|| !AmbientPurpose(Purpose))
				return Fail("polity due-work request is invalid", out Failure);
			string cause = Cause(Endpoint, Purpose);
			if (!Eligible(Endpoint, EndpointCount, Purpose, cause))
				return Fail("polity cohort purpose lacks its distinct concrete cause", out Failure);
			string topology = EndpointDigest(new List<KingdomPolityEndpointFacts> { Endpoint });
			Work = Build(RealmId, Endpoint, EndpointCount, 0, Window, CauseTick,
				Purpose, cause, topology); return true;
		}

		public static bool IsScheduled(KingdomPolityCohortPlan Cohort)
		{
			return Cohort != null && Cohort.EventStreamId != null
				&& Cohort.EventStreamId.StartsWith("taf:stream:polity-due:v1:",
					StringComparison.Ordinal);
		}

		public static bool Expired(KingdomPolityCohortPlan Cohort, long Tick)
		{
			if (!IsScheduled(Cohort) || Tick < 0L || Cohort.EventOrdinal >
				(ulong)(long.MaxValue / PeriodTicks)) return false;
			long start = (long)Cohort.EventOrdinal * PeriodTicks;
			return start <= long.MaxValue - StayTicks && Tick >= start + StayTicks;
		}

		public static bool ValidState(KingdomPolityDispatchState S, out string Failure)
		{
			Failure = null;
			if (S == null || S.Version != KingdomPolityDispatchState.CurrentVersion
				|| S.Revision < 0L || (S.RealmId != null
				&& !KingdomPolityRules.TypedId(S.RealmId, "taf:realm:"))
				|| S.FutureCauseFloorTick < 0L || !KingdomPolityRules.Text(S.Fault, false)
				|| !ValidDirectRecords(S, out Failure))
				return Fail("polity dispatch state is invalid", out Failure);
			if (!S.HasWindow)
				return S.LastWindowOrdinal == 0UL && S.WindowCauseTick == 0L
					&& S.EndpointDigest == null && S.EndpointCount == 0
					&& S.CompletedMask == 0
					&& (S.DirectRecords.Count == 0 || S.DirectRecords.Count == 1
						&& IsKind(S.DirectRecords[0], RetirementPrefix)
						&& S.RealmId != null && S.FutureCauseFloorTick == long.MaxValue)
					|| Fail("empty polity dispatch carries authority evidence", out Failure);
			return S.RealmId != null && S.WindowCauseTick >= 0L
				&& KingdomPolityRules.Digest(S.EndpointDigest) && S.EndpointCount >= 1
				&& S.EndpointCount <= MaximumEndpoints && S.CompletedMask >= 0
				&& (S.CompletedMask & ~((1 << S.EndpointCount) - 1)) == 0
				|| Fail("polity dispatch window evidence is invalid", out Failure);
		}

		public static string EndpointVerb(KingdomPolityCohortPurpose P)
		{
			switch (P) { case KingdomPolityCohortPurpose.Guard: return "keeps the gate";
			case KingdomPolityCohortPurpose.Patrol: return "returns from the road";
			case KingdomPolityCohortPurpose.Courier: return "brings word";
			case KingdomPolityCohortPurpose.Trader: return "opens a pack";
			case KingdomPolityCohortPurpose.Migrant: return "asks for a place";
			default: return null; }
		}

		private static long SafeStay(ulong Window)
		{
			return Window > (ulong)((long.MaxValue - StayTicks) / PeriodTicks)
				? long.MaxValue : (long)Window * PeriodTicks + StayTicks;
		}

		private static long WindowStart(ulong Window)
		{
			return Window > (ulong)(long.MaxValue / PeriodTicks)
				? long.MaxValue : (long)Window * PeriodTicks;
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason; return false;
		}
	}
}
