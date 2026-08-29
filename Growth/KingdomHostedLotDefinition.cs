using System;

namespace ThousandAndFirst
{
	/// <summary>One bounded lot type a persistent host may expose.</summary>
	[Serializable]
	public sealed class KingdomHostedLotDefinition
	{
		public string Key;
		public string DisplayName;
		public string InteriorCell;
		public string MaterialKey;
		public long BuildTicks;
		public int Crew;
		public string Supports;
		public bool RequiresWater;
		/// <summary>Exact stable fixture blueprint and count backing hosted production.
		/// The fixture blueprint carries the physical rate tag; the host receipt carries support.</summary>
		public string PhysicalProducerBlueprint;
		public int PhysicalProducerCount;
		public bool ReadOnly;
		/// <summary>Read-only view key. The Great Archive uses this to visualize realm
		/// knowledge; it never implies a queue or research command.</summary>
		public string KnowledgeView;

		public KingdomHostedLotDefinition Copy()
		{
			return (KingdomHostedLotDefinition)MemberwiseClone();
		}
	}

	public enum KingdomHostedLotPhase : byte
	{
		Dormant = 0,
		Working = 1,
		Active = 2,
		Quarantined = 3
	}

	public enum KingdomHostedAuthorityPhase : byte
	{
		Reserved = 1,
		Active = 2,
		Quarantined = 3
	}

	public enum KingdomHostedAuthorityAction : byte
	{
		Reserve = 1,
		Confirm = 2,
		Reject = 3,
		Quarantine = 4
	}
}
