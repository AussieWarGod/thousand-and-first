using System;

namespace ThousandAndFirst.Api
{
	public enum KingdomExternalOwnershipMode : byte
	{
		None = 0,
		Bind = 1
	}

	public enum KingdomExternalOwnershipState : byte
	{
		Unowned = 0,
		Owned = 1,
		ProviderFailed = 2,
		Conflicting = 3
	}

	public enum KingdomExternalBindingVerdict : byte
	{
		Open = 0,
		Exact = 1,
		ProviderUnavailable = 2,
		Diverged = 3,
		Malformed = 4
	}

	[Serializable]
	public sealed class KingdomExternalOwnershipObservation
	{
		public string ProviderId;
		public string ProviderVersion;
		public string OwnerGuid;
		public string SectorGuid;
		public string Evidence;
		public string ZoneId;
		public string ParasangId;

		public KingdomExternalOwnershipObservation Clone()
		{
			return (KingdomExternalOwnershipObservation)MemberwiseClone();
		}
	}

	[Serializable]
	public sealed class KingdomExternalOwnershipBinding
	{
		public KingdomExternalOwnershipMode Mode;
		public KingdomExternalOwnershipObservation Observation;

		public KingdomExternalOwnershipBinding Clone()
		{
			return new KingdomExternalOwnershipBinding
			{
				Mode = Mode,
				Observation = Observation?.Clone()
			};
		}
	}

	public sealed class KingdomExternalOwnershipReading
	{
		public KingdomExternalOwnershipState State;
		public KingdomExternalOwnershipObservation Observation;
		public string Failure;
	}
}
