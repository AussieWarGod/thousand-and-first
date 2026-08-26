using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomCommission
	{
		private const uint PlacementEventKind = 5U;
		private const uint PlacementDraw = 0U;

		/// <summary>Founder-facing refusal for the authored stage gate. Commit paths call this
		/// independently of menu visibility: a hidden row is not an authorization boundary.</summary>
		public static string StageRefusal(KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			if (System == null || Entry == null || System.Stage >= Entry.MinStage) return null;
			string seat = string.IsNullOrEmpty(System.SeatName) ? "The settlement" : System.SeatName;
			return seat + " has not yet grown into " + KingdomUpgradeRules.StageWord(Entry.MinStage)
				+ ", when the " + (Entry.Name ?? "work") + " can be raised.";
		}

		public static bool Commission(KingdomSystem System, string Key, out string Failure)
		{
			return Commission(System, Key, null, out Failure);
		}

		/// <summary>
		/// Issues one commission on the ground the founder is standing on.
		/// </summary>
		/// <param name="System">The realm; must be founded and must hold this ground.</param>
		/// <param name="Key">Registry key of the design.</param>
		/// <param name="SkinKey">Key of the design's own skin the founder chose, or null for the
		/// design's unmodified look. A key the design does not carry is ignored.</param>
		/// <param name="Failure">A founder-facing sentence when this returns false; null otherwise.
		/// Every refusal names what would lift it.</param>
		/// <returns>True once scaffolding is standing and the water is spent.</returns>
		public static bool Commission(KingdomSystem System, string Key, string SkinKey, out string Failure)
		{
			return Commission(System, Key, SkinKey, KingdomPlotRules.PlotSize.None, out Failure);
		}

		/// <summary>
		/// Issues one commission on the ground the founder is standing on, staking the tier of plot
		/// they chose. Identical in every check to the overload above: the tier only ever widens the
		/// envelope, never the building, and a design that is not a plot ignores it entirely.
		/// </summary>
		/// <param name="Stake">The tier to lay, from <c>KingdomPlots.StakeableSizes</c>.
		/// <c>PlotSize.None</c> stakes the design's own.</param>
		public static bool Commission(KingdomSystem System, string Key, string SkinKey, KingdomPlotRules.PlotSize Stake, out string Failure)
		{
			return Commission(System, Key, SkinKey, Stake, null, out Failure);
		}

		/// <summary>Commits an optional exact plotted quote after the UI has shown it.</summary>
		public static bool Commission(KingdomSystem System, string Key, string SkinKey,
			KingdomPlotRules.PlotSize Stake, KingdomPlotQuote Expected, out string Failure)
		{
			Failure = null;
			Zone zone = The.Player?.CurrentZone;
			if (!System.Founded || zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Failure = "Commissions are issued on the kingdom's own ground.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out var entry) || !KingdomRules.StyleAllows(entry.Styles, System.Style))
			{
				Failure = "No such design.";
				return false;
			}
			Failure = StageRefusal(System, entry);
			if (Failure != null)
			{
				return false;
			}
			// Before the room and the stores are counted, so a founder is never told what a design
			// would cost them on ground that was never going to take it.
			if (!KingdomZoning.Permits(System, zone.ZoneID, entry, out string refusal))
			{
				Failure = refusal;
				return false;
			}
			// And before the paths fork, because the way down is a fact about the GROUND and a wall
			// is as unbuildable in unopened rock as a gallery is. The plot path asks again on its
			// own account, since KingdomPlots.Commission is reachable without coming through here.
			Failure = KingdomDelve.Refusal(System, zone.ZoneID, entry.Key, entry.Name);
			if (Failure != null)
			{
				return false;
			}
			// A plot-sized design is raised over a rect in stages, not as one object in one cell.
			// Every design that declares no Plot size - which is all of them until one does - falls
			// through to the single-cell path below, untouched.
			if (KingdomPlots.IsPlotDesign(entry.Key))
			{
				return KingdomPlots.Commission(System, zone, entry, SkinKey, Stake,
					Expected, out Failure);
			}
			if (Expected != null)
			{
				Failure = "That work is not an authored plot and cannot consume a plot preview.";
				return false;
			}
			// Frontier works do not count against the plan. A palisade is a LINE, and charging a slot per
			// segment is what made enclosing a settlement impossible - the ring would have eaten
			// the whole allowance before anything civic was built.
			int built = KingdomPlots.CountBuilt(zone);
			if (!KingdomRules.IsFrontierWork(entry.Defence,
				KingdomPlots.IsPlotDesign(entry.Key))
				&& built >= KingdomRules.MaxBuildingsForStage(System.Stage))
			{
				Failure = "There is no more room in the plan. " + KingdomPresentation.Rich(System.SeatName) + " is as built-up as this ground allows, until it grows into something larger.";
				return false;
			}
			if (KingdomGrowth.CountStoredWater(zone) < entry.CostDrams)
			{
				Failure = "The work would cost {{C|" + entry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			// After the water and before the ground is chosen: a founder is told the whole price
			// before anything is committed, and a design with no material cost is always affordable,
			// which is every design the catalogue carried before materials existed.
			if (!KingdomMaterials.CanPay(zone, entry.Key, out var materialRefusal))
			{
				Failure = materialRefusal;
				return false;
			}
			Cell cell;
			string payload = SkinKey;
			KingdomLayoutRules.LayoutOutcome outcome;
			KingdomGatehousePlan gatePlan = null;
			long started = The.Game.TimeTicks;
			long due = started + CraftBuildTicks(entry.BuildTicks,
				System.ZoneDistricts.Values);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(entry.Key), KingdomMaterials.BitCostFor(entry.Key),
				KingdomMaterials.ExoticCostFor(entry.Key));
			KingdomConstructionJob job = null;
			if (KingdomGatehouseRules.IsGatehouse(entry.Key))
			{
				outcome = KingdomLayoutRules.LayoutOutcome.Grammar;
				// This is the destructive ground read: every one of the nine footprint cells,
				// both road approaches, existing reservations, creatures, and fixtures are proved
				// before either water or materials are reserved. Nothing is cleared or displaced.
				if (!KingdomGatehouse.TryPlan(zone, System, out gatePlan, out Failure)
					|| !KingdomGatehouseRules.TryEncode(gatePlan, out payload))
				{
					Failure = Failure ?? "The exact gatehouse footprint could not be frozen.";
					return false;
				}
				cell = zone.GetCell(gatePlan.GateX, gatePlan.GateY);
				for (int y = gatePlan.Y1; y <= gatePlan.Y2; y++)
				{
					for (int x = gatePlan.X1; x <= gatePlan.X2; x++)
					{
						if (KingdomConstruction.HasActiveAt(System, zone, zone.GetCell(x, y)))
						{
							Failure = "The gatehouse footprint already has a paid construction receipt at "
								+ x + "," + y + ".";
							return false;
						}
					}
				}
			}
			else
			{
				// Mint the durable construction owner before semantic siting. Its exact X/Y are
				// frozen into this same job before publication or any debit.
				job = KingdomConstruction.NewJob(System, zone,
					KingdomConstructionRoute.CommissionScaffold, null, null, entry.Key,
					payload, entry.CostDrams, claim, started, due);
				cell = FindBuildCell(zone, System, entry, job.Id, out outcome);
				if (cell == null)
				{
					Failure = "There is no clear ground for it here.";
					return false;
				}
				if (KingdomConstruction.HasActiveAt(System, zone, cell))
				{
					Failure = "That ground already has a paid construction receipt in hand.";
					return false;
				}
				job.X = cell.X;
				job.Y = cell.Y;
			}
			if (job == null)
				job = KingdomConstruction.NewJob(System, zone,
					KingdomConstructionRoute.CommissionScaffold, cell, null, entry.Key, payload,
					entry.CostDrams, claim, started, due);
			if (!KingdomConstruction.FreezeBuildTruth(job, System, entry.Defence, false))
			{
				Failure = "The commission's exact build effects could not be frozen.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(zone, System);
			KingdomWaterDebit water = survey.ReserveExactWater(entry.CostDrams);
			KingdomMaterialDebit materials = KingdomMaterials.ReservePayment(zone, entry.Key);
			KingdomConstructionStartResult funded = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funded == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stores could not cover the work after all.";
				return false;
			}
			if (funded == KingdomConstructionStartResult.Outstanding)
			{
				KingdomGovernanceScope.Commit("commission building");
				System.Ledger.Note("{{r|The commission has a measured receipt still outstanding. It remains queued and will not charge its paid claims twice.}} ");
				return true;
			}
			if (!ProjectScaffold(System, zone, entry, job.Payload, job, out job, out string projectionFailure))
			{
				KingdomGovernanceScope.Commit("commission building");
				System.Ledger.Note("{{r|The paid commission could not put its scaffold on the ground. Its durable receipt remains queued for another pass.}} ");
				KingdomLog.Log("construction: commission projection waits: " + projectionFailure);
				return true;
			}
			KingdomGovernanceScope.Commit("commission building");
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(entry.Name) + " was commissioned at " + KingdomPresentation.Rich(System.KingdomDisplayName));
			string clause = KingdomLayoutRules.PlacementClause(KingdomLayout.PurposeOfEntry(entry), outcome);
			MessageQueue.AddPlayerMessage("{{G|The " + entry.Name + " is commissioned. Scaffolding rises"
				+ ((clause == null) ? "" : (" " + clause)) + ".}}");
			return true;
		}

	}
}
