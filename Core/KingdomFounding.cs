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
		/// <returns>The new faction; the existing one if a kingdom is already founded (not an
		/// error); or null when a faction of that name is already registered, in which case
		/// nothing has changed.</returns>
		public static Faction Found(string Name)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				return Factions.Get(system.KingdomFactionName);
			}
			// Factions.AddNewFaction is a Dictionary.Add (XRL/World/Factions.cs:270) and a runtime
			// faction can never be removed or renamed - so after an expulsion the old realm's name
			// is taken forever. Refuse before the rite commits anything rather than throwing part
			// way through it.
			if (Factions.Exists(Name))
			{
				return null;
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
			Zone foundingZone = The.Player?.CurrentZone;
			system.Style = ResolveFoundingStyle(foundingZone, out string terrainBlueprint, out string regionName, out int zLevel);
			system.FoundingTerrainBlueprint = terrainBlueprint;
			system.FoundingRegionName = regionName;
			system.FoundingZLevel = zLevel;
			// Where the water was poured. Every later plot's heart is seeded here and drifts toward
			// whatever gets built (KingdomPlotRules.TryHeart).
			Cell riteCell = The.Player?.CurrentCell;
			if (foundingZone != null && riteCell != null && riteCell.ParentZone == foundingZone)
			{
				foundingZone.SetZoneProperty(KingdomPlots.RiteXProperty, riteCell.X.ToString());
				foundingZone.SetZoneProperty(KingdomPlots.RiteYProperty, riteCell.Y.ToString());
			}
			// A ruin's ground already had its own history; the rite restores it rather than
			// raising a settlement from nothing. See RestoreRuinStructures for what "restores"
			// means in practice, and STANDARDS/VISION on why nothing here is moved or destroyed.
			bool isRuin = KingdomRules.IsRuinSite(terrainBlueprint);
			int structuresRestored = isRuin ? RestoreRuinStructures(foundingZone) : 0;
			string verb = isRuin ? "reclaimed" : "founded";
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
			KingdomChronicle.Record(system, "you poured the first water, and " + faction.DisplayName + " was " + verb + " on " + StyleGroundClause(system.Style) + KingdomRules.RuinRestorationClause(structuresRestored), Accomplishment: true, MuralText: "Poured the first water and " + verb + " " + faction.DisplayName + ".");
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
			// A second city is never forced onto ground another faction already answers to, even
			// under Force &mdash; that parameter only ever meant "bypass the adjacency
			// requirement" (see the doc comment on it above). A living village is asked into a
			// covenant through the charter rite, never annexed by this one; anything else foreign
			// simply is not this rite's to take.
			if (KingdomRules.GroundIsForeignFaction(Site.GetZoneProperty("faction"), system.KingdomFactionName))
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
			bool isRuin = KingdomRules.IsRuinSite(terrainBlueprint);
			int structuresRestored = isRuin ? RestoreRuinStructures(Site) : 0;
			string verb = isRuin ? "reclaimed" : "founded";
			// Captured before the new city is seated, so the old one keeps every clock it had:
			// it is dormant, not paused, and catches up when the founder next stands in it.
			KingdomSettlement wasSeated = system.Capture();
			system.Restore(founded);
			system.Away = wasSeated;
			// One entry per register, not a stream: the founding, the purpose, and the fact that
			// the realm now holds two cities, said once. Written before the claim so the book
			// reads in the order the day happened.
			KingdomChronicle.Record(system, "you poured again on " + StyleGroundClause(system.Style) + ", and " + Name + " was " + verb + " as " + KingdomSettlement.VocationClause(vocation) + ", the second city of " + system.KingdomDisplayName + KingdomRules.RuinRestorationClause(structuresRestored), Accomplishment: true);
			// Force, because the whole point of this ground is that it does not border the realm.
			// The foreign-faction refusal above already stands regardless of this Force.
			ClaimZone(Site, Force: true);
			Cell secondRiteCell = The.Player?.CurrentCell;
			if (secondRiteCell != null && secondRiteCell.ParentZone == Site)
			{
				Site.SetZoneProperty(KingdomPlots.RiteXProperty, secondRiteCell.X.ToString());
				Site.SetZoneProperty(KingdomPlots.RiteYProperty, secondRiteCell.Y.ToString());
			}
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
		/// Judges what the founder's claim would do on the ground they are standing on: the
		/// facts gathered off the world, the verdict decided by
		/// <see cref="KingdomZoningRules.JudgeClaim"/>, which knows nothing about zones or
		/// factions and can therefore be tabled.
		/// <para>
		/// Every fact here is one <see cref="ClaimZone"/> already enforces, plus the one it does
		/// not: how much ground a city of this stage answers for. The primitive deliberately
		/// keeps no stage gate &mdash; the founding rite claims its first parasang at Camp, and a
		/// scripted second founding claims across the horizon &mdash; so the gate belongs to the
		/// founder's own action, which is the only claim anybody chooses.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom system.</param>
		/// <param name="Site">The zone the founder is standing in. Null reads as ground that
		/// borders nothing, which refuses by name rather than by silence.</param>
		public static KingdomZoningRules.ClaimVerdict JudgeClaim(KingdomSystem System, Zone Site)
		{
			if (System == null)
			{
				return KingdomZoningRules.ClaimVerdict.NothingFoundedYet;
			}
			bool ours = Site != null && System.ClaimedZones.Contains(Site.ZoneID);
			bool otherCitys = Site != null && System.Away != null && System.Away.ClaimedZones.Contains(Site.ZoneID);
			bool otherRealms = Site != null && System.ExiledRealmHolds(Site.ZoneID);
			bool foreign = Site != null && KingdomRules.GroundIsForeignFaction(Site.GetZoneProperty("faction"), System.KingdomFactionName);
			bool adjacent = false;
			if (Site != null)
			{
				foreach (string zoneID in System.ClaimedZones)
				{
					if (ZonesAdjacent(zoneID, Site.ZoneID))
					{
						adjacent = true;
						break;
					}
				}
			}
			return KingdomZoningRules.JudgeClaim(System.Founded, System.Stage, System.ClaimedZones.Count,
				ours, otherCitys, otherRealms, foreign, adjacent);
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
			// Ground the realm that put the founder out still holds is not claimable by the one
			// they founded next: the claim would overwrite the zone's faction property and hijack
			// a city that is supposed to be going on without them.
			if (system.ExiledRealmHolds(Z.ZoneID))
			{
				return false;
			}
			// Ground another faction already answers to is not claimed by pouring water on it:
			// writing the kingdom's faction over whatever a village (or another mod) already had
			// there was a live hazard the ecosystem-compat audit found in this exact call. Force
			// is for debug/scripted foundings only; FoundSecond judges this itself before it ever
			// reaches here with Force set, so a real second founding cannot route around it.
			if (!Force && KingdomRules.GroundIsForeignFaction(Z.GetZoneProperty("faction"), system.KingdomFactionName))
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
		/// instanced and blueprint-form IDs that a naive split would reject. Includes the
		/// vertical neighbour &mdash; a cellar directly below held ground, or a tower directly
		/// above it &mdash; because that is what a settlement's own territory means once it is
		/// building on more than one stratum: see <see cref="KingdomRules.CoordsAdjacent"/>.
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
			return KingdomRules.CoordsAdjacent(worldA, pxA * 3 + zxA, pyA * 3 + zyA, zA, worldB, pxB * 3 + zxB, pyB * 3 + zyB, zB, IncludeVertical: true);
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

		/// <summary>
		/// Credits a ruin founding with whatever of the ground's own history still stands. Every
		/// object already in the zone that carries a part the settlement already knows how to use
		/// for free &mdash; a bed for housing, a shrine for petitions &mdash; is stamped
		/// <c>KingdomBuilt</c>, the exact marker <c>r_KingdomScaffold.Complete</c> stamps on
		/// anything it finishes building, so <c>KingdomSurvey</c>, <c>KingdomCommission</c>, and
		/// <c>KingdomPetitions</c> count it without any change to those files.
		/// <para>
		/// Nothing is moved, replaced, or destroyed here &mdash; only recognised. Binding a ruin's
		/// standing furniture to the settlement's fate the moment the founder pours the rite over
		/// it is the explicit designation the protection law (STANDARDS 7) asks for: the founder
		/// chose this exact ground, once, deliberately, and the chronicle says so.
		/// </para>
		/// </summary>
		/// <param name="Site">The founding zone. Null yields zero.</param>
		/// <returns>How many structures were credited.</returns>
		private static int RestoreRuinStructures(Zone Site)
		{
			int restored = 0;
			if (Site == null)
			{
				return 0;
			}
			// A hostile part or a zone mid-teardown degrades to "nothing was already standing"
			// rather than breaking the rite (STANDARDS 9): the founder still gets their city.
			KingdomSystem.Guard("ruin restoration", delegate
			{
				foreach (GameObject item in Site.GetObjects())
				{
					if (item.GetIntProperty("KingdomBuilt") == 1)
					{
						continue;
					}
					if (item.HasPart("Bed") || item.HasPart("Shrine"))
					{
						item.SetIntProperty("KingdomBuilt", 1);
						restored++;
					}
				}
			});
			return restored;
		}

		/// <summary>
		/// Seals a charter with a living village: standing changes, nothing else does. The
		/// village's own faction keeps every zone, every villager, and every vanilla behaviour it
		/// already had &mdash; this never calls <see cref="ClaimZone"/>, never writes a zone
		/// property, and never touches a villager's allegiance. Only the realm's ledger and the
		/// village's feeling toward it move, through the same <see cref="KingdomSystem.SetStanding"/>
		/// every other faction's standing already moves through, and only upward: a charter the
		/// founder earned cannot make the village think worse of the realm than it already did.
		/// <para>
		/// This is deliberately not a second city. A full charter that lets a chartered village
		/// grow the way a founded one does is a larger claim than this rite makes; see the
		/// founding-paths summary for why that is out of scope this pass rather than shipped
		/// half-safe.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom system. Must already be founded; callers judge this
		/// via <see cref="KingdomRules.JudgeVillageCharter"/> before reaching here.</param>
		/// <param name="VillageFactionName">The village's own faction name (not display name).
		/// Never reassigned to any creature or zone.</param>
		/// <param name="VillageDisplayName">The village faction's display name, for the
		/// chronicle.</param>
		public static void CharterVillage(KingdomSystem System, string VillageFactionName, string VillageDisplayName)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(VillageFactionName))
			{
				return;
			}
			if (System.GetStanding(VillageFactionName) < KingdomRules.VillageCharterSealedStanding)
			{
				System.SetStanding(VillageFactionName, KingdomRules.VillageCharterSealedStanding);
			}
			KingdomChronicle.Record(System, "you asked, and " + VillageDisplayName + " agreed: their ground stays theirs, and a covenant now stands between them and " + System.KingdomDisplayName, Accomplishment: true);
		}
	}
}
