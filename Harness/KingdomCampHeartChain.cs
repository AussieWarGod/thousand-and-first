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
			private int ChainPhase;
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
					SupplyChain(3);
					ChainPhase = 1;
					return "paid-heart-chain setup; stage-refused=true; synthetic-residents=50; synthetic-homes=18"
						+ "; synthetic-water=3600; synthetic-food=1728; synthetic-knowledge=true"
						+ "; housing-calendar-frontier=true; no-improvement-driven=true; " + ChainState();
				}
				if (Verb == KingdomCampHeartChainScript.Supply)
				{
					Require(ChainPhase == 3, "court supply requires independently completed moot yard");
					SupplyChain(4); ChainPhase = 4;
					return "paid-heart-chain supplied; target=4; " + ChainState();
				}
				Require(Verb == KingdomCampHeartChainScript.Check, "unknown paid heart chain verb");
				if (ChainPhase == 1 || ChainPhase == 4)
				{
					CheckChainPaid(); ChainPhase++;
					return "paid-heart-chain paid; target=" + ChainTarget + "; job=" + ChainJobId
						+ "; water=" + ChainWater + "; materials=" + ChainSupplyClaim + "; " + ChainState();
				}
				if (ChainPhase == 2 || ChainPhase == 5)
				{
					CheckChainComplete(); ChainPhase++;
					return "paid-heart-chain completed; rung=" + ChainTarget + "; job=" + ChainJobId
						+ "; effects-settled=true; " + ChainState();
				}
				Require(ChainPhase == 6, "paid heart chain check out of order");
				CheckChainComplete();
				Require(KingdomPlots.RecoverFoundingHeart(System, Zone), "rung-four next-day recovery refused");
				CheckChainComplete(); ChainPhase = 7;
				return "paid-heart-chain complete; paid-rungs=1->2->3->4; next-day-recovery=true"
					+ "; ordinary-acceptance=false; save-load=untested; " + ChainState();
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
