using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private void ProveChainEnvelopeOccupancy()
			{
				Require(!KingdomSurvey.HasBoundPass, "envelope probe found an outstanding survey");
				long tick = Game.TimeTicks;
				var beforeSurvey = Census();
				int water = beforeSurvey.StoredWater;
				Require(KingdomConstruction.TryRead(out var jobs, out string failure), failure);
				int jobCount = jobs.Count;
				Require(KingdomArchitectureRuntime.TryRead(ChainHeart, out var before, out failure), failure);
				Require(KingdomArchitectureRuntime.TryPrepareSuccessorForUpgrade(System, Zone, ChainHeart,
					before, ChainTo, out var successor, out failure), failure);
				var claim = new KingdomMaterialDebitCost(KingdomMaterials.UpgradeCostFor(ChainFrom));
				Cell retainedCell = ProveChainRetainedGround(before, successor, claim);
				Cell blocked = ChainEnvelopeProbeCell(before, successor, true);
				Cell walkable = ChainEnvelopeProbeCell(before, successor, false);
				Require(blocked != null && walkable != null, "envelope probe lacks clear annexed slots");
				RequireChainUpgradePreflight(successor, claim, true, null);
				GameObject resident = FixtureResidents[0];
				Require(GameObject.Validate(resident) && resident.IsAlive && resident.CurrentZone == Zone
					&& KingdomCitizenship.BelongsTo(System, resident), "envelope probe resident absent");
				ProbeChainBody(resident, blocked, successor, claim, true, true);
				ProbeChainBody(The.Player, blocked, successor, claim, false, false);
				ProbeChainBody(The.Player, walkable, successor, claim, true, false);
				GameObject stranger = Create("NPC");
				GameObject wall = Create("r_KingdomStructureMudWall");
				try
				{
					Require(stranger.IsCreature && !KingdomCitizenship.BelongsTo(System, stranger),
						"envelope stranger is not foreign");
					ProbeChainBody(stranger, blocked, successor, claim, false, false);
					ProbeChainBody(stranger, walkable, successor, claim, true, false);
					Require(ReferenceEquals(walkable.AddObject(wall, NoStack: true), wall),
						"envelope wall placement substituted its object");
					RequireChainUpgradePreflight(successor, claim, false,
						"occupies plot-envelope growth ground at " + walkable.X + "," + walkable.Y);
					Require(wall.CurrentCell == walkable, "preflight moved a foreign wall");
				}
				finally
				{
					stranger.Obliterate(null, Silent: true);
					wall.Obliterate(null, Silent: true);
				}
				RequireChainUpgradePreflight(successor, claim, true, null);
				Require(Game.TimeTicks == tick && ReferenceEquals(The.Game, Game)
					&& !KingdomSurvey.HasBoundPass && Census().StoredWater == water,
					"read-only envelope probe changed clock, water or survey binding");
				Require(KingdomConstruction.TryRead(out jobs, out failure) && jobs.Count == jobCount,
					"read-only envelope probe created or removed paid work: " + failure);
				foreach (var unit in ChainSupplied)
					Require(GameObject.Validate(unit) && ReferenceEquals(unit.InInventory, ChainStore),
						"read-only envelope probe spent a supplied material");
				Require(KingdomArchitectureRuntime.TryRead(ChainHeart, out var retained, out failure)
					&& retained.EncodedSnapshot == before.EncodedSnapshot
					&& KingdomArchitectureStamper.TryVerifyComplete(ChainHeart, Zone, out failure), failure);
				RequireChainCustody();
				RequireChainSupport();
				Require(KingdomScenarioJournal.Append("camp-heart-chain-occupancy", true,
					"synthetic-placement=true; retained-walkable=true; scoped-assessment=true; resident-blocked-preflight=true; strict-blocked-refused=true"
					+ "; retained-resident=true; retained-founder=true; retained-stranger=true; retained-foreign-wall-refused=true"
					+ "; founder-blocked-refused=true; founder-walkable=true; stranger-blocked-refused=true"
					+ "; stranger-walkable=true; foreign-wall-refused=true; restored=true; no-debit=true"
					+ "; blocked=" + blocked.X + "," + blocked.Y
					+ "; retained=" + retainedCell.X + "," + retainedCell.Y
					+ "; walkable=" + walkable.X + "," + walkable.Y) == null,
					"envelope probe journal unavailable");
			}

			private Cell ChainEnvelopeProbeCell(KingdomArchitectureIntent Before,
				KingdomArchitectureIntent Successor, bool Blocked)
			{
				Require(KingdomArchitectureStamper.TryPlacementPassability(Successor, Zone,
					out var slots, out string failure), failure);
				foreach (var slot in slots)
				{
					int x = slot.Key % Zone.Width, y = slot.Key / Zone.Width;
					if (Before.Rect.Contains(x, y)
						|| (Blocked ? slot.Value != ArchitecturePassability.Blocked
							: slot.Value != ArchitecturePassability.Walkable)) continue;
					if (!Blocked && (x == Successor.Rect.X1 || x == Successor.Rect.X2
						|| y == Successor.Rect.Y1 || y == Successor.Rect.Y2)) continue;
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || !cell.IsPassable() || cell.HasOpenLiquidVolume()) continue;
					bool bare = true;
					foreach (var item in cell.GetObjects())
						if (!GameObject.Validate(item) || item.IsCreature || item.IsPlayer()
							|| KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare) bare = false;
					if (bare) return cell;
				}
				return null;
			}

			private void ProbeChainBody(GameObject Body, Cell At, KingdomArchitectureIntent Successor,
				KingdomMaterialDebitCost Claim, bool Accepted, bool Movable, bool Renovation = false)
			{
				Cell origin = Body.CurrentCell;
				string id = Body.ID;
				try
				{
					origin?.RemoveObject(Body);
					Require(ReferenceEquals(At.AddObject(Body, NoStack: true), Body)
						&& Body.CurrentCell == At && (Body.IsCreature || Body.IsPlayer()),
						"envelope body probe failed exact placement");
					RequireChainUpgradePreflight(Successor, Claim, Accepted, Accepted ? null
						: "a living occupant stands on " + (Renovation ? "renovation" : "plot-envelope growth")
							+ " ground at " + At.X + "," + At.Y);
					if (Movable)
					{
						Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope,
							out string failure), failure);
						using (scope)
						{
							Require(KingdomPlots.IsMovableEnvelopeOccupant(System, Zone, Body),
								"envelope resident probe did not reach movement authority");
							Require(KingdomArchitectureRuntime.TryRead(ChainHeart, out var before, out failure), failure);
							bool strict = Renovation
								? KingdomArchitectureStamper.TryProveRenovationOccupants(System, Zone, before,
									Successor, false, out _, out failure)
								: KingdomArchitectureStamper.TryProveEnvelopeGrowth(System, Zone,
									ChainHeart, null, Successor, false, out failure);
							Require(!strict && failure != null && failure.StartsWith("a living occupant stands on ",
									StringComparison.Ordinal), "strict envelope admitted an uncleared body: " + failure);
						}
						var assessment = AssessChain(out string context);
						Require(KingdomUpgradeRules.IsReady(assessment.Verdict),
							"chain assessment refused its movable resident: " + assessment.Reason + "; " + context);
					}
					Require(Body.CurrentCell == At && Body.IDIfAssigned == id,
						"read-only envelope preflight moved or replaced its body");
				}
				finally
				{
					Body.CurrentCell?.RemoveObject(Body);
					if (origin != null)
						Require(ReferenceEquals(origin.AddObject(Body, NoStack: true), Body)
							&& Body.CurrentCell == origin, "envelope probe could not restore its body");
				}
				Require(Body.CurrentCell == origin && Body.IDIfAssigned == id
					&& !KingdomSurvey.HasBoundPass, "envelope body restoration or survey disposal failed");
			}

			private void RequireChainUpgradePreflight(KingdomArchitectureIntent Successor,
				KingdomMaterialDebitCost Claim, bool Expected, string Refusal)
			{
				Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope,
					out string failure), failure);
				using (scope)
				{
					bool accepted = KingdomArchitectureStamper.TryPreflightUpgrade(System, Zone,
						ChainHeart, Successor, Claim, out var delta, out failure);
					Require(accepted == Expected && (Expected ? delta != null
						: delta == null && failure != null && failure.Contains(Refusal)),
						"envelope preflight expected=" + Expected + "; actual=" + accepted + "; reason=" + failure);
				}
			}
		}
	}
}
