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
		/// <summary>Resolves a mutable city name only when it denotes exactly one proven current
		/// city. It returns the immutable id; no caller receives first-match authority.</summary>
		internal bool TryResolveSettlementIdByName(string Name, out string SettlementId)
		{
			SettlementId = null;
			if (string.IsNullOrEmpty(Name)) return false;
			List<string> identities;
			string failure;
			if (!TryExactSettlementIds(RequirePublishedClaims: true, out identities,
				out failure)) return false;
			List<string> names = new List<string> { SettlementName };
			List<string> ids = new List<string> { City.SettlementId };
			List<KingdomSettlement> nonSeat = NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
			{
				names.Add(nonSeat[i].SettlementName);
				ids.Add(nonSeat[i].City.SettlementId);
			}
			KingdomIdentityFault fault;
			return KingdomIdentityRules.TryResolveUniqueSettlementName(names, ids, Name,
				out SettlementId, out fault);
		}

		internal bool TryExactSettlementIds(bool RequirePublishedClaims,
			out List<string> SettlementIds, out string Failure)
		{
			SettlementIds = new List<string>();
			Failure = null;
			if (!string.IsNullOrEmpty(IdentityFault))
			{
				Failure = IdentityFault;
				return false;
			}
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ReproveRealm(RealmId, RealmIdentityVersion,
				RealmIdentityOrigin, RealmIdentityTransactionId,
				RealmIdentityLegacyFaction, RealmIdentityFoundedTick,
				RealmIdentitySeedHigh, RealmIdentitySeedLow,
				RealmIdentityFirstClaimedZone, out fault) ||
				!SettlementIdentityMatches(City, SettlementIdentityVersion,
					SettlementIdentityOrigin, SettlementIdentityTransactionId,
					SettlementIdentityFoundedTick, SettlementIdentityFirstClaimedZone,
					RequirePublishedClaims, ClaimedZones, out fault) ||
				!LifecycleIdentityMatches(LifecycleBook, City?.SettlementId) ||
				!CarryIdentityMatches())
			{
				Failure = "The seated city identity cannot be reproved (" + fault + ").";
				return false;
			}
			SettlementIds.Add(City.SettlementId);
			List<KingdomSettlement> nonSeat = NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
			{
				KingdomSettlement row = nonSeat[i];
				if (!SettlementIdentityMatches(row.City, row.SettlementIdentityVersion,
					row.SettlementIdentityOrigin, row.SettlementIdentityTransactionId,
					row.SettlementIdentityFoundedTick,
					row.SettlementIdentityFirstClaimedZone, RequirePublishedClaims,
					row.ClaimedZones, out fault) ||
					!LifecycleIdentityMatches(row.LifecycleBook, row.City?.SettlementId))
				{
					Failure = "A non-seat city identity cannot be reproved (" + fault + ").";
					return false;
				}
				SettlementIds.Add(row.City.SettlementId);
			}
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out fault))
			{
				Failure = "The complete city identity set is invalid (" + fault + ").";
				return false;
			}
			SettlementIds.Sort(StringComparer.Ordinal);
			return true;
		}

		/// <summary>Returns the monotone authority topology: active cities, any retained seceded
		/// city, and optionally the exact pending later-city tuple.</summary>
		internal bool TryRetainedSettlementIds(bool RequirePublishedClaims,
			bool IncludePending, out List<string> SettlementIds, out string Failure)
		{
			if (!TryExactSettlementIds(RequirePublishedClaims, out SettlementIds,
				out Failure)) return false;
			KingdomIdentityFault fault = KingdomIdentityFault.None;
			if (Seceded != null)
			{
				if (!SettlementIdentityMatches(Seceded.City,
					Seceded.SettlementIdentityVersion, Seceded.SettlementIdentityOrigin,
					Seceded.SettlementIdentityTransactionId,
					Seceded.SettlementIdentityFoundedTick,
					Seceded.SettlementIdentityFirstClaimedZone, RequirePublishedClaims,
					Seceded.ClaimedZones, out fault) ||
					!LifecycleIdentityMatches(Seceded.LifecycleBook,
						Seceded.City?.SettlementId))
				{
					Failure = "The retained seceded city identity cannot be reproved (" +
						fault + ").";
					return false;
				}
				SettlementIds.Add(Seceded.City.SettlementId);
			}
			if (IncludePending && (!string.IsNullOrEmpty(PendingSettlementId) ||
				!string.IsNullOrEmpty(PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(PendingSettlementAuthority)))
			{
				if (!PendingSettlementTupleValid(out Failure)) return false;
				if (!SettlementIds.Contains(PendingSettlementId))
					SettlementIds.Add(PendingSettlementId);
			}
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out fault))
			{
				Failure = "The retained city identity set is invalid (" + fault + ").";
				return false;
			}
			SettlementIds.Sort(StringComparer.Ordinal);
			return true;
		}

		private bool TryBindDormantLifecycleIdentity(out string Failure)
		{
			Failure = null;
			if (LifecycleBook == null) LifecycleBook = new KingdomLifecycleBook();
			KingdomLifecycleRules.Normalize(LifecycleBook);
			List<string> otherLifecycleIds = LifecycleCollisionIds(
				IncludeSeat: false, IncludeAway: true);
			if (!KingdomLifecycleRules.BindSettlementIdentity(LifecycleBook,
				City?.SettlementId, LegacyMigration: false, MigrationKey: null,
				ExistingIds: otherLifecycleIds))
			{
				LifecycleBook.Quarantined = true;
				LifecycleBook.Fault =
					"lifecycle book could not bind exact seated-city identity";
				Failure = LifecycleBook.Fault;
				return false;
			}
			if (CarryBook == null) CarryBook = new KingdomCarryBook();
			KingdomLifecycleRules.Normalize(CarryBook);
			List<string> carrySettlementIds;
			if (!TryExpectedCarryTopology(out carrySettlementIds, out Failure))
			{
				CarryBook.Quarantined = true;
				CarryBook.Fault =
					"carry book could not bind exact immutable realm topology";
				Failure = Failure ?? CarryBook.Fault;
				return false;
			}
			// Pending publication permits exactly two cut states: old retained topology or
			// expanded topology. Never ask BindCarryIdentity to reinterpret an already-bound
			// old book as corruption before the paired coordinator can recover forward.
			if (CarryIdentityMatches(carrySettlementIds)) return true;
			if (KingdomLifecycleRules.CanOwnAuthority(CarryBook) &&
				CarryIdentityMatches()) return true;
			if (KingdomLifecycleRules.BindCarryIdentity(CarryBook, RealmId,
				carrySettlementIds, LegacyMigration: false, MigrationKey: null))
			{
				KingdomLifecycleRules.Normalize(CarryBook);
				if (CarryIdentityMatches(carrySettlementIds)) return true;
			}
			CarryBook.Quarantined = true;
			CarryBook.Fault = "carry book could not bind exact immutable realm identity";
			Failure = CarryBook.Fault;
			return false;
		}

		private static bool LifecycleIdentityMatches(KingdomLifecycleBook Book,
			string SettlementId)
		{
			return Book != null && !Book.LegacyIdentity &&
				string.Equals(Book.SettlementId, SettlementId,
					StringComparison.Ordinal) &&
				KingdomLifecycleRules.CanOwnAuthority(Book);
		}

		private bool CarryIdentityMatches()
		{
			List<string> expected;
			string failure;
			if (!TryExpectedCarryTopology(out expected, out failure)) return false;
			if (CarryIdentityMatches(expected)) return true;
			// A proved pending later-city tuple is a durable redo barrier. Save cuts may
			// therefore retain either the old exact Carry set or the expanded exact set;
			// no third topology is accepted.
			if (!string.IsNullOrEmpty(PendingSettlementId) &&
				expected.Remove(PendingSettlementId))
			{
				KingdomIdentityFault fault;
				return KingdomIdentityRules.ValidateRealmTopology(RealmId, expected,
					out fault) && CarryIdentityMatches(expected);
			}
			return false;
		}

		private bool CarryIdentityMatches(IList<string> Expected)
		{
			if (CarryBook == null || CarryBook.LegacyIdentity || Expected == null ||
				CarryBook.SettlementIds == null ||
				CarryBook.SettlementIds.Count != Expected.Count ||
				!string.Equals(CarryBook.RealmId, RealmId, StringComparison.Ordinal) ||
				!KingdomLifecycleRules.CanOwnAuthority(CarryBook)) return false;
			for (int i = 0; i < Expected.Count; i++)
				if (!string.Equals(CarryBook.SettlementIds[i], Expected[i],
					StringComparison.Ordinal)) return false;
			return true;
		}

		private bool TryExpectedCarryTopology(out List<string> SettlementIds,
			out string Failure)
		{
			SettlementIds = new List<string>();
			Failure = null;
			AddLifecycleCollisionId(SettlementIds,
				new HashSet<string>(StringComparer.Ordinal), City?.SettlementId);
			HashSet<string> seen = new HashSet<string>(SettlementIds,
				StringComparer.Ordinal);
			List<KingdomSettlement> nonSeat = NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				AddLifecycleCollisionId(SettlementIds, seen, nonSeat[i]?.City?.SettlementId);
			AddLifecycleCollisionId(SettlementIds, seen, Seceded?.City?.SettlementId);
			bool hasPending = !string.IsNullOrEmpty(PendingSettlementId) ||
				!string.IsNullOrEmpty(PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(PendingSettlementAuthority);
			if (hasPending)
			{
				if (!PendingSettlementTupleValid(out Failure)) return false;
				AddLifecycleCollisionId(SettlementIds, seen, PendingSettlementId);
			}
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out fault))
			{
				Failure = "The retained carry topology is invalid (" + fault + ").";
				return false;
			}
			SettlementIds.Sort(StringComparer.Ordinal);
			return true;
		}

	}
}
