using System;
using XRL.World;

namespace ThousandAndFirst.Api
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class KingdomExternalOwnershipProviderAttribute : Attribute
	{
	}

	public interface IKingdomExternalOwnershipProvider
	{
		string ProviderId { get; }
		string ProviderVersion { get; }

		bool TryObserve(Zone ActiveZone,
			out KingdomExternalOwnershipObservation Observation, out string Failure);
	}
}
