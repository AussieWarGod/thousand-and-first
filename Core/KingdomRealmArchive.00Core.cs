using System;
using System.Collections.Generic;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// The realm-scoped half of one exiled realm. Cities and standings remain in the product's
	/// existing ExiledSeat/ExiledAway/ExiledStandings slots, but those are independent mirrors:
	/// the authoritative archive owns deep settlement and standings copies. Its manual reader
	/// bounds every archive-owned row, string, and nested settlement payload before allocation.
	/// Version 8 is the first System writer; archive v1's unsafe reflected settlement wire was a
	/// pre-release format and is deliberately refused rather than partly interpreted.
	/// </summary>
	[Serializable]
	public sealed partial class KingdomRealmArchive
#if !TAF_TESTS
		: IComposite
#endif
	{
		private const int Magic = 0x54415231; // TAR1
		/// <summary>v4 appends explicit exact-delivery columns. v2 lacks mission and delivery
		/// payloads; v3 carries missions but not delivery payloads. Both readers remain explicit and
		/// pad only their absent envelopes with neutral values.</summary>
		public const int CurrentVersion = 4;
		internal const int LegacyJobVersion = 2;
		internal const int MissionJobVersion = 3;
		private const int MaxTextBytes = 8192;
		private const int MaxBindings = 136;
		private const int MaxJobs = 16;
		private const int MaxLegs = 96;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public int Version = CurrentVersion;
		public KingdomRealmArchivePhase Phase = KingdomRealmArchivePhase.Prepared;
		public bool Quarantined;
		public string Fault;

		public string RealmId;
		public string FactionName;
		public string DisplayName;
		public string ExileDeed;
		public long ClosedTick;
		/// <summary>Frozen complete topology. Exiled city objects remain mutable engine graphs;
		/// this independent bounded list is what catches replacement during return callbacks.</summary>
		public List<string> SettlementIds;
		public int RealmIdentityVersion;
		public KingdomIdentityOrigin RealmIdentityOrigin;
		public string RealmIdentityTransactionId;
		public string RealmIdentityLegacyFaction;
		public long RealmIdentityFoundedTick;
		public ulong RealmIdentitySeedHigh;
		public ulong RealmIdentitySeedLow;
		public string RealmIdentityFirstClaimedZone;

		public ulong SimulationSeedHigh;
		public ulong SimulationSeedLow;
		public KingdomSettlement Seat;
		public KingdomSettlement Away;
		public Dictionary<string, int> Standings;
		/// <summary>Opaque strictly-future nested settlement bytes. Any non-null value quarantines
		/// the archive but remains byte-for-byte writable for inspection by a newer build.</summary>
		public byte[] SeatOpaque;
		public byte[] AwayOpaque;
		public byte[] SecededOpaque;
		public int SeatWireVersion;
		public int AwayWireVersion;
		public int SecededWireVersion;
		public Simulation.City.KingdomBindingRegistry Bindings;
		public int ResidentCounter;
		public Simulation.City.KingdomJobRegistry Jobs;
		public long LastSliceTick;
		public long ReifyTick;
		public int ReifyThirdsSpent;
		public int ReifyHeavySpent;
		public long ReifyQuietUntilTick;
		public int DedicationCounter;

		public List<string> ChronicleEntries;
		public List<string> OutsiderEntries;
		public string ChronicleRegistry;
		public string ChronicleRegistryFault;

		public int RegardSpoken;
		public int Dissent;
		public int DissentSpoken;
		public long LastDissentTick;
		public string DeclaredCreed;
		public string DishName;
		public string DishText;
		public string DishStaple;
		public string DishSource;
		public long LastRiteTick;
		public long LastSoulRiteTick;
		public KingdomSettlement Seceded;
		public long SecededTick;
		public KingdomCarryHaul Haul;
		public KingdomCarryBook CarryBook;
		public int ReturnRegard = int.MinValue;

		public KingdomRealmCallbackReceipt ExileChronicle = new KingdomRealmCallbackReceipt();
		public KingdomRealmCallbackReceipt ExileAbility = new KingdomRealmCallbackReceipt();
		public KingdomRealmCallbackReceipt ReturnChronicle = new KingdomRealmCallbackReceipt();
		public KingdomRealmCallbackReceipt ReturnReputation = new KingdomRealmCallbackReceipt();
		public KingdomRealmCallbackReceipt ReturnFeelings = new KingdomRealmCallbackReceipt();
		public KingdomRealmCallbackReceipt ReturnSeat = new KingdomRealmCallbackReceipt();
		public KingdomRealmCallbackReceipt ReturnAbility = new KingdomRealmCallbackReceipt();

	}
}
