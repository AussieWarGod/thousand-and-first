using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomCarryBook
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int FormatVersion = KingdomLifecycleRules.CurrentCarryFormatVersion;
		public bool LegacyIdentity;
		public string LegacyMigrationKey;
		public bool Quarantined;
		public string Fault;
		public string RealmId;
		public List<string> SettlementIds = new List<string>();
		public bool IdentityBound;
		public string IdentityProof;
		public long NextSequence = 1L;
		public long RetiredThrough;
		public KingdomCarryOperation Open;
		public List<KingdomLifecycleResourceRevision> Resources =
			new List<KingdomLifecycleResourceRevision>();
		public List<KingdomLifecycleProof> RecentProofs = new List<KingdomLifecycleProof>();
		public int OpaqueWireVersion;
		public byte[] OpaquePayload;

		[NonSerialized]
		public bool WireRejected;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			KingdomLifecycleWireCodec.WriteCarry(Writer, this);
		}

		public void Read(SerializationReader Reader)
		{
			try
			{
				KingdomLifecycleWireCodec.ReadCarry(Reader, this);
			}
			catch (Exception)
			{
				WireRejected = true;
				Quarantined = true;
				Open = null;
				SettlementIds = new List<string>();
				Resources = new List<KingdomLifecycleResourceRevision>();
				RecentProofs = new List<KingdomLifecycleProof>();
				throw;
			}
		}
#endif
	}
}
