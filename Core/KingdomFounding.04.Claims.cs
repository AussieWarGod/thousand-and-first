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

	}
}
