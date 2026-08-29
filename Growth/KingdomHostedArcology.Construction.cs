using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomHostedArcology
	{
		internal static bool BeginLot(KingdomSystem System, Zone Z, r_KingdomArcology Root,
			KingdomHostedLotDefinition Lot)
		{
			GameObject shell = Root?.ParentObject;
			KingdomRules.BuildEntry entry;
			string receiptFailure = null;
			KingdomHostedLotReceipt prior = null;
			if (System == null || Z == null || !GameObject.Validate(shell) || shell.CurrentZone != Z
				|| Lot == null || Lot.ReadOnly || !Operational(shell)
				|| !TryReceipt(Root, Lot.Key, out prior, out receiptFailure) || prior != null
				|| !KingdomData.TryGetBuilding(Lot.MaterialKey, out entry)
				|| KingdomConstruction.HasActiveSubject(System, Z,
					KingdomConstructionRoute.HostedArcology, shell))
			{
				Popup.Show(receiptFailure ?? "The hosted lot cannot be commissioned from this shell.");
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z, System);
			KingdomWaterDebit water = survey.ReserveExactWater(entry.CostDrams);
			KingdomMaterialDebitCost cost = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(Lot.MaterialKey), KingdomMaterials.BitCostFor(Lot.MaterialKey),
				KingdomMaterials.ExoticCostFor(Lot.MaterialKey));
			KingdomMaterialDebit materials = KingdomMaterials.ReserveComposite(Z, cost);
			long now = The.Game.TimeTicks;
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.HostedArcology, shell.CurrentCell, shell, Lot.Key, "",
				entry.CostDrams, cost, now, now + Lot.BuildTicks);
			KingdomHostedLotReceipt receipt = new KingdomHostedLotReceipt {
				Phase = KingdomHostedLotPhase.Working, LotKey = Lot.Key, JobId = job.Id,
				RootId = shell.ID, Supports = Lot.Supports, Remaining = (int)Lot.BuildTicks,
				LastTick = now, StaffingBasis = 0, RequiresWater = Lot.RequiresWater
			};
			job.Payload = KingdomHostedArcologyReceiptCodec.EncodeLot(receipt);
			KingdomConstructionStartResult funded = KingdomConstruction.TryFundNew(job, water,
				materials, out job, out string failure);
			if (funded == KingdomConstructionStartResult.Refused)
			{
				Popup.Show(failure ?? "The stores cannot meet the hosted-lot price."); return false;
			}
			if (funded != KingdomConstructionStartResult.Funded)
			{
				Popup.Show("The hosted-lot payment has an outstanding exact receipt; it was not retried.");
				return true;
			}
			return ProjectLot(System, Z, Root, ref job);
		}

		internal static void RetryConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (Job == null || Job.Route != KingdomConstructionRoute.HostedArcology) return;
			r_KingdomArcology root;
			if (!TryExactRoot(Z, Job, out root, out string failure))
			{
				KingdomConstructionJob bad = Job; KingdomConstruction.Quarantine(ref bad, failure); return;
			}
			KingdomConstructionJob job = Job;
			if (job.Phase == KingdomConstructionPhase.Funded) ProjectLot(System, Z, root, ref job);
			else InspectConstruction(System, Z, job);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (Job == null || Job.Route != KingdomConstructionRoute.HostedArcology) return;
			r_KingdomArcology root;
			if (!TryExactRoot(Z, Job, out root, out string failure))
			{
				KingdomConstructionJob bad = Job; KingdomConstruction.Quarantine(ref bad, failure); return;
			}
			KingdomHostedLotReceipt receipt;
			if (!TryReceipt(root, Job.TargetKey, out receipt, out failure))
			{
				QuarantineJob(root, Job, failure); return;
			}
			if (Job.Phase == KingdomConstructionPhase.ProjectionPending)
			{
				ResumeProjection(System, Z, root, receipt, ref Job);
				return;
			}
			if (Job.Phase == KingdomConstructionPhase.Complete)
			{
				SettleLot(System, Z, root, ref Job); return;
			}
			if (Job.Phase != KingdomConstructionPhase.Working || receipt == null
				|| receipt.Phase != KingdomHostedLotPhase.Working
				|| Job.PhysicalReceipt != KingdomHostedArcologyReceiptCodec.EncodeLot(receipt)
				|| Job.PhysicalAmount != receipt.Remaining || receipt.JobId != Job.Id
				|| receipt.RootId != root.ParentObject.IDIfAssigned)
			{
				QuarantineJob(root, Job, "Hosted labour no longer matches its exact receipt."); return;
			}
			AdvanceLot(System, Z, root, receipt, ref Job);
		}

		private static bool ResumeProjection(KingdomSystem System, Zone Z,
			r_KingdomArcology Root, KingdomHostedLotReceipt Receipt,
			ref KingdomConstructionJob Job)
		{
			if (Receipt == null || !KingdomConstruction.HasReceipt(Root.ParentObject, Job))
			{
				QuarantineJob(Root, Job,
					"Hosted-lot projection was interrupted without exact callback proof.");
				return false;
			}
			string encoded = KingdomHostedArcologyReceiptCodec.EncodeLot(Receipt);
			if (encoded == Job.Payload)
			{
				return KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.EffectsPending, 0, Receipt.Remaining, 0,
					Root.ParentObject.IDIfAssigned, null, encoded)
					&& KingdomConstruction.FinishProjection(ref Job, true, true);
			}
			if (Receipt.Remaining != 0 || Receipt.JobId != Job.Id
				|| Receipt.RootId != Root.ParentObject.IDIfAssigned
				|| Job.PhysicalPhase != KingdomPhysicalPhase.EffectsPending
				|| Job.PhysicalAmount != 0) return QuarantineProjection(Root, Job);
			KingdomHostedLotReceipt physical;
			if (!KingdomHostedArcologyReceiptCodec.TryDecodeLot(Job.PhysicalReceipt,
				out physical) || physical.Remaining != 0 || physical.JobId != Job.Id
				|| physical.RootId != Root.ParentObject.IDIfAssigned || physical.LotKey != Job.TargetKey
				|| (physical.Phase != KingdomHostedLotPhase.Working
					&& physical.Phase != KingdomHostedLotPhase.Active)
				|| (physical.Phase == KingdomHostedLotPhase.Active
					&& physical.StaffingBasis != 0)) return QuarantineProjection(Root, Job);
			string before = Job.PhysicalReceipt;
			physical.Phase = KingdomHostedLotPhase.Active;
			physical.StaffingBasis = 0;
			string active = KingdomHostedArcologyReceiptCodec.EncodeLot(physical);
			if (Receipt.Phase == KingdomHostedLotPhase.Working)
			{
				if (Job.PhysicalReceipt != encoded
					|| physical.JobId != Receipt.JobId) return QuarantineProjection(Root, Job);
				Receipt.Phase = KingdomHostedLotPhase.Active; Receipt.StaffingBasis = 0;
				if (!SetReceipt(Root, Receipt, out string failure))
				{
					QuarantineJob(Root, Job, failure); return false;
				}
			}
			else if (Receipt.Phase != KingdomHostedLotPhase.Active
				|| encoded != active) return QuarantineProjection(Root, Job);
			if (before == active)
				return KingdomConstruction.Complete(ref Job)
					&& SettleLot(System, Z, Root, ref Job);
			return KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.EffectsPending, 0, 0, 0,
				Root.ParentObject.IDIfAssigned, null, active)
				&& KingdomConstruction.Complete(ref Job)
				&& SettleLot(System, Z, Root, ref Job);
		}

		private static bool QuarantineProjection(r_KingdomArcology Root,
			KingdomConstructionJob Job)
		{
			QuarantineJob(Root, Job,
				"Hosted-lot completion lost its exact before/after receipt.");
			return false;
		}

		private static bool ProjectLot(KingdomSystem System, Zone Z, r_KingdomArcology Root,
			ref KingdomConstructionJob Job)
		{
			KingdomHostedLotReceipt receipt;
			if (!KingdomHostedArcologyReceiptCodec.TryDecodeLot(Job.Payload, out receipt)
				|| receipt.JobId != Job.Id || receipt.RootId != Root.ParentObject.IDIfAssigned
				|| receipt.LotKey != Job.TargetKey || !KingdomConstruction.BeginProjection(ref Job, out _))
				return false;
			KingdomHostedLotReceipt current; string failure;
			if (!TryReceipt(Root, receipt.LotKey, out current, out failure)
				|| (current != null && KingdomHostedArcologyReceiptCodec.EncodeLot(current) != Job.Payload))
			{
				QuarantineJob(Root, Job, failure ?? "Another hosted-lot receipt occupies this key."); return false;
			}
			string carried = Root.ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob prior;
			if (!string.IsNullOrEmpty(carried) && carried != Job.Id
				&& (!KingdomConstruction.TryFind(carried, out prior)
					|| !KingdomConstruction.CanSupersedeTerminalReceipt(
						System, Z, Root.ParentObject, prior)))
			{
				QuarantineJob(Root, Job,
					"The hosted shell carries a non-terminal or foreign construction receipt.");
				return false;
			}
			if (current == null && !SetReceipt(Root, receipt, out failure))
			{
				QuarantineJob(Root, Job, failure); return false;
			}
			KingdomConstruction.Bind(Root.ParentObject, Job);
			if (!KingdomConstruction.HasReceipt(Root.ParentObject, Job)
				|| !KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.EffectsPending,
					0, receipt.Remaining, 0, Root.ParentObject.IDIfAssigned, null, Job.Payload)) return false;
			return KingdomConstruction.FinishProjection(ref Job, true, true);
		}

		private static void AdvanceLot(KingdomSystem System, Zone Z, r_KingdomArcology Root,
			KingdomHostedLotReceipt Receipt, ref KingdomConstructionJob Job)
		{
			long nextTick; long now = The.Game.TimeTicks;
			bool operational = Operational(Root.ParentObject);
			int remaining = operational
				? KingdomHostedArcologyRules.AdvanceLaborAfterMasterEdge(Receipt.Remaining,
					Receipt.LastTick, System.MasterOptionTick, now, Receipt.StaffingBasis,
					out nextTick)
				: KingdomHostedArcologyRules.AdvanceLaborAfterMasterEdge(Receipt.Remaining,
					Receipt.LastTick, System.MasterOptionTick, now, 0, out nextTick);
			Receipt.Remaining = remaining; Receipt.LastTick = nextTick;
			Receipt.StaffingBasis = operational
				? KingdomWear.EffectivenessOf(Root.ParentObject) : 0;
			string encoded = KingdomHostedArcologyReceiptCodec.EncodeLot(Receipt);
			string failure = null;
			if (!KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.EffectsPending,
				0, remaining, 0, Root.ParentObject.IDIfAssigned, null, encoded)
				|| !SetReceipt(Root, Receipt, out failure))
			{
				QuarantineJob(Root, Job, failure ?? "Hosted labour did not persist exactly."); return;
			}
			if (remaining > 0) return;
			if (!KingdomConstruction.BeginProjection(ref Job, out _)) return;
			Receipt.Phase = KingdomHostedLotPhase.Active; Receipt.StaffingBasis = 0;
			encoded = KingdomHostedArcologyReceiptCodec.EncodeLot(Receipt);
			if (!SetReceipt(Root, Receipt, out failure)
				|| !KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.EffectsPending,
					0, 0, 0, Root.ParentObject.IDIfAssigned, null, encoded)
				|| !KingdomConstruction.Complete(ref Job)) return;
			SettleLot(System, Z, Root, ref Job);
		}

		private static bool SettleLot(KingdomSystem System, Zone Z, r_KingdomArcology Root,
			ref KingdomConstructionJob Job)
		{
			KingdomHostedLotReceipt receipt; string failure;
			KingdomHostedLotDefinition lot;
			if (!TryReceipt(Root, Job.TargetKey, out receipt, out failure) || receipt == null
				|| receipt.Phase != KingdomHostedLotPhase.Active
				|| Job.PhysicalReceipt != KingdomHostedArcologyReceiptCodec.EncodeLot(receipt)
				|| !KingdomHostedArcologyRules.TryHostedLot(Job.TargetKey, out lot)) return false;
			if (Job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled) return true;
			if (!KingdomCeremony.EnsureBuildingRaised(System, Root.ParentObject.CurrentCell,
				lot.DisplayName, Job.DueTick, "inside the hosted arcology", ref Job)) return false;
			return KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.EffectsSettled,
				0, 0, 0, Root.ParentObject.IDIfAssigned, null, Job.PhysicalReceipt);
		}

		private static bool TryExactRoot(Zone Z, KingdomConstructionJob Job,
			out r_KingdomArcology Root, out string Failure)
		{
			Root = null; Failure = null; GameObject shell;
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Z, Job.SubjectId, out shell);
			if (state != KingdomPhysicalLookupState.Exact || !GameObject.Validate(shell)
				|| shell.CurrentCell != Z.GetCell(Job.X, Job.Y)
				|| KingdomUpgrade.DesignKeyOf(shell) != ArcologyKey
				|| (Root = shell.GetPart<r_KingdomArcology>()) == null)
				return Fail("The exact hosted-shell root is absent or ambiguous.", out Failure);
			return true;
		}

		private static void QuarantineJob(r_KingdomArcology Root, KingdomConstructionJob Job,
			string Failure)
		{
			Quarantine(Root, Failure); KingdomConstructionJob bad = Job;
			KingdomConstruction.Quarantine(ref bad, Failure);
		}
	}
}
