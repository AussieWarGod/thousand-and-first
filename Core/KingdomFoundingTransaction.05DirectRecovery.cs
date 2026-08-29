using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		private static void ClearDirectRecovery(Zone Site,
			string ExpectedTransaction = null)
		{
			if (Site == null) return;
			if (!string.IsNullOrEmpty(ExpectedTransaction) &&
				Site.GetZoneProperty(DirectRecoveryTransactionProperty, null) !=
					ExpectedTransaction) return;
			Site.RemoveZoneProperty(DirectRecoveryNameProperty);
			Site.RemoveZoneProperty(DirectRecoveryVocationProperty);
			Site.RemoveZoneProperty(DirectRecoveryRiteXProperty);
			Site.RemoveZoneProperty(DirectRecoveryRiteYProperty);
			Site.RemoveZoneProperty(DirectRecoveryTickProperty);
			Site.RemoveZoneProperty(DirectRecoveryRealmProperty);
			Site.RemoveZoneProperty(DirectRecoveryTransactionProperty);
		}

		private static bool HasDirectRecovery(Zone Site)
		{
			return HasSiteReservation(Site);
		}

		private static bool DirectRecoveryMatches(Zone Site, string Name, string Realm,
			string Transaction)
		{
			if (!TryReadSiteReservation(Site, out var authority, out var storedName,
				out var vocation, out var village, out var display, out var tick)) return false;
			return storedName == Name && authority.RealmFaction == Realm &&
				authority.TransactionID == Transaction;
		}

		private static bool StageDirectRecovery(Zone Site, string Name, string Vocation,
			int RiteX, int RiteY, string Realm, string Transaction)
		{
			if (Site == null || HasDirectRecovery(Site) || string.IsNullOrEmpty(Transaction))
				return false;
			string digest = DirectPayloadDigest(KingdomFoundingKind.SecondCity, Name,
				Vocation, null, null);
			KingdomFoundingAuthority authority = NewAuthority(
				KingdomFoundingKind.SecondCity, KingdomFoundingOwnerKind.Direct,
				Transaction, Guid.NewGuid().ToString("N"), Realm, Site.ZoneID,
				RiteX, RiteY, digest);
			string encoded = KingdomFoundingTransactionRules.FormatAuthority(authority);
			return encoded != null && StageSiteReservation(Site, encoded, Name,
				Vocation, null, null);
		}
	}
}
