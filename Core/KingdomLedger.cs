using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// What the settlement did since the founder last stood in it: the arithmetic of a
	/// visit, kept so the player can be told plainly rather than guessing why the stores
	/// moved. Reset at the start of every settlement pass.
	/// </summary>
	[Serializable]
	public class KingdomLedger
	{
		public int Fetched;

		public int UpkeepDrawn;

		public int ArrivalCost;

		public int Delivered;

		public int Plundered;

		public int Arrivals;

		public int Departures;

		public List<string> Notes = new List<string>();

		public bool Any
		{
			get
			{
				if (Arrivals <= 0 && Departures <= 0 && Delivered <= 0 && Plundered <= 0)
				{
					return Notes.Count > 0;
				}
				return true;
			}
		}

		public void Reset()
		{
			Fetched = 0;
			UpkeepDrawn = 0;
			ArrivalCost = 0;
			Delivered = 0;
			Plundered = 0;
			Arrivals = 0;
			Departures = 0;
			Notes.Clear();
		}

		public void Note(string Line)
		{
			if (!string.IsNullOrEmpty(Line) && Notes.Count < 12)
			{
				Notes.Add(Line);
			}
		}

		/// <summary>
		/// The homecoming report. Written as things that have already happened, because they
		/// have &mdash; the settlement lived while you were away, and this is the telling.
		/// </summary>
		/// <param name="Name">Settlement display name.</param>
		/// <param name="Days">Days accounted for.</param>
		public string Digest(string Name, int Days)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("{{C|").Append(Name).Append("}}, while you were away");
			if (Days > 0)
			{
				sb.Append(" (").Append(Days).Append((Days == 1) ? " day" : " days").Append(" accounted)");
			}
			sb.Append("\n");
			for (int i = 0; i < Notes.Count; i++)
			{
				sb.Append("\n").Append(Notes[i]);
			}
			sb.Append("\n\n{{K|The ledger: ");
			bool wrote = false;
			if (Fetched > 0)
			{
				sb.Append(Fetched).Append(" drams drawn from open water");
				wrote = true;
			}
			if (Delivered > 0)
			{
				sb.Append(wrote ? ", " : "").Append(Delivered).Append(" delivered under charter");
				wrote = true;
			}
			if (UpkeepDrawn > 0)
			{
				sb.Append(wrote ? ", " : "").Append(UpkeepDrawn).Append(" drunk by the settlement");
				wrote = true;
			}
			if (ArrivalCost > 0)
			{
				sb.Append(wrote ? ", " : "").Append(ArrivalCost).Append(" poured for new arrivals");
				wrote = true;
			}
			if (Plundered > 0)
			{
				sb.Append(wrote ? ", " : "").Append(Plundered).Append(" lost to raiders");
				wrote = true;
			}
			if (!wrote)
			{
				sb.Append("nothing moved");
			}
			sb.Append(".}}");
			return sb.ToString();
		}
	}
}
