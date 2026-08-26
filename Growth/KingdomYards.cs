using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of yard trades (see <see cref="KingdomYardRules"/> for the
	/// geometry, the parsing, and the prose): the registry of authored trades, finding real free
	/// ground in a real house's yard, and raising or striking the one object that stands for a
	/// household's trade.
	/// <para>
	/// A yard work is never itself a plot. It is furniture the way a shop's own furnishings are:
	/// tagged <c>KingdomPlots.PlotPartProperty</c> so the rest of the plot bookkeeping treats it
	/// exactly like anything <c>KingdomPlots.Furnish</c> rolled, and tied to its house by
	/// <see cref="YardKeyProperty"/> rather than by a chain or a successor, because taking one up
	/// is a household's own decision and never a construction the settlement schedules.
	/// </para>
	/// </summary>
	public static partial class KingdomYards
	{
		// --- Registry ----------------------------------------------------------------------

		// Keyed the same way KingdomPlots keys its own plot specs (STANDARDS 6): a later file
		// re-using a Key owns that trade's whole spec, including retheming its blueprint or its
		// prose. Order is kept alongside the map so a founder's picker lists trades in the order
		// they were authored rather than in hash order.
		private static readonly Dictionary<string, KingdomYardRules.YardWorkSpec> Specs = new Dictionary<string, KingdomYardRules.YardWorkSpec>();

		private static readonly List<string> SpecOrder = new List<string>();

		/// <summary>Forgets every registered yard work. Called by the registry loader before it
		/// re-reads the XML streams.</summary>
		public static void ClearSpecs()
		{
			Specs.Clear();
			SpecOrder.Clear();
		}

		/// <summary>
		/// Registers one <c>&lt;yardwork&gt;</c> entry as the registry parses it. Call once per
		/// element that parsed successfully, with the raw attribute strings.
		/// </summary>
		public static void RegisterSpec(string Key, string DisplayName, string Blueprint, string Trade, string Shades, string Goods)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomYardRules.TryParseYardWorkAttributes(Key, DisplayName, Blueprint, Trade, Shades, Goods, out var spec, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomYardWorks: " + error);
				if (Specs.Remove(Key))
				{
					SpecOrder.Remove(Key);
				}
				return;
			}
			if (!Specs.ContainsKey(Key))
			{
				SpecOrder.Add(Key);
			}
			Specs[Key] = spec;
		}

		/// <summary>The yard work a key was registered with, if any.</summary>
		public static bool TryGetSpec(string Key, out KingdomYardRules.YardWorkSpec Spec)
		{
			Spec = null;
			return !string.IsNullOrEmpty(Key) && Specs.TryGetValue(Key, out Spec) && Spec != null;
		}

		/// <summary>Every registered yard work, in authored order. A copy, so a caller may not
		/// perturb the registry by holding it.</summary>
		public static List<string> AllSpecKeys()
		{
			return new List<string>(SpecOrder);
		}

		// --- Properties ----------------------------------------------------------------------

		/// <summary>On the finished house: the registered key of the trade it has taken up, or
		/// absent for a house that never has.</summary>
		public const string YardKeyProperty = "KingdomYardKey";

		/// <summary>On the object standing in the yard: marks it as a yard work rather than an
		/// ordinary furnishing, so it can be found again to be struck.</summary>
		public const string YardWorkProperty = "KingdomYardWork";

		// --- Reading a house ------------------------------------------------------------------

		/// <summary>
		/// Everything a finished plot building needs to say about itself before a yard trade can
		/// be asked of it: its catalogue entry, its plot spec, and the rect it was stamped with.
		/// </summary>
		/// <returns>False for anything that is not a finished plot building at all &mdash; a
		/// single-cell design, a scaffold still under way, or an object the settlement never
		/// built.</returns>
		public static bool TryReadHouse(GameObject Building, out KingdomRules.BuildEntry Entry, out KingdomPlotRules.PlotSpec Spec, out KingdomPlotRules.PlotRect Rect)
		{
			Entry = null;
			Spec = null;
			Rect = default(KingdomPlotRules.PlotRect);
			if (Building == null || Building.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
			{
				return false;
			}
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out Entry) || !KingdomPlots.TryGetSpec(key, out Spec))
			{
				return false;
			}
			return KingdomPlots.TryReadRect(Building, out Rect);
		}

		/// <summary>
		/// The first free cell of a house's yard, scanned in the same row-major order
		/// <c>KingdomPlots.Furnish</c> fills furniture in, so the cell a yard work lands in is
		/// never one furniture could still roll into later &mdash; there is no later roll, but the
		/// shared order keeps the two passes legible as the same idea.
		/// </summary>
		public static bool TryFreeYardCell(Zone Z, KingdomPlotRules.PlotRect Interior, out Cell Free)
		{
			Free = null;
			if (Z == null)
			{
				return false;
			}
			for (int y = Interior.Y1; y <= Interior.Y2; y++)
			{
				for (int x = Interior.X1; x <= Interior.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell != null && cell.IsEmpty() && cell.IsPassable())
					{
						Free = cell;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>The first free cell of a house's yard, band by band in the order
		/// <c>KingdomPlotRules.YardBands</c> returns them, so a trade always lands on the same side
		/// of the same house. A tier that fills its plot has no yard outside it, and
		/// <c>KingdomPlots.YardRects</c> hands back its interior instead -- which is the ground yard
		/// trades used before footprints existed, so nothing standing today moves.</summary>
		public static bool TryFreeYardCell(Zone Z, List<KingdomPlotRules.PlotRect> Bands, out Cell Free)
		{
			Free = null;
			if (Bands == null)
			{
				return false;
			}
			for (int i = 0; i < Bands.Count; i++)
			{
				if (TryFreeYardCell(Z, Bands[i], out Free))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Every finished house in a zone with room to take up a yard trade or already
		/// carrying one &mdash; the candidate set <see cref="ShowYardTrades"/> lists.</summary>
		public static List<GameObject> ListHousesWithYards(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null)
			{
				return found;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (!TryReadHouse(item, out var entry, out var spec, out var rect) || !KingdomYardRules.IsEligibleDesign(spec.Size, spec.Open, entry.Category))
				{
					continue;
				}
				if (!string.IsNullOrEmpty(item.GetStringProperty(YardKeyProperty)))
				{
					found.Add(item);
					continue;
				}
				if (TryFreeYardCell(Z, KingdomPlots.YardRects(item), out _))
				{
					found.Add(item);
				}
			}
			return found;
		}
	}
}
