using System.Text;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal sealed partial class KingdomRecruitmentNativeChecks
	{
		private long WaitingDue, WaitingStarted;
		private int WaitingWater;

		internal void BeginWaiting(Zone Zone, StringBuilder Evidence)
		{
			WaitingStarted = Game.TimeTicks;
			WaitingWater = KingdomGrowth.CountStoredWater(Zone);
			Require(System.NextArrivalTick > WaitingStarted, "recruitment wait is already due at setup");
			Capture(); SetRegards(null);
			Evidence.Append("; recruitment-wait begin=true all-hostile=true initial-estimate=").Append(System.NextArrivalTick);
		}

		internal void CheckWaiting(Zone Zone, StringBuilder Evidence)
		{
			try
			{
				var growth = System.LifecycleBook.Growth;
				Require(Game.TimeTicks > WaitingStarted && growth.ArrivalDebtRanges.Count > 0,
					"ordinary turns did not retain recruitment debt");
				// Founding's estimate precedes the first healthy epoch. Bind the actual
				// cadence head before observing recovery; never rewrite the production clock.
				WaitingDue = growth.ArrivalDebtRanges[0].FirstDueTick;
				Evidence.Append("; recruitment-wait observed-head=").Append(growth.ArrivalDebtRanges[0].FirstOrdinal)
					.Append(" due=").Append(WaitingDue).Append(" epoch-start=").Append(growth.ArrivalRateEpochStartedTick)
					.Append(" interval=").Append(growth.ArrivalIntervalTicks)
					.Append(" population=").Append(System.Population)
					.Append(" candidate=").Append(growth.ArrivalCandidate?.Id ?? "none");
				Require(WaitingDue > WaitingStarted && WaitingDue <= Game.TimeTicks
					&& growth.ArrivalRateEpoch == 1L && growth.ArrivalDebtRanges[0].FirstOrdinal == 1UL
					&& WaitingDue == growth.ArrivalRateEpochStartedTick + growth.ArrivalIntervalTicks
					&& growth.ArrivalOpportunity == null && growth.ArrivalCandidate == null
					&& growth.ArrivalOp == null && System.Population == 0,
					"empty recruitment spent, replaced or materialized the waiting head");
				Require(System.NextArrivalTick == growth.NextArrivalTick
					&& KingdomLifecycleRules.CanOwnGrowthAuthority(growth, System.CurrentSettlementId),
					"empty recruitment left the arrival clock or authority invalid");
				// Roads run last in growth, after staffing, industry, lodging and plot work.
				// Read their real persisted checkpoint; never call or advance that subsystem here.
				Require(long.TryParse(Zone.GetZoneProperty(KingdomRoads.WalkedProperty, null),
					out long roadTick) && roadTick >= WaitingDue,
					"empty recruitment skipped downstream city work after the due tick");
				Require(KingdomGrowth.CountStoredWater(Zone) == WaitingWater,
					"empty recruitment spent arrival water");
				string need = KingdomReports.NextNeed(System, Zone);
				Require(need.Contains("No settlers are willing to come:") && need.Contains("No roof stands."),
					"recruitment wait hid its cause or the ordinary housing need");
				Evidence.Append("; recruitment-wait no-body=true debt-retained=true water-unspent=true")
					.Append(" downstream-roads-tick=").Append(roadTick)
					.Append(" due=").Append(WaitingDue).Append(" status-explained=true");
				ProbeStoryPolicy(Evidence);
			}
			finally { Restore(); }
			Evidence.Append(" relations-restored=true");
		}
	}
}
