#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>#272: a realm that has not founded its first city must be saveable. The dormant
	/// lifecycle book is written in the growth-free v5 frame; the growth envelope gate stays
	/// strict and never sees the unbound growth book.</summary>
	public class KingdomLifecycleDormantSaveTests
	{
		private const string Txn = "0123456789abcdef0123456789abcdef";
		private const string Refusal = "growth envelope is not bounded and writable";
		private const string BindFault = "lifecycle authority quarantined by an unbound fault";

		// The exact 139-byte v5 image of new KingdomLifecycleBook(); every reader since v0.3.0
		// parses it into the same pristine, foundable book.
		private const string DormantHex =
			"32434C540500000000FFFFFFFF00FFFFFFFFFFFFFFFF00FFFFFFFF01000000000000000000000000"
			+ "00000001000000000000000000000000000000010000000000000000000000000000000100000000"
			+ "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
			+ "00000000000000000000000000000000000000";

		// Reviewed against DormantLifecycleWireExact, PristineGrowthBook,
		// ConstructorDefaultRaidLedger and the v5 frame. A new serialized field fails T9 until
		// that review is repeated and the array updated.
		private static readonly string[] LifecycleFields = { "FormatVersion", "LegacyIdentity",
			"LegacyMigrationKey", "Quarantined", "Fault", "SettlementId", "IdentityBound",
			"IdentityProof", "PlainGuestNextSequence", "PlainGuestRetiredThrough",
			"NotableGuestNextSequence", "NotableGuestRetiredThrough", "RaidNextSequence",
			"RaidRetiredThrough", "PetitionNextSequence", "PetitionRetiredThrough", "LocusOption",
			"LocusOptionTick", "NotableOption", "NotableOptionTick", "RaidOption", "RaidOptionTick",
			"PetitionOption", "PetitionOptionTick", "PlainGuest", "NotableGuest", "Raid", "Petition",
			"Resources", "RecentProofs", "RaidLedger", "Growth" };
		private static readonly string[] GrowthFields = { "FormatVersion", "Quarantined", "Fault",
			"OpaqueWireVersion", "OpaquePayload", "SettlementId", "IdentityBound", "IdentityProof",
			"MigratedFromLifecycleVersion", "MigrationPending", "MigrationTick", "OptionState",
			"OptionTick", "HealthState", "HealthTick", "ScarcityOptionState", "ScarcityOptionTick",
			"WorkPaused", "WorkPauseStartedTick", "WorkPausedTicks", "EffectiveWorkTick",
			"LastHeartbeatTick", "NextArrivalTick", "ArrivalIntervalTicks", "ArrivalEventStreamId",
			"ArrivalRulesVersion", "ArrivalRateEpoch", "ArrivalRateEpochStartedTick",
			"ArrivalProcessedThroughTick", "ArrivalCadenceNextDueTick", "ArrivalRateCohort",
			"ArrivalOrdinalHighWater", "ArrivalOrdinalRetiredThrough", "ArrivalCadenceMigrationPending",
			"ArrivalCadenceResumePending", "ArrivalOpportunity", "ArrivalDebtRanges", "LastFetchTick",
			"LastMillTick", "LastSubsidenceTick", "LastDeliveryTick", "LastDepartureTick",
			"PendingCrop", "PendingCropBlueprint", "PendingCropZoneId", "HeartbeatNextSequence",
			"HeartbeatRetiredThrough", "ArrivalNextSequence", "ArrivalRetiredThrough",
			"DepartureNextSequence", "DepartureRetiredThrough", "DeliveryNextSequence",
			"DeliveryRetiredThrough", "FetchNextSequence", "FetchRetiredThrough", "MillNextSequence",
			"MillRetiredThrough", "ArrivalCandidateNextSequence", "ArrivalCandidateRetiredThrough",
			"HeartbeatOp", "ArrivalOp", "DepartureOp", "DeliveryOp", "FetchOp", "MillOp",
			"ArrivalCandidate", "FirstGuestTerminal", "FieldOps", "CropRows", "Resources",
			"RecentProofs" };
		private static readonly string[] RaidLedgerFields = { "Version", "StateRevision",
			"ScheduleRevision", "Grievances", "Incidents", "ActiveIncidentId",
			"LegacyEvidenceArchived", "LegacyRaidState", "LegacyFaction", "LegacyDueTick",
			"LegacyLastTick", "LegacyTimesDeferred", "OpaqueFuturePayload" };

		private static byte[] Write(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycle(new BinaryWriter(stream), book);
				return stream.ToArray();
			}
		}

		private static KingdomLifecycleBook Read(byte[] bytes)
		{
			KingdomLifecycleBook result = new KingdomLifecycleBook();
			using (MemoryStream stream = new MemoryStream(bytes, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				KingdomLifecycleWireCodec.ReadLifecycle(reader, result);
				ClassicAssert.AreEqual(stream.Length, stream.Position, "no trailing lifecycle bytes");
			}
			return result;
		}

		private static int WireVersion(byte[] bytes) { return BitConverter.ToInt32(bytes, 4); }

		private static void Mint(out string realm, out string settlement)
		{
			KingdomIdentityFault fault;
			ClassicAssert.IsTrue(KingdomIdentityRules.TryMintRealm(Txn, out realm, out fault));
			ClassicAssert.IsTrue(KingdomIdentityRules.TryMintSettlement(realm, Txn, out settlement,
				out fault));
		}

		private static KingdomLifecycleBook Bound(string id)
		{
			KingdomLifecycleBook book = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(book, id, false, null,
				new List<string>()));
			return book;
		}

		private static KingdomLifecycleBook CountersAndOptions(string fault)
		{
			return new KingdomLifecycleBook
			{
				Quarantined = true, Fault = fault, PlainGuestNextSequence = 7L,
				PlainGuestRetiredThrough = 6L, LocusOption = KingdomLifecycleOptionState.Enabled,
				LocusOptionTick = 1200L
			};
		}

		/// <summary>Every state the dormant frame must admit: T1, T4 (two faults) and T6.</summary>
		private static IEnumerable<KeyValuePair<string, KingdomLifecycleBook>> AdmittedStates()
		{
			KingdomLifecycleBook pristine = new KingdomLifecycleBook();
			KingdomLifecycleRules.Normalize(pristine);
			yield return new KeyValuePair<string, KingdomLifecycleBook>("pristine", pristine);
			yield return new KeyValuePair<string, KingdomLifecycleBook>("seat bind fault",
				new KingdomLifecycleBook { Quarantined = true,
					Fault = "lifecycle book could not bind exact seated-city identity" });
			yield return new KeyValuePair<string, KingdomLifecycleBook>("non-seat bind fault",
				new KingdomLifecycleBook { Quarantined = true,
					Fault = "non-seat lifecycle book does not match immutable city identity" });
			yield return new KeyValuePair<string, KingdomLifecycleBook>("counters and options",
				CountersAndOptions(BindFault));
			yield return new KeyValuePair<string, KingdomLifecycleBook>("4096-char fault",
				CountersAndOptions(new string('q', KingdomLifecycleRules.MaxTextChars)));
		}

		private static string[] SerializedFieldNames(Type type)
		{
			List<string> names = new List<string>();
			foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
				if (!Attribute.IsDefined(field, typeof(NonSerializedAttribute))) names.Add(field.Name);
			names.Sort(StringComparer.Ordinal);
			return names.ToArray();
		}

		private static void AssertSameFields(object expected, object actual, string path, int depth)
		{
			if (expected == null || actual == null)
			{
				ClassicAssert.AreEqual(expected == null, actual == null, path);
				return;
			}
			Type type = expected.GetType();
			ClassicAssert.AreEqual(type, actual.GetType(), path);
			if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
			{
				ClassicAssert.AreEqual(expected, actual, path);
				return;
			}
			if (expected is Array array)
			{
				CollectionAssert.AreEqual(array, (Array)actual, path);
				return;
			}
			ClassicAssert.Less(depth, 8, path + " nests too deeply for a dormant book");
			if (expected is IList list)
			{
				IList other = (IList)actual;
				ClassicAssert.AreEqual(list.Count, other.Count, path + ".Count");
				for (int i = 0; i < list.Count; i++)
					AssertSameFields(list[i], other[i], path + "[" + i + "]", depth + 1);
				return;
			}
			foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
				if (!Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
					AssertSameFields(field.GetValue(expected), field.GetValue(actual),
						path + "." + field.Name, depth + 1);
		}

		[Test]
		public void NewGameDormantLifecycleSavesAndColdLoadsPristine()
		{
			// Core/KingdomSystem.z02.State.City.cs:105 field initializer, as every new game has it.
			KingdomLifecycleBook dormant = new KingdomLifecycleBook();
			KingdomLifecycleRules.Normalize(dormant);
			byte[] saved = Write(dormant);
			ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion, WireVersion(saved));
			KingdomLifecycleBook loaded = Read(saved);
			ClassicAssert.IsFalse(loaded.Quarantined);
			ClassicAssert.IsFalse(loaded.WireRejected);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, loaded.FormatVersion);
			ClassicAssert.IsTrue(KingdomLifecycleRules.DormantLifecycleWireExact(loaded));
			CollectionAssert.AreEqual(saved, Write(loaded), "save/load/save is byte-stable");
		}

		[Test]
		public void ColdLoadedDormantBookStillFoundsTheFirstCity()
		{
			KingdomLifecycleBook loaded = Read(Write(new KingdomLifecycleBook()));
			Mint(out string realm, out string settlement);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPrepareFirstIdentityBooks(loaded,
				new KingdomCarryBook(), realm, settlement, out KingdomLifecycleBook founded,
				out KingdomCarryBook carry));
			ClassicAssert.NotNull(carry);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(founded));
			byte[] foundedWire = Write(founded);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, WireVersion(foundedWire),
				"bound authority keeps the current frame and growth section");
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(Read(foundedWire)));
		}

		[Test]
		public void GrowthEnvelopeGateStaysStrictForUnboundGrowth()
		{
			KingdomGrowthBook unbound = new KingdomGrowthBook();
			ClassicAssert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(unbound));
			InvalidDataException refused = Assert.Throws<InvalidDataException>(() =>
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(unbound));
			StringAssert.StartsWith(Refusal, refused.Message);
			ClassicAssert.AreEqual(Refusal + " (identity-unbound)", refused.Message);
		}

		[TestCase("lifecycle book could not bind exact seated-city identity")]
		[TestCase("non-seat lifecycle book does not match immutable city identity")]
		public void UnboundBindQuarantineSavesAndColdLoadsExactly(string fault)
		{
			// Core/KingdomSystem.z06.Identity.Topology.cs:121-123 and z25:62-64 leave this shape.
			KingdomLifecycleBook quarantined = new KingdomLifecycleBook { Quarantined = true, Fault = fault };
			byte[] saved = Write(quarantined);
			ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion, WireVersion(saved));
			KingdomLifecycleBook loaded = Read(saved);
			ClassicAssert.IsTrue(loaded.Quarantined);
			ClassicAssert.AreEqual(fault, loaded.Fault);
			ClassicAssert.IsFalse(loaded.IdentityBound);
			ClassicAssert.IsFalse(loaded.WireRejected);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(loaded));
			CollectionAssert.AreEqual(saved, Write(loaded));
		}

		[Test]
		public void SettlementRowDefaultBookSaves()
		{
			KingdomLifecycleBook row = new KingdomSettlement().LifecycleBook;
			ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion,
				WireVersion(Write(row)));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CanonicalUnboundQuarantineWithCountersAndOptionsRoundTripsExactly(bool longFault)
		{
			string fault = longFault ? new string('q', KingdomLifecycleRules.MaxTextChars) : BindFault;
			KingdomLifecycleBook book = CountersAndOptions(fault);
			ClassicAssert.IsTrue(KingdomLifecycleRules.DormantLifecycleWireExact(book));
			byte[] saved = Write(book);
			ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion, WireVersion(saved));
			KingdomLifecycleBook loaded = Read(saved);
			ClassicAssert.IsTrue(loaded.Quarantined);
			ClassicAssert.AreEqual(fault, loaded.Fault);
			ClassicAssert.AreEqual(7L, loaded.PlainGuestNextSequence);
			ClassicAssert.AreEqual(6L, loaded.PlainGuestRetiredThrough);
			ClassicAssert.AreEqual(KingdomLifecycleOptionState.Enabled, loaded.LocusOption);
			ClassicAssert.AreEqual(1200L, loaded.LocusOptionTick);
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(loaded));
			AssertSameFields(book, loaded, "book", 0);
			CollectionAssert.AreEqual(saved, Write(loaded), "save/load/save is byte-stable");
		}

		[Test]
		public void NonDormantUnboundStateIsNeverRedirectedOrTruncated()
		{
			KingdomLifecycleOperation operation = KingdomLifecycleRules.PrepareOperation(
				Bound("city-dormant-operation"), KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.Spawn, 1L);
			ClassicAssert.NotNull(operation, "production lane-operation fixture");
			Action<string, Action<KingdomLifecycleBook>> refuse = (name, change) =>
			{
				KingdomLifecycleBook book = new KingdomLifecycleBook();
				change(book);
				ClassicAssert.IsFalse(KingdomLifecycleRules.DormantLifecycleWireExact(book), name);
				Assert.Throws<InvalidDataException>(() => Write(book), name);
			};
			refuse("growth clock", b => b.Growth.OptionTick = 1L);
			refuse("growth fault", b => b.Growth.Fault = "x");
			refuse("raid ledger revision", b => b.RaidLedger.StateRevision = 1L);
			refuse("raid ledger future", b => b.RaidLedger.OpaqueFuturePayload = new byte[] { 1 });
			refuse("quarantine without fault", b => b.Quarantined = true);
			refuse("settlement id without binding", b => b.SettlementId = "city");
			refuse("wire rejected", b => b.WireRejected = true);
			refuse("locus option set", b =>
			{
				b.LocusOption = KingdomLifecycleOptionState.Enabled;
				b.LocusOptionTick = 1L;
			});
			refuse("lane operation", b => b.PlainGuest = operation);
			refuse("quarantined dirty raid ledger", b =>
			{
				b.Quarantined = true;
				b.Fault = BindFault;
				b.RaidLedger.StateRevision = 1L;
			});
			refuse("quarantined future raid ledger", b =>
			{
				b.Quarantined = true;
				b.Fault = BindFault;
				b.RaidLedger.OpaqueFuturePayload = new byte[] { 1 };
			});
		}

		[TestCase("quarantined")]
		[TestCase("staged")]
		public void NonPristineGrowthOnAnUnboundQuarantineKeepsTheCurrentFrame(string growth)
		{
			// GrowthAttachmentValid admits these growth shapes under an unbound canonical
			// quarantine, and the v5 reader would rebuild them as pristine growth.
			KingdomLifecycleBook book = new KingdomLifecycleBook { Quarantined = true, Fault = BindFault };
			if (growth == "quarantined")
			{
				book.Growth.Quarantined = true;
				book.Growth.Fault = "growth evidence quarantined";
			}
			else
			{
				book.Growth.MigrationPending = true;
				book.Growth.MigratedFromLifecycleVersion = KingdomLifecycleRules.LegacyLifecycleFormatVersion;
			}
			ClassicAssert.IsTrue(KingdomLifecycleRules.GrowthEnvelopeWritable(book.Growth));
			ClassicAssert.IsFalse(KingdomLifecycleRules.DormantLifecycleWireExact(book));
			byte[] saved = Write(book);
			ClassicAssert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, WireVersion(saved));
			KingdomLifecycleBook loaded = Read(saved);
			AssertSameFields(book, loaded, "book", 0);
			CollectionAssert.AreEqual(saved, Write(loaded), "save/load/save is byte-stable");
		}

		[Test]
		public void DormantFrameIsTheShippedV5ImageOfThePristineBook()
		{
			// Same bytes the historical fixture writer emits, which every reader since v0.3.0 parses.
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV5Fixture(new BinaryWriter(stream),
					new KingdomLifecycleBook());
				CollectionAssert.AreEqual(stream.ToArray(), Write(new KingdomLifecycleBook()));
				ClassicAssert.AreEqual(DormantHex,
					BitConverter.ToString(Write(new KingdomLifecycleBook())).Replace("-", ""));
			}
		}

		[Test]
		public void DormantFrameFieldCensusIsFrozen()
		{
			string review = "review DormantLifecycleWireExact, PristineGrowthBook, "
				+ "ConstructorDefaultRaidLedger and the v5 frame, then update the frozen array";
			string[] lifecycle = (string[])LifecycleFields.Clone();
			string[] growth = (string[])GrowthFields.Clone();
			string[] ledger = (string[])RaidLedgerFields.Clone();
			Array.Sort(lifecycle, StringComparer.Ordinal);
			Array.Sort(growth, StringComparer.Ordinal);
			Array.Sort(ledger, StringComparer.Ordinal);
			CollectionAssert.AreEqual(lifecycle, SerializedFieldNames(typeof(KingdomLifecycleBook)),
				"KingdomLifecycleBook: " + review);
			CollectionAssert.AreEqual(growth, SerializedFieldNames(typeof(KingdomGrowthBook)),
				"KingdomGrowthBook: " + review);
			CollectionAssert.AreEqual(ledger, SerializedFieldNames(typeof(KingdomRaidLedger)),
				"KingdomRaidLedger: " + review);
		}

		[Test]
		public void DormantRoundTripPreservesEveryField()
		{
			int states = 0;
			foreach (KeyValuePair<string, KingdomLifecycleBook> state in AdmittedStates())
			{
				ClassicAssert.IsTrue(KingdomLifecycleRules.DormantLifecycleWireExact(state.Value),
					state.Key);
				byte[] saved = Write(state.Value);
				ClassicAssert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion,
					WireVersion(saved), state.Key);
				AssertSameFields(state.Value, Read(saved), state.Key, 0);
				states++;
			}
			ClassicAssert.AreEqual(5, states);
		}

		[TestCase("pristine-unbound", "identity-unbound")]
		[TestCase("altered-proof", "identity-proof")]
		[TestCase("older-format", "format")]
		[TestCase("field-cap", "collections")]
		[TestCase("lone-surrogate", "root-shape")]
		[TestCase("unknown-option", "root-shape")]
		public void RefusalReasonNamesTheFailingCheck(string shape, string reason)
		{
			KingdomGrowthBook book = shape == "pristine-unbound" || shape == "older-format"
				|| shape == "field-cap" ? new KingdomGrowthBook() : Bound("city-" + shape).Growth;
			ClassicAssert.IsTrue(shape == "pristine-unbound" || shape == "older-format"
				|| shape == "field-cap" || KingdomLifecycleRules.GrowthEnvelopeWritable(book),
				"bound fixture starts writable");
			if (shape == "altered-proof") book.IdentityProof = book.IdentityProof + "0";
			if (shape == "older-format") book.FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion - 1;
			if (shape == "field-cap")
				for (int i = 0; i <= KingdomLifecycleRules.MaxGrowthFields; i++) book.FieldOps.Add(null);
			if (shape == "lone-surrogate")
			{
				book.PendingCrop = 1;
				book.PendingCropBlueprint = "\uD800";
				book.PendingCropZoneId = "zone-a";
			}
			if (shape == "unknown-option") book.OptionState = (KingdomLifecycleOptionState)250;
			ClassicAssert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(book), "verdict unchanged");
			ClassicAssert.AreEqual(reason, KingdomLifecycleRules.GrowthEnvelopeRefusalReason(book));
			InvalidDataException refused = Assert.Throws<InvalidDataException>(() =>
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(book));
			ClassicAssert.AreEqual(Refusal + " (" + reason + ")", refused.Message);
			ClassicAssert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(book), "reason never repairs");
		}
	}
}
#endif
