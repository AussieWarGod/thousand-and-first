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
		internal void ResumeAfterLoad(KingdomInheritanceLoadKind LoadKind,
			string LoadSourceFailure)
		{
			XRLGame game = The.Game;
			if (game == null)
			{
				return;
			}
			KingdomInheritanceLeaseOwner.BeginGame(game.GameID);
			if (RecoveryDisabled)
			{
				AnnounceFailure();
				return;
			}
			if (Phase == KingdomInheritancePhase.Empty || Phase == KingdomInheritancePhase.Refused)
			{
				return;
			}
			try
			{
				bool exactPrimaryLoad = LoadKind == KingdomInheritanceLoadKind.Primary;
				KingdomSealRecord legacy;
				KingdomSealReceipt reserved;
				if (!TryGetReservation(out legacy, out reserved))
				{
					SetRepair("the loaded inheritance state lost its canonical reservation");
					AnnounceFailure();
					return;
				}
				KingdomSeal seal = game.RequireSystem<KingdomSeal>();
				KingdomSealReceipt expected = reserved;
				KingdomSealReceipt savedCommitted;
				if (TryGetCommittedReceipt(out savedCommitted))
				{
					expected = savedCommitted;
				}
				KingdomSealReceipt current;
				string failure = "";
				if (seal == null || !seal.TryInspectImport(expected, out current, out failure)
					|| current == null)
				{
					SetRepair("the loaded inheritance receipt could not be inspected: "
						+ Nonempty(failure, "the exact profile receipt was unavailable"));
					AnnounceFailure();
					return;
				}
				if (KingdomInheritanceStateRules.ProfileReceiptBlocksRelease(current.State))
				{
					ProfileReceiptWasCommitted = true;
					if (LoadKind == KingdomInheritanceLoadKind.Unknown)
					{
						// Coda, case collisions, and unproved paths cannot mutate target state.
						// Still retain the no-release guard: inspection already proved a final receipt.
						ProfileCommittedReceipt = null;
						return;
					}
					ProfileCommittedReceipt = current;
					Zone provenZone;
					if (!TryDurableProof(legacy, reserved, Phase == KingdomInheritancePhase.Installed,
						out provenZone, out failure))
					{
						ReconcileCommittedRewind(legacy, reserved, current, LoadKind, failure);
						return;
					}
					AdoptCommitted(reserved, current, provenZone);
					return;
				}
				if (current.State != KingdomSealReceiptState.Reserved)
				{
					SetRepair("the loaded inheritance receipt entered an unsupported final state");
					AnnounceFailure();
					return;
				}
				if (current.Compose() != ReceiptText)
				{
					if (Phase == KingdomInheritancePhase.AppliedPendingDurability
						|| !string.IsNullOrEmpty(ApplicationMarker))
					{
						SetRepair("the reservation tick changed after an application marker was formed");
						AnnounceFailure();
						return;
					}
					ReceiptText = current.Compose();
					reserved = current;
				}
				if (!EnsureReservationLease(seal, reserved, out failure))
				{
					SetRepair("the loaded inheritance reservation could not resume: " + failure);
					AnnounceFailure();
					return;
				}

				if (ReleasePending)
				{
					string cleanupFailure;
					if (TryRemoveInstalledArtifacts(out cleanupFailure))
					{
						ReleaseReservation("the loaded target is retrying its exact refused import release",
							RestoreMutable: false);
					}
					else
					{
						SetRepair("the loaded target could not prove artifact cleanup before release: "
							+ cleanupFailure);
					}
					AnnounceFailure();
				}
				else if (Phase == KingdomInheritancePhase.AppliedPendingDurability
					&& exactPrimaryLoad)
				{
					CommitDurableProof(seal, legacy, reserved);
				}
				else if (Phase == KingdomInheritancePhase.Installed && exactPrimaryLoad
					&& The.ZoneManager != null && The.ZoneManager.IsZoneBuilt(TargetZoneId))
				{
					Zone recovered;
					if (TryDurableProof(legacy, reserved, AllowInstalledRecovery: true,
						out recovered, out failure))
					{
						Transition(KingdomInheritancePhase.AppliedPendingDurability);
						CommitKnownProof(seal, reserved, recovered);
					}
					else
					{
						SetRepair("a built target with installed state failed marker-ownership recovery: "
							+ failure);
						HideDiscoverability(recovered);
						AnnounceFailure();
					}
				}
				else if (Phase == KingdomInheritancePhase.RepairRequired)
				{
					RepairLoadedTarget(seal, legacy, reserved, exactPrimaryLoad);
				}
				else if (Phase == KingdomInheritancePhase.Committed)
				{
					SetRepair("the primary says committed while the exact profile receipt is reserved");
					AnnounceFailure();
				}
			}
			catch (Exception ex)
			{
				SetRepair("loaded inheritance recovery failed closed: " + ex.Message);
				AnnounceFailure();
			}
		}

		internal void HandleTargetZoneBuilt(Zone Zone)
		{
			if (RecoveryDisabled || Zone == null || Zone.ZoneID != TargetZoneId)
			{
				return;
			}
			try
			{
				if (Phase == KingdomInheritancePhase.Refused)
				{
					HideDiscoverability(Zone);
					string cleanupFailure;
					if (!TryRemoveInstalledArtifacts(out cleanupFailure))
					{
						SetRepair("the refused target retained unresolved artifacts: " + cleanupFailure);
					}
				}
				else if (Phase == KingdomInheritancePhase.RepairRequired)
				{
					HideDiscoverability(Zone);
					if (!KingdomInheritanceStateRules.RetainsDurableApplicationCandidate(
						ApplyStatusValue, ApplyFaultValue, ApplicationMarker))
					{
						string failure;
						if (!TryQuarantineExact(Zone, out failure))
						{
							SetRepair("the failed inherited zone could not be quarantined: " + failure);
						}
					}
				}
			}
			catch (Exception ex)
			{
				SetRepair("the failed inherited zone could not be hidden: " + ex.Message);
			}
			AnnounceFailure();
		}

	}
}
