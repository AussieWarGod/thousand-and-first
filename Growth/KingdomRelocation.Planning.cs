using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		/// <summary>Mutation-free plan for the exact yielding lots which block one heart rung.</summary>
		internal static bool TryPreparePlan(KingdomSystem System, Zone Zone, GameObject Heart,
			string SuccessorKey, IDictionary<string, KingdomPlotRules.PlotRect> Overrides,
			out PreparedPlan Prepared, out string Failure)
		{
			Prepared = null; Failure = null;
			if (System == null || !System.Founded || Zone == null || !GameObject.Validate(Heart)
				|| Heart.CurrentZone != Zone || !System.ClaimedZones.Contains(Zone.ZoneID)
				|| !KingdomPlots.IsHeartPlot(Heart) || string.IsNullOrEmpty(SuccessorKey)
				|| string.IsNullOrEmpty(System.RealmId))
			{
				Failure = "The exact owned heart authority cannot be proved.";
				return false;
			}
			int rung = KingdomPlotRules.HeartRungOf(SuccessorKey);
			KingdomPlotRules.PlotRect target;
			if (rung < 1 || !KingdomPlots.TryHeartRectFor(Zone, rung, out target))
			{
				Failure = "The next heart rung has no lawful surveyed ground.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Zone) ?? KingdomSurvey.Take(Zone, System);
			string heartLot = Heart.GetStringProperty(KingdomPlots.PlotIdProperty);
			List<GameObject> blockers = HeartBlockers(survey, Heart, heartLot, target, out Failure);
			if (blockers == null) return false;
			if (blockers.Count == 0)
			{
				Failure = "No yielding plot blocks this heart rung.";
				return false;
			}
			if (!HeartGroundLawful(Zone, target, heartLot, blockers, out Failure)) return false;
			long now = The.Game == null ? 0L : The.Game.TimeTicks;
			List<KingdomPlotRules.PlotRect> fixedRects = FixedRects(survey, heartLot,
				blockers, target);
			List<KingdomLayoutRules.LayoutMark> marks = FixedMarks(survey, heartLot, blockers);
			List<KingdomRelocationMove> moves = new List<KingdomRelocationMove>();
			for (int i = 0; i < blockers.Count; i++)
			{
				GameObject root = blockers[i];
				if (!KingdomPlots.TryReadRect(root, out KingdomPlotRules.PlotRect source)
					|| !TryChooseDestination(System, Zone, root, source, fixedRects, marks,
						Overrides, out KingdomPlotRules.PlotRect destination, out Failure)
					|| !TryFreezeMove(System, Zone, survey, root, destination, now,
						out KingdomRelocationMove move, out Failure)) return false;
				moves.Add(move); fixedRects.Add(destination); RemoveRect(fixedRects, source, target);
				KingdomLayoutRules.LayoutPurpose purpose = KingdomLayoutRules.LayoutPurpose.Unknown;
				if (KingdomData.TryGetBuilding(move.BuildKey, out KingdomRules.BuildEntry entry))
					purpose = KingdomLayout.PurposeOfEntry(entry);
				marks.Add(new KingdomLayoutRules.LayoutMark(destination.CenterX,
					destination.CenterY, purpose));
			}
			KingdomRelocationReceipt receipt = new KingdomRelocationReceipt
			{
				Schema = KingdomRelocationRules.Schema, PlanId = Guid.NewGuid().ToString("N"),
				ZoneId = Zone.ZoneID, RealmId = System.RealmId, HeartId = Heart.IDIfAssigned,
				SuccessorKey = SuccessorKey, HeartGround = Frozen(target), CreatedTick = now,
				Generation = 1, CurrentMove = 0, Phase = KingdomRelocationPhase.Active,
				Moves = moves
			};
			if (!KingdomRelocationRules.Valid(receipt, out Failure)
				|| !KingdomRelocationCodec.TryEncode(receipt, out _, out Failure)) return false;
			Prepared = new PreparedPlan { Receipt = receipt, Preview = Preview(receipt) };
			return true;
		}

		private static List<GameObject> HeartBlockers(KingdomSurvey Survey, GameObject Heart,
			string HeartLot, KingdomPlotRules.PlotRect Target, out string Failure)
		{
			Failure = null; List<GameObject> result = new List<GameObject>();
			for (int i = 0; i < Survey.PlotRoots.Count; i++)
			{
				GameObject item = Survey.PlotRoots[i];
				if (!GameObject.Validate(item) || ReferenceEquals(item, Heart)
					|| item.GetStringProperty(KingdomPlots.PlotIdProperty) == HeartLot
					|| !KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect laid)
					|| !KingdomPlotRules.Overlaps(Target, KingdomPlotRules.Reserved(laid))) continue;
				if (!KingdomPlots.IsYielding(item))
				{
					Failure = "The heart is protected from taking the unyielding "
						+ KingdomDesign.ReferenceFor(item, item.ShortDisplayNameStripped) + ".";
					return null;
				}
				result.Add(item);
			}
			result.Sort(delegate(GameObject a, GameObject b)
			{
				KingdomPlots.TryReadRect(a, out KingdomPlotRules.PlotRect ar);
				KingdomPlots.TryReadRect(b, out KingdomPlotRules.PlotRect br);
				int compared = ar.Y1.CompareTo(br.Y1);
				if (compared != 0) return compared;
				compared = ar.X1.CompareTo(br.X1);
				return compared != 0 ? compared : string.CompareOrdinal(
					a.GetStringProperty(KingdomPlots.PlotIdProperty),
					b.GetStringProperty(KingdomPlots.PlotIdProperty));
			});
			if (result.Count > KingdomRelocationRules.MaxMoves)
			{
				Failure = "The heart ring call exceeds its bounded number of yielding lots.";
				return null;
			}
			return result;
		}
	}
}
