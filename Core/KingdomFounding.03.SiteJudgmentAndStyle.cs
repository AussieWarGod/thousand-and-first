using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	public static partial class KingdomFounding
	{
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
		/// <returns>True only after claim, seat, Charter ability, rite placement, and seal are all
		/// verified. False is either a pre-publication refusal or an explicitly logged recoverable
		/// projection failure; calling the same name and site again resumes the latter. This debug
		/// route never spends water.</returns>
		public static bool FoundSecond(string Name, string Vocation, Zone Site, bool Force = false)
		{
			string failure;
			return KingdomFoundingTransaction.TryFoundSecondWithoutWater(
				Name, Vocation, Site, Force, out failure);
		}

		/// <summary>Every zone the realm holds. The seat's claims come first
		/// because most sites are judged against the ground the founder just walked off.</summary>
		private static IEnumerable<string> RealmClaims(KingdomSystem System)
		{
			foreach (string zoneID in System.ClaimedZones)
			{
				yield return zoneID;
			}
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				foreach (string zoneID in nonSeat[i].ClaimedZones) yield return zoneID;
		}

		/// <summary>
		/// Reads the founding site's terrain evidence and resolves it to a city style via
		/// <see cref="KingdomData.StyleForSite"/>. The audit in
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
		/// <returns>A style from the merged registry; "common" on any failure.</returns>
		internal static string ResolveFoundingStyle(Zone FoundingZone, out string TerrainBlueprint, out string RegionName, out int ZLevel)
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
				style = KingdomData.StyleForSite(terrainBlueprint, regionName, zLevel);
			});
			if (!KingdomData.TryGetStyle(style, out string canonical))
			{
				style = "common";
			}
			else
			{
				style = canonical;
			}
			TerrainBlueprint = terrainBlueprint;
			RegionName = regionName;
			ZLevel = zLevel;
			return style;
		}

		/// <summary>
		/// Founder-facing clause naming what the ground promises for a city style. Presentation
		/// only: <see cref="KingdomData.StyleForSite"/> owns which style a site resolves to, this
		/// only supplies the sentence fragment that tells the founder (and later, the chronicle
		/// and any tester reading <c>kingdom:dump</c>) what was read. Lower-case, no leading
		/// article, fit to follow "founded on " or stand alone.
		/// </summary>
		public static string StyleGroundClause(string Style)
		{
			return KingdomData.StyleGroundClause(Style);
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
			bool otherCitys = Site != null &&
				System.FindNonSeatSettlementByZone(Site.ZoneID) != null;
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

	}
}
