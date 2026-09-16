using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal static string Chain(string Verb, XRLGame Game)
		{
			Require(Game != null && Retained != null && Retained.Done && ReferenceEquals(Game, The.Game)
				&& ReferenceEquals(Game, Retained.Game)
				&& ReferenceEquals(The.Player?.CurrentZone, Retained.Zone)
				&& !KingdomScenarioAdvance.Pending && !KingdomScenarioFrames.Pending,
				"paid heart chain requires its completed source camp and no outstanding turns");
			return Retained.ChainStep(Verb);
		}

		private sealed partial class Frame
		{
			private int ChainPhase, ChainFinalRung;
			private string ChainCapitalReport;
			private GameObject ChainHeart, ChainBasin, ChainStore;
			private string ChainHeartId, ChainJobId;
			private int ChainTarget, ChainWater;
			private string ChainFrom, ChainTo;
			private readonly List<GameObject> ChainSupplied = new List<GameObject>();
			private List<GameObject> ChainBrush;
			private string ChainBrushDigest;
			private string ChainSupplyClaim;

			internal string ChainStep(string Verb)
			{
				if (Verb == KingdomCampHeartChainScript.Setup)
				{
					Require(ChainPhase == 0, "chain setup is not repeatable");
					ChainPhase = -1;
					Require(KingdomScenarioScript.TryRead(out var sealedScript, out string sealedFailure),
						sealedFailure);
					ChainFinalRung = KingdomCampHeartChainScript.SealedTargetRung(sealedScript);
					Require(ChainFinalRung == 4 || ChainFinalRung == 5,
						"the sealed paid heart chain names no exact final rung");
					ChainHeart = StandingHeart();
					Require(KingdomPlots.HeartRung(Zone) == 2, "chain did not start at paid rung two");
					var blocked = KingdomUpgrade.Assess(System, Zone, ChainHeart, Census(), 50, false);
					Require(blocked.Verdict == KingdomUpgradeRules.UpgradeVerdict.StageTooLow,
						"unsupported camp did not refuse the Town prerequisite: " + blocked.Verdict);
					Require(KingdomArchitectureStamper.TryExactAnchoredComponent(ChainHeart, Zone,
						KingdomPlots.HeartBasinRole, out ChainBasin, out string failure), failure);
					var units = ContentUnits(out ChainBrush);
					ChainBrushDigest = KingdomCampHeartSaveSnapshotCodec.CustodyDigest(units);
					Require(units.Count == 21 && ChainBrushDigest != null, "chain sentinel brush census differs");
					SeedChainSupport();
					ClearChainFounder();
					ChainPhase = 1;
					return "paid-heart-chain setup; stage-refused=true; synthetic-residents=50; synthetic-homes=18"
						+ "; synthetic-water=3600; synthetic-legacy-water-courts=8; synthetic-food=1728; synthetic-knowledge=true; synthetic-store-identities=true"
						+ "; housing-calendar-frontier=true; no-improvement-driven=true; " + ChainState();
				}
				if (Verb == KingdomCampHeartChainScript.Capital)
				{
					Require(ChainPhase == 8 && ChainFinalRung == 5,
						"the capital seed runs exactly once, after the rung-four leg");
					SeedChainCapital(); ChainPhase = 9;
					return "paid-heart-chain capital; " + ChainCapitalReport + "; " + ChainState();
				}
				if (Verb == KingdomCampHeartChainScript.Supply)
				{
					Require(ChainPhase == 1 || ChainPhase == 4 || ChainPhase == 9,
						"material supply requires the completed source tent, moot yard or crowned court");
					RequireChainSupport();
					if (ChainPhase == 1) HoldChainTent();
					SupplyChain(ChainPhase == 1 ? 3 : ChainPhase == 4 ? 4 : 5); ChainPhase++;
					return "paid-heart-chain supplied; target=" + ChainTarget + "; " + ChainState();
				}
				Require(Verb == KingdomCampHeartChainScript.Check, "unknown paid heart chain verb");
				if (ChainPhase == 2 || ChainPhase == 5 || ChainPhase == 10)
				{
					CheckChainPaid(); ChainPhase++;
					return "paid-heart-chain paid; target=" + ChainTarget + "; job=" + ChainJobId
						+ "; water=" + ChainWater + "; materials=" + ChainSupplyClaim + "; " + ChainState();
				}
				if (ChainPhase == 3 || ChainPhase == 6 || ChainPhase == 11)
				{
					CheckChainComplete(); ChainPhase++;
					return "paid-heart-chain completed; rung=" + ChainTarget + "; job=" + ChainJobId
						+ "; effects-settled=true; track-retained=true; " + ChainState();
				}
				if (ChainPhase == 7)
				{
					CheckChainComplete();
					Require(KingdomPlots.RecoverFoundingHeart(System, Zone), "rung-four next-day recovery refused");
					CheckChainComplete(); ChainPhase = 8;
					if (ChainFinalRung == 5)
						return "paid-heart-chain rung-four leg complete; paid-rungs=1->2->3->4"
							+ "; next-day-recovery=true; track-retained=true; " + ChainState();
					return "paid-heart-chain complete; paid-rungs=1->2->3->4; next-day-recovery=true"
						+ "; track-retained=true; ordinary-acceptance=false; save-load=untested; " + ChainState();
				}
				Require(ChainPhase == 12 && ChainFinalRung == 5, "paid heart chain check out of order");
				CheckChainComplete();
				Require(KingdomPlots.RecoverFoundingHeart(System, Zone), "rung-five next-day recovery refused");
				CheckChainComplete();
				RequireChainArcology();
				ChainPhase = 13;
				return "paid-heart-chain arcology; paid-rungs=1->2->3->4->5; next-day-recovery=true"
					+ "; footprint-unchanged=true; predecessor-retired=true; authority-active=true"
					+ "; track-retained=true; ordinary-acceptance=false; save-load=untested; " + ChainState();
			}

			private string ChainState()
			{
				return "stage=" + System.Stage + "; population=" + System.Population
					+ "; heart=" + StandingHeart().IDIfAssigned + "; store=" + StoreId
					+ "; basin=" + ChainBasin.IDIfAssigned + "; brush=21; turns=" + Game.Turns;
			}
		}
	}
}
