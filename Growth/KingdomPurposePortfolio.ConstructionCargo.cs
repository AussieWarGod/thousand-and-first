using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		internal const string PortfolioCargoSchemaProperty = "r_TAF_PurposePairCargoSchema";
		internal const string PortfolioCargoReceiptProperty = "r_TAF_PurposePairCargoReceipt";
		internal const int PortfolioCargoSchema = 1;

		private static bool TryQuotePortfolioCargo(KingdomSystem System, Zone Z,
			KingdomPurposeDefinition Definition, string SettlementId,
			out KingdomPurposePairReceipt Pair, out GameObject Cargo, out string Failure)
		{
			Pair = null;
			Cargo = null;
			Failure = null;
			if (!TryReadPortfolioPair(out Pair, out Failure)) return false;
			bool intended = Pair != null && Pair.Phase != KingdomPurposePairPhase.Dormant
				&& Pair.SecondKind == Definition.Kind
				&& Pair.SecondSettlementId == SettlementId;
			if (!intended)
			{
				if (Pair == null) return true;
				Pair = null;
				return !Definition.PortfolioOnly || Fail(
					"This purpose has no frozen reciprocal bootstrap on this city's pair register.",
					out Failure);
			}
			if (Pair.Phase != KingdomPurposePairPhase.SecondPending
				|| Pair.Operation == null || !Pair.Operation.BootstrapExemption
				|| Pair.Operation.Phase != KingdomPurposeOperationPhase.Delivered
				|| Pair.Operation.DestinationKind != Definition.Kind)
				return Fail("The frozen reciprocal bootstrap has not landed and settled yet.",
					out Failure);
			return ResolvePortfolioCargo(Z, Pair, out Cargo, out Failure);
		}

		internal static bool ResolveCommitReciprocalCargo(Zone Z, string BuildKey,
			string Receipt, out GameObject Cargo, out string Failure)
		{
			Cargo = null;
			Failure = null;
			if (!KingdomPurposeRules.TryDecodeCommitment(Receipt, out var commitment))
				return Fail("The frozen purpose commitment is absent or malformed.", out Failure);
			if (string.IsNullOrEmpty(commitment.ReciprocalCargoItemId)) return true;
			if (!KingdomPurposePortfolioRules.TryBuildKind(BuildKey, out var kind)
				|| !TryReadPortfolioPair(out KingdomPurposePairReceipt pair, out Failure)
				|| pair == null || pair.PairId != commitment.PortfolioPairId
				|| pair.Epoch != commitment.PortfolioEpoch
				|| pair.SecondKind != kind || pair.Phase != KingdomPurposePairPhase.SecondPending
				|| pair.Operation == null
				|| pair.Operation.OperationId != commitment.PortfolioOperationId
				|| pair.Operation.OutputCargoId != commitment.ReciprocalCargoItemId
				|| pair.Operation.OutputCargoReceipt != commitment.ReciprocalCargoReceipt)
				return Fail("The reciprocal pair register no longer matches the frozen commission.",
					out Failure);
			return ResolvePortfolioCargo(Z, pair, out Cargo, out Failure);
		}

		private static bool ResolvePortfolioCargo(Zone Z, KingdomPurposePairReceipt Pair,
			out GameObject Cargo, out string Failure)
		{
			Cargo = null;
			Failure = null;
			if (Z == null || Pair?.Operation == null
				|| FindExactKnown(Z, Pair.Operation.OutputCargoId, out Cargo)
					!= KingdomPhysicalLookupState.Exact
				|| !ExactPortfolioCargo(Cargo, Pair.Operation.OutputCargoReceipt,
					Pair.SecondInputStoreId))
			{
				Cargo = null;
				return Fail("The exact reciprocal cargo left its frozen destination input store, changed, or was consumed. No same-kind object substitutes for it.",
					out Failure);
			}
			return true;
		}

		internal static bool ExactPortfolioCargo(GameObject Cargo, string Encoded,
			string ExpectedStoreId)
		{
			GameObject store = GameObject.Validate(Cargo) ? Cargo.InInventory : null;
			return ExactPortfolioCargoIdentity(Cargo, Encoded) && GameObject.Validate(store)
				&& store.IDIfAssigned == ExpectedStoreId && ExactOwned(Cargo, store);
		}

		private static bool ExactPortfolioCargoIdentity(GameObject Cargo, string Encoded)
		{
			return GameObject.Validate(Cargo) && Cargo.Count == 1 && !Cargo.HasPart("Stacker")
				&& KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(PortfolioCargoSchemaProperty),
					Cargo.HasStringProperty(PortfolioCargoSchemaProperty), true)
				&& Cargo.GetIntProperty(PortfolioCargoSchemaProperty) == PortfolioCargoSchema
				&& KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(PortfolioCargoReceiptProperty),
					Cargo.HasStringProperty(PortfolioCargoReceiptProperty), false)
				&& Cargo.GetStringProperty(PortfolioCargoReceiptProperty) == Encoded
				&& KingdomPurposePortfolioRules.TryDecodeCargo(Encoded, out var receipt)
				&& ExactPortfolioCargoEvidenceShape(Cargo, receipt)
				&& KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(PortfolioCargoKeyProperty),
					Cargo.HasStringProperty(PortfolioCargoKeyProperty), false)
				&& Cargo.GetStringProperty(PortfolioCargoKeyProperty) == receipt.CargoKey
				&& KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(PortfolioCargoFoodProperty),
					Cargo.HasStringProperty(PortfolioCargoFoodProperty), true)
				&& Cargo.GetIntProperty(PortfolioCargoFoodProperty) == receipt.CarriedFood
				&& Cargo.IDIfAssigned == receipt.ObjectId
				&& Cargo.Blueprint == KingdomMaterials.BlueprintFor(receipt.EmbodiedMaterial)
				&& KingdomMaterials.TryMaterialOf(Cargo, out KingdomMaterial material)
				&& material == receipt.EmbodiedMaterial;
		}

		/// <summary>Portfolio cargo may carry only its own four identity fields and either no
		/// landing record or this operation's exact current progress record. Any legacy field,
		/// serving marker, attempt, fault, torn record, or foreign receipt makes it inert.</summary>
		private static bool ExactPortfolioCargoEvidenceShape(GameObject Cargo,
			KingdomPurposeCargoReceipt Receipt)
		{
			if (Receipt == null || CargoFieldPresent(Cargo, CargoSchemaProperty)
				|| CargoFieldPresent(Cargo, CargoKeyProperty)
				|| CargoFieldPresent(Cargo, CargoManifestProperty)
				|| CargoFieldPresent(Cargo, CargoConsignmentProperty)
				|| CargoFieldPresent(Cargo, CargoOriginProperty)
				|| CargoFieldPresent(Cargo, CargoDestinationProperty)
				|| CargoFieldPresent(Cargo, PortfolioLandedFoodProperty)
				|| CargoFieldPresent(Cargo, PortfolioLandedAttemptProperty)
				|| CargoFieldPresent(Cargo, PortfolioLandedFaultProperty)
				|| CargoFieldPresent(Cargo, PortfolioEffectAttemptProperty)
				|| CargoFieldPresent(Cargo, PortfolioEffectReadyProperty)
				|| CargoFieldPresent(Cargo, PortfolioEffectOfferProperty)
				|| CargoFieldPresent(Cargo, PortfolioEffectCountProperty)
				|| CargoFieldPresent(Cargo, PortfolioEffectFaultProperty)
				|| CargoFieldPresent(Cargo, PortfolioEffectMarkProperty)
				|| CargoFieldPresent(Cargo, PortfolioEffectIndexProperty)
				|| !KingdomPurposePortfolioRules.TryLandingReceipt(Receipt.PairId,
					Receipt.PairEpoch, Receipt.OperationId, out string landingReceipt)) return false;
			return TryPurposeLandedRecord(Cargo, landingReceipt, Receipt.CarriedFood, out _);
		}

		/// <summary>Any schema-field presence reserves a legacy or reciprocal purpose token from
		/// ordinary civic material consumers. Wrong-typed or torn evidence stays protected; exact
		/// required-purpose funding separately reproves the complete receipt before admission.</summary>
		internal static bool HasProtectedCargoEvidence(GameObject Cargo)
		{
			return Cargo != null && KingdomPurposePortfolioRules.PurposeCargoIsProtected(
				new KingdomPurposeCargoEvidence
				{
					LegacySchema = CargoFieldPresent(Cargo, CargoSchemaProperty),
					LegacyKey = CargoFieldPresent(Cargo, CargoKeyProperty),
					LegacyManifest = CargoFieldPresent(Cargo, CargoManifestProperty),
					LegacyConsignment = CargoFieldPresent(Cargo, CargoConsignmentProperty),
					LegacyOrigin = CargoFieldPresent(Cargo, CargoOriginProperty),
					LegacyDestination = CargoFieldPresent(Cargo, CargoDestinationProperty),
					PortfolioSchema = CargoFieldPresent(Cargo, PortfolioCargoSchemaProperty),
					PortfolioReceipt = CargoFieldPresent(Cargo, PortfolioCargoReceiptProperty),
					PortfolioKey = CargoFieldPresent(Cargo, PortfolioCargoKeyProperty),
					PortfolioFood = CargoFieldPresent(Cargo, PortfolioCargoFoodProperty),
					LandedFood = CargoFieldPresent(Cargo, PortfolioLandedFoodProperty),
					LandedReceipt = CargoFieldPresent(Cargo, PortfolioLandedReceiptProperty),
					LandedCount = CargoFieldPresent(Cargo, PortfolioLandedCountProperty),
					LandedAttempt = CargoFieldPresent(Cargo, PortfolioLandedAttemptProperty),
					LandedFault = CargoFieldPresent(Cargo, PortfolioLandedFaultProperty),
					EffectAttempt = CargoFieldPresent(Cargo, PortfolioEffectAttemptProperty),
					EffectReady = CargoFieldPresent(Cargo, PortfolioEffectReadyProperty),
					EffectOffer = CargoFieldPresent(Cargo, PortfolioEffectOfferProperty),
					EffectCount = CargoFieldPresent(Cargo, PortfolioEffectCountProperty),
					EffectFault = CargoFieldPresent(Cargo, PortfolioEffectFaultProperty),
					EffectMark = CargoFieldPresent(Cargo, PortfolioEffectMarkProperty),
					EffectIndex = CargoFieldPresent(Cargo, PortfolioEffectIndexProperty)
				});
		}

		private static bool CargoFieldPresent(GameObject Cargo, string Property)
		{
			return Cargo != null
				&& (Cargo.HasIntProperty(Property) || Cargo.HasStringProperty(Property));
		}

		private static bool CommitmentMatchesBuild(KingdomPurposeCommitment Commitment,
			string BuildKey)
		{
			if (Commitment == null
				|| !KingdomPurposePortfolioRules.TryBuildKind(BuildKey, out var kind)) return false;
			bool legacy = !string.IsNullOrEmpty(Commitment.Manifest);
			if (legacy && (!KingdomPurposeRules.TryDecodeManifest(Commitment.Manifest,
				out var manifest) || manifest.BuildKey != BuildKey || manifest.Kind != kind))
				return false;
			bool reciprocal = !string.IsNullOrEmpty(Commitment.ReciprocalCargoReceipt);
			bool initial = !string.IsNullOrEmpty(Commitment.InitialBuildKey);
			if (initial) return !legacy && !reciprocal
				&& Commitment.InitialBuildKey == BuildKey;
			if (reciprocal && (!KingdomPurposePortfolioRules.TryDecodeCargo(
				Commitment.ReciprocalCargoReceipt, out var cargo)
				|| cargo.DestinationKind != kind)) return false;
			return legacy || reciprocal;
		}
	}
}
