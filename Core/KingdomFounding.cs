using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	public static class KingdomFounding
	{
		/// <summary>
		/// Founds the player's kingdom: creates and registers a runtime faction following the
		/// engine's village-faction recipe, seeds its standings from the founder's current
		/// reputation with every faction, grants the Charter ability, and opens the chronicle.
		/// </summary>
		/// <param name="Name">Settlement name, used as both faction name and display name.</param>
		/// <returns>The new faction, or the existing one if a kingdom is already founded
		/// (one kingdom per game; this is not an error).</returns>
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
			The.Player?.RequirePart<KingdomCharterPart>().EnsureAbility();
			return faction;
		}

		/// <summary>
		/// Claims a zone for the kingdom: stamps the zone faction property (so future spawns
		/// enrol as citizens), adds it to the faction's holy places, and starts the growth
		/// clock on first claim.
		/// </summary>
		/// <param name="Z">Zone to claim. Null is rejected.</param>
		/// <param name="Force">True to bypass the adjacency requirement (debug and scripted
		/// foundings only). Normal claims must border existing kingdom ground.</param>
		/// <returns>True if claimed; false if unfounded, null, or not adjacent to the realm.</returns>
		public static bool ClaimZone(Zone Z, bool Force = false)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded || Z == null)
			{
				return false;
			}
			if (!Force && system.ClaimedZones.Count > 0 && !system.ClaimedZones.Contains(Z.ZoneID))
			{
				bool adjacent = false;
				foreach (string claimedZone in system.ClaimedZones)
				{
					if (ZonesAdjacent(claimedZone, Z.ZoneID))
					{
						adjacent = true;
						break;
					}
				}
				if (!adjacent)
				{
					return false;
				}
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

		/// <summary>
		/// Zone-level adjacency using the engine's own zone-ID parser, which understands
		/// instanced and blueprint-form IDs that a naive split would reject.
		/// </summary>
		public static bool ZonesAdjacent(string A, string B)
		{
			if (!ZoneID.Parse(A, out var worldA, out var pxA, out var pyA, out var zxA, out var zyA, out var zA))
			{
				return false;
			}
			if (!ZoneID.Parse(B, out var worldB, out var pxB, out var pyB, out var zxB, out var zyB, out var zB))
			{
				return false;
			}
			return KingdomRules.CoordsAdjacent(worldA, pxA * 3 + zxA, pyA * 3 + zyA, zA, worldB, pxB * 3 + zxB, pyB * 3 + zyB, zB);
		}

		/// <summary>
		/// Enrols a creature as a citizen of the kingdom: sets its allegiance to the kingdom
		/// faction, calms it, and marks it with the KingdomCitizen property.
		/// </summary>
		/// <param name="Citizen">The creature. The player is rejected; so is anything brainless.</param>
		/// <returns>True if enrolled, false if unfounded or the target is ineligible.</returns>
		/// <remarks>Enrolled creatures are protected: kingdom systems never destroy a citizen
		/// they did not themselves create (see the protection law in STANDARDS 7). Settlers
		/// spawned by the growth engine additionally carry KingdomBorn and may emigrate.</remarks>
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
