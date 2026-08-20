using System.Collections.Generic;
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
			system.SettlementName = faction.DisplayName;
			system.FoundedTick = The.Game.TimeTicks;
			system.LastHeartbeatTick = The.Game.TimeTicks;
			system.LastVisitTick = The.Game.TimeTicks;
			system.Style = ResolveFoundingStyle(The.Player?.CurrentZone, out string terrainBlueprint, out string regionName, out int zLevel);
			system.FoundingTerrainBlueprint = terrainBlueprint;
			system.FoundingRegionName = regionName;
			system.FoundingZLevel = zLevel;
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
			// The one civic event that earns a mural. Mural space is capped at sixteen across a
			// whole life and shared with the player's own history, so the settlement takes exactly
			// one slot: the founding, which happens once per realm and is what everything else
			// hangs off. Every other civic accomplishment files with no mural weight.
			KingdomChronicle.Record(system, "you poured the first water, and " + faction.DisplayName + " was founded on " + StyleGroundClause(system.Style), Accomplishment: true, MuralText: "Poured the first water and founded " + faction.DisplayName + ".");
			The.Player?.RequirePart<KingdomCharterPart>().EnsureAbility();
			return faction;
		}

		/// <summary>
		/// Judges what the founding rite would do on this ground, given a realm that already
		/// exists. The rite is the one the first city was founded with: the difference is where
		/// it is performed. Ground the realm already holds, or ground bordering it, is claimed
		/// rather than founded; a realm already holding
		/// <see cref="KingdomSettlement.MaxSettlements"/> cities founds nothing.
		/// </summary>
		/// <param name="System">The kingdom system.</param>
		/// <param name="Site">The zone the founder is standing in. Null reads as unclaimed,
		/// unbordered ground, which is what an unresolvable site should not be punished for.</param>
		/// <returns>The verdict; <see cref="KingdomSettlement.SecondFoundingVerdict.Allowed"/>
		/// means <see cref="FoundSecond"/> will proceed.</returns>
		public static KingdomSettlement.SecondFoundingVerdict JudgeSite(KingdomSystem System, Zone Site)
		{
			bool claimed = false;
			bool adjacent = false;
			if (Site != null)
			{
				foreach (string zoneID in RealmClaims(System))
				{
					if (zoneID == Site.ZoneID)
					{
						claimed = true;
						break;
					}
					if (!adjacent && ZonesAdjacent(zoneID, Site.ZoneID))
					{
						adjacent = true;
					}
				}
			}
			return KingdomSettlement.JudgeSecondFounding(System.Founded, System.SettlementCount, claimed, adjacent);
		}

		/// <summary>
		/// Founds the realm's second city on ground the founder is standing on: same faction,
		/// same standings, same chronicle, a new place with a purpose of its own. The city that
		/// was seated becomes <see cref="KingdomSystem.Away"/> and keeps its own clocks; the new
		/// one takes the seat and starts them from now.
		/// </summary>
		/// <param name="Name">The new city's name. Empty is rejected.</param>
		/// <param name="Vocation">What the city is for, from
		/// <see cref="KingdomSettlement.Vocations"/>. Anything else becomes the neutral vocation
		/// rather than being refused &mdash; a founder is never told their answer was invalid.</param>
		/// <param name="Site">The zone to found on. Null is rejected.</param>
		/// <param name="Force">True to found on ground that borders the realm (debug only, so a
		/// tester need not walk past the horizon). The two-city cap and the refusal to found on
		/// ground the realm already holds stand regardless &mdash; forcing either would leave two
		/// cities claiming one zone.</param>
		/// <returns>True if the city was founded. False means the site was refused, and nothing
		/// has changed &mdash; in particular no water has been spent, because the caller checks
		/// before it pours.</returns>
		public static bool FoundSecond(string Name, string Vocation, Zone Site, bool Force = false)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (string.IsNullOrEmpty(Name) || Site == null)
			{
				return false;
			}
			KingdomSettlement.SecondFoundingVerdict verdict = JudgeSite(system, Site);
			if (verdict != KingdomSettlement.SecondFoundingVerdict.Allowed && !(Force && verdict == KingdomSettlement.SecondFoundingVerdict.GroundIsTooClose))
			{
				return false;
			}
			string vocation = KingdomSettlement.IsKnownVocation(Vocation) ? Vocation : KingdomSettlement.NeutralVocation;
			KingdomSettlement founded = new KingdomSettlement();
			founded.SettlementName = Name;
			founded.Vocation = vocation;
			founded.Style = ResolveFoundingStyle(Site, out string terrainBlueprint, out string regionName, out int zLevel);
			founded.FoundingTerrainBlueprint = terrainBlueprint;
			founded.FoundingRegionName = regionName;
			founded.FoundingZLevel = zLevel;
			founded.FoundedTick = The.Game.TimeTicks;
			founded.LastHeartbeatTick = The.Game.TimeTicks;
			founded.LastVisitTick = The.Game.TimeTicks;
			// Captured before the new city is seated, so the old one keeps every clock it had:
			// it is dormant, not paused, and catches up when the founder next stands in it.
			KingdomSettlement wasSeated = system.Capture();
			system.Restore(founded);
			system.Away = wasSeated;
			// One entry per register, not a stream: the founding, the purpose, and the fact that
			// the realm now holds two cities, said once. Written before the claim so the book
			// reads in the order the day happened.
			KingdomChronicle.Record(system, "you poured again on " + StyleGroundClause(system.Style) + ", and " + Name + " was founded as " + KingdomSettlement.VocationClause(vocation) + ", the second city of " + system.KingdomDisplayName, Accomplishment: true);
			// Force, because the whole point of this ground is that it does not border the realm.
			ClaimZone(Site, Force: true);
			The.Player?.RequirePart<KingdomCharterPart>().EnsureAbility();
			return true;
		}

		/// <summary>Every zone the realm holds, across both cities. The seat's claims come first
		/// because most sites are judged against the ground the founder just walked off.</summary>
		private static IEnumerable<string> RealmClaims(KingdomSystem System)
		{
			foreach (string zoneID in System.ClaimedZones)
			{
				yield return zoneID;
			}
			if (System.Away != null)
			{
				foreach (string zoneID in System.Away.ClaimedZones)
				{
					yield return zoneID;
				}
			}
		}

		/// <summary>
		/// Reads the founding site's terrain evidence and resolves it to a city style via
		/// <see cref="KingdomRules.StyleForSite"/>. The audit in
		/// _notes/TERRAIN-FOOD-INDEPENDENT-AUDIT.md is what licenses this exact read: an explicit
		/// founding/preflight action on the zone the player is already standing in, not a
		/// background scan. Every step is wrapped by <see cref="KingdomSystem.Guard"/> so a bad
		/// zone, an unmapped terrain, or an engine hiccup degrades to "common" rather than
		/// breaking the founding rite (STANDARDS 9).
		/// </summary>
		/// <param name="FoundingZone">The zone the founder is standing in. Null is tolerated.</param>
		/// <param name="TerrainBlueprint">The exact terrain blueprint read, or null if unavailable.</param>
		/// <param name="RegionName">The canonical terrain region read, or null if unavailable.</param>
		/// <param name="ZLevel">The founding zone's depth, captured alongside the terrain evidence.</param>
		/// <returns>A style from <see cref="KingdomRules.Styles"/>; "common" on any failure.</returns>
		private static string ResolveFoundingStyle(Zone FoundingZone, out string TerrainBlueprint, out string RegionName, out int ZLevel)
		{
			string terrainBlueprint = null;
			string regionName = null;
			int zLevel = 0;
			string style = "common";
			KingdomSystem.Guard("founding style lookup", delegate
			{
				if (FoundingZone == null)
				{
					return;
				}
				terrainBlueprint = FoundingZone.GetTerrainObject()?.Blueprint;
				regionName = FoundingZone.GetTerrainRegion();
				zLevel = FoundingZone.Z;
				style = KingdomRules.StyleForSite(terrainBlueprint, regionName, zLevel);
			});
			if (!KingdomRules.IsKnownStyle(style))
			{
				style = "common";
			}
			TerrainBlueprint = terrainBlueprint;
			RegionName = regionName;
			ZLevel = zLevel;
			return style;
		}

		/// <summary>
		/// Founder-facing clause naming what the ground promises for a city style. Presentation
		/// only: <see cref="KingdomRules.StyleForSite"/> owns which style a site resolves to, this
		/// only supplies the sentence fragment that tells the founder (and later, the chronicle
		/// and any tester reading <c>kingdom:dump</c>) what was read. Lower-case, no leading
		/// article, fit to follow "founded on " or stand alone.
		/// </summary>
		public static string StyleGroundClause(string Style)
		{
			switch (Style)
			{
				case "verdant":
					return "ground green enough to root a verdant city";
				case "fungal":
					return "air thick enough to bloom a fungal city";
				case "gyre":
					return "skies restless enough to turn a gyre city";
				case "eater":
					return "stone old enough to answer an Eater city";
				default:
					return "common ground";
			}
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
			// Ground the realm's other city already holds is not claimable by this one, even
			// forced. Two cities claiming one zone would break the seat quietly rather than
			// loudly: TrySeat tests the seated claims first, so the zone would simply never
			// swap, and whichever city happened to be seated would answer for ground the other
			// one thinks it holds.
			if (system.Away != null && system.Away.ClaimedZones.Contains(Z.ZoneID))
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
