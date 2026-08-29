using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryPurposeEffectProductCensus(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeOperationReceipt Operation,
			out KingdomPurposeEffectProductCensus Census, out string Failure)
		{
			Census = null;
			Failure = null;
			if (Context == null || Operation == null
				|| !TryPurposeEffectScope(Operation, out string receipt, out int prefilter)
				|| !PurposeEffectEvidenceOnlyOnWorkOrProducts(Context,
					out IList<GameObject> loaded, out Failure)
				|| !TryReadPurposeEffectProducts(Context.Work, receipt,
					out KingdomPurposeEffectProductRecord recorded)
				|| !TryReadPurposeEffectAttempt(Context.Work, receipt,
					out KingdomPurposeEffectAttempt attempt, out bool attemptPresent))
				return Fail(Failure ?? "The bounded-effect record is torn or foreign.",
					out Failure);
			KingdomPurposeEffectProductCensus census =
				new KingdomPurposeEffectProductCensus
				{
					Receipt = receipt, Prefilter = prefilter, Recorded = recorded,
					Attempt = attempt, AttemptPresent = attemptPresent,
					Refined = recorded.Refined, Seed = recorded.Seed,
					Staple = recorded.Staple
				};
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (ReferenceEquals(item, Context.Work) || !AnyPurposeEffectField(item))
					continue;
				if (census.EvidenceCarrier != null)
					return Fail("More than one physical object carries one effect attempt.",
						out Failure);
				census.EvidenceCarrier = item;
			}
			if (!attemptPresent && census.EvidenceCarrier != null)
				return Fail("Physical effect evidence has no owning attempt.", out Failure);
			if (attemptPresent && ProductRole(attempt.Callback)
				!= KingdomPurposeEffectProductRole.Invalid)
			{
				if (census.EvidenceCarrier != null
						&& census.EvidenceCarrier.IDIfAssigned != attempt.ObjectId)
					return Fail("The product attempt carries debit-ready or foreign evidence.",
						out Failure);
			}
			Census = census;
			return true;
		}

		private static bool ExactPurposeEffectProductCustody(
			KingdomPurposeEffectRuntimeContext Context, GameObject Product,
			KingdomPurposeEffectProductRole Role, string ProductReceipt, int Prefilter,
			bool AllowRelease)
		{
			if (!GameObject.Validate(Context?.Store) || !GameObject.Validate(Product)
				|| !ReferenceEquals(Product.InInventory, Context.Store)
				|| Product.CurrentCell != null || Context.Store.Inventory == null
				|| !Context.Store.Inventory.InventoryContains(Product)) return false;
			int stage = PurposeEffectProductReleaseStage(Product, ProductReceipt, Prefilter);
			return stage >= 0 && (AllowRelease || stage == 0)
				&& ExactPurposeEffectProductShape(Context, Product, Role, stage < 3);
		}

		private static int PurposeEffectProductCount(
			KingdomPurposeEffectProductCensus Census,
			KingdomPurposeEffectProductRole Role)
		{
			return Role == KingdomPurposeEffectProductRole.Refined ? Census.Recorded.Refined
				: Role == KingdomPurposeEffectProductRole.Seed ? Census.Recorded.Seed
					: Role == KingdomPurposeEffectProductRole.Staple
						? Census.Recorded.Staple : -1;
		}
	}
}
