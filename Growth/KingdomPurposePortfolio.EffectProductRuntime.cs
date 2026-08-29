using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static KingdomPurposeBodyDriveState DrivePurposeEffectProductBatch(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeOperationReceipt Operation,
			KingdomPurposeEffectProductRole Role, int Target,
			out bool Complete, out string Failure)
		{
			Complete = false;
			Failure = null;
			if (Context == null || Operation == null || Target < 1
				|| ProductCallback(Role) == KingdomPurposeEffectCallbackKind.Invalid)
				return InvalidEffect("The bounded-effect product boundary is invalid.", out Failure);
			for (int pass = 0; pass <= Target + 1; pass++)
			{
				if (PurposeEffectIsFaulted(Context.Work))
					return InvalidEffect("A durable bounded-effect fault already stands.", out Failure);
				if (!TryPurposeEffectProductCensus(Context, Operation,
					out KingdomPurposeEffectProductCensus census, out Failure))
					return FaultedEffect(Context.Work, EffectReceiptOrEmpty(Operation),
						Operation.EffectStep, "product-census", Failure, out Failure);
				if (census.AttemptPresent && census.Attempt.Step < Operation.EffectStep)
				{
					if (!TryRetirePublishedEffectAttempt(Context, Operation, census, out Failure))
						return FaultedEffect(Context.Work, census.Receipt, Operation.EffectStep,
							"retire-attempt", Failure, out Failure);
					continue;
				}
				if (census.AttemptPresent)
				{
					if (census.Attempt.Step != Operation.EffectStep
						|| census.Attempt.Callback != ProductCallback(Role))
						return FaultedEffect(Context.Work, census.Receipt, Operation.EffectStep,
							"product-attempt", "The product attempt names another boundary.",
							out Failure);
					KingdomPurposeBodyDriveState recovered = RecoverPurposeEffectProductAttempt(
						Context, Operation, census, Role, out bool retry, out Failure);
					if (recovered == KingdomPurposeBodyDriveState.Invalid || !retry) return recovered;
					continue;
				}
				int count = PurposeEffectProductCount(census, Role);
				if (count == Target)
				{
					Complete = true;
					return KingdomPurposeBodyDriveState.Applied;
				}
				if (count < 0 || count > Target)
					return FaultedEffect(Context.Work, census.Receipt, Operation.EffectStep,
						"product-bound", "The product high-water left its fixed recipe.",
						out Failure);
				if (KingdomSurvey.HeldIn(Context.Store) >= KingdomSurvey.CapacityOf(Context.Store))
					return WaitingEffect("This work's own store has no room for its next exact product.",
						out Failure);
				KingdomPurposeBodyDriveState offered = OfferPurposeEffectProduct(
					Context, Operation, census, Role, out bool settled, out Failure);
				if (offered != KingdomPurposeBodyDriveState.Applied) return offered;
				if (!settled) return WaitingEffect(Failure, out Failure);
			}
			return FaultedEffect(Context.Work, EffectReceiptOrEmpty(Operation),
				Operation.EffectStep, "product-loop",
				"The bounded product loop exceeded its fixed recipe.", out Failure);
		}

		private static KingdomPurposeBodyDriveState OfferPurposeEffectProduct(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeOperationReceipt Operation,
			KingdomPurposeEffectProductCensus Before,
			KingdomPurposeEffectProductRole Role, out bool Settled, out string Failure)
		{
			Settled = false;
			Failure = null;
			if (!KingdomPurposePortfolioRules.TryEffectProductReceipt(Before.Receipt, Role,
				out string productReceipt)
				|| !TryCapturePurposeEffectRoster(Context, null,
					KingdomPurposeEffectRosterMode.Exact, null, null, 0,
					out KingdomPurposeEffectRosterSnapshot beforeRoster, out Failure))
				return InvalidEffect(Failure ?? "The product receipt or roster is unavailable.",
					out Failure);
			GameObject product = ExactPurposeEffectProduct(Context, Role, productReceipt,
				Before.Prefilter);
			if (product == null)
				return InvalidEffect("The exact bounded-effect product cannot be created.", out Failure);
			string objectId = product.ID;
			int beforeCount = PurposeEffectProductCount(Before, Role);
			if (!TryPurposeEffectExpectedProductAfter(beforeRoster, product, productReceipt,
				Before.Prefilter, out string afterDigest, out Failure)
				|| !KingdomPurposePortfolioRules.TryEffectAttempt(Before.Receipt,
					Operation.EffectStep, ProductCallback(Role), objectId, beforeCount,
					beforeCount, beforeCount + 1, beforeRoster.Digest, afterDigest,
					out string witness)
				|| !StampPurposeEffectAttempt(Context.Work, witness))
			{
				if (!WithdrawUnownedPurposeEffectProduct(product))
					return FaultedEffect(Context.Work, Before.Receipt, Operation.EffectStep,
						"product-offer-witness", "An unwitnessed product could not be withdrawn.",
						out Failure);
				return FaultedEffect(Context.Work, Before.Receipt, Operation.EffectStep,
					"product-offer-witness", Failure ?? "The product attempt did not persist.",
					out Failure);
			}
			if (!KingdomPurposePortfolioRules.TryReadEffectAttempt(witness, Before.Receipt,
				out KingdomPurposeEffectAttempt attempt))
				return FaultedEffect(Context.Work, Before.Receipt, Operation.EffectStep,
					"product-attempt-read", "The product attempt cannot round-trip.", out Failure);

			GameObject accepted = null;
			bool threw = false;
			try { accepted = Context.Store.Inventory.AddObject(product, null,
				Silent: true, NoStack: true); }
			catch (Exception error)
			{
				threw = true;
				Failure = "The bounded-effect product callback threw ("
					+ error.GetType().Name + ").";
			}
			KingdomSurvey.ObserveAddResultInActive(Context.Zone, product, accepted);
			bool observed = TryObservePurposeEffectProduct(Context, Before, Role, attempt,
				out bool beforeExact, out bool afterExact, out string observationFailure);
			bool safelyUnowned = observed && beforeExact && GameObject.Validate(product)
				&& !product.IsInvalid() && !product.IsInGraveyard()
				&& product.InInventory == null && product.CurrentCell == null;
			KingdomPurposeEffectCallbackAftermath aftermath =
				KingdomPurposePortfolioRules.ClassifyEffectProductAftermath(true, threw,
					observed && afterExact, safelyUnowned);
			if (aftermath == KingdomPurposeEffectCallbackAftermath.Settled)
			{
				if (!RecordReleaseAndClearPurposeEffectProduct(Context, Before, Role,
					attempt, out Failure))
					return FaultedEffect(Context.Work, Before.Receipt, Operation.EffectStep,
						"product-checkpoint", Failure, out Failure);
				Settled = true;
				return KingdomPurposeBodyDriveState.Applied;
			}
			if (aftermath == KingdomPurposeEffectCallbackAftermath.Unavailable
				&& WithdrawUnownedPurposeEffectProduct(product)
				&& ClearPurposeEffectAttempt(Context.Work, witness))
				return WaitingEffect("The exact product callback made no physical change; retry it.",
					out Failure);
			return FaultedEffect(Context.Work, Before.Receipt, Operation.EffectStep,
				"product-aftermath", Failure ?? observationFailure
					?? "The product callback reached an unknown aftermath.", out Failure);
		}

		private static bool WithdrawUnownedPurposeEffectProduct(GameObject Product)
		{
			if (!GameObject.Validate(Product)) return true;
			if (Product.InInventory != null || Product.CurrentCell != null) return false;
			try { Product.Obliterate(); } catch { return false; }
			return !GameObject.Validate(Product);
		}

		private static string EffectReceiptOrEmpty(KingdomPurposeOperationReceipt Operation)
		{
			return TryPurposeEffectScope(Operation, out string receipt, out _) ? receipt : "invalid";
		}
	}
}
