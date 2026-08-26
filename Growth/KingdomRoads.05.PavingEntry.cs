using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		// --- Paving -------------------------------------------------------------------------

		/// <summary>Cells of this zone that are a path and not yet paved, nearest a given cell
		/// first, ties broken north-then-west so the same order comes back every time.</summary>
		/// <param name="Z">The zone. Null yields an empty list.</param>
		/// <param name="From">Where the founder is standing, for the ordering. Null orders purely
		/// by position.</param>
		public static List<Cell> PathCells(Zone Z, Cell From)
		{
			List<Cell> cells = new List<Cell>();
			if (Z == null)
			{
				return cells;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item == null || item.GetIntProperty(PathStateProperty) != (int)KingdomRoadRules.WearState.Path)
				{
					continue;
				}
				Cell cell = item.CurrentCell;
				if (cell != null)
				{
					cells.Add(cell);
				}
			}
			cells.Sort(delegate(Cell a, Cell b)
			{
				if (From != null)
				{
					int da = KingdomLayoutRules.Chebyshev(a.X, a.Y, From.X, From.Y);
					int db = KingdomLayoutRules.Chebyshev(b.X, b.Y, From.X, From.Y);
					if (da != db)
					{
						return da - db;
					}
				}
				if (a.Y != b.Y)
				{
					return a.Y - b.Y;
				}
				return a.X - b.X;
			});
			return cells;
		}

		/// <summary>
		/// Lays the settlement's worn paths in the material it builds its walls in.
		/// <para>
		/// Consent before cost: the founder is shown the cells and the price and asked, and a
		/// refusal spends nothing and changes nothing. Nothing is paved that is not already a
		/// path &mdash; the founder formalises what the settlement decided by walking, and never
		/// decides it for them.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">The ground; must be the kingdom's own claim.</param>
		/// <param name="From">Where the founder stands, so a great city paves what is underfoot
		/// first. May be null.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// spent and nothing is laid when it does.</param>
		/// <returns>True once ground has actually been paved. A declined confirmation returns
		/// false with a null <paramref name="Failure"/>, because a refusal is free and is not an
		/// error.</returns>
		public static bool Pave(KingdomSystem System, Zone Z, Cell From, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (!Enabled)
			{
				Failure = "Ground here does not wear, so there is nothing worn to lay. (Options: the settlement's ways)";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = KingdomRoadRules.RefuseNotOurGround();
				return false;
			}
			if (KingdomConstruction.HasActive(System, Z, KingdomConstructionRoute.RoadPaving))
			{
				Failure = "A paid paving order on this ground is already in hand.";
				return false;
			}
			List<Cell> paths = PathCells(Z, From);
			if (paths.Count == 0)
			{
				Failure = KingdomRoadRules.RefuseNothingWorn(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			string wall = KingdomPlotRules.WallBlueprintFor(System.Style, System.FoundingRegionName);
			KingdomMaterial material = KingdomRoadRules.PaveMaterialFor(wall);
			if (!KingdomRoadRules.CanPaveIn(material))
			{
				Failure = KingdomRoadRules.RefuseMaterialKind(material);
				return false;
			}
			if (KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew) <= 0)
			{
				Failure = KingdomRoadRules.RefuseHands(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			int cells = KingdomRoadRules.PaveCells(paths.Count);
			int cost = KingdomRoadRules.PaveCost(cells);
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			int held = stock.Tally.Get(material);
			if (held < cost)
			{
				Failure = KingdomRoadRules.RefuseMaterial(material, cost, held);
				return false;
			}
			if (Popup.ShowYesNo("Lay " + cells + ((cells == 1) ? " cell" : " cells") + " of worn path at " + KingdomPresentation.Rich(System.SeatName)
				+ " in {{C|" + KingdomMaterialRules.MaterialName(material) + "}}?\n\nIt costs " + cost + " of it, and no water. "
				+ ((paths.Count > cells) ? ("There is more worn ground than one order covers; " + (paths.Count - cells) + " more will wait for the next.\n\n") : "")
				+ "Nothing changes about where anyone walks. The settlement only stops pretending it has not decided.") != DialogResult.Yes)
			{
				return false;
			}
			string blueprint = KingdomRoadRules.PavedFloorFor(wall);
			List<KingdomConstructionCell> route = new List<KingdomConstructionCell>();
			for (int i = 0; i < cells; i++)
			{
				route.Add(new KingdomConstructionCell(paths[i].X, paths[i].Y));
			}
			if (!KingdomConstructionRules.TryEncodeCells(route, out string payload))
			{
				Failure = "The paving route could not be recorded safely. Nothing was spent.";
				return false;
			}
			KingdomMaterialTally price = new KingdomMaterialTally();
			price.Add(material, cost);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(price);
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomWaterDebit water = survey.ReserveExactWater(0);
			KingdomMaterialDebit materials = KingdomMaterials.ReserveComposite(Z, claim);
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.RoadPaving, paths[0], null, blueprint, payload, 0, claim);
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stockpiles could not cover the paving after all.";
				return false;
			}
			KingdomGovernanceScope.Commit("pave ground");
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				System.Ledger.Note("{{r|The paving receipt remains outstanding and will retry without another charge.}}");
				return true;
			}
			if (!ProjectPaving(Z, blueprint, route, job, out job, out int laid,
				out string projectionFailure))
			{
				System.Ledger.Note("{{r|The paid paving could not all be laid. Its exact remaining cells stay queued.}}");
				KingdomLog.Log("construction: paving projection waits: " + projectionFailure);
				return true;
			}
			// Paving retires cells from the tally, so the ground the settlement is wearing now
			// has room to be recorded again, and the reason it stalled is over.
			SettleRoadTerminal(System, Z, blueprint, route, ref job);
			KingdomLog.Log("roads: paved " + laid + " cells in " + KingdomMaterialRules.MaterialKey(material) + " at " + System.SeatName);
			return true;
		}

		private static bool SettleRoadTerminal(KingdomSystem System, Zone Z,
			string Blueprint, IList<KingdomConstructionCell> Cells,
			ref KingdomConstructionJob Job)
		{
			if (System == null || Job == null || Job.Phase != KingdomConstructionPhase.Complete
				|| Job.PhysicalPhase == KingdomPhysicalPhase.Settled) return Job != null
					&& Job.PhysicalPhase == KingdomPhysicalPhase.Settled;
			if (Job.PhysicalPhase != KingdomPhysicalPhase.RoadTallySettled
				|| !CurrentRoadOwner(Z, Job)
				|| !RoadTerminalExact(Z, Blueprint, Cells, Job)
				|| !KingdomCeremony.EnsureRoadPavedFromReceipt(System, ref Job)) return false;
			return KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.Settled,
				Job.PhysicalIndex, Job.PhysicalAmount, Job.PhysicalSpilled,
				Job.PhysicalItemId, Job.PhysicalDestinationId, Job.PhysicalReceipt);
		}

	}
}
