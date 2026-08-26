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
		private static bool ObjectTreeHasPaidReceipt(GameObject Root)
		{
			if (Root == null)
			{
				return false;
			}
			r_FounderBasin basin = Root.GetPart<r_FounderBasin>();
			if (basin != null && basin.HasPendingRite)
			{
				return true;
			}
			foreach (GameObject item in Root.GetContents(new List<GameObject>()))
			{
				basin = item.GetPart<r_FounderBasin>();
				if (basin != null && basin.HasPendingRite)
				{
					return true;
				}
			}
			return false;
		}

		private static bool AcceptDirectResetAuthority(string Raw,
			HashSet<string> Realms, Zone Site, ref string Expected, out string Failure)
		{
			Failure = "";
			if (string.IsNullOrEmpty(Raw) || Site == null ||
				!KingdomFoundingTransactionRules.TryParseAuthority(Raw, out var parsed) ||
				parsed.OwnerKind != KingdomFoundingOwnerKind.Direct ||
				(parsed.Kind != KingdomFoundingKind.FirstCity &&
				 parsed.Kind != KingdomFoundingKind.SecondCity) ||
				parsed.ZoneID != Site.ZoneID || !Realms.Contains(parsed.RealmFaction))
			{
				Failure = "A paid, foreign, or malformed founding authority is still reserved.";
				return false;
			}
			if (!string.IsNullOrEmpty(Expected) && Expected != Raw)
			{
				Failure = "More than one direct founding authority is reserved.";
				return false;
			}
			Expected = Raw;
			return true;
		}

		private static string ParseTransaction(string Authority)
		{
			return KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed)
				? parsed.TransactionID : null;
		}

		private static bool LegacyDirectResetMarkersAreExact(Zone Site,
			HashSet<string> Realms)
		{
			return Site != null &&
				!string.IsNullOrEmpty(Site.GetZoneProperty(
					DirectRecoveryNameProperty, null)) &&
				KingdomSettlement.IsKnownVocation(Site.GetZoneProperty(
					DirectRecoveryVocationProperty, null)) &&
				int.TryParse(Site.GetZoneProperty(DirectRecoveryRiteXProperty, null),
					out var riteX) && riteX >= 0 && riteX < Site.Width &&
				int.TryParse(Site.GetZoneProperty(DirectRecoveryRiteYProperty, null),
					out var riteY) && riteY >= 0 && riteY < Site.Height &&
				long.TryParse(Site.GetZoneProperty(DirectRecoveryTickProperty, null),
					out var tick) && tick >= 0L &&
				Realms.Contains(Site.GetZoneProperty(DirectRecoveryRealmProperty, null)) &&
				!string.IsNullOrEmpty(Site.GetZoneProperty(
					DirectRecoveryTransactionProperty, null));
		}

	}
}
