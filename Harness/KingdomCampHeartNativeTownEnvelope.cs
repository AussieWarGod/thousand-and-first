using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The ground the heart will GROW ONTO (native run 44). The moot yard refused NoGroundToGrow
	/// because a seeded tent's reserved lane lay inside the heart's rung-3 envelope: production's
	/// envelope proof (<c>KingdomArchitectureStamper.EnvelopeGrowth</c>) refuses a successor rect
	/// that overlaps any other plot's RESERVED rect. So the seed derives, for every rung this
	/// persona will climb, the exact successor envelope production itself prepares from the
	/// authored map (<c>KingdomUpgrade.TryGetChain</c> for the successor key,
	/// <c>KingdomArchitectureRuntime.TryPrepareSuccessor</c> intent to intent), plus each
	/// envelope's own authored public ingress lanes, and keeps every seeded lot's reserved rect
	/// and lanes clear of all of it. Nothing here is a constant margin.
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			/// <summary>How many envelope cells (successor rects beyond the heart, plus their
			/// lanes) the last candidate search held reserved.</summary>
			internal int ReservedEnvelopeCells;

			/// <summary>The heart's current rect followed by the envelope of every successor rung
			/// up to the sealed target; each envelope's lane cells are added to LaneCells.</summary>
			internal List<KingdomPlotRules.PlotRect> HeartEnvelopes(KingdomPlotRules.PlotRect HeartRect,
				HashSet<int> LaneCells)
			{
				List<KingdomPlotRules.PlotRect> envelopes = new List<KingdomPlotRules.PlotRect>();
				envelopes.Add(HeartRect);
				string failure;
				KingdomArchitectureIntent intent = null;
				Require(KingdomArchitectureRuntime.TryRead(Heart, out intent, out failure),
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
					KingdomArchitectureIntent next = null;
					Require(KingdomArchitectureRuntime.TryPrepareSuccessor(System, Zone, intent,
						chain.SuccessorKey, out next, out failure) && next != null,
						"taf-camp-town-seed-heart-envelope: " + chain.SuccessorKey + " from " + key
							+ ": " + KingdomScenarioRules.Bounded(failure));
					ArchitectureLayoutSnapshot snapshot = null;
					Require(KingdomArchitectureRuntime.TryDecode(next, out snapshot, out failure),
						"taf-camp-town-seed-heart-snapshot: " + KingdomScenarioRules.Bounded(failure));
					List<ArchitecturePoint> lanes = new List<ArchitecturePoint>();
					LanesOf(snapshot, next.Rect, lanes);
					for (int i = 0; i < lanes.Count; i++)
						if (LaneCells.Add(Pack(lanes[i].X, lanes[i].Y))) cells++;
					envelopes.Add(next.Rect);
					cells += next.Rect.Area;
					Evidence.Append("\nheart-envelope rung=").Append(rung + 1).Append("; key=")
						.Append(chain.SuccessorKey).Append("; rect=").Append(next.Rect.X1)
						.Append(',').Append(next.Rect.Y1).Append(' ').Append(next.Rect.X2)
						.Append(',').Append(next.Rect.Y2).Append("; lanes=").Append(lanes.Count);
					intent = next;
					key = chain.SuccessorKey;
					rung++;
				}
				ReservedEnvelopeCells = cells;
				return envelopes;
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
