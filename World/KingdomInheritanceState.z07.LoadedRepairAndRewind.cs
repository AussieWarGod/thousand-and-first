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
		private void RepairLoadedTarget(KingdomSeal Seal, KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved, bool ExactPrimaryLoad)
		{
			if (ReleasePending)
			{
				string cleanupFailure;
				if (TryRemoveInstalledArtifacts(out cleanupFailure))
				{
					ReleaseReservation("the repaired target is retrying its exact profile release",
						RestoreMutable: false);
				}
				else
				{
					SetRepair("the repaired target could not prove cleanup before release: "
						+ cleanupFailure);
				}
				AnnounceFailure();
				return;
			}
			if (The.ZoneManager == null || !The.ZoneManager.IsZoneBuilt(TargetZoneId))
			{
				return;
			}
			Zone zone = The.ZoneManager.GetZone(TargetZoneId);
			if (zone == null || zone.ZoneID != TargetZoneId)
			{
				return;
			}
			HideDiscoverability(zone);
			string marker = zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			KingdomInheritApplyResult result;
			if (!string.IsNullOrEmpty(marker))
			{
				string retryFailure;
				if (TryRecoverUnvalidatedApplication(zone, Legacy, Reserved, out retryFailure))
				{
					marker = "";
				}
				else if (!ExactPrimaryLoad)
				{
					return;
				}
				else
				{
					Zone proven;
					string markerFailure;
					if (TryDurableProof(Legacy, Reserved, AllowInstalledRecovery: false,
						out proven, out markerFailure))
					{
						CommitKnownProof(Seal, Reserved, proven);
						return;
					}
					SetRepair("the loaded repair marker failed exact ownership proof: "
						+ markerFailure + "; " + retryFailure);
					HideDiscoverability(zone);
					AnnounceFailure();
					return;
				}
			}
			if (ApplyStatusValue != (int)KingdomInheritApplyStatus.Failed
				|| !RetryAuthorized)
			{
				return;
			}
			string failure;
			if (!TryQuarantineExact(zone, out failure))
			{
				SetRepair("the inherited target was not clean enough to retry: " + failure);
				AnnounceFailure();
				return;
			}
			if (!TryProveDirectRepairPrecondition(zone, Legacy, Reserved, out failure))
			{
				RetryAuthorized = false;
				SetRepair("the inherited target lost exact direct-repair provenance: " + failure);
				AnnounceFailure();
				return;
			}
			result = KingdomInheritEngine.Apply(Legacy, Reserved, TargetZoneId, zone);
			if (result == null)
			{
				RecordApplyResult(new KingdomInheritApplyResult(
					KingdomInheritApplyStatus.Failed,
					KingdomInheritApplyFault.PartialApplication,
					"the loaded repair Apply returned no result", "", 0, false));
				if (!TryCleanControlledRetry(zone, out failure))
				{
					SetRepair("null loaded repair result could not quarantine: " + failure);
				}
				HideDiscoverability(zone);
				AnnounceFailure();
				return;
			}
			if (result.Status == KingdomInheritApplyStatus.Applied
				|| result.Status == KingdomInheritApplyStatus.AlreadyApplied)
			{
				string reachFailure;
				if (!TryValidateAppliedZone(zone, out reachFailure))
				{
					KingdomInheritApplyResult failed = new KingdomInheritApplyResult(
						KingdomInheritApplyStatus.Failed,
						KingdomInheritApplyFault.PartialApplication, reachFailure,
						result.ApplicationMarker, result.PlacedCount, result.FreshEmptyVerified);
					RecordApplyResult(failed);
					string cleanupFailure;
					if (!TryCleanControlledRetry(zone, out cleanupFailure))
					{
						SetRepair("loaded repair failed reachability and exact quarantine: "
							+ cleanupFailure);
					}
					else
					{
						ApplicationMarker = "";
					}
					HideDiscoverability(zone);
					AnnounceFailure();
					return;
				}
				RecordApplyResult(result);
				TryRestoreDiscoverability(zone);
				return;
			}
			if (result.Status == KingdomInheritApplyStatus.Refused)
			{
				RetryAuthorized = false;
				SetRepair("the clean loaded inherited target was refused: " + result.Detail);
			}
			else
			{
				RecordApplyResult(result);
				if (result.Fault == KingdomInheritApplyFault.PartialApplication)
				{
					if (TryCleanControlledRetry(zone, out failure))
					{
						ApplicationMarker = "";
					}
					else
					{
						SetRepair("partial loaded repair could not quarantine: " + failure);
					}
				}
				else
				{
					RetryAuthorized = false;
				}
			}
			HideDiscoverability(zone);
			AnnounceFailure();
		}

		private void ReconcileCommittedRewind(KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved, KingdomSealReceipt Committed,
			KingdomInheritanceLoadKind LoadKind, string PriorFailure)
		{
			if (The.ZoneManager == null || Legacy == null || Reserved == null || Committed == null)
			{
				SetRepair("the externally committed inheritance could not inspect its rewound target: "
					+ PriorFailure);
				AnnounceFailure();
				return;
			}
			if (!The.ZoneManager.IsZoneBuilt(TargetZoneId))
			{
				string builderFailure;
				bool exactLazy = HasOnlyOwnedBuilders(TargetZoneId, Legacy.LegacyId,
					Reserved.TargetGameId,
					KingdomInheritEngine.ReconstructionVersionFor(Legacy), out builderFailure)
					&& The.ZoneManager.CountPartsFor(TargetZoneId) == 0;
				if (KingdomInheritanceStateRules.DecideCommittedRewind(LoadKind,
					ReceiptAlreadyCommitted: true, DurableProof: false, TargetBuilt: false,
					MarkerEmpty: true, ExactLazyBuilders: exactLazy,
					CleanReapplyPrecondition: false)
					!= KingdomCommittedRewindAction.AwaitLazyBuilder)
				{
					SetRepair("the rewound unbuilt target lost its exact lazy builder: "
						+ Nonempty(builderFailure, "a foreign persistent part was present"));
					AnnounceFailure();
				}
				// Keep Installed/Repair state and the nonserialized committed guard. The exact lazy
				// builder adopts immediately after exact application; a crash safely repeats it.
				return;
			}

			Zone zone = The.ZoneManager.GetZone(TargetZoneId);
			if (zone == null || zone.ZoneID != TargetZoneId)
			{
				SetRepair("the rewound committed target could not load its exact built zone");
				AnnounceFailure();
				return;
			}
			string marker = zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			if (!string.IsNullOrEmpty(marker))
			{
				string retryFailure;
				if (TryRecoverUnvalidatedApplication(zone, Legacy, Reserved, out retryFailure))
				{
					marker = "";
				}
				else
				{
					SetRepair("the rewound committed target carried a marker without valid "
						+ "saved provenance: " + retryFailure);
					HideDiscoverability(zone);
					AnnounceFailure();
					return;
				}
			}
			string failure;
			bool cleanReapply = TryQuarantineExact(zone, out failure)
				&& TryProveDirectRepairPrecondition(zone, Legacy, Reserved, out failure,
					RequireRetryAuthorization: false);
			if (KingdomInheritanceStateRules.DecideCommittedRewind(LoadKind,
				ReceiptAlreadyCommitted: true, DurableProof: false, TargetBuilt: true,
				MarkerEmpty: true, ExactLazyBuilders: true,
				CleanReapplyPrecondition: cleanReapply)
				!= KingdomCommittedRewindAction.ReapplyCleanBuiltTarget)
			{
				RetryAuthorized = false;
				SetRepair("the rewound committed target was not clean enough to reconstruct: "
					+ failure);
				HideDiscoverability(zone);
				AnnounceFailure();
				return;
			}
			AuthorizeExactOwnedRepair();
			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(Legacy, Reserved,
				TargetZoneId, zone);
			if (result != null && (result.Status == KingdomInheritApplyStatus.Applied
				|| result.Status == KingdomInheritApplyStatus.AlreadyApplied)
				&& TryValidateAppliedZone(zone, out failure))
			{
				RecordApplyResult(result);
				AdoptCommitted(Reserved, Committed, zone);
				return;
			}
			RecordApplyResult(new KingdomInheritApplyResult(KingdomInheritApplyStatus.Failed,
				KingdomInheritApplyFault.PartialApplication,
				result == null ? "the committed rewind Apply returned no result"
					: Nonempty(failure, result.Detail),
				result == null ? "" : result.ApplicationMarker,
				result == null ? 0 : result.PlacedCount,
				result != null && result.FreshEmptyVerified));
			if (!TryCleanControlledRetry(zone, out failure))
			{
				SetRepair("rewound committed reconstruction failed and could not quarantine: "
					+ failure);
			}
			else
			{
				ApplicationMarker = "";
			}
			HideDiscoverability(zone);
			AnnounceFailure();
		}

	}
}
