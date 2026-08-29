using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomPolityDispatchState
#if !TAF_TESTS
		: IComposite
#endif
	{
		public const int CurrentVersion = 2;
		public int Version = CurrentVersion;
		public string RealmId;
		public long Revision;
		public bool HasWindow;
		public ulong LastWindowOrdinal;
		public long WindowCauseTick;
		public long FutureCauseFloorTick;
		public string EndpointDigest;
		public int EndpointCount;
		public int CompletedMask;
		/// <summary>Bounded, durable facts emitted when another lane already owns the shared
		/// audience/body budget. These are records, never deferred actor work.</summary>
		public List<KingdomPolityDirectRecord> DirectRecords =
			new List<KingdomPolityDirectRecord>();
		public string Fault;

#if !TAF_TESTS
		public bool WantFieldReflection => false;
		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomPolityDispatchState));
		}
		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomPolityDispatchState));
		}
#endif
	}

	[Serializable]
	public sealed class KingdomPolityDirectRecord
	{
		public string RecordId;
		public string SourceRef;
		public string SettlementId;
		public KingdomPolityCohortPurpose Purpose;
		public ulong WindowOrdinal;
		public long CauseTick;
		public string EndpointVerb;
		public long AcknowledgedTick;

		public KingdomPolityDirectRecord Copy()
		{
			return (KingdomPolityDirectRecord)MemberwiseClone();
		}
	}

	[Serializable]
	public sealed class KingdomPolityEndpointFacts
	{
		public string SettlementId;
		public bool IsSeat;
		public int Population;
		public int Stage;
		public int ShopTier;
		public int KnownStorageSpace;
		public string GuardCauseRef;
		public string PatrolCauseRef;
		public string CourierCauseRef;
		public string TraderCauseRef;
		public string MigrantCauseRef;
	}

	[Serializable]
	public sealed class KingdomPolityDueWork
	{
		public int EndpointOrdinal;
		/// <summary>Exact read-only offer authority used to authenticate this plan.</summary>
		public string EndpointDigest;
		public string CauseRef;
		public string DueFacts;
		public string FairnessTicket;
		public string CohortId;
		public string EventStreamId;
		public string SourceRef;
		public string SettlementId;
		public KingdomPolityCohortPurpose Purpose;
		public ulong WindowOrdinal;
		public long CauseTick;
		public long StayUntilTick;
		public int MemberCount;
		public string EndpointVerb;
	}

	[Serializable]
	public sealed class KingdomPolityDispatchOffer
	{
		public string RealmId;
		public long Tick;
		public List<KingdomPolityEndpointFacts> Endpoints =
			new List<KingdomPolityEndpointFacts>();
	}
}
