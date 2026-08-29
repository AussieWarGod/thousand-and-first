using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryPurposeEffectDebitCensus(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeEffectCallbackKind Callback, out int Total,
			out GameObject First, out KingdomPurposeEffectRosterSnapshot Roster,
			out string Failure)
		{
			Total = 0;
			First = null;
			Roster = null;
			Failure = null;
			if (!KingdomConstructionInputLeaseAuthority.TryCapture(
				out KingdomConstructionInputLeaseSnapshot leases, out Failure)) return false;
			List<GameObject> items = new List<GameObject>(Context.Store.Inventory.Objects);
			items.Sort((a, b) => string.CompareOrdinal(a?.IDIfAssigned, b?.IDIfAssigned));
			for (int i = 0; i < items.Count; i++)
			{
				GameObject item = items[i];
				if (!DebitCandidatePhysical(Context, item, Callback)) continue;
				Total = Total > int.MaxValue - item.Count ? int.MaxValue : Total + item.Count;
				if (!DebitCandidate(Context, item, Callback, leases)) continue;
				if (string.IsNullOrEmpty(item.IDIfAssigned))
					return Fail("A bounded-effect debit candidate lacks exact identity.", out Failure);
				if (First == null) First = item;
			}
			if (First == null) return true;
			return TryCapturePurposeEffectRoster(Context, First.IDIfAssigned,
				KingdomPurposeEffectRosterMode.Exact, null, null, 0, out Roster, out Failure);
		}

		private static bool EnsurePurposeEffectDebitReservation(
			KingdomPurposeEffectRuntimeContext Context, KingdomPurposeEffectAttempt Attempt,
			string Witness, out string Failure)
		{
			Failure = null;
			if (Context == null || Attempt == null || string.IsNullOrEmpty(Witness)
				|| !OwnedStringField(Context.Work, PortfolioEffectAttemptProperty)
				|| Context.Work.GetStringProperty(PortfolioEffectAttemptProperty) != Witness)
				return Fail("The work no longer owns this debit attempt.", out Failure);
			if (OwnedFieldPresent(Context.Work, PortfolioEffectReadyProperty))
				return ExactPurposeEffectReady(Context.Work, Witness)
					|| Fail("The debit-ready checkpoint is foreign or torn.", out Failure);
			if (FindPortfolioObject(Attempt.ObjectId, out GameObject item, out bool graveyard)
				!= KingdomPhysicalLookupState.Exact || graveyard
				|| !ReferenceEquals(item.InInventory, Context.Store)
				|| item.CurrentCell != null || item.Count != Attempt.BeforeCount)
				return Fail("The unready debit target left its frozen before custody.", out Failure);
			bool reserved = ExactPurposeEffectDebitReservation(item, Witness);
			KingdomPurposeEffectRosterMode mode = reserved
				? KingdomPurposeEffectRosterMode.DebitReserved
				: KingdomPurposeEffectRosterMode.Exact;
			if (!TryCapturePurposeEffectRoster(Context, Attempt.ObjectId, mode,
				reserved ? Witness : null, null, 0,
				out KingdomPurposeEffectRosterSnapshot roster, out Failure)
				|| roster.Digest != Attempt.BeforeRosterDigest
				|| !DebitCandidateAtFrozenBefore(Context, item, Attempt.Callback,
					Witness, reserved, out Failure))
				return Fail(Failure ?? "The unready debit roster changed.", out Failure);
			if (!reserved && !StampPurposeEffectAttempt(item, Witness))
				return Fail("The exact input reservation could not persist.", out Failure);
			if (!TryCapturePurposeEffectRoster(Context, Attempt.ObjectId,
				KingdomPurposeEffectRosterMode.DebitReserved, Witness, null, 0,
				out roster, out Failure) || roster.Digest != Attempt.BeforeRosterDigest
				|| !DebitCandidateAtFrozenBefore(Context, item, Attempt.Callback,
					Witness, true, out Failure))
				return Fail(Failure ?? "The reserved debit roster is not its frozen before.",
					out Failure);
			return StampPurposeEffectReady(Context.Work, Witness)
				|| Fail("The completed debit reservation could not checkpoint.", out Failure);
		}

		private static bool ObservePurposeEffectDebit(
			KingdomPurposeEffectRuntimeContext Context, KingdomPurposeEffectAttempt Attempt,
			out bool Before, out bool After, out string Failure)
		{
			Before = false;
			After = false;
			Failure = null;
			string witness = KingdomPurposePortfolioRules.EncodeEffectAttempt(Attempt);
			if (string.IsNullOrEmpty(witness)
				|| !ExactPurposeEffectReady(Context.Work, witness))
				return Fail("No durable ready checkpoint proves prior input reservation.", out Failure);
			KingdomPhysicalLookupState state = FindPortfolioObject(Attempt.ObjectId,
				out GameObject item, out bool graveyard);
			if (state == KingdomPhysicalLookupState.Ambiguous) return false;
			bool reserved = state == KingdomPhysicalLookupState.Exact
				&& ExactPurposeEffectDebitReservation(item, witness);
			KingdomPurposeEffectRosterMode mode = reserved && !graveyard
				? KingdomPurposeEffectRosterMode.DebitReserved
				: KingdomPurposeEffectRosterMode.Exact;
			if (!TryCapturePurposeEffectRoster(Context, reserved && !graveyard
				? Attempt.ObjectId : null, mode, reserved && !graveyard ? witness : null,
				null, 0, out KingdomPurposeEffectRosterSnapshot roster, out Failure)) return false;
			bool direct = state == KingdomPhysicalLookupState.Exact && !graveyard
				&& ReferenceEquals(item.InInventory, Context.Store) && item.CurrentCell == null;
			Before = direct && reserved && item.Count == Attempt.BeforeCount
				&& roster.Digest == Attempt.BeforeRosterDigest
				&& DebitCandidateAtFrozenBefore(Context, item, Attempt.Callback,
					witness, true, out _);
			After = roster.Digest == Attempt.AfterRosterDigest && (Attempt.BeforeCount == 1
				? (state == KingdomPhysicalLookupState.Exact && graveyard && reserved)
					|| state == KingdomPhysicalLookupState.Absent
				: direct && reserved && item.Count == Attempt.BeforeCount - 1
					&& DebitCandidateAtFrozenAfter(Context, item, Attempt.Callback,
						witness, out _));
			return true;
		}

		private static bool DebitCandidateAtFrozenBefore(
			KingdomPurposeEffectRuntimeContext Context, GameObject Item,
			KingdomPurposeEffectCallbackKind Callback, string Witness, bool Reserved,
			out string Failure)
		{
			Failure = null;
			if (!Reserved)
			{
				if (!KingdomConstructionInputLeaseAuthority.TryCapture(
					out KingdomConstructionInputLeaseSnapshot leases, out Failure)) return false;
				return DebitCandidate(Context, Item, Callback, leases)
					|| Fail("The exact unreserved debit target lost its ordinary shape.", out Failure);
			}
			return DebitCandidateStillAvailable(Context, Item, Callback, Witness, out Failure);
		}

		private static bool DebitCandidateAtFrozenAfter(
			KingdomPurposeEffectRuntimeContext Context, GameObject Item,
			KingdomPurposeEffectCallbackKind Callback, string Witness, out string Failure)
		{
			return DebitCandidateStillAvailable(Context, Item, Callback, Witness, out Failure);
		}

		private static bool DebitCandidateStillAvailable(
			KingdomPurposeEffectRuntimeContext Context, GameObject Item,
			KingdomPurposeEffectCallbackKind Callback, string Witness, out string Failure)
		{
			Failure = null;
			if (!DebitCandidateShape(Context, Item, Callback)
				|| !ExactPurposeEffectDebitReservation(Item, Witness))
				return Fail("The exact reserved debit object changed shape.", out Failure);
			if (Callback == KingdomPurposeEffectCallbackKind.HarvestCrop)
				return KingdomOrdinaryFoodAuthority.TrySpendPurposeNow(Item, Witness, out Failure);
			if (!KingdomConstructionInputLeaseAuthority.TryCapture(
				out KingdomConstructionInputLeaseSnapshot leases, out Failure)) return false;
			return KingdomConstructionInputLeaseAuthority.CanUseMaterialForPurpose(
					leases, Item, Witness)
				&& KingdomMaterials.TryMaterialOf(Item, out KingdomMaterial material)
				&& material == Context.RawMaterial
				|| Fail("The exact reserved material is no longer callback-admissible.",
					out Failure);
		}

		private static bool DebitCandidate(KingdomPurposeEffectRuntimeContext Context,
			GameObject Item, KingdomPurposeEffectCallbackKind Callback,
			KingdomConstructionInputLeaseSnapshot Leases)
		{
			if (!DebitCandidateShape(Context, Item, Callback) || AnyPurposeEffectField(Item))
				return false;
			if (Callback == KingdomPurposeEffectCallbackKind.RefineRaw)
				return KingdomConstructionInputLeaseAuthority.CanUseMaterial(Leases, Item)
					&& KingdomMaterials.TryOrdinaryMaterialOf(Item,
						out KingdomMaterial material) && material == Context.RawMaterial;
			return Callback == KingdomPurposeEffectCallbackKind.HarvestCrop
				&& !AnyPurposeLandingField(Item)
				&& KingdomOrdinaryFoodAuthority.CanSpend(Leases, Item);
		}

		private static bool DebitCandidatePhysical(
			KingdomPurposeEffectRuntimeContext Context, GameObject Item,
			KingdomPurposeEffectCallbackKind Callback)
		{
			if (!DebitCandidateShape(Context, Item, Callback)) return false;
			if (Callback == KingdomPurposeEffectCallbackKind.RefineRaw)
				return KingdomMaterials.TryMaterialOf(Item,
					out KingdomMaterial material) && material == Context.RawMaterial;
			return Callback == KingdomPurposeEffectCallbackKind.HarvestCrop
				&& KingdomOrdinaryFoodAuthority.IsEdible(Item)
				&& !HasProtectedCargoEvidence(Item);
		}

		private static bool DebitCandidateShape(KingdomPurposeEffectRuntimeContext Context,
			GameObject Item, KingdomPurposeEffectCallbackKind Callback)
		{
			if (!GameObject.Validate(Item) || Item.Count < 1
				|| !ReferenceEquals(Item.InInventory, Context.Store)
				|| Item.CurrentCell != null) return false;
			return Callback != KingdomPurposeEffectCallbackKind.HarvestCrop
				|| Item.Blueprint == Context.CropBlueprint;
		}
	}
}
