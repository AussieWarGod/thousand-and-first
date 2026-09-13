using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomTeardownNativeChecks
	{
		/// <summary>review-teardown-run25-staked.md: Case.Rect is only the plot's bounding
		/// rectangle; the production stamper's occupant refusal
		/// (KingdomArchitectureStamper.Verification.cs CanInsert, "layout slot" in its own
		/// diagnostic) names one exact authored placement, not any cell of the bounding rect.
		/// Decodes a commissioned job's OWN payload via the same canonical-receipt decode chain
		/// production itself uses to resolve architecture (KingdomPlots.TryDecodePlotPayload -&gt;
		/// KingdomArchitectureRuntime.TryDecode, the same pair Preflight/ResolveArchitecture read
		/// from) -- never re-derived, never guessed from the rect -- and turns every authored
		/// placement into a WORLD cell via the same pure pose transform TryWorldFootprint itself
		/// uses (KingdomArchitectureRules.TryToWorld). Duplicate cells collapse. Same
		/// engine-touching compile surface as the rest of this Case shard (Case.cs/Telemetry.cs);
		/// split into its own file only to keep Case.cs under the harness line cap.</summary>
		internal static bool TryResolvePlacementCells(string Payload,
			out List<(int X, int Y)> Cells, out string Failure)
		{
			Cells = new List<(int, int)>();
			if (!KingdomPlots.TryDecodePlotPayload(Payload, out KingdomPlotRules.PlotRect rect,
				out string skinKey, out KingdomArchitectureIntent architecture, out bool legacy,
				out Failure)) return false;
			if (!KingdomArchitectureRuntime.TryDecode(architecture,
				out ArchitectureLayoutSnapshot snapshot, out Failure)) return false;
			HashSet<(int, int)> seen = new HashSet<(int, int)>();
			foreach (ArchitecturePlacement placement in snapshot.Placements)
			{
				if (!KingdomArchitectureRules.TryToWorld(rect.X1, rect.Y1, snapshot.Width,
					snapshot.Height, snapshot.Facing, placement.X, placement.Y,
					out int worldX, out int worldY)) continue;
				if (seen.Add((worldX, worldY))) Cells.Add((worldX, worldY));
			}
			return true;
		}
	}
}
