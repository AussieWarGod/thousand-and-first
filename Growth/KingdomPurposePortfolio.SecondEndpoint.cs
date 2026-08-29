using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		/// <summary>Prepares the sole second-root endpoint adoption without mutating world stock
		/// or either durable receipt. Legacy roots retain their already frozen endpoint identities.</summary>
		private static bool TryPrepareSecondEndpoint(GameObject Work,
			KingdomPurposePairReceipt Pair, out string InputId, out string OutputId,
			out string RouteDigest, out bool Adopts, out string Failure)
		{
			InputId = Pair?.SecondInputStoreId;
			OutputId = Pair?.SecondOutputStoreId;
			RouteDigest = Pair?.RouteDigest;
			Adopts = false;
			Failure = null;
			if (Pair?.Phase != KingdomPurposePairPhase.SecondPending
				|| !string.IsNullOrEmpty(Pair.SecondWorkId)
				|| !SecondWorkAnswersCommitment(Work, Pair)
				|| !TryPurposeZone(Pair.SecondZoneId, out Zone zone)
				|| Work.CurrentZone != zone
				|| FindExactKnown(zone, Work.IDIfAssigned, out GameObject exactWork)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactWork, Work))
				return Fail("The commissioned second purpose is not the exact pending endpoint.",
					out Failure);

			if (!TryAuthoredPurposeStores(zone, Work, out GameObject input,
				out GameObject output, out bool declared, out Failure)) return false;
			if (declared)
			{
				InputId = input.IDIfAssigned;
				OutputId = output.IDIfAssigned;
				if (!KingdomPurposePortfolioRules.TryRouteDigest(Pair.RealmId,
					Pair.FirstSettlementId, Pair.SecondSettlementId, Pair.FirstGateKey,
					Pair.SecondGateKey, Pair.FirstZoneId, Pair.SecondZoneId,
					Pair.FirstInputStoreId, Pair.FirstOutputStoreId, InputId, OutputId,
					out RouteDigest)) return Fail(
						"The second purpose's authored endpoint cannot authenticate this route.",
						out Failure);
				Adopts = InputId != Pair.SecondInputStoreId
					|| OutputId != Pair.SecondOutputStoreId
					|| RouteDigest != Pair.RouteDigest;
			}
			else if (!TryFrozenPurposeStores(zone, Pair.SecondInputStoreId,
				Pair.SecondOutputStoreId, out _, out _, out Failure)) return false;

			return TryProveBootstrapConstructionSettled(Work, zone, Pair, out Failure);
		}

		/// <summary>Fresh proof that the old destination cargo entered this exact completed
		/// construction debit. No cargo is moved or destroyed here; only an absent/graveyard
		/// aftermath and the terminal registry receipt are accepted.</summary>
		private static bool TryProveBootstrapConstructionSettled(GameObject Work, Zone Zone,
			KingdomPurposePairReceipt Pair, out string Failure)
		{
			Failure = null;
			string receipt = Work?.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (string.IsNullOrEmpty(receipt)
				|| !KingdomConstruction.TryFind(receipt, out KingdomConstructionJob job)
				|| job.Route != KingdomConstructionRoute.PlotCommission
				|| job.Phase != KingdomConstructionPhase.Complete
				|| job.PhysicalPhase != KingdomPhysicalPhase.EffectsSettled
				|| job.OutputId != Work.IDIfAssigned || job.ZoneId != Pair.SecondZoneId
				|| job.TargetKey != KingdomPurposePortfolioRules.BuildKey(Pair.SecondKind)
				|| string.IsNullOrEmpty(job.InputReceiptHash)
				|| !KingdomConstructionRules.FullyFundedExact(job)
				|| !KingdomConstruction.PaidBuildMatches(Work, job)
				|| !KingdomConstruction.HasReceipt(Work, job)
				|| !KingdomConstruction.IsCurrent(job)
				|| !KingdomConstruction.Owns(system, Zone, job))
				return Fail("The second purpose lacks its exact settled construction-custody proof.",
					out Failure);
			if (!job.Compacted
				&& (!KingdomConstructionRules.TryGetInputReceipt(job,
					out KingdomConstructionInputReceipt inputReceipt)
					|| inputReceipt.TxPhase != KingdomConstructionInputTxPhase.Committed
					|| !inputReceipt.RequiresObject(Pair.Operation.OutputCargoId)))
				return Fail(
					"The live construction receipt does not name the bootstrap cargo as a committed required object.",
					out Failure);
			KingdomPhysicalLookupState cargoState = FindPortfolioObject(
				Pair.Operation.OutputCargoId, out _, out bool graveyard);
			if (cargoState == KingdomPhysicalLookupState.Ambiguous
				|| (cargoState == KingdomPhysicalLookupState.Exact && !graveyard))
				return Fail("The bootstrap cargo is not freshly proved consumed by construction.",
					out Failure);
			return cargoState == KingdomPhysicalLookupState.Absent || graveyard;
		}
	}
}
