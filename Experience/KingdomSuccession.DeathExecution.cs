using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;
using XRL.World.Tinkering;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		private void CarryFounderSuccession(AfterDieEvent E, XRLGame game, GameObject founder,
			KingdomSystem system, string founderName, string founderCause, long deathTick,
			string token, HeirRuntime chosen, GameObject heirBody, string heirZoneId,
			bool heirWasSeated, KingdomSuccessionRite.Plan ritePlan)
		{
			string riteFailure;
			string citizenshipFailure;
			int newsDays;
			NewsRoad newsRoad;
			JudgeActualNews(system, founder.CurrentZone, out newsDays, out newsRoad);
			long dueTick = KingdomSuccessionRules.NewsDueTick(deathTick, newsDays);
			bool heldOffice = chosen.Rule.HoldsOffice;
			string heirCreed = heirBody.GetStringProperty(KingdomCreed.CreedProperty);

			PendingDeathToken = token;
			PendingPhase = InterregnumPhase.WordOnTheRoad;
			PendingDueTick = dueTick;
			PendingRoad = newsRoad;
			PendingDays = newsDays;
			LegacyPhysicalRiteUnavailable = false;
			PendingFounderName = founderName;
			PendingFounderObjectId = founder.IDIfAssigned;
			PendingFounderCause = founderCause;
			PendingHeirResidentId = chosen.Rule.ResidentId;
			PendingHeirObjectId = heirBody.IDIfAssigned;
			PendingHeirName = chosen.Rule.Name;
			PendingHeirZoneId = heirZoneId;
			PendingRiteZoneId = ritePlan.ZoneId;
			PendingRiteCityName = ritePlan.CityName;
			PendingRiteFixtureObjectId = ritePlan.FixtureObjectId;
			PendingRiteFixtureName = ritePlan.FixtureName;
			PendingShrineX = ritePlan.ShrineX;
			PendingShrineY = ritePlan.ShrineY;
			PendingRiteAttendeeManifest = ritePlan.Manifest;
			PendingShrineObjectId = "";
			Checkpoint(MourningRiteStage.Frozen);
			r_KingdomFounderRemains remains = new r_KingdomFounderRemains(token, founderName);
			founder.AddPart(remains);

			long advance = KingdomSuccessionRules.WorldTicksUntilDue(game.TimeTicks, dueTick);
			if (advance > 0L)
			{
				game.TimeTicks = dueTick;
			}
			PendingPhase = InterregnumPhase.RiteDue;
			Checkpoint(MourningRiteStage.WordArrived);

			GameObject walkedHeir;
			if (!KingdomSuccessionRite.TryHoldProcession(system, token, PendingRiteZoneId,
				PendingRiteFixtureObjectId, PendingRiteAttendeeManifest,
				out walkedHeir, out riteFailure)
				|| !ReferenceEquals(walkedHeir, heirBody)
				|| walkedHeir.GetIntProperty(KingdomResidents.ResidentIdProperty)
					!= PendingHeirResidentId
				|| !string.Equals(walkedHeir.IDIfAssigned, PendingHeirObjectId,
					StringComparison.Ordinal))
			{
				KingdomLog.Log("succession: physical procession refused (" + riteFailure + ")");
				AbortPendingBeforeTransfer(founder, remains);
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			Checkpoint(MourningRiteStage.ProcessionComplete);

			string shrineHistory = KingdomSuccessionRules.FounderEpitaph(
				KingdomPresentation.Rich(founderName),
				KingdomPresentation.Rich(PendingRiteCityName),
				KingdomPresentation.Rich(system.FoundingRegionName),
				KingdomPresentation.Rich(PendingFounderCause))
				+ " The named residents present walked to "
				+ KingdomPresentation.Rich(PendingRiteFixtureName)
				+ " and held the mourning rite here.";
			GameObject founderShrine;
			if (!KingdomSuccessionRite.TryEnsureFounderShrine(token, founderName, deathTick,
				PendingFounderCause, shrineHistory, PendingRiteCityName, PendingRiteZoneId,
				PendingRiteFixtureObjectId, PendingShrineX, PendingShrineY,
				PendingShrineObjectId, out founderShrine, out riteFailure))
			{
				KingdomLog.Log("succession: founder shrine refused (" + riteFailure + ")");
				AbortPendingBeforeTransfer(founder, remains);
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			PendingShrineObjectId = founderShrine.IDIfAssigned;
			CompletedShrineToken = token;
			CompletedShrineObjectId = PendingShrineObjectId;
			CompletedShrineZoneId = PendingRiteZoneId;
			Checkpoint(MourningRiteStage.ShrinePlaced);

			// After SetBody returns and the explicit global player-system sweep succeeds, no mod
			// action dispatches or yields before the prebuilt resident snapshots are published.
			KingdomResidentRow formerRow = default(KingdomResidentRow);
			// Unknown is repair-required. RefusedClean is trustworthy only when TryAccede
			// returns it after re-reading both exact original carriers, or when the
			// publication boundary was never entered at all.
			KingdomAccessionOutcome accession = KingdomAccessionOutcome.RepairRequired;
			bool accessionSeated = heirWasSeated;
			bool founderRestored = false;
			bool heirContinuationRegistrationsExact = false;
			// Procession and shrine callbacks can advance world state after early preflight.
			// Re-prove exact reversible citizenship immediately before irreversible body transfer.
			if (!KingdomCitizenship.CanRemove(system, heirBody, out citizenshipFailure))
			{
				KingdomLog.Log("succession: exact heir citizenship changed before body transfer ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				AbortPendingBeforeTransfer(founder, remains);
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			KingdomPlayerBodyTransfer forward = SetPlayerBodyAndRebindAll(game, founder,
				heirBody, "accession");
			if (forward.TargetControls)
			{
				Checkpoint(MourningRiteStage.BodyCrossed);
			}
			if (forward.MayPublishAccession)
			{
				heirContinuationRegistrationsExact = true;
				try
				{
					accession = KingdomAccessionOutcome.RepairRequired;
					accession = KingdomResidents.TryAccede(system, heirBody,
						out formerRow, out accessionSeated);
					if (accession == KingdomAccessionOutcome.RefusedClean)
					{
						accession = KingdomAccessionOutcome.RepairRequired;
						accession = KingdomResidents.TryAccede(system, heirBody,
							out formerRow, out accessionSeated);
					}
					if (accession == KingdomAccessionOutcome.RepairRequired
						&& formerRow.ResidentId == chosen.Rule.ResidentId)
					{
						accession = KingdomAccessionOutcome.RepairRequired;
						accession = KingdomResidents.TryRepairAccession(system, heirBody,
							chosen.Rule.ResidentId, accessionSeated, formerRow.Name,
							formerRow.ArrivedTick, formerRow.KeptCreeds, out formerRow);
					}
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: accession publish retry failed", ex);
				}
			}
			else
			{
				// Any thrown, short-circuited, misdirected, or incompletely rebound forward
				// transfer is not an accession. No resident carrier has been touched yet.
				KingdomLog.Log("succession: forward body transfer was not a clean globally rebound heir transfer; restoring founder control");
				KingdomPlayerBodyTransfer rollback = SetPlayerBodyAndRebindAll(game, heirBody,
					founder, "accession rollback");
				founderRestored = rollback.TargetControls;
				heirContinuationRegistrationsExact = rollback.OriginalControls
					&& rollback.RegistrationsExact;
				accession = founderRestored ? KingdomAccessionOutcome.RefusedClean
					: KingdomAccessionOutcome.RepairRequired;
			}
			if (accession != KingdomAccessionOutcome.Committed)
			{
				if (accession == KingdomAccessionOutcome.RepairRequired)
				{
					if (!KingdomSuccessionRules.MayQueueAccessionRepair(
						ReferenceEquals(The.Player, heirBody),
						heirContinuationRegistrationsExact))
					{
						FailCatastrophicBodyTransfer(system, founder, founderName, remains, E,
							"the failed body transfer ended on neither a globally rebound founder nor the exact heir");
						return;
					}
					AccessionOwnershipCommitted = true;
					QueueAccessionRepair(chosen.Rule, founderName, accessionSeated);
					TryPrepareRepairableHeir(heirBody);
					KingdomLog.Log("succession: accession carriers need repair; control remains with the heir");
					TryTellFailure("Control passed from the founder, but the resident accession carriers did not converge. The line remains open and the exact repair is queued.");
					return;
				}
				KingdomLog.Log("succession: CRITICAL accession publish failed immediately after SetBody; rolling control back to the dying founder");
				if (!founderRestored)
				{
					KingdomPlayerBodyTransfer rollback = SetPlayerBodyAndRebindAll(game,
						heirBody, founder, "accession rollback");
					founderRestored = rollback.TargetControls;
					heirContinuationRegistrationsExact = rollback.OriginalControls
						&& rollback.RegistrationsExact;
				}
				if (!KingdomSuccessionRules.MayTerminalAfterAccessionFailure(
					accession == KingdomAccessionOutcome.RefusedClean, founderRestored))
				{
					if (!KingdomSuccessionRules.MayQueueAccessionRepair(
						ReferenceEquals(The.Player, heirBody),
						heirContinuationRegistrationsExact))
					{
						FailCatastrophicBodyTransfer(system, founder, founderName, remains, E,
							"the clean resident refusal could not restore founder control or prove the exact heir");
						return;
					}
					// SetBody may change control and then throw. Never terminalize a lineage after
					// control left the dying founder; resident-law repair remains a separate task.
					AccessionOwnershipCommitted = true;
					QueueAccessionRepair(chosen.Rule, founderName, accessionSeated);
					TryPrepareRepairableHeir(heirBody);
					KingdomLog.Log("succession: CRITICAL founder control could not be restored; line remains open for accession repair");
					TryTellFailure("Control passed from the founder, but the resident accession record could not be published or rolled back. The line remains open and requires repair.");
					return;
				}
				AbortPendingBeforeTransfer(founder, remains);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}

			CompleteAccession(game, system, heirBody, founderName, formerRow, token,
				newsRoad, newsDays, heldOffice, heirCreed, heirZoneId, "accession");
		}

	}
}
