using System;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			// Synthetic probe bodies, disclosed and removed before engine turns. Never changes
			// a completed layout or removes foreign ground to obtain a usable probe cell.
			private void ProveStructuralOccupancy()
			{
				Require(KingdomArchitectureRuntime.TryRead(Heart, out var intent, out string failure),
					failure ?? "taf-camp-occupancy-layout-absent");
				Require(KingdomArchitectureRuntime.TryDecode(intent, out var snapshot, out failure),
					failure ?? "taf-camp-occupancy-layout-absent");
				Cell probe = null;
				foreach (var state in snapshot.Cells)
				{
					if (!KingdomArchitectureRules.IsClaimed(state.Claim)
						|| state.Passability != ArchitecturePassability.Walkable) continue;
					Require(KingdomArchitectureRuntime.TryWorldCell(snapshot, intent.Rect, state,
						out int x, out int y, out failure), failure);
					Cell cell = Zone.GetCell(x, y);
					if (cell != null && cell.IsPassable() && !cell.HasObjectWithPart("Door"))
					{ probe = cell; break; }
				}
				Require(probe != null, "taf-camp-occupancy-no-unoccupied-authored-walk-cell");
				Require(KingdomArchitectureStamper.TryVerifyComplete(Heart, Zone, out failure), failure);
				GameObject occupant = Create("NPC");
				try
				{
					Require(ReferenceEquals(probe.AddObject(occupant, NoStack: true), occupant)
						&& occupant.IsCombatObject() && !probe.IsPassable()
						&& probe.IsPassable(null, IncludeCombatObjects: false),
						"taf-camp-occupancy-positive-unreached");
					Require(KingdomArchitectureStamper.TryVerifyComplete(Heart, Zone, out failure),
						"taf-camp-occupancy-misclassified: " + failure);
				}
				finally { occupant.Obliterate(null, Silent: true); }
				GameObject wall = Create("r_KingdomStructureMudWall");
				try
				{
					Require(ReferenceEquals(probe.AddObject(wall, NoStack: true), wall)
						&& !probe.IsPassable(null, IncludeCombatObjects: false),
						"taf-camp-solid-negative-unreached");
					Require(!KingdomArchitectureStamper.TryVerifyComplete(Heart, Zone, out failure)
						&& failure != null && failure.StartsWith("concrete authored walk cell is blocked at ",
							StringComparison.Ordinal), "taf-camp-solid-negative-wrong: " + failure);
				}
				finally { wall.Obliterate(null, Silent: true); }
				Require(probe.IsPassable()
					&& KingdomArchitectureStamper.TryVerifyComplete(Heart, Zone, out failure),
					"taf-camp-occupancy-cleanup-unproved: " + failure);
				Evidence.Append("\nsynthetic-occupancy-probes=true; transient-body-accepted=true")
					.Append("; solid-wall-refused=true; probe-cleanup-proved=true; cell=")
					.Append(probe.X).Append(',').Append(probe.Y);
			}
		}
	}
}
