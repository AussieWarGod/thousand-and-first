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
		private void FailCatastrophicBodyTransfer(KingdomSystem System, GameObject Founder,
			string FounderName, r_KingdomFounderRemains Remains, AfterDieEvent Death,
			string Reason)
		{
			// A third or unproved controller is not the chosen heir. Never aim the persisted
			// resident roll-forward token at that body. End and disable this succession record.
			AccessionOwnershipCommitted = true;
			SuccessionDisabled = true;
			PendingDeathToken = "";
			PendingPhase = InterregnumPhase.None;
			PendingDueTick = 0L;
			PendingRoad = NewsRoad.Seat;
			PendingDays = 0;
			PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = "";
			PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSettlementId = "";
			ClearLegacyAccessionRepairSeated();
			PendingAccessionRepairArrivedTick = 0L;
			PendingAccessionRepairKeptCreeds = "";
			ClearPendingRiteIdentity();
			try
			{
				Founder?.RemovePart(Remains);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: catastrophic succession remains cleanup failed", ex);
			}
			PublishFounderDeath(System, FounderName, Death);
			try
			{
				KingdomChronicle.Record(System,
					KingdomSuccessionRules.DynastyEndChronicle(KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(FounderName)));
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: catastrophic dynasty-end chronicle failed", ex);
			}
			try
			{
				string failure;
				if (!KingdomSeal.TryTerminalFromSuccession(Death, LineEnded: true, out failure))
				{
					KingdomLog.Log("succession: catastrophic terminal seal attempt failed closed ("
						+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: catastrophic terminal seal attempt threw", ex);
			}
			KingdomLog.Log("succession: CATASTROPHIC body-transfer refusal; succession disabled ("
				+ Reason + ")");
			TryTellFailure("The body transfer ended in an unproved controller state. The dynasty has ended, succession is disabled for this save, and no resident identity was applied to the uncontrolled body.");
		}

		private void AbortPendingBeforeTransfer(GameObject Founder,
			r_KingdomFounderRemains Remains)
		{
			try
			{
				Founder?.RemovePart(Remains);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: founder-remains rollback failed", ex);
			}
			PendingDeathToken = "";
			PendingPhase = InterregnumPhase.None;
			PendingDueTick = 0L;
			PendingRoad = NewsRoad.Seat;
			PendingDays = 0;
			PendingSelectionReceipt = "";
			LegacySelectionReceiptUnavailable = false;
			ClearPendingRiteIdentity();
		}

		private void ClearPendingRiteIdentity()
		{
			PendingRiteStage = MourningRiteStage.None;
			PendingFounderName = "";
			PendingFounderObjectId = "";
			PendingFounderCause = "";
			PendingHeirResidentId = 0;
			PendingHeirObjectId = "";
			PendingHeirName = "";
			PendingHeirZoneId = "";
			PendingRiteZoneId = "";
			PendingRiteCityName = "";
			PendingRiteFixtureObjectId = "";
			PendingRiteFixtureName = "";
			PendingShrineX = 0;
			PendingShrineY = 0;
			PendingRiteAttendeeManifest = "";
			PendingShrineObjectId = "";
		}

		private void Checkpoint(MourningRiteStage Stage)
		{
			if (!KingdomSuccessionRules.MayAdvanceRite(PendingRiteStage, Stage))
			{
				throw new InvalidOperationException("The mourning rite attempted to skip a physical checkpoint.");
			}
			PendingRiteStage = Stage;
			InjectedCheckpoint?.Invoke(Stage);
		}

	}
}
