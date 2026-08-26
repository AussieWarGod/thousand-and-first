using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for the late-game life of a small house: an S/M roofed plot with a free
	/// cell in its own yard can take up ONE yard trade &mdash; vine lattice, hide rack, dye vat,
	/// vellum press in the first-pass set &mdash; and the household that lives there takes up the
	/// trade.
	/// <para>
	/// "Yard cells" are read straight off the plot geometry <c>KingdomPlotRules</c> already owns:
	/// a roofed plot's border is where the walls stand (<c>PlotRect.IsBorder</c>), so every cell
	/// that is <em>inside the rect and not on the border</em> is a cell the walls do not occupy.
	/// Most of those cells are already spoken for by the design's own furnishing pass
	/// (<c>KingdomPlots.Furnish</c> rolls <see cref="KingdomPlotRules.ContentsRolls"/> pieces of
	/// furniture into that same interior, and a hut's one roll rarely fills a 3x2 room) &mdash; the
	/// ones left empty are the yard. Nothing here invents a second rect or a strip of open ground
	/// the settlement does not already model.
	/// </para>
	/// <para>
	/// Nothing here touches <c>XRL</c>. It never reads a cell and never places an object; it
	/// answers questions about rectangles, tallies, and prose. The engine-coupled half &mdash;
	/// finding real free cells, placing the real object, keeping the registry &mdash; is
	/// <c>KingdomYards</c>, in Growth/KingdomYards.cs.
	/// </para>
	/// </summary>
	public static class KingdomYardRules
	{
		/// <summary>
		/// One authored yard trade: what it is called, what stands in the yard for it, and what a
		/// household taking it up does for the settlement.
		/// </summary>
		public sealed class YardWorkSpec
		{
			/// <summary>Registry key, e.g. <c>hiderack</c>.</summary>
			public string Key;

			/// <summary>How the founder reads it in a list: "hide rack".</summary>
			public string DisplayName;

			/// <summary>The object placed in the yard.</summary>
			public string Blueprint;

			/// <summary>
			/// The trade itself, as a household is said to take it up: "tanning". Falls back to
			/// <see cref="DisplayName"/> when a file declares none, which reads a little
			/// object-first but is never wrong.
			/// </summary>
			public string Trade;

			/// <summary>
			/// What the trade shades the settlement's equilibrium by
			/// (<c>KingdomCatalogueRules.Equilibrium</c>'s own <c>support:amount</c> language).
			/// Never null; empty for a trade that shades nothing.
			/// </summary>
			public List<KindAmount> Shades;

			/// <summary>
			/// True for a trade whose output is a caravan good rather than anything the
			/// settlement's own equilibrium reads &mdash; the dye vat's own case. Pure flag only:
			/// what a chartered caravan does with that is <c>KingdomTrade</c>'s business, not
			/// this file's.
			/// </summary>
			public bool FeedsGoods;
		}

		/// <summary>
		/// The most one yard work may shade the equilibrium by, summed across every
		/// <c>support:amount</c> pair it declares. "Small and capped": a household's sideline
		/// never competes with a purpose-built work, and a plot only ever carries one yard work
		/// at all, so this is the whole ceiling a third-party file can reach for one house.
		/// </summary>
		public const int MaxShadePerWork = 2;

		/// <summary>Category a plot's design must declare to ever be a candidate. Folded the same
		/// way every other kind string in the catalogue is.</summary>
		public const string HouseCategory = "housing";

		// --- Eligibility --------------------------------------------------------------------

		/// <summary>
		/// Whether a design's plot could ever take up a yard trade at all &mdash; before anything
		/// about the ground, the rect, or what is already standing there is asked. Small and
		/// middling houses only: a hut and a house are Manor Lords' burgage plot, a hall or a
		/// manor is a purpose-built work with no household sideline to speak of.
		/// </summary>
		public static bool IsEligibleDesign(KingdomPlotRules.PlotSize Size, bool Open, string Category)
		{
			if (Open)
			{
				return false;
			}
			if (Size != KingdomPlotRules.PlotSize.Small && Size != KingdomPlotRules.PlotSize.Medium)
			{
				return false;
			}
			return Fold(Category) == HouseCategory;
		}

		/// <summary>
		/// The yard of a roofed plot: every cell inside the rect that is not one of the wall
		/// cells. Identical bounds to <c>KingdomPlots.Furnish</c>'s own interior loop, so a cell
		/// this returns and a cell furniture could have rolled into are always the same set.
		/// </summary>
		/// <returns>False for a rect too small to have an interior at all &mdash; every real plot
		/// tier clears this, so this only ever refuses a hostile or malformed rect.</returns>
		public static bool TryYardInterior(KingdomPlotRules.PlotRect Rect, out KingdomPlotRules.PlotRect Interior)
		{
			Interior = default(KingdomPlotRules.PlotRect);
			if (Rect.Width <= 2 || Rect.Height <= 2)
			{
				return false;
			}
			Interior = new KingdomPlotRules.PlotRect(Rect.X1 + 1, Rect.Y1 + 1, Rect.X2 - 1, Rect.Y2 - 1);
			return true;
		}

		// --- Parsing (STANDARDS 6: authorable from XML like everything else) -----------------

		/// <summary>
		/// Parses one <c>&lt;yardwork&gt;</c> entry's raw attribute strings. Mirrors
		/// <c>KingdomPlotRules.TryParsePlotAttributes</c>'s shape: every field is checked, and the
		/// first thing wrong is the whole error, so a malformed third-party entry logs one clear
		/// line and is dropped rather than half-registered (STANDARDS 9).
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Required.</param>
		/// <param name="DisplayName">Raw <c>DisplayName</c>. Required.</param>
		/// <param name="Blueprint">Raw <c>Blueprint</c>. Required.</param>
		/// <param name="Trade">Raw <c>Trade</c>. Optional; falls back to <paramref name="DisplayName"/>.</param>
		/// <param name="Shades">Raw <c>Shades</c>, a <c>support:amount</c> list in
		/// <c>KingdomCatalogueRules.TryParseTally</c>'s own language. Optional.</param>
		/// <param name="Goods">Raw <c>Goods</c> yes/no. Optional, defaults to no.</param>
		/// <param name="Spec">The parsed spec, or null on failure.</param>
		/// <param name="Error">A log-facing reason, or null on success.</param>
		public static bool TryParseYardWorkAttributes(string Key, string DisplayName, string Blueprint, string Trade, string Shades, string Goods, out YardWorkSpec Spec, out string Error)
		{
			Spec = null;
			Error = null;
			if (string.IsNullOrWhiteSpace(Key))
			{
				Error = "yard work attributes need a Key";
				return false;
			}
			if (string.IsNullOrWhiteSpace(DisplayName))
			{
				Error = "yard work " + Key + " needs a DisplayName";
				return false;
			}
			if (string.IsNullOrWhiteSpace(Blueprint))
			{
				Error = "yard work " + Key + " needs a Blueprint";
				return false;
			}
			if (!KingdomPlotRules.TryParseFlag(Goods, out var goods))
			{
				Error = "yard work " + Key + " has a bad Goods (want Yes or No)";
				return false;
			}
			if (!KingdomCatalogueRules.TryParseTally(Shades, out var shades, out var shadesError))
			{
				Error = "yard work " + Key + " has a bad Shades: " + shadesError;
				return false;
			}
			int total = 0;
			for (int i = 0; i < shades.Count; i++)
			{
				total += shades[i].Amount;
			}
			if (total > MaxShadePerWork)
			{
				Error = "yard work " + Key + " shades " + total + ", more than the " + MaxShadePerWork + " a single yard work may add";
				return false;
			}
			if (goods && shades.Count != 0)
			{
				Error = "yard work " + Key + " declares both Goods and Shades; caravan goods are instead of equilibrium support";
				return false;
			}
			Spec = new YardWorkSpec
			{
				Key = Key.Trim(),
				DisplayName = DisplayName.Trim(),
				Blueprint = Blueprint.Trim(),
				Trade = string.IsNullOrWhiteSpace(Trade) ? DisplayName.Trim() : Trade.Trim(),
				Shades = shades,
				FeedsGoods = goods
			};
			return true;
		}

		/// <summary>One line summing up what a trade does, for a picker that lists trades before
		/// one is chosen.</summary>
		public static string ShadeSummary(YardWorkSpec Spec)
		{
			if (Spec.FeedsGoods)
			{
				return KingdomYardGoodsRules.EffectSummary();
			}
			if (Spec.Shades == null || Spec.Shades.Count == 0)
			{
				return "shades nothing; it is only what it is";
			}
			string summary = null;
			for (int i = 0; i < Spec.Shades.Count; i++)
			{
				string piece = "shades " + Spec.Shades[i].Kind + " by " + Spec.Shades[i].Amount;
				summary = (summary == null) ? piece : (summary + ", " + piece);
			}
			return summary;
		}

		// --- Saying so (STANDARDS 7b: nothing stalls in silence) -----------------------------

		/// <summary>The founder tried to work ground that was never a candidate: the wrong plot
		/// size, an open plot, or not a house at all.</summary>
		public static string RefuseNotEligible(string HouseName)
		{
			return "A yard trade is a small or middling house's own doing. " + HouseName + " has no yard to take one up in.";
		}

		/// <summary>Every cell of the yard is already spoken for &mdash; furnished, held, or
		/// simply too small a house to have one free.</summary>
		public static string RefuseNoRoom(string HouseName)
		{
			return "There is no free ground in the yard of " + HouseName + " for a trade to stand in.";
		}

		/// <summary>A plot only ever carries one yard work. Taking up a second means letting the
		/// first go first.</summary>
		public static string RefuseAlreadyWorking(string HouseName, string ExistingTradeName)
		{
			return HouseName + " has already turned its yard to " + ExistingTradeName + ". Let that go before it takes up another.";
		}

		/// <summary>The Key named resolves to nothing the registry knows.</summary>
		public static string RefuseUnknownWork(string Key)
		{
			return "\"" + Key + "\" names no yard trade the settlement knows.";
		}

		/// <summary>Nothing standing anywhere claimed is even a candidate.</summary>
		public const string RefuseNoneStanding = "Nothing here is a house with room in its yard for a trade.";

		/// <summary>The household's own line, read when the trade is taken up. First person from
		/// the settlement's own record, present tense: it is happening now.</summary>
		public static string TakeUpLine(string HouseName, YardWorkSpec Spec)
		{
			return "the household of " + HouseName + " turns its yard to " + Spec.Trade + ", and a " + Spec.DisplayName + " stands there now";
		}

		/// <summary>Appended to the house's own description once a trade is standing, so looking
		/// at the place says what looking at the Charter already does.</summary>
		public static string DescriptionLine(YardWorkSpec Spec)
		{
			return "{{rules|The household here has turned its yard to " + Spec.Trade + ": a " + Spec.DisplayName + " stands in it.}}";
		}

		/// <summary>
		/// Read when a yard work comes down. Free and returns nothing &mdash; unlike striking a
		/// building, there is no salvage line here, and the prose says so plainly rather than
		/// simply omitting one, so a founder who expects a yield the way clearance and strikes
		/// give one is not left wondering where it went.
		/// </summary>
		public static string ReleaseLine(string HouseName, YardWorkSpec Spec)
		{
			return "the " + Spec.DisplayName + " comes down from the yard of " + HouseName + ". Nothing is recovered; a trade given up returns only the room it stood in";
		}

		// --- helpers ---------------------------------------------------------------------

		private static string Fold(string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return null;
			}
			string trimmed = Value.Trim().ToLowerInvariant();
			return (trimmed.Length == 0) ? null : trimmed;
		}
	}
}
