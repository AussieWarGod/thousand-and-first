using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		// --- Plots already laid -----------------------------------------------------------

		/// <summary>The rect an object carries, if it represents a plot.</summary>
		public static bool TryReadRect(GameObject Object, out KingdomPlotRules.PlotRect Rect)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Zone zone = Object == null ? null : Object.CurrentZone;
			if (zone == null)
			{
				return false;
			}
			bool x1 = Object.HasIntProperty(PlotX1Property);
			bool y1 = Object.HasIntProperty(PlotY1Property);
			bool x2 = Object.HasIntProperty(PlotX2Property);
			bool y2 = Object.HasIntProperty(PlotY2Property);
			bool mistyped = Object.HasStringProperty(PlotX1Property)
				|| Object.HasStringProperty(PlotY1Property)
				|| Object.HasStringProperty(PlotX2Property)
				|| Object.HasStringProperty(PlotY2Property);
			if (mistyped) return false;
			bool anyProperties = x1 || y1 || x2 || y2;
			bool allProperties = x1 && y1 && x2 && y2;
			r_KingdomPlotWorks works = Object.GetPart<r_KingdomPlotWorks>();
			if (works != null)
			{
				Rect = works.Rect();
				if (anyProperties && (!allProperties
					|| Object.GetIntProperty(PlotX1Property) != Rect.X1
					|| Object.GetIntProperty(PlotY1Property) != Rect.Y1
					|| Object.GetIntProperty(PlotX2Property) != Rect.X2
					|| Object.GetIntProperty(PlotY2Property) != Rect.Y2)) return false;
				return KingdomPlotRules.ValidZoneRect(Rect, zone.Width, zone.Height);
			}
			if (!allProperties)
			{
				return false;
			}
			Rect = new KingdomPlotRules.PlotRect(
				Object.GetIntProperty(PlotX1Property),
				Object.GetIntProperty(PlotY1Property),
				Object.GetIntProperty(PlotX2Property),
				Object.GetIntProperty(PlotY2Property));
			return KingdomPlotRules.ValidZoneRect(Rect, zone.Width, zone.Height);
		}

		/// <summary>
		/// The stamped rect of a plot object that is not standing in any zone yet: a prepared
		/// works object between creation and AddObject. Same part/property agreement as
		/// <see cref="TryReadRect"/>, without the zone bounds that a placed object is held to; the
		/// caller compares the rect with the plan it was stamped from.
		/// </summary>
		public static bool TryReadStampedRect(GameObject Object, out KingdomPlotRules.PlotRect Rect)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			if (Object == null) return false;
			bool x1 = Object.HasIntProperty(PlotX1Property);
			bool y1 = Object.HasIntProperty(PlotY1Property);
			bool x2 = Object.HasIntProperty(PlotX2Property);
			bool y2 = Object.HasIntProperty(PlotY2Property);
			if (Object.HasStringProperty(PlotX1Property) || Object.HasStringProperty(PlotY1Property)
				|| Object.HasStringProperty(PlotX2Property) || Object.HasStringProperty(PlotY2Property))
				return false;
			if (!(x1 && y1 && x2 && y2)) return false;
			Rect = new KingdomPlotRules.PlotRect(
				Object.GetIntProperty(PlotX1Property), Object.GetIntProperty(PlotY1Property),
				Object.GetIntProperty(PlotX2Property), Object.GetIntProperty(PlotY2Property));
			r_KingdomPlotWorks works = Object.GetPart<r_KingdomPlotWorks>();
			if (works != null)
			{
				KingdomPlotRules.PlotRect part = works.Rect();
				if (part.X1 != Rect.X1 || part.Y1 != Rect.Y1 || part.X2 != Rect.X2
					|| part.Y2 != Rect.Y2) return false;
			}
			return Rect.X1 <= Rect.X2 && Rect.Y1 <= Rect.Y2;
		}

		/// <summary>Any persisted plot-coordinate prefix. Used to fail closed when a torn root no
		/// longer qualifies for the survey's valid-plot index.</summary>
		internal static bool HasRectEvidence(GameObject Object)
		{
			return Object != null && (Object.GetPart<r_KingdomPlotWorks>() != null
				|| Object.HasIntProperty(PlotX1Property)
				|| Object.HasIntProperty(PlotY1Property)
				|| Object.HasIntProperty(PlotX2Property)
				|| Object.HasIntProperty(PlotY2Property)
				|| Object.HasStringProperty(PlotX1Property)
				|| Object.HasStringProperty(PlotY1Property)
				|| Object.HasStringProperty(PlotX2Property)
				|| Object.HasStringProperty(PlotY2Property));
		}

		/// <summary>Every plot already laid out in a zone, finished or still rising. The road
		/// budget and the lane rule are both reckoned against this.</summary>
		public static List<KingdomPlotRules.PlotRect> ReadPlots(Zone Z)
		{
			List<KingdomPlotRules.PlotRect> plots = new List<KingdomPlotRules.PlotRect>();
			if (Z == null)
			{
				return plots;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			if (TryReadReservedPlots(Z, survey, out plots)) return plots;
			// Ambiguous handoff geometry is a temporary closed reservation, not free ground. A
			// full-zone sentinel makes budget, siting, and road wear all refuse until recovery.
			plots.Clear();
			plots.Add(new KingdomPlotRules.PlotRect(0, 0, Z.Width - 1, Z.Height - 1));
			return plots;
		}

		/// <summary>Stamps a rect onto the object that represents a plot, so the ground reads as
		/// laid out for every later siting.</summary>
		public static void StampRect(GameObject Object, KingdomPlotRules.PlotRect Rect)
		{
			if (Object == null)
			{
				return;
			}
			Object.SetIntProperty(PlotX1Property, Rect.X1);
			Object.SetIntProperty(PlotY1Property, Rect.Y1);
			Object.SetIntProperty(PlotX2Property, Rect.X2);
			Object.SetIntProperty(PlotY2Property, Rect.Y2);
		}

		/// <summary>Stamps the current tier's own ground, and what stands over it, on the object
		/// that represents a plot. Read back by <see cref="TryReadFootprint"/> and
		/// <see cref="RoofOf"/>.</summary>
		public static void StampFootprint(GameObject Object, KingdomPlotRules.PlotRect Footprint, KingdomPlotRules.RoofState Roof)
		{
			if (Object == null)
			{
				return;
			}
			Object.SetIntProperty(FootX1Property, Footprint.X1);
			Object.SetIntProperty(FootY1Property, Footprint.Y1);
			Object.SetIntProperty(FootX2Property, Footprint.X2);
			Object.SetIntProperty(FootY2Property, Footprint.Y2);
			Object.SetIntProperty(PlotRoofProperty, (int)Roof);
		}

		/// <summary>
		/// The ground the building itself stands on. Falls back to the plot rect for anything
		/// raised before tiers declared footprints, which is the honest answer: it filled its
		/// plot, and it still does.
		/// </summary>
		/// <returns>False for an object that is not a plot at all.</returns>
		public static bool TryReadFootprint(GameObject Object, out KingdomPlotRules.PlotRect Footprint)
		{
			Footprint = default(KingdomPlotRules.PlotRect);
			if (Object == null)
			{
				return false;
			}
			bool x1 = Object.HasIntProperty(FootX1Property);
			bool y1 = Object.HasIntProperty(FootY1Property);
			bool x2 = Object.HasIntProperty(FootX2Property);
			bool y2 = Object.HasIntProperty(FootY2Property);
			if (Object.HasStringProperty(FootX1Property)
				|| Object.HasStringProperty(FootY1Property)
				|| Object.HasStringProperty(FootX2Property)
				|| Object.HasStringProperty(FootY2Property)) return false;
			if (x1 || y1 || x2 || y2)
			{
				Zone zone = Object.CurrentZone;
				if (zone == null || !x1 || !y1 || !x2 || !y2) return false;
				Footprint = new KingdomPlotRules.PlotRect(
					Object.GetIntProperty(FootX1Property),
					Object.GetIntProperty(FootY1Property),
					Object.GetIntProperty(FootX2Property),
					Object.GetIntProperty(FootY2Property));
				return KingdomPlotRules.ValidZoneRect(Footprint, zone.Width, zone.Height);
			}
			return TryReadRect(Object, out Footprint);
		}

	}
}
