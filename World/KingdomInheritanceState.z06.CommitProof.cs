using System;
using System.Collections.Generic;
using System.IO;
using Genkit;
using Qud.API;
using XRL;
using XRL.CharacterBuilds.Qud;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.WorldBuilders;

namespace ThousandAndFirst
{
	public sealed partial class KingdomInheritanceState
	{
		private void CommitDurableProof(KingdomSeal Seal, KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved)
		{
			Zone zone;
			string failure = "";
			if (!TryDurableProof(Legacy, Reserved, AllowInstalledRecovery: false,
				out zone, out failure))
			{
				SetRepair("the inherited application was not durable in the loaded primary: " + failure);
				HideDiscoverability(zone);
				AnnounceFailure();
				return;
			}
			CommitKnownProof(Seal, Reserved, zone);
		}

		private void CommitKnownProof(KingdomSeal Seal, KingdomSealReceipt Reserved, Zone Zone)
		{
			KingdomSealReservationLease lease = GetReservationLease(Reserved);
			KingdomSealReceipt committed;
			string failure = "";
			if (lease == null || Seal == null
				|| !Seal.TryCommitImport(Reserved, lease, out committed, out failure)
				|| committed == null)
			{
				SetRepair("the durable inherited application could not commit its receipt: "
					+ Nonempty(failure, "the exact live reservation was unavailable"));
				HideDiscoverability(Zone);
				AnnounceFailure();
				return;
			}
			KingdomInheritanceLeaseOwner.Forget(lease);
			ReservationLease = null;
			// The profile transition is already durable at this point. Guard against any
			// subsequent target-state/adoption fault attempting to release the spent receipt.
			ProfileReceiptWasCommitted = true;
			ProfileCommittedReceipt = committed;
			AdoptCommitted(Reserved, committed, Zone);
		}

		private void AdoptCommitted(KingdomSealReceipt Reserved,
			KingdomSealReceipt Committed, Zone Zone)
		{
			if (Reserved == null || Committed == null
				|| Committed.State != KingdomSealReceiptState.Committed
				|| Committed.LineageId != Reserved.LineageId
				|| Committed.LegacyId != Reserved.LegacyId
				|| Committed.TargetGameId != Reserved.TargetGameId
				|| Committed.WrittenTick < Reserved.WrittenTick)
			{
				SetRepair("the committed receipt was not a monotone state of the exact reservation");
				return;
			}
			string committedText;
			try
			{
				committedText = Committed.Compose();
			}
			catch (Exception ex)
			{
				SetRepair("the committed receipt could not be persisted canonically: " + ex.Message);
				AnnounceFailure();
				return;
			}
			string discoveryFailure = "";
			string marker = ApplicationMarker;
			bool zoneMarkerValid = Zone == null;
			if (Zone != null)
			{
				try
				{
					string observedMarker = Bound(Zone.GetZoneProperty(
						KingdomInheritEngine.ZoneMarkerProperty, ""), 1000);
					if (string.IsNullOrEmpty(observedMarker)
						|| (!string.IsNullOrEmpty(ApplicationMarker)
							&& observedMarker != ApplicationMarker))
					{
						discoveryFailure = "the committed zone marker changed after exact proof";
					}
					else
					{
						marker = observedMarker;
						zoneMarkerValid = true;
					}
				}
				catch (Exception ex)
				{
					discoveryFailure = "the committed zone marker could not be reread: "
						+ ex.Message;
				}
			}
			CommittedReceiptText = committedText;
			ProfileReceiptWasCommitted = true;
			ProfileCommittedReceipt = Committed;
			ApplicationMarker = marker;
			FailureDetail = "";
			FailureAnnounced = false;
			Transition(KingdomInheritancePhase.Committed);
			try
			{
				KingdomInheritanceLeaseOwner.Finish(Committed.TargetGameId, Reserved);
			}
			catch (Exception ex)
			{
				discoveryFailure = AppendFailure(discoveryFailure,
					"the completed process lease could not close: " + ex.Message);
			}
			ReservationLease = null;
			if (!string.IsNullOrEmpty(discoveryFailure))
			{
				RecordDiscoveryFailure(discoveryFailure);
			}
			if (!zoneMarkerValid)
			{
				BestEffortHideBrokenDiscovery(Zone);
				return;
			}
			TryRestoreDiscoverability(Zone);
		}

		private bool TryDurableProof(KingdomSealRecord Legacy, KingdomSealReceipt Reserved,
			bool AllowInstalledRecovery, out Zone Zone, out string Failure)
		{
			Zone = null;
			Failure = "";
			if (ReleasePending)
			{
				Failure = "the target is pending release rather than durable commit";
				return false;
			}
			string expected;
			if (!KingdomInheritanceStateRules.TryComposeApplicationMarker(Legacy, Reserved,
				TargetZoneId, KingdomInheritEngine.ReconstructionVersion, out expected))
			{
				Failure = "the canonical reservation could not recompute its application marker";
				return false;
			}
			bool built = The.ZoneManager != null
				&& KingdomInheritanceSiteRules.IsCanonicalSurfaceZoneId(TargetZoneId)
				&& The.ZoneManager.IsZoneBuilt(TargetZoneId);
			if (!built)
			{
				Failure = "the exact target zone was not persisted as built";
				return false;
			}
			Zone = The.ZoneManager.GetZone(TargetZoneId);
			if (Zone == null || Zone.ZoneID != TargetZoneId)
			{
				Failure = "the persisted target zone could not be loaded exactly";
				return false;
			}
			string marker = Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			if (!KingdomInheritanceStateRules.IsDurableMarkerProof(Phase, ApplyStatusValue,
				built, ApplicationMarker, expected, marker, AllowInstalledRecovery))
			{
				Failure = "the persisted phase and exact zone marker do not prove one durable application";
				return false;
			}
			// Engine.Apply already proved the exact objects before publishing this marker. On a later
			// Primary load, state phase + recomputed marker + loaded zone marker are the durability
			// proof. Rechecking objects here would punish lawful moving, filling, or destruction.
			ApplicationMarker = expected;
			ApplyStatusValue = (int)KingdomInheritApplyStatus.AlreadyApplied;
			ApplyFaultValue = (int)KingdomInheritApplyFault.None;
			return true;
		}

	}
}
