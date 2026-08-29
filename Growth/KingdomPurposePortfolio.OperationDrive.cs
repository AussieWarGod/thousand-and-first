using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private const int MaxPurposeOperationSteps = 128;

		private static bool DrivePortfolioOperation(KingdomSystem System,
			KingdomPurposePairReceipt Pair, out KingdomPurposePairReceipt Published,
			out string Failure)
		{
			Published = Pair;
			Failure = null;
			if (!TryReconcilePortfolioTopology(ref Pair, out Failure)) return false;
			Published = Pair;
			for (int step = 0; step < MaxPurposeOperationSteps; step++)
			{
				KingdomPurposeOperationReceipt operation = Published?.Operation;
				if (operation == null) return true;
				if (!KingdomPurposePortfolioRules.PairRevisionHeadroomIsValid(Published))
					return QuarantinePortfolio(Published,
						"The purpose operation no longer has its phase-bounded revision headroom.",
						out Published, out Failure);
				if (operation.Phase == KingdomPurposeOperationPhase.Delivered) return true;
				if (Published.Revision == int.MaxValue || operation.Revision == int.MaxValue)
					return Fail("The purpose operation exhausted its exact revision range before another callback.",
						out Failure);
				if (Published.Phase == KingdomPurposePairPhase.Quarantined) return false;
				// Doctrine: a paused realm refuses new work but must still finish exact committed
				// recovery. Every phase reachable here belongs to an operation whose receipt is
				// already published, and the only entrance to a brand-new one is gated in
				// TryPortfolioOperationPreflight, so a committed landing stays resumable while
				// paused. A phase outside that set is not committed work and still refuses.
				if (!KingdomPurposePortfolioRules.OperationPhaseIsCommitted(operation.Phase)
					&& !KingdomMaster.NewWorkAllowed(System))
					return Fail("New purpose work is paused by realm transition authority.", out Failure);
				KingdomPurposePairReceipt before = Published;
				bool advanced;
				switch (operation.Phase)
				{
				case KingdomPurposeOperationPhase.Prepared:
					advanced = operation.BootstrapExemption || operation.ReturnExemption
						? BeginLocalDebit(before, out Published, out Failure)
						: BeginInputDebit(before, out Published, out Failure);
					break;
				case KingdomPurposeOperationPhase.InputDebitPending:
					advanced = DriveInputDebit(before, out Published, out Failure); break;
				case KingdomPurposeOperationPhase.InputDebited:
					advanced = BeginLocalDebit(before, out Published, out Failure); break;
				case KingdomPurposeOperationPhase.LocalDebitPending:
					advanced = DriveLocalDebit(before, out Published, out Failure); break;
				case KingdomPurposeOperationPhase.LocalDebited:
					advanced = BeginPurposeEffect(before, out Published, out Failure); break;
				case KingdomPurposeOperationPhase.EffectPending:
					advanced = DrivePurposeEffect(before, out Published, out Failure); break;
				case KingdomPurposeOperationPhase.EffectApplied:
					advanced = BeginPurposeOutput(before, out Published, out Failure); break;
				case KingdomPurposeOperationPhase.OutputPending:
					advanced = DrivePurposeOutput(before, out Published, out Failure); break;
				case KingdomPurposeOperationPhase.Dispatching:
					advanced = DrivePurposeDispatch(System, before, out Published, out Failure); break;
				case KingdomPurposeOperationPhase.PickupComplete:
					advanced = AcknowledgePurposeTransit(before, out Published, out Failure); break;
				case KingdomPurposeOperationPhase.LandingPending:
					advanced = DrivePurposeLanding(System, before, out Published, out Failure); break;
				default:
					return QuarantinePortfolio(before,
						"The purpose operation entered an unsupported phase.",
						out Published, out Failure);
				}
				if (!advanced) return false;
				if (Published == null || Published.Revision == before.Revision)
					return Fail("The purpose operation waits without a new durable checkpoint.",
						out Failure);
			}
			return Fail("The purpose operation reached its bounded work slice; retry it.", out Failure);
		}

		private static bool BeginLocalDebit(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			if (!TryOperationGround(Pair?.Operation, out Zone zone, out _,
				out GameObject input, out _, out _, out _, out Failure)) return false;
			if (!TryPlanLocalDebit(Pair.Operation, zone, input,
				out string receipt, out Failure)) return false;
			KingdomPurposeOperationReceipt next = Pair.Operation.Copy();
			next.LocalDebitReceipt = receipt;
			next.Phase = KingdomPurposeOperationPhase.LocalDebitPending;
			next.Revision++;
			return TryPublishOperation(Pair, next, Pair.Phase, out Published, out Failure);
		}

		private static bool AcceptPortfolioCredit(GameObject Work,
			KingdomPurposePairReceipt Pair, string ProcedureKey, string ProcedureReceipt,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			if (!TryReconcilePortfolioTopology(ref Pair, out Failure)) return false;
			Published = Pair;
			if (Pair?.Operation == null
				|| (Pair.Phase != KingdomPurposePairPhase.CargoAwaitingActivation
					&& Pair.Phase != KingdomPurposePairPhase.CargoAwaitingConsumption)) return false;
			bool activation = Pair.Phase == KingdomPurposePairPhase.CargoAwaitingActivation;
			if (!KingdomPurposePortfolioRules.PairRevisionHeadroomIsValid(Pair)
				|| (activation && (!KingdomPurposePortfolioRules.CanStartOperationAtRevision(
					Pair.Revision, Pair.Phase) || Pair.NextOperationOrdinal == int.MaxValue)))
				return Fail("The purpose pair exhausted its exact operation range.", out Failure);
			KingdomPurposeKind nextKind = Pair.Phase
				== KingdomPurposePairPhase.CargoAwaitingActivation
					? Pair.FirstKind : Pair.Operation.DestinationKind;
			string workId = nextKind == Pair.FirstKind ? Pair.FirstWorkId : Pair.SecondWorkId;
			string inputId = nextKind == Pair.FirstKind
				? Pair.FirstInputStoreId : Pair.SecondInputStoreId;
			if (!GameObject.Validate(Work) || Work.ID != workId
				|| !TryPurposeZone(nextKind == Pair.FirstKind ? Pair.FirstZoneId : Pair.SecondZoneId,
					out Zone zone)
				|| FindExactKnown(zone, Pair.Operation.OutputCargoId, out GameObject cargo)
					!= KingdomPhysicalLookupState.Exact
				|| !ExactPortfolioCargo(cargo, Pair.Operation.OutputCargoReceipt, inputId))
				return Fail("The exact delivered cargo is not in this purpose's frozen input store.",
					out Failure);
			if (activation)
			{
				string operationId = "purpose-op-" + Pair.PairId + "-"
					+ Pair.NextOperationOrdinal;
				if (!KingdomPurposePortfolioRules.TryCreateOperation(Pair, operationId,
					Pair.NextOperationOrdinal, Pair.FirstKind, false, false,
					Pair.Operation.OutputCargoId, Pair.Operation.OutputCargoReceipt,
					ProcedureKey, ProcedureReceipt, null,
					out KingdomPurposeOperationReceipt operation, out var fault))
					return Fail("The activation operation receipt was refused (" + fault + ").",
						out Failure);
				if (!TryPortfolioOperationPreflight(operation, out Failure)) return false;
				KingdomPurposePairReceipt activating = Pair.Copy();
				activating.Operation = operation;
				activating.Phase = KingdomPurposePairPhase.OperationOutstanding;
				activating.NextKind = Pair.FirstKind;
				activating.NextOperationOrdinal++;
				activating.Revision++;
				if (!ExactPublishedPortfolioPair(Pair))
					return Fail("The purpose-pair register changed before this credit could retire its cargo.",
						out Failure);
				if (!TryRetireCreditedPurposeCargo(Pair.Operation))
					return Fail("This delivered landing could not be retired from the destination's custody; nothing was released.",
						out Failure);
				if (!TryPublishPortfolioPair(Pair, activating, out Failure)) return false;
				Published = activating;
				return true;
			}
			// The delivered operation is now durably credited, so its landing marks name a
			// finished operation and may be retired by exact receipt, and its rooted cargo has
			// passed into this pair's credit and no longer needs a root of its own. Retiring before
			// the publish keeps it retry-safe: a crash here re-runs an idempotent removal, and no
			// window is left in which a stale mark could reach the next landing and be cut on as
			// evidence of an owner nobody can name. The register is reproved immediately before it,
			// so a cleanup never runs against a pair some other hand has already moved.
			if (!ExactPublishedPortfolioPair(Pair))
				return Fail("The purpose-pair register changed before this credit could retire its cargo.",
					out Failure);
			if (!TryRetireCreditedPurposeCargo(Pair.Operation))
				return Fail("This delivered landing could not be retired from the destination's custody; nothing was released.",
					out Failure);
			KingdomPurposePairReceipt next = Pair.Copy();
			next.Phase = KingdomPurposePairPhase.Active;
			next.NextKind = nextKind;
			next.CreditCargoId = Pair.Operation.OutputCargoId;
			next.CreditCargoReceipt = Pair.Operation.OutputCargoReceipt;
			next.Operation = null;
			next.Revision++;
			if (!TryPublishPortfolioPair(Pair, next, out Failure)) return false;
			Published = next;
			return true;
		}

		/// <summary>Whether the register still holds exactly the pair this work was previewed
		/// against. Read immediately before any cleanup that precedes a publish, so a retirement
		/// can never run on behalf of a pair another hand has already moved on.</summary>
		private static bool ExactPublishedPortfolioPair(KingdomPurposePairReceipt Pair)
		{
			string expected = Pair == null ? null
				: Pair.LegacyWire ? KingdomPurposePortfolioRules.EncodeLegacyPair(Pair)
					: KingdomPurposePortfolioRules.EncodePair(Pair);
			return The.Game != null && expected != null
				&& The.Game.GetStringGameState(PortfolioStateKey, "") == expected;
		}

		/// <summary>Everything one delivered operation still holds once its cargo has passed on:
		/// the exact landing marks it stamped, and the root entry it was published under. Called at
		/// every point where a pair leaves a delivered operation behind &mdash; both credit paths
		/// and the return start, which replaces the delivered bootstrap operation no credit path
		/// ever reaches. Idempotent in both halves, so any save cut simply retries it.</summary>
		private static bool TryRetireCreditedPurposeCargo(KingdomPurposeOperationReceipt Operation)
		{
			if (Operation == null
				|| Operation.Phase != KingdomPurposeOperationPhase.Delivered) return true;
			// A retirement that cannot prove itself blocks the release outright. Clearing the
			// witnesses and the root while any owned evidence stood would leave exactly what this
			// lane refuses to create: fields whose operation no longer exists.
			if (!TryRetireDeliveredPurposeLanding(Operation)) return false;
			if (KingdomPurposePortfolioRules.TryDecodeCargo(Operation.OutputCargoReceipt,
				out KingdomPurposeCargoReceipt cargo)) RemovePurposeCargoRoots(cargo);
			return true;
		}

		/// <summary>The legacy, idempotent fallback retirement for one delivered operation. The
		/// landing itself retires its marks before its own checkpoint, while the servings are still
		/// provably in the measured larders; this runs only to finish a delivery written before
		/// that law, and walks the fresh recursive destination custody for the same reason. Only
		/// this operation's whole mark is retired, so a mark whose owner cannot be named survives
		/// and is cut on.</summary>
		private static bool TryRetireDeliveredPurposeLanding(
			KingdomPurposeOperationReceipt Operation)
		{
			if (!KingdomPurposePortfolioRules.TryCarriedFood(Operation.SourceKind,
				Operation.DestinationKind, out _, out int carried, out _)) return false;
			if (carried <= 0) return true;
			if (!TryPurposeLandingMark(Operation, out string receipt, out int prefilter)
				|| !TryPurposeZone(Operation.DestinationZoneId, out Zone destination)) return false;
			// Everything is read and allowed before anything is mutated, and the credited cargo is
			// classified first: it stands on the destination ground itself, so a global absence
			// proof taken ahead of its own record would refuse every lawful delivery. A refused
			// retirement therefore leaves every serving mark and the root exactly where they were.
			// An absent root is the idempotent case: a previous pass cleared the record and was
			// cut before its publish.
			bool rooted = TryRootedPurposeCargoExact(Operation, out GameObject cargo);
			GameObject allowed = rooted ? cargo : null;
			if (rooted && !PurposeCargoRecordIsRetirable(cargo, receipt, carried)) return false;
			if (!OnlyRetirableLandingEvidence(destination, allowed, receipt, prefilter)
				|| !TryRetirePurposeLandingMarks(destination, receipt, prefilter)
				|| !NoPurposeLandingEvidenceRemains(destination, allowed)) return false;
			if (rooted && !TryClearPurposeLandingWitnesses(cargo, receipt, carried)) return false;
			return NoPurposeLandingEvidenceRemains(destination, null);
		}
	}
}
