using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		private static bool TryReleaseFrozenCustody(KingdomPolityLedger Ledger, string RealmId,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			FrozenCustodyPlan Plan, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Plan.Roots.Count; i++)
				if (!TryProcessCustodyNode(Plan.Roots[i], OwnedContext: true, RealmId, Cohort,
					Receipt, Plan, out Failure)) return false;
			for (int i = 0; i < Plan.Roots.Count; i++)
				if (!TryVerifyReleasedCustody(Plan.Roots[i], OwnedContext: true, RealmId, Cohort,
					Receipt, Plan, out Failure)) return FailPhysical(Failure ??
						"released cohort custody changed, vanished, or reached a foreign holder", out Failure);
			if (!TryBuildCustodyPlan(Ledger, Plan.Cell.ParentZone, RealmId, Cohort, Receipt,
				Plan.Body, Plan.MemberOrdinal, AllowRemovedGear: true,
				out FrozenCustodyPlan cleared, out Failure)) return false;
			return CustodyIsClear(cleared) || FailPhysical(
				"foreign or marked cohort custody remains", out Failure);
		}

		private static bool TryProcessCustodyNode(FrozenCustodyNode Node, bool OwnedContext,
			string RealmId, KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, FrozenCustodyPlan Plan, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Node?.Object) || (!ReferenceEquals(Node.Object.InInventory,
				Node.Parent) && !ReferenceEquals(Node.Object.Equipped, Node.Parent)))
				return FailPhysical("cohort custody moved before its exact release", out Failure);
			bool childOwned = Node.Kind == FrozenCustodyKind.ExactGear ||
				Node.Kind == FrozenCustodyKind.Natural && OwnedContext;
			for (int i = 0; i < Node.Children.Count; i++)
				if (!TryProcessCustodyNode(Node.Children[i], childOwned, RealmId, Cohort,
					Receipt, Plan, out Failure)) return false;
			if (!GameObject.Validate(Node.Object) || (!ReferenceEquals(Node.Object.InInventory,
				Node.Parent) && !ReferenceEquals(Node.Object.Equipped, Node.Parent)))
				return FailPhysical("cohort custody moved during descendant release", out Failure);
			if (Node.Kind == FrozenCustodyKind.ExactGear)
				return TryRemoveExactGear(Node.Object, RealmId, Cohort, Receipt, Plan,
					Node.GearOrdinal, out Failure);
			if (KingdomPolityPhysicalCustodyRules.TransferCrossesOwnedBoundary(
				Node.Kind == FrozenCustodyKind.Foreign ?
					KingdomPolityCustodyDecision.TransferForeign :
					KingdomPolityCustodyDecision.PreserveNatural, OwnedContext))
				return TryMoveForeignObject(Node.Object, Plan.Cell, out Failure);
			return true;
		}

		private static bool TryVerifyReleasedCustody(FrozenCustodyNode Node, bool OwnedContext,
			string RealmId, KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, FrozenCustodyPlan Plan, out string Failure)
		{
			Failure = null;
			bool childOwned = Node.Kind == FrozenCustodyKind.ExactGear ||
				Node.Kind == FrozenCustodyKind.Natural && OwnedContext;
			for (int i = 0; i < Node.Children.Count; i++)
				if (!TryVerifyReleasedCustody(Node.Children[i], childOwned, RealmId, Cohort,
					Receipt, Plan, out Failure)) return false;
			if (Node.Kind == FrozenCustodyKind.ExactGear)
			{
				string id = GearObjectId(RealmId, Cohort, Receipt, Plan.Spec, Node.GearOrdinal);
				if (!TryFindResidentObject(id, out GameObject resident, out Failure)) return false;
				return !GameObject.Validate(Node.Object) && !GameObject.Validate(resident) &&
					HasRemovalWitness(Plan.Cell.ParentZone,
						KingdomPolityPhysicalCustodyRules.GearRemovalKind, RealmId, Cohort.CohortId,
						Receipt.ProjectionId, id, Node.GearOrdinal);
			}
			if (Node.Kind == FrozenCustodyKind.Foreign && OwnedContext)
				return GameObject.Validate(Node.Object) && Node.Object.CurrentCell == Plan.Cell &&
					Node.Object.InInventory == null && Node.Object.Equipped == null;
			return GameObject.Validate(Node.Object) &&
				(ReferenceEquals(Node.Object.InInventory, Node.Parent) ||
				 ReferenceEquals(Node.Object.Equipped, Node.Parent));
		}

		private static bool TryRemoveExactGear(GameObject Item, string RealmId,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			FrozenCustodyPlan Plan, int GearOrdinal, out string Failure)
		{
			Failure = null; string expected = GearObjectId(RealmId, Cohort, Receipt,
				Plan.Spec, GearOrdinal);
			if (!GameObject.Validate(Item) || Item.IDIfAssigned != expected ||
				!KingdomPolityNpcRuntime.ExactGear(Item, Plan.Spec, RealmId, Cohort.CohortId,
					Receipt.ProjectionId, KingdomPolityCohortRules.PreparedObjectId(Cohort,
						Plan.MemberOrdinal), GearOrdinal, ExactCustody: true))
				return FailPhysical("exact polity gear changed before removal", out Failure);
			if (!TryPrepareRemovalWitness(Plan.Cell,
				KingdomPolityPhysicalCustodyRules.GearRemovalKind, RealmId, Cohort.CohortId,
				Receipt.ProjectionId, expected, GearOrdinal, out Failure)) return false;
			try { Item.ForceUnequipAndRemove(Silent: true); }
			catch (Exception ex)
			{
				if (GameObject.Validate(Item)) return FailPhysical(
					"exact polity gear detach callback failed: " + ex.Message, out Failure);
			}
			if (GameObject.Validate(Item) && (Item.InInventory != null || Item.Equipped != null ||
				Item.CurrentCell != null)) return FailPhysical(
				"exact polity gear moved instead of entering removal custody", out Failure);
			if (GameObject.Validate(Item))
			{
				if (!KingdomPolityNpcRuntime.ExactGear(Item, Plan.Spec, RealmId,
					Cohort.CohortId, Receipt.ProjectionId,
					KingdomPolityCohortRules.PreparedObjectId(Cohort, Plan.MemberOrdinal),
					GearOrdinal, ExactCustody: true, RequireResidentIndex: false))
					return FailPhysical("exact polity gear changed during detach", out Failure);
				try { Item.Obliterate(null, Silent: true); }
				catch (Exception ex)
				{
					if (GameObject.Validate(Item)) return FailPhysical(
						"exact polity gear removal failed: " + ex.Message, out Failure);
				}
			}
			if (GameObject.Validate(Item)) return FailPhysical(
				"exact polity gear survived removal", out Failure);
			if (!TryFindResidentObject(expected, out GameObject resident, out Failure)) return false;
			if (GameObject.Validate(resident)) return FailPhysical(
				"exact polity gear id was replaced during removal", out Failure);
			return TryWriteRemovalWitness(Plan.Cell,
				KingdomPolityPhysicalCustodyRules.GearRemovalKind, RealmId, Cohort.CohortId,
				Receipt.ProjectionId, expected, GearOrdinal, out Failure);
		}

		private static bool TryMoveForeignObject(GameObject Item, Cell Cell, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item) || Cell == null)
				return FailPhysical("foreign cohort custody disappeared before release", out Failure);
			try { Item.ForceUnequipAndRemove(Silent: true); }
			catch (Exception ex)
			{
				if (Item.CurrentCell != Cell) return FailPhysical(
					"foreign cohort custody detach failed: " + ex.Message, out Failure);
			}
			if (!GameObject.Validate(Item)) return FailPhysical(
				"foreign cohort custody was destroyed during release", out Failure);
			if (Item.CurrentCell == Cell && Item.InInventory == null && Item.Equipped == null)
				return true;
			if (Item.CurrentCell != null || Item.InInventory != null || Item.Equipped != null)
				return FailPhysical("foreign cohort custody moved outside exact release ground", out Failure);
			GameObject accepted = null;
			try { accepted = Cell.AddObject(Item, Silent: true, NoStack: true); }
			catch (Exception ex)
			{
				if (Item.CurrentCell != Cell) return FailPhysical(
					"foreign cohort custody placement failed: " + ex.Message, out Failure);
			}
			return GameObject.Validate(Item) &&
				(accepted == null || ReferenceEquals(accepted, Item)) && Item.CurrentCell == Cell &&
				Item.InInventory == null && Item.Equipped == null || FailPhysical(
					"foreign cohort custody did not reach exact loaded ground", out Failure);
		}

		private static bool CustodyIsClear(FrozenCustodyPlan Plan)
		{
			return Plan != null && ClearNodes(Plan.Roots);
		}

		private static bool ClearNodes(System.Collections.Generic.List<FrozenCustodyNode> Nodes)
		{
			for (int i = 0; Nodes != null && i < Nodes.Count; i++)
			{
				if (Nodes[i].Kind != FrozenCustodyKind.Natural || !ClearNodes(Nodes[i].Children))
					return false;
			}
			return true;
		}

		private static bool TryRemoveExactBody(GameObject Body, Cell Cell, string RealmId,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			int Ordinal, out string Failure)
		{
			Failure = null; string objectId = KingdomPolityCohortRules.PreparedObjectId(Cohort,
				Ordinal);
			if (!TryFindResidentObject(objectId, out GameObject resident, out Failure)) return false;
			if (!GameObject.Validate(Body) && !GameObject.Validate(resident))
			{
				Zone recoveryZone = Cell?.ParentZone;
				if (!TryProveLocalObjectAbsence(recoveryZone, objectId, out Failure)) return false;
				KingdomPolityCleanupEvidenceProof intent = TryProveCleanupIntent(recoveryZone,
					RealmId, Cohort.CohortId, Receipt.ProjectionId, objectId, Ordinal,
					(byte)Cohort.Phase, (byte)Receipt.Phase, out Cell frozenCell,
					out string intentKey, out string intentValue, out Failure);
				if (intent != KingdomPolityCleanupEvidenceProof.Exact ||
					!ReferenceEquals(frozenCell, Cell)) return FailPhysical(Failure ??
						"absent cohort body lacks an exact prepared cleanup intent", out Failure);
				if (!TryWriteRemovalWitness(Cell, KingdomPolityPhysicalCustodyRules.CleanupRemovalKind,
					RealmId, Cohort.CohortId, Receipt.ProjectionId, objectId, Ordinal, out Failure))
					return false;
				if (!TryClearCleanupIntent(recoveryZone, Cell, RealmId, Cohort.CohortId,
					Receipt.ProjectionId, objectId, Ordinal, (byte)Cohort.Phase,
					(byte)Receipt.Phase, intentKey, intentValue, out Failure)) return false;
				return TryProveLocalObjectAbsence(recoveryZone, objectId, out Failure) &&
					TryProveRemovalWitness(recoveryZone,
						KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId,
						Cohort.CohortId, Receipt.ProjectionId, objectId, Ordinal,
						out string _, out Failure) == KingdomPolityCleanupEvidenceProof.Exact;
			}
			if (!GameObject.Validate(Body) || !ReferenceEquals(Body.CurrentCell, Cell) ||
				Body.InInventory != null || Body.Equipped != null || Body.IDIfAssigned != objectId ||
				!ReferenceEquals(resident, Body) ||
				Body.GetStringProperty(CohortOwnerProperty) != Cohort.PolityId ||
				Body.GetStringProperty(CohortProperty) != Cohort.CohortId ||
				Body.GetStringProperty(ProjectionProperty) != Receipt.ProjectionId ||
				Body.GetIntProperty(MemberOrdinalProperty, -1) != Ordinal ||
				Body.GetIntProperty(CohortXProperty, -1) != Cell.X ||
				Body.GetIntProperty(CohortYProperty, -1) != Cell.Y ||
				Body.GetPart<XRL.World.Parts.r_KingdomPolityCohortBody>()?.RealmId != RealmId)
				return FailPhysical("exact cohort body changed before removal", out Failure);
			if (!TryPrepareRemovalWitness(Cell,
				KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId, Cohort.CohortId,
				Receipt.ProjectionId, objectId, Ordinal, out Failure)) return false;
			if (!TryWriteCleanupIntent(Cell, RealmId, Cohort.CohortId, Receipt.ProjectionId,
				objectId, Ordinal, (byte)Cohort.Phase, (byte)Receipt.Phase,
				out string cleanupKey, out string cleanupValue, out Failure)) return false;
			XRL.World.Parts.r_KingdomPolityCohortBody part =
				Body.GetPart<XRL.World.Parts.r_KingdomPolityCohortBody>();
			if (part == null) return FailPhysical("cleanup body lacks its exact callback bridge", out Failure);
			part.ArmCleanup(Receipt.ProjectionId, objectId, Ordinal, Cell,
				(byte)Cohort.Phase, (byte)Receipt.Phase, cleanupKey, cleanupValue);
			try { Body.Obliterate(null, Silent: true); }
			catch (Exception ex)
			{
				part.ClearCleanup();
				if (GameObject.Validate(Body)) return FailPhysical(
					"exact cohort body removal failed: " + ex.Message, out Failure);
			}
			if (GameObject.Validate(Body)) { part.ClearCleanup(); return FailPhysical(
				"exact cohort body survived removal", out Failure); }
			if (!TryFindResidentObject(objectId, out resident, out Failure)) return false;
			if (GameObject.Validate(resident)) return FailPhysical(
				"exact cohort body id was replaced during removal", out Failure);
			if (!TryWriteRemovalWitness(Cell,
				KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId, Cohort.CohortId,
				Receipt.ProjectionId, objectId, Ordinal, out Failure)) return false;
			if (!TryClearCleanupIntent(Cell.ParentZone, Cell, RealmId, Cohort.CohortId,
				Receipt.ProjectionId, objectId, Ordinal, (byte)Cohort.Phase,
				(byte)Receipt.Phase, cleanupKey, cleanupValue, out Failure)) return false;
			return TryProveLocalObjectAbsence(Cell.ParentZone, objectId, out Failure);
		}

		private static bool TryPromotePreparedCleanupIntents(Zone Zone, string RealmId,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Cohort.ResolvedMembers.Count; i++)
			{
				string objectId = KingdomPolityCohortRules.PreparedObjectId(Cohort, i);
				if (!TryFindResidentObject(objectId, out GameObject resident, out Failure)) return false;
				if (GameObject.Validate(resident)) continue;
				if (!TryProveLocalObjectAbsence(Zone, objectId, out Failure)) return false;
				KingdomPolityCleanupEvidenceProof intent = TryProveCleanupIntent(Zone, RealmId,
					Cohort.CohortId, Receipt.ProjectionId, objectId, i, (byte)Cohort.Phase,
					(byte)Receipt.Phase, out Cell cell, out string _, out string _, out Failure);
				KingdomPolityCleanupEvidenceProof witness = TryProveRemovalWitness(Zone,
					KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId,
					Cohort.CohortId, Receipt.ProjectionId, objectId, i, out string _, out Failure);
				if (!KingdomPolityPhysicalCustodyRules.PreparedAbsenceCanRollback(intent, witness))
					return FailPhysical(Failure ??
						"prepared body absence lacks exact cleanup evidence", out Failure);
				if (intent == KingdomPolityCleanupEvidenceProof.Exact &&
					!TryRemoveExactBody(null, cell, RealmId, Cohort, Receipt, i, out Failure))
					return false;
				if (!TryFindResidentObject(objectId, out resident, out Failure) ||
					GameObject.Validate(resident) || !TryProveLocalObjectAbsence(Zone, objectId,
						out Failure) || TryProveRemovalWitness(Zone,
							KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId,
							Cohort.CohortId, Receipt.ProjectionId, objectId, i,
							out string _, out Failure) != KingdomPolityCleanupEvidenceProof.Exact)
					return FailPhysical(Failure ??
						"prepared body cleanup evidence changed during promotion", out Failure);
			}
			return true;
		}
	}
}
