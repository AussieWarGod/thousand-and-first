using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool BeginPurposeOutput(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			if (!TryOperationGround(Pair?.Operation, out _, out _, out _, out _, out _, out _,
				out Failure)) return false;
			if (!TryRetireCompletedPurposeEffect(Pair, out Failure))
				return QuarantinePortfolio(Pair, Failure, out Published, out Failure);
			KingdomPurposeOperationReceipt next = Pair.Operation.Copy();
			next.Phase = KingdomPurposeOperationPhase.OutputPending;
			next.OutputBeforeDigest = PurposeDigest("purpose-output", next.PairId,
				next.OperationId, "absent");
			next.Revision++;
			return TryPublishOperation(Pair, next, Pair.Phase, out Published, out Failure);
		}

		private static bool DrivePurposeOutput(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (!TryOperationGround(operation, out Zone zone, out _, out _,
				out GameObject output, out _, out _, out Failure)) return false;
			if (string.IsNullOrEmpty(operation.OutputCargoId))
			{
				if (!TryPreparePurposeCargo(Pair, out GameObject prepared,
					out KingdomPurposeCargoReceipt cargoReceipt, out Failure)) return false;
				KingdomPurposeOperationReceipt adopted = operation.Copy();
				adopted.OutputCargoId = prepared.ID;
				adopted.OutputCargoReceipt = KingdomPurposePortfolioRules.EncodeCargo(cargoReceipt);
				adopted.TransportJobId = cargoReceipt.TransportJobId;
				adopted.Revision++;
				return TryPublishOperation(Pair, adopted, Pair.Phase,
					out Published, out Failure);
			}
			if (!TryRootedPurposeCargo(operation, out GameObject cargo)
				|| !ExactPortfolioCargoIdentity(cargo, operation.OutputCargoReceipt))
				return QuarantinePortfolio(Pair,
					"The rooted purpose output identity is absent or changed.",
					out Published, out Failure);
			string after = PurposeDigest("purpose-output", operation.OutputCargoId,
				operation.OutputCargoReceipt, operation.SourceOutputStoreId, "present");
			if (!ReferenceEquals(cargo.InInventory, output))
			{
				if (cargo.InInventory != null || cargo.CurrentCell != null)
					return QuarantinePortfolio(Pair,
						"The purpose output has an unexpected owner before settlement.",
						out Published, out Failure);
				GameObject accepted;
				try { accepted = output.Inventory.AddObject(cargo, null, Silent: true, NoStack: true); }
				catch (Exception ex)
				{
					Failure = "Purpose output placement waits after callback: " + ex.Message;
					return false;
				}
				KingdomSurvey.ObserveAddResultInActive(zone, cargo, accepted);
				if (!ReferenceEquals(accepted, cargo) || !ReferenceEquals(cargo.InInventory, output))
					return QuarantinePortfolio(Pair,
						"The purpose output store replaced or rejected its exact object.",
						out Published, out Failure);
			}
			KingdomPurposeOperationReceipt next = operation.Copy();
			next.Phase = KingdomPurposeOperationPhase.Dispatching;
			next.OutputAfterDigest = after;
			next.Revision++;
			return TryPublishOperation(Pair, next, Pair.Phase, out Published, out Failure);
		}

		private static bool DrivePurposeDispatch(KingdomSystem System,
			KingdomPurposePairReceipt Pair, out KingdomPurposePairReceipt Published,
			out string Failure)
		{
			Published = Pair;
			Failure = null;
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (!TryOperationGround(operation, out Zone sourceZone, out _, out _,
				out GameObject source, out Zone destinationZone, out GameObject destination,
				out Failure)) return false;
			if (!FindLocalConnection(System, sourceZone, out KingdomPurposeConnection connection,
				out Failure) || connection.SourceKey != operation.SourceGateKey
				|| connection.DestinationKey != operation.DestinationGateKey
				|| connection.DestinationZone.ZoneID != operation.DestinationZoneId)
				return Fail(Failure ?? "The frozen reciprocal mirror route is unavailable.", out Failure);
			if (!TryRootedPurposeCargo(operation, out GameObject cargo)
				|| !ExactPortfolioCargoIdentity(cargo, operation.OutputCargoReceipt))
				return QuarantinePortfolio(Pair, "The dispatch cargo lost its exact rooted identity.",
					out Published, out Failure);
			if (ReferenceEquals(cargo.InInventory, destination))
				return QuarantinePortfolio(Pair,
					"Purpose cargo landed before its in-flight receipt was published.",
					out Published, out Failure);
			if (ReferenceEquals(cargo.InInventory, source))
			{
				bool removed;
				try { removed = source.Inventory.RemoveObjectFromInventory(cargo, null,
					Silent: true, NoStack: true); }
				catch (Exception ex)
				{
					Failure = "Purpose dispatch source removal waits: " + ex.Message;
					return false;
				}
				KingdomSurvey.ObserveChangedInActive(sourceZone, source);
				if (!removed && ReferenceEquals(cargo.InInventory, source)) return false;
			}
			if (cargo.InInventory != null || cargo.CurrentCell != null)
				return QuarantinePortfolio(Pair, "Purpose cargo left its frozen route custody.",
					out Published, out Failure);
			KingdomPurposeOperationReceipt picked = operation.Copy();
			picked.Phase = KingdomPurposeOperationPhase.PickupComplete;
			picked.Revision++;
			return TryPublishOperation(Pair, picked, Pair.Phase, out Published, out Failure);
		}

		private static bool AcknowledgePurposeTransit(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (operation == null || !TryRootedPurposeCargo(operation, out GameObject cargo)
				|| !ExactPortfolioCargoIdentity(cargo, operation.OutputCargoReceipt))
				return QuarantinePortfolio(Pair,
					"The in-flight purpose cargo lost its exact rooted identity.",
					out Published, out Failure);
			if (cargo.InInventory != null || cargo.CurrentCell != null)
				return QuarantinePortfolio(Pair,
					"The in-flight purpose cargo acquired an unreceipted owner.",
					out Published, out Failure);
			KingdomPurposeOperationReceipt next = operation.Copy();
			next.Phase = KingdomPurposeOperationPhase.LandingPending;
			next.Revision++;
			return TryPublishOperation(Pair, next, Pair.Phase, out Published, out Failure);
		}

		private static bool DrivePurposeLanding(KingdomSystem System,
			KingdomPurposePairReceipt Pair, out KingdomPurposePairReceipt Published,
			out string Failure)
		{
			Published = Pair;
			Failure = null;
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (!TryOperationGround(operation, out Zone sourceZone, out _, out _, out _,
				out Zone destinationZone, out GameObject destination, out Failure)) return false;
			if (!FindLocalConnection(System, sourceZone, out KingdomPurposeConnection connection,
				out Failure) || connection.SourceKey != operation.SourceGateKey
				|| connection.DestinationKey != operation.DestinationGateKey
				|| connection.DestinationZone.ZoneID != operation.DestinationZoneId)
				return Fail(Failure ?? "The frozen reciprocal mirror route is unavailable.",
					out Failure);
			if (!TryRootedPurposeCargo(operation, out GameObject cargo)
				|| !ExactPortfolioCargoIdentity(cargo, operation.OutputCargoReceipt))
				return QuarantinePortfolio(Pair, "The landing cargo lost its exact rooted identity.",
					out Published, out Failure);
			if (!ReferenceEquals(cargo.InInventory, destination))
			{
				if (cargo.InInventory != null || cargo.CurrentCell != null)
					return QuarantinePortfolio(Pair,
						"Purpose cargo left its frozen route before landing.",
						out Published, out Failure);
				GameObject accepted;
				try { accepted = destination.Inventory.AddObject(cargo, null,
					Silent: true, NoStack: true); }
				catch (Exception ex)
				{
					Failure = "Purpose landing destination placement waits: " + ex.Message;
					return false;
				}
				KingdomSurvey.ObserveAddResultInActive(destinationZone, cargo, accepted);
				if (!ReferenceEquals(accepted, cargo)
					|| !ReferenceEquals(cargo.InInventory, destination))
					return QuarantinePortfolio(Pair,
						"Purpose destination replaced or rejected the exact cargo.",
						out Published, out Failure);
			}
			if (!TryLandCarriedFood(System, operation, cargo, destinationZone, out bool ambiguous,
				out string landing))
				return !ambiguous ? Fail(landing, out Failure)
					: QuarantinePortfolio(Pair, landing, out Published, out Failure);
			if (!PurposeLandingStillExact(operation, cargo, out string moved))
				return QuarantinePortfolio(Pair, moved, out Published, out Failure);
			KingdomPurposePairPhase delivered = Pair.Phase
				== KingdomPurposePairPhase.BootstrapOutstanding
					? KingdomPurposePairPhase.SecondPending
				: Pair.Phase == KingdomPurposePairPhase.ReturnOutstanding
					? KingdomPurposePairPhase.CargoAwaitingActivation
					: Pair.Phase == KingdomPurposePairPhase.Orphaned
						&& Pair.ResumePhase == KingdomPurposePairPhase.BootstrapOutstanding
						? KingdomPurposePairPhase.SecondPending
					: Pair.Phase == KingdomPurposePairPhase.Orphaned
						&& Pair.ResumePhase == KingdomPurposePairPhase.ReturnOutstanding
						? KingdomPurposePairPhase.CargoAwaitingActivation
					: KingdomPurposePairPhase.CargoAwaitingConsumption;
			KingdomPurposeOperationReceipt next = operation.Copy();
			next.Phase = KingdomPurposeOperationPhase.Delivered;
			next.Revision++;
			if (!TryPublishOperation(Pair, next, delivered, out Published, out Failure)) return false;
			NotePurposeProvisionArrival(System, operation);
			return true;
		}

		/// <summary>Everything the Delivered checkpoint is about to assert, reproved after the last
		/// provision callback has run. Placing servings in the destination's larders runs engine
		/// code that can reach the cargo itself, so identity, root and custody are all measured
		/// again rather than carried forward from before the callbacks. Each is a separate refusal,
		/// because each names a different thing that went wrong.</summary>
		private static bool PurposeLandingStillExact(KingdomPurposeOperationReceipt Operation,
			GameObject Cargo, out string Fault)
		{
			Fault = null;
			if (!ExactPortfolioCargoIdentity(Cargo, Operation.OutputCargoReceipt))
				return Fail("The landing cargo lost its exact identity under the provision callbacks.",
					out Fault);
			if (!TryRootedPurposeCargoExact(Operation, out GameObject rooted)
				|| !ReferenceEquals(rooted, Cargo))
				return Fail("The landing cargo lost its canonical root under the provision callbacks.",
					out Fault);
			// A reference the caller happens to hold is not a store proof: the store must still be
			// what this operation's own frozen id and ground resolve to.
			if (!TryExactDestinationStore(Operation, out GameObject store, out Fault)) return false;
			// Custody is proved in both directions. The parent pointer alone is a claim the store
			// never has to agree with: a callback can drop the cargo out of the inventory's own
			// list and leave the pointer standing (XRL/World/Parts/Inventory.cs:819-822), and the
			// fresh custody walk, which reads that list, would then never see the cargo at all.
			return ReferenceEquals(Cargo.InInventory, store) && Cargo.CurrentCell == null
				&& store.Inventory.InventoryContains(Cargo)
				|| Fail("The landing cargo left the frozen destination store under the provision callbacks.",
					out Fault);
		}

		private static bool TryPreparePurposeCargo(KingdomPurposePairReceipt Pair,
			out GameObject Cargo, out KingdomPurposeCargoReceipt Receipt, out string Failure)
		{
			Cargo = null;
			Receipt = null;
			Failure = null;
			string key = PurposeCargoRootKey(Pair.Operation);
			object rooted = null;
			if (The.Game != null && The.Game.ObjectGameState.TryGetValue(key, out rooted))
				Cargo = rooted as GameObject;
			if (!GameObject.Validate(Cargo))
			{
				if (!KingdomPurposePortfolioRules.TryRecipe(Pair.Operation.SourceKind,
					Pair.Operation.DestinationKind, out var recipe)) return false;
				try { Cargo = GameObject.Create(KingdomMaterials.BlueprintFor(
					recipe.EmbodiedMaterial)); }
				catch (Exception ex) { return Fail("Purpose cargo creation failed: " + ex.Message,
					out Failure); }
				if (!GameObject.Validate(Cargo) || Cargo.Count != 1) return false;
				try { Cargo.RemovePart("Stacker"); }
				catch { return Fail("Purpose cargo could not become an exact unit.", out Failure); }
				The.Game.ObjectGameState[key] = Cargo;
			}
			Cargo.SetIntProperty("NeverStack", 1);
			string job = "purpose-trip-" + PurposeDigest(Pair.PairId,
				Pair.Operation.OperationId).Substring(0, 24);
			if (!KingdomPurposePortfolioRules.TryCreateCargo(Pair, Pair.Operation, Cargo.ID, job,
				out Receipt, out var fault)) return Fail("Purpose cargo receipt failed (" + fault + ").",
				out Failure);
			string encoded = KingdomPurposePortfolioRules.EncodeCargo(Receipt);
			Cargo.SetIntProperty(PortfolioCargoSchemaProperty, PortfolioCargoSchema);
			Cargo.SetStringProperty(PortfolioCargoReceiptProperty, encoded);
			Cargo.SetStringProperty(PortfolioCargoKeyProperty, Receipt.CargoKey);
			Cargo.SetIntProperty(PortfolioCargoFoodProperty, Receipt.CarriedFood);
			Cargo.DisplayName = Receipt.CargoKey.Replace('-', ' ');
			return ExactPortfolioCargoIdentity(Cargo, encoded)
				|| Fail("The rooted purpose cargo failed semantic reproval.", out Failure);
		}
	}
}
