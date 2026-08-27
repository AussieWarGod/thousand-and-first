using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	public static partial class KingdomCrops
	{
		// ==================================================================================
		// The rows themselves
		// ==================================================================================

		/// <summary>
		/// Lays up to <paramref name="Rows"/> standing plants across the field's footprint, on a
		/// stride so they read as rows rather than as a heap, and never over anything (STANDARDS
		/// 7: automatic placement targets empty cells only). Returns how many actually stood,
		/// which is what the field is worth from here on.
		/// </summary>
		public static int LayRows(Zone Z, GameObject Work, string RowBlueprint, int Rows)
		{
			if (Z == null || Work == null || string.IsNullOrEmpty(RowBlueprint) || Rows <= 0)
			{
				return 0;
			}
			KingdomPlotRules.PlotRect rect;
			if (!KingdomPlots.TryReadFootprint(Work, out rect))
			{
				return 0;
			}
			string id = Work.GetStringProperty(KingdomPlots.PlotIdProperty);
			List<Cell> open = new List<Cell>();
			for (int y = rect.Y1; y <= rect.Y2; y++)
			{
				for (int x = rect.X1; x <= rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell != null && cell.IsEmpty() && cell.IsPassable())
					{
						open.Add(cell);
					}
				}
			}
			if (open.Count == 0)
			{
				return 0;
			}
			// Strided rather than sequential, so a sparse kitchen garden reads as a garden spread
			// over its patch instead of a solid block in one corner. Vanilla's own village farm
			// builder lays a crop on a stride for the same reason.
			//
			// Which cells are spoken for is tracked here rather than re-asked of the cell, because
			// Cell.IsEmpty() is true of a cell already holding a plant (a RenderLayer-3 non-combat
			// object does not make a cell non-empty), so a second sweep would stack two rows on one
			// square and the field would look half its size while counting full.
			bool[] used = new bool[open.Count];
			int stride = open.Count / Rows;
			if (stride < 1)
			{
				stride = 1;
			}
			int laid = 0;
			for (int i = 0; i < open.Count && laid < Rows; i += stride)
			{
				if (StandRow(open[i], RowBlueprint, Work, id))
				{
					used[i] = true;
					laid++;
				}
			}
			// A dense design wants more rows than one strided sweep reaches; fill the gaps left
			// behind rather than shorting the field for an arithmetic reason.
			for (int i = 0; i < open.Count && laid < Rows; i++)
			{
				if (used[i])
				{
					continue;
				}
				if (StandRow(open[i], RowBlueprint, Work, id))
				{
					used[i] = true;
					laid++;
				}
			}
			return laid;
		}

		/// <summary>Stands one row in one cell, marked as this field's own. False when the
		/// blueprint does not resolve, which stops the sweep rather than spinning it.</summary>
		private static bool StandRow(Cell C, string RowBlueprint, GameObject Work, string PlotId)
		{
			GameObject plant = GameObject.Create(RowBlueprint);
			if (plant == null)
			{
				return false;
			}
			plant.SetIntProperty(RowProperty, 1);
			plant.SetIntProperty(KingdomPlots.PlotPartProperty, 1);
			plant.SetStringProperty(RowFieldProperty, Work.ID);
			if (!string.IsNullOrEmpty(PlotId))
			{
				plant.SetStringProperty(KingdomPlots.PlotIdProperty, PlotId);
			}
			GameObject accepted = null;
			try { accepted = C.AddObject(plant); }
			finally { KingdomSurvey.ObserveAddResultInActive(C.ParentZone, plant, accepted); }
			return ReferenceEquals(accepted, plant) && ReferenceEquals(plant.CurrentCell, C);
		}

		/// <summary>Rows standing ripe right now &mdash; what a gathering is actually owed, and
		/// what a founder who walked the rows with a basket has already reduced.</summary>
		public static int CountRipe(List<GameObject> Rows)
		{
			int ripe = 0;
			for (int i = 0; (Rows != null) && i < Rows.Count; i++)
			{
				Harvestable harvestable = Rows[i].GetPart<Harvestable>();
				if (harvestable != null && harvestable.Ripe)
				{
					ripe++;
				}
			}
			return ripe;
		}

		/// <summary>Every row this field laid that is still standing.</summary>
		public static List<GameObject> RowsOf(Zone Z, GameObject Work)
		{
			List<GameObject> rows = new List<GameObject>();
			if (Z == null || Work == null)
			{
				return rows;
			}
			string id = Work.ID;
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			for (int i = 0; i < survey.CropRows.Count; i++)
			{
				GameObject item = survey.CropRows[i];
				if (item.GetIntProperty(RowProperty) == 1 && item.GetStringProperty(RowFieldProperty) == id)
				{
					rows.Add(item);
				}
			}
			return rows;
		}

		/// <summary>Takes this field's own rows up. Only objects this file created and marked are
		/// touched, which is the protection law's whole warrant.</summary>
		public static void ClearRows(Zone Z, GameObject Work)
		{
			List<GameObject> rows = RowsOf(Z, Work);
			for (int i = 0; i < rows.Count; i++)
			{
				try { rows[i].Obliterate(); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(Z, rows[i]); }
			}
		}

		/// <summary>Turns every standing row ripe or unripe, through vanilla's own
		/// <c>Harvestable.UpdateRipeStatus</c> so the tile and the colours swap exactly the way
		/// every other plant in the game swaps them.</summary>
		/// <returns>Rows that were standing ripe BEFORE the change, which is what a gathering
		/// counts.</returns>
		public static int SetRipe(List<GameObject> Rows, bool Ripe)
		{
			int wereRipe = 0;
			for (int i = 0; (Rows != null) && i < Rows.Count; i++)
			{
				Harvestable harvestable = Rows[i].GetPart<Harvestable>();
				if (harvestable == null)
				{
					continue;
				}
				if (harvestable.Ripe)
				{
					wereRipe++;
				}
				if (harvestable.Ripe != Ripe)
				{
					harvestable.UpdateRipeStatus(Ripe);
				}
			}
			return wereRipe;
		}

	}
}
