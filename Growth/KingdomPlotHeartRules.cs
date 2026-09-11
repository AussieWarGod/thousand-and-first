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
		/// The five rungs of the heart, in order, by design key. The heart is ONE plot that grows
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
		public static readonly string[] HeartRungKeys = new string[5]
		{
			"heartbasin",
			"heartwaterstone",
			"heartmoot",
			"heartcourt",
			"arcology"
		};

		/// <summary>Which rung of the heart a design key is, one-based.</summary>
		/// <returns>Zero for every design that is not the heart, which is all but five of
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
				case 5:
					return PlotSize.Huge;
				default:
					return PlotSize.None;
			}
		}

		/// <summary>
		/// Whether the two ENDPOINTS of an authored heart transition are a lawful adjacent step
		/// on this ladder: the rungs are adjacent, and each end stands on the tier
		/// <see cref="HeartSizeForRung"/> gives its own rung.
		/// <para>
		/// Rungs one to four each happen to stand on the tier numbered like themselves, which is
		/// why a rung-number comparison passed for so long. Rungs four and five both stand on
		/// Huge: the catalogue puts the great court and the arcology in one XL binding and marks
		/// the arcology a renovation, so the last step is a same-footprint renovation of the
		/// ground the court already holds. Asking the mapping rather than the number is what
		/// makes that step expressible without loosening anything below it.
		/// </para>
		/// <para>
		/// Endpoints only. Ownership, ground, rects, facing, anchors, basin custody and every
		/// other proof stay where they are; this answers one question and no more.
		/// </para>
		/// </summary>
		/// <param name="BeforeTier">The standing lot's tier as its integer value.</param>
		/// <param name="AfterTier">The successor lot's tier as its integer value.</param>
		public static bool HeartRungEndpointsAdmit(int BeforeRung, int AfterRung,
			int BeforeTier, int AfterTier)
		{
			PlotSize before = HeartSizeForRung(BeforeRung);
			PlotSize after = HeartSizeForRung(AfterRung);
			// Both ends must be ON the ladder. Without this, two rungs off the ladder would both
			// answer PlotSize.None and match each other, admitting a step that does not exist.
			return before != PlotSize.None && after != PlotSize.None
				&& BeforeRung >= 1 && AfterRung == BeforeRung + 1
				&& BeforeTier == (int)before && AfterTier == (int)after;
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
				case 5:
					return 80;
				default:
					return RiteHeartWeight;
			}
		}

		/// <summary>
		/// How many drams the first basin holds at each rung, one-based: sixteen at the rite
		/// ground, forty-eight at the waterstone, a hundred and sixty at the moot, five hundred
		/// and twelve at the court, a thousand and twenty-four at the arcology.
		/// <para>
		/// The basin is the settlement's FIRST water store and, at a camp, its only one. The
		/// ladder is therefore tuned against the stage gates it feeds: sixteen is exactly the
		/// Steading capacity gate, so five settlers drinking from a basin can become a steading
		/// without a cask rack, which is the whole reason a camp has a store at all. Every rung
		/// below the top then stands SHORT of the next gate &mdash; 48 under Village's 64, 160
		/// under Town's 256, 512 under City's 1024 &mdash; so the basin always helps a settlement
		/// climb and never climbs it alone.
		/// </para>
		/// <para>
		/// It is tuned against the leak law as well. A store at the wear ceiling loses its
		/// capacity divided by fifty every day, so a neglected basin at each rung sheds less than
		/// the daily water bill of the rung that holds it: a leak thins the cushion the settlement
		/// keeps and can never outrun what the settlement makes.
		/// </para>
		/// </summary>
		/// <param name="Rung">The heart's rung, one-based, as
		/// <see cref="HeartRungOf"/> reads it.</param>
		/// <returns>Zero off the ladder, which is every design but the heart's five. Nothing off
		/// the ladder ever has a water capacity reckoned from here, and a zero is never written
		/// onto a vessel: the reconciler treats it as "not applicable" and leaves the vessel
		/// exactly as it found it.</returns>
		public static int HeartBasinCapacityForRung(int Rung)
		{
			switch (Rung)
			{
				case 1:
					return 16;
				case 2:
					return 48;
				case 3:
					return 160;
				case 4:
					return 512;
				case 5:
					return 1024;
				default:
					return 0;
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
		/// every rung above it. That preserves the rite anchor while each authored successor remains
		/// free to renovate the standing interior, expand into proved ground, or do both.
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

		/// <summary>
		/// Whether a finished receipt's rung may be settled over the rung already standing on the
		/// ground. The ladder only accretes: a receipt naming a LOWER rung than the one standing
		/// describes ground that no longer exists, and settling it would tell the settlement it
		/// had un-built its own heart.
		/// <para>
		/// Deliberately not a plus-one rule. That the ladder climbs one rung at a time is gated
		/// UPSTREAM, where both ends of the transition are known
		/// (<c>KingdomArchitectureRuntime.TryPrepareSuccessor</c> admits accretion only when the
		/// successor's rung is exactly the predecessor's plus one, and only when the zone already
		/// stands at the predecessor's rung). By the time a receipt is being settled its
		/// predecessor is gone, so re-deriving that gate here would guess at a number this
		/// helper cannot see. What it CAN see, and all it judges, is the direction.
		/// </para>
		/// </summary>
		/// <param name="Rung">The rung the finished receipt names, one-based.</param>
		/// <param name="Standing">The rung the ground already stands at; zero before any rung.
		/// </param>
		public static bool RungMaySettle(int Rung, int Standing)
		{
			return Rung > 0 && Rung >= Standing;
		}

	}
}
