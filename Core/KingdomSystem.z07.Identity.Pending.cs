using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		internal bool TryBindTradeIdentity(out string Failure)
		{
			List<string> settlements;
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: true, out settlements, out Failure)) return false;
			if (TradeBook == null) TradeBook = new KingdomTradeBook();
			KingdomTradeRules.Normalize(TradeBook);
			return KingdomTradeRules.BindExactIdentity(TradeBook, RealmId, settlements,
				out Failure);
		}

		/// <summary>Freezes paired detached Trade and Carry replacements. No live authority
		/// changes until the basin has published its forward-recovery barrier.</summary>
		internal bool TryPrepareSecondCityTopology(string NewSettlementId,
			out KingdomSecondCityTopologyPlan Plan, out string Failure)
		{
			Plan = null;
			bool hasPending = !PendingSettlementIdentityAbsent();
			if (hasPending && (!PendingSettlementTupleValid(out Failure) ||
				PendingSettlementId != NewSettlementId))
			{
				Failure = Failure ??
					"Another pending city owns the topology publication barrier.";
				return false;
			}
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out List<string> current, out Failure)) return false;
			return KingdomSecondCityPublicationRules.TryPrepare(RealmId, current,
				NewSettlementId, TradeBook, CarryBook, out Plan, out Failure);
		}

		/// <summary>Publishes a prepared paired replacement after PublicationCommitted.
		/// Exact retries retain both original references and bytes.</summary>
		internal bool TryCommitSecondCityTopology(KingdomSecondCityTopologyPlan Plan,
			string TransactionId, string ZoneId, string Authority, out string Failure)
		{
			Failure = null;
			if (Plan == null || !PendingSettlementTupleMatches(TransactionId, ZoneId,
				Authority) || Plan.SettlementId != PendingSettlementId)
			{
				Failure = "The paired topology plan does not match the pending city tuple.";
				return false;
			}
			return KingdomSecondCityPublicationRules.TryCommit(Plan, ref TradeBook,
				ref CarryBook, out Failure);
		}

		internal bool TryProveSettledSecondCityTopology(out string Failure)
		{
			Failure = null;
			if (!PendingSettlementIdentityAbsent())
			{
				Failure = "The later-city pending tuple has not settled.";
				return false;
			}
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out List<string> published, out Failure)) return false;
			if (KingdomSecondCityPublicationRules.ExactTopology(published, RealmId,
				TradeBook, CarryBook)) return true;
			Failure = "Trade and Carry do not name the exact published city topology.";
			return false;
		}

		internal bool TryStagePendingSettlementIdentity(string SettlementId,
			string TransactionId, string ZoneId, string Authority, out string Failure)
		{
			Failure = null;
			string expected;
			KingdomIdentityFault fault;
			KingdomFoundingAuthority parsed;
			if (!KingdomIdentityRules.TryMintSettlement(RealmId, TransactionId,
					out expected, out fault) || expected != SettlementId ||
				string.IsNullOrEmpty(ZoneId) || ZoneId.Length > 512 ||
				string.IsNullOrEmpty(Authority) || Authority.Length > 4096 ||
				!KingdomFoundingTransactionRules.TryParseAuthority(Authority, out parsed) ||
				parsed.Kind != KingdomFoundingKind.SecondCity ||
				parsed.TransactionID != TransactionId || parsed.ZoneID != ZoneId ||
				parsed.RealmFaction != KingdomFactionName)
			{
				Failure = "The pending city identity tuple is malformed.";
				return false;
			}
			if (string.IsNullOrEmpty(PendingSettlementId) &&
				string.IsNullOrEmpty(PendingSettlementTransactionId) &&
				string.IsNullOrEmpty(PendingSettlementZoneId) &&
				string.IsNullOrEmpty(PendingSettlementAuthority))
			{
				PendingSettlementId = SettlementId;
				PendingSettlementTransactionId = TransactionId;
				PendingSettlementZoneId = ZoneId;
				PendingSettlementAuthority = Authority;
			}
			if (PendingSettlementId == SettlementId &&
				PendingSettlementTransactionId == TransactionId &&
				PendingSettlementZoneId == ZoneId &&
				PendingSettlementAuthority == Authority) return true;
			QuarantineIdentity("pending later-city identity carries a third value");
			Failure = IdentityFault;
			return false;
		}

		internal bool TryAbortPendingSettlementIdentity(string TransactionId,
			string ZoneId, string Authority, out string Failure)
		{
			Failure = null;
			if (PendingSettlementIdentityAbsent()) return true;
			if (!PendingSettlementTupleMatches(TransactionId, ZoneId, Authority))
			{
				Failure = "The pending city tuple does not match abort authority.";
				return false;
			}
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out List<string> published, out Failure) ||
				!KingdomSecondCityPublicationRules.CanAbort(published,
					PendingSettlementId, RealmId, TradeBook, CarryBook))
			{
				Failure = Failure ??
					"Expanded or published city topology can only recover forward.";
				return false;
			}
			ClearPendingSettlementIdentityFields();
			return PendingSettlementIdentityAbsent();
		}

		internal bool TrySettlePendingSettlementIdentity(string TransactionId,
			string ZoneId, string Authority, out string Failure)
		{
			Failure = null;
			if (PendingSettlementIdentityAbsent())
				return TryProveSettledSecondCityTopology(out Failure);
			if (!PendingSettlementTupleMatches(TransactionId, ZoneId, Authority))
			{
				Failure = "The pending city tuple does not match settlement authority.";
				return false;
			}
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out List<string> published, out Failure) ||
				!KingdomSecondCityPublicationRules.CanSettle(published,
					PendingSettlementId, RealmId, TradeBook, CarryBook))
			{
				Failure = Failure ??
					"Published city, Trade, and Carry do not prove one exact topology.";
				return false;
			}
			ClearPendingSettlementIdentityFields();
			return TryProveSettledSecondCityTopology(out Failure);
		}

		private bool PendingSettlementTupleMatches(string TransactionId,
			string ZoneId, string Authority)
		{
			return PendingSettlementTupleValid(out string _) &&
				PendingSettlementTransactionId == TransactionId &&
				PendingSettlementZoneId == ZoneId &&
				PendingSettlementAuthority == Authority;
		}

		private bool PendingSettlementIdentityAbsent()
		{
			return string.IsNullOrEmpty(PendingSettlementId) &&
				string.IsNullOrEmpty(PendingSettlementTransactionId) &&
				string.IsNullOrEmpty(PendingSettlementZoneId) &&
				string.IsNullOrEmpty(PendingSettlementAuthority);
		}

		private void ClearPendingSettlementIdentityFields()
		{
			PendingSettlementId = null;
			PendingSettlementTransactionId = null;
			PendingSettlementZoneId = null;
			PendingSettlementAuthority = null;
		}

		private bool SettlementIdentityMatches(Simulation.City.KingdomCityBook Book,
			int Version, KingdomIdentityOrigin Origin, string TransactionId,
			long IdentityFoundedTick, string FirstClaimedZone, bool RequirePublishedClaim,
			List<string> Claims, out KingdomIdentityFault Fault)
		{
			if (Book == null || string.IsNullOrEmpty(FirstClaimedZone) ||
				(RequirePublishedClaim && (Claims == null ||
				 !Claims.Contains(FirstClaimedZone))))
			{
				Fault = KingdomIdentityFault.InvalidEvidence;
				return false;
			}
			return KingdomIdentityRules.ReproveSettlement(Book.SettlementId, RealmId,
				Version, Origin, TransactionId, IdentityFoundedTick, FirstClaimedZone,
				out Fault);
		}

		private bool FirstIdentityStateEmpty()
		{
			return string.IsNullOrEmpty(RealmId) && RealmIdentityVersion == 0 &&
				RealmIdentityOrigin == KingdomIdentityOrigin.None &&
				string.IsNullOrEmpty(RealmIdentityTransactionId) &&
				string.IsNullOrEmpty(RealmIdentityLegacyFaction) &&
				RealmIdentityFoundedTick == 0L && RealmIdentitySeedHigh == 0UL &&
				RealmIdentitySeedLow == 0UL &&
				string.IsNullOrEmpty(RealmIdentityFirstClaimedZone) &&
				string.IsNullOrEmpty(IdentityFault) && SettlementIdentityVersion == 0 &&
				SettlementIdentityOrigin == KingdomIdentityOrigin.None &&
				string.IsNullOrEmpty(SettlementIdentityTransactionId) &&
				SettlementIdentityFoundedTick == 0L &&
				string.IsNullOrEmpty(SettlementIdentityFirstClaimedZone) &&
				string.IsNullOrEmpty(SettlementIdentityLegacyId) &&
				(City == null || string.IsNullOrEmpty(City.SettlementId)) && Away == null;
		}

		internal void QuarantineIdentity(string Failure)
		{
			if (!string.IsNullOrEmpty(IdentityFault)) return;
			IdentityFault = string.IsNullOrEmpty(Failure)
				? "immutable identity requires inspection"
				: (Failure.Length > 512 ? Failure.Substring(0, 512) : Failure);
			KingdomLog.Log("identity: quarantined: " + IdentityFault);
		}

		/// <summary>
		/// Mints the realm's simulation seed, once, at founding.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE staged this after W0; the implemented kernel says what it has to be:
		/// "whatever mints it must domain-separate on realm incarnation". So it is a pure function
		/// of the world seed, the immutable realm id and the tick the water was poured &mdash; two realms
		/// in one world differ, and the same realm across a reload does not. Re-minting is refused
		/// rather than performed: a seed that moves is a history that did not happen.
		/// </para>
		/// </summary>
		internal bool MintSimulationSeed(int WorldSeed, string ExactRealmId, long FoundedTick)
		{
			if (SimulationSeedHigh != 0UL || SimulationSeedLow != 0UL)
			{
				return false;
			}
			Simulation.Kernel.KernelSeed128 seed;
			Simulation.City.KingdomCityFault fault = Simulation.City.KingdomCityFault.None;
			if (!KingdomIdentityRules.IsRealmId(ExactRealmId) ||
				!Simulation.City.KingdomCityRules.TryMintSeed(WorldSeed, ExactRealmId,
					FoundedTick, out seed, out fault))
			{
				KingdomLog.Log("seed: refused (" + fault + "); the realm runs unseeded until it is founded again");
				return false;
			}
			SimulationSeedHigh = seed.High;
			SimulationSeedLow = seed.Low;
			KingdomLog.Log("seed: minted for immutable realm " + ExactRealmId +
				" at tick " + FoundedTick);
			return true;
		}

		internal bool SimulationSeedMatches(int WorldSeed, string ExactRealmId,
			long FoundedTick)
		{
			Simulation.Kernel.KernelSeed128 expected;
			Simulation.City.KingdomCityFault fault;
			return KingdomIdentityRules.IsRealmId(ExactRealmId) && FoundedTick >= 0L &&
				Simulation.City.KingdomCityRules.TryMintSeed(WorldSeed, ExactRealmId,
					FoundedTick, out expected, out fault) &&
				SimulationSeedHigh == expected.High && SimulationSeedLow == expected.Low;
		}

	}
}
