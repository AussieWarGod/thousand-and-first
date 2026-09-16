using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private Cell ProveChainRetainedGround(KingdomArchitectureIntent Before,
				KingdomArchitectureIntent Successor, KingdomMaterialDebitCost Claim)
			{
				Require(KingdomArchitectureRuntime.TryDecode(Before, out var before, out string failure), failure);
				Require(KingdomArchitectureRuntime.TryDecode(Successor, out var after, out failure), failure);
				Require(KingdomArchitectureRules.TryBuildDelta(before, after, out var delta, out failure), failure);
				Require(KingdomArchitectureStamper.TryPlacementPassability(Before, Zone,
					out var beforeSlots, out failure), failure);
				Require(KingdomArchitectureStamper.TryPlacementPassability(Successor, Zone,
					out var afterSlots, out failure), failure);
				Cell retained = null;
				foreach (var change in delta.Cells)
				{
					if (change.After == null || !KingdomArchitectureRules.IsClaimed(change.After.Claim)) continue;
					Require(KingdomArchitectureRuntime.TryWorldCell(after, Successor.Rect, change.After,
						out int x, out int y, out failure), failure);
					int packed = y * Zone.Width + x;
					if (!Before.Rect.Contains(x, y)
						|| !beforeSlots.TryGetValue(packed, out var oldPass) || oldPass != ArchitecturePassability.Walkable
						|| !afterSlots.TryGetValue(packed, out var newPass) || newPass != ArchitecturePassability.Walkable)
						continue;
					Cell candidate = Zone.GetCell(x, y);
					if (candidate == null || !candidate.IsPassable() || candidate.HasOpenLiquidVolume()) continue;
					bool occupied = false;
					foreach (var item in candidate.GetObjects())
						if (!GameObject.Validate(item) || item.IsCreature || item.IsPlayer()) occupied = true;
					if (!occupied) { retained = candidate; break; }
				}
				Require(retained != null, "retained-ground probe lacks an impacted walkable predecessor cell");
				ProbeChainBody(FixtureResidents[0], retained, Successor, Claim, true, false);
				ProbeChainBody(XRL.The.Player, retained, Successor, Claim, true, false);
				GameObject stranger = Create("NPC");
				GameObject wall = Create("r_KingdomStructureMudWall");
				try
				{
					Require(stranger.IsCreature && !KingdomCitizenship.BelongsTo(System, stranger),
						"retained-ground stranger is not foreign");
					ProbeChainBody(stranger, retained, Successor, Claim, true, false);
					Require(ReferenceEquals(retained.AddObject(wall, NoStack: true), wall),
						"retained-ground wall substituted its object");
					RequireChainUpgradePreflight(Successor, Claim, false,
						"foreign or protected state occupies authored successor ground at "
						+ retained.X + "," + retained.Y);
					Require(wall.CurrentCell == retained, "retained-ground preflight moved the foreign wall");
				}
				finally
				{
					stranger.Obliterate(null, Silent: true);
					wall.Obliterate(null, Silent: true);
				}
				return retained;
			}
		}
	}
}
