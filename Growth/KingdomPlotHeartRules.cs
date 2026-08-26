using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- The heart -------------------------------------------------------------------

		/// <summary>
		/// How heavily the rite ground counts against the works when the heart is reckoned with
		/// nothing standing on it. One: the rite ground is where the settlement started and never
		/// stops mattering, but a city of forty buildings has moved, and the heart moves with it.
		/// <para>
		/// This is the floor of the ladder, not the whole of it. Once the heart's own great work
		/// stands on that ground, <see cref="HeartWeightForRung"/> is what the rite ground counts
		/// for, and the settled centre is drawn back onto the monument as it rises.
		/// </para>
		/// </summary>
		public const int RiteHeartWeight = 1;

		// --- The heart's own ladder -------------------------------------------------------

		/// <summary>
		/// The four rungs of the heart, in order, by design key. The heart is ONE plot that grows
		/// with its rung &mdash; basin, then the waterstone laid around it, then the moot yard
		/// raised over that, then the great court raised around the yard &mdash; and each rung is
		/// built OVER the last rather than in place of it, so the ground reads as history.
		/// <para>
		/// Keys rather than an authored attribute, deliberately and for this wave only: the
		/// catalogue loader hands <c>KingdomPlots.RegisterSpec</c> a fixed set of attributes, and
		/// a fifth one is a change to the shared loader rather than to the heart. A third-party
		/// file re-declaring one of these keys owns that rung entirely (merge-by-key), which is
		/// how the ladder is retheme-able today; authoring a NEW rung wants the
		/// <c>Heart="yes"</c> attribute noted in the wave report.
		/// </para>
		/// </summary>
		public static readonly string[] HeartRungKeys = new string[4]
		{
			"heartbasin",
			"heartwaterstone",
			"heartmoot",
			"heartcourt"
		};

		/// <summary>Which rung of the heart a design key is, one-based.</summary>
		/// <returns>Zero for every design that is not the heart, which is all but four of
		/// them.</returns>
		public static int HeartRungOf(string Key)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return 0;
			}
			for (int i = 0; i < HeartRungKeys.Length; i++)
			{
				if (HeartRungKeys[i] == Key)
				{
					return i + 1;
				}
			}
			return 0;
		}

		/// <summary>The design key of one rung, one-based; null outside the ladder.</summary>
		public static string HeartKeyForRung(int Rung)
		{
			if (Rung < 1 || Rung > HeartRungKeys.Length)
			{
				return null;
			}
			return HeartRungKeys[Rung - 1];
		}

		/// <summary>
		/// The plot tier each rung stands on: S at the founding, then M, L, and XL. The same
		/// ladder the stage gate already climbs (<see cref="MaxSizeForStage"/>), which is why the
		/// heart needs no gate of its own &mdash; a settlement that cannot lay a great plot cannot
		/// raise the great court either, and is told so in the words it already knows.
		/// </summary>
		public static PlotSize HeartSizeForRung(int Rung)
		{
			switch (Rung)
			{
				case 1:
					return PlotSize.Small;
				case 2:
					return PlotSize.Medium;
				case 3:
					return PlotSize.Large;
				case 4:
					return PlotSize.Huge;
				default:
					return PlotSize.None;
			}
		}

		/// <summary>
		/// What the rite ground counts for when the heart is reckoned, by the rung standing on it.
		/// One at the basin &mdash; a tin bowl on bare ground is not a monument, and the heart
		/// still walks after the city, which is correct. Four, twelve, and forty as the great work
		/// rises, until the settled centre is drawn back onto it and the city visibly re-centres
		/// on the thing it built.
		/// <para>
		/// Qud's own shape: Ezra is described as an "archaeological and cultural outgrowth" of the
		/// Tomb of the Eaters &mdash; the village grew around the great work, not beside it.
		/// </para>
		/// </summary>
		public static int HeartWeightForRung(int Rung)
		{
			switch (Rung)
			{
				case 2:
					return 4;
				case 3:
					return 12;
				case 4:
					return 40;
				default:
					return RiteHeartWeight;
			}
		}

		/// <summary>
		/// The whole ground the heart is surveyed for at the founding rite: the final rung's plot,
		/// centred on the rite ground and slid whole until it lies inside the zone's interior.
		/// Nothing is claimed, spent, or reserved by this &mdash; it is the founder's ambition
		/// paced out, and every later rung is staked inside it.
		/// </summary>
		/// <returns>False for a zone with no interior to survey, in which case the settlement
		/// simply has no surveyed heart and every plot is sited exactly as it was before.</returns>
		public static bool TrySurveyedHeart(int RiteX, int RiteY, int Width, int Height, out PlotRect Survey)
		{
			Survey = default(PlotRect);
			if (!TryInterior(Width, Height, out var interior)
				|| !TryDimensions(HeartSizeForRung(HeartRungKeys.Length), out var surveyWidth, out var surveyHeight))
			{
				return false;
			}
			return TryCentred(interior, RiteX, RiteY, surveyWidth, surveyHeight, out Survey);
		}

		/// <summary>
		/// One rung's plot: a rect of that rung's tier, centred on the rite ground and slid whole
		/// until it lies inside the surveyed ground. The basin's own ground therefore stays inside
		/// every rung above it, which is what makes the rungs accrete rather than replace.
		/// </summary>
		/// <returns>False when the tier does not fit the surveyed ground at all.</returns>
		public static bool TryHeartRect(PlotRect Survey, int RiteX, int RiteY, PlotSize Size, out PlotRect Rect)
		{
			Rect = default(PlotRect);
			if (!TryDimensions(Size, out var width, out var height))
			{
				return false;
			}
			return TryCentred(Survey, RiteX, RiteY, width, height, out Rect);
		}

		/// <summary>
		/// A rect of the given span centred on a point and then slid &mdash; never shrunk &mdash;
		/// until it lies wholly inside Bounds. Deterministic: the same point and bounds always
		/// give the same rect.
		/// </summary>
		/// <returns>False when the span does not fit inside Bounds at all.</returns>
		public static bool TryCentred(PlotRect Bounds, int X, int Y, int Width, int Height, out PlotRect Rect)
		{
			Rect = default(PlotRect);
			if (Width < 1 || Height < 1 || Bounds.Width < Width || Bounds.Height < Height)
			{
				return false;
			}
			int x1 = X - (Width - 1) / 2;
			int y1 = Y - (Height - 1) / 2;
			if (x1 < Bounds.X1)
			{
				x1 = Bounds.X1;
			}
			if (y1 < Bounds.Y1)
			{
				y1 = Bounds.Y1;
			}
			if (x1 + Width - 1 > Bounds.X2)
			{
				x1 = Bounds.X2 - Width + 1;
			}
			if (y1 + Height - 1 > Bounds.Y2)
			{
				y1 = Bounds.Y2 - Height + 1;
			}
			Rect = new PlotRect(x1, y1, x1 + Width - 1, y1 + Height - 1);
			return true;
		}

	}
}
