using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private const string PortfolioEffectReceiptProperty = "r_TAF_PurposeEffectReceipt";
		private const string PortfolioEffectOrdinalProperty = "r_TAF_PurposeEffectOrdinal";
		private const string PortfolioCargoRootPrefix = "r_TAF_PurposePairCargo:";
		private const string PortfolioCargoFoodProperty = "r_TAF_PurposePairCargoFood";
		private const string PortfolioCargoKeyProperty = "r_TAF_PurposePairCargoKey";

		private static bool TryStartPortfolioOperation(GameObject Work,
			KingdomPurposePairReceipt Pair, string ProcedureKey, string ProcedureReceipt,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			if (!GameObject.Validate(Work) || Pair == null) return false;
			if (!TryReconcilePortfolioTopology(ref Pair, out Failure)) return false;
			Published = Pair;
			bool bootstrap = Pair.Phase == KingdomPurposePairPhase.Frozen;
			bool returned = Pair.Phase == KingdomPurposePairPhase.SecondPending;
			bool normal = Pair.Phase == KingdomPurposePairPhase.Active;
			KingdomPurposeKind source = bootstrap ? Pair.FirstKind
				: returned ? Pair.SecondKind : normal ? Pair.NextKind : KingdomPurposeKind.None;
			if (source == KingdomPurposeKind.None) return false;
			string expectedWork = source == Pair.FirstKind ? Pair.FirstWorkId : Pair.SecondWorkId;
			string newSecond = null;
			string newSecondInput = null;
			string newSecondOutput = null;
			string newRouteDigest = null;
			bool adoptsSecondEndpoint = false;
			if (returned && string.IsNullOrEmpty(expectedWork))
			{
				if (!SecondWorkAnswersCommitment(Work, Pair)) return Fail(
					"The completed second purpose does not carry this pair's exact construction commitment.",
					out Failure);
				newSecond = Work.IDIfAssigned;
				expectedWork = Work.IDIfAssigned;
				if (!TryPrepareSecondEndpoint(Work, Pair, out newSecondInput,
					out newSecondOutput, out newRouteDigest, out adoptsSecondEndpoint,
					out Failure)) return false;
			}
			if (Work.IDIfAssigned != expectedWork || Work.GetIntProperty("KingdomBuilt") != 1)
				return Fail("Only the exact next-token purpose work may start this operation.",
					out Failure);
			if (!KingdomPurposePortfolioRules.CanStartOperationAtRevision(
				Pair.Revision, Pair.Phase)
				|| Pair.NextOperationOrdinal == int.MaxValue)
				return Fail("The purpose pair lacks the exact revision headroom needed to finish another operation.", out Failure);
			string operationId = "purpose-op-" + Pair.PairId + "-"
				+ Pair.NextOperationOrdinal;
			KingdomPurposeOperationReceipt operation;
			KingdomPurposePairFault fault;
			bool created = adoptsSecondEndpoint
				? KingdomPurposePortfolioRules.TryCreateOperationWithSecondEndpoint(Pair,
					operationId, Pair.NextOperationOrdinal, source, ProcedureKey,
					ProcedureReceipt, newSecond, newSecondInput, newSecondOutput,
					newRouteDigest, out operation, out fault)
				: KingdomPurposePortfolioRules.TryCreateOperation(Pair, operationId,
					Pair.NextOperationOrdinal, source, bootstrap, returned,
					normal ? Pair.CreditCargoId : null,
					normal ? Pair.CreditCargoReceipt : null, ProcedureKey, ProcedureReceipt,
					newSecond, out operation, out fault);
			if (!created)
				return Fail("The purpose operation receipt was refused (" + fault + ").", out Failure);
			if (!TryPortfolioOperationPreflight(operation, out Failure)) return false;
			KingdomPurposePairReceipt next = Pair.Copy();
			if (newSecond != null)
			{
				next.SecondWorkId = newSecond;
				if (adoptsSecondEndpoint)
				{
					next.SecondInputStoreId = newSecondInput;
					next.SecondOutputStoreId = newSecondOutput;
					next.RouteDigest = newRouteDigest;
				}
			}
			next.Operation = operation;
			next.NextOperationOrdinal++;
			next.CreditCargoId = null;
			next.CreditCargoReceipt = null;
			if (bootstrap)
			{
				next.BootstrapUsed = true;
				next.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			}
			else if (returned)
			{
				next.ReturnUsed = true;
				next.Phase = KingdomPurposePairPhase.ReturnOutstanding;
			}
			else next.Phase = KingdomPurposePairPhase.OperationOutstanding;
			next.Revision++;
			// The return start is where the delivered bootstrap operation is dropped: its cargo was
			// consumed by the second shell's construction rather than by a credit, so no credit path
			// ever reaches it, and its landing marks would otherwise stand in the destination
			// larders and cut the next landing there as evidence of an owner nobody can name. Runs
			// before the endpoint-adopting publish, so a crash simply retries an idempotent retirement.
			if (!ExactPublishedPortfolioPair(Pair))
				return Fail("The purpose-pair register changed before this start could retire its cargo.",
					out Failure);
			if (!TryRetireCreditedPurposeCargo(Pair.Operation))
				return Fail("The delivered bootstrap landing could not be retired from the destination's custody; nothing was released.",
					out Failure);
			if (!TryPublishPortfolioPair(Pair, next, out Failure)) return false;
			Published = next;
			return true;
		}

		private static bool TryPortfolioOperationPreflight(
			KingdomPurposeOperationReceipt Operation, out string Failure)
		{
			Failure = null;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null) return false;
			// Refuse-new stands ahead of every ground read and every reservation, so a paused realm
			// cannot spend a bootstrap or return bit on an operation it may not advance.
			if (!KingdomMaster.NewWorkAllowed(system))
				return Fail("New purpose work is paused by realm transition authority.", out Failure);
			if (!TryOperationGround(Operation, out Zone sourceZone, out _,
				out GameObject input, out _, out Zone destinationZone, out _, out Failure))
				return false;
			if (!TryOperationGround(Operation, out _, out GameObject work, out _, out _, out _,
				out _, out Failure) || !TryPreflightBodyAuthority(system, work, Operation,
				out Failure)) return false;
			if (!FindLocalConnection(system, sourceZone,
				out KingdomPurposeConnection connection, out Failure)
				|| connection.SourceKey != Operation.SourceGateKey
				|| connection.DestinationKey != Operation.DestinationGateKey
				|| connection.DestinationZone.ZoneID != Operation.DestinationZoneId)
				return Fail(Failure ?? "The frozen reciprocal mirror route is unavailable.",
					out Failure);
			if (!Operation.BootstrapExemption && !Operation.ReturnExemption
				&& (FindExactKnown(sourceZone, Operation.InputCargoId, out GameObject cargo)
					!= KingdomPhysicalLookupState.Exact
					|| !ExactPortfolioCargo(cargo, Operation.InputCargoReceipt,
						Operation.SourceInputStoreId)))
				return Fail("The exact current-epoch incoming cargo is not in the frozen input store.",
					out Failure);
			if (!TryPreflightCarriedFood(system, Operation, destinationZone, out Failure))
				return false;
			if (!TryPreflightPurposeEffect(system, Operation, out Failure)) return false;
			return TryPlanLocalDebit(Operation, sourceZone, input, out _, out Failure);
		}

		private static bool SecondWorkAnswersCommitment(GameObject Work,
			KingdomPurposePairReceipt Pair)
		{
			if (!GameObject.Validate(Work)
				|| !KingdomPurposePortfolioRules.TryBuildKind(
					KingdomUpgrade.DesignKeyOf(Work), out var kind) || kind != Pair.SecondKind
				|| !KingdomPurposeRules.TryDecodeCommitment(
					Work.GetStringProperty(CommitmentProperty), out var commitment)) return false;
			return commitment.PortfolioPairId == Pair.PairId
				&& commitment.PortfolioEpoch == Pair.Epoch
				&& commitment.PortfolioOperationId == Pair.Operation?.OperationId
				&& commitment.ReciprocalCargoItemId == Pair.Operation?.OutputCargoId
				&& commitment.ReciprocalCargoReceipt == Pair.Operation?.OutputCargoReceipt;
		}

		private static bool TryPublishOperation(KingdomPurposePairReceipt Pair,
			KingdomPurposeOperationReceipt Operation, KingdomPurposePairPhase PairPhase,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			if (Pair == null || Pair.Operation == null || Operation == null
				|| Pair.Revision == int.MaxValue
				|| Pair.Operation.Revision == int.MaxValue)
				return Fail("The purpose operation exhausted its exact revision range.", out Failure);
			KingdomPurposePairReceipt next = Pair.Copy();
			next.Operation = Operation;
			if (Pair.Phase == KingdomPurposePairPhase.Orphaned)
			{
				next.Phase = KingdomPurposePairPhase.Orphaned;
				next.ResumePhase = PairPhase;
			}
			else
			{
				next.Phase = PairPhase;
				if (PairPhase == KingdomPurposePairPhase.CargoAwaitingConsumption)
					next.NextKind = Operation.DestinationKind;
			}
			next.Revision++;
			if (!TryPublishPortfolioPair(Pair, next, out Failure)) return false;
			Published = next;
			return true;
		}

		private static bool QuarantinePortfolio(KingdomPurposePairReceipt Pair, string Fault,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			if (Pair == null) return Fail("No purpose pair exists to quarantine.", out Failure);
			if (Pair.Revision == int.MaxValue)
				return Fail("The purpose pair exhausted its exact revision range before quarantine.",
					out Failure);
			KingdomPurposePairReceipt next = Pair.Copy();
			next.Phase = KingdomPurposePairPhase.Quarantined;
			next.ResumePhase = KingdomPurposePairPhase.Invalid;
			next.Fault = string.IsNullOrEmpty(Fault) ? "An exact purpose callback became ambiguous."
				: Fault.Length <= 700 ? Fault : Fault.Substring(0, 700);
			next.Revision++;
			if (!TryPublishPortfolioPair(Pair, next, out Failure)) return false;
			Published = next;
			return false;
		}

		private static bool SameOperationEvidence(KingdomPurposeOperationReceipt A,
			KingdomPurposeOperationReceipt B)
		{
			return A != null && B != null && A.Phase == B.Phase
				&& A.WaterSpent == B.WaterSpent && A.FoodSpent == B.FoodSpent
				&& A.MaterialSpent == B.MaterialSpent && A.EffectStep == B.EffectStep;
		}
	}
}
