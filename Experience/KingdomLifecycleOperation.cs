using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomLifecycleOperation
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomLifecycleLane Lane;
		public KingdomLifecycleAction Action;
		public KingdomLifecyclePhase Phase;
		public long CreatedTick;
		public long UpdatedTick;
		public string SettlementId;
		public string ZoneId;
		public string ObjectId;
		public string ObjectMarker;
		public string Blueprint;
		public KingdomLifecycleTopology ObjectTopology;
		public string ObjectOwnerId;
		public int ObjectX = -1;
		public int ObjectY = -1;
		public string ObjectName;
		public string Origin;
		public string Faction;
		public string DisplayFaction;
		public string Detail;
		public string Creed;
		public int Kind;
		public int Target;
		public int Count;
		public int DepartedCount;
		public long DueBefore;
		public long DueAfter;
		public long DepartTick;
		public int WaterRequested;
		public int WaterProved;
		public int WaterOutstanding;
		public int WaterLost;
		public int WaterAmbiguous;
		public KingdomLifecyclePhysicalState WaterState;
		public List<KingdomLifecycleWaterLeg> WaterLegs = new List<KingdomLifecycleWaterLeg>();
		public KingdomLifecyclePhysicalState RemovalState;
		public List<KingdomLifecycleProjection> Projections = new List<KingdomLifecycleProjection>();
		public KingdomLifecyclePhysicalState EffectState;
		public List<KingdomLifecycleResourceLease> ResourceLeases =
			new List<KingdomLifecycleResourceLease>();
		public int Defence;
		public int PartySize;
		public int Spawned;
		public int PlunderRequested;
		public int PlunderProved;
		public string ArrivalText;
		public KingdomLifecycleOutbox Outbox;
		public string Fault;

		[NonSerialized]
		public object LiveAuthority;
	}
}
