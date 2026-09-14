using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private const string PendingRetirementPhaseProperty =
			"r_TAF_ArchitectureUpgradePendingRetirementPhase";

		/// <summary>A nonblocking floor may be placed beneath its own paid, still-standing predecessor.
		/// Exact job identities and the frozen phase-four upgrade receipt authorize this one main cell.</summary>
		private static bool ExactFloorUpgradePredecessor(GameObject Target, GameObject Predecessor,
			Zone Z, Cell Cell, string Lot, string Hash)
		{
			if (!GameObject.Validate(Target) || !GameObject.Validate(Predecessor)
				|| Target.CurrentCell != Cell || Predecessor.CurrentCell != Cell || Target.CurrentZone != Z
				|| Predecessor.CurrentZone != Z || Target.HasIntProperty(KingdomConstruction.ReceiptProperty)
				|| !r_KingdomScaffold.IsExactPendingImprovementSuccessor(Target)
				|| !KingdomConstruction.TryFind(Target.GetStringProperty(KingdomConstruction.ReceiptProperty), out var job)
				|| job.Route != KingdomConstructionRoute.Improvement || job.Phase != KingdomConstructionPhase.ProjectionPending
				|| job.OutputId != Target.IDIfAssigned || job.SubjectId != Predecessor.IDIfAssigned
				|| !KingdomConstruction.Owns(global::XRL.The.Game?.GetSystem<KingdomSystem>(), Z, job)
				|| !KingdomConstruction.HasReceipt(Target, job) || !KingdomConstruction.HasReceipt(Predecessor, job)
				|| !KingdomConstruction.IsCurrent(job)
				|| KingdomConstruction.FindExactId(Z, job.SubjectId, out var source) != KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(source, Predecessor)
				|| KingdomConstruction.FindExactId(Z, job.OutputId, out var output) != KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(output, Target)
				|| !KingdomArchitectureRuntime.TryRead(Target, out var after, out _) || after.SnapshotHash != Hash
				|| !ExactSuccessorOwner(Target, after, Lot, out _)
				|| Cell != Z.GetCell(after.MainWorldX, after.MainWorldY)
				|| !TryUpgradeBase(Predecessor, Z, after, out _, out _, out _, out var delta, out var lot, out _)
				|| lot != Lot || !TryReadUpgradeReceipt(Predecessor, Target, after, Lot, delta, out _)
				|| Predecessor.GetIntProperty(UpgradePhaseProperty) != 4) return false;
			return true;
		}

		/// <summary>Every successor-layout component is inert until the same durable handover
		/// commits predecessor absence. A bare carried flag is never activation authority.</summary>
		private static bool ExactPendingComponentState(GameObject Owner, GameObject Item,
			KingdomArchitectureIntent Intent)
		{
			bool successorReceipt = Owner != null && Intent != null
				&& Owner.HasIntProperty(UpgradeSchemaProperty)
				&& !Owner.HasStringProperty(UpgradeSchemaProperty)
				&& Owner.GetIntProperty(UpgradeSchemaProperty) == UpgradeSchema
				&& Owner.HasStringProperty(UpgradeHashProperty)
				&& !Owner.HasIntProperty(UpgradeHashProperty)
				&& Owner.GetStringProperty(UpgradeHashProperty) == Intent.SnapshotHash;
			if (successorReceipt
				|| r_KingdomScaffold.HasPendingImprovementSuccessorAuthority(Owner))
				return r_KingdomScaffold.IsExactPendingImprovementSuccessor(Item);
			return !r_KingdomScaffold.HasPendingImprovementSuccessorEvidence(Item);
		}

		private static void StampPendingComponentState(GameObject Owner, GameObject Item)
		{
			if (r_KingdomScaffold.HasPendingImprovementSuccessorAuthority(Owner))
				Item.SetIntProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty, 1);
		}

		/// <summary>Retires inert component state only after exact global predecessor absence.
		/// Components clear while the root still keeps the whole work inert; the root clears last.</summary>
		internal static bool TryRetirePendingUpgradeComponents(GameObject Owner, Zone Z,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Owner) || Z == null || Owner.CurrentZone != Z)
				return Fail("pending layout component retirement has no exact owner", out Failure);
			if (!r_KingdomScaffold.HasCommittedImprovementRemoval(Owner))
				return Fail("pending layout owner has not reached its exact removal boundary",
					out Failure);
			if (Owner.HasStringProperty(PendingRetirementPhaseProperty))
				return Fail("pending layout retirement phase has the wrong type", out Failure);
			int phase = Owner.GetIntProperty(PendingRetirementPhaseProperty);
			if (phase < 0 || phase > 2)
				return Fail("pending layout retirement phase is outside its range", out Failure);
			bool exactRootPending = r_KingdomScaffold
				.IsExactPendingImprovementSuccessor(Owner);
			if (phase < 2 && !exactRootPending
				|| phase == 2 && !exactRootPending
					&& r_KingdomScaffold.HasPendingImprovementSuccessorEvidence(Owner))
				return Fail("pending layout root state is absent or malformed", out Failure);
			if (!Owner.HasIntProperty(SchemaProperty)
				&& !Owner.HasStringProperty(SchemaProperty))
			{
				if (phase < 2 && !CommitPendingRetirement(Owner, 2, out Failure)) return false;
				return RetirePendingRoot(Owner, out Failure);
			}
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (!TryReadOwner(Owner, out intent, out snapshot, out lot, out Failure)
				|| Owner.GetIntProperty(NextLayerProperty) != 3)
				return Failure != null ? false : Fail("pending layout is not settled", out Failure);
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				string idProperty = OutputId(placement);
				string id = Owner.GetStringProperty(idProperty);
				GameObject item;
				if (Owner.HasIntProperty(idProperty) || string.IsNullOrEmpty(id)
					|| KingdomConstruction.FindExactId(Z, id, out item)
						!= KingdomPhysicalLookupState.Exact) return Fail(
						"pending layout component identity is absent or duplicated", out Failure);
				if (item.HasStringProperty(ComponentCarriedProperty)
					|| item.HasIntProperty(ComponentCarriedProperty)
						&& item.GetIntProperty(ComponentCarriedProperty) != 1
					|| (phase == 0
						? !r_KingdomScaffold.IsExactPendingImprovementSuccessor(item)
						: r_KingdomScaffold.HasPendingImprovementSuccessorEvidence(item)
							&& !r_KingdomScaffold.IsExactPendingImprovementSuccessor(item)))
					return Fail("added layout component lacks exact pending-state evidence",
						out Failure);
			}
			if (phase == 0)
			{
				if (!CommitPendingRetirement(Owner, 1, out Failure)) return false;
				phase = 1;
			}
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				GameObject item;
				KingdomConstruction.FindExactId(Z,
					Owner.GetStringProperty(OutputId(placement)), out item);
				if (r_KingdomScaffold.IsExactPendingImprovementSuccessor(item))
					item.RemoveIntProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty);
				if (r_KingdomScaffold.HasPendingImprovementSuccessorEvidence(item))
					return Fail("added layout component pending state could not retire", out Failure);
				KingdomSurvey.ObserveChangedInActive(Z, item);
			}
			if (phase < 2 && !CommitPendingRetirement(Owner, 2, out Failure)) return false;
			if (!RetirePendingRoot(Owner, out Failure)) return false;
			return TryVerifyComplete(Owner, Z, out Failure);
		}

		private static bool CommitPendingRetirement(GameObject Owner, int Phase,
			out string Failure)
		{
			Failure = null;
			Owner.SetIntProperty(PendingRetirementPhaseProperty, Phase);
			return !Owner.HasStringProperty(PendingRetirementPhaseProperty)
				&& Owner.GetIntProperty(PendingRetirementPhaseProperty) == Phase
				|| Fail("pending layout retirement receipt did not persist", out Failure);
		}

		private static bool RetirePendingRoot(GameObject Owner, out string Failure)
		{
			Failure = null;
			if (r_KingdomScaffold.IsExactPendingImprovementSuccessor(Owner))
				Owner.RemoveIntProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty);
			return !r_KingdomScaffold.HasPendingImprovementSuccessorEvidence(Owner)
				|| Fail("improvement successor pending state could not retire", out Failure);
		}
	}
}
