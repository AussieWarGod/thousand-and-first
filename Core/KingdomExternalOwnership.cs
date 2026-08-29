using System;
using System.Collections.Generic;
using ThousandAndFirst.Api;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	[HasModSensitiveStaticCache]
	public static class KingdomExternalOwnership
	{
		private sealed class ProviderRow
		{
			public string Id;
			public string Version;
			public IKingdomExternalOwnershipProvider Provider;
		}

		[ModSensitiveStaticCache]
		private static List<ProviderRow> Providers;

		[ModSensitiveStaticCache]
		private static List<string> RegistrationFaults;

		public static KingdomExternalOwnershipReading Inspect(Zone ActiveZone,
			string RequiredProviderId = null)
		{
			if (ActiveZone == null || The.ZoneManager?.ActiveZone != ActiveZone)
				return Failed("External ownership can be read only from the exact active zone.");
			List<ProviderRow> providers = Registry();
			if (RegistrationFaults.Count > 0)
				return Failed(RegistrationFaults[0]);
			if (!string.IsNullOrEmpty(RequiredProviderId))
			{
				int requiredCount = 0;
				for (int i = 0; i < providers.Count; i++)
					if (providers[i].Id == RequiredProviderId) requiredCount++;
				if (requiredCount != 1)
					return Failed("Required external ownership provider is unavailable: "
						+ RequiredProviderId + ".");
			}
			KingdomExternalOwnershipObservation observed = null;
			for (int i = 0; i < providers.Count; i++)
			{
				ProviderRow row = providers[i];
				KingdomExternalOwnershipObservation candidate = null;
				string failure = null;
				bool owned = false;
				try
				{
					owned = row.Provider.TryObserve(ActiveZone, out candidate, out failure);
				}
				catch (Exception ex)
				{
					failure = ex.Message;
				}
				if (!string.IsNullOrEmpty(failure))
					return Failed(row.Id + " ownership observation failed: " + failure);
				if (!owned)
				{
					if (candidate != null)
						return Failed(row.Id + " returned ownership evidence with a false result.");
					continue;
				}
				if (!KingdomExternalOwnershipRules.ValidObservation(candidate)
					|| candidate.ProviderId != row.Id
					|| candidate.ProviderVersion != row.Version
					|| candidate.ZoneId != ActiveZone.ZoneID)
					return Failed(row.Id + " returned malformed or mismatched ownership evidence.");
				if (observed != null)
					return new KingdomExternalOwnershipReading
					{
						State = KingdomExternalOwnershipState.Conflicting,
						Failure = "More than one external owner answers for this ground."
					};
				observed = candidate.Clone();
			}
			return new KingdomExternalOwnershipReading
			{
				State = observed == null ? KingdomExternalOwnershipState.Unowned
					: KingdomExternalOwnershipState.Owned,
				Observation = observed
			};
		}

		private static KingdomExternalOwnershipReading Failed(string Failure)
		{
			return new KingdomExternalOwnershipReading
			{
				State = KingdomExternalOwnershipState.ProviderFailed,
				Failure = string.IsNullOrEmpty(Failure)
					? "External ownership observation failed." : Failure
			};
		}

		private static List<ProviderRow> Registry()
		{
			if (Providers != null) return Providers;
			Providers = new List<ProviderRow>();
			RegistrationFaults = new List<string>();
			List<Type> types = ModManager.GetTypesWithAttribute(
				typeof(KingdomExternalOwnershipProviderAttribute));
			for (int i = 0; i < types.Count; i++) Collect(types[i]);
			Providers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
			for (int i = 1; i < Providers.Count; i++)
				if (Providers[i - 1].Id == Providers[i].Id)
					RegistrationFaults.Add("Duplicate external ownership provider: "
						+ Providers[i].Id + ".");
			return Providers;
		}

		private static void Collect(Type Type)
		{
			if (Type == null || !typeof(IKingdomExternalOwnershipProvider).IsAssignableFrom(Type))
			{
				RegistrationFaults.Add("Marked external ownership type has no provider contract.");
				return;
			}
			try
			{
				IKingdomExternalOwnershipProvider provider =
					Activator.CreateInstance(Type) as IKingdomExternalOwnershipProvider;
				string id = provider?.ProviderId;
				string version = provider?.ProviderVersion;
				if (!KingdomExternalOwnershipRules.ValidToken(id, 64)
					|| !KingdomExternalOwnershipRules.ValidToken(version, 32))
					throw new InvalidOperationException("provider identity is malformed");
				Providers.Add(new ProviderRow { Id = id, Version = version, Provider = provider });
			}
			catch (Exception ex)
			{
				RegistrationFaults.Add((Type.FullName ?? Type.Name)
					+ " could not register: " + ex.Message);
			}
		}
	}
}
