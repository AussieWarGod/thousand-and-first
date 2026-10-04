using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The capital seed's two reads that production would otherwise only make with turns or a
	/// visit, and the five-rung form's read-only territory preflight at chain setup.
	/// <para>
	/// SYNTHETIC, DISCLOSED. The craft lessons go through <c>KingdomZoning.Learn</c> exactly as
	/// the setup's foundry lessons do. The crown hall's zone is read into the city book by
	/// production's own suspend-time check-out (<c>KingdomCity.OnSuspending</c>), pulled forward
	/// because the seed spends no turns. Nothing here writes the crown, a work row or a claim.
	/// </para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private bool ChainCrownedBefore, ChainCrownBookBefore, ChainCrownBookAfter;
			private string ChainTerritoryPreflight;

			/// <summary>The five disk lessons between foundry and arclight. The chain's setup seeds
			/// foundry - nine lessons - for EVERY sealed form, the fixture the accepted four-rung run
			/// climbed on, so the paid 1->2->3->4 prefix keeps its inputs; the arcology's own
			/// MinTech="arclight" (RuntimeData/KingdomBuildings.xml:1407) is reached here, after
			/// rung four. A research node is worth zero craft points (Growth/KingdomZoningRules.cs:234),
			/// so lessons are taught and the level is read BACK, never written.</summary>
			private void TeachArclightCraft()
			{
				for (int i = KingdomZoningRules.PointsForLevel(TechLevel.Foundry);
					i < KingdomZoningRules.PointsForLevel(TechLevel.Arclight); i++)
					Require(KingdomZoning.Learn(System, "disk", "paid-heart-chain-fixture-" + i),
						"taf-camp-rung5-lesson-refused: synthetic craft lesson " + i
							+ " was already present or refused");
			}

			/// <summary>
			/// SYNTHETIC BOOK READ. The crown resolves from the city book plus a survey of the ACTIVE
			/// zone only (Growth/KingdomCrownDiscovery.cs:84-119), and a zone's works enter the book
			/// only at that zone's own check-in, check-out or suspension
			/// (Simulation/City/KingdomCity.z01.CheckIn.cs:98, KingdomCity.z02.CheckOut.cs:102). The
			/// hall stands on a neighbour the founder never enters and the seed spends no turns, so
			/// neither source sees it yet. This makes the read production makes for that zone when
			/// it suspends (Core/KingdomSystem.z20.Events.cs:138), then drops the crown's per-tick
			/// answer (KingdomCrown.ClearCache), because the tick does not turn over inside the
			/// seed. The crown and the book are read before and after; the gate asks the book first.
			/// </summary>
			private void PublishChainCrownZone()
			{
				Zone ground = ChainCrownHall?.CurrentZone;
				Require(ground != null && ground.ZoneID == ChainCrownZoneId && !ReferenceEquals(ground, Zone)
					&& System.ClaimedZones.Contains(ground.ZoneID),
					"taf-camp-rung5-crown-zone: the crown hall does not stand on a claimed neighbour");
				ChainCrownedBefore = KingdomCrown.CrownedOn(System, Zone.ZoneID);
				ChainCrownBookBefore = ChainCrownBookHolds();
				Require(!KingdomSurvey.HasBoundPass, "crown book read found an outstanding survey");
				KingdomCity.OnSuspending(System, ground);
				Require(!KingdomSurvey.HasBoundPass, "crown book read left its survey bound");
				KingdomCrown.ClearCache();
				ChainCrownBookAfter = ChainCrownBookHolds();
			}

			private bool ChainCrownBookHolds()
			{
				Require(KingdomData.TryGetBuilding(KingdomCrownRules.CrownKey, out var entry),
					"authored crown hall missing from the catalogue");
				var book = System.City;
				return book != null && KingdomCampHeartChainRules.CrownBookHolds(book.WorkIds,
					book.WorkZoneIds, book.WorkDesignKeys, KingdomCityRules.StableId(ChainCrownHall.IDIfAssigned),
					ChainCrownZoneId, KingdomCrownRules.CrownKey, entry.Blueprint);
			}

			/// <summary>READ-ONLY, at chain setup, for the five-rung form only: whether the
			/// arcology's four zones can be claimed from the heart's cardinal neighbours, read from
			/// what the engine already knows WITHOUT building any zone (zone ids by direction, and
			/// zone properties, which the zone manager keeps by id). It cannot see terrain or
			/// whether a hall fits - only the seed can, once it builds the ground - but an
			/// impossible territory refuses here, in minutes, rather than after the 1->4 prefix.</summary>
			private void PreflightChainTerritory()
			{
				var notes = new List<string>();
				int open = 0;
				foreach (string direction in ChainClaimDirections)
				{
					string id = Zone.GetZoneIDFromDirection(direction);
					string verdict;
					if (string.IsNullOrEmpty(id)) verdict = "absent";
					else if (System.ClaimedZones.Contains(id)) verdict = "already-held";
					else if (!KingdomFounding.ZonesAdjacent(Zone.ZoneID, id)) verdict = "not-adjacent";
					else if (System.FindNonSeatSettlementByZone(id) != null) verdict = "other-city";
					else if (System.ExiledRealmHolds(id)) verdict = "exiled-realm";
					else if (KingdomRules.GroundIsForeignFaction(
						The.ZoneManager.GetZoneProperty(id, "faction") as string,
						System.KingdomFactionName)) verdict = "foreign-faction";
					else { verdict = "open"; open++; }
					notes.Add(direction + ":" + verdict);
				}
				int wanted = ArcologyZones - System.ClaimedZones.Count;
				ChainTerritoryPreflight = "capital-preflight=" + string.Join(",", notes)
					+ "; open=" + open + "; wanted=" + wanted;
				Require(open >= wanted, "taf-camp-rung5-territory-infeasible: " + ChainTerritoryPreflight);
			}
		}
	}
}
