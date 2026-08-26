using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{

	/// <summary>Per-settlement authority. Every lane has its own monotone replay barrier.</summary>
	[Serializable]
	public sealed class KingdomLifecycleBook
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int FormatVersion = KingdomLifecycleRules.CurrentFormatVersion;
		public bool LegacyIdentity;
		public string LegacyMigrationKey;
		public bool Quarantined;
		public string Fault;
		public string SettlementId;
		public bool IdentityBound;
		public string IdentityProof;
		public long PlainGuestNextSequence = 1L;
		public long PlainGuestRetiredThrough;
		public long NotableGuestNextSequence = 1L;
		public long NotableGuestRetiredThrough;
		public long RaidNextSequence = 1L;
		public long RaidRetiredThrough;
		public long PetitionNextSequence = 1L;
		public long PetitionRetiredThrough;
		public KingdomLifecycleOptionState LocusOption;
		public long LocusOptionTick;
		public KingdomLifecycleOptionState NotableOption;
		public long NotableOptionTick;
		public KingdomLifecycleOptionState RaidOption;
		public long RaidOptionTick;
		public KingdomLifecycleOptionState PetitionOption;
		public long PetitionOptionTick;
		public KingdomLifecycleOperation PlainGuest;
		public KingdomLifecycleOperation NotableGuest;
		public KingdomLifecycleOperation Raid;
		public KingdomLifecycleOperation Petition;
		public List<KingdomLifecycleResourceRevision> Resources =
			new List<KingdomLifecycleResourceRevision>();
		public List<KingdomLifecycleProof> RecentProofs = new List<KingdomLifecycleProof>();
		public KingdomRaidLedger RaidLedger = new KingdomRaidLedger();
		public KingdomGrowthBook Growth = new KingdomGrowthBook();

		[NonSerialized]
		public bool WireRejected;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			KingdomLifecycleWireCodec.WriteLifecycle(Writer, this);
		}

		public void Read(SerializationReader Reader)
		{
			try
			{
				KingdomLifecycleWireCodec.ReadLifecycle(Reader, this);
			}
			catch (Exception)
			{
				WireRejected = true;
				Quarantined = true;
				PlainGuest = null;
				NotableGuest = null;
				Raid = null;
				Petition = null;
				Resources = new List<KingdomLifecycleResourceRevision>();
				RecentProofs = new List<KingdomLifecycleProof>();
				RaidLedger = new KingdomRaidLedger();
				throw;
			}
		}
#endif
	}
}
