using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The ground the heart will GROW ONTO (native runs 44 and 48). Production's envelope proof
	/// (<c>KingdomArchitectureStamper.EnvelopeGrowth</c>) refuses a successor rect that overlaps
	/// any other plot's RESERVED rect, so every seeded lot must stay clear of every rung this
	/// persona will climb. Run 48 showed the envelopes cannot be PREPARED ahead: production
	/// refuses a successor from a rung that is not standing. They are therefore PREDICTED from
	/// authored geometry only and PROVEN when each rung stands:
	/// <list type="bullet">
	/// <item>rect: <c>KingdomPlots.TryHeartRectFor(Zone, rung)</c>, the pure helper
	/// <c>TryPrepareSuccessor</c> itself takes the accreted rect from -- that rung's authored
	/// tier centred on the rite ground inside the founding survey; never a constant;</item>
	/// <item>lanes: the successor design's authored map resolved by production's own read-only
	/// siting probe (<c>KingdomArchitectureRuntime.TryCreateSitingProbe</c> /
	/// <c>SitingProbe.TryAccept</c>, heart frontage, production's selection context), then the
	/// same authored-lane rule the road ingress verifier uses; never a live receipt;</item>
	/// <item>proof: when a rung stands, its live rect and lanes are read off the real receipt and
	/// must equal the prediction (<c>heart-envelope-check rung=N match=true</c>), else the run
	/// refuses -- the prediction is proven, not trusted.</item>
	/// </list>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private sealed class PredictedEnvelope
			{
				internal KingdomPlotRules.PlotRect Rect;
				internal HashSet<int> Lanes;
				internal string LaneFailure;
			}

			private readonly Dictionary<int, PredictedEnvelope> Predicted =
				new Dictionary<int, PredictedEnvelope>();
			/// <summary>How many envelope cells (successor rects beyond the heart, plus their
			/// lanes) the last candidate search held reserved.</summary>
			internal int ReservedEnvelopeCells;

			/// <summary>The heart's current rect followed by the predicted envelope of every
			/// successor rung up to the sealed target; each envelope's lane cells are added to
			/// LaneCells.</summary>
			internal List<KingdomPlotRules.PlotRect> HeartEnvelopes(KingdomPlotRules.PlotRect HeartRect,
				HashSet<int> LaneCells)
			{
				List<KingdomPlotRules.PlotRect> envelopes = new List<KingdomPlotRules.PlotRect>();
				envelopes.Add(HeartRect);
				string failure;
				KingdomArchitectureIntent standing = null;
				Require(KingdomArchitectureRuntime.TryRead(Heart, out standing, out failure),
					"taf-camp-town-seed-heart-layout: " + KingdomScenarioRules.Bounded(failure));
				string key = KingdomUpgrade.DesignKeyOf(Heart);
				int rung = KingdomPlots.HeartRung(Zone);
				Require(!string.IsNullOrEmpty(key) && rung >= 1,
					"taf-camp-town-seed-heart-rung: the founded heart has no design key or rung");
				int cells = 0;
				while (rung < TargetRung)
				{
					KingdomUpgradeRules.UpgradeChain chain;
					Require(KingdomUpgrade.TryGetChain(key, out chain) && chain != null
						&& !string.IsNullOrEmpty(chain.SuccessorKey),
						"taf-camp-town-seed-heart-chain: " + key + " names no successor");
					KingdomPlotRules.PlotRect rect = default(KingdomPlotRules.PlotRect);
					Require(KingdomPlots.TryHeartRectFor(Zone, rung + 1, out rect),
						"taf-camp-town-seed-heart-envelope: rung " + (rung + 1)
							+ " has no authored ground centred on the rite ground");
					PredictedEnvelope predicted = new PredictedEnvelope { Rect = rect };
					List<ArchitecturePoint> lanes = new List<ArchitecturePoint>();
					if (TryAuthoredLanes(chain.SuccessorKey, standing.LotType, rect, lanes,
						out predicted.LaneFailure))
					{
						predicted.Lanes = new HashSet<int>();
						for (int i = 0; i < lanes.Count; i++)
						{
							int packed = Pack(lanes[i].X, lanes[i].Y);
							predicted.Lanes.Add(packed);
							if (LaneCells.Add(packed)) cells++;
						}
					}
					Predicted[rung + 1] = predicted;
					envelopes.Add(rect);
					cells += rect.Area;
					Evidence.Append("\nheart-envelope rung=").Append(rung + 1).Append("; key=")
						.Append(chain.SuccessorKey).Append("; rect=").Append(rect.X1).Append(',')
						.Append(rect.Y1).Append(' ').Append(rect.X2).Append(',').Append(rect.Y2)
						.Append("; lanes=").Append(predicted.Lanes == null
							? "unresolved: " + KingdomScenarioRules.Bounded(predicted.LaneFailure)
							: lanes.Count.ToString());
					key = chain.SuccessorKey;
					rung++;
				}
				ReservedEnvelopeCells = cells;
				return envelopes;
			}

			/// <summary>The authored public ingress lanes of Key standing on Rect, from the
			/// design's own map through production's read-only siting probe.</summary>
			private bool TryAuthoredLanes(string Key, string LotType, KingdomPlotRules.PlotRect Rect,
				List<ArchitecturePoint> Lanes, out string Failure)
			{
				KingdomArchitectureRuntime.SitingProbe probe;
				ArchitectureLayoutSnapshot snapshot;
				if (!KingdomArchitectureRuntime.TryCreateSitingProbe(System, Zone, Rect, Key,
						LotType, out probe, out Failure)
					|| !probe.TryAccept(Rect, out snapshot, out Failure)) return false;
				LanesOf(snapshot, Rect, Lanes);
				return true;
			}

			/// <summary>PROOF. The rung stands: its live rect and lanes, read off the real
			/// receipt, must equal what the seed predicted from authored geometry.</summary>
			internal void RequireEnvelopeMatches(int Rung, GameObject Standing)
			{
				PredictedEnvelope predicted;
				if (!Predicted.TryGetValue(Rung, out predicted))
				{
					Evidence.Append("\nheart-envelope-check rung=").Append(Rung)
						.Append("; predicted=false");
					return;
				}
				KingdomPlotRules.PlotRect live = default(KingdomPlotRules.PlotRect);
				Require(KingdomPlots.TryReadRect(Standing, out live),
					"taf-camp-town-seed-envelope-unreadable: rung " + Rung + " has no rect");
				bool rectMatch = live.X1 == predicted.Rect.X1 && live.Y1 == predicted.Rect.Y1
					&& live.X2 == predicted.Rect.X2 && live.Y2 == predicted.Rect.Y2;
				HashSet<int> liveLanes = new HashSet<int>();
				AddLanes(Standing, live, liveLanes);
				bool lanesMatch = predicted.Lanes != null && predicted.Lanes.SetEquals(liveLanes);
				Evidence.Append("\nheart-envelope-check rung=").Append(Rung)
					.Append("; match=").Append(rectMatch && (predicted.Lanes == null || lanesMatch))
					.Append("; predicted rect=").Append(predicted.Rect.X1).Append(',')
					.Append(predicted.Rect.Y1).Append(' ').Append(predicted.Rect.X2).Append(',')
					.Append(predicted.Rect.Y2).Append("; live rect=").Append(live.X1).Append(',')
					.Append(live.Y1).Append(' ').Append(live.X2).Append(',').Append(live.Y2)
					.Append("; predicted lanes=").Append(predicted.Lanes == null ? "unresolved"
						: predicted.Lanes.Count.ToString())
					.Append("; live lanes=").Append(liveLanes.Count)
					.Append("; lanes match=").Append(predicted.Lanes == null ? "unpredicted"
						: lanesMatch.ToString());
				Require(rectMatch, "taf-camp-town-seed-envelope-mismatch: rung " + Rung
					+ " stands on a rect the seed did not predict, so the seeded lots may lie on "
					+ "its ground");
				Require(predicted.Lanes == null || lanesMatch,
					"taf-camp-town-seed-envelope-lanes-mismatch: rung " + Rung
						+ " has lanes the seed did not predict");
			}

			/// <summary>Whether a candidate's reserved rect (lot plus road margin, the very rect
			/// production's envelope proof tests) overlaps any envelope.</summary>
			private bool CrowdsEnvelope(KingdomPlotRules.PlotRect Candidate,
				List<KingdomPlotRules.PlotRect> Envelopes)
			{
				KingdomPlotRules.PlotRect reserved = KingdomPlotRules.Reserved(Candidate);
				for (int i = 0; i < Envelopes.Count; i++)
					if (KingdomPlotRules.Overlaps(reserved, Envelopes[i])) return true;
				return false;
			}
		}
	}
}
