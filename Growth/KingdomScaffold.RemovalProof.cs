using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomScaffold
	{
		public const int ScaffoldRemovalIntentVersion = 1;
		public const string ScaffoldRemovalIntentIdProperty =
			"r_TAF_ScaffoldRemovalIntentId";
		public const string ScaffoldRemovalIntentSchemaProperty =
			"r_TAF_ScaffoldRemovalIntentSchema";

		/// <summary>Publishes payload first and schema last. An exact payload-only cut may
		/// finish publication; committed or opposite-typed malformed evidence never heals.</summary>
		private static bool TryPublishScaffoldRemovalIntent(GameObject Successor,
			string ScaffoldId)
		{
			if (!GameObject.Validate(Successor) || string.IsNullOrEmpty(ScaffoldId)
				|| Successor.HasStringProperty(ScaffoldRemovalIntentSchemaProperty)
				|| Successor.HasIntProperty(ScaffoldRemovalIntentIdProperty)) return false;
			bool hasSchema = Successor.HasIntProperty(ScaffoldRemovalIntentSchemaProperty);
			bool hasId = Successor.HasStringProperty(ScaffoldRemovalIntentIdProperty);
			if (hasSchema)
				return Successor.GetIntProperty(ScaffoldRemovalIntentSchemaProperty)
					== ScaffoldRemovalIntentVersion && hasId
					&& Successor.GetStringProperty(ScaffoldRemovalIntentIdProperty) == ScaffoldId;
			if (hasId && Successor.GetStringProperty(ScaffoldRemovalIntentIdProperty)
				!= ScaffoldId) return false;
			if (!hasId)
				Successor.SetStringProperty(ScaffoldRemovalIntentIdProperty, ScaffoldId);
			if (!Successor.HasStringProperty(ScaffoldRemovalIntentIdProperty)
				|| Successor.GetStringProperty(ScaffoldRemovalIntentIdProperty) != ScaffoldId)
				return false;
			Successor.SetIntProperty(ScaffoldRemovalIntentSchemaProperty,
				ScaffoldRemovalIntentVersion);
			return HasExactScaffoldRemovalIntent(Successor, ScaffoldId);
		}

		public static bool HasExactScaffoldRemovalIntent(GameObject Successor,
			string ScaffoldId)
		{
			return GameObject.Validate(Successor) && !string.IsNullOrEmpty(ScaffoldId)
				&& Successor.HasIntProperty(ScaffoldRemovalIntentSchemaProperty)
				&& !Successor.HasStringProperty(ScaffoldRemovalIntentSchemaProperty)
				&& Successor.GetIntProperty(ScaffoldRemovalIntentSchemaProperty)
					== ScaffoldRemovalIntentVersion
				&& Successor.HasStringProperty(ScaffoldRemovalIntentIdProperty)
				&& !Successor.HasIntProperty(ScaffoldRemovalIntentIdProperty)
				&& Successor.GetStringProperty(ScaffoldRemovalIntentIdProperty) == ScaffoldId;
		}

		/// <summary>Commits globally proved scaffold absence into the established generic
		/// predecessor proof. Separate intent fields remain immutable because an improvement
		/// later lawfully replaces the generic proof with its standing-work predecessor ID.</summary>
		public static bool TryCommitScaffoldRemovalProof(KingdomSystem System, Zone Z,
			GameObject Successor, GameObject ExpectedPredecessor, string Blueprint,
			string ScaffoldId, ref KingdomConstructionJob Job, out string Failure)
		{
			Failure = null;
			Cell cell = Z == null || Job == null ? null : Z.GetCell(Job.X, Job.Y);
			bool scaffoldRoute = Job != null
				&& (Job.Route == KingdomConstructionRoute.CommissionScaffold
					|| Job.Route == KingdomConstructionRoute.PlanScaffold)
				&& Job.SubjectId == ScaffoldId;
			bool improvement = Job != null && Job.Route == KingdomConstructionRoute.Improvement
				&& Job.SubjectId != ScaffoldId
				&& IsExactPendingImprovementSuccessor(Successor);
			if (cell == null || string.IsNullOrEmpty(Blueprint)
				|| (!scaffoldRoute && !improvement)
				|| (Job.Phase != KingdomConstructionPhase.ProjectionPending
					&& Job.Phase != KingdomConstructionPhase.Working)
				|| !KingdomConstruction.Owns(System, Z, Job)
				|| !KingdomConstruction.IsCurrent(Job)
				|| !HasExactScaffoldRemovalIntent(Successor, ScaffoldId)
				|| !IsExactSuccessor(Successor, Z, cell, Job, Blueprint)
				|| KingdomGatehouseRules.IsGatehouse(Job.TargetKey)
					&& !KingdomGatehouse.ProjectionComplete(Successor, Z)
				|| KingdomConstruction.FindExactId(Z, Job.OutputId, out GameObject exactSuccessor)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactSuccessor, Successor))
				return Fail("Scaffold-removal intent or successor identity changed.", out Failure);
			KingdomPhysicalLookupState state = KingdomConstruction.FindGlobalLiveId(
				ScaffoldId, out _);
			if (state != KingdomPhysicalLookupState.Absent
				|| GameObject.Validate(ExpectedPredecessor))
				return Fail("Scaffold absence is not globally exact.", out Failure);
			if (!ExactScaffoldReceiptClosure(Job, Successor, improvement, Z, cell))
				return Fail("A renamed, moved, or duplicate scaffold still carries the receipt.",
					out Failure);
			string proof = Successor.GetStringProperty(RemovalProofProperty);
			if (Successor.HasIntProperty(RemovalProofProperty)
				|| Successor.HasStringProperty(RemovalProofProperty) && proof != ScaffoldId)
				return Fail("Scaffold-removal proof carries foreign or opposite-typed evidence.",
					out Failure);
			if (!Successor.HasStringProperty(RemovalProofProperty))
				Successor.SetStringProperty(RemovalProofProperty, ScaffoldId);
			KingdomConstructionJob refreshed;
			if (!HasRemovalProof(Successor, ScaffoldId)
				|| !KingdomConstruction.TryFind(Job.Id, out refreshed)
				|| !SameFinalProjectionIdentity(Job, refreshed)
				|| !KingdomConstruction.Owns(System, Z, refreshed)
				|| !KingdomConstruction.IsCurrent(refreshed)
				|| KingdomConstruction.FindGlobalLiveId(ScaffoldId, out _)
					!= KingdomPhysicalLookupState.Absent
				|| !ExactScaffoldReceiptClosure(refreshed, Successor, improvement, Z, cell)
				|| !IsExactSuccessor(Successor, Z, cell, refreshed, Blueprint)
				|| !HasExactScaffoldRemovalIntent(Successor, ScaffoldId))
				return Fail("Scaffold-removal proof changed during registry reproof.", out Failure);
			Job = refreshed;
			return true;
		}

		private static bool ExactScaffoldReceiptClosure(KingdomConstructionJob Job,
			GameObject Successor, bool Improvement, Zone Z, Cell Cell)
		{
			GameObject allowedSubject = null;
			if (Improvement)
			{
				if (KingdomConstruction.FindGlobalLiveId(Job.SubjectId, out allowedSubject)
						!= KingdomPhysicalLookupState.Exact
					|| !GameObject.Validate(allowedSubject)
					|| allowedSubject.CurrentZone != Z || allowedSubject.CurrentCell != Cell
					|| allowedSubject.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
					|| !KingdomConstruction.HasReceipt(allowedSubject, Job)) return false;
				r_KingdomImprovement intent = allowedSubject.GetPart<r_KingdomImprovement>();
				if (intent == null || !intent.Working || intent.SuccessorKey != Job.TargetKey)
					return false;
			}
			return KingdomConstruction.FindGlobalLiveReceipt(Job.Id, Successor,
				allowedSubject, out _) == KingdomPhysicalLookupState.Absent;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}

		private static bool SameFinalProjectionIdentity(KingdomConstructionJob Expected,
			KingdomConstructionJob Observed)
		{
			return Expected != null && Observed != null && Expected.Id == Observed.Id
				&& Expected.OwnerKey == Observed.OwnerKey && Expected.ZoneId == Observed.ZoneId
				&& Expected.Route == Observed.Route && Expected.Phase == Observed.Phase
				&& Expected.Projection == Observed.Projection
				&& Expected.X == Observed.X && Expected.Y == Observed.Y
				&& Expected.SubjectId == Observed.SubjectId && Expected.SourceId == Observed.SourceId
				&& Expected.OutputId == Observed.OutputId && Expected.TargetKey == Observed.TargetKey
				&& Expected.Payload == Observed.Payload
				&& Expected.PhysicalPhase == Observed.PhysicalPhase
				&& Expected.PhysicalIndex == Observed.PhysicalIndex
				&& Expected.PhysicalAmount == Observed.PhysicalAmount
				&& Expected.PhysicalSpilled == Observed.PhysicalSpilled
				&& Expected.PhysicalItemId == Observed.PhysicalItemId
				&& Expected.PhysicalDestinationId == Observed.PhysicalDestinationId
				&& Expected.PhysicalReceipt == Observed.PhysicalReceipt
				&& Expected.BuildTruthSchema == Observed.BuildTruthSchema
				&& Expected.BuildHasPlot == Observed.BuildHasPlot
				&& Expected.BuildFrontier == Observed.BuildFrontier
				&& Expected.BuildDefence == Observed.BuildDefence;
		}
	}
}
