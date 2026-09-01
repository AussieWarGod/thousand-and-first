using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		private static bool TryAdoptUnpublishedLegacyCustody(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part, int Index,
			KingdomGatehouseCell Spec, GameObject Staged, out string Failure)
		{
			Failure = null;
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure)) return false;
			string stagedId = GameObject.Validate(Staged) ? Staged.IDIfAssigned : null;
			bool blueprintExact = GameObject.Validate(Staged)
				&& Staged.Blueprint == Spec.Blueprint;
			bool unplaced = GameObject.Validate(Staged) && Staged.CurrentCell == null
				&& Staged.InInventory == null && Staged.Equipped == null;
			bool boundedId = KingdomGatehouseProjectionRules.ExactStoredSatelliteId(false,
				Root.IDIfAssigned, PlanReceipt, Index, stagedId);
			bool uniqueId = boundedId && LegacyUnpublishedIdentityUnique(Z, Part, Index,
				Staged, stagedId);
			bool compatibleMarks = CompatibleUnpublishedLegacyMarks(Staged, Root,
				Plan, Index, Spec);
			if (KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(Index,
				KingdomGatehouseSlotState.Empty, false,
				ReferenceEquals(ProjectionCustody(Part, Index), Staged), blueprintExact,
				unplaced, boundedId, uniqueId, compatibleMarks,
				KingdomGatehouseSlotEvidence.Foreign)
					!= KingdomGatehouseLegacyPublicationAction.AdoptCustody)
				return ContestSlot(Root, Index,
					"Unpublished legacy callback custody is foreign, landed, duplicated, or malformed.",
					out Failure);
			if (!TryApplySatellitePalette(Staged, Plan, Index))
				return ContestSlot(Root, Index,
					"Unpublished legacy callback custody rejected its frozen palette.", out Failure);
			StampProjectionSatellite(Staged, Root, Plan, Index, Spec);
			if (!ExactProjectionMarks(Staged, Root, Plan, Index, Spec, stagedId)
				|| !LegacyUnpublishedIdentityUnique(Z, Part, Index, Staged, stagedId))
				return ContestSlot(Root, Index,
					"Recovered legacy callback custody did not become one exact staged body.",
					out Failure);
			Root.SetStringProperty(SatelliteIdProperty(Index), stagedId);
			if (Root.GetStringProperty(SatelliteIdProperty(Index)) != stagedId)
				return FailSlot("Recovered legacy identity did not persist.", out Failure);
			Root.SetIntProperty(SatelliteStateProperty(Index),
				(int)KingdomGatehouseSlotState.Pending);
			return Root.GetIntProperty(SatelliteStateProperty(Index))
				== (int)KingdomGatehouseSlotState.Pending
				|| FailSlot("Recovered legacy phase did not persist.", out Failure);
		}

		private static bool LegacyUnpublishedIdentityUnique(Zone Z, IPart Part, int Index,
			GameObject Staged, string StagedId)
		{
			if (!GameObject.Validate(Staged) || string.IsNullOrEmpty(StagedId)
				|| !ReferenceEquals(ProjectionCustody(Part, Index), Staged)
				|| CountLoadedIdentity(Z, StagedId, out _) != 0) return false;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				if (i == Index) continue;
				GameObject other = ProjectionCustody(Part, i);
				if (ReferenceEquals(other, Staged)
					|| (GameObject.Validate(other) && other.IDIfAssigned == StagedId)) return false;
			}
			return true;
		}

		private static bool CompatibleUnpublishedLegacyMarks(GameObject Item,
			GameObject Root, KingdomGatehousePlan Plan, int Index, KingdomGatehouseCell Spec)
		{
			if (!GameObject.Validate(Item) || !GameObject.Validate(Root) || Plan == null
				|| Item.Blueprint != Spec.Blueprint
				|| !CompatibleIntMark(Item, SatelliteProperty, 1)
				|| !CompatibleStringMark(Item, OwnerProperty, Root.IDIfAssigned)
				|| !CompatibleIntMark(Item, IndexProperty, Index)
				|| !CompatibleStringMark(Item, SlotProperty, Spec.Slot)
				|| !CompatibleIntMark(Item, KingdomPlots.PlotPartProperty, 0)) return false;
			if (Index == 0)
				return CompatibleIntMark(Item, ReservationProperty, Schema)
					&& CompatibleIntMark(Item, KingdomPlots.PlotX1Property, Plan.X1)
					&& CompatibleIntMark(Item, KingdomPlots.PlotY1Property, Plan.Y1)
					&& CompatibleIntMark(Item, KingdomPlots.PlotX2Property, Plan.X2)
					&& CompatibleIntMark(Item, KingdomPlots.PlotY2Property, Plan.Y2);
			return !Item.HasIntProperty(ReservationProperty)
				&& !Item.HasStringProperty(ReservationProperty) && !HasPlotRectMark(Item);
		}

		private static bool CompatibleIntMark(GameObject Item, string Key, int Expected)
		{
			return !Item.HasStringProperty(Key)
				&& (!Item.HasIntProperty(Key) || Item.GetIntProperty(Key) == Expected);
		}

		private static bool CompatibleStringMark(GameObject Item, string Key, string Expected)
		{
			return !Item.HasIntProperty(Key)
				&& (!Item.HasStringProperty(Key)
					|| Item.GetStringProperty(Key) == Expected);
		}

		private static bool TryCreateLegacyStagedSatellite(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part, int Index,
			KingdomGatehouseCell Spec, out string Failure)
		{
			Failure = null; GameObject item;
			try { item = GameObject.Create(Spec.Blueprint); }
			catch (Exception ex)
			{
				return FailSlot("A legacy gatehouse satellite blueprint threw before staging: "
					+ ex.Message, out Failure);
			}
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure)) return false;
			if (!GameObject.Validate(item))
				return FailSlot("A legacy gatehouse satellite blueprint created no object.",
					out Failure);
			string generatedId = item.ID;
			if (!KingdomGatehouseProjectionRules.ExactStoredSatelliteId(false,
				Root.IDIfAssigned, PlanReceipt, Index, generatedId)
				|| !TryApplySatellitePalette(item, Plan, Index))
				return FailSlot("A legacy gatehouse satellite produced no bounded exact identity.",
					out Failure);
			if (!SetProjectionCustody(Part, Index, item))
				return FailSlot("A legacy gatehouse satellite could not enter serialized custody.",
					out Failure);
			StampProjectionSatellite(item, Root, Plan, Index, Spec);
			if (!ExactProjectionMarks(item, Root, Plan, Index, Spec, generatedId)
				|| !LegacyUnpublishedIdentityUnique(Z, Part, Index, item, generatedId))
				return ContestSlot(Root, Index,
					"A legacy gatehouse satellite could not prove one staged identity.", out Failure);
			Root.SetStringProperty(SatelliteIdProperty(Index), generatedId);
			if (Root.GetStringProperty(SatelliteIdProperty(Index)) != generatedId)
				return FailSlot("A legacy gatehouse satellite identity did not persist.", out Failure);
			Root.SetIntProperty(SatelliteStateProperty(Index),
				(int)KingdomGatehouseSlotState.Pending);
			if (Root.GetIntProperty(SatelliteStateProperty(Index))
				!= (int)KingdomGatehouseSlotState.Pending
				|| !TryProjectionEvidence(Root, Z, Plan, Part, Index, generatedId,
					out KingdomGatehouseSlotEvidence evidence, out GameObject exact,
					out Failure) || evidence != KingdomGatehouseSlotEvidence.Staged
				|| !ReferenceEquals(exact, item))
				return ContestSlot(Root, Index, Failure
					?? "A legacy gatehouse satellite did not retain exact staged custody.",
					out Failure);
			return true;
		}

	}
}
