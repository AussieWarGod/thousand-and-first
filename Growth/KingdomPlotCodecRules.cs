using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Parsing ---------------------------------------------------------------------

		/// <summary>
		/// Reads one design's plot attributes without a footprint or a roof, exactly as the
		/// schema read before tiers declared their own ground. Kept because it is supported API:
		/// a design read this way fills its plot and takes the walled default.
		/// </summary>
		public static bool TryParsePlotAttributes(string Key, string Plot, string Open, string Sky, string Contents, out PlotSpec Spec, out string Error)
		{
			return TryParsePlotAttributes(Key, Plot, Open, Sky, Contents, null, null, out Spec, out Error);
		}

		/// <summary>
		/// Reads one design's plot attributes, footprint and roof included. Every one of them is
		/// optional, and a design that declares none is not a plot at all &mdash; which is how
		/// every design that already exists keeps the single-cell path it has always had.
		/// <para>
		/// A design that declares a <c>Plot</c> and no <c>Footprint</c> fills that plot and is
		/// walled unless it is <c>Open</c>, which is exactly what it did before footprints
		/// existed: not one entry written against the old schema changes what it builds.
		/// </para>
		/// </summary>
		/// <param name="Key">The design's key. Blank is refused.</param>
		/// <param name="Plot">Raw <c>Plot</c>: S, M, L, XL, or the long spellings. Absent means
		/// not a plot.</param>
		/// <param name="Open">Raw <c>Open</c>: an unroofed plot.</param>
		/// <param name="Sky">Raw <c>Sky</c>: needs weather, so refuses underground and refuses a
		/// tier that declares itself walled.</param>
		/// <param name="Contents">Raw <c>Contents</c>: population table the interior is furnished
		/// from.</param>
		/// <param name="Footprint">Raw <c>Footprint</c>: <c>WxH</c>, the ground this TIER stands
		/// on inside the plot. Absent fills the plot. Larger than the plot is refused here and
		/// again by the whole-catalogue validator, which is the one that sees the merged value.
		/// </param>
		/// <param name="Roof">Raw <c>Roof</c>: Open, Soft, Walled, or Carved.</param>
		/// <param name="Spec">The parsed spec, or null on failure.</param>
		/// <param name="Error">A log-facing reason, or null on success.</param>
		public static bool TryParsePlotAttributes(string Key, string Plot, string Open, string Sky, string Contents, string Footprint, string Roof, out PlotSpec Spec, out string Error)
		{
			Spec = null;
			Error = null;
			if (string.IsNullOrWhiteSpace(Key))
			{
				Error = "plot attributes need a Key";
				return false;
			}
			if (!TryParseSize(Plot, out var size))
			{
				Error = "building " + Key + " has a bad Plot (want S, M, L, or XL)";
				return false;
			}
			if (!TryParseFlag(Open, out var open))
			{
				Error = "building " + Key + " has a bad Open (want Yes or No)";
				return false;
			}
			if (!TryParseFlag(Sky, out var sky))
			{
				Error = "building " + Key + " has a bad Sky (want Yes or No)";
				return false;
			}
			if (!TryParseFootprint(Footprint, out var footprintWidth, out var footprintHeight))
			{
				Error = "building " + Key + " has a bad Footprint (want WxH, as in 6x4)";
				return false;
			}
			if (!TryParseRoof(Roof, out var roof, out var roofDeclared))
			{
				Error = "building " + Key + " has a bad Roof (want Open, Soft, Walled, or Carved)";
				return false;
			}
			bool footprintDeclared = footprintWidth > 0 && footprintHeight > 0;
			if (size == PlotSize.None && (open || sky || footprintDeclared || roofDeclared || !string.IsNullOrWhiteSpace(Contents)))
			{
				Error = "building " + Key + " declares plot attributes without a Plot size; they would do nothing";
				return false;
			}
			bool openDeclared = !string.IsNullOrWhiteSpace(Open);
			if (roofDeclared && openDeclared && open != (roof == RoofState.Open))
			{
				Error = "building " + Key + " declares Open=" + (open ? "Yes" : "No") + " and a Roof of "
					+ roof.ToString().ToLowerInvariant() + ", which disagree";
				return false;
			}
			if (!roofDeclared)
			{
				roof = DefaultRoof(open);
			}
			if (footprintDeclared && !FootprintFits(size, footprintWidth, footprintHeight))
			{
				TryDimensions(size, out var plotWidth, out var plotHeight);
				Error = "building " + Key + " wants a footprint of " + SpanWord(footprintWidth, footprintHeight)
					+ " on a " + SizeName(size) + " plot, which is " + SpanWord(plotWidth, plotHeight)
					+ "; a footprint never outgrows its plot";
				return false;
			}
			Spec = new PlotSpec
			{
				Key = Key.Trim(),
				Size = size,
				Open = (roof == RoofState.Open),
				RequiresSky = sky,
				Contents = string.IsNullOrWhiteSpace(Contents) ? null : Contents.Trim(),
				FootprintWidth = footprintDeclared ? footprintWidth : 0,
				FootprintHeight = footprintDeclared ? footprintHeight : 0,
				Roof = roof,
				RoofDeclared = roofDeclared
			};
			return true;
		}

		/// <summary>
		/// Reads a footprint. Absent is "fills the plot" and not an error; anything the shape
		/// <c>WxH</c> cannot be read out of is, rather than quietly filling the plot, because a
		/// mistyped footprint that silently became the whole plot would move a building's walls
		/// without saying so.
		/// </summary>
		public static bool TryParseFootprint(string Raw, out int Width, out int Height)
		{
			Width = 0;
			Height = 0;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			string[] parts = Raw.Trim().ToLowerInvariant().Split(FootprintSeparator);
			if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var width) || !int.TryParse(parts[1].Trim(), out var height)
				|| width < 1 || height < 1)
			{
				return false;
			}
			Width = width;
			Height = height;
			return true;
		}

		/// <summary>Between the two spans of a footprint: <c>6x4</c>. Case-folded before the
		/// split, so <c>6X4</c> reads the same.</summary>
		public const char FootprintSeparator = 'x';

		/// <summary>Parses a roof state. Absent leaves <paramref name="Declared"/> false and the
		/// design making no claim about its roof, which is what every entry written before roofs
		/// existed does; anything unrecognised is an error rather than a silent walled default.
		/// </summary>
		public static bool TryParseRoof(string Raw, out RoofState Roof, out bool Declared)
		{
			Roof = RoofState.Walled;
			Declared = false;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			switch (Raw.Trim().ToLowerInvariant())
			{
				case "open":
					Roof = RoofState.Open;
					break;
				case "soft":
				case "canvas":
					Roof = RoofState.Soft;
					break;
				case "walled":
				case "walls":
					Roof = RoofState.Walled;
					break;
				case "carved":
					Roof = RoofState.Carved;
					break;
				default:
					return false;
			}
			Declared = true;
			return true;
		}

		/// <summary>
		/// Whether a design that needs weather is contradicted by its own tier. Only a tier that
		/// DECLARES itself walled or carved contradicts it: a design that never claimed a roof has
		/// made no claim to contradict, and is raised exactly as it always was.
		/// </summary>
		public static bool RoofRefusesSky(PlotSpec Spec)
		{
			return Spec != null && Spec.RequiresSky && Spec.RoofDeclared && !AdmitsSky(Spec.Roof);
		}

		/// <summary>Parses a tier. Absent is <see cref="PlotSize.None"/> and not an error;
		/// anything unrecognised is.</summary>
		public static bool TryParseSize(string Raw, out PlotSize Size)
		{
			Size = PlotSize.None;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			switch (Raw.Trim().ToLowerInvariant())
			{
				case "s":
				case "small":
					Size = PlotSize.Small;
					return true;
				case "m":
				case "medium":
					Size = PlotSize.Medium;
					return true;
				case "l":
				case "large":
					Size = PlotSize.Large;
					return true;
				case "xl":
				case "huge":
					Size = PlotSize.Huge;
					return true;
				default:
					return false;
			}
		}

		/// <summary>Parses a yes/no attribute. Absent is false and not an error; anything
		/// unrecognised is, rather than quietly reading as no.</summary>
		public static bool TryParseFlag(string Raw, out bool Value)
		{
			Value = false;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			switch (Raw.Trim().ToLowerInvariant())
			{
				case "yes":
				case "true":
				case "1":
					Value = true;
					return true;
				case "no":
				case "false":
				case "0":
					return true;
				default:
					return false;
			}
		}
	}
}
