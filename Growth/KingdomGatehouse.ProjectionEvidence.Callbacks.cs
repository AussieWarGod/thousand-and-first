using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		private static bool TryCreateStagedSatellite(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part,
			int Index, KingdomGatehouseCell Spec, string ExpectedId, out string Failure)
		{
			Failure = null; GameObject item;
			try { item = GameObject.Create(Spec.Blueprint); }
			catch (Exception ex)
			{
				return FailSlot("A gatehouse satellite blueprint threw before staging: "
					+ ex.Message, out Failure);
			}
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure)) return false;
			if (!GameObject.Validate(item))
				return FailSlot("A gatehouse satellite blueprint created no object.", out Failure);
			if (!TryApplySatellitePalette(item, Plan, Index))
				return FailSlot("A gatehouse satellite could not retain its frozen render palette.",
					out Failure);
			item.ID = ExpectedId;
			StampProjectionSatellite(item, Root, Plan, Index, Spec);
			if (!KingdomGatehouseProjectionRules.CanSerializeDeterministicCustody(
				ExactSatellitePalette(item, Plan, Index), item.IDIfAssigned == ExpectedId,
				ExactProjectionMarks(item, Root, Plan, Index, Spec, ExpectedId)))
				return FailSlot("A gatehouse satellite could not freeze its body before custody.",
					out Failure);
			if (!SetProjectionCustody(Part, Index, item))
				return FailSlot("A gatehouse satellite could not enter serialized custody.", out Failure);
			if (!TryProjectionEvidence(Root, Z, Plan, Part, Index, ExpectedId,
				out KingdomGatehouseSlotEvidence evidence, out GameObject exact,
				out Failure) || evidence != KingdomGatehouseSlotEvidence.Staged
				|| !ReferenceEquals(exact, item))
				return ContestSlot(Root, Index, Failure
					?? "A created gatehouse satellite did not retain exact staged custody.", out Failure);
			return true;
		}

		private static bool TryPlaceStagedSatellite(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part,
			int Index, KingdomGatehouseCell Spec, string ExpectedId, GameObject Item,
			out string Failure)
		{
			Failure = null; GameObject accepted = null; Exception callback = null;
			try { accepted = Z.GetCell(Spec.X, Spec.Y).AddObject(Item, NoStack: true); }
			catch (Exception ex) { callback = ex; }
			finally { KingdomSurvey.ObserveAddResultInActive(Z, Item, accepted); }
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure)) return false;
			if (!TryProjectionEvidence(Root, Z, Plan, Part, Index, ExpectedId,
				out KingdomGatehouseSlotEvidence evidence, out GameObject exact,
				out Failure)) return ContestSlot(Root, Index, Failure, out Failure);
			if (evidence == KingdomGatehouseSlotEvidence.ExactPlacement
				&& (callback != null || ReferenceEquals(accepted, Item))
				&& ReferenceEquals(exact, Item))
				return SettleProjectionSlot(Root, Part, Index, Item, out Failure);
			if (callback == null && !ReferenceEquals(accepted, Item))
				return ContestSlot(Root, Index,
					"Gatehouse AddObject returned a different physical identity.", out Failure);
			if (evidence == KingdomGatehouseSlotEvidence.Staged)
			{
				if (Plan.ReceiptVersion == 1)
					return FailSlot((callback == null
						? "Legacy gatehouse AddObject refused exact placement."
						: "Legacy gatehouse AddObject threw before exact placement: "
							+ callback.Message)
						+ " Exact serialized custody was retained for retry.", out Failure);
				if (!TryRetireStagedSatellite(Root, RootCell, Job, Scaffold, Z, Plan,
					PlanReceipt, Part, Index, ExpectedId, Item,
					out KingdomGatehouseSlotEvidence after, out Failure)) return false;
				return FailSlot((callback == null ? "Gatehouse AddObject refused exact placement."
					: "Gatehouse AddObject threw before exact placement: " + callback.Message)
					+ (after == KingdomGatehouseSlotEvidence.Staged
						? " Exact serialized custody was retained after cleanup veto." : ""),
					out Failure);
			}
			if (evidence == KingdomGatehouseSlotEvidence.Absent)
			{
				SetProjectionCustody(Part, Index, null);
				return FailSlot("Gatehouse AddObject left the published output exactly absent.",
					out Failure);
			}
			return ContestSlot(Root, Index,
				"Gatehouse AddObject left duplicate or foreign physical evidence.", out Failure);
		}

		private static bool TryRetireStagedSatellite(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part,
			int Index, string ExpectedId, GameObject Item,
			out KingdomGatehouseSlotEvidence Evidence, out string Failure)
		{
			Failure = null;
			try { Item.Obliterate(null, Silent: true); }
			catch (Exception) { }
			KingdomSurvey.ObserveCurrentTopologyInActive(Z, Item);
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure))
			{
				Evidence = KingdomGatehouseSlotEvidence.Foreign;
				return false;
			}
			TryProjectionEvidence(Root, Z, Plan, Part, Index, ExpectedId,
				out Evidence, out _, out _);
			if (KingdomGatehouseProjectionRules.CanClearCustody(
				KingdomGatehouseSlotState.Pending, true, Evidence))
				SetProjectionCustody(Part, Index, null);
			return true;
		}

		private static bool SettleProjectionSlot(GameObject Root,
			IPart Part,
			int Index, GameObject Item, out string Failure)
		{
			Failure = null;
			Root.SetIntProperty(SatelliteStateProperty(Index),
				(int)KingdomGatehouseSlotState.Settled);
			if (Root.GetIntProperty(SatelliteStateProperty(Index))
				!= (int)KingdomGatehouseSlotState.Settled)
				return FailSlot("A gatehouse satellite settled phase did not persist.", out Failure);
			GameObject custody = ProjectionCustody(Part, Index);
			if (custody != null && !ReferenceEquals(custody, Item))
				return ContestSlot(Root, Index,
					"A gatehouse satellite settled beside foreign callback custody.", out Failure);
			return SetProjectionCustody(Part, Index, null)
				|| FailSlot("A settled gatehouse custody reference did not clear.", out Failure);
		}

	}
}
