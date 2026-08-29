using System;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		public static bool TryFreezeGrowthArrivalOpportunity(KingdomGrowthBook Book,
			int RulesVersion, string EventStreamId, uint EventKindCode, bool FirstGuest,
			string Blueprint, string Origin, string Creed, string PersonName, string Arrived,
			out KingdomGrowthArrivalOpportunity Opportunity)
		{
			Opportunity = null;
			if (!GrowthArrivalCadenceShape(Book) || Book.ArrivalCadenceMigrationPending
				|| Book.ArrivalOpportunity != null || Book.ArrivalDebtRanges.Count == 0
				|| RulesVersion <= 0 || EventKindCode == 0U
				|| !ValidRootId(Book.SettlementId)
				|| !string.Equals(EventStreamId, GrowthArrivalEventStreamId,
					StringComparison.Ordinal) || !ValidName(Blueprint) || !ValidName(Origin)
				|| !ValidName(Creed) || !ValidName(PersonName) || !ValidName(Arrived)) return false;
			KingdomGrowthArrivalDebtRange head = Book.ArrivalDebtRanges[0];
			if (head == null || head.Count == 0UL || head.RulesVersionAtCreation != RulesVersion)
				return false;
			if (head.Count > 1UL && head.FirstDueTick > long.MaxValue - head.IntervalTicks)
				return false;
			KingdomGrowthArrivalOpportunity result = new KingdomGrowthArrivalOpportunity
			{
				RulesVersionAtCreation = RulesVersion, RateEpoch = head.RateEpoch,
				Cohort = head.Cohort, Ordinal = head.FirstOrdinal, DueTick = head.FirstDueTick,
				IntervalTicks = head.IntervalTicks,
				SettlementId = Book.SettlementId, EventStreamId = EventStreamId,
				EventKindCode = EventKindCode,
				FirstGuest = FirstGuest, Blueprint = Blueprint, Origin = Origin, Creed = Creed,
				PersonName = PersonName, Arrived = Arrived
			};
			result.EventId = GrowthArrivalOpportunityEventId(result.SettlementId,
				result.RulesVersionAtCreation, result.EventStreamId, result.EventKindCode,
				result.Ordinal);
			if (!TryGrowthArrivalOpportunityPayloadHash(result, out result.PayloadHash)) return false;
			Book.ArrivalOpportunity = result;
			if (head.Count == 1UL) Book.ArrivalDebtRanges.RemoveAt(0);
			else
			{
				head.FirstOrdinal++; head.Count--; head.FirstDueTick += head.IntervalTicks;
			}
			Book.NextArrivalTick = ArrivalClockFrontier(Book);
			Opportunity = result;
			return GrowthArrivalCadenceShape(Book);
		}

		public static bool TryRetireGrowthArrivalOpportunity(KingdomGrowthBook Book,
			KingdomGrowthArrivalOpportunity Opportunity)
		{
			if (!GrowthArrivalCadenceShape(Book) || Opportunity == null
				|| !ReferenceEquals(Book.ArrivalOpportunity, Opportunity)
				|| Book.ArrivalCandidate != null || Book.ArrivalOp != null
				|| Opportunity.Ordinal != Book.ArrivalOrdinalRetiredThrough + 1UL
				|| !GrowthArrivalOpportunityShape(Opportunity)) return false;
			Book.ArrivalOrdinalRetiredThrough = Opportunity.Ordinal;
			Book.ArrivalOpportunity = null;
			Book.NextArrivalTick = ArrivalClockFrontier(Book);
			return GrowthArrivalCadenceShape(Book);
		}

		/// <summary>Folds the outstanding cohort through its durable terminal tick, then
		/// starts the post-disposition rate before physical/logical retirement can be cut by a save.</summary>
		public static bool TryTransitionGrowthArrivalCadenceForRetirement(
			KingdomGrowthBook Book, KingdomGrowthArrivalOpportunity Opportunity,
			long TerminalTick, long ObservationTick, long DesiredIntervalTicks,
			int DesiredCohort, int RulesVersion, out string Failure)
		{
			Failure = null;
			if (!GrowthArrivalCadenceShape(Book) || Book.ArrivalCadenceMigrationPending
				|| Opportunity == null || !ReferenceEquals(Book.ArrivalOpportunity, Opportunity)
				|| !GrowthArrivalOpportunityShape(Opportunity) || TerminalTick < 0L
				|| ObservationTick < TerminalTick || DesiredIntervalTicks <= 0L
				|| DesiredCohort < 0 || RulesVersion <= 0)
				return FailCadence("arrival retirement cadence input is malformed", out Failure);
			if (Book.WorkPaused) return true;
			if (Book.ArrivalCadenceResumePending)
				return TryRestartGrowthArrivalCadenceAfterPause(Book, ObservationTick,
					DesiredIntervalTicks, DesiredCohort, RulesVersion, out Failure);
			if (Book.ArrivalProcessedThroughTick > TerminalTick)
				return RetirementRateMatches(Book, DesiredIntervalTicks, DesiredCohort,
					RulesVersion) || FailCadence(
						"arrival retirement cadence crossed its terminal tick", out Failure);
			return TryAdvanceGrowthArrivalCadence(Book, TerminalTick, DesiredIntervalTicks,
				DesiredCohort, RulesVersion, out Failure);
		}

		private static bool RetirementRateMatches(KingdomGrowthBook book, long interval,
			int cohort, int rulesVersion)
		{
			return book != null && book.ArrivalIntervalTicks == interval
				&& book.ArrivalRateCohort == cohort && book.ArrivalRulesVersion == rulesVersion;
		}

		public static bool TryGrowthArrivalOpportunityPayloadHash(
			KingdomGrowthArrivalOpportunity Opportunity, out string Hash)
		{
			Hash = null;
			if (Opportunity == null || Opportunity.RulesVersionAtCreation <= 0
				|| Opportunity.RateEpoch <= 0L || Opportunity.Cohort < 0
				|| Opportunity.Ordinal == 0UL || Opportunity.DueTick < 0L
				|| Opportunity.IntervalTicks <= 0L || Opportunity.EventKindCode == 0U
				|| !ValidRootId(Opportunity.SettlementId) || !ValidGeneratedId(Opportunity.EventId)
				|| !string.Equals(Opportunity.EventStreamId, GrowthArrivalEventStreamId,
					StringComparison.Ordinal) || !ValidName(Opportunity.Blueprint)
				|| !ValidName(Opportunity.Origin) || !ValidName(Opportunity.Creed)
				|| !ValidName(Opportunity.PersonName) || !ValidName(Opportunity.Arrived)
				|| !string.Equals(Opportunity.EventId, GrowthArrivalOpportunityEventId(
					Opportunity.SettlementId, Opportunity.RulesVersionAtCreation,
					Opportunity.EventStreamId, Opportunity.EventKindCode,
					Opportunity.Ordinal), StringComparison.Ordinal)) return false;
			Hash = HashId("growth-arrival-opportunity-payload", delegate(BinaryWriter w)
			{
				w.Write(Opportunity.RulesVersionAtCreation); w.Write(Opportunity.RateEpoch);
				w.Write(Opportunity.Cohort); w.Write(Opportunity.Ordinal);
				w.Write(Opportunity.DueTick); w.Write(Opportunity.IntervalTicks);
				CanonicalString(w, Opportunity.SettlementId);
				CanonicalString(w, Opportunity.EventStreamId); w.Write(Opportunity.EventKindCode);
				CanonicalString(w, Opportunity.EventId);
				w.Write(Opportunity.FirstGuest); CanonicalString(w, Opportunity.Blueprint);
				CanonicalString(w, Opportunity.Origin); CanonicalString(w, Opportunity.Creed);
				CanonicalString(w, Opportunity.PersonName); CanonicalString(w, Opportunity.Arrived);
			});
			return ValidHashNamespace(Hash, "growth-arrival-opportunity-payload");
		}

		public static string GrowthArrivalOpportunityEventId(string SettlementId,
			int RulesVersion, string EventStreamId, uint EventKindCode, ulong Ordinal)
		{
			if (!ValidRootId(SettlementId) || RulesVersion <= 0 || EventKindCode == 0U
				|| Ordinal == 0UL || !string.Equals(EventStreamId,
					GrowthArrivalEventStreamId, StringComparison.Ordinal)) return null;
			return HashId("growth-arrival-opportunity", delegate(BinaryWriter w)
			{
				CanonicalString(w, SettlementId); w.Write(RulesVersion);
				CanonicalString(w, EventStreamId); w.Write(EventKindCode); w.Write(Ordinal);
			});
		}

		internal static long ArrivalClockAfterOpportunity(KingdomGrowthBook book)
		{
			if (book == null || book.ArrivalOpportunity == null) return -1L;
			return book.ArrivalDebtRanges.Count > 0
				? book.ArrivalDebtRanges[0].FirstDueTick : book.ArrivalCadenceNextDueTick;
		}

		internal static long ArrivalClockFrontier(KingdomGrowthBook book)
		{
			if (book == null) return -1L;
			if (book.ArrivalOp != null && book.ArrivalOp.ClockLease != null)
			{
				bool after = book.ArrivalOp.ClockState == KingdomLifecyclePhysicalState.Proved;
				return after ? book.ArrivalOp.ClockLease.After : book.ArrivalOp.ClockLease.Before;
			}
			if (book.ArrivalOpportunity != null) return book.ArrivalOpportunity.DueTick;
			if (book.ArrivalDebtRanges != null && book.ArrivalDebtRanges.Count > 0)
				return book.ArrivalDebtRanges[0].FirstDueTick;
			return book.ArrivalCadenceNextDueTick;
		}

		internal static ulong ArrivalDebtCount(KingdomGrowthBook book)
		{
			if (book?.ArrivalDebtRanges == null) return ulong.MaxValue;
			ulong total = book.ArrivalOpportunity == null ? 0UL : 1UL;
			for (int i = 0; i < book.ArrivalDebtRanges.Count; i++)
			{
				ulong count = book.ArrivalDebtRanges[i]?.Count ?? ulong.MaxValue;
				if (total > ulong.MaxValue - count) return ulong.MaxValue;
				total += count;
			}
			return total;
		}

		internal static bool HasGrowthArrivalSemanticDebt(KingdomGrowthBook book)
		{
			return book != null && (book.ArrivalOpportunity != null
				|| book.ArrivalDebtRanges != null && book.ArrivalDebtRanges.Count > 0);
		}
	}
}
