using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		internal static bool TryRequiredFundingObjectIds(KingdomConstructionJob Job,
			out List<string> RequiredObjectIds, out string Failure)
		{
			RequiredObjectIds = new List<string>();
			Failure = null;
			if (!RequiresExactFunding(Job)) return true;
			if (!HasFrozenCommitment(Job)
				|| !KingdomPurposeRules.TryDecodeCommitment(Job.PhysicalReceipt,
					out KingdomPurposeCommitment commitment)
				|| Job.Claims == null || !KingdomMaterialDebitCost.TryParseClaim(
					Job.Claims.MaterialOutstanding, out KingdomMaterialDebitCost outstanding))
				return Fail("The frozen city-purpose funding receipt cannot be decoded for retry.",
					out Failure);
			KingdomMaterialTally required = new KingdomMaterialTally();
			if (!string.IsNullOrEmpty(commitment.CargoItemId))
			{
				if (!KingdomPurposeRules.TryDecodeManifest(commitment.Manifest,
					out KingdomPurposeManifest manifest))
					return Fail("The legacy purpose cargo receipt is malformed.", out Failure);
				RequiredObjectIds.Add(commitment.CargoItemId);
				required.Add(manifest.CargoMaterial, 1);
			}
			if (!string.IsNullOrEmpty(commitment.ReciprocalCargoItemId))
			{
				if (!KingdomPurposePortfolioRules.TryDecodeCargo(
					commitment.ReciprocalCargoReceipt, out KingdomPurposeCargoReceipt cargo))
					return Fail("The reciprocal purpose cargo receipt is malformed.", out Failure);
				RequiredObjectIds.Add(commitment.ReciprocalCargoItemId);
				required.Add(cargo.EmbodiedMaterial, cargo.EmbodiedUnits);
			}
			if (RequiredObjectIds.Count < 1
				&& commitment.InitialBuildKey != Job.TargetKey) return Fail(
				"The purpose commitment names neither an initial shell nor exact funding object.",
				out Failure);
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				if (outstanding.Materials.Get((KingdomMaterial)i)
					< required.Get((KingdomMaterial)i))
					return Fail("The purpose cargo identities and outstanding typed claim disagree. Inspect the receipt; no same-kind object may substitute.",
						out Failure);
			return true;
		}

		internal static bool RequiredFundingObjectsMatch(KingdomConstructionJob Job,
			IList<string> RequiredObjectIds)
		{
			if (!RequiresExactFunding(Job)) return RequiredObjectIds == null
				|| RequiredObjectIds.Count == 0;
			if (!TryRequiredFundingObjectIds(Job, out List<string> expected, out _)
				|| RequiredObjectIds == null || expected.Count != RequiredObjectIds.Count)
				return false;
			for (int i = 0; i < expected.Count; i++)
				if (expected[i] != RequiredObjectIds[i]) return false;
			return true;
		}

		internal static bool RequiredFundingObjectMatches(KingdomConstructionJob Job,
			string RequiredObjectId)
		{
			if (!RequiresExactFunding(Job)) return string.IsNullOrEmpty(RequiredObjectId);
			return TryRequiredFundingObjectIds(Job, out List<string> expected, out _)
				&& (expected.Count == 0 ? string.IsNullOrEmpty(RequiredObjectId)
					: expected.Count == 1 && expected[0] == RequiredObjectId);
		}

		internal static bool ExactRequiredFundingItem(KingdomConstructionJob Job,
			GameObject Item)
		{
			if (!RequiresExactFunding(Job)) return false;
			if (!GameObject.Validate(Item)
				|| !TryRequiredFundingObjectIds(Job, out List<string> expected, out _)
				|| !expected.Contains(Item.IDIfAssigned)
				|| !KingdomPurposeRules.TryDecodeCommitment(Job.PhysicalReceipt,
					out KingdomPurposeCommitment commitment)) return false;
			if (Item.IDIfAssigned == commitment.ReciprocalCargoItemId)
				return ExactPortfolioCargoIdentity(Item, commitment.ReciprocalCargoReceipt);
			if (Item.IDIfAssigned != commitment.CargoItemId
				|| !KingdomPurposeRules.TryDecodeManifest(commitment.Manifest, out var manifest)
				|| !KingdomConstruction.TryFind(commitment.ConsignmentId, out var consignment)
				|| consignment.Route != KingdomConstructionRoute.PurposeConsignment
				|| consignment.OutputId != commitment.CargoItemId
				|| !SettledConsignment(consignment, commitment.Manifest,
					commitment.CargoItemId)) return false;
			return ExactCargo(Item, consignment, manifest);
		}

		/// <summary>Admits one protected token only through the complete required-object
		/// vector frozen by this exact purpose commitment. The id must occur exactly once.</summary>
		internal static bool ExactProtectedFundingAuthorization(KingdomConstructionJob Job,
			IList<string> RequiredObjectIds, GameObject Item)
		{
			if (!RequiresExactFunding(Job) || !GameObject.Validate(Item)
				|| !RequiredFundingObjectsMatch(Job, RequiredObjectIds)) return false;
			string id = Item.IDIfAssigned;
			if (string.IsNullOrEmpty(id)) return false;
			int matches = 0;
			for (int i = 0; i < RequiredObjectIds.Count; i++)
				if (RequiredObjectIds[i] == id) matches++;
			return matches == 1 && ExactRequiredFundingItem(Job, Item);
		}

		internal static bool TryRequiredFundingItems(Zone Z, KingdomConstructionJob Job,
			out List<GameObject> RequiredItems, out string Failure)
		{
			RequiredItems = new List<GameObject>();
			Failure = null;
			if (!TryRequiredFundingObjectIds(Job, out List<string> ids, out Failure)) return false;
			if (!KingdomPurposeRules.TryDecodeCommitment(Job.PhysicalReceipt,
				out KingdomPurposeCommitment commitment)) return false;
			if (!string.IsNullOrEmpty(commitment.CargoItemId))
			{
				if (!ResolveCommitCargo(Z, Job.TargetKey, Job.PhysicalReceipt,
					out GameObject legacy, out Failure)) return false;
				RequiredItems.Add(legacy);
			}
			if (!string.IsNullOrEmpty(commitment.ReciprocalCargoItemId))
			{
				if (!ResolveCommitReciprocalCargo(Z, Job.TargetKey, Job.PhysicalReceipt,
					out GameObject reciprocal, out Failure)) return false;
				RequiredItems.Add(reciprocal);
			}
			if (RequiredItems.Count != ids.Count) return false;
			for (int i = 0; i < ids.Count; i++)
				if (!GameObject.Validate(RequiredItems[i]) || RequiredItems[i].IDIfAssigned != ids[i]
					|| !ExactRequiredFundingItem(Job, RequiredItems[i])) return false;
			return true;
		}

		internal static bool TryRequiredFundingItem(Zone Z, KingdomConstructionJob Job,
			out GameObject RequiredItem, out string Failure)
		{
			RequiredItem = null;
			if (!TryRequiredFundingItems(Z, Job, out List<GameObject> items, out Failure)
				|| items.Count != 1) return false;
			RequiredItem = items[0];
			return true;
		}
	}
}
