using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	[Serializable]
	public class KingdomSystem : IGameSystem
	{
		public int SerializationVersion = 1;

		public string KingdomFactionName;

		public string KingdomDisplayName;

		public string Style = "common";

		public long FoundedTick;

		public GrowthStage Stage = GrowthStage.Camp;

		public int Population;

		public int DryStreak;

		public bool Withered;

		public bool HasShopkeeper;

		public long NextArrivalTick;

		public int RaidState;

		public string RaidFactionName;

		public long RaidDueTick;

		public long LastRaidTick;

		public List<string> ClaimedZones = new List<string>();

		public Dictionary<string, string> ZoneDistricts = new Dictionary<string, string>();

		public List<string> ChronicleEntries = new List<string>();

		public List<string> OutsiderEntries = new List<string>();

		public Dictionary<string, int> OriginCounts = new Dictionary<string, int>();

		public Dictionary<string, int> Standings = new Dictionary<string, int>();

		public bool Founded => !string.IsNullOrEmpty(KingdomFactionName);

		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterReputationChangeEvent.ID);
			Registrar.Register(AfterGameLoadedEvent.ID);
			Registrar.Register(ZoneActivatedEvent.ID);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			KingdomGrowth.OnZoneActivated(this, E.Zone);
			KingdomRaids.OnZoneActivated(this, E.Zone);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterReputationChangeEvent E)
		{
			if (Founded && !E.Transient && E.Faction != null && E.Faction.Name != KingdomFactionName && E.Faction.Name != "Player")
			{
				AdjustStanding(E.Faction.Name, KingdomRules.SpilloverDelta(E.To - E.From, Stage));
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			ReassertFeelings();
			return base.HandleEvent(E);
		}

		public int GetStanding(string FactionName)
		{
			if (!Standings.TryGetValue(FactionName, out var value))
			{
				return 0;
			}
			return value;
		}

		public void SetStanding(string FactionName, int Value, bool Mirror = true)
		{
			Standings[FactionName] = Value;
			if (Mirror)
			{
				MirrorFeeling(FactionName);
			}
		}

		public void AdjustStanding(string FactionName, int Delta, bool Mirror = true)
		{
			if (Delta != 0)
			{
				SetStanding(FactionName, GetStanding(FactionName) + Delta, Mirror);
			}
		}

		public void MirrorFeeling(string FactionName)
		{
			if (!Founded || FactionName == KingdomFactionName || FactionName == "Player")
			{
				return;
			}
			Faction faction = Factions.Get(FactionName);
			if (faction != null)
			{
				faction.SetFactionFeeling(KingdomFactionName, Reputation.GetFeeling((float)GetStanding(FactionName)));
			}
		}

		public void ReassertFeelings()
		{
			if (!Founded)
			{
				return;
			}
			foreach (KeyValuePair<string, int> standing in Standings)
			{
				MirrorFeeling(standing.Key);
			}
			Factions.Get(KingdomFactionName)?.SetFactionFeeling("Player", 100);
		}
	}
}
