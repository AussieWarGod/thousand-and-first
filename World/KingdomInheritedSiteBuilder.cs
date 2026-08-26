using System;
using System.Collections.Generic;
using Qud.API;
using ThousandAndFirst;
using XRL;
using XRL.World;

namespace XRL.World.ZoneBuilders
{
	/// <summary>
	/// Builds one inheritance-owned special site on a validated pristine surface zone. The short
	/// class name is persisted by ZoneManager, so this namespace is an exact engine contract.
	/// </summary>
	public sealed partial class KingdomInheritedSiteBuilder
	{
		public string LegacyId;

		public string TargetGameId;

		public string TargetZoneId;

		public int ReconstructionVersion;

		public bool BuildZone(Zone Z)
		{
			KingdomInheritanceState state = KingdomInheritanceState.Instance;
			bool exactOwnedStage = false;
			try
			{
				KingdomSealRecord legacy;
				KingdomSealReceipt receipt;
				string failure = "";
				if (state == null || !state.TryBuilderPayload(LegacyId, TargetGameId,
					TargetZoneId, ReconstructionVersion, out legacy, out receipt, out failure))
				{
					if (state != null)
					{
						state.RecordBuilderFailure(Nonempty(failure,
							"the target singleton was not available to its persisted builder"));
						return state.PrepareVanillaFallback(Z,
							"the persisted inherited builder lost its exact payload");
					}
					return EmergencyFallback(Z);
				}

				if (!state.HasOnlyOwnedBuilders(TargetZoneId, LegacyId, TargetGameId,
					ReconstructionVersion, out failure)
					|| The.ZoneManager.CountPartsFor(TargetZoneId) != 0)
				{
					KingdomInheritApplyResult refused = new KingdomInheritApplyResult(
						KingdomInheritApplyStatus.Refused,
						KingdomInheritApplyFault.ApplicationConflict,
						Nonempty(failure, "the target acquired a foreign persistent zone part"),
						"", 0, false);
					state.RecordApplyResult(refused, WillRetry: false, DuringZoneBuild: true);
					return state.PrepareVanillaFallback(Z,
						"the inherited target was no longer exclusively owned");
				}

				string tile;
				string color;
				string render;
				if (!state.TryGroundPaint(TargetZoneId, out tile, out color, out render, out failure)
					|| !IsPristine(Z, out failure))
				{
					KingdomInheritApplyResult refused = new KingdomInheritApplyResult(
						KingdomInheritApplyStatus.Refused, KingdomInheritApplyFault.ApplicationConflict,
						failure, "", 0, false);
					state.RecordApplyResult(refused, WillRetry: false, DuringZoneBuild: true);
					return state.PrepareVanillaFallback(Z,
						"the inherited target was not a pristine fresh zone");
				}

				if (!KingdomInheritanceStateRules.CanAuthorizeDirectRepair(ExactBuilders: true,
					ZoneParts: The.ZoneManager.CountPartsFor(TargetZoneId), Pristine: true))
				{
					return state.PrepareVanillaFallback(Z,
						"the inherited target lost direct-repair provenance before painting");
				}
				state.AuthorizeExactOwnedRepair();
				exactOwnedStage = true;
				PaintGround(Z, tile, color, render);
				KingdomInheritApplyResult result = KingdomInheritEngine.Apply(legacy, receipt,
					TargetZoneId, Z);
				bool retry = false;
				if (result != null && (result.Status == KingdomInheritApplyStatus.Applied
					|| result.Status == KingdomInheritApplyStatus.AlreadyApplied))
				{
					string reachFailure;
					if (!state.TryValidateAppliedZone(Z, out reachFailure))
					{
						result = new KingdomInheritApplyResult(KingdomInheritApplyStatus.Failed,
							KingdomInheritApplyFault.PartialApplication,
							reachFailure,
							result.ApplicationMarker, result.PlacedCount, result.FreshEmptyVerified);
					}
				}
				if (result != null && KingdomInheritanceStateRules.ShouldRetryBuild(
					result.Status, Z.BuildTries, ExactCleanupSucceeded: true))
				{
					string cleanupFailure;
					bool cleaned = state.TryCleanControlledRetry(Z, out cleanupFailure);
					retry = KingdomInheritanceStateRules.ShouldRetryBuild(result.Status,
						Z.BuildTries, cleaned);
					if (!cleaned)
					{
						result = new KingdomInheritApplyResult(KingdomInheritApplyStatus.Failed,
							KingdomInheritApplyFault.PartialApplication,
							"the one controlled retry could not clean exact owned artifacts: "
								+ cleanupFailure, result.ApplicationMarker, result.PlacedCount,
							result.FreshEmptyVerified);
					}
				}
				state.RecordApplyResult(result, retry, DuringZoneBuild: true);
				if (retry)
				{
					return false;
				}
				if (result != null && (result.Status == KingdomInheritApplyStatus.Applied
					|| result.Status == KingdomInheritApplyStatus.AlreadyApplied))
				{
					try
					{
						state.AdoptExternalCommittedIfKnown(Z);
					}
					catch (Exception ex)
					{
						// Exact application is authoritative. Never enter destructive fallback for
						// optional adoption/discovery failure after that point.
						try
						{
							state.RecordDiscoveryFailure(
								"post-application adoption threw: " + ex.Message);
						}
						catch (Exception)
						{
						}
					}
					return true;
				}
				return state.PrepareVanillaFallback(Z,
					"the inherited-site transaction did not reach exact success",
					ExactOwnedZone: true);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst inherited-site builder", ex);
				if (state != null)
				{
					state.RecordBuilderFailure(ex.Message);
					return state.PrepareVanillaFallback(Z,
						"the inherited-site builder threw before exact success",
						ExactOwnedZone: exactOwnedStage);
				}
				return EmergencyFallback(Z);
			}
		}

		private static bool IsPristine(Zone Z, out string Failure)
		{
			Failure = "";
			if (Z == null || Z.ZoneID == null || Z.Width != KingdomInheritRules.TargetWidth
				|| Z.Height != KingdomInheritRules.TargetHeight || Z.GetObjects().Count != 0)
			{
				Failure = "the fresh target already carries objects or has the wrong dimensions";
				return false;
			}
			for (int y = 0; y < Z.Height; y++)
			{
				for (int x = 0; x < Z.Width; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null || !string.IsNullOrEmpty(cell.PaintTile)
						|| !string.IsNullOrEmpty(cell.PaintTileColor)
						|| !string.IsNullOrEmpty(cell.PaintColorString)
						|| !string.IsNullOrEmpty(cell.PaintRenderString))
					{
						Failure = "the fresh target already carries foreign cell paint";
						return false;
					}
				}
			}
			return true;
		}

		private static void PaintGround(Zone Z, string Tile, string Color, string Render)
		{
			for (int y = 0; y < Z.Height; y++)
			{
				for (int x = 0; x < Z.Width; x++)
				{
					Cell cell = Z.GetCell(x, y);
					cell.PaintTile = Tile;
					cell.PaintTileColor = Color;
					cell.PaintColorString = Color;
					cell.PaintRenderString = Render;
				}
			}
			// Qud 2.0.211.51 Sky is the z5-z9 builder. A Joppa z10 surface terrain does not
			// receive Air (whose stairs/chasm material would also violate inheritance preflight).
			// ZoneManager.GenerateZone adds DaylightWidget after builders when Zone.IsOutside().
			Z.ClearReachableMap();
			Z.BuildReachableMap(0, 0);
		}
	}
}
