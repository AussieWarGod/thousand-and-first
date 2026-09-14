using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private void ProveChainRenovationOccupancy()
			{
				long tick = Game.TimeTicks;
				int water = Census().StoredWater;
				Require(KingdomConstruction.TryRead(out var jobs, out string failure), failure);
				int count = jobs.Count;
				Require(KingdomArchitectureRuntime.TryRead(ChainHeart, out var before, out failure), failure);
				Require(KingdomArchitectureRuntime.TryPrepareSuccessorForUpgrade(System, Zone, ChainHeart,
					before, ChainTo, out var successor, out failure), failure);
				Require(KingdomArchitectureStamper.TryNewBlockingCells(Zone, before, successor,
					out var slots, out failure), failure);
				Cell blocked = null;
				foreach (var slot in slots)
				{
					int x = slot.Key % Zone.Width, y = slot.Key / Zone.Width;
					Cell cell = Zone.GetCell(x, y);
					if (!before.Rect.Contains(x, y) || cell == null || !cell.IsPassable()
						|| cell.HasOpenLiquidVolume()) continue;
					bool occupied = false;
					foreach (var item in cell.GetObjects())
						if (GameObject.Validate(item) && (item.IsCreature || item.IsPlayer())) occupied = true;
					if (!occupied) { blocked = cell; break; }
				}
				Require(blocked != null, "court renovation probe lacks a retained floor becoming a wall");
				var claim = new KingdomMaterialDebitCost(KingdomMaterials.UpgradeCostFor(ChainFrom));
				ProbeChainBody(FixtureResidents[0], blocked, successor, claim, true, true, true);
				ProbeChainBody(The.Player, blocked, successor, claim, false, false, true);
				GameObject stranger = Create("NPC");
				try
				{
					Require(stranger.IsCreature && !KingdomCitizenship.BelongsTo(System, stranger),
						"renovation stranger is not foreign");
					ProbeChainBody(stranger, blocked, successor, claim, false, false, true);
				}
				finally { stranger.Obliterate(null, Silent: true); }
				RequireChainUpgradePreflight(successor, claim, true, null);
				Require(Game.TimeTicks == tick && Census().StoredWater == water && !KingdomSurvey.HasBoundPass,
					"renovation probe changed time, water or survey binding");
				Require(KingdomConstruction.TryRead(out jobs, out failure) && jobs.Count == count, failure);
				foreach (var unit in ChainSupplied)
					Require(GameObject.Validate(unit) && ReferenceEquals(unit.InInventory, ChainStore),
						"renovation preflight spent a supplied material");
				Require(KingdomArchitectureRuntime.TryRead(ChainHeart, out var retained, out failure)
					&& retained.EncodedSnapshot == before.EncodedSnapshot
					&& KingdomArchitectureStamper.TryVerifyComplete(ChainHeart, Zone, out failure), failure);
				RequireChainCustody(); RequireChainSupport();
				Require(KingdomScenarioJournal.Append("camp-heart-chain-renovation", true,
					"retained-new-wall=true; resident-preflight=true; scoped-assessment=true; strict-refused=true"
					+ "; founder-protected=true; stranger-protected=true; restored=true; no-debit=true"
					+ "; blocked=" + blocked.X + "," + blocked.Y) == null, "renovation journal unavailable");
			}
		}
	}
}
