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
		/// Idempotently finishes the realm-side publication after faction registration. The step
		/// marker lives on the runtime faction because that is the one engine object registration
		/// cannot remove. A retry therefore continues rather than colliding with its own name.
		/// Claim and external seal remain the basin transaction's checked responsibility.
		/// </summary>
		private static Faction CompleteFirstPublication(KingdomSystem system, Faction faction,
			string FactionId, string Name, Zone foundingZone, Cell riteCell, string TransactionID,
			string Authority)
		{
			if (system == null || faction == null || string.IsNullOrEmpty(Name) ||
				!system.FirstIdentityMatches(TransactionID, foundingZone?.ZoneID) ||
				faction.Name != FactionId || faction.DisplayName != Name ||
				faction.GetIntProperty("PlayerKingdom") != 1 ||
				faction.GetIntProperty("Village") != 1 ||
				string.IsNullOrEmpty(TransactionID) ||
				faction.GetStringProperty(
					KingdomFoundingTransaction.PendingFactionTransactionProperty, null) !=
					TransactionID ||
				faction.GetStringProperty(
					KingdomFoundingTransaction.PendingFactionAuthorityProperty, null) !=
					Authority ||
				faction.GetStringProperty(
					KingdomFoundingTransaction.RealmReservationProperty, null) != Authority ||
				faction.GetStringProperty(FoundingChronicleEventProperty, null) !=
					KingdomFoundingTransaction.FoundingEventID(
						KingdomFoundingKind.FirstCity, TransactionID, "chronicle") ||
				!KingdomFoundingTransaction.FactionRegistryCoherent(FactionId, faction))
			{
				return null;
			}
			int step = faction.GetIntProperty(FoundingStepProperty);
			int chronicleStage = faction.GetIntProperty(FoundingChronicleStageProperty);
			if (step < 0 || step > 4 || chronicleStage < 0 || chronicleStage > 2 ||
				(step < 3 && chronicleStage != 0) ||
				(step == 4 && chronicleStage != 2))
			{
				return null;
			}
			if (step < 1)
			{
				system.KingdomFactionName = faction.Name;
				system.KingdomDisplayName = faction.DisplayName;
				system.SettlementName = faction.DisplayName;
				if (system.FoundedTick <= 0L)
				{
					system.FoundedTick = The.Game.TimeTicks;
				}
			// The realm's simulation seed, minted here and never again. Deferred out of W0 because
			// there was nothing to seed yet; the founding is the one moment that has a realm name,
			// a founding tick and a world to domain-separate against all at once.
				if ((system.SimulationSeedHigh == 0UL && system.SimulationSeedLow == 0UL &&
					 !system.MintSimulationSeed(The.Game.GetWorldSeed(), system.RealmId,
						system.FoundedTick)) ||
					!system.SimulationSeedMatches(The.Game.GetWorldSeed(), system.RealmId,
						system.FoundedTick))
				{
					return null;
				}
				if (system.LastHeartbeatTick <= 0L)
				{
					system.LastHeartbeatTick = The.Game.TimeTicks;
				}
				if (system.LastVisitTick <= 0L)
				{
					system.LastVisitTick = The.Game.TimeTicks;
				}
				if (system.LastSemanticTick <= 0L)
				{
					system.LastSemanticTick = The.Game.TimeTicks;
				}
				system.Style = ResolveFoundingStyle(foundingZone, out string terrainBlueprint,
					out string regionName, out int zLevel);
				system.FoundingTerrainBlueprint = terrainBlueprint;
				system.FoundingRegionName = regionName;
				system.FoundingZLevel = zLevel;
			// Where the water was poured. Every later plot's heart is seeded here and drifts toward
			// whatever gets built (KingdomPlotRules.TryHeart).
				if (foundingZone != null && riteCell != null && riteCell.ParentZone == foundingZone)
				{
					foundingZone.SetZoneProperty(KingdomPlots.RiteXProperty, riteCell.X.ToString());
					foundingZone.SetZoneProperty(KingdomPlots.RiteYProperty, riteCell.Y.ToString());
				}
				if (!system.FirstIdentityMatches(TransactionID, foundingZone.ZoneID))
				{
					return null;
				}
				faction.SetProperty(FoundingStepProperty, 1);
				step = 1;
			}
			// A ruin's ground already had its own history; the rite restores it rather than
			// raising a settlement from nothing. See TryRestoreRuinStructures for what "restores"
			// means in practice, and STANDARDS/VISION on why nothing here is moved or destroyed.
			if (step < 2)
			{
				if (!TryReadOrFreezeFoundingStandings(faction,
					out List<KeyValuePair<string, int>> frozenStandings) ||
					!TryResolveFoundingStandings(faction, frozenStandings,
						out List<KeyValuePair<Faction, int>> resolvedStandings) ||
					!KingdomFoundingTransaction.FoundingAuthorityStillExact(
						Authority, foundingZone))
				{
					return null;
				}
				The.Game.PlayerReputation.Set(faction.Name, RuleSettings.REPUTATION_LOVED + 100);
				// Reputation writes raise engine events synchronously. A handler may try to found
				// again or disturb a reservation, so do not continue publishing on stale authority.
				if (!system.FirstIdentityMatches(TransactionID, foundingZone.ZoneID) ||
					!KingdomFoundingTransaction.FoundingAuthorityStillExact(
					Authority, foundingZone))
				{
					return null;
				}
				foreach (KeyValuePair<Faction, int> target in resolvedStandings)
				{
					system.SetStanding(target.Key.Name, target.Value);
					faction.SetFactionFeeling(target.Key.Name,
						Reputation.GetFeeling((float)target.Value));
					if (!system.FirstIdentityMatches(TransactionID, foundingZone.ZoneID))
					{
						return null;
					}
				}
				faction.SetProperty(FoundingStepProperty, 2);
				step = 2;
			}
			// The realm's own favourite dish, in vanilla's own place for one: three plain fields
			// on the Faction that vanilla's own serializer writes and reads
			// (D/XRL/World/Faction.cs:72-76,286-288,362). Derived from the ground this rite was
			// poured on; re-derived later if the people who settle here turn out to hold with
			// somebody (KingdomDish.Ensure, called from every settlement pass). Silent here: the
			// founding already has its line, and a realm has no dish to have CHANGED yet.
			bool isRuin = KingdomRules.IsRuinSite(system.FoundingTerrainBlueprint);
			int structuresRestored = faction.GetIntProperty("TAFFoundingRestored");
			if (step < 3)
			{
				if (isRuin && !TryRestoreRuinStructures(foundingZone, TransactionID,
					out structuresRestored)) return null;
				if (!isRuin) structuresRestored = 0;
				faction.SetProperty("TAFFoundingRestored", structuresRestored);
				KingdomDish.Ensure(system, Announce: false);
				if (!system.FirstIdentityMatches(TransactionID, foundingZone.ZoneID))
				{
					return null;
				}
				faction.SetProperty(FoundingStepProperty, 3);
				step = 3;
			}
			// The one civic event that earns a mural. Mural space is capped at sixteen across a
			// whole life and shared with the player's own history, so the settlement takes exactly
			// one slot: the founding, which happens once per realm and is what everything else
			// hangs off. Every other civic accomplishment files with no mural weight.
			string verb = isRuin ? "reclaimed" : "founded";
			if (step < 4)
			{
				string eventID = faction.GetStringProperty(
					FoundingChronicleEventProperty, null);
				string displayName = KingdomPresentation.Rich(faction.DisplayName);
				KingdomFoundingTransaction.RecordChronicleOnce(system, eventID,
					"you poured the first water, and " + displayName + " was " +
					verb + " on " + StyleGroundClause(system.Style) +
					KingdomRules.RuinRestorationClause(structuresRestored),
					Accomplishment: true, MuralText: "Poured the first water and " +
					verb + " " + displayName + ".",
					ReadStage: () => faction.GetIntProperty(FoundingChronicleStageProperty),
					WriteStage: stage => faction.SetProperty(
						FoundingChronicleStageProperty, stage),
					ReadDisposition: () => faction.HasProperty(
						FoundingChronicleDispositionProperty)
						? (int?)faction.GetIntProperty(FoundingChronicleDispositionProperty)
						: null,
					WriteDisposition: disposition => faction.SetProperty(
						FoundingChronicleDispositionProperty, disposition),
					ValidateAuthority: () =>
						KingdomFoundingTransaction.FoundingAuthorityStillExact(
							Authority, foundingZone));
				if (!system.FirstIdentityMatches(TransactionID, foundingZone.ZoneID))
				{
					return null;
				}
				faction.SetProperty(FoundingStepProperty, 4);
			}
			// Paced out while the water soaks in, and AFTER the ruin's own structures are back on
			// their ground, so the heart is surveyed around what is standing rather than through
			// it: the whole extent the heart may one day take, stood about with stakes anybody can
			// walk up to and read. It costs nothing, claims nothing, and refuses nothing -- the
			// layout grammar reads it as a preference, and a plot staked inside it is told so and
			// marked to yield. The first rung is staked here too: the basin, on the ground the
			// water was poured on, which is the settlement's whole heart until it grows one.
			if (foundingZone != null && riteCell != null && riteCell.ParentZone == foundingZone)
			{
				KingdomSystem.Guard("founding: the heart surveyed", delegate
				{
					if (!KingdomPlots.TrySurveyedHeart(foundingZone, out var _))
					{
						KingdomPlots.SurveyHeart(system, foundingZone, riteCell.X, riteCell.Y);
					}
				});
			}
			The.Player?.RequirePart<KingdomCharterPart>().EnsureAbility();
			// The first durable snapshot waits for the rite's ground claim. Before that claim the
			// seal has no honest semantic ground id to carry.
			KingdomSeal.MarkSemanticDirty("founding publication");
			return faction;
		}

		internal static KingdomChronicleDisposition FirstChronicleDisposition(Faction Faction)
		{
			return Faction == null || !Faction.HasProperty(FoundingChronicleDispositionProperty)
				? KingdomChronicleDisposition.None
				: (KingdomChronicleDisposition)Faction.GetIntProperty(
					FoundingChronicleDispositionProperty);
		}

		internal static bool ClearDebugFoundingMarkers(Faction Faction)
		{
			if (Faction == null)
			{
				return false;
			}
			Faction.RemoveProperty(PendingFactionProperty);
			Faction.RemoveProperty(KingdomFoundingTransaction.PendingFactionTransactionProperty);
			Faction.RemoveProperty(KingdomFoundingTransaction.PendingFactionAuthorityProperty);
			Faction.RemoveProperty(FoundingStepProperty);
			Faction.RemoveProperty(FoundingChronicleEventProperty);
			Faction.RemoveProperty(FoundingChronicleStageProperty);
			Faction.RemoveProperty(FoundingChronicleDispositionProperty);
			Faction.RemoveProperty(FoundingStandingsProperty);
			Faction.RemoveProperty("TAFFoundingRestored");
			return !Faction.HasProperty(PendingFactionProperty) &&
				!Faction.HasProperty(
					KingdomFoundingTransaction.PendingFactionTransactionProperty) &&
				!Faction.HasProperty(
					KingdomFoundingTransaction.PendingFactionAuthorityProperty) &&
				!Faction.HasProperty(FoundingStepProperty) &&
				!Faction.HasProperty(FoundingChronicleEventProperty) &&
				!Faction.HasProperty(FoundingChronicleStageProperty) &&
				!Faction.HasProperty(FoundingChronicleDispositionProperty) &&
				!Faction.HasProperty(FoundingStandingsProperty) &&
				!Faction.HasProperty("TAFFoundingRestored");
		}

	}
}
