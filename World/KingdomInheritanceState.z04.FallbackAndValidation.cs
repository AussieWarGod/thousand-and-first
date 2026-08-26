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
		internal bool PrepareVanillaFallback(Zone Zone, string Detail, bool ExactOwnedZone = false)
		{
			string cleanupFailure = "";
			bool zoneClean = false;
			try
			{
				HideDiscoverability(Zone);
			}
			catch (Exception ex)
			{
				cleanupFailure = AppendFailure(cleanupFailure,
					"failed to hide inherited discovery during fallback: " + ex.Message);
			}
			try
			{
				string quarantineFailure;
				zoneClean = TryQuarantineExact(Zone, out quarantineFailure);
				if (!zoneClean)
				{
					cleanupFailure = AppendFailure(cleanupFailure, quarantineFailure);
				}
			}
			catch (Exception ex)
			{
				cleanupFailure = AppendFailure(cleanupFailure,
					"exact inherited-zone quarantine threw: " + ex.Message);
			}
			KingdomSealReceipt committedReceipt;
			bool profileCommitted = Phase == KingdomInheritancePhase.Committed
				|| ProfileReceiptWasCommitted
				|| TryGetCommittedReceipt(out committedReceipt);
			bool artifactsClean = false;
			if (KingdomInheritanceStateRules.ShouldAttemptFallbackArtifactCleanup(zoneClean,
				profileCommitted))
			{
				ApplicationMarker = "";
				RetryAuthorized = false;
				string artifactFailure;
				artifactsClean = TryRemoveInstalledArtifacts(out artifactFailure);
				if (!artifactsClean)
				{
					cleanupFailure = AppendFailure(cleanupFailure, artifactFailure);
				}
				if (artifactsClean)
				{
					ReleaseReservation(Detail, RestoreMutable: false);
					AnnounceFailure();
					// Persistent builders/properties are absent. ApplyTo stops on false, and the next
					// attempt therefore runs ordinary vanilla terrain even if profile release is pending.
					return false;
				}
			}
			if (KingdomInheritanceStateRules.MustPersistFallbackReleaseIntent(zoneClean,
				profileCommitted, artifactsClean))
			{
				ReleasePending = true;
			}

			if (!zoneClean && (!OwnsSkipTerrainBuilders || !OwnsNoBiomes))
			{
				RetryAuthorized = false;
			}
			SetRepair(Detail + "; exact cleanup could not be proved: "
				+ Nonempty(cleanupFailure, "the target retained unresolved inheritance artifacts"));
			string terminalFailure;
			if (!TryPrepareSafeTerminalZone(Zone, ExactOwnedZone, out terminalFailure))
			{
				SetRepair(FailureDetail + "; safe hidden terrain validation failed: " + terminalFailure);
				AnnounceFailure();
				return false;
			}
			AnnounceFailure();
			// Never repeat false until Qud force-ignores the custom builder. The success-aware
			// finder that follows no-ops in RepairRequired, leaving this passable zone hidden.
			return true;
		}

		private bool TryPrepareSafeTerminalZone(Zone Zone, bool ExactOwnedZone,
			out string Failure)
		{
			Failure = "";
			string tile;
			string color;
			string render;
			if (Zone == null || Zone.ZoneID != TargetZoneId
				|| !TryGroundPaint(TargetZoneId, out tile, out color, out render, out Failure))
			{
				Failure = Nonempty(Failure, "the exact fallback zone or terrain paint was unavailable");
				return false;
			}
			if (ExactOwnedZone)
			{
				List<GameObject> objects = Zone.GetObjects();
				for (int i = objects.Count - 1; i >= 0; i--)
				{
					objects[i].Obliterate(null, Silent: true);
				}
				Zone.RemoveZoneProperty(KingdomInheritEngine.ZoneMarkerProperty);
			}
			for (int y = 0; y < Zone.Height; y++)
			{
				for (int x = 0; x < Zone.Width; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null)
					{
						Failure = "the fallback terrain contained a missing cell";
						return false;
					}
					// ExactOwnedZone follows a pristine proof and may clear its own partial placement.
					// Foreign-conflict fallback preserves nonblank mod paint and fills only suppressed
					// blank terrain.
					if (ExactOwnedZone || string.IsNullOrEmpty(cell.PaintTile)) cell.PaintTile = tile;
					if (ExactOwnedZone || string.IsNullOrEmpty(cell.PaintTileColor))
						cell.PaintTileColor = color;
					if (ExactOwnedZone || string.IsNullOrEmpty(cell.PaintColorString))
						cell.PaintColorString = color;
					if (ExactOwnedZone || string.IsNullOrEmpty(cell.PaintRenderString))
						cell.PaintRenderString = render;
				}
			}
			Zone.ClearReachableMap();
			int reachable = Zone.BuildReachableMap(0, 0);
			if (!KingdomInheritanceStateRules.CanTerminalizeHiddenFallback(reachable, 0))
			{
				Failure = "the hidden fallback had only " + reachable.ToString()
					+ " cells reachable from its entry";
				return false;
			}
			return true;
		}

		internal bool TryCleanControlledRetry(Zone Zone, out string Failure)
		{
			Failure = "";
			try
			{
				RemoveLocationFinders(Zone);
				return TryQuarantineExact(Zone, out Failure);
			}
			catch (Exception ex)
			{
				Failure = "exact retry cleanup threw: " + ex.Message;
				return false;
			}
		}

		internal bool TryValidateAppliedZone(Zone Zone, out string Failure)
		{
			Failure = "";
			try
			{
				if (Zone == null || Zone.ZoneID != TargetZoneId)
				{
					Failure = "the exact applied target zone was unavailable";
					return false;
				}
				Zone.ClearReachableMap();
				int reachable = Zone.BuildReachableMap(0, 0);
				if (!KingdomInheritanceStateRules.MeetsReachability(reachable))
				{
					Failure = "the reconstructed site left only " + reachable.ToString()
						+ " cells reachable from its entry";
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "post-application reachability validation threw: " + ex.Message;
				return false;
			}
		}

		private bool TryRecoverUnvalidatedApplication(Zone Zone, KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved, out string Failure)
		{
			Failure = "";
			string expected;
			string marker = Zone == null ? ""
				: Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			if (!KingdomInheritanceStateRules.TryComposeApplicationMarker(Legacy, Reserved,
					TargetZoneId, KingdomInheritEngine.ReconstructionVersion, out expected)
				|| !KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
					ApplyStatusValue, ApplyFaultValue, RetryAuthorized, ApplicationMarker,
					marker, expected))
			{
				Failure = "the marker was not an exact retry-authorized unvalidated application";
				return false;
			}
			if (!TryCleanControlledRetry(Zone, out Failure))
			{
				return false;
			}
			ApplicationMarker = "";
			return true;
		}

	}
}
