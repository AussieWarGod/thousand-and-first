using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		public static bool TryCapture(KingdomSystem System, string ChronicleRegistry,
			string ChronicleFault, long ClosedTick, string ExileDeed,
			out KingdomRealmArchive Archive, out string Failure)
		{
			Archive = null;
			Failure = null;
			if (System == null || !string.IsNullOrEmpty(System.IdentityFault) ||
				!string.IsNullOrEmpty(System.PendingSettlementId) ||
				!string.IsNullOrEmpty(System.PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(System.PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(System.PendingSettlementAuthority) ||
				!KingdomIdentityRules.IsRealmId(System.CurrentRealmId) ||
				!System.TryExactSettlementIds(RequirePublishedClaims: true,
					out List<string> settlementIds, out Failure) ||
				!System.TryRetainedSettlementIds(RequirePublishedClaims: true,
					IncludePending: false, out List<string> retainedIds, out Failure))
			{
				Failure = "current immutable realm identity cannot be proved";
				return false;
			}
			if (ClosedTick < 0L || !BoundedText(ExileDeed))
			{
				Failure = "realm archive tick or deed is not bounded";
				return false;
			}
			KingdomSettlement capturedSeat;
			try { capturedSeat = System.Capture(); }
			catch (Exception ex)
			{
				Failure = "seated settlement capture failed: " + Bound(ex.Message, 512);
				return false;
			}
			if (!KingdomArchivedSettlementCodec.TryClone(capturedSeat,
				out KingdomSettlement frozenSeat, out Failure) ||
				System.SettlementTopology == null ||
				!System.SettlementTopology.TryClone(
					out KingdomSettlementTopology frozenTopology, out Failure) ||
				!KingdomArchivedSettlementCodec.TryClone(System.Seceded,
					out KingdomSettlement frozenSeceded, out Failure) ||
				!TryCloneCarry(System.CarryBook, out KingdomCarryBook frozenCarry, out Failure) ||
				!ExactCarrySettlementIds(frozenCarry, retainedIds))
			{
				Failure = Failure ?? "retained Carry topology does not match the archived realm";
				return false;
			}
			KingdomRealmArchive candidate;
			try
			{
				candidate = new KingdomRealmArchive
				{
				RealmId = System.RealmId,
				FactionName = System.KingdomFactionName,
				DisplayName = System.KingdomDisplayName,
				ExileDeed = ExileDeed,
				ClosedTick = ClosedTick,
				SettlementIds = new List<string>(settlementIds),
				RealmIdentityVersion = System.RealmIdentityVersion,
				RealmIdentityOrigin = System.RealmIdentityOrigin,
				RealmIdentityTransactionId = System.RealmIdentityTransactionId,
				RealmIdentityLegacyFaction = System.RealmIdentityLegacyFaction,
				RealmIdentityFoundedTick = System.RealmIdentityFoundedTick,
				RealmIdentitySeedHigh = System.RealmIdentitySeedHigh,
				RealmIdentitySeedLow = System.RealmIdentitySeedLow,
				RealmIdentityFirstClaimedZone = System.RealmIdentityFirstClaimedZone,
					SimulationSeedHigh = System.SimulationSeedHigh,
					SimulationSeedLow = System.SimulationSeedLow,
					Seat = frozenSeat,
					SettlementTopology = frozenTopology,
					Away = frozenTopology.Get(0),
				Standings = CloneStandings(System.RegardForRealm),
				RealmPolicyToward = CloneStandings(System.RealmPolicyToward),
				RegardSpilloverRemainders = CloneStandings(
					System.RegardSpilloverRemainders),
				RegardSpilloverObservedReputation = CloneStandings(
					System.RegardSpilloverObservedReputation),
				DirectionalStandingSchemaVersion = System.DirectionalStandingSchemaVersion,
				CallbackAuthoritySchemaVersion = 2,
				Bindings = CloneBindings(System.Bindings),
				ResidentCounter = System.ResidentCounter,
				Jobs = CloneJobs(System.Jobs),
				LastSliceTick = System.LastSliceTick,
				ReifyTick = System.ReifyTick,
				ReifyThirdsSpent = System.ReifyThirdsSpent,
				ReifyHeavySpent = System.ReifyHeavySpent,
				ReifyQuietUntilTick = System.ReifyQuietUntilTick,
				DedicationCounter = System.DedicationCounter,
				ChronicleEntries = CloneStrings(System.ChronicleEntries),
				OutsiderEntries = CloneStrings(System.OutsiderEntries),
				ChronicleRegistry = ChronicleRegistry,
				ChronicleRegistryFault = ChronicleFault,
				RegardSpoken = System.RegardSpoken,
				Dissent = System.Dissent,
				DissentSpoken = System.DissentSpoken,
				LastDissentTick = System.LastDissentTick,
				DeclaredCreed = System.DeclaredCreed,
				DishName = System.DishName,
				DishText = System.DishText,
				DishStaple = System.DishStaple,
				DishSource = System.DishSource,
				LastRiteTick = System.LastRiteTick,
				LastSoulRiteTick = System.LastSoulRiteTick,
				Seceded = frozenSeceded,
				SecededTick = System.SecededTick,
				Haul = CloneHaul(System.Haul),
				CarryBook = frozenCarry,
				SeatWireVersion = KingdomArchivedSettlementCodec.CurrentVersion,
				AwayWireVersion = KingdomArchivedSettlementCodec.CurrentVersion,
				SecededWireVersion = KingdomArchivedSettlementCodec.CurrentVersion
				};
			}
			catch (Exception ex)
			{
				Failure = "realm graph clone failed: " + Bound(ex.Message, 512);
				return false;
			}
			if (!candidate.TryRefreshDirectionalStandingDigest(out Failure) ||
				!candidate.Validate(out Failure) ||
				!candidate.CurrentGraphMatches(System, out Failure)) return false;
			Archive = candidate;
			return true;
		}

		/// <summary>Hashes the authoritative archived realm while excluding only the field family
		/// the named callback is allowed to update. The current and later callback receipts plus
		/// transition phase are excluded to avoid self-reference; every earlier settled receipt is
		/// included so established callback proof cannot be changed behind a later callback.</summary>
	}
}
