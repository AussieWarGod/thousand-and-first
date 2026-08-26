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
	public static class KingdomFounding
	{
		private const string PendingFactionProperty = "TAFFoundingPending";
		private const string FoundingStepProperty = "TAFFoundingStep";
		private const string FoundingChronicleEventProperty = "TAFFoundingChronicleEvent_v1";
		private const string FoundingChronicleStageProperty = "TAFFoundingChronicleStage_v1";
		private const string FoundingChronicleDispositionProperty =
			"TAFFoundingChronicleDisposition_v1";
		private const string FoundingStandingsProperty = "TAFFoundingStandings_v1";
		private const int MaxFoundingStandingsLength = 262144;

		/// <summary>
		/// Founds the player's kingdom: creates and registers a runtime faction following the
		/// engine's village-faction recipe, seeds its standings from the founder's current
		/// reputation with every faction, grants the Charter ability, and opens the chronicle.
		/// </summary>
		/// <param name="Name">Settlement display name. New engine factions use the immutable
		/// namespaced realm id as their key.</param>
		/// <returns>The new faction; the existing one if a kingdom is already founded (not an
		/// error); or null when a faction of that name is already registered, in which case
		/// nothing has changed.</returns>
		public static Faction Found(string Name)
		{
			return KingdomFoundingTransaction.TryFoundFirstWithoutWater(Name,
				The.Player?.CurrentZone, out var faction, out var failure)
				? faction : null;
		}

		/// <summary>Transaction overload binding terrain and rite placement to the prepared site,
		/// not wherever the player happens to stand during a recovery attempt.</summary>
		internal static Faction Found(string Name, Zone FoundingZone, Cell RiteCell,
			string TransactionID, string Authority)
		{
			if (string.IsNullOrEmpty(Name) || FoundingZone == null || RiteCell == null ||
				RiteCell.ParentZone != FoundingZone ||
				!KingdomFoundingTransactionRules.TryParseAuthority(Authority,
					out var parsedAuthority) ||
				parsedAuthority.Kind != KingdomFoundingKind.FirstCity ||
				parsedAuthority.TransactionID != TransactionID ||
				!KingdomIdentityRules.FirstFactionKeyMatches(parsedAuthority.RealmFaction,
					TransactionID, Name, AllowLegacy: true) ||
				parsedAuthority.ZoneID != FoundingZone.ZoneID ||
				parsedAuthority.RiteX != RiteCell.X || parsedAuthority.RiteY != RiteCell.Y ||
				!KingdomFoundingTransaction.AuthorityIsSynchronouslyInFlight(Authority) ||
				!KingdomFoundingTransaction.GlobalReservationMatches(Authority) ||
				!KingdomFoundingTransaction.SiteReservationMatches(FoundingZone, Authority))
			{
				return null;
			}
			string factionId = parsedAuthority.RealmFaction;
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			string identityFailure;
			// Plain persisted fields only: immutable realm + first-city ids exist and read back
			// before faction registration, reputation, zone properties, journal, or any step marker.
			if (!system.TryBindFirstFoundingIdentity(TransactionID, FoundingZone.ZoneID,
				out identityFailure))
			{
				KingdomLog.Log("founding identity refused: " + identityFailure);
				return null;
			}
			if (system.Founded)
			{
				Faction current = Factions.GetIfExists(system.KingdomFactionName);
				// Ordinary repeated debug calls keep their old idempotent meaning. The sole exception
				// is an interrupted first founding, whose faction is deliberately marked until the
				// basin has verified claim, ability, placement, and seal.
				if (current == null || current.GetIntProperty(PendingFactionProperty) != 1 ||
					system.KingdomFactionName != factionId)
				{
					return current;
				}
				if (!KingdomFoundingTransaction.FactionRegistryCoherent(factionId, current) ||
					string.IsNullOrEmpty(TransactionID) ||
					current.GetStringProperty(
						KingdomFoundingTransaction.PendingFactionTransactionProperty, null) !=
						TransactionID)
				{
					return null;
				}
					return CompleteFirstPublication(system, current, factionId, Name,
						FoundingZone, RiteCell, TransactionID, Authority);
			}
			if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(TransactionID))
			{
				return null;
			}
			// Factions.AddNewFaction is a Dictionary.Add (XRL/World/Factions.cs:270) and a runtime
			// faction can never be removed or renamed - so after an expulsion the old realm's name
			// is taken forever. Refuse before the rite commits anything rather than throwing part
			// way through it.
			Faction faction;
			if (Factions.Exists(factionId))
			{
				// AddNewFaction cannot be reversed. A faction carrying this marker is the exact
				// recoverable state left if the engine threw after Dictionary.Add; any other existing
				// faction is somebody else's identity and remains an absolute refusal.
				faction = Factions.GetIfExists(factionId);
				if (faction == null || faction.GetIntProperty(PendingFactionProperty) != 1 ||
					faction.GetIntProperty("PlayerKingdom") != 1 ||
					faction.GetStringProperty(
						KingdomFoundingTransaction.PendingFactionTransactionProperty, null) !=
						TransactionID ||
					faction.GetStringProperty(
						KingdomFoundingTransaction.PendingFactionAuthorityProperty, null) !=
						Authority ||
					faction.GetStringProperty(FoundingChronicleEventProperty, null) !=
						KingdomFoundingTransaction.FoundingEventID(
							KingdomFoundingKind.FirstCity, TransactionID, "chronicle") ||
					!KingdomFoundingTransaction.RepairPendingFactionRegistry(factionId,
						TransactionID, Authority))
				{
					return null;
				}
			}
			else
			{
				if (!KingdomFoundingTransaction.FactionNameAvailable(factionId))
				{
					return null;
				}
				faction = new Faction();
				faction.Old = false;
				faction.ExtradimensionalVersions = false;
				faction.Visible = true;
				faction.Name = factionId;
				faction.DisplayName = Name;
				faction.PositiveSound = "Sounds/Reputation/sfx_reputation_village_positive";
				faction.NegativeSound = "Sounds/Reputation/sfx_reputation_village_negative";
				faction.SetProperty("PlayerKingdom", 1);
				faction.SetProperty("Village", 1);
				faction.SetProperty(PendingFactionProperty, 1);
				faction.SetProperty(
					KingdomFoundingTransaction.PendingFactionTransactionProperty,
					TransactionID);
				faction.SetProperty(
					KingdomFoundingTransaction.PendingFactionAuthorityProperty, Authority);
				faction.SetProperty(
					KingdomFoundingTransaction.RealmReservationProperty, Authority);
				faction.SetProperty(FoundingStepProperty, 0);
				faction.SetProperty(FoundingChronicleEventProperty,
					KingdomFoundingTransaction.FoundingEventID(
						KingdomFoundingKind.FirstCity, TransactionID, "chronicle"));
				faction.SetProperty(FoundingChronicleStageProperty, 0);
				faction.SetProperty(FoundingChronicleDispositionProperty,
					(int)KingdomChronicleDisposition.None);
				faction.WaterRitualLiquid = "water";
				VillageBase.SetVillageFactionEmblem(faction, faction.Name);
				faction.SetFactionFeeling("Player", 100);
				Factions.AddNewFaction(faction);
				if (!system.FirstIdentityMatches(TransactionID, FoundingZone.ZoneID) ||
					!KingdomFoundingTransaction.FactionRegistryCoherent(factionId, faction))
				{
					return null;
				}
			}
			return CompleteFirstPublication(system, faction, factionId, Name,
				FoundingZone, RiteCell, TransactionID, Authority);
		}

		internal static bool TryGetRecoverableFirstPublication(KingdomSystem System,
			string Name, out Faction Faction, out string TransactionID)
		{
			Faction = null;
			TransactionID = null;
			foreach (Faction candidate in Factions.GetList())
			{
				if (candidate == null || candidate.DisplayName != Name ||
					candidate.GetIntProperty(PendingFactionProperty) != 1 ||
					candidate.GetIntProperty("PlayerKingdom") != 1) continue;
				if (Faction != null) return false;
				Faction = candidate;
			}
			TransactionID = Faction?.GetStringProperty(
				KingdomFoundingTransaction.PendingFactionTransactionProperty, null);
			return System != null && !string.IsNullOrEmpty(Name) &&
				Faction != null && !string.IsNullOrEmpty(TransactionID) &&
				Faction.GetIntProperty(PendingFactionProperty) == 1 &&
				Faction.GetIntProperty("PlayerKingdom") == 1 &&
				KingdomFoundingTransaction.FactionRegistryCoherent(Faction.Name, Faction) &&
				KingdomIdentityRules.FirstFactionKeyMatches(Faction.Name, TransactionID,
					Name, AllowLegacy: true) &&
				(!System.Founded || System.KingdomFactionName == Faction.Name);
		}

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

		/// <summary>
		/// Freezes the founder's standings before the first standing mutation. A publication
		/// retry must finish the same ledger even if reputation changed while the prior attempt
		/// was interrupted.
		/// </summary>
		private static bool TryReadOrFreezeFoundingStandings(Faction Realm,
			out List<KeyValuePair<string, int>> Targets)
		{
			Targets = null;
			if (Realm == null || string.IsNullOrEmpty(Realm.Name))
			{
				return false;
			}
			if (!Realm.HasProperty(FoundingStandingsProperty))
			{
				List<KeyValuePair<string, int>> captured =
					new List<KeyValuePair<string, int>>();
				foreach (Faction other in Factions.Loop())
				{
					if (other == null || ReferenceEquals(other, Realm) || other.Name == "Player")
					{
						continue;
					}
					if (string.IsNullOrEmpty(other.Name) || other.Name.Length > 512)
					{
						return false;
					}
					captured.Add(new KeyValuePair<string, int>(other.Name,
						The.Game.PlayerReputation.Get(other)));
				}
				captured.Sort(delegate(KeyValuePair<string, int> Left,
					KeyValuePair<string, int> Right)
				{
					return StringComparer.Ordinal.Compare(Left.Key, Right.Key);
				});
				string encoded = EncodeFoundingStandings(captured);
				if (encoded == null)
				{
					return false;
				}
				Realm.SetProperty(FoundingStandingsProperty, encoded);
				if (Realm.GetStringProperty(FoundingStandingsProperty, null) != encoded)
				{
					return false;
				}
			}
			return TryDecodeFoundingStandings(
				Realm.GetStringProperty(FoundingStandingsProperty, null), out Targets);
		}

		private static bool TryResolveFoundingStandings(Faction Realm,
			List<KeyValuePair<string, int>> Targets,
			out List<KeyValuePair<Faction, int>> Resolved)
		{
			Resolved = new List<KeyValuePair<Faction, int>>();
			if (Realm == null || Targets == null)
			{
				return false;
			}
			foreach (KeyValuePair<string, int> target in Targets)
			{
				Faction other = Factions.GetIfExists(target.Key);
				if (other == null || ReferenceEquals(other, Realm) || other.Name == "Player")
				{
					return false;
				}
				Resolved.Add(new KeyValuePair<Faction, int>(other, target.Value));
			}
			return true;
		}

		private static string EncodeFoundingStandings(
			List<KeyValuePair<string, int>> Targets)
		{
			StringBuilder encoded = new StringBuilder("v1");
			string previous = null;
			for (int i = 0; i < Targets.Count; i++)
			{
				string name = Targets[i].Key;
				if (string.IsNullOrEmpty(name) || name.Length > 512 ||
					(previous != null && StringComparer.Ordinal.Compare(previous, name) >= 0))
				{
					return null;
				}
				previous = name;
				encoded.Append(';').Append(Convert.ToBase64String(
					Encoding.UTF8.GetBytes(name))).Append(':').Append(
					Targets[i].Value.ToString(CultureInfo.InvariantCulture));
				if (encoded.Length > MaxFoundingStandingsLength)
				{
					return null;
				}
			}
			return encoded.ToString();
		}

		private static bool TryDecodeFoundingStandings(string Encoded,
			out List<KeyValuePair<string, int>> Targets)
		{
			Targets = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxFoundingStandingsLength)
			{
				return false;
			}
			string[] rows = Encoded.Split(';');
			if (rows.Length == 0 || rows[0] != "v1")
			{
				return false;
			}
			List<KeyValuePair<string, int>> decoded =
				new List<KeyValuePair<string, int>>(rows.Length - 1);
			string previous = null;
			try
			{
				UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
				for (int i = 1; i < rows.Length; i++)
				{
					int separator = rows[i].IndexOf(':');
					if (separator <= 0 || separator != rows[i].LastIndexOf(':'))
					{
						return false;
					}
					string nameText = rows[i].Substring(0, separator);
					byte[] nameBytes = Convert.FromBase64String(nameText);
					if (Convert.ToBase64String(nameBytes) != nameText)
					{
						return false;
					}
					string name = strictUtf8.GetString(nameBytes);
					if (string.IsNullOrEmpty(name) || name.Length > 512 ||
						(previous != null && StringComparer.Ordinal.Compare(previous, name) >= 0) ||
						!int.TryParse(rows[i].Substring(separator + 1),
							NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
							out int standing))
					{
						return false;
					}
					previous = name;
					decoded.Add(new KeyValuePair<string, int>(name, standing));
				}
			}
			catch (FormatException)
			{
				return false;
			}
			catch (DecoderFallbackException)
			{
				return false;
			}
			if (EncodeFoundingStandings(decoded) != Encoded)
			{
				return false;
			}
			Targets = decoded;
			return true;
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
			return ClaimZone(Z, Force, StageSnapshot: true, Authority: null);
		}

		internal static bool ClaimZone(Zone Z, bool Force, bool StageSnapshot,
			string Authority)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded || Z == null)
			{
				return false;
			}
			Faction reservedRealm = Factions.GetIfExists(system.KingdomFactionName);
			bool reserved = KingdomFoundingTransaction.HasGlobalReservation() ||
				KingdomFoundingTransaction.HasSiteReservation(Z) ||
				!string.IsNullOrEmpty(reservedRealm?.GetStringProperty(
					KingdomFoundingTransaction.RealmReservationProperty, null));
			KingdomFoundingAuthority foundingAuthority = default(KingdomFoundingAuthority);
			if (reserved)
			{
				if (string.IsNullOrEmpty(Authority) ||
					!KingdomFoundingTransaction.GlobalReservationMatches(Authority) ||
					!KingdomFoundingTransaction.SiteReservationMatches(Z, Authority) ||
					!KingdomFoundingTransactionRules.TryParseAuthority(Authority,
						out foundingAuthority) ||
					foundingAuthority.Kind != KingdomFoundingKind.FirstCity ||
					foundingAuthority.RealmFaction != system.KingdomFactionName ||
					foundingAuthority.ZoneID != Z.ZoneID)
				{
					return false;
				}
			}
			else if (!string.IsNullOrEmpty(Authority))
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
			if (KingdomRules.GroundIsForeignFaction(Z.GetZoneProperty("faction"), system.KingdomFactionName))
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
			bool firstRealmClaim = system.SettlementCount == 1
				&& system.ClaimedZones.Count == 0;
			Faction faction = reservedRealm;
			if (!KingdomFoundingTransaction.FactionRegistryCoherent(
				system.KingdomFactionName, faction) ||
				faction.GetIntProperty("PlayerKingdom") != 1 ||
				faction.GetIntProperty("Village") != 1 ||
				(reserved &&
				 (faction.GetStringProperty(
					 KingdomFoundingTransaction.PendingFactionTransactionProperty, null) !=
						foundingAuthority.TransactionID ||
				  faction.GetStringProperty(
					 KingdomFoundingTransaction.PendingFactionAuthorityProperty, null) !=
						Authority)))
			{
				return false;
			}
			bool alreadyClaimed = system.ClaimedZones.Contains(Z.ZoneID);
			string claimEvent = Z.GetZoneProperty(
				KingdomFoundingTransaction.ClaimChronicleEventProperty, null);
			string expectedClaimEvent = reserved
				? KingdomFoundingTransaction.FoundingEventID(
					KingdomFoundingKind.FirstCity, foundingAuthority.TransactionID, "claim")
				: claimEvent;
			if (string.IsNullOrEmpty(expectedClaimEvent))
			{
				expectedClaimEvent = "taf:claim:v1:" + Guid.NewGuid().ToString("N");
			}
			if (string.IsNullOrEmpty(claimEvent))
			{
				Z.SetZoneProperty(KingdomFoundingTransaction.ClaimChronicleEventProperty,
					expectedClaimEvent);
				Z.SetZoneProperty(KingdomFoundingTransaction.ClaimChronicleStageProperty,
					alreadyClaimed ? "2" : "0");
				Z.SetZoneProperty(
					KingdomFoundingTransaction.ClaimChronicleDispositionProperty,
					((int)(alreadyClaimed
						? KingdomChronicleDisposition.Skipped
						: KingdomChronicleDisposition.None)).ToString());
				Z.SetZoneProperty(KingdomFoundingTransaction.ClaimFoundingProperty,
					firstRealmClaim ? "1" : "0");
				claimEvent = Z.GetZoneProperty(
					KingdomFoundingTransaction.ClaimChronicleEventProperty, null);
			}
			if (claimEvent != expectedClaimEvent)
			{
				return false;
			}
			string foundingRaw = Z.GetZoneProperty(
				KingdomFoundingTransaction.ClaimFoundingProperty, null);
			if ((foundingRaw != "0" && foundingRaw != "1") ||
				(reserved && foundingRaw != "1"))
			{
				return false;
			}
			bool foundingClaim = foundingRaw == "1";
			int ClaimStage()
			{
				string raw = Z.GetZoneProperty(
					KingdomFoundingTransaction.ClaimChronicleStageProperty, null);
				if (!int.TryParse(raw, out var stage) || stage < 0 || stage > 2)
				{
					throw new InvalidOperationException("The claim chronicle stage is malformed.");
				}
				return stage;
			}
			Z.SetZoneProperty("faction", system.KingdomFactionName);
			if (faction != null && !faction.HolyPlaces.Contains(Z.ZoneID))
			{
				faction.HolyPlaces.Add(Z.ZoneID);
			}
			try
			{
				KingdomFoundingTransaction.RecordChronicleOnce(system, claimEvent,
					KingdomPresentation.Rich(system.KingdomDisplayName) + " claimed " +
					Grammar.GetProsaicZoneName(Z),
					Accomplishment: false, MuralText: null,
					ReadStage: ClaimStage,
					WriteStage: stage => Z.SetZoneProperty(
						KingdomFoundingTransaction.ClaimChronicleStageProperty,
						stage.ToString()),
					ReadDisposition: () => Z.HasZoneProperty(
						KingdomFoundingTransaction.ClaimChronicleDispositionProperty)
						? (int?)int.Parse(Z.GetZoneProperty(
							KingdomFoundingTransaction.ClaimChronicleDispositionProperty, null))
						: null,
					WriteDisposition: disposition => Z.SetZoneProperty(
						KingdomFoundingTransaction.ClaimChronicleDispositionProperty,
						disposition.ToString()),
					ValidateAuthority: reserved
						? (Func<bool>)(() =>
							KingdomFoundingTransaction.FoundingAuthorityStillExact(
								Authority, Z))
						: null);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("claim chronicle remains recoverable: " + ex.Message);
				return false;
			}
			if (!alreadyClaimed)
			{
				system.ClaimedZones.Add(Z.ZoneID);
			}
			if (!system.ClaimedZones.Contains(Z.ZoneID) ||
				Z.GetZoneProperty("faction", null) != system.KingdomFactionName ||
				!faction.HolyPlaces.Contains(Z.ZoneID) || ClaimStage() != 2 ||
				Z.GetZoneProperty(
					KingdomFoundingTransaction.ClaimChronicleDispositionProperty, null) !=
					((int)KingdomChronicleDisposition.Skipped).ToString())
			{
				return false;
			}
			if (system.NextArrivalTick <= 0)
			{
				system.NextArrivalTick = The.Game.TimeTicks + KingdomRules.ArrivalIntervalTicks(system.Population);
			}
			if (StageSnapshot)
			{
				string sealFailure;
				bool sealedClaim;
				if (foundingClaim)
				{
					sealedClaim = KingdomSeal.TryFoundingCompleted(out sealFailure);
				}
				else
				{
					sealedClaim = KingdomSeal.TryStageSemanticSnapshot(
						"ground claimed", out sealFailure);
				}
					if (!sealedClaim)
					{
						// Faction, holy place, chronicle, and ClaimedZones are already durable.
						// Report committed so Charter charges exactly once; seal dirty state is its
						// own retryable outbox and will flush on later Charter/zone boundaries.
						KingdomSeal.MarkSemanticDirty("ground claim seal pending");
						KingdomLog.Log("claim seal remains pending: " + sealFailure);
						return true;
					}
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
		/// Enrols a creature as a citizen by owning exactly one namespaced slot in its base
		/// allegiance. Every other slot, temporary layer, flag and Brain field remains untouched.
		/// </summary>
		/// <param name="Citizen">The creature. The player is rejected; so is anything brainless.</param>
		/// <returns>True if enrolled, false if unfounded or the target is ineligible.</returns>
		/// <remarks>Enrolled creatures are protected: kingdom systems never destroy a citizen
		/// they did not themselves create (see the protection law in STANDARDS 7). Settlers
		/// spawned by the growth engine additionally carry KingdomBorn and may emigrate.</remarks>
		public static bool EnrollCitizen(GameObject Citizen)
		{
			return EnrollCitizen(Citizen, KingdomCitizenshipEnrollmentReason.Arrival);
		}

		public static bool EnrollCitizen(GameObject Citizen,
			KingdomCitizenshipEnrollmentReason Reason)
		{
			return EnrollCitizen(Citizen, Reason,
				The.Game == null ? 0L : The.Game.TimeTicks);
		}

		public static bool EnrollCitizen(GameObject Citizen,
			KingdomCitizenshipEnrollmentReason Reason, long FrozenAppliedTick)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			string failure;
			bool enrolled = KingdomCitizenship.TryEnroll(system, Citizen, Reason,
				FrozenAppliedTick, out failure);
			if (!enrolled && !string.IsNullOrEmpty(failure))
				KingdomLog.Log("citizenship: enrolment refused (" + failure + ")");
			return enrolled;
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
		private const string RuinRestorationTransactionProperty =
			"r_TAF_RuinRestorationTransaction_v1";

		/// <summary>Second-founding restoration receipt. Each eligible object retains the
		/// exact transaction before KingdomBuilt changes, so interruption can recount and
		/// finish the same set without losing already-stamped structures.</summary>
		internal static bool TryRestoreRuinStructures(Zone Site, string TransactionId,
			out int Restored)
		{
			Restored = 0;
			if (Site == null || !KingdomIdentityRules.IsFoundingTransaction(TransactionId))
				return false;
			try
			{
				List<GameObject> objects = Site.GetObjects();
				if (objects == null || objects.Count > 65536) return false;
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (!GameObject.Validate(item)) return false;
					bool eligible = item.HasPart("Bed") || item.HasPart("Shrine");
					string owner = item.GetStringProperty(
						RuinRestorationTransactionProperty, null);
					if (!string.IsNullOrEmpty(owner) && owner != TransactionId)
					{
						// Completed furniture from an older realm is ordinary prebuilt ground for
						// this rite. Only a foreign incomplete or malformed marker blocks reuse.
						if (eligible && item.GetIntProperty("KingdomBuilt") == 1) continue;
						return false;
					}
					if (owner == TransactionId)
					{
						if (!eligible) return false;
						if (item.GetIntProperty("KingdomBuilt") != 1)
							item.SetIntProperty("KingdomBuilt", 1);
						if (item.GetIntProperty("KingdomBuilt") != 1) return false;
						continue;
					}
					if (!eligible || item.GetIntProperty("KingdomBuilt") == 1) continue;
					item.SetStringProperty(RuinRestorationTransactionProperty, TransactionId);
					if (item.GetStringProperty(RuinRestorationTransactionProperty, null) !=
						TransactionId) return false;
					item.SetIntProperty("KingdomBuilt", 1);
					if (item.GetIntProperty("KingdomBuilt") != 1) return false;
				}
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (item.GetStringProperty(RuinRestorationTransactionProperty, null) !=
						TransactionId) continue;
					if (item.GetIntProperty("KingdomBuilt") != 1 ||
						(!item.HasPart("Bed") && !item.HasPart("Shrine"))) return false;
					if (Restored == int.MaxValue) return false;
					Restored++;
				}
				return true;
			}
			catch
			{
				Restored = 0;
				return false;
			}
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
			Faction village = Factions.GetIfExists(VillageFactionName);
			if (KingdomFoundingTransaction.HasGlobalReservation() ||
				!KingdomFoundingTransaction.FactionRegistryCoherent(
					VillageFactionName, village) ||
				village.GetIntProperty("Village") != 1 ||
				village.DisplayName != VillageDisplayName)
			{
				return;
			}
			if (System.GetStanding(VillageFactionName) < KingdomRules.VillageCharterSealedStanding)
			{
				System.SetStanding(VillageFactionName, KingdomRules.VillageCharterSealedStanding);
			}
			KingdomFoundingTransaction.RecordChronicleAtomically(System,
				"you asked, and " + KingdomPresentation.Rich(VillageDisplayName) +
				" agreed: their ground stays theirs, and a covenant now stands between them and " +
				KingdomPresentation.Rich(System.KingdomDisplayName), Accomplishment: true);
			string sealFailure;
			KingdomSeal.TryStageSemanticSnapshot("village charter", out sealFailure);
		}
	}
}
