using System.Text;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// What is true of the fifth rung and of no rung below it. The arcology is a SAME-FOOTPRINT
	/// renovation: rungs four and five both stand on <c>PlotSize.Huge</c>
	/// (Growth/KingdomPlotHeartRules.cs:83-93), so the held rect must not grow, and
	/// <c>Transition="renovate"</c> is not fabric-preserving
	/// (Growth/KingdomArchitectureTransitionRules.cs:59-63) - the moot pavilion, both founder
	/// statues, the rostrum and the gate torch-posts are struck by design. Only the two
	/// doubly-protected placements are asserted by identity: the first basin and the heart
	/// stockpile.
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private KingdomPlotRules.PlotRect ChainRectBeforeArcology;
			private bool ChainRectCaptured;

			/// <summary>The rung-5 analogue of the tier-four survey-stake probe. There is no
			/// annexed ground this time, so the invariant is the opposite one: the four exact
			/// founding stakes are already INSIDE the held rect and must stay exact across a
			/// renovation that changes almost everything around them.</summary>
			private void ProveChainRetainedStakes()
			{
				long tick = Game.TimeTicks;
				RequireChainFoundingRecovery("before the arcology stake probe");
				Require(KingdomPlots.TryHeartRectFor(Zone, 4, out var rung4), "rung-four lot absent");
				Require(KingdomPlots.TryHeartRectFor(Zone, 5, out var rung5), "rung-five lot absent");
				Require(rung4.X1 == rung5.X1 && rung4.Y1 == rung5.Y1
					&& rung4.X2 == rung5.X2 && rung4.Y2 == rung5.Y2,
					"taf-camp-rung5-lot-grew: the fifth rung's lot is not the fourth rung's lot");
				Require(KingdomPlots.TryReadRect(ChainHeart, out ChainRectBeforeArcology),
					"the standing court's rect could not be read");
				ChainRectCaptured = true;
				int stakes = 0;
				foreach (var item in Census().Objects)
				{
					if (item.GetIntProperty(KingdomPlots.HeartStakeProperty) != 1) continue;
					Require(KingdomPlots.IsExactFoundingHeartSurveyStake(System, Zone, item)
						&& item.CurrentCell != null
						&& rung5.Contains(item.CurrentCell.X, item.CurrentCell.Y),
						"taf-camp-rung5-stake-foreign: a heart stake is unauthenticated or outside "
							+ "the held lot");
					stakes++;
				}
				Require(stakes == 4,
					"taf-camp-rung5-stakes-miscount: " + stakes + " survey stake(s) stand");
				Require(Game.TimeTicks == tick && !KingdomSurvey.HasBoundPass,
					"the arcology stake probe changed time or left a bound survey");
				Require(KingdomScenarioJournal.Append("camp-heart-chain-arcology", true,
					"stage=preflight; rung=4; lot=" + Describe(rung4) + "; lot-unchanged=true"
					+ "; exact-stakes=" + stakes + "; rect=" + Describe(ChainRectBeforeArcology)
					+ "; no-turns=true") == null, "arcology preflight journal unavailable");
			}

			/// <summary>The fifth rung stands. Journalled BEFORE the first assertion so a refusal
			/// names its reason rather than arriving as an empty report.</summary>
			private void RequireChainArcology()
			{
				var standing = StandingHeart();
				Require(ChainRectCaptured, "the arcology check has no pre-climb rect");
				Require(KingdomPlots.TryReadRect(standing, out var after),
					"the standing arcology's rect could not be read");
				Require(KingdomScenarioJournal.Append("camp-heart-chain-arcology", true,
					DescribeArcology(standing, after)) == null, "arcology journal unavailable");
				Require(KingdomPlots.HeartRung(Zone) == 5,
					"taf-camp-rung5-rung: the ground does not stand at rung five");
				Require(KingdomUpgrade.DesignKeyOf(standing) == KingdomHostedArcology.ArcologyKey,
					"taf-camp-rung5-design: the standing heart is not the arcology");
				Require(after.X1 == ChainRectBeforeArcology.X1
					&& after.Y1 == ChainRectBeforeArcology.Y1
					&& after.X2 == ChainRectBeforeArcology.X2
					&& after.Y2 == ChainRectBeforeArcology.Y2,
					"taf-camp-rung5-footprint-grew: " + Describe(ChainRectBeforeArcology)
						+ " became " + Describe(after));
				Require(KingdomArchitectureStamper.TryVerifyComplete(standing, Zone,
					out string layoutFailure),
					"taf-camp-rung5-layout-incomplete: " + layoutFailure);
				RequireRetiredCourtChain(standing);
				RequireHostedAuthorityActive(standing);
			}

			/// <summary>Production retires the predecessor when the successor stands
			/// (Growth/KingdomUpgrade.25.HandoverRemoval.cs), so the court is NOT looked for. What
			/// is proved is the chain production binds ground by, and the court's absence is
			/// consulted LAST: a court merely gone has no completed job naming it and no removal
			/// proof, and still refuses.</summary>
			private void RequireRetiredCourtChain(GameObject Standing)
			{
				Require(KingdomConstruction.TryFind(ChainJobId, out var job) && job != null
					&& job.SubjectId == ChainHeartId && job.OutputId == Standing.IDIfAssigned
					&& job.TargetKey == KingdomHostedArcology.ArcologyKey
					&& job.Phase == KingdomConstructionPhase.Complete,
					"taf-camp-rung5-predecessor-unproved: no completed arcology job retires the "
						+ "court " + ChainHeartId);
				Require(KingdomConstruction.HasReceipt(Standing, job)
					&& KingdomUpgrade.IsFunctionallyBuilt(Standing)
					&& XRL.World.Parts.r_KingdomScaffold.HasRemovalProof(Standing, job.SubjectId),
					"taf-camp-rung5-predecessor-unproved: the standing arcology carries no receipt "
						+ "or removal proof naming the court");
				Require(KingdomPlots.TryChainedWorkSuccessor(Zone,
						KingdomCityRules.StableId(ChainHeartId), out var chained)
					&& ReferenceEquals(chained, Standing),
					"taf-camp-rung5-predecessor-unproved: production's own chain reader does not "
						+ "bind this ground to the standing arcology");
				Require(KingdomConstruction.FindGlobalLiveId(ChainHeartId, out var court)
						!= KingdomPhysicalLookupState.Exact && !GameObject.Validate(court),
					"taf-camp-rung5-predecessor-unretired: the great court is still live");
			}

			/// <summary>The one invariant that belongs to this rung alone: handover binds the
			/// realm's hosted-shell authority to the standing arcology and QUARANTINES the job
			/// when it cannot (Growth/KingdomUpgrade.20.HandOver.cs:218-227,
			/// Growth/KingdomHostedArcology.Authority.cs:88-106). The row is read out of its own
			/// durable slots and decoded with production's codec; nothing here writes it.</summary>
			private void RequireHostedAuthorityActive(GameObject Standing)
			{
				KingdomHostedArcologyAuthority row = null;
				for (int slot = 0; slot < 2 && row == null; slot++)
				{
					string encoded = Game.GetStringGameState(
						"r_TAF_HostedArcologyAuthorityV1:" + slot, "");
					if (string.IsNullOrEmpty(encoded)) continue;
					Require(KingdomHostedArcologyReceiptCodec.TryDecodeAuthority(encoded, out row)
						&& row != null, "taf-camp-rung5-authority-torn: slot " + slot
							+ " does not decode");
				}
				Require(row != null && row.Valid()
					&& row.Phase == KingdomHostedAuthorityPhase.Active
					&& row.RealmId == System.RealmId && row.ZoneId == Zone.ZoneID
					&& row.CarrierId == Standing.IDIfAssigned && row.ConstructionJobId == ChainJobId,
					"taf-camp-rung5-authority-unbound: phase=" + row?.Phase + "; carrier="
						+ KingdomScenarioRules.Bounded(row?.CarrierId) + "; job="
						+ KingdomScenarioRules.Bounded(row?.ConstructionJobId));
				Require(KingdomConstruction.TryOwnedActive(System, Zone, out var jobs) && jobs != null,
					"taf-camp-rung5-registry-unreadable: the construction registry refused to be read");
				foreach (var job in jobs)
					Require(job == null || job.Route != KingdomConstructionRoute.HostedArcology,
						"taf-camp-rung5-hosted-lot-started: the climb commissioned a hosted floor");
			}

			private string DescribeArcology(GameObject Standing, KingdomPlotRules.PlotRect After)
			{
				var text = new StringBuilder();
				text.Append("stage=standing; rung=").Append(KingdomPlots.HeartRung(Zone))
					.Append("; design=").Append(KingdomUpgrade.DesignKeyOf(Standing))
					.Append("; standing=").Append(Standing.IDIfAssigned)
					.Append("; retired-court=").Append(ChainHeartId)
					.Append("; rect=").Append(Describe(After))
					.Append("; rect-before=").Append(Describe(ChainRectBeforeArcology))
					.Append("; rect-unchanged=").Append(After.X1 == ChainRectBeforeArcology.X1
						&& After.Y1 == ChainRectBeforeArcology.Y1
						&& After.X2 == ChainRectBeforeArcology.X2
						&& After.Y2 == ChainRectBeforeArcology.Y2)
					.Append("; basin=").Append(ChainBasin?.IDIfAssigned)
					.Append("; basin-capacity=").Append(BasinCapacity(Standing))
					.Append("; store=").Append(StoreId).Append("; brush=21")
					.Append("; job=").Append(ChainJobId)
					// The arcology tier's function:arcology-gateway role has no runtime consumer;
					// it is a census requirement only, so the layout verifier is what proves it.
					.Append("; gateway-role=declarative-only");
				return text.ToString();
			}

			private static string Describe(KingdomPlotRules.PlotRect Rect)
			{
				return Rect.X1 + "," + Rect.Y1 + "," + Rect.X2 + "," + Rect.Y2;
			}
		}
	}
}
