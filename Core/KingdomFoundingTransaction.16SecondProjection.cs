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
			KingdomSettlement nonSeat = System.FindNonSeatSettlementById(expectedId);
			return nonSeat != null && nonSeat.SettlementName == Name &&
				nonSeat.ClaimedZones.Contains(ZoneID) &&
				System.LaterSettlementIdentityMatches(nonSeat, expectedId,
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

		private static bool SecondIsExactNonSeat(KingdomSystem System, string Name,
			string ZoneID,
			string TransactionId)
		{
			string expected;
			KingdomIdentityFault fault;
			if (System == null || string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(ZoneID) ||
				!KingdomIdentityRules.TryMintSettlement(System.RealmId, TransactionId,
					out expected, out fault)) return false;
			KingdomSettlement nonSeat = System.FindNonSeatSettlementById(expected);
			return nonSeat != null && nonSeat.SettlementName == Name &&
				nonSeat.ClaimedZones != null && nonSeat.ClaimedZones.Contains(ZoneID) &&
				System.LaterSettlementIdentityMatches(nonSeat, expected, TransactionId, ZoneID);
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
				// Another seated city claiming target ground is not this transaction.
				return false;
			}
			if (!SecondIsExactNonSeat(System, Name, Site.ZoneID, TransactionId))
			{
				return false;
			}
			try
			{
				return System.TrySeat(Site) && SecondIsExactSeat(System, Name, Site.ZoneID,
					TransactionId);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("later founding TrySeat retry failed: " + Describe(ex));
				return SecondIsExactSeat(System, Name, Site.ZoneID, TransactionId);
			}
		}

	}
}
