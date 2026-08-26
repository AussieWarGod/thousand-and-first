using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRoadRules
	{
		// --- The tally -------------------------------------------------------------------

		/// <summary>The traffic recorded on one cell, or zero when it is not tracked.</summary>
		public static int TrafficAt(IList<WornCell> Cells, int X, int Y)
		{
			if (Cells == null)
			{
				return 0;
			}
			for (int i = 0; i < Cells.Count; i++)
			{
				if (Cells[i].X == X && Cells[i].Y == Y)
				{
					return Cells[i].Traffic;
				}
			}
			return 0;
		}

		/// <summary>Index of a cell in the tally, or -1.</summary>
		public static int IndexOf(IList<WornCell> Cells, int X, int Y)
		{
			if (Cells == null)
			{
				return -1;
			}
			for (int i = 0; i < Cells.Count; i++)
			{
				if (Cells[i].X == X && Cells[i].Y == Y)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// Lays traffic on a cell, admitting it to the tally if this is the first time anyone
		/// walked there.
		/// </summary>
		/// <param name="Cells">The tally, which is modified in place.</param>
		/// <param name="X">Cell x.</param>
		/// <param name="Y">Cell y.</param>
		/// <param name="Traffic">Traffic to lay. Zero or less does nothing and succeeds.</param>
		/// <param name="Total">The cell's tally afterward, or zero when nothing was laid.</param>
		/// <returns>False only when a NEW cell could not be admitted because the tally already
		/// holds <see cref="MaxTrackedCells"/> &mdash; the one condition in this file the founder
		/// has to be told about, because it is the one that stops ground wearing for a reason
		/// they can act on (STANDARDS 7b).</returns>
		public static bool Accrue(IList<WornCell> Cells, int X, int Y, int Traffic, out int Total)
		{
			Total = 0;
			if (Cells == null)
			{
				return false;
			}
			int index = IndexOf(Cells, X, Y);
			if (index < 0)
			{
				if (Traffic <= 0)
				{
					return true;
				}
				if (Cells.Count >= MaxTrackedCells)
				{
					return false;
				}
				Total = (Traffic > MaxTraffic) ? MaxTraffic : Traffic;
				Cells.Add(new WornCell(X, Y, Total));
				return true;
			}
			WornCell cell = Cells[index];
			Total = cell.Traffic + ((Traffic > 0) ? Traffic : 0);
			if (Total > MaxTraffic)
			{
				Total = MaxTraffic;
			}
			cell.Traffic = Total;
			Cells[index] = cell;
			return true;
		}

		/// <summary>Forgets one cell &mdash; what a cell that has become a path does, because the
		/// laid path is the record from then on and the tally is only ever for ground still on
		/// its way.</summary>
		/// <returns>False when the cell was not tracked.</returns>
		public static bool Retire(IList<WornCell> Cells, int X, int Y)
		{
			int index = IndexOf(Cells, X, Y);
			if (index < 0)
			{
				return false;
			}
			Cells.RemoveAt(index);
			return true;
		}

		// --- Writing the tally down ------------------------------------------------------

		/// <summary>Separator between cells in the written tally.</summary>
		public const char CellSeparator = ';';

		/// <summary>Separator between a cell's three numbers.</summary>
		public const char FieldSeparator = ',';

		/// <summary>Largest coordinate the written tally accepts. Far past any Qud zone, and
		/// present so a corrupt or hostile string cannot ask for an array nobody has.</summary>
		public const int MaxCoordinate = 999;

		/// <summary>
		/// The tally as one short string: <c>x,y,traffic</c> for each cell, separated by
		/// semicolons. Cells with nothing on them are left out, so an untouched settlement
		/// writes the empty string and costs nothing to carry.
		/// </summary>
		/// <param name="Cells">The tally. Null writes the empty string.</param>
		public static string Encode(IList<WornCell> Cells)
		{
			if (Cells == null || Cells.Count == 0)
			{
				return "";
			}
			StringBuilder text = new StringBuilder();
			int written = 0;
			for (int i = 0; i < Cells.Count && written < MaxTrackedCells; i++)
			{
				WornCell cell = Cells[i];
				if (cell.Traffic <= 0 || cell.X < 0 || cell.Y < 0 || cell.X > MaxCoordinate || cell.Y > MaxCoordinate)
				{
					continue;
				}
				if (written > 0)
				{
					text.Append(CellSeparator);
				}
				int traffic = (cell.Traffic > MaxTraffic) ? MaxTraffic : cell.Traffic;
				text.Append(cell.X).Append(FieldSeparator).Append(cell.Y).Append(FieldSeparator).Append(traffic);
				written++;
			}
			return text.ToString();
		}

		/// <summary>
		/// Reads a written tally back. Hostile-input discipline throughout (STANDARDS 9): a
		/// malformed entry is dropped rather than believed and rather than thrown over, every
		/// number is clamped to a range this build can act on, a repeated cell keeps its largest
		/// tally, and a tally longer than <see cref="MaxTrackedCells"/> is cut to length.
		/// </summary>
		/// <param name="Raw">The written tally. Null and blank read as an empty tally, which is
		/// success: a settlement that has never been walked has nothing to say.</param>
		/// <param name="Cells">Receives the tally. Never null on return.</param>
		/// <param name="Error">A log-facing note when anything was dropped or clamped, or null
		/// when the string was read whole. Never a reason to fail.</param>
		/// <returns>False only when something was dropped, so a caller that wants to log can;
		/// <paramref name="Cells"/> is usable either way.</returns>
		public static bool TryDecode(string Raw, out List<WornCell> Cells, out string Error)
		{
			Cells = new List<WornCell>();
			Error = null;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			int dropped = 0;
			string[] entries = Raw.Split(CellSeparator);
			for (int i = 0; i < entries.Length; i++)
			{
				string entry = entries[i];
				if (string.IsNullOrWhiteSpace(entry))
				{
					continue;
				}
				string[] fields = entry.Split(FieldSeparator);
				if (fields.Length != 3
					|| !int.TryParse(fields[0].Trim(), out var x)
					|| !int.TryParse(fields[1].Trim(), out var y)
					|| !int.TryParse(fields[2].Trim(), out var traffic))
				{
					dropped++;
					continue;
				}
				if (x < 0 || y < 0 || x > MaxCoordinate || y > MaxCoordinate || traffic <= 0)
				{
					dropped++;
					continue;
				}
				if (traffic > MaxTraffic)
				{
					traffic = MaxTraffic;
				}
				int index = IndexOf(Cells, x, y);
				if (index >= 0)
				{
					dropped++;
					if (traffic > Cells[index].Traffic)
					{
						Cells[index] = new WornCell(x, y, traffic);
					}
					continue;
				}
				if (Cells.Count >= MaxTrackedCells)
				{
					dropped++;
					continue;
				}
				Cells.Add(new WornCell(x, y, traffic));
			}
			if (dropped > 0)
			{
				Error = "roads: " + dropped + " unreadable or repeated cells were dropped from a settlement's worn ground";
				return false;
			}
			return true;
		}

	}
}
