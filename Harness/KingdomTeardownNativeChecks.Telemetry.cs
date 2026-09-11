using System;
using System.Collections.Generic;
using System.Text;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomTeardownNativeChecks
	{
		private sealed partial class Case
		{
			/// <summary>review-teardown-run15-neverbuilt.md finding 1: the job row and its tick
			/// counter were read once at Start and never again, so an awaiting-built stall was
			/// undiagnosable. Re-reads the raising root's own production properties every Check
			/// -- required/remaining/last-worked ticks (KingdomPlots.PlotWork*Property), the raw
			/// prior-interval witness (PlotWorkWindowProperty), and the one-gang allocator's own
			/// presence (KingdomConstructionPresence.Selected/Hands/EffectivenessProperty) -- plus
			/// the live construction registry row's Phase by re-TryFind-ing the job's own id
			/// (JobId), never re-using the Start-time snapshot.
			/// <para>
			/// review-teardown-run20-stuckworking.md: run 15's telemetry still could not have
			/// named run 20's real cause (labour fully paid, physical stage silently stuck at
			/// Cleared because a living occupant refused the ground layer). Added:
			/// stage-applied/stage-target (r_KingdomPlotWorks.StageApplied vs the terminal
			/// KingdomPlotRules.PlotStage.Done every design targets), built= (the same
			/// IsFunctionallyBuilt predicate the caller just tested), completed-tick=
			/// (PlotWorkCompletedTickProperty), apply-failure= (disclosed as "unread": the real
			/// refusal is KingdomLog.Log only, dev-option gated, never stored on any object this
			/// harness can read), and occupants= (creature/player ids standing on the raising's
			/// own footprint, via the shared OccupantIdsOn). Read-only; asserts nothing.
			/// </para>
			/// </summary>
			private string Telemetry(GameObject Root)
			{
				long required = ReadLong(Root, KingdomPlots.PlotWorkRequiredProperty);
				long remaining = ReadLong(Root, KingdomPlots.PlotWorkRemainingProperty);
				long lastWorked = ReadLong(Root, KingdomPlots.PlotWorkLastTickProperty);
				long completedTick = ReadLong(Root, KingdomPlots.PlotWorkCompletedTickProperty);
				string window = Root == null ? "" : (Root.GetStringProperty(
					KingdomPlots.PlotWorkWindowProperty) ?? "");
				bool selected = Root != null
					&& Root.GetIntProperty(KingdomConstructionPresence.SelectedProperty) == 1;
				int hands = Root == null ? 0
					: Root.GetIntProperty(KingdomConstructionPresence.HandsProperty);
				int effectiveness = Root == null ? 0
					: Root.GetIntProperty(KingdomConstructionPresence.EffectivenessProperty);
				string jobPhase = "unread";
				if (!string.IsNullOrEmpty(JobId)
					&& KingdomConstruction.TryFind(JobId, out KingdomConstructionJob row) && row != null)
					jobPhase = row.Phase.ToString();
				XRL.World.Parts.r_KingdomPlotWorks part = Root?.GetPart<XRL.World.Parts.r_KingdomPlotWorks>();
				string stageApplied = part == null ? "unread"
					: ((KingdomPlotRules.PlotStage)part.StageApplied).ToString();
				bool built = Root != null && KingdomUpgrade.IsFunctionallyBuilt(Root);
				// review-teardown-run25-staked.md: swept over the authored placement cells (the
				// exact "layout slot" set the stamper's occupant refusal names), never just the
				// bounding rect -- a second occupant/plot outside the rect is no longer invisible.
				string occupants = PlacementCells.Count > 0 ? OccupantIdsOn(Zone, PlacementCells)
					: (HasRect ? OccupantIdsOn(Zone, Rect) : "unread");
				return new StringBuilder()
					.Append(" required=").Append(required)
					.Append(" remaining=").Append(remaining)
					.Append(" last-worked=").Append(lastWorked)
					.Append(" window=").Append(KingdomScenarioRules.Bounded(window))
					.Append(" presence=selected:").Append(selected ? 1 : 0)
					.Append(",hands:").Append(hands).Append(",effectiveness:").Append(effectiveness)
					.Append(" job-phase=").Append(jobPhase)
					.Append(" stage-applied=").Append(stageApplied)
					.Append(" stage-target=").Append(KingdomPlotRules.PlotStage.Done)
					.Append(" built=").Append(built)
					.Append(" completed-tick=").Append(completedTick)
					.Append(" apply-failure=unread")
					.Append(" occupants=").Append(occupants)
					.ToString();
			}

			private static long ReadLong(GameObject Root, string Property)
			{
				if (Root == null) return -1L;
				long value;
				return long.TryParse(Root.GetStringProperty(Property), out value) ? value : -1L;
			}
		}

		/// <summary>review-teardown-run20-stuckworking.md: creature/player ids (never any other
		/// object) standing on any cell of Rect, comma-joined, or "none". Read-only; used both to
		/// journal a raising's footprint and to decide which of THIS fixture's own crew bodies
		/// (never a production resident or the player) Frame.KeepCrewOutsideRaisings must
		/// relocate.</summary>
		internal static string OccupantIdsOn(Zone Zone, KingdomPlotRules.PlotRect Rect)
		{
			StringBuilder ids = null;
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Zone?.GetCell(x, y);
					if (cell == null) continue;
					foreach (GameObject item in cell.GetObjects())
					{
						if (!GameObject.Validate(item) || !(item.IsCreature || item.IsPlayer())) continue;
						if (ids == null) ids = new StringBuilder();
						else ids.Append(',');
						ids.Append(item.IDIfAssigned ?? "unassigned");
					}
				}
			return ids == null ? "none" : ids.ToString();
		}

		/// <summary>review-teardown-run25-staked.md: the same creature/player-id sweep, but over
		/// an exact set of authored placement cells (Case.PlacementCells) instead of every cell
		/// of a bounding rect -- the narrower, correct set the production stamper's own occupant
		/// refusal checks. Read-only; duplicate cells collapse (callers may pass cells pooled
		/// across more than one raising).</summary>
		internal static string OccupantIdsOn(Zone Zone, List<(int X, int Y)> Cells)
		{
			StringBuilder ids = null;
			foreach ((int X, int Y) cell in Cells)
			{
				Cell worldCell = Zone?.GetCell(cell.X, cell.Y);
				if (worldCell == null) continue;
				foreach (GameObject item in worldCell.GetObjects())
				{
					if (!GameObject.Validate(item) || !(item.IsCreature || item.IsPlayer())) continue;
					if (ids == null) ids = new StringBuilder();
					else ids.Append(',');
					ids.Append(item.IDIfAssigned ?? "unassigned");
				}
			}
			return ids == null ? "none" : ids.ToString();
		}
	}
}
