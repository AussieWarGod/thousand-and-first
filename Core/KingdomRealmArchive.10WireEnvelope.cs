using System;
using System.Collections.Generic;
using System.IO;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			if (AwayOpaque == null) Away = SettlementTopology?.Get(0);
			string failure;
			if (!ValidateEnvelope(out failure)) throw new InvalidDataException(failure);
			Writer.Write(Magic); Writer.Write(Version); Writer.Write((byte)Phase);
			Writer.Write(Quarantined ? (byte)1 : (byte)0); WriteString(Writer, Fault, 4096);
			WriteString(Writer, RealmId, 256);
			WriteString(Writer, FactionName, 512); WriteString(Writer, DisplayName, 512);
			WriteString(Writer, ExileDeed, 4096); Writer.Write(ClosedTick);
			WriteStrings(Writer, SettlementIds, KingdomIdentityRules.MaxSettlements, 256);
			Writer.Write(RealmIdentityVersion);
			Writer.Write((byte)RealmIdentityOrigin);
			WriteString(Writer, RealmIdentityTransactionId, 64);
			WriteString(Writer, RealmIdentityLegacyFaction, 512);
			Writer.Write(RealmIdentityFoundedTick); Writer.Write(RealmIdentitySeedHigh);
			Writer.Write(RealmIdentitySeedLow);
			WriteString(Writer, RealmIdentityFirstClaimedZone, 512);
			Writer.Write(SimulationSeedHigh); Writer.Write(SimulationSeedLow);
			WriteArchivedSettlement(Writer, Seat, SeatOpaque);
			WriteArchivedSettlement(Writer, Away, AwayOpaque);
			WriteStandings(Writer, Standings);
			WriteBindings(Writer, Bindings); Writer.Write(ResidentCounter);
			WriteJobs(Writer, Jobs); Writer.Write(LastSliceTick); Writer.Write(ReifyTick);
			Writer.Write(ReifyThirdsSpent); Writer.Write(ReifyHeavySpent);
			Writer.Write(ReifyQuietUntilTick); Writer.Write(DedicationCounter);
			WriteStrings(Writer, ChronicleEntries, KingdomChronicle.MaxEntries,
				KingdomChronicleReceiptRules.MaxEntryChars);
			WriteStrings(Writer, OutsiderEntries, KingdomChronicle.MaxEntries,
				KingdomChronicleReceiptRules.MaxEntryChars);
			WriteString(Writer, ChronicleRegistry, KingdomChronicleReceiptRules.MaxRegistryChars);
			WriteString(Writer, ChronicleRegistryFault, 160);
			Writer.Write(RegardSpoken); Writer.Write(Dissent); Writer.Write(DissentSpoken);
			Writer.Write(LastDissentTick); WriteString(Writer, DeclaredCreed, 4096);
			WriteString(Writer, DishName, 4096); WriteString(Writer, DishText, 4096);
			WriteString(Writer, DishStaple, 4096); WriteString(Writer, DishSource, 4096);
			Writer.Write(LastRiteTick); Writer.Write(LastSoulRiteTick);
			WriteArchivedSettlement(Writer, Seceded, SecededOpaque); Writer.Write(SecededTick);
			WriteHaul(Writer, Haul); Writer.Write((IComposite)CarryBook);
			Writer.Write(ReturnRegard);
			WriteCallback(Writer, ExileChronicle); WriteCallback(Writer, ExileAbility);
			WriteCallback(Writer, ReturnChronicle); WriteCallback(Writer, ReturnReputation);
			WriteCallback(Writer, ReturnFeelings); WriteCallback(Writer, ReturnSeat);
			WriteCallback(Writer, ReturnAbility);
			Writer.Write((IComposite)SettlementTopology);
			WriteStandings(Writer, RealmPolicyToward);
			WriteStandings(Writer, RegardSpilloverRemainders);
			WriteStandings(Writer, RegardSpilloverObservedReputation);
			Writer.Write(DirectionalStandingSchemaVersion);
			Writer.Write(CallbackAuthoritySchemaVersion);
			WriteString(Writer, DirectionalStandingDigest, 64);
		}

		public void Read(SerializationReader Reader)
		{
			try
			{
				ReadCore(Reader);
			}
			catch (Exception ex)
			{
				// Qud retains the same composite instance after a failed reader callback. Never
				// leave a partially decoded authority graph in that instance: replace every field
				// with one bounded, writable, non-authoritative v2 poison envelope before rethrow.
				ResetToPoisonEnvelope(ex.Message);
				throw;
			}
		}

		private void ReadCore(SerializationReader Reader)
		{
			if (Reader.ReadInt32() != Magic) throw new InvalidDataException("Invalid realm archive marker.");
			Version = Reader.ReadInt32();
			if (Version == 1) throw new InvalidDataException(
				"Pre-release realm archive v1 used unsafe nested reflected settlement wire.");
			if (Version != LegacyJobVersion && Version != MissionJobVersion &&
				Version != ExactDeliveryJobVersion &&
				Version != ExpandedDeliveryJobVersion &&
				Version != SettlementTopologyVersion && Version != CurrentVersion)
				throw new InvalidDataException("Unknown realm archive version.");
			int wireVersion = Version;
			Phase = (KingdomRealmArchivePhase)Reader.ReadByte();
			byte quarantineFlag = Reader.ReadByte();
			if (quarantineFlag > 1) throw new InvalidDataException(
				"Realm archive quarantine flag is noncanonical.");
			Quarantined = quarantineFlag == 1;
			Fault = ReadString(Reader, 4096); RealmId = ReadString(Reader, 256);
			FactionName = ReadString(Reader, 512); DisplayName = ReadString(Reader, 512);
			ExileDeed = ReadString(Reader, 4096); ClosedTick = Reader.ReadInt64();
			SettlementIds = ReadStrings(Reader, KingdomIdentityRules.MaxSettlements, 256);
			RealmIdentityVersion = Reader.ReadInt32();
			RealmIdentityOrigin = (KingdomIdentityOrigin)Reader.ReadByte();
			RealmIdentityTransactionId = ReadString(Reader, 64);
			RealmIdentityLegacyFaction = ReadString(Reader, 512);
			RealmIdentityFoundedTick = Reader.ReadInt64();
			RealmIdentitySeedHigh = Reader.ReadUInt64(); RealmIdentitySeedLow = Reader.ReadUInt64();
			RealmIdentityFirstClaimedZone = ReadString(Reader, 512);
			SimulationSeedHigh = Reader.ReadUInt64(); SimulationSeedLow = Reader.ReadUInt64();
			Seat = ReadArchivedSettlement(Reader, out SeatOpaque, out SeatWireVersion);
			Away = ReadArchivedSettlement(Reader, out AwayOpaque, out AwayWireVersion);
			Standings = ReadStandings(Reader);
			Bindings = ReadBindings(Reader); ResidentCounter = Reader.ReadInt32();
			Jobs = ReadJobs(Reader, wireVersion); LastSliceTick = Reader.ReadInt64();
			ReifyTick = Reader.ReadInt64(); ReifyThirdsSpent = Reader.ReadInt32();
			ReifyHeavySpent = Reader.ReadInt32(); ReifyQuietUntilTick = Reader.ReadInt64();
			DedicationCounter = Reader.ReadInt32();
			ChronicleEntries = ReadStrings(Reader, KingdomChronicle.MaxEntries,
				KingdomChronicleReceiptRules.MaxEntryChars);
			OutsiderEntries = ReadStrings(Reader, KingdomChronicle.MaxEntries,
				KingdomChronicleReceiptRules.MaxEntryChars);
			ChronicleRegistry = ReadString(Reader, KingdomChronicleReceiptRules.MaxRegistryChars);
			ChronicleRegistryFault = ReadString(Reader, 160);
			RegardSpoken = Reader.ReadInt32(); Dissent = Reader.ReadInt32();
			DissentSpoken = Reader.ReadInt32(); LastDissentTick = Reader.ReadInt64();
			DeclaredCreed = ReadString(Reader, 4096); DishName = ReadString(Reader, 4096);
			DishText = ReadString(Reader, 4096); DishStaple = ReadString(Reader, 4096);
			DishSource = ReadString(Reader, 4096); LastRiteTick = Reader.ReadInt64();
			LastSoulRiteTick = Reader.ReadInt64();
			Seceded = ReadArchivedSettlement(Reader, out SecededOpaque,
				out SecededWireVersion); SecededTick = Reader.ReadInt64();
			Haul = ReadHaul(Reader);
			CarryBook = Reader.ReadComposite<KingdomCarryBook>();
			if (CarryBook == null || CarryBook.WireRejected)
				throw new InvalidDataException("Archived carry payload was rejected.");
			ReturnRegard = Reader.ReadInt32();
			ExileChronicle = ReadCallback(Reader); ExileAbility = ReadCallback(Reader);
			ReturnChronicle = ReadCallback(Reader); ReturnReputation = ReadCallback(Reader);
			ReturnFeelings = ReadCallback(Reader); ReturnSeat = ReadCallback(Reader);
			ReturnAbility = ReadCallback(Reader);
			if (wireVersion >= SettlementTopologyVersion)
			{
				KingdomSettlement legacyProjection = Away;
				SettlementTopology = Reader.ReadComposite<KingdomSettlementTopology>();
				if (SettlementTopology == null)
					throw new InvalidDataException("Archived settlement topology is absent.");
				if (SettlementTopology.HasOpaqueEvidence)
				{
					Away = null;
				}
				else
				{
					KingdomSettlement canonical = SettlementTopology.Get(0);
					if (!KingdomArchivedSettlementCodec.ExactGraph(legacyProjection, canonical,
						out string projectionFailure))
						Quarantine("legacy archive projection differs from topology: " +
							projectionFailure);
					Away = canonical;
				}
			}
			else
			{
				SettlementTopology = new KingdomSettlementTopology();
				if (Away != null && AwayOpaque == null &&
					!SettlementTopology.TryAdoptLegacy(Away, out string migrationFailure))
					throw new InvalidDataException(migrationFailure);
			}
			if (wireVersion >= DirectionalStandingVersion)
			{
				RealmPolicyToward = ReadStandings(Reader);
				RegardSpilloverRemainders = ReadStandings(Reader);
				RegardSpilloverObservedReputation = ReadStandings(Reader);
				DirectionalStandingSchemaVersion = Reader.ReadInt32();
				CallbackAuthoritySchemaVersion = Reader.ReadInt32();
				DirectionalStandingDigest = ReadString(Reader, 64);
			}
			else
			{
				RealmPolicyToward = new Dictionary<string, int>(StringComparer.Ordinal);
				RegardSpilloverRemainders =
					new Dictionary<string, int>(StringComparer.Ordinal);
				RegardSpilloverObservedReputation =
					new Dictionary<string, int>(StringComparer.Ordinal);
				DirectionalStandingSchemaVersion = 0;
				CallbackAuthoritySchemaVersion = 1;
				DirectionalStandingDigest = null;
				RequiresDirectionalStandingMigration = true;
			}
			// v2 predates mission and delivery columns; v3 predates delivery columns. ReadJobs
			// pads only absent envelopes; v4 delivery columns remain the exact v5 layout.
			Version = CurrentVersion;
			string failure;
			if (!ValidateEnvelope(out failure)) throw new InvalidDataException(failure);
			if (SeatOpaque != null || AwayOpaque != null || SecededOpaque != null)
				Quarantine("archive contains a future opaque settlement payload");
			else if (!Quarantined && !RequiresDirectionalStandingMigration &&
				!Validate(out failure)) Quarantine(failure);
		}

		internal void ResetToPoisonEnvelope(string Failure)
		{
			Version = CurrentVersion;
			Phase = KingdomRealmArchivePhase.Quarantined;
			Quarantined = true;
			Fault = Bound(Failure ?? "realm archive reader rejected partial payload", 4096);
			RealmId = null; FactionName = null; DisplayName = null; ExileDeed = null;
			ClosedTick = 0L; SettlementIds = new List<string>();
			RealmIdentityVersion = 0; RealmIdentityOrigin = KingdomIdentityOrigin.None;
			RealmIdentityTransactionId = null; RealmIdentityLegacyFaction = null;
			RealmIdentityFoundedTick = 0L; RealmIdentitySeedHigh = 0UL;
			RealmIdentitySeedLow = 0UL; RealmIdentityFirstClaimedZone = null;
			SimulationSeedHigh = 0UL; SimulationSeedLow = 0UL;
			Seat = null; Away = null; Seceded = null;
			SettlementTopology = new KingdomSettlementTopology();
			SeatOpaque = null; AwayOpaque = null; SecededOpaque = null;
			SeatWireVersion = 0; AwayWireVersion = 0; SecededWireVersion = 0;
			Standings = new Dictionary<string, int>(StringComparer.Ordinal);
			RealmPolicyToward = new Dictionary<string, int>(StringComparer.Ordinal);
			RegardSpilloverRemainders = new Dictionary<string, int>(StringComparer.Ordinal);
			RegardSpilloverObservedReputation =
				new Dictionary<string, int>(StringComparer.Ordinal);
			DirectionalStandingSchemaVersion = 0;
			CallbackAuthoritySchemaVersion = 2;
			DirectionalStandingDigest = null;
			RequiresDirectionalStandingMigration = false;
			Bindings = new Simulation.City.KingdomBindingRegistry(); ResidentCounter = 0;
			Jobs = new Simulation.City.KingdomJobRegistry(); LastSliceTick = 0L;
			ReifyTick = 0L; ReifyThirdsSpent = 0; ReifyHeavySpent = 0;
			ReifyQuietUntilTick = 0L; DedicationCounter = 0;
			ChronicleEntries = new List<string>(); OutsiderEntries = new List<string>();
			ChronicleRegistry = KingdomChronicleReceiptRules.Header;
			ChronicleRegistryFault = null;
			RegardSpoken = 0; Dissent = 0; DissentSpoken = 0; LastDissentTick = 0L;
			DeclaredCreed = null; DishName = null; DishText = null; DishStaple = null;
			DishSource = null; LastRiteTick = 0L; LastSoulRiteTick = 0L;
			SecededTick = 0L; Haul = null; CarryBook = new KingdomCarryBook();
			ReturnRegard = int.MinValue;
			ExileChronicle = new KingdomRealmCallbackReceipt();
			ExileAbility = new KingdomRealmCallbackReceipt();
			ReturnChronicle = new KingdomRealmCallbackReceipt();
			ReturnReputation = new KingdomRealmCallbackReceipt();
			ReturnFeelings = new KingdomRealmCallbackReceipt();
			ReturnSeat = new KingdomRealmCallbackReceipt();
			ReturnAbility = new KingdomRealmCallbackReceipt();
		}

#endif
	}
}
