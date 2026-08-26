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
		internal bool StageSite(KingdomInheritanceSiteCandidate Candidate, int X, int Y,
			MutabilityMap Map, WorldInfo Info, out string Failure)
		{
			Failure = "";
			if (Phase != KingdomInheritancePhase.Reserved || !KingdomInheritanceSiteRules.IsSafe(Candidate)
				|| Map == null || Info == null || Map.GetMutable(X, Y) != 0
				|| Candidate.ZoneId != XRL.World.ZoneID.Assemble(KingdomInheritanceSiteRules.WorldId,
					X / 3, Y / 3, X % 3, Y % 3, KingdomInheritanceSiteRules.SurfaceDepth))
			{
				Failure = "the selected inherited site was not an exact removed mutable surface cell";
				return false;
			}
			TargetZoneId = Candidate.ZoneId;
			TargetTerrainBlueprint = Candidate.TerrainBlueprint;
			TargetTerrainRank = Candidate.TerrainRank;
			ReservedMap = Map;
			ReservedWorldInfo = Info;
			TargetX = X;
			TargetY = Y;
			ReservedTerrainTag = Candidate.TerrainTag ?? "";
			Transition(KingdomInheritancePhase.SiteSelected);
			return true;
		}

		internal void RefuseBootstrap(string Detail)
		{
			if (Phase == KingdomInheritancePhase.Reserved
				|| Phase == KingdomInheritancePhase.SiteSelected
				|| Phase == KingdomInheritancePhase.WorldValidated)
			{
				ReleaseReservation(Detail);
			}
		}

		internal void RecordApplyResult(KingdomInheritApplyResult Result, bool WillRetry = false,
			bool DuringZoneBuild = false)
		{
			if (Result == null)
			{
				SetRepair("the inherited-site builder returned no result");
				if (!WillRetry)
				{
					AnnounceFailure();
				}
				return;
			}
			ApplyStatusValue = (int)Result.Status;
			ApplyFaultValue = (int)Result.Fault;
			ApplicationMarker = Bound(Result.ApplicationMarker, 1000);
			switch (Result.Status)
			{
			case KingdomInheritApplyStatus.Applied:
			case KingdomInheritApplyStatus.AlreadyApplied:
				RetryAuthorized = false;
				FailureDetail = "";
				FailureAnnounced = false;
				ReleasePending = false;
				if (Phase != KingdomInheritancePhase.Committed)
				{
					Transition(KingdomInheritancePhase.AppliedPendingDurability);
				}
				break;
			case KingdomInheritApplyStatus.Refused:
				RetryAuthorized = false;
				if (DuringZoneBuild)
				{
					SetRepair("the inherited site was refused when its zone was built: "
						+ Result.Detail);
				}
				else
				{
					ReleaseReservation("the inherited site was refused: " + Result.Detail);
				}
				break;
			default:
				SetRepair("the inherited site needs repair: " + Result.Detail);
				break;
			}
			if (!WillRetry)
			{
				AnnounceFailure();
			}
		}

		/// <summary>An older same-game rollback can retain an unbuilt exact lazy site after the
		/// profile receipt has already committed. The builder may adopt that external final state
		/// immediately after exact application; no second profile transition or durability spend is
		/// involved, and a crash simply repeats the deterministic reconstruction.</summary>
		internal void AdoptExternalCommittedIfKnown(Zone Zone)
		{
			try
			{
				if (!ProfileReceiptWasCommitted || ProfileCommittedReceipt == null)
				{
					return;
				}
				KingdomSealRecord legacy;
				KingdomSealReceipt reserved;
				string expected;
				string marker = Zone == null ? ""
					: Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
				if (!TryGetReservation(out legacy, out reserved)
					|| Zone == null || Zone.ZoneID != TargetZoneId
					|| !KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy, reserved,
						TargetZoneId, KingdomInheritEngine.ReconstructionVersion, out expected)
					|| !KingdomInheritanceStateRules.RetainsDurableApplicationCandidate(
						ApplyStatusValue, ApplyFaultValue, ApplicationMarker)
					|| ApplicationMarker != expected || marker != expected)
				{
					SetRepair("the externally committed lazy site lost its exact application marker");
					HideDiscoverability(Zone);
					AnnounceFailure();
					return;
				}
				AdoptCommitted(reserved, ProfileCommittedReceipt, Zone);
			}
			catch (Exception ex)
			{
				// Exact Apply already succeeded. Optional profile adoption/discovery must never
				// escape into the builder's application-fallback catch.
				RecordDiscoveryFailure("external committed-site adoption threw: " + ex.Message);
			}
		}

		internal void AuthorizeExactOwnedRepair()
		{
			RetryAuthorized = true;
		}

		internal void RecordBuilderFailure(string Detail)
		{
			ApplyStatusValue = (int)KingdomInheritApplyStatus.Failed;
			ApplyFaultValue = (int)KingdomInheritApplyFault.PartialApplication;
			SetRepair("the inherited-site builder failed closed: " + Detail);
			AnnounceFailure();
		}

		internal bool TryBuilderPayload(string LegacyId, string TargetGameId, string ZoneId,
			int ReconstructionVersion, out KingdomSealRecord Legacy, out KingdomSealReceipt Receipt,
			out string Failure)
		{
			Legacy = null;
			Receipt = null;
			Failure = "";
			if ((Phase != KingdomInheritancePhase.Installed
					&& Phase != KingdomInheritancePhase.AppliedPendingDurability
					&& Phase != KingdomInheritancePhase.Committed
					&& Phase != KingdomInheritancePhase.RepairRequired)
				|| ReconstructionVersion != KingdomInheritEngine.ReconstructionVersion
				|| ZoneId != TargetZoneId || !TryGetReservation(out Legacy, out Receipt)
				|| Legacy.LegacyId != LegacyId || Receipt.TargetGameId != TargetGameId)
			{
				Failure = "the persisted builder does not name this exact inherited target";
				Legacy = null;
				Receipt = null;
				return false;
			}
			return true;
		}

	}
}
