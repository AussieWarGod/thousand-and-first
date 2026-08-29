using System.Collections.Generic;
using ThousandAndFirst.Api;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomExternalOwnershipBindingRuntime
	{
		private static readonly string[] ResetProperties = new string[]
		{
			StageAuthorityProperty, StageProperty, BindingAuthorityProperty, BindingProperty,
			ContestedProperty, ToldProperty
		};

		internal static bool TryPrepareRealmReset(Zone Site, ICollection<string> Realms,
			out KingdomExternalOwnershipResetPlan Plan, out string Failure)
		{
			Plan = null;
			if (!CanResetForRealms(Site, Realms, out Failure)) return false;
			Plan = new KingdomExternalOwnershipResetPlan { ZoneId = Site?.ZoneID ?? "" };
			if (Site == null) return true;
			for (int i = 0; i < ResetProperties.Length; i++)
			{
				string key = ResetProperties[i];
				if (!Site.HasZoneProperty(key)) continue;
				Plan.Values[key] = Site.GetZoneProperty(key, null);
			}
			return true;
		}

		internal static bool TryClearForRealmReset(Zone Site, ICollection<string> Realms,
			KingdomExternalOwnershipResetPlan Plan, out string Failure)
		{
			Failure = null;
			if (!ResetSnapshotExact(Site, Plan)
				|| !CanResetForRealms(Site, Realms, out Failure))
			{
				if (Failure == null) Failure = "External-owner reset evidence changed after preview.";
				return false;
			}
			return ClearPreparedRealmReset(Site, out Failure);
		}

		internal static bool CanResetForRealms(Zone Site, ICollection<string> Realms,
			out string Failure)
		{
			Failure = null;
			if (Site == null) return true;
			if (!PairOwnedByRealms(Site, StageAuthorityProperty, StageProperty,
					Realms, out Failure)
				|| !PairOwnedByRealms(Site, BindingAuthorityProperty, BindingProperty,
					Realms, out Failure)) return false;
			return true;
		}

		internal static bool TryClearForRealmReset(Zone Site, ICollection<string> Realms,
			out string Failure)
		{
			if (!CanResetForRealms(Site, Realms, out Failure)) return false;
			return ClearPreparedRealmReset(Site, out Failure);
		}

		private static bool ClearPreparedRealmReset(Zone Site, out string Failure)
		{
			Failure = null;
			if (Site == null) return true;
			Site.RemoveZoneProperty(StageAuthorityProperty);
			Site.RemoveZoneProperty(StageProperty);
			Site.RemoveZoneProperty(BindingAuthorityProperty);
			Site.RemoveZoneProperty(BindingProperty);
			Site.RemoveZoneProperty(ContestedProperty);
			Site.RemoveZoneProperty(ToldProperty);
			if (!HasAnyOwnershipProperty(Site)) return true;
			Failure = "The exact external-owner receipt was not cleared by reset.";
			return false;
		}

		private static bool ResetSnapshotExact(Zone Site,
			KingdomExternalOwnershipResetPlan Plan)
		{
			if (Plan == null || (Site?.ZoneID ?? "") != Plan.ZoneId) return false;
			if (Site == null) return Plan.Values.Count == 0;
			for (int i = 0; i < ResetProperties.Length; i++)
			{
				string key = ResetProperties[i];
				bool present = Site.HasZoneProperty(key);
				if (present != Plan.Values.ContainsKey(key)) return false;
				if (present && Site.GetZoneProperty(key, null) != Plan.Values[key]) return false;
			}
			return true;
		}

		private static bool PairOwnedByRealms(Zone Site, string AuthorityProperty,
			string ValueProperty, ICollection<string> Realms, out string Failure)
		{
			Failure = null;
			bool hasAuthority = Site.HasZoneProperty(AuthorityProperty);
			bool hasValue = Site.HasZoneProperty(ValueProperty);
			if (!hasAuthority && !hasValue) return true;
			string authority = Site.GetZoneProperty(AuthorityProperty, null);
			string encoded = Site.GetZoneProperty(ValueProperty, null);
			if (!hasAuthority || !hasValue || string.IsNullOrEmpty(authority)
				|| !KingdomExternalOwnershipRules.TryDecode(encoded, out var binding)
				|| (binding.Mode == KingdomExternalOwnershipMode.Bind
					&& binding.Observation.ZoneId != Site.ZoneID)
				|| !AuthorityOwnedByRealms(authority, Site.ZoneID, Realms))
			{
				Failure = "The current zone carries a partial, foreign, or malformed " +
					"external-owner receipt.";
				return false;
			}
			return true;
		}

		private static bool AuthorityOwnedByRealms(string Authority, string ZoneId,
			ICollection<string> Realms)
		{
			if (Realms == null || Realms.Count == 0) return false;
			foreach (string realm in Realms)
				if (Authority == ClaimAuthority(realm, ZoneId)) return true;
			return KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed)
				&& parsed.ZoneID == ZoneId && Realms.Contains(parsed.RealmFaction);
		}

		private static bool HasAnyOwnershipProperty(Zone Site)
		{
			return Site.HasZoneProperty(StageAuthorityProperty)
				|| Site.HasZoneProperty(StageProperty)
				|| Site.HasZoneProperty(BindingAuthorityProperty)
				|| Site.HasZoneProperty(BindingProperty)
				|| Site.HasZoneProperty(ContestedProperty)
				|| Site.HasZoneProperty(ToldProperty);
		}
	}
}
