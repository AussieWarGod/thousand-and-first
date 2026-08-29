using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteGrowthArrivalCadence(BinaryWriter w, KingdomGrowthBook book)
		{
			S(w, book.ArrivalEventStreamId, false); w.Write(book.ArrivalRulesVersion);
			w.Write(book.ArrivalRateEpoch); w.Write(book.ArrivalRateEpochStartedTick);
			w.Write(book.ArrivalProcessedThroughTick); w.Write(book.ArrivalCadenceNextDueTick);
			w.Write(book.ArrivalRateCohort); w.Write(book.ArrivalOrdinalHighWater);
			w.Write(book.ArrivalOrdinalRetiredThrough); w.Write(book.ArrivalCadenceMigrationPending);
			w.Write(book.ArrivalCadenceResumePending);
			WriteGrowthArrivalOpportunity(w, book.ArrivalOpportunity);
			EnsureCount(book.ArrivalDebtRanges, KingdomLifecycleRules.MaxGrowthArrivalDebtRanges,
				"growth arrival debt ranges");
			w.Write(book.ArrivalDebtRanges.Count);
			for (int i = 0; i < book.ArrivalDebtRanges.Count; i++)
			{
				KingdomGrowthArrivalDebtRange range = book.ArrivalDebtRanges[i];
				w.Write(range.RulesVersionAtCreation); w.Write(range.RateEpoch);
				w.Write(range.Cohort); w.Write(range.FirstOrdinal); w.Write(range.Count);
				w.Write(range.FirstDueTick); w.Write(range.IntervalTicks);
			}
		}

		private static void ReadGrowthArrivalCadence(BinaryReader r, KingdomGrowthBook book)
		{
			book.ArrivalEventStreamId = S(r, false); book.ArrivalRulesVersion = r.ReadInt32();
			book.ArrivalRateEpoch = r.ReadInt64(); book.ArrivalRateEpochStartedTick = r.ReadInt64();
			book.ArrivalProcessedThroughTick = r.ReadInt64();
			book.ArrivalCadenceNextDueTick = r.ReadInt64();
			book.ArrivalRateCohort = r.ReadInt32(); book.ArrivalOrdinalHighWater = r.ReadUInt64();
			book.ArrivalOrdinalRetiredThrough = r.ReadUInt64();
			book.ArrivalCadenceMigrationPending = ReadExactBoolean(r);
			book.ArrivalCadenceResumePending = ReadExactBoolean(r);
			book.ArrivalOpportunity = ReadGrowthArrivalOpportunity(r);
			int count = ReadCount(r, KingdomLifecycleRules.MaxGrowthArrivalDebtRanges);
			book.ArrivalDebtRanges = new System.Collections.Generic.List<
				KingdomGrowthArrivalDebtRange>(count);
			for (int i = 0; i < count; i++)
			{
				book.ArrivalDebtRanges.Add(new KingdomGrowthArrivalDebtRange
				{
					RulesVersionAtCreation = r.ReadInt32(), RateEpoch = r.ReadInt64(),
					Cohort = r.ReadInt32(), FirstOrdinal = r.ReadUInt64(), Count = r.ReadUInt64(),
					FirstDueTick = r.ReadInt64(), IntervalTicks = r.ReadInt64()
				});
			}
		}

		private static void WriteGrowthArrivalOpportunity(BinaryWriter w,
			KingdomGrowthArrivalOpportunity opportunity)
		{
			w.Write(opportunity != null); if (opportunity == null) return;
			w.Write(opportunity.RulesVersionAtCreation); w.Write(opportunity.RateEpoch);
			w.Write(opportunity.Cohort); w.Write(opportunity.Ordinal);
			w.Write(opportunity.DueTick); w.Write(opportunity.IntervalTicks);
			S(w, opportunity.SettlementId, false); S(w, opportunity.EventStreamId, false);
			w.Write(opportunity.EventKindCode); S(w, opportunity.EventId, false);
			w.Write(opportunity.FirstGuest); S(w, opportunity.Blueprint, false);
			S(w, opportunity.Origin, false); S(w, opportunity.Creed, false);
			S(w, opportunity.PersonName, false); S(w, opportunity.Arrived, false);
			S(w, opportunity.PayloadHash, false);
		}

		private static KingdomGrowthArrivalOpportunity ReadGrowthArrivalOpportunity(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomGrowthArrivalOpportunity
			{
				RulesVersionAtCreation = r.ReadInt32(), RateEpoch = r.ReadInt64(),
				Cohort = r.ReadInt32(), Ordinal = r.ReadUInt64(), DueTick = r.ReadInt64(),
				IntervalTicks = r.ReadInt64(), SettlementId = S(r, false),
				EventStreamId = S(r, false), EventKindCode = r.ReadUInt32(),
				EventId = S(r, false), FirstGuest = ReadExactBoolean(r),
				Blueprint = S(r, false), Origin = S(r, false), Creed = S(r, false),
				PersonName = S(r, false), Arrived = S(r, false), PayloadHash = S(r, false)
			};
		}

		private static void WriteGrowthArrivalCandidateCadence(BinaryWriter w,
			KingdomGrowthArrivalCandidate candidate)
		{
			w.Write(candidate.ArrivalOpportunityOrdinal);
			w.Write(candidate.ArrivalOpportunityDueTick);
			w.Write(candidate.ArrivalOpportunityRateEpoch);
			S(w, candidate.ArrivalOpportunityPayloadHash, true);
		}

		private static void ReadGrowthArrivalCandidateCadence(BinaryReader r,
			KingdomGrowthArrivalCandidate candidate)
		{
			candidate.ArrivalOpportunityOrdinal = r.ReadUInt64();
			candidate.ArrivalOpportunityDueTick = r.ReadInt64();
			candidate.ArrivalOpportunityRateEpoch = r.ReadInt64();
			candidate.ArrivalOpportunityPayloadHash = S(r, true);
		}
	}
}
