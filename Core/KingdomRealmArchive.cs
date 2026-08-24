using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public enum KingdomRealmArchivePhase : byte
	{
		None = 0,
		Prepared = 1,
		TradeClosed = 2,
		ChronicleFrozen = 3,
		ChronicleCleared = 4,
		Closed = 5,
		Restoring = 6,
		Restored = 7,
		Quarantined = 8,
		/// <summary>Durable intent published before clearing the live old-realm graph.</summary>
		Resetting = 9,
		/// <summary>Durable intent published before retiring the exile mirrors after return.</summary>
		ReturnCleaning = 10,
		/// <summary>All exile mirrors exactly published; later callbacks may no longer repair
		/// a canonical-looking missing mirror.</summary>
		MirrorsPublished = 11
	}

	public enum KingdomRealmCallbackPhase : byte
	{
		None = 0,
		Intent = 1,
		Attempting = 2,
		Settled = 3
	}

	public enum KingdomRealmCallbackDisposition : byte
	{
		None = 0,
		Delivered = 1,
		Skipped = 2,
		Lost = 3
	}

	public enum KingdomRealmCallbackScope : byte
	{
		None = 0,
		Chronicle = 1,
		Ability = 2,
		Reputation = 3,
		Feelings = 4,
		Seat = 5
	}

	[Serializable]
	public sealed class KingdomRealmCallbackReceipt
	{
		public const int MaxEffectChars = 65536;
		public KingdomRealmCallbackPhase Phase;
		public KingdomRealmCallbackDisposition Disposition;
		public KingdomRealmCallbackScope Scope;
		public string BeforeGraph;
		public string AfterGraph;
		public string BeforeArchiveGraph;
		public string AfterArchiveGraph;
		public string BeforeEffect;
		public string AfterEffect;
		public string ObservedEffect;
		public int BeforeStamp = int.MinValue;
		public int AfterStamp = int.MinValue;

		public bool Validate()
		{
			if (!Enum.IsDefined(typeof(KingdomRealmCallbackPhase), Phase) ||
				!Enum.IsDefined(typeof(KingdomRealmCallbackDisposition), Disposition) ||
				!Enum.IsDefined(typeof(KingdomRealmCallbackScope), Scope)) return false;
			if (Phase == KingdomRealmCallbackPhase.None)
				return Disposition == KingdomRealmCallbackDisposition.None && Scope ==
					KingdomRealmCallbackScope.None &&
					BeforeGraph == null && AfterGraph == null &&
					BeforeArchiveGraph == null && AfterArchiveGraph == null &&
					BeforeEffect == null && AfterEffect == null && ObservedEffect == null &&
					BeforeStamp == int.MinValue && AfterStamp == int.MinValue;
			if (string.IsNullOrEmpty(BeforeGraph) || BeforeGraph.Length != 64 ||
				string.IsNullOrEmpty(BeforeArchiveGraph) || BeforeArchiveGraph.Length != 64 ||
				Scope == KingdomRealmCallbackScope.None || BeforeEffect == null ||
				AfterEffect == null) return false;
			if (Scope == KingdomRealmCallbackScope.Feelings)
			{
				if (!Enum.IsDefined(typeof(RealmRegard), BeforeStamp) ||
					!Enum.IsDefined(typeof(RealmRegard), AfterStamp)) return false;
			}
			else if (BeforeStamp != int.MinValue || AfterStamp != int.MinValue) return false;
			if (Phase != KingdomRealmCallbackPhase.Settled)
				return Disposition == KingdomRealmCallbackDisposition.None &&
					AfterGraph == null && AfterArchiveGraph == null && ObservedEffect == null;
			return Disposition != KingdomRealmCallbackDisposition.None &&
				!string.IsNullOrEmpty(AfterGraph) && AfterGraph.Length == 64 &&
				!string.IsNullOrEmpty(AfterArchiveGraph) && AfterArchiveGraph.Length == 64 &&
				ObservedEffect != null;
		}
	}

	/// <summary>
	/// The realm-scoped half of one exiled realm. Cities and standings remain in the product's
	/// existing ExiledSeat/ExiledAway/ExiledStandings slots, but those are independent mirrors:
	/// the authoritative archive owns deep settlement and standings copies. Its manual reader
	/// bounds every archive-owned row, string, and nested settlement payload before allocation.
	/// Version 8 is the first System writer; archive v1's unsafe reflected settlement wire was a
	/// pre-release format and is deliberately refused rather than partly interpreted.
	/// </summary>
	[Serializable]
	public sealed class KingdomRealmArchive
#if !TAF_TESTS
		: IComposite
