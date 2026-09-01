using System;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomScaffold
	{
		private void PrepareSuccessor(GameObject Successor, KingdomConstructionJob Job)
		{
			string displayName = TargetDisplayName ?? "structure";
			KingdomConstruction.Bind(Successor, Job);
			if (!KingdomConstruction.FreezePaidBuild(Successor, Job,
				Job.Route == KingdomConstructionRoute.Improvement ? ParentObject : null))
				throw new InvalidOperationException(
					"The exact paid construction receipt could not be frozen on its successor.");
			KingdomDesign.ApplyRenderOverrides(Successor,
				ParentObject.GetStringProperty(KingdomDesign.StagedColorStringProperty),
				ParentObject.GetStringProperty(KingdomDesign.StagedDetailColorProperty),
				ParentObject.GetStringProperty(KingdomDesign.StagedRenderStringProperty),
				ParentObject.GetStringProperty(KingdomDesign.StagedTileProperty));
			if (Job.TargetKey == KingdomGatehouseRules.BuildKey
				&& !KingdomGatehouse.TryApplyRootForm(Successor, Job.Payload))
				throw new InvalidOperationException(
					"The gatehouse successor could not retain its exact frozen v2 form.");
			if (Successor.GetPart<LiquidVolume>() != null)
			{
				Successor.SetIntProperty("KingdomStores", 1);
			}
			else if (TargetBlueprint == LarderBlueprint)
			{
				Successor.SetIntProperty("KingdomLarder", 1);
			}
			if (Job.Route == KingdomConstructionRoute.Improvement)
			{
				if (Successor.HasStringProperty(PendingImprovementSuccessorProperty)
					|| (Successor.HasIntProperty(PendingImprovementSuccessorProperty)
						&& Successor.GetIntProperty(PendingImprovementSuccessorProperty) != 1))
					throw new InvalidOperationException(
						"The improvement successor carries foreign pending-state evidence.");
				Successor.SetIntProperty(PendingImprovementSuccessorProperty, 1);
			}
			Successor.SetIntProperty("KingdomBuilt", 1);
			Successor.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Job.TargetKey);
			int defence = ParentObject.GetIntProperty("KingdomDefencePending");
			if (defence > 0) Successor.SetIntProperty("KingdomDefence", defence);
			if (ParentObject.GetIntProperty(KingdomPlots.FrontierWorkProperty) == 1)
				Successor.SetIntProperty(KingdomPlots.FrontierWorkProperty, 1);
			if (StaffNeeded > 0)
			{
				Successor.SetIntProperty("KingdomStaffNeeded", StaffNeeded);
				if (ThresholdManning) Successor.SetIntProperty("KingdomThresholdManning", 1);
				if (Successor.GetPart<Capacitor>() != null)
					Successor.SetIntProperty("KingdomHandCranked", 1);
			}
			Successor.SetStringProperty(CompletionNameProperty, displayName);
			Successor.SetStringProperty(CompletionTickProperty,
				CompleteTick.ToString(CultureInfo.InvariantCulture));
			string quote = ParentObject.GetStringProperty(KingdomCeremony.SurveyorsPlanProperty);
			if (!string.IsNullOrEmpty(quote)) Successor.SetStringProperty(CompletionPlanProperty, quote);
		}

		private static void QuarantineOrRetryAfterAdd(ref KingdomConstructionJob Job,
			GameObject Successor, Zone Z, string Failure)
		{
			// Gatehouse EnteredCell owns a six-output callback transaction. If any exact
			// identity was published, keep its landed root and paid predecessor for resume.
			if (KingdomGatehouse.HasProjectionCustody(Successor))
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, Successor);
				return;
			}
			bool removed = false;
			try
			{
				removed = !GameObject.Validate(Successor)
					|| (Successor.Obliterate(null, Silent: true) && !GameObject.Validate(Successor));
			}
			catch
			{
				removed = false;
			}
			KingdomSurvey.ObserveCurrentTopologyInActive(Z, Successor);
			if (removed)
			{
				KingdomSurvey.ObserveRemovedFromActive(Z, Successor);
				KingdomConstruction.Quarantine(ref Job,
					Failure + " The frozen successor identity was retired and cannot be replaced.");
			}
			else
				KingdomConstruction.Quarantine(ref Job, Failure);
		}

		private bool ExactPredecessor(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			Cell expected = Z == null || Job == null ? null : Z.GetCell(Job.X, Job.Y);
			if (!KingdomConstruction.Owns(System, Z, Job) || !GameObject.Validate(ParentObject)
				|| expected == null || ParentObject.CurrentZone != Z
				|| ParentObject.CurrentCell != expected
				|| !KingdomConstruction.IsCurrent(Job)
				|| !KingdomConstruction.HasReceipt(ParentObject, Job)
				|| ParentObject.GetPart<r_KingdomScaffold>() != this
				|| ParentObject.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Job.TargetKey)
				return false;
			if (Job.Route != KingdomConstructionRoute.Improvement)
			{
				return (Job.Route == KingdomConstructionRoute.CommissionScaffold
					|| Job.Route == KingdomConstructionRoute.PlanScaffold)
					&& ParentObject.IDIfAssigned == Job.SubjectId;
			}
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out work);
			r_KingdomImprovement intent = GameObject.Validate(work)
				? work.GetPart<r_KingdomImprovement>() : null;
			return workState == KingdomPhysicalLookupState.Exact
				&& intent != null && work.CurrentZone == Z && work.CurrentCell == expected
				&& KingdomConstruction.HasReceipt(work, Job)
				&& work.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
				&& (string.IsNullOrEmpty(Job.Payload)
					|| work.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == Job.Payload)
				&& intent.Working && intent.Scaffold == ParentObject
				&& intent.SuccessorKey == Job.TargetKey
				&& intent.SuccessorBlueprint == TargetBlueprint;
		}

		public static int FindExactSuccessors(Zone Z, KingdomConstructionJob Job,
			string Blueprint, GameObject Predecessor, out GameObject Successor)
		{
			Successor = null;
			if (Z == null || Job == null || string.IsNullOrEmpty(Blueprint)) return 0;
			Cell cell = Z.GetCell(Job.X, Job.Y);
			if (cell == null) return 0;
			int count = 0;
			bool conflict = false;
			foreach (GameObject item in cell.GetObjects())
			{
				if (item == Predecessor || !IsMarkedSuccessor(item, Z, cell, Job, Blueprint)) continue;
				if (item.IDIfAssigned != Job.OutputId)
				{
					conflict = true;
					continue;
				}
				if (Successor == null) Successor = item;
				count++;
			}
			if (conflict || count > 1) return 2;
			if (count == 1)
			{
				GameObject global;
				if (KingdomConstruction.FindExactId(Z, Job.OutputId, out global)
					!= KingdomPhysicalLookupState.Exact
					|| !ReferenceEquals(global, Successor)) return 2;
			}
			return count;
		}

		public static bool IsExactSuccessor(GameObject Successor, Zone Z, Cell Cell,
			KingdomConstructionJob Job, string Blueprint,
			GameObject ImprovementPredecessor = null)
		{
			if (!IsMarkedSuccessor(Successor, Z, Cell, Job, Blueprint)
				|| string.IsNullOrEmpty(Job.OutputId) || Successor.IDIfAssigned != Job.OutputId) return false;
			if (Job.Route != KingdomConstructionRoute.Improvement)
				return KingdomConstruction.PaidBuildMatches(Successor, Job);
			if (GameObject.Validate(ImprovementPredecessor))
				return KingdomConstruction.PaidBuildMatches(Successor, Job,
					ImprovementPredecessor);
			int schema = Successor.GetIntProperty(KingdomConstruction.PaidBuildSchemaProperty);
			return schema == 0 || (schema == KingdomConstruction.PaidBuildSchema
				&& KingdomConstruction.TryReadPaidBuild(Successor, out _));
		}

		private static bool IsMarkedSuccessor(GameObject Successor, Zone Z, Cell Cell,
			KingdomConstructionJob Job, string Blueprint)
		{
			if (Z == null || Job == null || Cell == null
				|| Cell != Z.GetCell(Job.X, Job.Y)
				|| !GameObject.Validate(Successor) || Successor.CurrentZone != Z
				|| Successor.CurrentCell != Cell || Successor.Blueprint != Blueprint
				|| Successor.GetIntProperty("KingdomBuilt") != 1
				|| Successor.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Job.TargetKey
				|| !KingdomConstruction.FinalBuildTruthMatches(Successor, Job)
				|| !KingdomConstruction.HasReceipt(Successor, Job)) return false;
			if (Job.Route != KingdomConstructionRoute.Improvement) return true;
			bool pending = IsExactPendingImprovementSuccessor(Successor);
			bool anyPending = HasPendingImprovementSuccessorEvidence(Successor);
			if (!ImprovementRemovalCommitted(Successor, Job)) return pending;
			// The recovery cut commits global predecessor absence before retiring the marker.
			// Both sides of that one write are exact; malformed evidence is never accepted.
			return pending || !anyPending;
		}

		public static bool HasRemovalProof(GameObject Successor, string PredecessorId)
		{
			return GameObject.Validate(Successor) && !string.IsNullOrEmpty(PredecessorId)
				&& Successor.HasStringProperty(RemovalProofProperty)
				&& !Successor.HasIntProperty(RemovalProofProperty)
				&& Successor.GetStringProperty(RemovalProofProperty) == PredecessorId;
		}

		/// <summary>Any marker evidence is nonfunctional; malformed evidence never grants benefits.</summary>
		public static bool HasPendingImprovementSuccessorEvidence(GameObject Successor)
		{
			return Successor != null
				&& (Successor.HasIntProperty(PendingImprovementSuccessorProperty)
					|| Successor.HasStringProperty(PendingImprovementSuccessorProperty));
		}

		/// <summary>Semantic pending authority survives a callback deleting the convenience marker.
		/// The construction registry and exact final-removal receipt remain the independent owner.</summary>
		public static bool HasPendingImprovementSuccessorAuthority(GameObject Successor)
		{
			if (HasPendingImprovementSuccessorEvidence(Successor)) return true;
			if (!GameObject.Validate(Successor)) return false;
			if (Successor.HasIntProperty(KingdomConstruction.ReceiptProperty)) return true;
			string receipt = Successor.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob job;
			return !string.IsNullOrEmpty(receipt)
				&& KingdomConstruction.TryFind(receipt, out job)
				&& job.Route == KingdomConstructionRoute.Improvement
				&& !string.IsNullOrEmpty(job.OutputId)
				&& job.OutputId == Successor.IDIfAssigned
				&& !ImprovementRemovalCommitted(Successor, job);
		}

		/// <summary>The registry and successor independently prove the predecessor-removal cut.</summary>
		public static bool HasCommittedImprovementRemoval(GameObject Successor)
		{
			if (!GameObject.Validate(Successor)
				|| Successor.HasIntProperty(KingdomConstruction.ReceiptProperty)) return false;
			string receipt = Successor.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob job;
			return !string.IsNullOrEmpty(receipt)
				&& KingdomConstruction.TryFind(receipt, out job)
				&& job.Route == KingdomConstructionRoute.Improvement
				&& job.OutputId == Successor.IDIfAssigned
				&& ImprovementRemovalCommitted(Successor, job);
		}

		private static bool ImprovementRemovalCommitted(GameObject Successor,
			KingdomConstructionJob Job)
		{
			if (!HasRemovalProof(Successor, Job?.SubjectId) || Job == null) return false;
			return Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved
				|| Job.PhysicalPhase == KingdomPhysicalPhase.EffectsPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
				|| Job.Phase == KingdomConstructionPhase.Complete;
		}

		/// <summary>Exact resumable pending state used by the paid handover transaction.</summary>
		public static bool IsExactPendingImprovementSuccessor(GameObject Successor)
		{
			return GameObject.Validate(Successor)
				&& Successor.HasIntProperty(PendingImprovementSuccessorProperty)
				&& !Successor.HasStringProperty(PendingImprovementSuccessorProperty)
				&& Successor.GetIntProperty(PendingImprovementSuccessorProperty) == 1;
		}

	}
}
