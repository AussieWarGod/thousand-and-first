using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		private static bool SecondPublished(KingdomSystem System, string Name, string ZoneID,
			string TransactionId)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(Name) ||
				string.IsNullOrEmpty(ZoneID) || System.SettlementCount < 2 ||
				!KingdomIdentityRules.TryMintSettlement(System.RealmId, TransactionId,
					out string expectedId, out KingdomIdentityFault identityFault))
			{
				return false;
			}
			if (System.SettlementName == Name && System.ClaimedZones.Contains(ZoneID) &&
				System.SeatedLaterIdentityMatches(expectedId, TransactionId, ZoneID))
			{
				return true;
			}
			return System.Away != null && System.Away.SettlementName == Name &&
				System.Away.ClaimedZones.Contains(ZoneID) &&
				System.LaterSettlementIdentityMatches(System.Away, expectedId,
					TransactionId, ZoneID);
		}

		/// <summary>A direct contender may retain locks after terminal seat loss only when
		/// immutable forward work exists for this exact transaction. Site reservation alone is
		/// reversible staging and never qualifies.</summary>
		private static bool DirectSecondHasForwardRedo(KingdomSystem System, Zone Site,
			KingdomFoundingAuthority Authority, string EncodedAuthority)
		{
			if (System == null || Site == null || string.IsNullOrEmpty(EncodedAuthority) ||
				Authority.Kind != KingdomFoundingKind.SecondCity ||
				Authority.ZoneID != Site.ZoneID ||
				Authority.RealmFaction != System.KingdomFactionName ||
				!KingdomIdentityRules.TryMintSettlement(System.RealmId,
					Authority.TransactionID, out string settlementId,
					out KingdomIdentityFault fault)) return false;
			if (PublishedSecondAuthorityMatches(Site, EncodedAuthority)) return true;
			bool pending = System.PendingSettlementId == settlementId &&
				System.PendingSettlementTransactionId == Authority.TransactionID &&
				System.PendingSettlementZoneId == Site.ZoneID &&
				System.PendingSettlementAuthority == EncodedAuthority;
			if (pending) return true;
			bool trade = KingdomTradeRules.BookUsable(System.TradeBook) &&
				System.TradeBook.RealmId == System.RealmId &&
				System.TradeBook.SettlementIds != null &&
				System.TradeBook.SettlementIds.Contains(settlementId);
			bool carry = KingdomLifecycleRules.CanOwnAuthority(System.CarryBook) &&
				System.CarryBook.RealmId == System.RealmId &&
				System.CarryBook.SettlementIds != null &&
				System.CarryBook.SettlementIds.Contains(settlementId);
			return trade || carry;
		}

		private static bool PublishedSecondAuthorityMatches(Zone Site, string Authority)
		{
			return Site != null && !string.IsNullOrEmpty(Authority) &&
				Site.GetZoneProperty(SecondPublicationAuthorityProperty, null) == Authority;
		}

		private static bool SecondIsExactSeat(KingdomSystem System, string Name, string ZoneID,
			string TransactionId)
		{
			string expected;
			KingdomIdentityFault fault;
			return System != null && !string.IsNullOrEmpty(Name) &&
				!string.IsNullOrEmpty(ZoneID) && System.SettlementName == Name &&
				System.ClaimedZones.Contains(ZoneID) &&
				KingdomIdentityRules.TryMintSettlement(System.RealmId, TransactionId,
					out expected, out fault) &&
				System.SeatedLaterIdentityMatches(expected, TransactionId, ZoneID);
		}

		private static bool SecondIsExactAway(KingdomSystem System, string Name, string ZoneID,
			string TransactionId)
		{
			string expected;
			KingdomIdentityFault fault;
			return System != null && System.Away != null && !string.IsNullOrEmpty(Name) &&
				!string.IsNullOrEmpty(ZoneID) && System.Away.SettlementName == Name &&
				System.Away.ClaimedZones != null && System.Away.ClaimedZones.Contains(ZoneID) &&
				KingdomIdentityRules.TryMintSettlement(System.RealmId, TransactionId,
					out expected, out fault) &&
				System.LaterSettlementIdentityMatches(System.Away, expected,
					TransactionId, ZoneID);
		}

		private static bool SeatSecond(KingdomSystem System, string Name, Zone Site,
			string Authority, string TransactionId)
		{
			if (System == null || Site == null ||
				!SiteReservationMatches(Site, Authority) ||
				!PublishedSecondAuthorityMatches(Site, Authority))
			{
				return false;
			}
			if (SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId))
			{
				return true;
			}
			if (System.ClaimedZones != null &&
				System.ClaimedZones.Contains(Site.ZoneID))
			{
				// Another seated city claiming the target ground is not this transaction and
				// cannot be displaced merely because the exact target waits in Away.
				return false;
			}
			if (!SecondIsExactAway(System, Name, Site.ZoneID, TransactionId))
			{
				return false;
			}
			KingdomSettlement exactAway = System.Away;
			KingdomSettlement preSeat;
			try
			{
				preSeat = System.Capture();
			}
			catch (Exception ex)
			{
				KingdomLog.Log("second founding pre-Capture retry failed: " + Describe(ex));
				return false;
			}
			try
			{
				if (System.TrySeat(Site) && SecondIsExactSeat(System, Name, Site.ZoneID,
					TransactionId))
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				KingdomLog.Log("second founding TrySeat retry: " + Describe(ex));
				if (SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId) &&
					ReferenceEquals(System.Away, exactAway))
				{
					System.Away = preSeat;
					return true;
				}
			}
			if (SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId))
			{
				if (ReferenceEquals(System.Away, exactAway))
				{
					System.Away = preSeat;
				}
				return System.Away != null && !ReferenceEquals(System.Away, exactAway);
			}
			// TrySeat may be fault-injected at Capture or Restore. Retry those exact operations
			// only while Away is still the same transaction city; never overwrite a newcomer.
			if (!ReferenceEquals(System.Away, exactAway) ||
				!SecondIsExactAway(System, Name, Site.ZoneID, TransactionId))
			{
				return false;
			}
			KingdomSettlement oldSeat = preSeat;
			if (!ReferenceEquals(System.Away, exactAway) ||
				!SecondIsExactAway(System, Name, Site.ZoneID, TransactionId))
			{
				return false;
			}
			try
			{
				System.Restore(exactAway);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("second founding Restore retry failed: " + Describe(ex));
				if (SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId) &&
					ReferenceEquals(System.Away, exactAway))
				{
					System.Away = oldSeat;
					return true;
				}
				return false;
			}
			// Restore is all-or-nothing by KingdomSettlement.WriteTo contract. Only after exact
			// city is seated do we publish captured old seat into Away.
			if (!SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId) ||
				!ReferenceEquals(System.Away, exactAway))
			{
				return false;
			}
			System.Away = oldSeat;
			return SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId) &&
				System.Away == oldSeat;
		}

	}
}