#endif
	{
		private const int Magic = 0x54415231; // TAR1
		public const int CurrentVersion = 2;
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

		public static bool TryCapture(KingdomSystem System, string ChronicleRegistry,
			string ChronicleFault, long ClosedTick, string ExileDeed,
			out KingdomRealmArchive Archive, out string Failure)
		{
			Archive = null;
			Failure = null;
			if (System == null || !string.IsNullOrEmpty(System.IdentityFault) ||
				!string.IsNullOrEmpty(System.PendingSettlementId) ||
				!string.IsNullOrEmpty(System.PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(System.PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(System.PendingSettlementAuthority) ||
				!KingdomIdentityRules.IsRealmId(System.CurrentRealmId) ||
				!System.TryExactSettlementIds(RequirePublishedClaims: true,
					out List<string> settlementIds, out Failure))
			{
				Failure = "current immutable realm identity cannot be proved";
				return false;
			}
			if (ClosedTick < 0L || !BoundedText(ExileDeed))
			{
				Failure = "realm archive tick or deed is not bounded";
				return false;
			}
			KingdomSettlement capturedSeat;
			try { capturedSeat = System.Capture(); }
			catch (Exception ex)
			{
				Failure = "seated settlement capture failed: " + Bound(ex.Message, 512);
				return false;
			}
			if (!KingdomArchivedSettlementCodec.TryClone(capturedSeat,
				out KingdomSettlement frozenSeat, out Failure) ||
				!KingdomArchivedSettlementCodec.TryClone(System.Away,
					out KingdomSettlement frozenAway, out Failure) ||
				!KingdomArchivedSettlementCodec.TryClone(System.Seceded,
					out KingdomSettlement frozenSeceded, out Failure) ||
				!TryCloneCarry(System.CarryBook, out KingdomCarryBook frozenCarry, out Failure))
				return false;
			KingdomRealmArchive candidate;
			try
			{
				candidate = new KingdomRealmArchive
				{
				RealmId = System.RealmId,
				FactionName = System.KingdomFactionName,
				DisplayName = System.KingdomDisplayName,
				ExileDeed = ExileDeed,
				ClosedTick = ClosedTick,
				SettlementIds = new List<string>(settlementIds),
				RealmIdentityVersion = System.RealmIdentityVersion,
				RealmIdentityOrigin = System.RealmIdentityOrigin,
				RealmIdentityTransactionId = System.RealmIdentityTransactionId,
				RealmIdentityLegacyFaction = System.RealmIdentityLegacyFaction,
				RealmIdentityFoundedTick = System.RealmIdentityFoundedTick,
				RealmIdentitySeedHigh = System.RealmIdentitySeedHigh,
				RealmIdentitySeedLow = System.RealmIdentitySeedLow,
				RealmIdentityFirstClaimedZone = System.RealmIdentityFirstClaimedZone,
				SimulationSeedHigh = System.SimulationSeedHigh,
				SimulationSeedLow = System.SimulationSeedLow,
				Seat = frozenSeat,
				Away = frozenAway,
				Standings = CloneStandings(System.Standings),
				Bindings = CloneBindings(System.Bindings),
				ResidentCounter = System.ResidentCounter,
				Jobs = CloneJobs(System.Jobs),
				LastSliceTick = System.LastSliceTick,
				ReifyTick = System.ReifyTick,
				ReifyThirdsSpent = System.ReifyThirdsSpent,
				ReifyHeavySpent = System.ReifyHeavySpent,
				ReifyQuietUntilTick = System.ReifyQuietUntilTick,
				DedicationCounter = System.DedicationCounter,
				ChronicleEntries = CloneStrings(System.ChronicleEntries),
				OutsiderEntries = CloneStrings(System.OutsiderEntries),
				ChronicleRegistry = ChronicleRegistry,
				ChronicleRegistryFault = ChronicleFault,
				RegardSpoken = System.RegardSpoken,
				Dissent = System.Dissent,
				DissentSpoken = System.DissentSpoken,
				LastDissentTick = System.LastDissentTick,
				DeclaredCreed = System.DeclaredCreed,
				DishName = System.DishName,
				DishText = System.DishText,
				DishStaple = System.DishStaple,
				DishSource = System.DishSource,
				LastRiteTick = System.LastRiteTick,
				LastSoulRiteTick = System.LastSoulRiteTick,
				Seceded = frozenSeceded,
				SecededTick = System.SecededTick,
				Haul = CloneHaul(System.Haul),
				CarryBook = frozenCarry,
				SeatWireVersion = KingdomArchivedSettlementCodec.CurrentVersion,
				AwayWireVersion = KingdomArchivedSettlementCodec.CurrentVersion,
				SecededWireVersion = KingdomArchivedSettlementCodec.CurrentVersion
				};
			}
			catch (Exception ex)
			{
				Failure = "realm graph clone failed: " + Bound(ex.Message, 512);
				return false;
			}
			if (!candidate.Validate(out Failure) ||
				!candidate.CurrentGraphMatches(System, out Failure)) return false;
			Archive = candidate;
			return true;
		}

		/// <summary>Hashes the authoritative archived realm while excluding only the field family
		/// the named callback is allowed to update. The current and later callback receipts plus
		/// transition phase are excluded to avoid self-reference; every earlier settled receipt is
		/// included so established callback proof cannot be changed behind a later callback.</summary>
		internal bool TryAuthorityHash(KingdomRealmCallbackReceipt ExcludedReceipt,
			KingdomRealmCallbackScope Scope, out string Hash, out string Failure)
		{
			Hash = null;
			Failure = null;
			if (Scope == KingdomRealmCallbackScope.None ||
				!Enum.IsDefined(typeof(KingdomRealmCallbackScope), Scope))
			{
				Failure = "callback authority scope is invalid";
				return false;
			}
			if (!OwnsCallbackReceipt(ExcludedReceipt))
			{
				Failure = "callback receipt is not owned by this archive";
				return false;
			}
			try
			{
				if (!KingdomArchivedSettlementCodec.TryEncode(Seat, out byte[] seatBytes,
					out Failure) || !KingdomArchivedSettlementCodec.TryEncode(Away,
						out byte[] awayBytes, out Failure) ||
					!KingdomArchivedSettlementCodec.TryEncode(Seceded,
						out byte[] secededBytes, out Failure) ||
					!TryCarryBytes(CarryBook, out byte[] carryBytes, out Failure)) return false;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(0x54414131); // TAA1
					writer.Write((byte)Scope);
					WriteGraphBytes(writer, seatBytes); WriteGraphBytes(writer, awayBytes);
					WriteGraphBytes(writer, secededBytes); WriteGraphBytes(writer, carryBytes);
					WriteGraphString(writer, RealmId); WriteGraphString(writer, FactionName);
					WriteGraphString(writer, DisplayName); WriteGraphString(writer, ExileDeed);
					writer.Write(ClosedTick); WriteGraphStrings(writer, SettlementIds);
					writer.Write(RealmIdentityVersion); writer.Write((byte)RealmIdentityOrigin);
					WriteGraphString(writer, RealmIdentityTransactionId);
					WriteGraphString(writer, RealmIdentityLegacyFaction);
					writer.Write(RealmIdentityFoundedTick); writer.Write(RealmIdentitySeedHigh);
					writer.Write(RealmIdentitySeedLow);
					WriteGraphString(writer, RealmIdentityFirstClaimedZone);
					writer.Write(SimulationSeedHigh); writer.Write(SimulationSeedLow);
					WriteGraphBindings(writer, Bindings); WriteGraphJobs(writer, Jobs);
					writer.Write(ResidentCounter); writer.Write(LastSliceTick);
					writer.Write(ReifyTick); writer.Write(ReifyThirdsSpent);
					writer.Write(ReifyHeavySpent); writer.Write(ReifyQuietUntilTick);
					writer.Write(DedicationCounter); WriteGraphDictionary(writer, Standings);
					if (Scope != KingdomRealmCallbackScope.Chronicle)
					{
						WriteGraphStrings(writer, ChronicleEntries);
						WriteGraphStrings(writer, OutsiderEntries);
						WriteGraphString(writer, ChronicleRegistry);
						WriteGraphString(writer, ChronicleRegistryFault);
					}
					if (Scope != KingdomRealmCallbackScope.Feelings) writer.Write(RegardSpoken);
					writer.Write(Dissent); writer.Write(DissentSpoken); writer.Write(LastDissentTick);
					WriteGraphString(writer, DeclaredCreed); WriteGraphString(writer, DishName);
					WriteGraphString(writer, DishText); WriteGraphString(writer, DishStaple);
					WriteGraphString(writer, DishSource); writer.Write(LastRiteTick);
					writer.Write(LastSoulRiteTick); writer.Write(SecededTick);
					WriteGraphHaul(writer, Haul); writer.Write(ReturnRegard);
					WritePriorAuthorityCallbacks(writer, ExcludedReceipt);
					writer.Flush();
					if (stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes * 4L)
						throw new InvalidDataException("Archive authority graph exceeds proof cap.");
					using (global::System.Security.Cryptography.SHA256 sha =
						global::System.Security.Cryptography.SHA256.Create())
					{
						byte[] digest = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
						Hash = text.ToString();
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message, 512);
				return false;
			}
		}

		private bool OwnsCallbackReceipt(KingdomRealmCallbackReceipt Value)
		{
			return Value != null && (ReferenceEquals(Value, ExileChronicle) ||
				ReferenceEquals(Value, ExileAbility) || ReferenceEquals(Value, ReturnChronicle) ||
				ReferenceEquals(Value, ReturnReputation) || ReferenceEquals(Value, ReturnFeelings) ||
				ReferenceEquals(Value, ReturnSeat) || ReferenceEquals(Value, ReturnAbility));
		}

		private void WritePriorAuthorityCallbacks(BinaryWriter Writer,
			KingdomRealmCallbackReceipt Current)
		{
			Writer.Write((byte)0x71);
			if (ReferenceEquals(Current, ExileChronicle)) return;
			WriteAuthorityCallback(Writer, ExileChronicle);
			if (ReferenceEquals(Current, ExileAbility)) return;
			WriteAuthorityCallback(Writer, ExileAbility);
			if (ReferenceEquals(Current, ReturnChronicle)) return;
			WriteAuthorityCallback(Writer, ReturnChronicle);
			if (ReferenceEquals(Current, ReturnReputation)) return;
			WriteAuthorityCallback(Writer, ReturnReputation);
			if (ReferenceEquals(Current, ReturnFeelings)) return;
			WriteAuthorityCallback(Writer, ReturnFeelings);
			if (ReferenceEquals(Current, ReturnSeat)) return;
			WriteAuthorityCallback(Writer, ReturnSeat);
		}

		private static void WriteAuthorityCallback(BinaryWriter Writer,
			KingdomRealmCallbackReceipt Value)
		{
			Writer.Write((byte)1); Writer.Write((byte)Value.Phase);
			Writer.Write((byte)Value.Disposition); Writer.Write((byte)Value.Scope);
			WriteGraphString(Writer, Value.BeforeGraph); WriteGraphString(Writer, Value.AfterGraph);
			WriteGraphString(Writer, Value.BeforeArchiveGraph);
			WriteGraphString(Writer, Value.AfterArchiveGraph);
			WriteGraphString(Writer, Value.BeforeEffect); WriteGraphString(Writer, Value.AfterEffect);
			WriteGraphString(Writer, Value.ObservedEffect);
			Writer.Write(Value.BeforeStamp); Writer.Write(Value.AfterStamp);
		}

		public bool Validate(out string Failure)
		{
			Failure = null;
			if (!ValidateEnvelope(out Failure)) return false;
			if (Phase == KingdomRealmArchivePhase.None ||
				Phase == KingdomRealmArchivePhase.Quarantined || Quarantined)
				return Refuse("archive phase or quarantine state grants no authority", out Failure);
			KingdomIdentityFault identityFault;
			if (!KingdomIdentityRules.ReproveRealm(RealmId, RealmIdentityVersion,
				RealmIdentityOrigin, RealmIdentityTransactionId, RealmIdentityLegacyFaction,
				RealmIdentityFoundedTick, RealmIdentitySeedHigh, RealmIdentitySeedLow,
				RealmIdentityFirstClaimedZone, out identityFault))
				return Refuse("archived realm provenance cannot be reproved (" + identityFault + ")",
					out Failure);
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out identityFault) || !StrictlySorted(SettlementIds))
				return Refuse("archived settlement topology cannot be reproved (" + identityFault + ")",
					out Failure);
			if (SeatOpaque != null || AwayOpaque != null || SecededOpaque != null ||
				SeatWireVersion != KingdomArchivedSettlementCodec.CurrentVersion ||
				AwayWireVersion != KingdomArchivedSettlementCodec.CurrentVersion ||
				SecededWireVersion != KingdomArchivedSettlementCodec.CurrentVersion ||
				Seat == null || Standings == null ||
				!ExactArchivedSettlements(RealmId, Seat, Away, SettlementIds) ||
				ReferenceEquals(Seat, Away) || ReferenceEquals(Seat, Seceded) ||
				ReferenceEquals(Away, Seceded))
				return Refuse("archived settlement graph is opaque, aliased, or lacks exact topology",
					out Failure);
			List<KingdomChronicleReceipt> receipts;
			bool migrated;
			KingdomChronicleRegistryFault registryFault;
			if (!KingdomChronicleReceiptRules.TryParseRegistry(ChronicleRegistry,
				out receipts, out migrated, out registryFault) || migrated)
				return Refuse("archive chronicle receipt graph is not canonical (" + registryFault + ")",
					out Failure);
			if (CarryBook == null || CarryBook.LegacyIdentity ||
				!string.Equals(CarryBook.RealmId, RealmId, StringComparison.Ordinal) ||
				!KingdomLifecycleRules.CanOwnAuthority(CarryBook))
				return Refuse("archive carry authority does not match exact realm identity", out Failure);
			if (!ValidHaulAuthority(Haul))
				return Refuse("archive haul has malformed value or immutable destination evidence",
					out Failure);
			if (!ValidCallback(ExileChronicle) || !ValidCallback(ExileAbility) ||
				!ValidCallback(ReturnChronicle) || !ValidCallback(ReturnReputation) ||
				!ValidCallback(ReturnFeelings) || !ValidCallback(ReturnSeat) ||
				!ValidCallback(ReturnAbility))
				return Refuse("archive callback receipt graph is malformed", out Failure);
			return true;
		}

		/// <summary>Codec safety independent of authority. Quarantined evidence must remain
		/// serializable, otherwise fail-closing one transition would make the whole save unwritable.</summary>
		internal bool ValidateEnvelope(out string Failure)
		{
			Failure = null;
			if (Version != CurrentVersion ||
				!Enum.IsDefined(typeof(KingdomRealmArchivePhase), Phase) ||
				(Quarantined != (Phase == KingdomRealmArchivePhase.Quarantined)))
				return Refuse("archive version, phase, or quarantine flag is noncanonical",
					out Failure);
			if (ClosedTick < 0L || ResidentCounter < 0 || LastSliceTick < 0L ||
				DedicationCounter < 0 ||
				ChronicleEntries == null || OutsiderEntries == null ||
				ChronicleEntries.Count > KingdomChronicle.MaxEntries ||
				OutsiderEntries.Count > KingdomChronicle.MaxEntries ||
				!BoundedStrings(ChronicleEntries, KingdomChronicleReceiptRules.MaxEntryChars) ||
				!BoundedStrings(OutsiderEntries, KingdomChronicleReceiptRules.MaxEntryChars) ||
				!BoundedUtf8(RealmId, 256, 1024) ||
				!BoundedUtf8(FactionName, 512, 2048) ||
				!BoundedUtf8(DisplayName, 512, 2048) || !BoundedText(ExileDeed) ||
				SettlementIds == null ||
				SettlementIds.Count > KingdomIdentityRules.MaxSettlements ||
				!BoundedStrings(SettlementIds, 256) ||
				!BoundedUtf8(RealmIdentityTransactionId, 64, 256) ||
				!BoundedUtf8(RealmIdentityLegacyFaction, 512, 2048) ||
				!BoundedUtf8(RealmIdentityFirstClaimedZone, 512, 2048) ||
				ChronicleRegistry == null ||
				!BoundedUtf8(ChronicleRegistry,
					KingdomChronicleReceiptRules.MaxRegistryChars,
					KingdomChronicleReceiptRules.MaxRegistryChars * 4) ||
				!BoundedUtf8(ChronicleRegistryFault, 160, 640) ||
				!BoundedText(Fault) || !BoundedText(DeclaredCreed) || !BoundedText(DishName) ||
				!BoundedText(DishText) || !BoundedText(DishStaple) || !BoundedText(DishSource) ||
				!BoundedOpaque(SeatOpaque) || !BoundedOpaque(AwayOpaque) ||
				!BoundedOpaque(SecededOpaque) || !BoundedStandings(Standings) ||
				CarryBook == null || CarryBook.WireRejected ||
				!ValidBindings(Bindings) || !ValidJobs(Jobs) || !BoundedHaul(Haul) ||
				!ValidCallbackEnvelope(ExileChronicle) || !ValidCallbackEnvelope(ExileAbility) ||
				!ValidCallbackEnvelope(ReturnChronicle) ||
				!ValidCallbackEnvelope(ReturnReputation) ||
				!ValidCallbackEnvelope(ReturnFeelings) ||
				!ValidCallbackEnvelope(ReturnSeat) ||
				!ValidCallbackEnvelope(ReturnAbility))
				return Refuse("archive payload is ragged or exceeds codec bounds", out Failure);
			return true;
		}

		public void Quarantine(string Failure)
		{
			Quarantined = true;
			Phase = KingdomRealmArchivePhase.Quarantined;
			Fault = Bound(Failure, 4096);
		}

		/// <summary>Exact full-graph comparison used after every engine callback. It compares
		/// values and rejects shared mutable references; identity/topology alone is insufficient
		/// because a callback can retain the same ids while replacing or editing a city graph.</summary>
		internal bool CurrentGraphMatches(KingdomSystem System, out string Failure)
		{
			bool swapped = ReturnSeat != null &&
				ReturnSeat.Phase == KingdomRealmCallbackPhase.Settled &&
				!string.Equals(ReturnSeat.BeforeEffect, ReturnSeat.AfterEffect,
					StringComparison.Ordinal);
			return CurrentGraphMatches(System, swapped, IgnoreChronicle: false, out Failure);
		}

		internal bool CurrentGraphMatchesAfterSeat(KingdomSystem System, bool Swapped,
			out string Failure)
		{
			return CurrentGraphMatches(System, Swapped, IgnoreChronicle: false, out Failure);
		}

		internal bool CurrentGraphMatchesExceptChronicle(KingdomSystem System,
			out string Failure)
		{
			bool swapped = ReturnSeat != null &&
				ReturnSeat.Phase == KingdomRealmCallbackPhase.Settled &&
				!string.Equals(ReturnSeat.BeforeEffect, ReturnSeat.AfterEffect,
					StringComparison.Ordinal);
			return CurrentGraphMatches(System, swapped, IgnoreChronicle: true, out Failure);
		}

		private bool CurrentGraphMatches(KingdomSystem System, bool SeatSwapped,
			bool IgnoreChronicle, out string Failure)
		{
			Failure = null;
			if (System == null || Quarantined ||
				!string.IsNullOrEmpty(System.IdentityFault) ||
				!string.IsNullOrEmpty(System.PendingSettlementId) ||
				!string.IsNullOrEmpty(System.PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(System.PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(System.PendingSettlementAuthority) ||
				!string.Equals(System.RealmId, RealmId, StringComparison.Ordinal) ||
				!string.Equals(System.KingdomFactionName, FactionName, StringComparison.Ordinal) ||
				!string.Equals(System.KingdomDisplayName, DisplayName, StringComparison.Ordinal) ||
				System.RealmIdentityVersion != RealmIdentityVersion ||
				System.RealmIdentityOrigin != RealmIdentityOrigin ||
				!string.Equals(System.RealmIdentityTransactionId,
					RealmIdentityTransactionId, StringComparison.Ordinal) ||
				!string.Equals(System.RealmIdentityLegacyFaction,
					RealmIdentityLegacyFaction, StringComparison.Ordinal) ||
				System.RealmIdentityFoundedTick != RealmIdentityFoundedTick ||
				System.RealmIdentitySeedHigh != RealmIdentitySeedHigh ||
				System.RealmIdentitySeedLow != RealmIdentitySeedLow ||
				!string.Equals(System.RealmIdentityFirstClaimedZone,
					RealmIdentityFirstClaimedZone, StringComparison.Ordinal))
				return Refuse("current realm scalar identity differs from archive", out Failure);
			KingdomSettlement currentSeat;
			try { currentSeat = System.Capture(); }
			catch (Exception ex) { return Refuse(Bound(ex.Message, 512), out Failure); }
			KingdomSettlement expectedSeat = SeatSwapped ? Away : Seat;
			KingdomSettlement expectedAway = SeatSwapped ? Seat : Away;
			if (!KingdomArchivedSettlementCodec.ExactGraph(expectedSeat, currentSeat, out Failure) ||
				!KingdomArchivedSettlementCodec.ExactGraph(expectedAway, System.Away, out Failure) ||
				!KingdomArchivedSettlementCodec.ExactGraph(Seceded, System.Seceded, out Failure))
				return false;
			if (!ExactDictionary(Standings, System.Standings) ||
				ReferenceEquals(Standings, System.Standings))
				return Refuse("current standings differ from or alias archive", out Failure);
			if (IgnoreChronicle && (ReferenceEquals(ChronicleEntries, System.ChronicleEntries) ||
				ReferenceEquals(ChronicleEntries, System.OutsiderEntries) ||
				ReferenceEquals(OutsiderEntries, System.ChronicleEntries) ||
				ReferenceEquals(OutsiderEntries, System.OutsiderEntries)))
				return Refuse("current Chronicle registers alias archive evidence", out Failure);
			if (!ExactBindings(Bindings, System.Bindings) ||
				!ExactJobs(Jobs, System.Jobs) ||
				(!IgnoreChronicle &&
				 (!ExactStrings(ChronicleEntries, System.ChronicleEntries) ||
				  !ExactStrings(OutsiderEntries, System.OutsiderEntries))) ||
				!ExactHaul(Haul, System.Haul) ||
				!ExactCarry(CarryBook, System.CarryBook))
				return Refuse("current realm mutable graph differs from or aliases archive", out Failure);
			// IgnoreChronicle suppresses only the two value comparisons while their declared
			// callback is in flight. Chronicle roots remain in the reference proof so they
			// cannot alias a seat, registry, carry, haul, or opposite-realm root.
			object[] archivedRoots = { Seat, Away, Seceded, Standings, Bindings, Jobs,
				ChronicleEntries, OutsiderEntries, Haul, CarryBook };
			object[] liveRoots = { currentSeat, System.Away, System.Seceded, System.Standings,
				System.Bindings, System.Jobs, System.ChronicleEntries, System.OutsiderEntries,
				System.Haul, System.CarryBook };
			if (!KingdomArchivedSettlementCodec.DisjointMutableGraphs(archivedRoots, liveRoots,
				out Failure)) return false;
			if (SimulationSeedHigh != System.SimulationSeedHigh ||
				SimulationSeedLow != System.SimulationSeedLow ||
				ResidentCounter != System.ResidentCounter || LastSliceTick != System.LastSliceTick ||
				ReifyTick != System.ReifyTick || ReifyThirdsSpent != System.ReifyThirdsSpent ||
				ReifyHeavySpent != System.ReifyHeavySpent ||
				ReifyQuietUntilTick != System.ReifyQuietUntilTick ||
				DedicationCounter != System.DedicationCounter || RegardSpoken != System.RegardSpoken ||
				Dissent != System.Dissent || DissentSpoken != System.DissentSpoken ||
				LastDissentTick != System.LastDissentTick || DeclaredCreed != System.DeclaredCreed ||
				DishName != System.DishName || DishText != System.DishText ||
				DishStaple != System.DishStaple || DishSource != System.DishSource ||
				LastRiteTick != System.LastRiteTick || LastSoulRiteTick != System.LastSoulRiteTick ||
				SecededTick != System.SecededTick)
				return Refuse("current realm counters differ from archive", out Failure);
			return true;
		}

		internal bool ExactMirrors(string MirrorFaction, string MirrorDisplay,
			string MirrorDeed, long MirrorTick, KingdomSettlement MirrorSeat,
			KingdomSettlement MirrorAway, Dictionary<string, int> MirrorStandings,
			out string Failure)
		{
			Failure = null;
			if (!string.Equals(FactionName, MirrorFaction, StringComparison.Ordinal) ||
				!string.Equals(DisplayName, MirrorDisplay, StringComparison.Ordinal) ||
				!string.Equals(ExileDeed, MirrorDeed, StringComparison.Ordinal) ||
				ClosedTick != MirrorTick || !KingdomArchivedSettlementCodec.ExactGraph(Seat,
					MirrorSeat, out Failure) || !KingdomArchivedSettlementCodec.ExactGraph(Away,
					MirrorAway, out Failure)) return false;
			if (ReferenceEquals(Standings, MirrorStandings) ||
				!ExactDictionary(Standings, MirrorStandings))
				return Refuse("exile standings mirror differs from or aliases archive", out Failure);
			object[] archivedRoots = { Seat, Away, Standings };
			object[] mirrorRoots = { MirrorSeat, MirrorAway, MirrorStandings };
			if (!KingdomArchivedSettlementCodec.DisjointMutableGraphs(archivedRoots, mirrorRoots,
				out Failure)) return false;
			return true;
		}

		internal static bool TryCurrentGraphHash(KingdomSystem System, out string Hash,
			out string Failure)
		{
			Hash = null;
			Failure = null;
			if (System == null) { Failure = "current realm is absent"; return false; }
			try
			{
				KingdomSettlement seat = System.Capture();
				if (!KingdomArchivedSettlementCodec.TryEncode(seat, out byte[] seatBytes, out Failure) ||
					!KingdomArchivedSettlementCodec.TryEncode(System.Away, out byte[] awayBytes,
						out Failure) ||
					!KingdomArchivedSettlementCodec.TryEncode(System.Seceded, out byte[] secededBytes,
						out Failure) ||
					!TryCarryBytes(System.CarryBook, out byte[] carryBytes, out Failure)) return false;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(0x54414731); // TAG1
					WriteGraphBytes(writer, seatBytes); WriteGraphBytes(writer, awayBytes);
					WriteGraphBytes(writer, secededBytes); WriteGraphBytes(writer, carryBytes);
					WriteGraphString(writer, System.RealmId); WriteGraphString(writer, System.KingdomFactionName);
					WriteGraphString(writer, System.KingdomDisplayName);
					writer.Write(System.RealmIdentityVersion);
					writer.Write((byte)System.RealmIdentityOrigin);
					WriteGraphString(writer, System.RealmIdentityTransactionId);
					WriteGraphString(writer, System.RealmIdentityLegacyFaction);
					writer.Write(System.RealmIdentityFoundedTick); writer.Write(System.RealmIdentitySeedHigh);
					writer.Write(System.RealmIdentitySeedLow);
					WriteGraphString(writer, System.RealmIdentityFirstClaimedZone);
					WriteGraphString(writer, System.IdentityFault);
					WriteGraphString(writer, System.PendingSettlementId);
					WriteGraphString(writer, System.PendingSettlementTransactionId);
					WriteGraphString(writer, System.PendingSettlementZoneId);
					WriteGraphString(writer, System.PendingSettlementAuthority);
					writer.Write(System.SimulationSeedHigh); writer.Write(System.SimulationSeedLow);
					WriteGraphBindings(writer, System.Bindings); WriteGraphJobs(writer, System.Jobs);
					writer.Write(System.ResidentCounter); writer.Write(System.LastSliceTick);
					writer.Write(System.ReifyTick); writer.Write(System.ReifyThirdsSpent);
					writer.Write(System.ReifyHeavySpent); writer.Write(System.ReifyQuietUntilTick);
					writer.Write(System.DedicationCounter);
					WriteGraphDictionary(writer, System.Standings);
					WriteGraphStrings(writer, System.ChronicleEntries);
					WriteGraphStrings(writer, System.OutsiderEntries);
					writer.Write(System.RegardSpoken); writer.Write(System.Dissent);
					writer.Write(System.DissentSpoken); writer.Write(System.LastDissentTick);
					WriteGraphString(writer, System.DeclaredCreed); WriteGraphString(writer, System.DishName);
					WriteGraphString(writer, System.DishText); WriteGraphString(writer, System.DishStaple);
					WriteGraphString(writer, System.DishSource); writer.Write(System.LastRiteTick);
					writer.Write(System.LastSoulRiteTick); writer.Write(System.SecededTick);
					WriteGraphHaul(writer, System.Haul);
					writer.Flush();
					if (stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes * 4L)
						throw new InvalidDataException("Current realm graph exceeds proof cap.");
					using (global::System.Security.Cryptography.SHA256 sha =
						global::System.Security.Cryptography.SHA256.Create())
					{
						byte[] digest = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
						Hash = text.ToString();
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message, 512);
				return false;
			}
		}

		private bool Refuse(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}

		private static bool BoundedText(string Value)
		{
			return BoundedUtf8(Value, 4096, MaxTextBytes);
		}

		private static bool BoundedOpaque(byte[] Value)
		{
			return Value == null || Value.Length <= KingdomArchivedSettlementCodec.MaxPayloadBytes;
		}

		private static bool BoundedStandings(Dictionary<string, int> Value)
		{
			if (Value == null || Value.Count > 512) return false;
			foreach (KeyValuePair<string, int> row in Value)
				if (!BoundedUtf8(row.Key, 512, 2048)) return false;
			return true;
		}

		private static bool ValidCallback(KingdomRealmCallbackReceipt Value)
		{
			return Value != null && Value.Validate();
		}

		private static bool ValidCallbackEnvelope(KingdomRealmCallbackReceipt Value)
		{
			return Value != null &&
				Enum.IsDefined(typeof(KingdomRealmCallbackPhase), Value.Phase) &&
				Enum.IsDefined(typeof(KingdomRealmCallbackDisposition), Value.Disposition) &&
				Enum.IsDefined(typeof(KingdomRealmCallbackScope), Value.Scope) &&
				BoundedUtf8(Value.BeforeGraph, 64, 64) &&
				BoundedUtf8(Value.AfterGraph, 64, 64) &&
				BoundedUtf8(Value.BeforeArchiveGraph, 64, 64) &&
				BoundedUtf8(Value.AfterArchiveGraph, 64, 64) &&
				BoundedUtf8(Value.BeforeEffect, KingdomRealmCallbackReceipt.MaxEffectChars,
					KingdomRealmCallbackReceipt.MaxEffectChars * 4) &&
				BoundedUtf8(Value.AfterEffect, KingdomRealmCallbackReceipt.MaxEffectChars,
					KingdomRealmCallbackReceipt.MaxEffectChars * 4) &&
				BoundedUtf8(Value.ObservedEffect, KingdomRealmCallbackReceipt.MaxEffectChars,
					KingdomRealmCallbackReceipt.MaxEffectChars * 4);
		}

		private static bool ExactArchivedSettlements(string RealmId,
			KingdomSettlement Seat, KingdomSettlement Away, IList<string> ExpectedIds)
		{
			List<string> ids = new List<string>();
			if (!ArchivedSettlementMatches(RealmId, Seat, out string seatId)) return false;
			ids.Add(seatId);
			if (Away != null)
			{
				if (!ArchivedSettlementMatches(RealmId, Away, out string awayId)) return false;
				ids.Add(awayId);
			}
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, ids, out fault)) return false;
			ids.Sort(StringComparer.Ordinal);
			if (ExpectedIds == null || ids.Count != ExpectedIds.Count) return false;
			for (int i = 0; i < ids.Count; i++)
				if (!string.Equals(ids[i], ExpectedIds[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ArchivedSettlementMatches(string RealmId,
			KingdomSettlement Settlement, out string SettlementId)
		{
			SettlementId = Settlement?.City?.SettlementId;
			KingdomIdentityFault fault;
			return Settlement != null && Settlement.ClaimedZones != null &&
				Settlement.ClaimedZones.Contains(Settlement.SettlementIdentityFirstClaimedZone) &&
				KingdomIdentityRules.ReproveSettlement(SettlementId, RealmId,
					Settlement.SettlementIdentityVersion, Settlement.SettlementIdentityOrigin,
					Settlement.SettlementIdentityTransactionId,
					Settlement.SettlementIdentityFoundedTick,
					Settlement.SettlementIdentityFirstClaimedZone, out fault) &&
				Settlement.LifecycleBook != null && !Settlement.LifecycleBook.LegacyIdentity &&
				string.Equals(Settlement.LifecycleBook.SettlementId, SettlementId,
					StringComparison.Ordinal) &&
				KingdomLifecycleRules.CanOwnAuthority(Settlement.LifecycleBook);
		}

		private static bool BoundedUtf8(string Value, int MaxChars, int MaxBytes)
		{
			if (Value == null) return true;
			try
			{
				return Value.Length <= MaxChars && StrictUtf8.GetByteCount(Value) <= MaxBytes;
			}
			catch (EncoderFallbackException)
			{
				return false;
			}
		}

		private static bool BoundedHaul(KingdomCarryHaul Value)
		{
			return Value == null ||
				(BoundedUtf8(Value.OriginZoneID, 512, 2048) &&
				 BoundedUtf8(Value.DestinationSettlementId, 256, 1024) &&
				 BoundedUtf8(Value.DestinationSettlementName, 512, 2048));
		}

		private static bool ValidHaulAuthority(KingdomCarryHaul Value)
		{
			if (Value == null) return true;
			return KingdomIdentityRules.IsSettlementId(Value.DestinationSettlementId) &&
				Value.PlantedTick >= 0L && Value.DueTick >= Value.PlantedTick &&
				Value.Mud >= 0 && Value.Brush >= 0 && Value.Timber >= 0 &&
				Value.Stone >= 0 && Value.Marble >= 0 && Value.Scrap >= 0;
		}

		private static bool StrictlySorted(IList<string> Values)
		{
			if (Values == null) return false;
			for (int i = 1; i < Values.Count; i++)
				if (string.CompareOrdinal(Values[i - 1], Values[i]) >= 0) return false;
			return true;
		}

		private static bool BoundedStrings(List<string> Values, int MaxChars)
		{
			if (Values == null) return false;
			for (int i = 0; i < Values.Count; i++)
			{
				if (Values[i] == null || Values[i].Length > MaxChars) return false;
				try
				{
					if (StrictUtf8.GetByteCount(Values[i]) > MaxTextBytes * 4) return false;
				}
				catch (EncoderFallbackException)
				{
					return false;
				}
			}
			return true;
		}

		private static bool ValidBindings(Simulation.City.KingdomBindingRegistry Value)
		{
			if (Value == null || Value.Keys == null || Value.Kinds == null || Value.ZoneIds == null
				|| Value.ObjectIds == null || Value.MintedTicks == null) return false;
			int count = Value.Keys.Count;
			return count <= MaxBindings && Value.Kinds.Count == count && Value.ZoneIds.Count == count
				&& Value.ObjectIds.Count == count && Value.MintedTicks.Count == count
				&& BoundedStrings(Value.ZoneIds, 512) && BoundedStrings(Value.ObjectIds, 512);
		}

		private static bool ValidJobs(Simulation.City.KingdomJobRegistry Value)
		{
			if (Value == null || Value.JobCounter < 0 || Value.JobIds == null || Value.Kinds == null
				|| Value.Cargos == null || Value.CargoAmounts == null || Value.SourceZoneIds == null
				|| Value.DestZoneIds == null || Value.StartTicks == null || Value.WalkTicksPerCell == null
				|| Value.Statuses == null || Value.OriginCodes == null || Value.DepositLegIndexes == null
				|| Value.LegCounts == null || Value.LegZoneIds == null || Value.LegEnterX == null
				|| Value.LegEnterY == null || Value.LegExitX == null || Value.LegExitY == null
				|| Value.LegLengths == null || Value.LegDepartTicks == null || Value.LegArriveTicks == null)
				return false;
			int jobs = Value.JobIds.Count;
			if (jobs > MaxJobs || Value.Kinds.Count != jobs || Value.Cargos.Count != jobs
				|| Value.CargoAmounts.Count != jobs || Value.SourceZoneIds.Count != jobs
				|| Value.DestZoneIds.Count != jobs || Value.StartTicks.Count != jobs
				|| Value.WalkTicksPerCell.Count != jobs || Value.Statuses.Count != jobs
				|| Value.OriginCodes.Count != jobs || Value.DepositLegIndexes.Count != jobs
				|| Value.LegCounts.Count != jobs || !BoundedStrings(Value.SourceZoneIds, 512)
				|| !BoundedStrings(Value.DestZoneIds, 512)) return false;
			int legs = 0;
			for (int i = 0; i < jobs; i++)
			{
				if (Value.LegCounts[i] < 0 || Value.LegCounts[i] > 6) return false;
				legs += Value.LegCounts[i];
			}
			return legs <= MaxLegs && Value.LegZoneIds.Count == legs
				&& Value.LegEnterX.Count == legs && Value.LegEnterY.Count == legs
				&& Value.LegExitX.Count == legs && Value.LegExitY.Count == legs
				&& Value.LegLengths.Count == legs && Value.LegDepartTicks.Count == legs
				&& Value.LegArriveTicks.Count == legs && BoundedStrings(Value.LegZoneIds, 512);
		}

		private static string Bound(string Value, int Maximum)
		{
			if (string.IsNullOrEmpty(Value)) return "realm archive requires inspection";
			string bounded = Value.Length <= Maximum ? Value : Value.Substring(0, Maximum);
			try
			{
				if (StrictUtf8.GetByteCount(bounded) <= MaxTextBytes) return bounded;
				bounded = bounded.Substring(0, Math.Min(2048, bounded.Length));
				return StrictUtf8.GetByteCount(bounded) <= MaxTextBytes
					? bounded : "realm archive requires inspection";
			}
			catch (EncoderFallbackException)
			{
				return "realm archive requires inspection";
			}
		}

		internal static List<string> CloneStrings(List<string> Value)
		{
			return Value == null ? null : new List<string>(Value);
		}

		internal static Dictionary<string, int> CloneStandings(Dictionary<string, int> Value)
		{
			return Value == null ? null : new Dictionary<string, int>(Value,
				StringComparer.Ordinal);
		}

		internal static Simulation.City.KingdomBindingRegistry CloneBindings(
			Simulation.City.KingdomBindingRegistry Value)
		{
			if (Value == null) return null;
			return new Simulation.City.KingdomBindingRegistry
			{
				Keys = new List<int>(Value.Keys), Kinds = new List<int>(Value.Kinds),
				ZoneIds = new List<string>(Value.ZoneIds),
				ObjectIds = new List<string>(Value.ObjectIds),
				MintedTicks = new List<long>(Value.MintedTicks)
			};
		}

		internal static Simulation.City.KingdomJobRegistry CloneJobs(
			Simulation.City.KingdomJobRegistry Value)
		{
			if (Value == null) return null;
			return new Simulation.City.KingdomJobRegistry
			{
				JobCounter = Value.JobCounter,
				JobIds = new List<int>(Value.JobIds), Kinds = new List<int>(Value.Kinds),
				Cargos = new List<int>(Value.Cargos),
				CargoAmounts = new List<int>(Value.CargoAmounts),
				SourceZoneIds = new List<string>(Value.SourceZoneIds),
				DestZoneIds = new List<string>(Value.DestZoneIds),
				StartTicks = new List<long>(Value.StartTicks),
				WalkTicksPerCell = new List<int>(Value.WalkTicksPerCell),
				Statuses = new List<int>(Value.Statuses),
				OriginCodes = new List<int>(Value.OriginCodes),
				DepositLegIndexes = new List<int>(Value.DepositLegIndexes),
				LegCounts = new List<int>(Value.LegCounts),
				LegZoneIds = new List<string>(Value.LegZoneIds),
				LegEnterX = new List<int>(Value.LegEnterX),
				LegEnterY = new List<int>(Value.LegEnterY),
				LegExitX = new List<int>(Value.LegExitX),
				LegExitY = new List<int>(Value.LegExitY),
				LegLengths = new List<int>(Value.LegLengths),
				LegDepartTicks = new List<long>(Value.LegDepartTicks),
				LegArriveTicks = new List<long>(Value.LegArriveTicks)
			};
		}

		internal static KingdomCarryHaul CloneHaul(KingdomCarryHaul Value)
		{
			if (Value == null) return null;
			return new KingdomCarryHaul
			{
				OriginZoneID = Value.OriginZoneID, OriginX = Value.OriginX, OriginY = Value.OriginY,
				DestinationSettlementId = Value.DestinationSettlementId,
				DestinationSettlementName = Value.DestinationSettlementName,
				PlantedTick = Value.PlantedTick, DueTick = Value.DueTick,
				Mud = Value.Mud, Brush = Value.Brush, Timber = Value.Timber,
				Stone = Value.Stone, Marble = Value.Marble, Scrap = Value.Scrap
			};
		}

		internal static bool TryCloneCarry(KingdomCarryBook Value,
			out KingdomCarryBook Clone, out string Failure)
		{
			Clone = null;
			Failure = null;
			if (Value == null) return true;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
						KingdomLifecycleWireCodec.WriteCarry(writer, Value);
					if (stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
						throw new InvalidDataException("Archived carry book exceeds cap.");
					stream.Position = 0L;
					Clone = new KingdomCarryBook();
					using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
						KingdomLifecycleWireCodec.ReadCarry(reader, Clone);
					if (stream.Position != stream.Length)
						throw new InvalidDataException("Archived carry book has trailing bytes.");
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message, 512);
				Clone = null;
				return false;
			}
		}

		private static bool ExactStrings(List<string> Archived, List<string> Current)
		{
			if (Archived == null || Current == null || ReferenceEquals(Archived, Current) ||
				Archived.Count != Current.Count) return false;
			for (int i = 0; i < Archived.Count; i++)
				if (!string.Equals(Archived[i], Current[i], StringComparison.Ordinal)) return false;
			return true;
		}

		internal static bool ExactDictionary(Dictionary<string, int> Archived,
			Dictionary<string, int> Current)
		{
			if (Archived == null || Current == null || Archived.Count != Current.Count) return false;
			foreach (KeyValuePair<string, int> row in Archived)
				if (!Current.TryGetValue(row.Key, out int value) || value != row.Value) return false;
			return true;
		}

		private static bool ExactBindings(Simulation.City.KingdomBindingRegistry Archived,
			Simulation.City.KingdomBindingRegistry Current)
		{
			return Archived != null && Current != null && !ReferenceEquals(Archived, Current) &&
				!ReferenceEquals(Archived.Keys, Current.Keys) &&
				ExactList(Archived.Keys, Current.Keys) && ExactList(Archived.Kinds, Current.Kinds) &&
				ExactList(Archived.ZoneIds, Current.ZoneIds) &&
				ExactList(Archived.ObjectIds, Current.ObjectIds) &&
				ExactList(Archived.MintedTicks, Current.MintedTicks);
		}

		private static bool ExactJobs(Simulation.City.KingdomJobRegistry Archived,
			Simulation.City.KingdomJobRegistry Current)
		{
			return Archived != null && Current != null && !ReferenceEquals(Archived, Current) &&
				Archived.JobCounter == Current.JobCounter &&
				ExactList(Archived.JobIds, Current.JobIds) && ExactList(Archived.Kinds, Current.Kinds) &&
				ExactList(Archived.Cargos, Current.Cargos) &&
				ExactList(Archived.CargoAmounts, Current.CargoAmounts) &&
				ExactList(Archived.SourceZoneIds, Current.SourceZoneIds) &&
				ExactList(Archived.DestZoneIds, Current.DestZoneIds) &&
				ExactList(Archived.StartTicks, Current.StartTicks) &&
				ExactList(Archived.WalkTicksPerCell, Current.WalkTicksPerCell) &&
				ExactList(Archived.Statuses, Current.Statuses) &&
				ExactList(Archived.OriginCodes, Current.OriginCodes) &&
				ExactList(Archived.DepositLegIndexes, Current.DepositLegIndexes) &&
				ExactList(Archived.LegCounts, Current.LegCounts) &&
				ExactList(Archived.LegZoneIds, Current.LegZoneIds) &&
				ExactList(Archived.LegEnterX, Current.LegEnterX) &&
				ExactList(Archived.LegEnterY, Current.LegEnterY) &&
				ExactList(Archived.LegExitX, Current.LegExitX) &&
				ExactList(Archived.LegExitY, Current.LegExitY) &&
				ExactList(Archived.LegLengths, Current.LegLengths) &&
				ExactList(Archived.LegDepartTicks, Current.LegDepartTicks) &&
				ExactList(Archived.LegArriveTicks, Current.LegArriveTicks);
		}

		private static bool ExactList<T>(List<T> Archived, List<T> Current)
		{
			if (Archived == null || Current == null || ReferenceEquals(Archived, Current) ||
				Archived.Count != Current.Count) return false;
			EqualityComparer<T> comparer = EqualityComparer<T>.Default;
			for (int i = 0; i < Archived.Count; i++)
				if (!comparer.Equals(Archived[i], Current[i])) return false;
			return true;
		}

		private static bool ExactHaul(KingdomCarryHaul Archived, KingdomCarryHaul Current)
		{
			if (Archived == null || Current == null) return Archived == null && Current == null;
			return !ReferenceEquals(Archived, Current) && Archived.OriginZoneID == Current.OriginZoneID &&
				Archived.OriginX == Current.OriginX && Archived.OriginY == Current.OriginY &&
				Archived.DestinationSettlementId == Current.DestinationSettlementId &&
				Archived.DestinationSettlementName == Current.DestinationSettlementName &&
				Archived.PlantedTick == Current.PlantedTick && Archived.DueTick == Current.DueTick &&
				Archived.Mud == Current.Mud && Archived.Brush == Current.Brush &&
				Archived.Timber == Current.Timber && Archived.Stone == Current.Stone &&
				Archived.Marble == Current.Marble && Archived.Scrap == Current.Scrap;
		}

		private static bool ExactCarry(KingdomCarryBook Archived, KingdomCarryBook Current)
		{
			if (Archived == null || Current == null) return Archived == null && Current == null;
			if (ReferenceEquals(Archived, Current) ||
				!TryCarryBytes(Archived, out byte[] left, out string _) ||
				!TryCarryBytes(Current, out byte[] right, out string _) || left.Length != right.Length)
				return false;
			int difference = 0;
			for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
			return difference == 0;
		}

		private static bool TryCarryBytes(KingdomCarryBook Value, out byte[] Bytes,
			out string Failure)
		{
			Bytes = null;
			Failure = null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					KingdomLifecycleWireCodec.WriteCarry(writer, Value);
					writer.Flush();
					if (stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
						throw new InvalidDataException("Archived carry book exceeds cap.");
					Bytes = stream.ToArray();
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message, 512);
				return false;
			}
		}

		private static void WriteGraphBytes(BinaryWriter Writer, byte[] Value)
		{
			if (Value == null || Value.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
				throw new InvalidDataException("Realm graph byte block exceeds cap.");
			Writer.Write(Value.Length); Writer.Write(Value);
		}

		private static void WriteGraphString(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			int count = StrictUtf8.GetByteCount(Value);
			if (count > MaxTextBytes) throw new InvalidDataException("Realm graph string exceeds cap.");
			Writer.Write(count); Writer.Write(StrictUtf8.GetBytes(Value));
		}

		private static void WriteGraphStrings(BinaryWriter Writer, List<string> Value)
		{
			if (Value == null || Value.Count > KingdomChronicle.MaxEntries)
				throw new InvalidDataException("Realm graph list exceeds cap.");
			Writer.Write(Value.Count);
			for (int i = 0; i < Value.Count; i++) WriteGraphString(Writer, Value[i]);
		}

		private static void WriteGraphDictionary(BinaryWriter Writer,
			Dictionary<string, int> Value)
		{
			if (!BoundedStandings(Value)) throw new InvalidDataException("Realm graph map exceeds cap.");
			List<string> keys = new List<string>(Value.Keys); keys.Sort(StringComparer.Ordinal);
			Writer.Write(keys.Count);
			for (int i = 0; i < keys.Count; i++)
			{
				WriteGraphString(Writer, keys[i]); Writer.Write(Value[keys[i]]);
			}
		}

		private static void WriteGraphBindings(BinaryWriter Writer,
			Simulation.City.KingdomBindingRegistry Value)
		{
			if (!ValidBindings(Value)) throw new InvalidDataException("Realm binding graph is invalid.");
			Writer.Write(Value.Keys.Count);
			for (int i = 0; i < Value.Keys.Count; i++)
			{
				Writer.Write(Value.Keys[i]); Writer.Write(Value.Kinds[i]);
				WriteGraphString(Writer, Value.ZoneIds[i]); WriteGraphString(Writer, Value.ObjectIds[i]);
				Writer.Write(Value.MintedTicks[i]);
			}
		}

		private static void WriteGraphJobs(BinaryWriter Writer,
			Simulation.City.KingdomJobRegistry Value)
		{
			if (!ValidJobs(Value)) throw new InvalidDataException("Realm job graph is invalid.");
			Writer.Write(Value.JobCounter); Writer.Write(Value.JobIds.Count);
			for (int i = 0; i < Value.JobIds.Count; i++)
			{
				Writer.Write(Value.JobIds[i]); Writer.Write(Value.Kinds[i]); Writer.Write(Value.Cargos[i]);
				Writer.Write(Value.CargoAmounts[i]); WriteGraphString(Writer, Value.SourceZoneIds[i]);
				WriteGraphString(Writer, Value.DestZoneIds[i]); Writer.Write(Value.StartTicks[i]);
				Writer.Write(Value.WalkTicksPerCell[i]); Writer.Write(Value.Statuses[i]);
				Writer.Write(Value.OriginCodes[i]); Writer.Write(Value.DepositLegIndexes[i]);
				Writer.Write(Value.LegCounts[i]);
			}
			Writer.Write(Value.LegZoneIds.Count);
			for (int i = 0; i < Value.LegZoneIds.Count; i++)
			{
				WriteGraphString(Writer, Value.LegZoneIds[i]); Writer.Write(Value.LegEnterX[i]);
				Writer.Write(Value.LegEnterY[i]); Writer.Write(Value.LegExitX[i]);
				Writer.Write(Value.LegExitY[i]); Writer.Write(Value.LegLengths[i]);
				Writer.Write(Value.LegDepartTicks[i]); Writer.Write(Value.LegArriveTicks[i]);
			}
		}

		private static void WriteGraphHaul(BinaryWriter Writer, KingdomCarryHaul Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			WriteGraphString(Writer, Value.OriginZoneID); Writer.Write(Value.OriginX);
			Writer.Write(Value.OriginY); WriteGraphString(Writer, Value.DestinationSettlementId);
			WriteGraphString(Writer, Value.DestinationSettlementName); Writer.Write(Value.PlantedTick);
			Writer.Write(Value.DueTick); Writer.Write(Value.Mud); Writer.Write(Value.Brush);
			Writer.Write(Value.Timber); Writer.Write(Value.Stone); Writer.Write(Value.Marble);
			Writer.Write(Value.Scrap);
		}

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
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
			if (Version != CurrentVersion) throw new InvalidDataException("Unknown realm archive version.");
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
			Jobs = ReadJobs(Reader); LastSliceTick = Reader.ReadInt64();
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
			string failure;
			if (!ValidateEnvelope(out failure)) throw new InvalidDataException(failure);
			if (SeatOpaque != null || AwayOpaque != null || SecededOpaque != null)
				Quarantine("archive contains a future opaque settlement payload");
			else if (!Quarantined && !Validate(out failure)) Quarantine(failure);
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
			SeatOpaque = null; AwayOpaque = null; SecededOpaque = null;
			SeatWireVersion = 0; AwayWireVersion = 0; SecededWireVersion = 0;
			Standings = new Dictionary<string, int>(StringComparer.Ordinal);
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

		private static void WriteString(SerializationWriter Writer, string Value, int MaxChars)
		{
			if (Value == null) { Writer.Write(-1); return; }
			if (Value.Length > MaxChars) throw new InvalidDataException("Realm archive string exceeds cap.");
			byte[] bytes = StrictUtf8.GetBytes(Value);
			if (bytes.Length > MaxTextBytes * 1024) throw new InvalidDataException("Realm archive UTF-8 exceeds cap.");
			Writer.Write(bytes.Length); Writer.Write(bytes, 0, bytes.Length);
		}

		private static string ReadString(SerializationReader Reader, int MaxChars)
		{
			int length = Reader.ReadInt32();
			if (length == -1) return null;
			int maxBytes = Math.Min(MaxTextBytes * 1024, checked(MaxChars * 4));
			if (length < 0 || length > maxBytes) throw new InvalidDataException("Realm archive string length exceeds cap.");
			byte[] bytes = Reader.ReadBytesDirect(length);
			if (bytes.Length != length) throw new EndOfStreamException("Truncated realm archive string.");
			string value = StrictUtf8.GetString(bytes);
			if (value.Length > MaxChars) throw new InvalidDataException("Realm archive decoded string exceeds cap.");
			return value;
		}

		private static void WriteStrings(SerializationWriter Writer, List<string> Values,
			int MaxCount, int MaxChars)
		{
			if (Values == null || Values.Count > MaxCount) throw new InvalidDataException("Realm archive list exceeds cap.");
			Writer.Write(Values.Count);
			for (int i = 0; i < Values.Count; i++) WriteString(Writer, Values[i], MaxChars);
		}

		private static List<string> ReadStrings(SerializationReader Reader, int MaxCount,
			int MaxChars)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > MaxCount) throw new InvalidDataException("Realm archive list count exceeds cap.");
			List<string> values = new List<string>(count);
			for (int i = 0; i < count; i++) values.Add(ReadString(Reader, MaxChars));
			return values;
		}

		private static void WriteArchivedSettlement(SerializationWriter Writer,
			KingdomSettlement Value, byte[] Opaque)
		{
			byte[] payload = Opaque;
			if (payload == null && !KingdomArchivedSettlementCodec.TryEncode(Value,
				out payload, out string failure)) throw new InvalidDataException(failure);
			if (payload == null || payload.Length < 8 ||
				payload.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
				throw new InvalidDataException("Archived settlement payload exceeds cap.");
			Writer.Write(payload.Length);
			Writer.Write(payload, 0, payload.Length);
		}

		private static KingdomSettlement ReadArchivedSettlement(SerializationReader Reader,
			out byte[] Opaque, out int WireVersion)
		{
			Opaque = null;
			WireVersion = 0;
			int length = Reader.ReadInt32();
			if (length < 8 || length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
				throw new InvalidDataException("Archived settlement raw length exceeds cap.");
			byte[] payload = Reader.ReadBytesDirect(length);
			if (payload.Length != length)
				throw new EndOfStreamException("Archived settlement payload is truncated.");
			if (KingdomArchivedSettlementCodec.TryDecode(payload, out KingdomSettlement value,
				out int future, out string failure))
			{
				WireVersion = KingdomArchivedSettlementCodec.CurrentVersion;
				return value;
			}
			if (future > KingdomArchivedSettlementCodec.CurrentVersion)
			{
				Opaque = payload;
				WireVersion = future;
				return null;
			}
			throw new InvalidDataException(failure);
		}

		private static void WriteStandings(SerializationWriter Writer,
			Dictionary<string, int> Value)
		{
			if (!BoundedStandings(Value))
				throw new InvalidDataException("Archived standings exceed cap.");
			List<string> keys = new List<string>(Value.Keys);
			keys.Sort(StringComparer.Ordinal);
			Writer.Write(keys.Count);
			for (int i = 0; i < keys.Count; i++)
			{
				WriteString(Writer, keys[i], 512);
				Writer.Write(Value[keys[i]]);
			}
		}

		private static Dictionary<string, int> ReadStandings(SerializationReader Reader)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > 512)
				throw new InvalidDataException("Archived standings count exceeds cap.");
			Dictionary<string, int> value = new Dictionary<string, int>(count,
				StringComparer.Ordinal);
			string previous = null;
			for (int i = 0; i < count; i++)
			{
				string key = ReadString(Reader, 512);
				if (key == null || (previous != null &&
					string.CompareOrdinal(previous, key) >= 0))
					throw new InvalidDataException("Archived standings order is noncanonical.");
				value.Add(key, Reader.ReadInt32());
				previous = key;
			}
			return value;
		}

		private static void WriteCallback(SerializationWriter Writer,
			KingdomRealmCallbackReceipt Value)
		{
			if (!ValidCallbackEnvelope(Value))
				throw new InvalidDataException("Archived callback receipt exceeds cap.");
			Writer.Write((byte)Value.Phase); Writer.Write((byte)Value.Disposition);
			Writer.Write((byte)Value.Scope);
			WriteString(Writer, Value.BeforeGraph, 64); WriteString(Writer, Value.AfterGraph, 64);
			WriteString(Writer, Value.BeforeArchiveGraph, 64);
			WriteString(Writer, Value.AfterArchiveGraph, 64);
			WriteString(Writer, Value.BeforeEffect, KingdomRealmCallbackReceipt.MaxEffectChars);
			WriteString(Writer, Value.AfterEffect, KingdomRealmCallbackReceipt.MaxEffectChars);
			WriteString(Writer, Value.ObservedEffect, KingdomRealmCallbackReceipt.MaxEffectChars);
			Writer.Write(Value.BeforeStamp); Writer.Write(Value.AfterStamp);
		}

		private static KingdomRealmCallbackReceipt ReadCallback(SerializationReader Reader)
		{
			KingdomRealmCallbackReceipt value = new KingdomRealmCallbackReceipt
			{
				Phase = (KingdomRealmCallbackPhase)Reader.ReadByte(),
				Disposition = (KingdomRealmCallbackDisposition)Reader.ReadByte(),
				Scope = (KingdomRealmCallbackScope)Reader.ReadByte(),
				BeforeGraph = ReadString(Reader, 64),
				AfterGraph = ReadString(Reader, 64),
				BeforeArchiveGraph = ReadString(Reader, 64),
				AfterArchiveGraph = ReadString(Reader, 64),
				BeforeEffect = ReadString(Reader, KingdomRealmCallbackReceipt.MaxEffectChars),
				AfterEffect = ReadString(Reader, KingdomRealmCallbackReceipt.MaxEffectChars),
				ObservedEffect = ReadString(Reader, KingdomRealmCallbackReceipt.MaxEffectChars),
				BeforeStamp = Reader.ReadInt32(),
				AfterStamp = Reader.ReadInt32()
			};
			if (!ValidCallbackEnvelope(value))
				throw new InvalidDataException("Archived callback receipt is malformed.");
			return value;
		}

		private static void WriteBindings(SerializationWriter Writer,
			Simulation.City.KingdomBindingRegistry Value)
		{
			if (!ValidBindings(Value)) throw new InvalidDataException("Invalid archived binding columns.");
			Writer.Write(Value.Keys.Count);
			for (int i = 0; i < Value.Keys.Count; i++)
			{
				Writer.Write(Value.Keys[i]); Writer.Write(Value.Kinds[i]);
				WriteString(Writer, Value.ZoneIds[i], 512); WriteString(Writer, Value.ObjectIds[i], 512);
				Writer.Write(Value.MintedTicks[i]);
			}
		}

		private static void WriteHaul(SerializationWriter Writer, KingdomCarryHaul Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			WriteString(Writer, Value.OriginZoneID, 512);
			Writer.Write(Value.OriginX); Writer.Write(Value.OriginY);
			WriteString(Writer, Value.DestinationSettlementId, 256);
			WriteString(Writer, Value.DestinationSettlementName, 512);
			Writer.Write(Value.PlantedTick); Writer.Write(Value.DueTick);
			Writer.Write(Value.Mud); Writer.Write(Value.Brush); Writer.Write(Value.Timber);
			Writer.Write(Value.Stone); Writer.Write(Value.Marble); Writer.Write(Value.Scrap);
		}

		private static KingdomCarryHaul ReadHaul(SerializationReader Reader)
		{
			byte present = Reader.ReadByte();
			if (present > 1) throw new InvalidDataException(
				"Realm archive haul flag is noncanonical.");
			if (present == 0) return null;
			return new KingdomCarryHaul
			{
				OriginZoneID = ReadString(Reader, 512),
				OriginX = Reader.ReadInt32(),
				OriginY = Reader.ReadInt32(),
				DestinationSettlementId = ReadString(Reader, 256),
				DestinationSettlementName = ReadString(Reader, 512),
				PlantedTick = Reader.ReadInt64(),
				DueTick = Reader.ReadInt64(),
				Mud = Reader.ReadInt32(),
				Brush = Reader.ReadInt32(),
				Timber = Reader.ReadInt32(),
				Stone = Reader.ReadInt32(),
				Marble = Reader.ReadInt32(),
				Scrap = Reader.ReadInt32()
			};
		}

		private static Simulation.City.KingdomBindingRegistry ReadBindings(SerializationReader Reader)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > MaxBindings) throw new InvalidDataException("Archived binding count exceeds cap.");
			Simulation.City.KingdomBindingRegistry value = new Simulation.City.KingdomBindingRegistry();
			for (int i = 0; i < count; i++)
			{
				value.Keys.Add(Reader.ReadInt32()); value.Kinds.Add(Reader.ReadInt32());
				value.ZoneIds.Add(ReadString(Reader, 512)); value.ObjectIds.Add(ReadString(Reader, 512));
				value.MintedTicks.Add(Reader.ReadInt64());
			}
			return value;
		}

		private static void WriteJobs(SerializationWriter Writer,
			Simulation.City.KingdomJobRegistry Value)
		{
			if (!ValidJobs(Value)) throw new InvalidDataException("Invalid archived job columns.");
			Writer.Write(Value.JobCounter); Writer.Write(Value.JobIds.Count);
			for (int i = 0; i < Value.JobIds.Count; i++)
			{
				Writer.Write(Value.JobIds[i]); Writer.Write(Value.Kinds[i]); Writer.Write(Value.Cargos[i]);
				Writer.Write(Value.CargoAmounts[i]); WriteString(Writer, Value.SourceZoneIds[i], 512);
				WriteString(Writer, Value.DestZoneIds[i], 512); Writer.Write(Value.StartTicks[i]);
				Writer.Write(Value.WalkTicksPerCell[i]); Writer.Write(Value.Statuses[i]);
				Writer.Write(Value.OriginCodes[i]); Writer.Write(Value.DepositLegIndexes[i]);
				Writer.Write(Value.LegCounts[i]);
			}
			Writer.Write(Value.LegZoneIds.Count);
			for (int i = 0; i < Value.LegZoneIds.Count; i++)
			{
				WriteString(Writer, Value.LegZoneIds[i], 512); Writer.Write(Value.LegEnterX[i]);
				Writer.Write(Value.LegEnterY[i]); Writer.Write(Value.LegExitX[i]);
				Writer.Write(Value.LegExitY[i]); Writer.Write(Value.LegLengths[i]);
				Writer.Write(Value.LegDepartTicks[i]); Writer.Write(Value.LegArriveTicks[i]);
			}
		}

		private static Simulation.City.KingdomJobRegistry ReadJobs(SerializationReader Reader)
		{
			Simulation.City.KingdomJobRegistry value = new Simulation.City.KingdomJobRegistry();
			value.JobCounter = Reader.ReadInt32();
			int jobs = Reader.ReadInt32();
			if (jobs < 0 || jobs > MaxJobs) throw new InvalidDataException("Archived job count exceeds cap.");
			for (int i = 0; i < jobs; i++)
			{
				value.JobIds.Add(Reader.ReadInt32()); value.Kinds.Add(Reader.ReadInt32());
				value.Cargos.Add(Reader.ReadInt32()); value.CargoAmounts.Add(Reader.ReadInt32());
				value.SourceZoneIds.Add(ReadString(Reader, 512)); value.DestZoneIds.Add(ReadString(Reader, 512));
				value.StartTicks.Add(Reader.ReadInt64()); value.WalkTicksPerCell.Add(Reader.ReadInt32());
				value.Statuses.Add(Reader.ReadInt32()); value.OriginCodes.Add(Reader.ReadInt32());
				value.DepositLegIndexes.Add(Reader.ReadInt32()); value.LegCounts.Add(Reader.ReadInt32());
			}
			int legs = Reader.ReadInt32();
			if (legs < 0 || legs > MaxLegs) throw new InvalidDataException("Archived leg count exceeds cap.");
			for (int i = 0; i < legs; i++)
			{
				value.LegZoneIds.Add(ReadString(Reader, 512)); value.LegEnterX.Add(Reader.ReadInt32());
				value.LegEnterY.Add(Reader.ReadInt32()); value.LegExitX.Add(Reader.ReadInt32());
				value.LegExitY.Add(Reader.ReadInt32()); value.LegLengths.Add(Reader.ReadInt32());
				value.LegDepartTicks.Add(Reader.ReadInt64()); value.LegArriveTicks.Add(Reader.ReadInt64());
			}
			if (!ValidJobs(value)) throw new InvalidDataException("Archived job columns are inconsistent.");
			return value;
		}
#endif
	}
}
