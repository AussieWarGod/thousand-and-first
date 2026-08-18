using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	public static class KingdomFounding
	{
		public static Faction Found(string Name)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				return Factions.Get(system.KingdomFactionName);
			}
			Faction faction = new Faction();
			faction.Old = false;
			faction.ExtradimensionalVersions = false;
			faction.Visible = true;
			faction.Name = Name;
			faction.DisplayName = Name;
			faction.PositiveSound = "Sounds/Reputation/sfx_reputation_village_positive";
			faction.NegativeSound = "Sounds/Reputation/sfx_reputation_village_negative";
			faction.SetProperty("PlayerKingdom", 1);
			faction.WaterRitualLiquid = "water";
			VillageBase.SetVillageFactionEmblem(faction, faction.Name);
			faction.SetFactionFeeling("Player", 100);
			Factions.AddNewFaction(faction);
			system.KingdomFactionName = faction.Name;
			system.KingdomDisplayName = faction.DisplayName;
			system.FoundedTick = The.Game.TimeTicks;
			The.Game.PlayerReputation.Set(faction.Name, RuleSettings.REPUTATION_LOVED + 100);
			foreach (Faction other in Factions.Loop())
			{
				if (other != faction && other.Name != "Player")
				{
					int standing = The.Game.PlayerReputation.Get(other);
					system.SetStanding(other.Name, standing);
					faction.SetFactionFeeling(other.Name, Reputation.GetFeeling((float)standing));
				}
			}
			KingdomChronicle.Record(system, "you poured the first water, and " + faction.DisplayName + " was founded", Accomplishment: true);
			return faction;
		}

		public static bool ClaimZone(Zone Z)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded || Z == null)
			{
				return false;
			}
			Z.SetZoneProperty("faction", system.KingdomFactionName);
			Faction faction = Factions.Get(system.KingdomFactionName);
			if (faction != null && !faction.HolyPlaces.Contains(Z.ZoneID))
			{
				faction.HolyPlaces.Add(Z.ZoneID);
			}
			if (!system.ClaimedZones.Contains(Z.ZoneID))
			{
				system.ClaimedZones.Add(Z.ZoneID);
				KingdomChronicle.Record(system, system.KingdomDisplayName + " claimed " + Grammar.GetProsaicZoneName(Z));
			}
			if (system.NextArrivalTick <= 0)
			{
				system.NextArrivalTick = The.Game.TimeTicks + KingdomRules.ArrivalIntervalTicks(system.Population);
			}
			return true;
		}

		public static bool EnrollCitizen(GameObject Citizen)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded || Citizen == null || Citizen.Brain == null || Citizen.IsPlayer())
			{
				return false;
			}
			Citizen.Brain.Factions = system.KingdomFactionName + "-100";
			Citizen.Brain.Allegiance.Calm = true;
			Citizen.Brain.Allegiance.Hostile = false;
			Citizen.SetIntProperty("KingdomCitizen", 1);
			return true;
		}
	}
}
