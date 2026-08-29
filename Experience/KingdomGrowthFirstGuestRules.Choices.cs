using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		internal static bool TryCheckGrowthFirstGuestCurrentApplicability(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, int Population, int PopulationCap,
			int SupportedLevel, int SupportCap, int WaterAvailable, int WaterRequired,
			out string Failure)
		{
			Failure = null;
			KingdomGrowthFirstGuestOpportunity guest = Candidate?.FirstGuest;
			KingdomGrowthFirstGuestChoiceState choice = guest == null
				? default(KingdomGrowthFirstGuestChoiceState) : Candidate.FirstGuest.ChoiceState;
			bool correspondence = Candidate?.Phase == KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				&& (choice == KingdomGrowthFirstGuestChoiceState.AwaitingChoice
					|| choice == KingdomGrowthFirstGuestChoiceState.Deferred);
			bool citizenship = Candidate?.Phase == KingdomGrowthArrivalCandidatePhase.GuestHosted
				&& guest?.RulesVersion == 2 && choice == KingdomGrowthFirstGuestChoiceState.Admitted
				&& guest.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Hosted;
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| !correspondence && !citizenship
				|| Population < 0 || PopulationCap <= 0 || SupportedLevel < 0
				|| SupportCap <= 0 || WaterAvailable < 0 || WaterRequired <= 0)
			{
				Failure = "current first-guest applicability is malformed"; return false;
			}
			if (Population >= PopulationCap)
			{
				Failure = "the settlement is now at its population limit"; return false;
			}
			if (SupportedLevel > 0 && Population >= SupportCap)
			{
				Failure = "the settlement's current support no longer permits this arrival";
				return false;
			}
			if (WaterAvailable < WaterRequired)
			{
				Failure = "the exact held ground no longer has enough shared water"; return false;
			}
			return true;
		}

		public static bool TryDeferGrowthFirstGuest(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				|| Tick < Candidate.UpdatedTick || Candidate.FirstGuest == null) return false;
			KingdomGrowthFirstGuestOpportunity x = Candidate.FirstGuest;
			if (x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Deferred) return true;
			if (x.ChoiceState != KingdomGrowthFirstGuestChoiceState.AwaitingChoice) return false;
			string oldHash = Candidate.PlanHash; long oldTick = Candidate.UpdatedTick;
			x.ChoiceState = KingdomGrowthFirstGuestChoiceState.Deferred;
			x.DeferredTick = Tick;
			x.DeferredReceiptId = GrowthFirstGuestReceiptId(x.OpportunityId, "defer", Tick);
			Candidate.UpdatedTick = Tick;
			if (TryRehashFirstGuest(Book, Candidate)) return true;
			x.ChoiceState = KingdomGrowthFirstGuestChoiceState.AwaitingChoice;
			x.DeferredTick = -1L; x.DeferredReceiptId = null;
			Candidate.PlanHash = oldHash; Candidate.UpdatedTick = oldTick; return false;
		}

		public static bool TryAdmitGrowthFirstGuest(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, KingdomExperienceBodyReservation Lease,
			long Tick)
		{
			return TryAdmitGrowthFirstGuestCore(Book, Candidate, Lease, Tick);
		}

		/// <summary>Ordinary Growth citizenship owns its body. Optional Experience capacity
		/// cannot gate facts-only admission or recovery.</summary>
		public static bool TryAdmitGrowthFirstGuest(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			return TryAdmitGrowthFirstGuestCore(Book, Candidate, null, Tick);
		}

		private static bool TryAdmitGrowthFirstGuestCore(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, KingdomExperienceBodyReservation Lease,
			long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				|| Tick < Candidate.UpdatedTick || Lease != null
					&& !ExactFirstGuestBodyLease(Candidate, Lease, Tick))
				return false;
			KingdomGrowthFirstGuestOpportunity x = Candidate.FirstGuest;
			if (x.RulesVersion >= 2 && Lease == null) return false;
			if (x.ChoiceState != KingdomGrowthFirstGuestChoiceState.AwaitingChoice
				&& x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Deferred) return false;
			KingdomGrowthFirstGuestChoiceState oldChoice = x.ChoiceState;
			long oldDecision = x.DecisionTick; string oldReceipt = x.DecisionReceiptId;
			string oldReservation = x.BodyReservationId; string oldRealm = x.BodyRealmId;
			KingdomExperienceOptionKind oldOption = x.BodyOptionKind;
			long oldEpoch = x.BodyEnableEpoch; long oldReserved = x.BodyReservedTick;
			KingdomGrowthFirstGuestBodyLeaseState oldLease = x.BodyLeaseState;
			KingdomGrowthFirstGuestGuestPhase oldGuestPhase = x.GuestPhase;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			string oldHash = Candidate.PlanHash; long oldTick = Candidate.UpdatedTick;
			x.ChoiceState = KingdomGrowthFirstGuestChoiceState.Admitted;
			x.DecisionTick = Tick;
			x.DecisionReceiptId = GrowthFirstGuestReceiptId(x.OpportunityId, "admit", Tick);
			if (Lease != null)
			{
				x.BodyReservationId = Lease.ReservationId; x.BodyRealmId = Lease.RealmId;
				x.BodyOptionKind = Lease.OptionKind; x.BodyEnableEpoch = Lease.EnableEpoch;
				x.BodyReservedTick = Lease.ReservedTick;
				x.BodyLeaseState = KingdomGrowthFirstGuestBodyLeaseState.Reserved;
			}
			if (x.RulesVersion >= 2)
				x.GuestPhase = KingdomGrowthFirstGuestGuestPhase.Preparing;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Prepared;
			Candidate.UpdatedTick = Tick;
			if (TryRehashFirstGuest(Book, Candidate)) return true;
			x.ChoiceState = oldChoice; x.DecisionTick = oldDecision;
			x.DecisionReceiptId = oldReceipt; x.BodyReservationId = oldReservation;
			x.BodyRealmId = oldRealm; x.BodyOptionKind = oldOption; x.BodyEnableEpoch = oldEpoch;
			x.BodyReservedTick = oldReserved; x.BodyLeaseState = oldLease;
			x.GuestPhase = oldGuestPhase;
			Candidate.Phase = oldPhase; Candidate.PlanHash = oldHash;
			Candidate.UpdatedTick = oldTick; return false;
		}

		public static bool TryDeclineGrowthFirstGuest(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				|| Tick < Candidate.UpdatedTick || Candidate.FirstGuest == null) return false;
			KingdomGrowthFirstGuestOpportunity x = Candidate.FirstGuest;
			if (x.ChoiceState != KingdomGrowthFirstGuestChoiceState.AwaitingChoice
				&& x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Deferred) return false;
			KingdomGrowthFirstGuestChoiceState oldChoice = x.ChoiceState;
			string oldHash = Candidate.PlanHash; long oldTick = Candidate.UpdatedTick;
			x.ChoiceState = KingdomGrowthFirstGuestChoiceState.Declined;
			x.DecisionTick = Tick;
			x.DecisionReceiptId = GrowthFirstGuestReceiptId(x.OpportunityId, "decline", Tick);
			Candidate.Disposition = KingdomGrowthArrivalDisposition.Declined;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Declined;
			Candidate.UpdatedTick = Tick;
			if (TryRehashFirstGuest(Book, Candidate)) return true;
			x.ChoiceState = oldChoice; x.DecisionTick = -1L; x.DecisionReceiptId = null;
			Candidate.Disposition = KingdomGrowthArrivalDisposition.None;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.AwaitingChoice;
			Candidate.PlanHash = oldHash; Candidate.UpdatedTick = oldTick; return false;
		}

		public static bool TryHostGrowthFirstGuest(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			KingdomGrowthFirstGuestOpportunity x = Candidate?.FirstGuest;
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate) || x?.RulesVersion != 2
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Escrowed
				|| x.GuestPhase != KingdomGrowthFirstGuestGuestPhase.Preparing
				|| x.BodyLeaseState != KingdomGrowthFirstGuestBodyLeaseState.Reserved
				|| Tick < Candidate.UpdatedTick) return false;
			string oldHash = Candidate.PlanHash; long oldTick = Candidate.UpdatedTick;
			x.GuestPhase = KingdomGrowthFirstGuestGuestPhase.Hosted;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.GuestHosted;
			Candidate.UpdatedTick = Tick;
			if (TryRehashFirstGuest(Book, Candidate)) return true;
			x.GuestPhase = KingdomGrowthFirstGuestGuestPhase.Preparing;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Escrowed;
			Candidate.PlanHash = oldHash; Candidate.UpdatedTick = oldTick; return false;
		}

		public static bool TryBeginGrowthFirstGuestCitizenship(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			return TryBeginGrowthFirstGuestAction(Book, Candidate, Tick,
				KingdomGrowthFirstGuestGuestPhase.CitizenshipIntent, "welcome");
		}

		public static bool TryBeginGrowthFirstGuestDeparture(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			return TryBeginGrowthFirstGuestAction(Book, Candidate, Tick,
				KingdomGrowthFirstGuestGuestPhase.DepartureIntent, "depart");
		}

		private static bool TryBeginGrowthFirstGuestAction(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick,
			KingdomGrowthFirstGuestGuestPhase Next, string Kind)
		{
			KingdomGrowthFirstGuestOpportunity x = Candidate?.FirstGuest;
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate) || x?.RulesVersion != 2
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.GuestHosted
				|| x.GuestPhase != KingdomGrowthFirstGuestGuestPhase.Hosted
				|| Tick < Candidate.UpdatedTick) return false;
			string oldHash = Candidate.PlanHash; long oldTick = Candidate.UpdatedTick;
			x.GuestPhase = Next; x.GuestActionTick = Tick;
			x.GuestActionReceiptId = GrowthFirstGuestReceiptId(x.OpportunityId, Kind, Tick);
			Candidate.UpdatedTick = Tick;
			if (TryRehashFirstGuest(Book, Candidate)) return true;
			x.GuestPhase = KingdomGrowthFirstGuestGuestPhase.Hosted;
			x.GuestActionTick = -1L; x.GuestActionReceiptId = null;
			Candidate.PlanHash = oldHash; Candidate.UpdatedTick = oldTick; return false;
		}

		public static bool TryPrepareGrowthFirstGuestCitizenship(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			KingdomGrowthFirstGuestOpportunity x = Candidate?.FirstGuest;
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate) || x?.RulesVersion != 2
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.GuestHosted
				|| x.GuestPhase != KingdomGrowthFirstGuestGuestPhase.CitizenshipIntent
				|| Tick < Candidate.UpdatedTick) return false;
			string oldHash = Candidate.PlanHash; long oldTick = Candidate.UpdatedTick;
			x.GuestPhase = KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Escrowed;
			Candidate.UpdatedTick = Tick;
			if (TryRehashFirstGuest(Book, Candidate)) return true;
			x.GuestPhase = KingdomGrowthFirstGuestGuestPhase.CitizenshipIntent;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.GuestHosted;
			Candidate.PlanHash = oldHash; Candidate.UpdatedTick = oldTick; return false;
		}

		public static bool TryObserveGrowthFirstGuestTerminal(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ObjectId, string Marker,
			string ZoneId, KingdomGrowthFirstGuestTerminalState Terminal, long Tick)
		{
			KingdomGrowthFirstGuestOpportunity x = Candidate?.FirstGuest;
			bool departed = Terminal == KingdomGrowthFirstGuestTerminalState.Departed;
			bool died = Terminal == KingdomGrowthFirstGuestTerminalState.Died;
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate) || x?.RulesVersion != 2
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.GuestHosted
				|| !(departed && x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.DepartureIntent
					|| died && (x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Hosted
						|| x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipIntent))
				|| ObjectId != Candidate.ObjectId || Marker != Candidate.Marker
				|| ZoneId != Candidate.LodgingZoneId || Tick < Candidate.UpdatedTick) return false;
			KingdomGrowthFirstGuestGuestPhase oldPhase = x.GuestPhase;
			string oldHash = Candidate.PlanHash; long oldTick = Candidate.UpdatedTick;
			x.GuestPhase = KingdomGrowthFirstGuestGuestPhase.Terminal;
			x.GuestTerminalState = Terminal; x.GuestTerminalTick = Tick;
			x.GuestTerminalReceiptId = GrowthFirstGuestReceiptId(x.OpportunityId,
				departed ? "departed" : "died", Tick);
			Candidate.Disposition = KingdomGrowthArrivalDisposition.Departed;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.GuestTerminal;
			Candidate.UpdatedTick = Tick;
			if (TryRehashFirstGuest(Book, Candidate)) return true;
			x.GuestPhase = oldPhase; x.GuestTerminalState =
				KingdomGrowthFirstGuestTerminalState.None;
			x.GuestTerminalTick = -1L; x.GuestTerminalReceiptId = null;
			Candidate.Disposition = KingdomGrowthArrivalDisposition.None;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.GuestHosted;
			Candidate.PlanHash = oldHash; Candidate.UpdatedTick = oldTick; return false;
		}

		private static bool ExactFirstGuestBodyLease(KingdomGrowthArrivalCandidate candidate,
			KingdomExperienceBodyReservation lease, long tick)
		{
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			return x != null && lease != null && lease.ReservationId ==
				GrowthFirstGuestBodyReservationId(x.OpportunityId)
				&& lease.RealmId != null && lease.SettlementId == candidate.SettlementId
				&& lease.SourceId == x.OpportunityId && lease.Lane == KingdomExperienceLane.FirstGuest
				&& lease.OptionKind == KingdomExperienceOptionKind.CivicStory
				&& lease.CauseTick == lease.ReservedTick
				&& lease.ReservedTick >= x.CauseTick
				&& lease.ReservedTick <= tick && lease.EnableEpoch > 0L && lease.BodyCount == 1;
		}

		private static bool TryRehashFirstGuest(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate)
		{
			if (!TryGrowthArrivalCandidatePlanHash(candidate, out string hash)) return false;
			KingdomGrowthObjectCallbackStep create = candidate.CreateStep;
			string oldProof = create?.ReceiptProofId;
			candidate.PlanHash = hash;
			if (create != null && create.State == KingdomLifecyclePhysicalState.Proved)
				create.ReceiptProofId = GrowthArrivalCandidateCallbackProof(candidate, create, 0);
			if (ExactGrowthArrivalCandidateAuthority(book, candidate)) return true;
			if (create != null) create.ReceiptProofId = oldProof;
			return false;
		}
	}
}
