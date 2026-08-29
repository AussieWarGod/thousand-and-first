using System; using System.Collections.Generic; using XRL; using XRL.Messages; using XRL.UI; using XRL.World; using XRL.World.Parts;
namespace ThousandAndFirst {
	internal static partial class KingdomLab {
		/// <summary>
		/// Takes the drams, spends the kept parts, pays the standing, and performs the work.
		/// <para>
		/// <b>The whole verdict is asked AGAIN here</b>, and not because the slate might be wrong:
		/// because the founder may have walked away, come back a season later, and had the answer
		/// change under them. A commit that trusts the screen that opened it is a commit that will
		/// one day take a founder's water for a thing it cannot do.
		/// </para>
		/// </summary>
		private static void Commission(GameObject Building, GameObject Actor, KingdomSystem System, LabProcedure Procedure, List<LabSlot> Anatomy, int At, List<GameObject> Kept, string City)
		{
			if (Building == null || Building.GetPart<r_KingdomLabJob>() != null
				|| ActiveRemovalJob(Actor) != null)
			{
				Popup.Show("This hall already owns a commission. Inspect its slate first.");
				return;
			}
			string realmId = RealmIdentity(System);
			if (!KingdomIdentityRules.IsRealmId(realmId))
			{
				Popup.Show("The realm's immutable identity cannot be proved. No commission or charge was started.");
				return;
			}
			List<int> categories = KingdomProcedures.Categories(Procedure);
			LabVerdict verdict = KingdomProcedureRules.JudgeSlot(Procedure, Anatomy[At], categories);
			if (verdict != LabVerdict.Allowed)
			{
				Popup.Show(KingdomProcedureRules.RefusalLine(verdict, Procedure));
				return;
			}
			GameObject source = FirstSourceFor(Kept, Procedure);
			if (source == null || CountFor(Kept, Procedure) < Procedure.Preserved)
			{
				Popup.Show(KingdomProcedureRules.RefusalLine(LabVerdict.RefusedUnkept, Procedure));
				return;
			}
			string stamp = source.GetStringProperty(KingdomProcedures.StampProperty);
			KeptSpendPreparation keptSpend;
			KingdomKeptSpendPhase keptPhase = PrepareKeptSpend(Kept, Procedure, out keptSpend);
			if (keptPhase != KingdomKeptSpendPhase.ApplyCounts)
			{
				Popup.Show(keptPhase == KingdomKeptSpendPhase.RefusedClean
					? "The kept parts would not agree to be spent. Nothing else was changed."
					: "A kept stack changed while the hall asked whether every source could release. No water was spent and no graft was made; inspect your kept parts before trying again.");
				return;
			}
			if (KingdomProcedures.HasProcedureClass(Actor, Procedure))
			{
				Popup.Show("That procedure already exists somewhere on you. The hall will not commission a second live instance. Nothing was spent.");
				return;
			}
			XRL.World.Anatomy.BodyPart selected = SelectedPart(Actor, At);
			GameObject bearer = (Procedure.Attach == LabAttach.Weapon) ? selected?.DefaultBehavior : Actor;
			if (selected == null || !GameObject.Validate(bearer))
			{
				Popup.Show("The selected body part changed before the commission could be recorded. Nothing was spent.");
				return;
			}
			string pendingProperty = PendingProperty(Procedure.Key);
			if (!string.IsNullOrEmpty(Actor.GetStringProperty(pendingProperty)))
			{
				Popup.Show("A live commission for that procedure already follows you. Recover it before commissioning another.");
				return;
			}
			KingdomRulerLifeSnapshot rulerLife;
			if (!TryFreezeRulerLife(System, Actor, realmId, out rulerLife)) return;
			KingdomSurvey survey = (Actor.CurrentZone == null) ? null : KingdomSurvey.Take(Actor.CurrentZone, System);
			KingdomWaterDebit debit;
			if (survey == null || !survey.TryReserveExactWater(Procedure.Cost, out debit))
			{
				Popup.Show("The stores at " + KingdomLabRules.Named(
					KingdomPresentation.Rich(City)) + " cannot spare {{C|" + Procedure.Cost
					+ "}} drams. Fill them, and the hall will take the work on.");
				return;
			}
			KingdomBitTally bitCost;
			string bitError;
			if (!KingdomMaterialRules.TryParseBitCost(Procedure.Bits, out bitCost, out bitError))
			{
				Popup.Show("The procedure's bit price is invalid (" + bitError + "). Nothing was spent.");
				return;
			}
			KingdomMaterialDebit bitDebit = bitCost.IsEmpty() ? null
				: KingdomMaterials.ReserveBits(Actor.CurrentZone, bitCost);
			if (!bitCost.IsEmpty() && (bitDebit == null
				|| bitDebit.Reservation.Outcome != KingdomMaterialDebitOutcome.Reserved))
			{
				Popup.Show("The settlement's dedicated stockpiles cannot cover that exact bit price. Nothing was spent.");
				return;
			}
			string jobId = PurposeCommissionJobId(Building, Actor, Procedure, selected)
				?? Guid.NewGuid().ToString("N");
			string manager = KingdomProcedures.ManagerFor(Procedure.Key);
			string detail = KingdomProcedures.ExecutionDetail(Procedure, stamp);
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, Procedure.Key, Procedure.Grants,
				(int)Procedure.Source, (int)Procedure.Attach, manager, detail);
			r_KingdomLabJob job = new r_KingdomLabJob
			{
				JobId = jobId,
				BuildingId = Building.ID,
				ProcedureKey = Procedure.Key,
				PatientId = Actor.ID,
				GameId = The.Game?.GameID ?? "",
				RealmId = realmId,
				RealmFoundedTick = System.FoundedTick,
					RulerSuccessionOrdinal = rulerLife.SuccessionOrdinal, RulerLifeId = rulerLife.RulerLifeId, BodyHistoryContractVersion = KingdomBodyHistoryRules.LabContractVersion, BodyHistoryPhase = (int)KingdomLabBodyHistoryPhase.Pending,
				BodyPartId = selected.ID,
				BearerId = bearer.ID,
				Stamp = stamp,
				City = City ?? "",
				ContractVersion = KingdomLabRules.EffectContractVersion,
				FrozenName = Procedure.Named,
				FrozenGrants = Procedure.Grants,
				FrozenSource = (int)Procedure.Source,
				FrozenAttach = (int)Procedure.Attach,
				FrozenManager = manager,
				FrozenDetail = detail,
				FrozenMagnitude = Procedure.Magnitude ?? "",
				FrozenCreeds = Procedure.Creeds ?? "",
				FrozenClass = (int)Procedure.Class,
				FrozenStaffDays = Procedure.StaffDays,
				FrozenFingerprint = fingerprint,
				Phase = (int)KingdomLabJobPhase.Funding,
				RemainingTicks = KingdomProcedureRules.StaffDayTicks(Procedure.StaffDays),
				LastWorkedTick = The.Game?.TimeTicks ?? 0L,
				WaterOwed = Procedure.Cost,
				KeptOwed = Procedure.Preserved,
				BitClaim = bitCost.IsEmpty() ? "" : bitDebit.Reservation.Requested.ToClaimString(),
				BitOutstanding = bitCost.IsEmpty() ? "" : bitDebit.Reservation.Requested.ToClaimString()
			};
			List<KeyValuePair<string, int>> standing = KingdomLabRules.StandingCost(Procedure.Creeds,
				KingdomLabRules.StandingPerCreed);
			for (int i = 0; i < standing.Count; i++)
			{
				job.StandingFactions.Add(standing[i].Key);
				job.StandingDeltas.Add(standing[i].Value);
				job.StandingBefore.Add(int.MinValue);
				job.StandingTargets.Add(int.MinValue);
				job.StandingPhases.Add((int)KingdomLabStandingPhase.Pending);
			}
			job.ChronicleEventId = "lab:apply:" + jobId + ":chronicle";
			job.PetitionEventId = "lab:apply:" + jobId + ":petition";
			job.AnnounceEventId = "lab:apply:" + jobId + ":message";
			job.ReadyMessageEventId = "lab:apply:" + jobId + ":ready-message";
			job.Normalize();
			if (job.SchemaQuarantined || !WriteCanonical(job, KingdomLabRegistryStatus.Active))
			{
				debit.Rollback();
				bitDebit?.Cancel();
				Popup.Show("The canonical commission receipt could not be persisted. Nothing was spent.");
				return;
			}
			try
			{
				Building.AddPart(job);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: job publication threw (" + ex.Message + ")");
			}
			if (KingdomProcedures.ReferencePartOrdinal(Building, job) < 0
				|| !ReferenceEquals(job.ParentObject, Building)
				|| CountParts<r_KingdomLabJob>(Building) != 1)
			{
				WriteCanonical(job, KingdomLabRegistryStatus.Quarantined);
				debit.Rollback();
				bitDebit?.Cancel();
				try
				{
					if (KingdomProcedures.ReferencePartOrdinal(Building, job) >= 0)
						Building.RemovePart(job);
				}
				catch { }
				Popup.Show("The hall could not prove one exact physical commission projection. Its canonical intent was quarantined; nothing was spent.");
				return;
			}
			try
			{
				Actor.SetStringProperty(pendingProperty, jobId);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: patient pending marker threw (" + ex.Message + ")");
			}
			if (!string.Equals(Actor.GetStringProperty(pendingProperty), jobId, StringComparison.Ordinal))
			{
				job.State = KingdomLabJobPhase.FundingRecovery;
				job.Fault = "The patient-side commission identity could not be persisted. No payment was attempted.";
				bitDebit?.Cancel();
				Popup.Show(job.Fault);
				return;
			}
			job.IntentPublished = true;
			LabProcedure frozen = FrozenProcedure(job);
			if (!ValidApplicationTarget(Actor, job, frozen))
			{
				debit.Rollback();
				bitDebit?.Cancel();
				job.State = KingdomLabJobPhase.FundingRecovery;
				job.Fault = "The exact patient slot or bearer changed before water commit. Nothing was charged.";
				return;
			}
			debit.Commit();
			if (!ValidApplicationTarget(Actor, job, frozen))
			{
				debit.Rollback();
				MergeWaterReceipt(job, debit);
				bitDebit?.Cancel();
				job.State = job.WaterQuarantined ? KingdomLabJobPhase.ApplicationRecovery
					: KingdomLabJobPhase.FundingRecovery;
				job.Fault = job.WaterQuarantined
					? "The target changed during water callbacks and exact compensation could not be proved. The receipt is quarantined."
					: "The target changed during water callbacks. The exact debit was compensated; retry charges only the outstanding price.";
				EnsureJobGovernance(job);
				return;
			}
			bool waterExact = MergeWaterReceipt(job, debit);
			bool bitsExact = bitCost.IsEmpty();
			if (!waterExact)
			{
				bitDebit?.Cancel();
			}
			else if (bitDebit != null)
			{
				if (!ValidApplicationTarget(Actor, job, frozen))
				{
					debit.Rollback();
					MergeWaterReceipt(job, debit);
					bitDebit.Cancel();
					job.State = job.WaterQuarantined ? KingdomLabJobPhase.ApplicationRecovery
						: KingdomLabJobPhase.FundingRecovery;
					job.Fault = "The exact target changed before bit commit. Water compensation was measured; no bits or body effect were touched.";
					EnsureJobGovernance(job);
					return;
				}
				KingdomMaterialDebitResult bitResult = bitDebit.Commit();
				bitsExact = bitResult.Exact;
				if (bitResult.Outcome == KingdomMaterialDebitOutcome.RecoverablePartial
					&& bitDebit.CanCompensate)
				{
					KingdomMaterialDebitResult compensation = bitDebit.Compensate();
					if (compensation.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
					{
						bitResult = compensation;
					}
				}
				job.BitOutstanding = bitsExact ? "" : ((bitResult.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
					? bitDebit.Reservation.Requested.ToClaimString()
					: bitResult.Outstanding.ToClaimString());
				if (!bitsExact)
				{
					job.Fault = bitResult.Failure ?? "The exact bit debit was interrupted.";
				}
			}
			if (waterExact && bitsExact && !ValidApplicationTarget(Actor, job, frozen))
			{
				bool bitsRestored = bitDebit == null;
				if (bitDebit != null && bitDebit.CanCompensate)
				{
					KingdomMaterialDebitResult compensation = bitDebit.Compensate();
					bitsRestored = compensation.Outcome == KingdomMaterialDebitOutcome.CompensatedExact;
					if (bitsRestored) job.BitOutstanding = job.BitClaim;
				}
				bool waterRestored = debit.Rollback();
				MergeWaterReceipt(job, debit);
				job.State = (bitsRestored && waterRestored && !job.WaterQuarantined)
					? KingdomLabJobPhase.FundingRecovery : KingdomLabJobPhase.ApplicationRecovery;
				job.Fault = (bitsRestored && waterRestored && !job.WaterQuarantined)
					? "The exact target changed during funding callbacks. Water and bits were compensated; no kept part or body effect was touched."
					: "The exact target changed during funding callbacks and complete compensation could not be proved. The receipt is quarantined.";
				EnsureJobGovernance(job);
				return;
			}
			keptPhase = (waterExact && bitsExact) ? SpendKeptExact(keptSpend)
				: KingdomKeptSpendPhase.RefusedClean;
			int keptMeasured = (waterExact && bitsExact) ? KeptSpent(keptSpend) : 0;
			job.KeptPaid = keptMeasured; job.KeptLost = keptMeasured;
			if (keptPhase == KingdomKeptSpendPhase.Partial)
			{
				job.KeptMeasurementExact = false;
				job.KeptQuarantined = true;
			}
			job.State = job.KeptQuarantined ? KingdomLabJobPhase.ApplicationRecovery
				: KingdomLabRules.FundingPhase(waterExact, bitsExact, keptPhase);
			EnsureJobGovernance(job);
			if (job.State == KingdomLabJobPhase.Working)
			{
				MessageQueue.AddPlayerMessage("{{G|" + KingdomLabRules.StakedLine(Procedure.Named,
					Procedure.StaffDays) + "}}");
				return;
			}
			Popup.Show("The paid commission is persisted, but its exact funding was interrupted. No graft was made. Read this hall's slate to inspect and retry the outstanding receipt.");
		}
	} }
