using System;
using System.Collections.Generic;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// What the settlement did since the founder last stood in it: the arithmetic of a
	/// visit, kept so the player can be told plainly rather than guessing why the stores
	/// moved. It accumulates across settlement passes and resets only after the founder reads it.
	/// </summary>
	[Serializable]
	public class KingdomLedger
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int Fetched;

		public int UpkeepDrawn;

		public int ArrivalCost;

		public int Delivered;

		/// <summary>Servings the settlement's own fields brought into the larders this pass. The
		/// food mirror of <see cref="Fetched"/>.</summary>
		public int Harvested;

		/// <summary>Servings the settlement's free hands brought in off the land, eaten hand to
		/// mouth rather than stored. The food mirror of the hauling half of
		/// <see cref="Fetched"/>.</summary>
		public int Foraged;

		/// <summary>Servings drawn out of the larders to feed the settlement. The food mirror of
		/// <see cref="UpkeepDrawn"/>.</summary>
		public int RationsDrawn;

		/// <summary>Servings the settlement's mills ADDED this pass by binding a raw harvest into
		/// something that keeps: what came back out of the millstone less what went into it
		/// (<c>KingdomGrowth.GrindHarvest</c>). The gain only &mdash; the crops themselves were
		/// already counted when they were gathered, and counting them twice is exactly the error
		/// the subtraction in <c>FoodMadePerDay</c> exists to prevent.</summary>
		public int Milled;

		/// <summary>Servings the fields made with nowhere to keep them. Loss and not transfer:
		/// there was no larder, or the larders were full, and it was left in the field.</summary>
		public int HarvestLost;

		public int Plundered;

		public int Arrivals;

		public int Departures;

		public List<string> Notes = new List<string>();

		/// <summary>
		/// The brink lane: what is one window away from happening, and what stepped back from
		/// one. Kept apart from <see cref="Notes"/> and printed above them, because a settler
		/// about to leave and a settler who found a roof are not the same weight of news as the
		/// drams that moved &mdash; and because the founder must not have to read past six lines
		/// of housekeeping to find the one thing they can still act on (STANDARDS 7b).
		/// <para>
		/// Written only through <see cref="NoteBrink"/> and <see cref="NoteBrinkLifted"/>, which
		/// <c>KingdomBrink</c> is the only caller of. Every line in here has already been said
		/// once and will not be said again while its brink stands: the announce-once discipline
		/// lives in the brink record, not in this list.
		/// </para>
		/// </summary>
		public List<string> BrinkLines = new List<string>();

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomLedger));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomLedger));
			Normalize();
		}
#endif

		public void Normalize()
		{
			if (Notes == null)
			{
				Notes = new List<string>();
			}
			if (BrinkLines == null)
			{
				BrinkLines = new List<string>();
			}
		}

		public bool Any
		{
			get
			{
				return Fetched > 0 || UpkeepDrawn > 0 || ArrivalCost > 0 || Delivered > 0 || Plundered > 0 || Arrivals > 0 || Departures > 0
					|| Harvested > 0 || Foraged > 0 || RationsDrawn > 0 || HarvestLost > 0 || Milled > 0
					|| Notes.Count > 0 || BrinkLines.Count > 0;
			}
		}

		public void Reset()
		{
			Fetched = 0;
			UpkeepDrawn = 0;
			ArrivalCost = 0;
			Delivered = 0;
			Harvested = 0;
			Milled = 0;
			Foraged = 0;
			RationsDrawn = 0;
			HarvestLost = 0;
			Plundered = 0;
			Arrivals = 0;
			Departures = 0;
			Notes.Clear();
			BrinkLines.Clear();
		}

		public void Note(string Line)
		{
			if (!string.IsNullOrEmpty(Line) && Notes.Count < 12)
			{
				Notes.Add(Line);
			}
		}

		/// <summary>Brink lines a homecoming report will carry before it gives up and stops
		/// listing them. Eight: a settlement in which eight separate irreversible things are one
		/// window away is a settlement whose founder needs to be told to come home, not handed a
		/// longer list.</summary>
		public const int MaxBrinkLines = 8;

		/// <summary>
		/// One brink's announcement, in the colour of a thing that has not happened yet but will.
		/// Said once per brink by <c>KingdomBrink.Announce</c>; this list never dedupes, because
		/// the brink record upstream already guarantees the line arrives once.
		/// </summary>
		public void NoteBrink(string Line)
		{
			AddBrink(Line, "{{r|");
		}

		/// <summary>The unsaying: a brink whose cause went before its window did. Green, because
		/// it is the only good news in this lane and the founder earned it by acting.</summary>
		public void NoteBrinkLifted(string Line)
		{
			AddBrink(Line, "{{G|");
		}

		private void AddBrink(string Line, string Colour)
		{
			if (!string.IsNullOrEmpty(Line) && BrinkLines.Count < MaxBrinkLines)
			{
				BrinkLines.Add(Colour + Line + "}}");
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
			// The brink lane first, and dated by the day count above it: these are the lines the
			// founder can still do something about, and every pass they spend reading past them
			// is a pass of somebody's window.
			for (int i = 0; i < BrinkLines.Count; i++)
			{
				sb.Append("\n").Append(BrinkLines[i]);
			}
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
			// The food half, in servings rather than drams and said so, because a founder
			// reading one running total in two units would read it wrong.
			if (Harvested > 0)
			{
				sb.Append(wrote ? ", " : "").Append(Harvested).Append(" gathered into the larders");
				wrote = true;
			}
			if (Milled > 0)
			{
				sb.Append(wrote ? ", " : "").Append(Milled).Append(" more won out of the millstone");
				wrote = true;
			}
			if (Foraged > 0)
			{
				sb.Append(wrote ? ", " : "").Append(Foraged).Append(" foraged off the land");
				wrote = true;
			}
			if (RationsDrawn > 0)
			{
				sb.Append(wrote ? ", " : "").Append(RationsDrawn).Append(" eaten out of the larders");
				wrote = true;
			}
			if (HarvestLost > 0)
			{
				sb.Append(wrote ? ", " : "").Append(HarvestLost).Append(" left in the field for want of a larder");
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
