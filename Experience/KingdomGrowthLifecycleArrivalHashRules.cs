using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		public static bool TryObserveGrowthScarcityOption(KingdomGrowthBook Book,
			bool Enabled, long Tick)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Tick < Book.ScarcityOptionTick || Tick < Book.MigrationTick) return false;
			KingdomLifecycleOptionState beforeState = Book.ScarcityOptionState;
			long beforeTick = Book.ScarcityOptionTick;
			Book.ScarcityOptionState = Enabled ? KingdomLifecycleOptionState.Enabled
				: KingdomLifecycleOptionState.Disabled;
			Book.ScarcityOptionTick = Tick;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.ScarcityOptionState = beforeState;
			Book.ScarcityOptionTick = beforeTick;
			return false;
		}

		private static bool TryGrowthEffectiveNow(KingdomGrowthBook Book, long Now,
			out long EffectiveNow)
		{
			EffectiveNow = 0L;
			if (Book == null || Now < 0L || Book.WorkPausedTicks < 0L
				|| (Book.WorkPaused && (Book.WorkPauseStartedTick < 0L
					|| Now < Book.WorkPauseStartedTick))) return false;
			long anchor = Book.WorkPaused ? Book.WorkPauseStartedTick : Now;
			if (anchor < Book.WorkPausedTicks) return false;
			EffectiveNow = anchor - Book.WorkPausedTicks;
			return true;
		}

		private static bool GrowthEffectiveWorkBounded(KingdomGrowthBook book)
		{
			if (book == null || book.FieldOps == null) return false;
			long observationTick = Math.Max(book.OptionTick, book.HealthTick);
			long ceiling;
			if (!TryGrowthEffectiveNow(book, observationTick, out ceiling)) return false;
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field == null || field.ClockTick < 0L) return false;
				if (field.ClockTick > ceiling) ceiling = field.ClockTick;
			}
			return book.EffectiveWorkTick <= ceiling;
		}

		public static string GrowthOperationId(string SettlementId, KingdomGrowthSlotKind Slot,
			string FieldId, long Sequence)
		{
			if (Slot != KingdomGrowthSlotKind.Field && FieldId != null && FieldId.Length == 0)
				FieldId = null;
			if (!ValidRootId(SettlementId) || !KnownGrowthSlot(Slot) || Sequence <= 0L
				|| (Slot == KingdomGrowthSlotKind.Field ? !ValidRootId(FieldId)
					: FieldId != null)) return null;
			return HashId("growth-operation", delegate(BinaryWriter w)
			{
				CanonicalString(w, SettlementId); w.Write((byte)Slot);
				CanonicalString(w, FieldId); w.Write(Sequence);
			});
		}

		public static string GrowthArrivalCandidateId(string SettlementId, long Sequence)
		{
			if (!ValidRootId(SettlementId) || Sequence <= 0L) return null;
			return HashId("growth-arrival-candidate", delegate(BinaryWriter w)
			{
				CanonicalString(w, SettlementId); w.Write(Sequence);
			});
		}

		public static bool TryGrowthArrivalCandidatePlanHash(
			KingdomGrowthArrivalCandidate Candidate, out string Hash)
		{
			string baseHash;
			if (!TryGrowthArrivalCandidateBasePlanHash(Candidate, out baseHash))
			{
				Hash = null;
				return false;
			}
			KingdomGrowthArrivalCandidatePhase phase = Candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? Candidate.EvidencePhase : Candidate.Phase;
			if (phase == KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				|| phase == KingdomGrowthArrivalCandidatePhase.Declined
				|| GrowthFirstGuestDeclinedSettled(Candidate, phase)
				|| GrowthFirstGuestPhysicalTerminalSettled(Candidate, phase)
				|| phase == KingdomGrowthArrivalCandidatePhase.Prepared
				|| phase == KingdomGrowthArrivalCandidatePhase.CreateIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.Escrowed
				|| phase == KingdomGrowthArrivalCandidatePhase.GuestHosted
				|| phase == KingdomGrowthArrivalCandidatePhase.GuestTerminal)
			{
				Hash = baseHash;
				return true;
			}
			if (phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent)
				return TryGrowthArrivalLodgingIntentPlanHash(Candidate, baseHash, out Hash);
			string observedHash;
			if (!TryGrowthArrivalObservedPlanHash(Candidate, baseHash, out observedHash))
			{
				Hash = null;
				return false;
			}
			if (phase == KingdomGrowthArrivalCandidatePhase.Observed)
			{
				Hash = observedHash;
				return true;
			}
			return TryGrowthArrivalDispositionPlanHash(Candidate, observedHash, out Hash);
		}

		private static bool TryGrowthArrivalLodgingIntentPlanHash(
			KingdomGrowthArrivalCandidate Candidate, string BaseHash, out string Hash)
		{
			try
			{
				Hash = HashId("growth-arrival-candidate-plan", delegate(BinaryWriter w)
				{
					CanonicalString(w, BaseHash); CanonicalString(w, "lodging-intent");
					CanonicalString(w, Candidate.ObjectId);
					CanonicalString(w, Candidate.LodgingZoneId);
					w.Write(Candidate.LodgingX); w.Write(Candidate.LodgingY);
					CanonicalString(w, Candidate.LodgingBeforeGraphHash);
					CanonicalString(w, Candidate.LodgingReceiptId);
				});
				return ValidHashNamespace(Hash, "growth-arrival-candidate-plan");
			}
			catch (Exception) { Hash = null; return false; }
		}

		private static bool TryGrowthArrivalObservedPlanHash(
			KingdomGrowthArrivalCandidate Candidate, string BaseHash, out string Hash)
		{
			Hash = null;
			if (Candidate == null || Candidate.LodgingState !=
				KingdomLifecyclePhysicalState.Proved) return false;
			try
			{
				Hash = HashId("growth-arrival-candidate-plan", delegate(BinaryWriter w)
				{
					CanonicalString(w, BaseHash); CanonicalString(w, "observed");
					CanonicalString(w, Candidate.ObjectId);
					CanonicalString(w, Candidate.LodgingZoneId);
					w.Write(Candidate.LodgingX); w.Write(Candidate.LodgingY);
					w.Write((byte)Candidate.Disposition);
					w.Write((byte)Candidate.RefusalReason);
					CanonicalString(w, Candidate.LodgingBeforeGraphHash);
					CanonicalString(w, Candidate.LodgingDeclaredGraphHash);
					CanonicalString(w, Candidate.LodgingReceiptGraphHash);
					CanonicalString(w, Candidate.LodgingCallbackReferenceHash);
					w.Write(Candidate.LodgingSameReference);
					CanonicalString(w, Candidate.LodgingReceiptId);
				});
				return ValidHashNamespace(Hash, "growth-arrival-candidate-plan");
			}
			catch (Exception) { Hash = null; return false; }
		}

		private static bool TryGrowthArrivalDispositionPlanHash(
			KingdomGrowthArrivalCandidate Candidate, string ObservedHash, out string Hash)
		{
			Hash = null;
			KingdomGrowthObjectCallbackStep step = Candidate == null
				? null : Candidate.DispositionStep;
			if (step == null || string.IsNullOrEmpty(Candidate.ConsumingOperationId)
				|| Candidate.ConsumingOperationSequence <= 0L) return false;
			try
			{
				Hash = HashId("growth-arrival-candidate-plan", delegate(BinaryWriter w)
				{
					CanonicalString(w, ObservedHash); CanonicalString(w, "disposition-intent");
					CanonicalString(w, Candidate.ConsumingOperationId);
					w.Write(Candidate.ConsumingOperationSequence);
					WriteGrowthObjectCallbackPlan(w, step);
					CanonicalString(w, step.BeforeOwnerGraphHash);
					CanonicalString(w, step.AfterOwnerGraphHash);
					CanonicalString(w, step.BeforeObjectGraphHash);
					CanonicalString(w, step.AfterObjectGraphHash);
					CanonicalString(w, step.BeforeTopologyHash);
					CanonicalString(w, step.AfterTopologyHash);
				});
				return ValidHashNamespace(Hash, "growth-arrival-candidate-plan");
			}
			catch (Exception) { Hash = null; return false; }
		}

		private static bool TryGrowthArrivalCandidateBasePlanHash(
			KingdomGrowthArrivalCandidate Candidate, out string Hash)
		{
			return TryGrowthArrivalCandidateBasePlanHashCore(Candidate, true,
				Candidate != null && !Candidate.LegacySemanticPlan,
				Candidate != null && Candidate.FirstGuest != null, out Hash);
		}

		private static bool TryLegacyGrowthArrivalCandidateBasePlanHash(
			KingdomGrowthArrivalCandidate Candidate, out string Hash)
		{
			return TryGrowthArrivalCandidateBasePlanHashCore(Candidate, false, false, false,
				out Hash);
		}

		private static bool TryGrowthV3ArrivalCandidateBasePlanHash(
			KingdomGrowthArrivalCandidate Candidate, out string Hash)
		{
			return TryGrowthArrivalCandidateBasePlanHashCore(Candidate, true,
				Candidate != null && !Candidate.LegacySemanticPlan, false, out Hash);
		}

		private static bool TryGrowthArrivalCandidateBasePlanHashCore(
			KingdomGrowthArrivalCandidate Candidate, bool IncludeZone, bool IncludeSemantic,
			bool IncludeFirstGuest,
			out string Hash)
		{
			Hash = null;
			if (Candidate == null || Candidate.CreateStep == null) return false;
			try
			{
				Hash = HashId("growth-arrival-candidate-plan", delegate(BinaryWriter w)
				{
					w.Write(Candidate.Sequence); CanonicalString(w, Candidate.Id);
					CanonicalString(w, Candidate.SettlementId); w.Write(Candidate.CreatedTick);
					if (Candidate.ArrivalOpportunityOrdinal != 0UL)
					{
						CanonicalString(w, "arrival-opportunity-v1");
						w.Write(Candidate.ArrivalOpportunityOrdinal);
						w.Write(Candidate.ArrivalOpportunityDueTick);
						w.Write(Candidate.ArrivalOpportunityRateEpoch);
						CanonicalString(w, Candidate.ArrivalOpportunityPayloadHash);
					}
					CanonicalString(w, Candidate.Marker); CanonicalString(w, Candidate.Blueprint);
					CanonicalString(w, Candidate.EscrowKey);
					if (IncludeZone) CanonicalString(w, Candidate.LodgingZoneId);
					if (IncludeSemantic)
					{
						CanonicalString(w, "semantic-person-plan");
						w.Write(Candidate.SemanticPlanVersion);
						CanonicalString(w, Candidate.SemanticStreamId);
						w.Write(Candidate.SemanticEventKind);
						CanonicalString(w, Candidate.PlannedOrigin);
						CanonicalString(w, Candidate.PlannedCreed);
						CanonicalString(w, Candidate.PlannedName);
						CanonicalString(w, Candidate.PlannedArrived);
						w.Write(Candidate.ArrivalX); w.Write(Candidate.ArrivalY);
					}
					if (IncludeFirstGuest) WriteGrowthFirstGuestPlan(w, Candidate.FirstGuest);
					WriteLeasePlan(w, Candidate.CandidateLease);
					WriteLeasePlan(w, Candidate.LodgingLease);
					WriteLeasePlan(w, Candidate.EscrowLease);
					KingdomGrowthObjectCallbackStep step = Candidate.CreateStep;
					CanonicalString(w, step.EventId); w.Write((byte)step.Kind);
					w.Write((byte)step.FromLocation); w.Write((byte)step.ToLocation);
					CanonicalString(w, step.EscrowKey); w.Write(step.BeforeCount);
					w.Write(step.AfterCount); w.Write(step.NoStack);
					CanonicalString(w, step.BeforeOwnerGraphHash);
					CanonicalString(w, step.BeforeObjectGraphHash);
					CanonicalString(w, step.BeforeTopologyHash);
					CanonicalString(w, step.ReceiptId);
				});
				return ValidHashNamespace(Hash, "growth-arrival-candidate-plan");
			}
			catch (Exception) { Hash = null; return false; }
		}

		private static string GrowthArrivalLodgingProof(
			KingdomGrowthArrivalCandidate Candidate)
		{
			string baseHash;
			if (!TryGrowthArrivalCandidateBasePlanHash(Candidate, out baseHash)) return null;
			string proof = HashId("growth-arrival-lodging-proof", delegate(BinaryWriter w)
			{
				CanonicalString(w, baseHash); CanonicalString(w, Candidate.ObjectId);
				CanonicalString(w, Candidate.LodgingZoneId);
				w.Write(Candidate.LodgingX); w.Write(Candidate.LodgingY);
				w.Write((byte)Candidate.Disposition); w.Write((byte)Candidate.RefusalReason);
				CanonicalString(w, Candidate.LodgingBeforeGraphHash);
				CanonicalString(w, Candidate.LodgingReceiptGraphHash);
				CanonicalString(w, Candidate.LodgingCallbackReferenceHash);
				w.Write(Candidate.LodgingSameReference);
				CanonicalString(w, Candidate.LodgingReceiptId);
			});
			return ValidHashNamespace(proof, "growth-arrival-lodging-proof")
				? proof.Substring(proof.Length - 64) : null;
		}

	}
}
