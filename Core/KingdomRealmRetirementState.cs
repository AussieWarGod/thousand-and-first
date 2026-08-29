using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public sealed class KingdomRemovalLocator
	{
		public string ZoneId;
		public string SettlementId;
		public KingdomRemovalLocatorState State;
		public int Revision;
		public long CleanedTick;
		public int ObjectCount;
		public string EvidenceDigest;

		public KingdomRemovalLocator Clone()
		{
			return (KingdomRemovalLocator)MemberwiseClone();
		}
	}

	public sealed class KingdomRemovalRecord
	{
		public KingdomRemovalProjectionKind Kind;
		public string Id;
		public KingdomRemovalDisposition Disposition;
		public string BeforeDigest;
		public string AfterDigest;
		public long Amount;
		public string Detail;

		public KingdomRemovalRecord Clone()
		{
			return (KingdomRemovalRecord)MemberwiseClone();
		}
	}

	/// <summary>Bounded, explicit current-realm retirement authority.</summary>
	public sealed class KingdomRealmRetirementState
	{
		public const int CurrentVersion = 1;
		public const int MaxLocators = 32;
		public const int MaxRecords = 512;

		public int Version = CurrentVersion;
		public KingdomRealmRetirementPhase Phase;
		public int Revision;
		public string ReceiptId;
		public string RealmId;
		public string FactionId;
		public string GameId;
		public long RealmIncarnation;
		public long StartedTick;
		public long UpdatedTick;
		public string AuthorityDigest;
		public string Fault;
		public List<KingdomRemovalLocator> Locators = new List<KingdomRemovalLocator>();
		public List<KingdomRemovalRecord> Records = new List<KingdomRemovalRecord>();

		public KingdomRealmRetirementState Clone()
		{
			KingdomRealmRetirementState copy = (KingdomRealmRetirementState)MemberwiseClone();
			copy.Locators = new List<KingdomRemovalLocator>();
			copy.Records = new List<KingdomRemovalRecord>();
			for (int i = 0; i < (Locators?.Count ?? 0); i++)
				copy.Locators.Add(Locators[i]?.Clone());
			for (int i = 0; i < (Records?.Count ?? 0); i++)
				copy.Records.Add(Records[i]?.Clone());
			return copy;
		}
	}

	/// <summary>Base-game-only safety metadata that survives absence of this assembly.</summary>
	public sealed class KingdomIdentityFence
	{
		public const int CurrentVersion = 2;
		public int Version = CurrentVersion;
		public int Revision;
		public string GameId;
		public long NextRealmIncarnation;
		public string LastRealmId;
		public string LastRealmDigest;
		public string TombstoneChainDigest;
		/// <summary>Exact digest of the operational wire replaced by removal CAS.</summary>
		public string PreparedFromDigest;
		/// <summary>Exact digest of the ReadyForFence receipt that authorized this terminal CAS.</summary>
		public string PreparedReceiptDigest;
		public KingdomIdentityFenceDisposition Disposition;
		public string PendingTransactionId;
		public long PendingIncarnation;

		public KingdomIdentityFence Clone()
		{
			return (KingdomIdentityFence)MemberwiseClone();
		}
	}
}
