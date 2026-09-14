using System.Text;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal sealed partial class KingdomRecruitmentNativeChecks
	{
		private long WaitingDue;
		private int WaitingWater;

		internal void BeginWaiting(Zone Zone, StringBuilder Evidence)
		{
			WaitingDue = System.NextArrivalTick;
			WaitingWater = KingdomGrowth.CountStoredWater(Zone);
			Require(WaitingDue > Game.TimeTicks, "recruitment wait is already due at setup");
			Capture(); SetRegards(null);
			Evidence.Append("; recruitment-wait begin=true all-hostile=true due=").Append(WaitingDue);
		}

		internal void CheckWaiting(Zone Zone, StringBuilder Evidence)
		{
			try
			{
				var growth = System.LifecycleBook.Growth;
				Require(Game.TimeTicks > WaitingDue && growth.ArrivalDebtRanges.Count > 0,
					"ordinary turns did not retain recruitment debt");
				Require(growth.ArrivalDebtRanges[0].FirstOrdinal == 1UL
					&& growth.ArrivalDebtRanges[0].FirstDueTick == WaitingDue
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
			}
			finally { Restore(); }
			Evidence.Append(" relations-restored=true");
		}
	}
}
