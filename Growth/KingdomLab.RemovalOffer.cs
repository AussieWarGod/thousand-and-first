using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomLab
	{
		/// <summary>Offers to take a graft off. Costs less than the graft, returns nothing, and says
		/// so before the founder agrees.</summary>
		private static void OfferRemoval(GameObject Actor, KingdomSystem System, string Key, string City)
		{
			if (Actor == null || System == null || The.Game == null) return;
			string realmId = RealmIdentity(System);
			if (!KingdomIdentityRules.IsRealmId(realmId))
			{
				Popup.Show("The realm's immutable identity cannot be proved. No removal or charge was started.");
				return;
			}
			KingdomLabOwnershipSnapshot snapshot;
			KingdomLabOwnedTargetState target = KingdomProcedures.SnapshotOwned(Actor, Key,
				out snapshot);
			if (target == KingdomLabOwnedTargetState.Absent)
			{
				LabProcedure stale = new LabProcedure { Key = snapshot.ProcedureKey,
					Grants = snapshot.Grants, Source = (LabSource)snapshot.Source,
					Attach = (LabAttach)snapshot.Attach };
				KingdomProcedures.CleanupOwned(Actor, stale, snapshot);
				Popup.Show("The exact graft is absent. Its stale receipt was cleaned; no water was reserved or spent.");
				return;
			}
			if (target != KingdomLabOwnedTargetState.Present)
			{
				Popup.Show("The hall cannot prove which exact current or detached effect this record owns. Nothing was reserved, charged, or touched.");
				return;
			}
			LabProcedure procedure;
			if (!KingdomProcedures.TryGet(Key, out procedure))
			{
				Popup.Show("The immutable graft is known, but no current catalogue row can quote a removal price. The receipt is left untouched and nothing was charged.");
				return;
			}
			if (!KingdomProcedures.CatalogMatchesExecutionDetail(procedure, snapshot.Detail))
			{
				Popup.Show("The catalogue execution shape changed since this graft was made. It cannot redirect or price the frozen removal receipt.");
				return;
			}
			string currentDetail = snapshot.Detail;
			string currentManager = KingdomProcedures.ManagerFor(procedure.Key);
			string currentFingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, procedure.Key, procedure.Grants,
				(int)procedure.Source, (int)procedure.Attach, currentManager, currentDetail);
			if (!string.Equals(currentFingerprint, snapshot.Fingerprint,
				StringComparison.Ordinal))
			{
				Popup.Show("The catalogue row changed since this graft was made. It may describe the receipt, but cannot redirect or price its removal. Nothing was charged.");
				return;
			}
			if (ActiveRemovalJob(Actor) != null)
			{
				Popup.Show("A live removal receipt already follows you. Recover it before asking for another procedure.");
				return;
			}
			int priorReceiptCount = RemovalReceiptCount(Actor);
			if (priorReceiptCount >= KingdomLabRules.MaxEffectRows)
			{
				Popup.Show("The bounded patient removal archive is full. No new receipt, charge, or body action was started.");
				return;
			}
			int price = procedure.Cost / 4;
			if (Popup.ShowYesNoCancel("Have " + procedure.Named + " taken off?\n\n{{rules|--}} It costs {{C|"
				+ price + "}} drams and returns nothing. What was kept for it is spent and stays spent."
				+ (procedure.IsNamed ? "\n{{r|--}} It was a once-ever procedure. Taking it off does not give you the once back."
					: "")) != DialogResult.Yes)
			{
				return;
			}
			KingdomSurvey survey = (Actor.CurrentZone == null) ? null : KingdomSurvey.Take(Actor.CurrentZone, System);
			KingdomWaterDebit debit = null;
			if (price > 0 && (survey == null || !survey.TryReserveExactWater(price, out debit)))
			{
				Popup.Show("The stores cannot reserve exactly {{C|" + price + "}} drams. Nothing was taken off and no water was spent.");
				return;
			}
			r_KingdomLabRemovalJob job = new r_KingdomLabRemovalJob
			{
				RemovalId = Guid.NewGuid().ToString("N"),
				ProcedureKey = procedure.Key,
				OriginalJobId = snapshot.JobId,
				PatientId = Actor.ID,
				GameId = The.Game.GameID,
				RealmId = realmId,
				RealmFoundedTick = System.FoundedTick,
				BodyPartId = snapshot.BodyPartId,
				BearerId = snapshot.BearerId,
				City = City ?? "",
				ContractVersion = KingdomLabRules.EffectContractVersion,
				FrozenName = procedure.Named,
				FrozenGrants = snapshot.Grants,
				FrozenSource = snapshot.Source,
				FrozenAttach = snapshot.Attach,
				FrozenManager = snapshot.Manager,
				FrozenDetail = snapshot.Detail,
				FrozenFingerprint = snapshot.Fingerprint,
				EffectNonce = snapshot.EffectNonce,
				PartOrdinal = snapshot.PartOrdinal,
				WaterOwed = price,
				WaterPaid = 0,
				Phase = (int)KingdomLabRemovalPhase.Funding
			};
			job.ChronicleEventId = "lab:remove:" + job.RemovalId + ":chronicle";
			job.AnnounceEventId = "lab:remove:" + job.RemovalId + ":message";
			job.Normalize();
			if (job.SchemaQuarantined)
			{
				debit?.Rollback();
				Popup.Show(job.Fault);
				return;
			}
			try
			{
				Actor.AddPart(job);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: removal receipt publication threw (" + ex.Message + ")");
			}
			if (KingdomProcedures.ReferencePartOrdinal(Actor, job) < 0
				|| !ReferenceEquals(job.ParentObject, Actor)
				|| RemovalReceiptCount(Actor) != priorReceiptCount + 1)
			{
				debit?.Rollback();
				job.State = KingdomLabRemovalPhase.Cancelled;
				try
				{
					if (KingdomProcedures.ReferencePartOrdinal(Actor, job) >= 0)
						Actor.RemovePart(job);
				}
				catch { }
				Popup.Show("The patient-side removal receipt was absent or duplicated during publication. Nothing was spent or removed.");
				return;
			}
			KingdomLabOwnedTarget ignored;
			target = KingdomProcedures.ClassifyOwned(Actor, snapshot, out ignored);
			if (target != KingdomLabOwnedTargetState.Present)
			{
				debit?.Rollback();
				if (target == KingdomLabOwnedTargetState.Absent)
				{
					ArchiveCleanAbsentRemoval(Actor, job, procedure, snapshot);
					Popup.Show("The exact graft became absent while its receipt was attached. Nothing was charged and no governance action was committed.");
				}
				else
				{
					job.State = KingdomLabRemovalPhase.Quarantined;
					job.Fault = "The exact target became uncertain before payment. The patient receipt is quarantined and no water or effect was touched.";
					Popup.Show(job.Fault);
				}
				return;
			}
			if (price <= 0)
			{
				job.State = KingdomLabRemovalPhase.Paid;
			}
			else
			{
				debit.Commit();
				target = KingdomProcedures.ClassifyOwned(Actor, snapshot, out ignored);
				if (target != KingdomLabOwnedTargetState.Present)
				{
					bool compensated = debit.Rollback();
					MergeRemovalWater(job, debit);
					if (target == KingdomLabOwnedTargetState.Absent && compensated
						&& job.WaterPaid == 0 && job.WaterLost == 0 && !job.WaterQuarantined)
					{
						ArchiveCleanAbsentRemoval(Actor, job, procedure, snapshot);
						Popup.Show("The exact graft became absent during water callbacks. The debit was compensated exactly; no removal success or governance action was claimed.");
					}
					else
					{
						job.State = KingdomLabRemovalPhase.Quarantined;
						job.Fault = "The exact target changed during water callbacks. Compensation was measured; the receipt is quarantined and no replacement was touched.";
						EnsureRemovalGovernance(job);
						Popup.Show(job.Fault);
					}
					return;
				}
				MergeRemovalWater(job, debit);
				job.State = KingdomLabRules.RemovalFundingPhase(job.WaterOwed,
					job.WaterPaid, job.WaterQuarantined);
			}
			if (job.State == KingdomLabRemovalPhase.FundingRecovery
				&& job.WaterPaid == 0 && job.WaterLost == 0 && !job.WaterQuarantined)
			{
				DiscardCleanRemovalReceipt(Actor, job);
				Popup.Show("The exact water debit refused cleanly. Nothing was spent or removed, and the action remains free.");
				return;
			}
			EnsureRemovalGovernance(job);
			if (job.State == KingdomLabRemovalPhase.Paid)
			{
				AttemptRemoval(Actor, System, job, procedure);
			}
			else
			{
				Popup.Show(job.WaterQuarantined
					? "The persisted water receipt is uncertain and has been quarantined. No effect was touched."
					: "Part of the exact water price was measured. The persisted receipt will retry only its outstanding balance; no effect was touched.");
			}
		}

	}
}
