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
		private bool RealmTransitionActive()
		{
			if (ExiledRealmArchive == null ||
				ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Closed ||
				ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Restored) return false;
			if (ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Quarantined)
				return string.Equals(RealmId, ExiledRealmArchive.RealmId,
					StringComparison.Ordinal);
			return true;
		}

		/// <summary>Stages the first realm and city ids before faction registration or any engine
		/// callback. A retry accepts only the exact same transaction and founding ground.</summary>
		internal bool TryBindFirstFoundingIdentity(string TransactionId, string ZoneId,
			out string Failure)
		{
			Failure = null;
			string realm;
			string settlement;
			KingdomIdentityFault fault = KingdomIdentityFault.None;
			if (string.IsNullOrEmpty(ZoneId) || ZoneId.Length > 512 ||
				!KingdomIdentityRules.TryMintRealm(TransactionId, out realm, out fault) ||
				!KingdomIdentityRules.TryMintSettlement(realm, TransactionId,
					out settlement, out fault))
			{
				Failure = "The first founding transaction could not mint bounded immutable identity (" +
					fault + ").";
				return false;
			}
			if (FirstIdentityStateEmpty())
			{
				KingdomLifecycleBook preparedLifecycle;
				KingdomCarryBook preparedCarry;
				if (!KingdomLifecycleRules.TryPrepareFirstIdentityBooks(LifecycleBook,
					CarryBook, realm, settlement, out preparedLifecycle,
					out preparedCarry))
				{
					Failure = "Dormant lifecycle or carry evidence is not pristine.";
					return false;
				}
				RealmId = realm;
				RealmIdentityVersion = KingdomIdentityRules.RulesVersion;
				RealmIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction;
				RealmIdentityTransactionId = TransactionId;
				RealmIdentityLegacyFaction = null;
				RealmIdentityFoundedTick = 0L;
				RealmIdentitySeedHigh = 0UL;
				RealmIdentitySeedLow = 0UL;
				RealmIdentityFirstClaimedZone = ZoneId;
				IdentityFault = null;
				if (City == null) City = new Simulation.City.KingdomCityBook();
				City.SettlementId = settlement;
				SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
				SettlementIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction;
				SettlementIdentityTransactionId = TransactionId;
				SettlementIdentityFoundedTick = 0L;
				SettlementIdentityFirstClaimedZone = ZoneId;
				SettlementIdentityLegacyId = null;
				LifecycleBook = preparedLifecycle;
				CarryBook = preparedCarry;
			}
			if (TryBindDormantLifecycleIdentity(out Failure))
			{
				if (FirstIdentityMatches(TransactionId, ZoneId)) return true;
				// An exact pending tuple owned by another transaction is not corruption. Refuse
				// without poisoning the only authority that can resume it.
				if (FirstIdentityMatches(RealmIdentityTransactionId,
					RealmIdentityFirstClaimedZone))
				{
					Failure = "The immutable first founding belongs to another transaction or site.";
					return false;
				}
			}
			QuarantineIdentity("first-founding immutable identity is partial or replaced");
			Failure = IdentityFault;
			return false;
		}

		internal bool FirstIdentityMatches(string TransactionId, string ZoneId)
		{
			KingdomIdentityFault fault = KingdomIdentityFault.None;
			return string.IsNullOrEmpty(IdentityFault) && NonSeatSettlementCount == 0 && City != null &&
				RealmIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction &&
				SettlementIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction &&
				RealmIdentityTransactionId == TransactionId &&
				SettlementIdentityTransactionId == TransactionId &&
				RealmIdentityFirstClaimedZone == ZoneId &&
				SettlementIdentityFirstClaimedZone == ZoneId &&
				KingdomIdentityRules.ReproveRealm(RealmId, RealmIdentityVersion,
					RealmIdentityOrigin, RealmIdentityTransactionId,
					RealmIdentityLegacyFaction, RealmIdentityFoundedTick,
					RealmIdentitySeedHigh, RealmIdentitySeedLow,
					RealmIdentityFirstClaimedZone, out fault) &&
				KingdomIdentityRules.ReproveSettlement(City.SettlementId, RealmId,
					SettlementIdentityVersion, SettlementIdentityOrigin,
					SettlementIdentityTransactionId, SettlementIdentityFoundedTick,
					SettlementIdentityFirstClaimedZone, out fault) &&
				LifecycleIdentityMatches(LifecycleBook, City.SettlementId) &&
				CarryIdentityMatches();
		}

		/// <summary>Computes (without publishing) the later city's exact id. The caller freezes it
		/// on the founding site before any permanent city marker or Away assignment.</summary>
		internal bool TryPrepareLaterSettlementIdentity(string TransactionId, string ZoneId,
			out string SettlementId, out string Failure)
		{
			SettlementId = null;
			Failure = null;
			List<string> current;
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out current, out Failure))
				return false;
			KingdomIdentityFault fault = KingdomIdentityFault.None;
			if (string.IsNullOrEmpty(ZoneId) || ZoneId.Length > 512 ||
				!KingdomIdentityRules.TryMintSettlement(RealmId, TransactionId,
					out SettlementId, out fault))
			{
				Failure = "The later founding transaction could not mint immutable city identity (" +
					fault + ").";
				SettlementId = null;
				return false;
			}
			if (current.Contains(SettlementId))
			{
				bool exactPendingPublication = PendingSettlementTupleValid(out string _) &&
					PendingSettlementId == SettlementId &&
					PendingSettlementTransactionId == TransactionId &&
					PendingSettlementZoneId == ZoneId &&
					!string.IsNullOrEmpty(PendingSettlementAuthority) &&
					(SeatedLaterIdentityMatches(SettlementId, TransactionId, ZoneId) ||
					 LaterSettlementIdentityMatches(FindNonSeatSettlementById(SettlementId),
						SettlementId, TransactionId, ZoneId));
				if (exactPendingPublication) return true;
				Failure = "The later founding transaction collides with an existing city identity.";
				SettlementId = null;
				return false;
			}
			return true;
		}

		/// <summary>Returns every other retained lifecycle identity that a binding must scan.
		/// Exact ids are de-duplicated because archived mirrors may name the same retained city.</summary>
		internal List<string> LifecycleCollisionIds(bool IncludeSeat, bool IncludeAway)
		{
			List<string> ids = new List<string>();
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			if (IncludeSeat) AddLifecycleCollisionId(ids, seen, City?.SettlementId);
			if (IncludeAway)
			{
				List<KingdomSettlement> nonSeat = NonSeatSettlements();
				for (int i = 0; i < nonSeat.Count; i++)
					AddLifecycleCollisionId(ids, seen, nonSeat[i]?.City?.SettlementId);
			}
			AddLifecycleCollisionId(ids, seen, Seceded?.City?.SettlementId);
			AddLifecycleCollisionId(ids, seen, ExiledSeat?.City?.SettlementId);
			List<KingdomSettlement> exiled = ExiledSettlementTopology?.Snapshot();
			if (exiled != null)
				for (int i = 0; i < exiled.Count; i++)
					AddLifecycleCollisionId(ids, seen, exiled[i]?.City?.SettlementId);
			ids.Sort(StringComparer.Ordinal);
			return ids;
		}

		private static void AddLifecycleCollisionId(List<string> Ids,
			HashSet<string> Seen, string Id)
		{
			if (!string.IsNullOrEmpty(Id) && Seen.Add(Id)) Ids.Add(Id);
		}

		internal static bool TryBindSettlementIdentity(KingdomSettlement Settlement,
			string SettlementId, string TransactionId, string ZoneId, long FoundedTick,
			ICollection<string> ExistingSettlementIds, out string Failure)
		{
			Failure = null;
			if (Settlement == null)
			{
				Failure = "No settlement record was supplied for immutable identity.";
				return false;
			}
			if (Settlement.City == null)
				Settlement.City = new Simulation.City.KingdomCityBook();
			Settlement.City.SettlementId = SettlementId;
			Settlement.SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
			Settlement.SettlementIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction;
			Settlement.SettlementIdentityTransactionId = TransactionId;
				Settlement.SettlementIdentityFoundedTick = FoundedTick;
				Settlement.SettlementIdentityFirstClaimedZone = ZoneId;
				Settlement.SettlementIdentityLegacyId = null;
			if (Settlement.LifecycleBook == null)
				Settlement.LifecycleBook = new KingdomLifecycleBook();
			KingdomLifecycleRules.Normalize(Settlement.LifecycleBook);
			if (KingdomLifecycleRules.BindSettlementIdentity(Settlement.LifecycleBook,
				SettlementId, LegacyMigration: false, MigrationKey: null,
				ExistingIds: ExistingSettlementIds)) return true;
			Settlement.LifecycleBook.Quarantined = true;
			Settlement.LifecycleBook.Fault =
				"lifecycle book could not bind the exact new settlement identity";
			Failure = Settlement.LifecycleBook.Fault;
			return false;
		}

		internal bool LaterSettlementIdentityMatches(KingdomSettlement Settlement,
			string ExpectedId, string TransactionId, string ZoneId)
		{
			if (Settlement == null || Settlement.City == null ||
				Settlement.SettlementIdentityFirstClaimedZone != ZoneId ||
				Settlement.ClaimedZones == null || !Settlement.ClaimedZones.Contains(ZoneId))
				return false;
			KingdomIdentityFault fault;
			return string.Equals(Settlement.City.SettlementId, ExpectedId,
					StringComparison.Ordinal) &&
				Settlement.SettlementIdentityOrigin ==
					KingdomIdentityOrigin.FoundingTransaction &&
				Settlement.SettlementIdentityTransactionId == TransactionId &&
				KingdomIdentityRules.ReproveSettlement(Settlement.City.SettlementId,
					RealmId, Settlement.SettlementIdentityVersion,
					Settlement.SettlementIdentityOrigin,
					Settlement.SettlementIdentityTransactionId,
					Settlement.SettlementIdentityFoundedTick,
					Settlement.SettlementIdentityFirstClaimedZone, out fault);
		}

		internal bool SeatedLaterIdentityMatches(string ExpectedId,
			string TransactionId, string ZoneId)
		{
			if (City == null || SettlementIdentityFirstClaimedZone != ZoneId ||
				ClaimedZones == null || !ClaimedZones.Contains(ZoneId)) return false;
			KingdomIdentityFault fault;
			return string.Equals(City.SettlementId, ExpectedId, StringComparison.Ordinal) &&
				SettlementIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction &&
				SettlementIdentityTransactionId == TransactionId &&
				KingdomIdentityRules.ReproveSettlement(City.SettlementId, RealmId,
					SettlementIdentityVersion, SettlementIdentityOrigin,
					SettlementIdentityTransactionId, SettlementIdentityFoundedTick,
					SettlementIdentityFirstClaimedZone, out fault) &&
				LifecycleIdentityMatches(LifecycleBook, City.SettlementId);
		}

	}
}
