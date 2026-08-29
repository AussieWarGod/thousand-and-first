using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static KingdomPurposeBodyDriveState RecoverPurposeEffectProductAttempt(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeOperationReceipt Operation,
			KingdomPurposeEffectProductCensus Census,
			KingdomPurposeEffectProductRole Role, out bool Retry, out string Failure)
		{
			Retry = false;
			Failure = null;
			KingdomPurposeEffectAttempt attempt = Census.Attempt;
			int recorded = PurposeEffectProductCount(Census, Role);
			if (attempt == null || attempt.ExpectedProgress < 1
				|| recorded < attempt.ExpectedProgress - 1
				|| recorded > attempt.ExpectedProgress)
				return FaultedEffect(Context.Work, Census.Receipt, Operation.EffectStep,
					"recover-product-range", "The product high-water cannot match its attempt.",
					out Failure);
			if (recorded == attempt.ExpectedProgress)
			{
				if (!RecordReleaseAndClearPurposeEffectProduct(Context, Census, Role,
					attempt, out Failure))
					return FaultedEffect(Context.Work, Census.Receipt, Operation.EffectStep,
						"recover-product-release", Failure, out Failure);
				Retry = true;
				return KingdomPurposeBodyDriveState.Applied;
			}
			if (!TryObservePurposeEffectProduct(Context, Census, Role, attempt,
				out bool before, out bool after, out Failure))
				return FaultedEffect(Context.Work, Census.Receipt, Operation.EffectStep,
					"recover-product-roster", Failure, out Failure);
			KingdomPurposeEffectCallbackAftermath aftermath =
				KingdomPurposePortfolioRules.ClassifyEffectProductAftermath(true, false,
					after, before);
			if (aftermath == KingdomPurposeEffectCallbackAftermath.Settled)
			{
				if (!RecordReleaseAndClearPurposeEffectProduct(Context, Census, Role,
					attempt, out Failure))
					return FaultedEffect(Context.Work, Census.Receipt, Operation.EffectStep,
						"recover-product-checkpoint", Failure, out Failure);
				Retry = true;
				return KingdomPurposeBodyDriveState.Applied;
			}
			if (aftermath == KingdomPurposeEffectCallbackAftermath.Unavailable)
			{
				string witness = KingdomPurposePortfolioRules.EncodeEffectAttempt(attempt);
				FindPortfolioObject(attempt.ObjectId, out GameObject unowned, out _);
				if (!WithdrawUnownedPurposeEffectProduct(unowned)
					|| !ClearPurposeEffectAttempt(Context.Work, witness))
					return FaultedEffect(Context.Work, Census.Receipt, Operation.EffectStep,
						"recover-product-before", "The no-change product attempt cannot retire.",
						out Failure);
				return WaitingEffect("The witnessed product offer made no physical change; retry it.",
					out Failure);
			}
			return FaultedEffect(Context.Work, Census.Receipt, Operation.EffectStep,
				"recover-product-aftermath", "The product roster has an unknown aftermath.",
				out Failure);
		}

		private static bool TryObservePurposeEffectProduct(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeEffectProductCensus Census,
			KingdomPurposeEffectProductRole Role, KingdomPurposeEffectAttempt Attempt,
			out bool Before, out bool After, out string Failure)
		{
			Before = false;
			After = false;
			Failure = null;
			if (Census == null || Attempt == null
				|| !KingdomPurposePortfolioRules.TryEffectProductReceipt(Census.Receipt, Role,
					out string productReceipt)) return false;
			KingdomPhysicalLookupState state = FindPortfolioObject(Attempt.ObjectId,
				out GameObject product, out bool graveyard);
			if (state == KingdomPhysicalLookupState.Ambiguous) return false;
			bool direct = state == KingdomPhysicalLookupState.Exact && !graveyard
				&& ReferenceEquals(product.InInventory, Context.Store)
				&& product.CurrentCell == null && Context.Store.Inventory != null
				&& Context.Store.Inventory.InventoryContains(product);
			int stage = direct
				? PurposeEffectProductReleaseStage(product, productReceipt, Census.Prefilter) : -1;
			KingdomPurposeEffectRosterMode mode = stage >= 0
				? KingdomPurposeEffectRosterMode.ProductRelease
				: KingdomPurposeEffectRosterMode.Exact;
			if (!TryCapturePurposeEffectRoster(Context, stage >= 0 ? Attempt.ObjectId : null,
				mode, null, stage >= 0 ? productReceipt : null, Census.Prefilter,
				out KingdomPurposeEffectRosterSnapshot roster, out Failure)
				|| !PurposeEffectEvidenceOnlyOnWorkOrProducts(Context,
					out IList<GameObject> loaded, out Failure)) return false;
			GameObject carrier = null;
			for (int i = 0; i < loaded.Count; i++)
				if (!ReferenceEquals(loaded[i], Context.Work) && AnyPurposeEffectField(loaded[i]))
				{
					if (carrier != null) return false;
					carrier = loaded[i];
				}
			Before = roster.Digest == Attempt.BeforeRosterDigest && carrier == null && !direct;
			After = roster.Digest == Attempt.AfterRosterDigest && direct && stage == 0
				&& ReferenceEquals(carrier, product)
				&& ExactPurposeEffectProductCustody(Context, product, Role, productReceipt,
					Census.Prefilter, false);
			return true;
		}

		private static bool RecordReleaseAndClearPurposeEffectProduct(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeEffectProductCensus Census,
			KingdomPurposeEffectProductRole Role, KingdomPurposeEffectAttempt Attempt,
			out string Failure)
		{
			Failure = null;
			if (Census == null || Attempt == null) return false;
			int recorded = PurposeEffectProductCount(Census, Role);
			if (recorded == Attempt.ExpectedProgress - 1)
			{
				if (!TryObservePurposeEffectProduct(Context, Census, Role, Attempt,
					out _, out bool after, out Failure) || !after)
					return Fail(Failure ?? "The exact new product is not at its frozen after roster.",
						out Failure);
				KingdomPurposeEffectProductRecord next = Census.Recorded;
				if (Role == KingdomPurposeEffectProductRole.Refined) next.Refined++;
				else if (Role == KingdomPurposeEffectProductRole.Seed) next.Seed++;
				else if (Role == KingdomPurposeEffectProductRole.Staple) next.Staple++;
				else return false;
				if (PurposeEffectRecordedCount(next, Role) != Attempt.ExpectedProgress
					|| !RecordPurposeEffectProducts(Context.Work, Census.Receipt, next))
					return Fail("The product high-water did not persist exactly.", out Failure);
			}
			else if (recorded != Attempt.ExpectedProgress)
				return Fail("The product attempt and high-water diverged.", out Failure);
			return TryReleasePurposeEffectProduct(Context, Census, Role, Attempt, out Failure);
		}

		private static bool TryReleasePurposeEffectProduct(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeEffectProductCensus Census,
			KingdomPurposeEffectProductRole Role, KingdomPurposeEffectAttempt Attempt,
			out string Failure)
		{
			Failure = null;
			string witness = KingdomPurposePortfolioRules.EncodeEffectAttempt(Attempt);
			if (string.IsNullOrEmpty(witness)
				|| !KingdomPurposePortfolioRules.TryEffectProductReceipt(Census.Receipt, Role,
					out string productReceipt)) return false;
			KingdomPhysicalLookupState state = FindPortfolioObject(Attempt.ObjectId,
				out GameObject product, out bool graveyard);
			int stage = state == KingdomPhysicalLookupState.Exact && !graveyard
				? PurposeEffectProductReleaseStage(product, productReceipt, Census.Prefilter) : -1;
			bool ready = OwnedFieldPresent(Context.Work, PortfolioEffectReadyProperty);
			if (ready && !ExactPurposeEffectReady(Context.Work, witness))
				return Fail("The product release checkpoint is torn.", out Failure);
			if (!ready && (stage == 0 || stage == 1))
			{
				if (!ExactPurposeEffectProductCustody(Context, product, Role, productReceipt,
					Census.Prefilter, true)
					|| !TryCapturePurposeEffectRoster(Context, Attempt.ObjectId,
						KingdomPurposeEffectRosterMode.ProductRelease, null, productReceipt,
						Census.Prefilter, out KingdomPurposeEffectRosterSnapshot roster, out Failure)
					|| roster.Digest != Attempt.AfterRosterDigest)
					return Fail(Failure ?? "The protected product changed before release.", out Failure);
				if (stage == 0)
				{
					product.RemoveStringProperty(PortfolioEffectMarkProperty);
					stage = PurposeEffectProductReleaseStage(product, productReceipt, Census.Prefilter);
					if (stage != 1) return Fail("The product mark removal did not checkpoint.",
						out Failure);
				}
				if (!StampPurposeEffectReady(Context.Work, witness))
					return Fail("The per-product release checkpoint did not persist.", out Failure);
				ready = true;
			}
			if (ready)
			{
				if (stage == 0 || graveyard && AnyPurposeEffectField(product))
					return Fail("Release-ready evidence precedes exact mark retirement.", out Failure);
				if (stage == 1)
				{
					product.RemoveIntProperty(PortfolioEffectIndexProperty);
					stage = PurposeEffectProductReleaseStage(product, productReceipt, Census.Prefilter);
					if (stage != 2) return Fail("The product index removal did not checkpoint.",
						out Failure);
				}
				if (stage == 2)
				{
					product.RemoveIntProperty("NeverStack");
					stage = PurposeEffectProductReleaseStage(product, productReceipt, Census.Prefilter);
					if (stage != 3) return Fail("The product release shape did not checkpoint.",
						out Failure);
				}
				if (stage < 0 && state == KingdomPhysicalLookupState.Exact
					&& AnyPurposeEffectField(product))
					return Fail("Protected product evidence moved during release.", out Failure);
				if (!ClearPurposeEffectReady(Context.Work, witness))
					return Fail("The completed product release checkpoint could not retire.",
						out Failure);
			}
			else if (stage != 3)
				return Fail("An uncheckpointed product release is incomplete.", out Failure);
			return ClearPurposeEffectAttempt(Context.Work, witness)
				|| Fail("The released product attempt could not retire.", out Failure);
		}

		private static bool TryRetirePublishedEffectAttempt(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeOperationReceipt Operation,
			KingdomPurposeEffectProductCensus Census, out string Failure)
		{
			Failure = null;
			if (!TryPurposeEffectScope(Operation, out string receipt, out _)
				|| !TryReadPurposeEffectAttempt(Context.Work, receipt,
					out KingdomPurposeEffectAttempt attempt, out bool present)
				|| !present || attempt.Step + 1 != Operation.EffectStep
				|| !TryExpectedEffectCallback(Operation.SourceKind, attempt.Step,
					out KingdomPurposeEffectCallbackKind expected)
				|| expected != attempt.Callback)
				return Fail("A prior attempt does not name the published predecessor step.",
					out Failure);
			if (attempt.Callback == KingdomPurposeEffectCallbackKind.RefineRaw
				|| attempt.Callback == KingdomPurposeEffectCallbackKind.HarvestCrop)
				return TryRetirePublishedDebitReservation(Context, attempt, out Failure);
			KingdomPurposeEffectProductRole role = ProductRole(attempt.Callback);
			return Census != null && role != KingdomPurposeEffectProductRole.Invalid
				&& PurposeEffectRecordedCount(Census.Recorded, role) >= attempt.ExpectedProgress
				&& RecordReleaseAndClearPurposeEffectProduct(Context, Census, role, attempt,
					out Failure)
				|| Fail(Failure ?? "A prior product attempt is not exactly settled.", out Failure);
		}

		private static int PurposeEffectRecordedCount(
			KingdomPurposeEffectProductRecord Record, KingdomPurposeEffectProductRole Role)
		{
			return Role == KingdomPurposeEffectProductRole.Refined ? Record.Refined
				: Role == KingdomPurposeEffectProductRole.Seed ? Record.Seed
					: Role == KingdomPurposeEffectProductRole.Staple ? Record.Staple : -1;
		}

		private static KingdomPurposeEffectProductRole ProductRole(
			KingdomPurposeEffectCallbackKind Callback)
		{
			return Callback == KingdomPurposeEffectCallbackKind.RefinedProduct
				? KingdomPurposeEffectProductRole.Refined
				: Callback == KingdomPurposeEffectCallbackKind.HarvestSeed
					? KingdomPurposeEffectProductRole.Seed
					: Callback == KingdomPurposeEffectCallbackKind.HarvestStaple
						? KingdomPurposeEffectProductRole.Staple
						: KingdomPurposeEffectProductRole.Invalid;
		}
	}
}
