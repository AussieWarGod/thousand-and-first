using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private sealed class FoundingHeartAllocationFence
		{
			private readonly FoundingHeartReservationStore Store = new FoundingHeartReservationStore();
			private readonly FoundingHeartContext Context;
			private readonly KingdomFoundingHeartPlan Plan;
			private readonly KingdomSystem System;
			private readonly Zone Zone;
			private readonly ZoneManager Manager;
			private readonly string Wire, Realm, Settlement;

			internal FoundingHeartAllocationFence(Zone Zone, FoundingHeartContext Context)
			{
				this.Zone = Zone; this.Context = Context; Plan = Context?.Plan;
				Wire = KingdomFoundingHeartRules.Encode(Plan);
				Manager = The.ZoneManager; System = Store.Game?.GetSystem<KingdomSystem>();
				Realm = System?.CurrentRealmId; Settlement = System?.CurrentSettlementId;
			}

			internal bool Current
			{
				get
				{
					try
					{
						return Store.Current && Zone != null && Wire != null && Context != null
							&& ReferenceEquals(Context.Plan, Plan) && Context.Receipt == Wire
							&& KingdomFoundingHeartRules.Encode(Plan) == Wire && Zone.ZoneID == Plan.ZoneId
							&& ReferenceEquals(The.ZoneManager, Manager) && ReferenceEquals(Store.Game.ZoneManager, Manager)
							&& ReferenceEquals(Store.Game.GetSystem<KingdomSystem>(), System) && System != null
							&& System.CurrentRealmId == Realm && System.CurrentSettlementId == Settlement
							&& TryFoundingHeartTransaction(System, Zone, out string transaction) && transaction == Plan.TransactionId
							&& Zone.GetZoneProperty(FoundingHeartReceiptProperty, null) == Wire
							&& ExactFoundingHeartZoneTruth(Zone, Plan) && Store.CheckPlan(Plan);
					}
					catch { return false; }
				}
			}

			internal GameObject Create(string Blueprint)
			{
				if (!Current) return null;
				GameObject observed = null;
				string originalId = null;
				int observations = 0;
				bool owned = false;
				GameObject result;
				try
				{
					result = GameObject.Create(Blueprint, BeforeObjectCreated: candidate => {
						observations++;
						observed = candidate;
						originalId = candidate?.IDIfAssigned;
						owned = observations == 1 && UnplacedFoundingHeartOutput(candidate, Blueprint) && Current;
					});
				}
				catch { return null; }
				// Native creation events can return a pre-existing ReplacementObject. Custody alone
				// cannot authorize it: only the exact pre-event factory reference may acquire our ID.
				return owned && observations == 1 && ReferenceEquals(result, observed)
					&& UnplacedFoundingHeartOutput(result, Blueprint) && result.IDIfAssigned == originalId
					&& Current ? result : null;
			}
		}

		private static bool UnplacedFoundingHeartOutput(GameObject Output, string Blueprint)
		{
			return GameObject.Validate(Output) && Output.Blueprint == Blueprint
				&& Output.CurrentCell == null && Output.CurrentZone == null
				&& Output.InInventory == null && (Output.Physics == null || Output.Physics.InInventory == null)
				&& FoundingHeartLoadedReferenceCount(Output) == 0;
		}
	}
}
