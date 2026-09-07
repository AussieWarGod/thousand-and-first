#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomTradeRulesTests
	{
		[Test]
		public void PatternCasVerdictAbiIsFrozen()
		{
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomTradePatternCasVerdict",
				typeof(KingdomTradePatternCasVerdict).FullName);
			ClassicAssert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomTradePatternCasVerdict)));
			ClassicAssert.AreEqual(0, (byte)KingdomTradePatternCasVerdict.Invalid);
			ClassicAssert.AreEqual(1, (byte)KingdomTradePatternCasVerdict.Apply);
			ClassicAssert.AreEqual(2, (byte)KingdomTradePatternCasVerdict.AlreadyApplied);
			ClassicAssert.AreEqual(3, (byte)KingdomTradePatternCasVerdict.ThirdValue);
		}

		private static readonly string Realm = new string('a', 64);
		private static readonly string CityA = new string('b', 64);
		private static readonly string CityB = new string('c', 64);
		private const string PatternCity = "taf:settlement:pattern-city";

		private static KingdomTradeBook Book()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { CityB, CityA }, out failure), failure);
			return book;
		}

		private static KingdomTradeOperation ManifestOperation(KingdomTradeBook book,
			KingdomTradeOperationKind kind, long tick = 10L)
		{
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(book, kind, tick);
			ClassicAssert.NotNull(operation);
			operation.ZoneId = "zone-a";
			operation.SettlementId = CityA;
			operation.SettlementName = "City A";
			operation.ManifestId = KingdomTradeRules.ManifestId(
				KingdomTradeRules.OperationId(Realm, 8L));
			operation.OriginId = CityA;
			operation.OriginName = "City A";
			operation.DestinationId = CityB;
			operation.DestinationName = "City B";
			operation.WaterDirection = kind == KingdomTradeOperationKind.ManifestLoad
				? KingdomTradeWaterDirection.Debit
				: (kind == KingdomTradeOperationKind.ManifestDelivery
					? KingdomTradeWaterDirection.Credit : KingdomTradeWaterDirection.None);
			if (kind == KingdomTradeOperationKind.ManifestLoad)
				operation.ManifestId = KingdomTradeRules.ManifestId(operation.Id);
			operation.Outbox = SettledOutbox(operation.Id);
			return operation;
		}

		private static KingdomTradeOperation CharterOperation(KingdomTradeBook book)
		{
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(book,
				KingdomTradeOperationKind.CharterDelivery, 10L);
			ClassicAssert.NotNull(operation);
			operation.ZoneId = "zone-a";
			operation.SettlementId = CityA;
			operation.SettlementName = "City A";
			operation.CharterId = KingdomTradeRules.CharterId(Realm, 1L);
			operation.DealKey = "water";
			operation.DealDisplayName = "Water";
			operation.Faction = "villagers";
			operation.Cycles = 2;
			operation.IncomePerCycle = 3;
			operation.IntervalTicks = 5L;
			operation.DueBefore = 8L;
			operation.DueAfter = 15L;
			operation.CaravanBlueprint = "Camel";
			operation.WaterDirection = KingdomTradeWaterDirection.Credit;
			operation.RequestedWater = 6;
			operation.ProvedWater = 6;
			operation.WaterLegs.Add(new KingdomTradeWaterLeg
			{
				OwnerId = "owner-a", ZoneId = "zone-a", Capacity = 20,
				Before = 4, Delta = 6, After = 10,
				BeforeComposition = "fresh-water", AfterComposition = "fresh-water",
				State = KingdomTradePhysicalState.Proved
			});
			KingdomTradeCharter charter = null;
			for (int i = 0; i < book.Charters.Count; i++)
				if (string.Equals(book.Charters[i]?.Id, operation.CharterId,
					StringComparison.Ordinal)) charter = book.Charters[i];
			if (charter == null)
			{
				charter = new KingdomTradeCharter
				{
					Sequence = 1L, Id = operation.CharterId, DealKey = operation.DealKey,
					Faction = operation.Faction, CreatedTick = 1L, NextTick = operation.DueAfter
				};
				book.Charters.Add(charter);
			}
			book.NextCharterSequence = Math.Max(book.NextCharterSequence, charter.Sequence + 1L);
			operation.Outbox = SettledCharterOutbox(operation);
			return operation;
		}

		private static KingdomTradeOutbox SettledCharterOutbox(KingdomTradeOperation operation)
		{
			return new KingdomTradeOutbox
			{
				EventId = operation.Id,
				Chronicle = "charter chronicle",
				ChronicleState = KingdomTradeSinkState.Delivered,
				LedgerNote = "charter ledger",
				LedgerDeliveredDelta = operation.ProvedWater,
				LedgerState = KingdomTradeSinkState.Delivered,
				Message = "charter message",
				MessageState = KingdomTradeSinkState.Delivered,
				Deed = "charter deed",
				DeedState = KingdomTradeSinkState.Delivered
			};
		}

		private static KingdomTradeOutbox SettledOutbox(string operationId)
		{
			return new KingdomTradeOutbox
			{
				EventId = operationId,
				ChronicleState = KingdomTradeSinkState.Skipped,
				LedgerState = KingdomTradeSinkState.Skipped,
				MessageState = KingdomTradeSinkState.Skipped,
				DeedState = KingdomTradeSinkState.Skipped
			};
		}

		private static void PublishTurnedBackManifest(KingdomTradeBook book,
			KingdomTradeOperation operation, int escrow)
		{
			operation.RequestedWater = escrow;
			book.Manifest = new KingdomTradeManifestState
			{
				OperationSequence = 8L,
				OperationId = KingdomTradeRules.OperationId(Realm, 8L),
				Id = operation.ManifestId,
				OriginId = operation.DestinationId,
				OriginName = operation.DestinationName,
				DestinationId = operation.OriginId,
				DestinationName = operation.OriginName,
				OriginalDrams = escrow,
				EscrowDrams = escrow,
				LoadedTick = operation.CreatedTick,
				DeadlineTick = operation.CreatedTick + 10L,
				TurnedBack = true,
				Status = KingdomTradeManifestStatus.InFlight
			};
		}

		private static KingdomTradeOperation TerminalDelivery(KingdomTradeBook book,
			out KingdomTradeManifestState manifest)
		{
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestDelivery);
			operation.RequestedWater = 25;
			operation.ProvedWater = 25;
			operation.WaterLegs.Add(new KingdomTradeWaterLeg
			{
				OwnerId = "owner-a", ZoneId = "zone-a", Capacity = 50,
				Before = 5, Delta = 25, After = 30,
				BeforeComposition = "fresh-water", AfterComposition = "fresh-water",
				State = KingdomTradePhysicalState.Proved
			});
			operation.ManifestEscrowBefore = 25;
			operation.ManifestEscrowDebit = 25;
			operation.ManifestEscrowAfter = 0;
			operation.ManifestEscrowState = KingdomTradePhysicalState.Proved;
			manifest = new KingdomTradeManifestState
			{
				OperationSequence = 8L,
				OperationId = KingdomTradeRules.OperationId(Realm, 8L),
				Id = operation.ManifestId,
				OriginId = CityA, OriginName = "City A",
				DestinationId = CityB, DestinationName = "City B",
				OriginalDrams = 25, EscrowDrams = 0,
				LoadedTick = 1L, DeadlineTick = 20L,
				Status = KingdomTradeManifestStatus.Delivered
			};
			book.Manifest = manifest;
			operation.Phase = KingdomTradePhase.RetirementReady;
			return operation;
		}

		private static byte[] Envelope(int wire, byte[] payload)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(KingdomTradeCodec.Magic);
				writer.Write(wire);
				writer.Write(payload.Length);
				writer.Write(payload);
				return stream.ToArray();
			}
		}

		private static byte[] PayloadThroughSettlementCount(int count)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(KingdomTradeRules.CurrentFormatVersion);
				writer.Write((byte)KingdomTradeSchemaState.Compatible);
				writer.Write(-1);
				writer.Write(false);
				writer.Write(0);
				writer.Write(-1);
				writer.Write(false);
				writer.Write(count);
				return stream.ToArray();
			}
		}

		private static string ReadRepoSource(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static List<KingdomTradePatternDesign> PatternCandidates()
		{
			return new List<KingdomTradePatternDesign>
			{
				new KingdomTradePatternDesign { BuildingKey = "yd-roof", LearnName = "yd roofline", Label = "Yd freehold roofline" },
				new KingdomTradePatternDesign { BuildingKey = "hindren-hall", LearnName = "hindren weave", Label = "hindren weave-hall" },
				new KingdomTradePatternDesign { BuildingKey = "salt-vault", LearnName = "salt vault", Label = "salt-vault" },
				new KingdomTradePatternDesign { BuildingKey = "glass-court", LearnName = "glass court", Label = "glass court" }
			};
		}

		private static long PatternSequence(bool offered)
		{
			for (long sequence = 1L; sequence < 10000L; sequence++)
				if (KingdomCeremonyRules.ShouldOfferPattern(PatternCity,
					(ulong)sequence) == offered)
					return sequence;
			Assert.Fail("No deterministic pattern sequence found for requested chance branch.");
			return 0L;
		}

		private static KingdomTradePatternReceipt OfferedPattern()
		{
			KingdomTradePatternReceipt receipt = KingdomTradePatternRules.Freeze(PatternCity,
				PatternSequence(true), PatternCandidates());
			ClassicAssert.AreEqual(KingdomTradePatternState.Offered, receipt.State);
			return receipt;
		}

		private static KingdomTradePatternReceipt RoundTripPattern(
			KingdomTradePatternReceipt receipt, bool normalize = false)
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.Pattern = receipt;
			KingdomTradeBook decoded = KingdomTradeCodec.DecodeEnvelope(
				KingdomTradeCodec.EncodeEnvelope(book));
			if (normalize) KingdomTradeRules.Normalize(decoded);
			ClassicAssert.NotNull(decoded.OpenOperation);
			return decoded.OpenOperation.Pattern;
		}

		private static string RawSha256(byte[] Bytes)
		{
			using (SHA256 sha = SHA256.Create())
				return BitConverter.ToString(sha.ComputeHash(Bytes)).Replace("-", "")
					.ToLowerInvariant();
		}

		[Test]
		public void ExactIdentityBind_SortsUpToProductCapAndRejectsChanges()
		{
			KingdomTradeBook book = Book();
			ClassicAssert.AreEqual(CityA, book.SettlementIds[0]);
			ClassicAssert.AreEqual(CityB, book.SettlementIds[1]);
			ClassicAssert.AreEqual(4, KingdomTradeRules.MaxSettlementIds);
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { CityA, CityB }, out failure));
			ClassicAssert.IsFalse(KingdomTradeRules.BindExactIdentity(book, new string('d', 64),
				new[] { CityA, CityB }, out failure));
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
		}

		[Test]
		public void ExactIdentityExpansion_GrowsOneToTwoToFourAndEqualIsIdempotent()
		{
			string cityC = new string('d', 64);
			string cityD = new string('e', 64);
			KingdomTradeBook book = new KingdomTradeBook();
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { CityA }, out failure), failure);
			ClassicAssert.IsTrue(KingdomTradeRules.ExpandExactIdentity(book, Realm,
				new[] { CityB, CityA }, out failure), failure);
			ClassicAssert.IsTrue(KingdomTradeRules.ExpandExactIdentity(book, Realm,
				new[] { cityD, CityB, CityA, cityC }, out failure), failure);
			List<string> expanded = book.SettlementIds;
			ClassicAssert.IsTrue(KingdomTradeRules.ExpandExactIdentity(book, Realm,
				new[] { CityA, CityB, cityC, cityD }, out failure), failure);
			ClassicAssert.AreSame(expanded, book.SettlementIds);
			CollectionAssert.AreEqual(new[] { CityA, CityB, cityC, cityD }, book.SettlementIds);
		}

		[Test]
		public void ExactIdentityExpansion_AllowsSettledManifestAuthority()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { CityA }, out failure));
			book.Manifest = new KingdomTradeManifestState
			{
				OperationSequence = 8L,
				OperationId = KingdomTradeRules.OperationId(Realm, 8L),
				Id = KingdomTradeRules.ManifestId(KingdomTradeRules.OperationId(Realm, 8L)),
				OriginId = CityA, OriginName = "City A", DestinationId = CityA,
				DestinationName = "City A", OriginalDrams = 10, EscrowDrams = 10,
				Status = KingdomTradeManifestStatus.InFlight
			};
			ClassicAssert.IsTrue(KingdomTradeRules.ExpandExactIdentity(book, Realm,
				new[] { CityA, CityB }, out failure), failure);
			ClassicAssert.NotNull(book.Manifest);
			ClassicAssert.AreEqual(2, book.SettlementIds.Count);
		}

		[Test]
		public void ExactIdentityExpansion_BusyReceiptDefersWithoutMutationOrQuarantine()
		{
			KingdomTradeBook operationBook = new KingdomTradeBook();
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(operationBook, Realm,
				new[] { CityA }, out failure));
			KingdomTradeRules.NewOperation(operationBook,
				KingdomTradeOperationKind.ManifestTurnback, 1L);
			List<string> original = operationBook.SettlementIds;
			ClassicAssert.IsFalse(KingdomTradeRules.ExpandExactIdentity(operationBook, Realm,
				new[] { CityA, CityB }, out failure));
			ClassicAssert.AreSame(original, operationBook.SettlementIds);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Compatible, operationBook.SchemaState);

			KingdomTradeBook pendingBook = new KingdomTradeBook();
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(pendingBook, Realm,
				new[] { CityA }, out failure));
			pendingBook.PendingRetirement = new KingdomTradeProof();
			original = pendingBook.SettlementIds;
			ClassicAssert.IsFalse(KingdomTradeRules.ExpandExactIdentity(pendingBook, Realm,
				new[] { CityA, CityB }, out failure));
			ClassicAssert.AreSame(original, pendingBook.SettlementIds);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Compatible, pendingBook.SchemaState);
		}

		[Test]
		public void ExactIdentityExpansion_ImmutableViolationsQuarantineWithoutReplacement()
		{
			string failure;
			KingdomTradeBook removal = Book();
			List<string> original = removal.SettlementIds;
			ClassicAssert.IsFalse(KingdomTradeRules.ExpandExactIdentity(removal, Realm,
				new[] { CityA }, out failure));
			ClassicAssert.AreSame(original, removal.SettlementIds);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Quarantined, removal.SchemaState);

			KingdomTradeBook replacement = Book();
			original = replacement.SettlementIds;
			ClassicAssert.IsFalse(KingdomTradeRules.ExpandExactIdentity(replacement, Realm,
				new[] { CityA, new string('d', 64) }, out failure));
			ClassicAssert.AreSame(original, replacement.SettlementIds);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Quarantined, replacement.SchemaState);

			KingdomTradeBook wrongRealm = Book();
			original = wrongRealm.SettlementIds;
			ClassicAssert.IsFalse(KingdomTradeRules.ExpandExactIdentity(wrongRealm,
				new string('e', 64), new[] { CityA, CityB }, out failure));
			ClassicAssert.AreSame(original, wrongRealm.SettlementIds);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Quarantined, wrongRealm.SchemaState);
		}

		[Test]
		public void ExactIdentityExpansion_InvalidCandidateNeverPartiallyMutates()
		{
			string cityC = new string('d', 64);
			string cityD = new string('e', 64);
			string cityE = new string('f', 64);
			foreach (string[] invalid in new[]
			{
				new[] { CityA, CityA },
				new[] { CityA, "" },
				new[] { CityA, CityB, cityC, cityD, cityE }
			})
			{
				KingdomTradeBook book = new KingdomTradeBook();
				string failure;
				ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
					new[] { CityA }, out failure));
				List<string> original = book.SettlementIds;
				ClassicAssert.IsFalse(KingdomTradeRules.ExpandExactIdentity(book, Realm,
					invalid, out failure));
				ClassicAssert.AreSame(original, book.SettlementIds);
				ClassicAssert.AreEqual(KingdomTradeSchemaState.Compatible, book.SchemaState);
			}
		}

		[Test]
		public void UnboundEvidence_CannotBePromotedToLiveAuthority()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			book.Charters.Add(new KingdomTradeCharter { Sequence = 1L, Id = "legacy-name-id" });
			string failure;
			ClassicAssert.IsFalse(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { CityA }, out failure));
			ClassicAssert.IsFalse(book.IdentityBound);
			ClassicAssert.AreEqual("legacy-name-id", book.Charters[0].Id);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
		}

		[Test]
		public void StableIds_AreSha256DomainSeparatedAndLengthPrefixed()
		{
			string operation = KingdomTradeRules.OperationId(Realm, 7L);
			ClassicAssert.AreEqual(operation, KingdomTradeRules.OperationId(Realm, 7L));
			ClassicAssert.AreNotEqual(operation, KingdomTradeRules.CharterId(Realm, 7L));
			ClassicAssert.AreNotEqual(operation, KingdomTradeRules.OperationId(Realm, 8L));
			ClassicAssert.AreEqual(64, operation.Length);
			StringAssert.IsMatch("^[0-9a-f]{64}$", operation);
			ClassicAssert.AreNotEqual(
				KingdomTradeRules.LegacyCharterId(Realm, "a|b", "c", 0),
				KingdomTradeRules.LegacyCharterId(Realm, "a", "b|c", 0));
		}

		[Test]
		public void Codec_RoundTripsEveryPersistedOptionAndAccountingField()
		{
			KingdomTradeBook book = Book();
			book.OptionState = KingdomTradeOptionState.Enabled;
			book.OptionObservedTick = 99L;
			book.OptionEpoch = 7L;
			book.RestampPending = true;
			book.RetainedEscrowDrams = 44L;
			byte[] encoded = KingdomTradeCodec.EncodeEnvelope(book);
			KingdomTradeBook decoded = KingdomTradeCodec.DecodeEnvelope(encoded);
			ClassicAssert.IsTrue(KingdomTradeRules.BookUsable(decoded));
			ClassicAssert.AreEqual(99L, decoded.OptionObservedTick);
			ClassicAssert.AreEqual(7L, decoded.OptionEpoch);
			ClassicAssert.IsTrue(decoded.RestampPending);
			ClassicAssert.AreEqual(44L, decoded.RetainedEscrowDrams);
			CollectionAssert.AreEqual(book.SettlementIds, decoded.SettlementIds);
		}

		[Test]
		public void Codec_RejectsOversizedStringBeforeReadingItsBytes()
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(KingdomTradeRules.CurrentFormatVersion);
				writer.Write((byte)KingdomTradeSchemaState.Compatible);
				writer.Write(KingdomTradeCodec.MaxStringBytes + 1);
				byte[] envelope = Envelope(KingdomTradeCodec.CurrentWireVersion, stream.ToArray());
				Assert.Throws<InvalidDataException>(() => KingdomTradeCodec.DecodeEnvelope(envelope));
			}
		}

		[Test]
		public void Codec_RejectsHostileCountBeforeListAllocation()
		{
			byte[] envelope = Envelope(KingdomTradeCodec.CurrentWireVersion,
				PayloadThroughSettlementCount(int.MaxValue));
			Assert.Throws<InvalidDataException>(() => KingdomTradeCodec.DecodeEnvelope(envelope));
		}

		[Test]
		public void Codec_RefusesMissingEvidenceListInsteadOfLaunderingItEmpty()
		{
			KingdomTradeBook book = Book();
			book.Charters = null;
			Assert.Throws<InvalidDataException>(() => KingdomTradeCodec.EncodeEnvelope(book));
		}

		[Test]
		public void Codec_RejectsBadEnvelopeLengthsAndPreReleaseNamedGraph()
		{
			Assert.Throws<InvalidDataException>(() => KingdomTradeCodec.DecodeEnvelope(new byte[11]));
			byte[] mismatch = Envelope(KingdomTradeCodec.CurrentWireVersion, new byte[] { 1 });
			mismatch[8] = 2;
			Assert.Throws<InvalidDataException>(() => KingdomTradeCodec.DecodeEnvelope(mismatch));
			byte[] old = (byte[])mismatch.Clone();
			old[0] = 0;
			Assert.Throws<InvalidDataException>(() => KingdomTradeCodec.DecodeEnvelope(old));
		}

		[Test]
		public void Codec_FutureWireIsOpaqueNonAuthorityAndReencodesByteExactly()
		{
			byte[] original = Envelope(99, new byte[] { 0, 1, 2, 255 });
			KingdomTradeBook book = KingdomTradeCodec.DecodeEnvelope(original);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Unknown, book.SchemaState);
			ClassicAssert.IsFalse(KingdomTradeRules.BookUsable(book));
			CollectionAssert.AreEqual(original, KingdomTradeCodec.EncodeEnvelope(book));
		}

		[Test]
		public void Codec_UnrecognizedPriorBoundedWireStaysOpaqueButUnsafeV1IsRefused()
		{
			byte[] prior = Envelope(KingdomTradeCodec.PriorWireVersion - 1,
				new byte[] { 4, 3, 2, 1 });
			KingdomTradeBook book = KingdomTradeCodec.DecodeEnvelope(prior);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Unknown, book.SchemaState);
			ClassicAssert.IsFalse(KingdomTradeRules.BookUsable(book));
			CollectionAssert.AreEqual(prior, KingdomTradeCodec.EncodeEnvelope(book));
			Assert.Throws<InvalidDataException>(() => KingdomTradeCodec.DecodeEnvelope(
				Envelope(1, new byte[] { 0 })));
		}

		[Test]
		public void Codec_OldKnownSchemaPreservesEvidenceButNeverAuthorizesIt()
		{
			KingdomTradeBook book = Book();
			book.FormatVersion = 2;
			book.SchemaFault = "old evidence";
			KingdomTradeBook decoded = KingdomTradeCodec.DecodeEnvelope(
				KingdomTradeCodec.EncodeEnvelope(book));
			ClassicAssert.AreEqual(2, decoded.FormatVersion);
			ClassicAssert.AreEqual("old evidence", decoded.SchemaFault);
			ClassicAssert.IsFalse(KingdomTradeRules.BookUsable(decoded));
		}

		[Test]
		public void PatternFreeze_NoCandidatesAndChanceMissAreDistinctTerminalReceipts()
		{
			KingdomTradePatternReceipt empty = KingdomTradePatternRules.Freeze(CityA, 1L,
				new KingdomTradePatternDesign[0]);
			ClassicAssert.AreEqual(KingdomTradePatternState.NoCandidates, empty.State);
			ClassicAssert.IsTrue(KingdomTradePatternRules.Terminal(empty));

			KingdomTradePatternReceipt miss = KingdomTradePatternRules.Freeze(PatternCity,
				PatternSequence(false), PatternCandidates());
			ClassicAssert.AreEqual(KingdomTradePatternState.ChanceMiss, miss.State);
			ClassicAssert.IsTrue(KingdomTradePatternRules.Terminal(miss));
		}

		[Test]
		public void PatternFreeze_OperationSequenceDeterministicallyPicksUpToThreeDetachedRows()
		{
			long sequence = PatternSequence(true);
			List<KingdomTradePatternDesign> source = PatternCandidates();
			KingdomTradePatternReceipt first = KingdomTradePatternRules.Freeze(PatternCity,
				sequence, source);
			KingdomTradePatternReceipt second = KingdomTradePatternRules.Freeze(PatternCity,
				sequence, PatternCandidates());
			ClassicAssert.AreEqual(3, first.Offers.Count);
			for (int i = 0; i < first.Offers.Count; i++)
			{
				ClassicAssert.AreEqual(first.Offers[i].BuildingKey, second.Offers[i].BuildingKey);
				ClassicAssert.AreEqual(first.Offers[i].LearnName, second.Offers[i].LearnName);
				ClassicAssert.AreEqual(first.Offers[i].Label, second.Offers[i].Label);
			}
			string frozenKey = first.Offers[0].BuildingKey;
			string frozenLabel = first.Offers[0].Label;
			source[0].BuildingKey = "catalogue-drift";
			source[0].Label = "catalogue drift";
			ClassicAssert.AreEqual(frozenKey, first.Offers[0].BuildingKey);
			ClassicAssert.AreEqual(frozenLabel, first.Offers[0].Label);
		}

		[Test]
		public void PatternChoice_CancelPersistsWithoutRosterOrSinkMutation()
		{
			KingdomTradePatternReceipt receipt = OfferedPattern();
			ClassicAssert.IsTrue(KingdomTradePatternRules.BeginChoice(receipt));
			ClassicAssert.IsTrue(KingdomTradePatternRules.Decline(receipt));
			ClassicAssert.AreEqual(KingdomTradePatternState.Declined, receipt.State);
			ClassicAssert.IsTrue(KingdomTradePatternRules.Terminal(receipt));
			KingdomTradePatternReceipt reloaded = RoundTripPattern(receipt, true);
			ClassicAssert.AreEqual(KingdomTradePatternState.Declined, reloaded.State);
			ClassicAssert.IsTrue(KingdomTradePatternRules.Terminal(reloaded));
		}

		[Test]
		public void PatternChoice_ExactBeforeAfterCasSurvivesEveryMutationCut()
		{
			KingdomTradePatternReceipt receipt = OfferedPattern();
			ClassicAssert.IsTrue(KingdomTradePatternRules.BeginChoice(receipt));
			string failure;
			ClassicAssert.IsTrue(KingdomTradePatternRules.TrySelect(receipt, 0,
				"disk:water", "City A", out failure), failure);
			ClassicAssert.AreEqual(KingdomTradePatternState.Selected, receipt.State);
			ClassicAssert.AreEqual(KingdomTradePatternCasVerdict.Apply,
				KingdomTradePatternRules.InspectRoster(receipt, receipt.RosterBefore));

			KingdomTradePatternReceipt selected = RoundTripPattern(receipt, true);
			ClassicAssert.AreEqual(KingdomTradePatternState.Selected, selected.State);
			ClassicAssert.IsTrue(KingdomTradePatternRules.MarkRosterIntent(selected));
			KingdomTradePatternReceipt intent = RoundTripPattern(selected, true);
			ClassicAssert.AreEqual(KingdomTradePatternState.RosterIntent, intent.State);
			ClassicAssert.AreEqual(KingdomTradePatternCasVerdict.Apply,
				KingdomTradePatternRules.InspectRoster(intent, intent.RosterBefore));
			ClassicAssert.AreEqual(KingdomTradePatternCasVerdict.AlreadyApplied,
				KingdomTradePatternRules.InspectRoster(intent, intent.RosterAfter));
			ClassicAssert.IsTrue(KingdomTradePatternRules.MarkLearned(intent));
			intent.ChronicleState = KingdomTradeSinkState.Delivered;
			intent.MessageState = KingdomTradeSinkState.Delivered;
			KingdomTradePatternReceipt learned = RoundTripPattern(intent, true);
			ClassicAssert.AreEqual(KingdomTradePatternState.Learned, learned.State);
			ClassicAssert.IsTrue(KingdomTradePatternRules.Terminal(learned));
		}

		[Test]
		public void PatternChoice_AlreadyKnownAndCasThirdValueNeverOverwrite()
		{
			KingdomTradePatternReceipt known = OfferedPattern();
			string selectedKey = KingdomZoningRules.ComposeKey(
				KingdomCeremonyRules.PatternKnowledgeKind, known.Offers[0].LearnName);
			string failure;
			ClassicAssert.IsTrue(KingdomTradePatternRules.TrySelect(known, 0, selectedKey,
				"City A", out failure), failure);
			ClassicAssert.AreEqual(KingdomTradePatternState.AlreadyKnown, known.State);
			ClassicAssert.IsTrue(KingdomTradePatternRules.Terminal(known));

			KingdomTradePatternReceipt conflict = OfferedPattern();
			ClassicAssert.IsTrue(KingdomTradePatternRules.TrySelect(conflict, 0, "disk:water",
				"City A", out failure), failure);
			ClassicAssert.AreEqual(KingdomTradePatternCasVerdict.ThirdValue,
				KingdomTradePatternRules.InspectRoster(conflict, "disk:water|disk:third"));
			KingdomTradePatternRules.MarkConflict(conflict, "third value");
			ClassicAssert.IsTrue(KingdomTradePatternRules.Terminal(conflict));
		}

		[Test]
		public void PatternReload_ChoiceAndSinkIntentUseTheirExactReplayPolicies()
		{
			KingdomTradePatternReceipt choice = OfferedPattern();
			ClassicAssert.IsTrue(KingdomTradePatternRules.BeginChoice(choice));
			choice = RoundTripPattern(choice, true);
			ClassicAssert.AreEqual(KingdomTradePatternState.Offered, choice.State,
				"UI intent has no durable effect and is safely re-offered");

			string failure;
			ClassicAssert.IsTrue(KingdomTradePatternRules.TrySelect(choice, 0, "", "City A",
				out failure), failure);
			ClassicAssert.IsTrue(KingdomTradePatternRules.MarkRosterIntent(choice));
			ClassicAssert.IsTrue(KingdomTradePatternRules.MarkLearned(choice));
			choice.ChronicleState = KingdomTradeSinkState.Intent;
			choice.MessageState = KingdomTradeSinkState.Intent;
			choice = RoundTripPattern(choice, true);
			ClassicAssert.AreEqual(KingdomTradeSinkState.Intent, choice.ChronicleState,
				"RecordOnce owns retry/reconciliation");
			ClassicAssert.AreEqual(KingdomTradeSinkState.Lost, choice.MessageState,
				"uninspectable player messages never repeat after an intent cut");
		}

		[Test]
		public void PatternCodec_RejectsHostileStateCountAndAlreadyKnownProof()
		{
			KingdomTradePatternReceipt offered = OfferedPattern();
			byte[] exact = KingdomTradeCodec.EncodePatternFixture(offered);
			byte[] badState = (byte[])exact.Clone();
			badState[0] = byte.MaxValue;
			Assert.Throws<InvalidDataException>(() =>
				KingdomTradeCodec.DecodePatternFixture(badState));

			byte[] badCount = (byte[])exact.Clone();
			Buffer.BlockCopy(BitConverter.GetBytes(KingdomTradePatternRules.MaxOffers + 1),
				0, badCount, 1, 4);
			Assert.Throws<InvalidDataException>(() =>
				KingdomTradeCodec.DecodePatternFixture(badCount));

			offered.Offers.Add(new KingdomTradePatternDesign
				{ BuildingKey = "fourth", LearnName = "fourth", Label = "fourth" });
			Assert.Throws<InvalidDataException>(() =>
				KingdomTradeCodec.EncodePatternFixture(offered));

			KingdomTradePatternReceipt known = OfferedPattern();
			string key = KingdomZoningRules.ComposeKey(
				KingdomCeremonyRules.PatternKnowledgeKind, known.Offers[0].LearnName);
			string failure;
			ClassicAssert.IsTrue(KingdomTradePatternRules.TrySelect(known, 0, key,
				"City A", out failure), failure);
			known.RosterBefore = known.RosterAfter = "";
			ClassicAssert.IsFalse(KingdomTradePatternRules.Valid(known),
				"AlreadyKnown must prove the selected exact key is in the frozen roster");
		}

		[Test]
		public void Codec_WireV3FixtureMigratesToFormat5AndCurrentRewrite()
		{
			KingdomTradeBook prior = Book();
			prior.FormatVersion = 4;
			byte[] wire3 = KingdomTradeCodec.EncodeEnvelopeV3Fixture(prior);
			ClassicAssert.AreEqual(KingdomTradeCodec.PriorWireVersion,
				BitConverter.ToInt32(wire3, 4));
			KingdomTradeBook migrated = KingdomTradeCodec.DecodeEnvelope(wire3);
			ClassicAssert.AreEqual(KingdomTradeRules.CurrentFormatVersion, migrated.FormatVersion);
			ClassicAssert.IsTrue(KingdomTradeRules.BookUsable(migrated));
			byte[] current = KingdomTradeCodec.EncodeEnvelope(migrated);
			ClassicAssert.AreEqual(KingdomTradeCodec.CurrentWireVersion,
				BitConverter.ToInt32(current, 4));
			KingdomTradeBook again = KingdomTradeCodec.DecodeEnvelope(current);
			ClassicAssert.IsTrue(KingdomTradeRules.BookUsable(again));
		}

		[Test]
		public void Codec_WireV3FixtureHasImmutableIndependentGolden()
		{
			KingdomTradeBook prior = Book();
			prior.FormatVersion = 4;
			byte[] wire3 = KingdomTradeCodec.EncodeEnvelopeV3Fixture(prior);
			ClassicAssert.AreEqual(
				"328:21ec3e3bb3b41346e72e9509f9452aa0b09b663c1a01acff188d6c013009f280",
				wire3.Length + ":" + RawSha256(wire3));
		}

		[Test]
		public void Codec_ProductionWriterCannotRouteThroughFrozenV3Writer()
		{
			string source = KingdomTradeStateLogicalSource.Read();
			int current = source.IndexOf("public static byte[] EncodePayload(",
				StringComparison.Ordinal);
			int prior = source.IndexOf("internal static byte[] EncodePayloadV3ForMigration(",
				current, StringComparison.Ordinal);
			string currentWriter = source.Substring(current, prior - current);
			StringAssert.Contains("WriteOperation)", currentWriter);
			ClassicAssert.IsFalse(currentWriter.Contains("WriteOperationV3"));
		}

		[Test]
		public void Codec_WireV3OpenCharterDefaultsPatternLaneWithoutReroll()
		{
			KingdomTradeBook prior = Book();
			KingdomTradeOperation operation = CharterOperation(prior);
			operation.Pattern = null;
			prior.FormatVersion = 4;
			KingdomTradeBook migrated = KingdomTradeCodec.DecodeEnvelope(
				KingdomTradeCodec.EncodeEnvelopeV3Fixture(prior));
			ClassicAssert.NotNull(migrated.OpenOperation.Pattern);
			ClassicAssert.AreEqual(KingdomTradePatternState.None,
				migrated.OpenOperation.Pattern.State);
			ClassicAssert.IsTrue(KingdomTradePatternRules.Terminal(
				migrated.OpenOperation.Pattern));
		}

		[Test]
		public void AuthoritySeal_RejectsDueAfterAndLedgerNoteMutation()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.Outbox = new KingdomTradeOutbox
			{
				EventId = operation.Id, LedgerNote = "before",
				ChronicleState = KingdomTradeSinkState.Skipped,
				LedgerState = KingdomTradeSinkState.Pending,
				MessageState = KingdomTradeSinkState.Skipped,
				DeedState = KingdomTradeSinkState.Skipped
			};
			List<string> claimed = new List<string> { "zone-a" };
			List<string> city = new List<string> { "zone-a" };
			KingdomTradeAuthoritySeal seal = KingdomTradeRules.CaptureAuthoritySeal(book, claimed, city);
			ClassicAssert.IsTrue(KingdomTradeRules.ExactAuthoritySeal(book, claimed, city, seal));
			operation.DueAfter++;
			ClassicAssert.IsFalse(KingdomTradeRules.ExactAuthoritySeal(book, claimed, city, seal));
			operation.DueAfter--;
			seal = KingdomTradeRules.CaptureAuthoritySeal(book, claimed, city);
			operation.Outbox.LedgerNote = "hostile";
			ClassicAssert.IsFalse(KingdomTradeRules.ExactAuthoritySeal(book, claimed, city, seal));
		}

		[Test]
		public void AuthoritySeal_RejectsZoneReplacementAndContentMutation()
		{
			KingdomTradeBook book = Book();
			List<string> claimed = new List<string> { "zone-a" };
			List<string> city = new List<string> { "zone-a" };
			KingdomTradeAuthoritySeal seal = KingdomTradeRules.CaptureAuthoritySeal(book, claimed, city);
			ClassicAssert.IsFalse(KingdomTradeRules.ExactAuthoritySeal(book, claimed,
				new List<string> { "zone-a" }, seal));
			seal = KingdomTradeRules.CaptureAuthoritySeal(book, claimed, city);
			city[0] = "zone-b";
			ClassicAssert.IsFalse(KingdomTradeRules.ExactAuthoritySeal(book, claimed, city, seal));
		}

		[Test]
		public void ManifestSnapshot_IsDetachedFromPersistedAuthority()
		{
			KingdomTradeManifestState authority = new KingdomTradeManifestState
			{
				OperationId = "operation", Id = "manifest", EscrowDrams = 20,
				OriginId = CityA, DestinationId = CityB, Status = KingdomTradeManifestStatus.InFlight
			};
			KingdomTradeManifestState snapshot = KingdomTradeRules.SnapshotManifest(authority);
			ClassicAssert.AreNotSame(authority, snapshot);
			snapshot.EscrowDrams = 0;
			snapshot.OriginId = CityB;
			ClassicAssert.AreEqual(20, authority.EscrowDrams);
			ClassicAssert.AreEqual(CityA, authority.OriginId);
		}

		[Test]
		public void ManifestCredit_ReconcilesExactlyOnceAndRejectsUnknownCurrentValue()
		{
			int after;
			bool apply;
			ClassicAssert.IsTrue(KingdomTradeRules.TryReconcileEscrow(100, 60, 100, out after, out apply));
			ClassicAssert.AreEqual(40, after);
			ClassicAssert.IsTrue(apply);
			ClassicAssert.IsTrue(KingdomTradeRules.TryReconcileEscrow(100, 60, 40, out after, out apply));
			ClassicAssert.IsFalse(apply);
			ClassicAssert.IsFalse(KingdomTradeRules.TryReconcileEscrow(100, 60, 70, out after, out apply));
		}

		[Test]
		public void RetainedEscrow_ReconcilesExactlyOnceAndRejectsOverflowAlias()
		{
			long after;
			bool apply;
			ClassicAssert.IsTrue(KingdomTradeRules.TryReconcileRetained(20L, 15L, 20L, out after, out apply));
			ClassicAssert.AreEqual(35L, after);
			ClassicAssert.IsTrue(apply);
			ClassicAssert.IsTrue(KingdomTradeRules.TryReconcileRetained(20L, 15L, 35L, out after, out apply));
			ClassicAssert.IsFalse(apply);
			ClassicAssert.IsFalse(KingdomTradeRules.TryReconcileRetained(20L, 15L, 30L, out after, out apply));
			ClassicAssert.IsFalse(KingdomTradeRules.TryReconcileRetained(long.MaxValue, 1L,
				long.MaxValue, out after, out apply), "saturation aliases before and after");
		}

		[Test]
		public void RetirementProof_RetainsEverySinkAndAccountingDisposition()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestDelivery);
			operation.RequestedWater = 60;
			operation.ProvedWater = 25;
			operation.ManifestEscrowBefore = 60;
			operation.ManifestEscrowDebit = 25;
			operation.ManifestEscrowAfter = 35;
			operation.ManifestEscrowState = KingdomTradePhysicalState.Proved;
			operation.WaterLegs.Add(new KingdomTradeWaterLeg
			{
				OwnerId = "owner-a", ZoneId = "zone-a", Capacity = 50,
				Before = 5, Delta = 25, After = 30,
				BeforeComposition = "fresh-water", AfterComposition = "fresh-water",
				State = KingdomTradePhysicalState.Proved
			});
			book.Manifest = new KingdomTradeManifestState
			{
				Id = operation.ManifestId, EscrowDrams = 35
			};
			operation.Outbox = new KingdomTradeOutbox
			{
				EventId = operation.Id,
				ChronicleState = KingdomTradeSinkState.Delivered,
				LedgerState = KingdomTradeSinkState.Skipped,
				MessageState = KingdomTradeSinkState.Delivered,
				DeedState = KingdomTradeSinkState.Skipped
			};
			operation.Phase = KingdomTradePhase.RetirementReady;
			ClassicAssert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, "closed"));
			KingdomTradeProof proof = book.RecentProofs[0];
			ClassicAssert.AreEqual(60, proof.ManifestEscrowBefore);
			ClassicAssert.AreEqual(25, proof.ManifestEscrowDebit);
			ClassicAssert.AreEqual(35, proof.ManifestEscrowAfter);
			ClassicAssert.AreEqual(KingdomTradePhysicalState.Proved, proof.ManifestEscrowState);
			ClassicAssert.AreEqual(0L, proof.RetainedBefore);
			ClassicAssert.AreEqual(0L, proof.RetainedDelta);
			ClassicAssert.AreEqual(0L, proof.RetainedAfter);
			ClassicAssert.AreEqual(KingdomTradePhysicalState.None, proof.RetainedState);
			ClassicAssert.AreEqual(KingdomTradeSinkState.Delivered, proof.ChronicleState);
			ClassicAssert.AreEqual(KingdomTradeSinkState.Skipped, proof.LedgerState);
			ClassicAssert.AreEqual(KingdomTradeSinkState.Delivered, proof.MessageState);
			ClassicAssert.AreEqual(KingdomTradeSinkState.Skipped, proof.DeedState);
		}

		[Test]
		public void LapseRetirementProof_RetainsExactEscrowTransferDisposition()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestLapse);
			PublishTurnedBackManifest(book, operation, 12);
			operation.RetainedBefore = 5L;
			operation.RetainedDelta = 12L;
			operation.RetainedAfter = 17L;
			operation.RetainedState = KingdomTradePhysicalState.Proved;
			book.RetainedEscrowDrams = 17L;
			book.Manifest.Status = KingdomTradeManifestStatus.Quarantined;
			operation.Phase = KingdomTradePhase.RetirementReady;
			ClassicAssert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = book.RecentProofs[0];
			ClassicAssert.AreEqual(5L, proof.RetainedBefore);
			ClassicAssert.AreEqual(12L, proof.RetainedDelta);
			ClassicAssert.AreEqual(17L, proof.RetainedAfter);
			ClassicAssert.AreEqual(KingdomTradePhysicalState.Proved, proof.RetainedState);
		}

		[Test]
		public void LostEffect_NeverRetiresOrErasesOpenReceipt()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestDelivery);
			operation.ManifestEscrowBefore = 60;
			operation.ManifestEscrowDebit = 25;
			operation.ManifestEscrowAfter = 35;
			operation.ManifestEscrowState = KingdomTradePhysicalState.Lost;
			operation.Phase = KingdomTradePhase.Quarantined;
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Quarantined, 20L, "uncertain credit"));
			ClassicAssert.AreSame(operation, book.OpenOperation);
			ClassicAssert.IsNull(book.PendingRetirement);
			ClassicAssert.AreEqual(0, book.RecentProofs.Count);
			ClassicAssert.AreEqual(KingdomTradePhase.Quarantined, operation.Phase);
		}

		[Test]
		public void MismatchedPhysicalCreditAndEscrowDebit_CannotRetire()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestDelivery);
			operation.RequestedWater = 60;
			operation.ProvedWater = 25;
			operation.ManifestEscrowBefore = 60;
			operation.ManifestEscrowDebit = 24;
			operation.ManifestEscrowAfter = 36;
			operation.ManifestEscrowState = KingdomTradePhysicalState.Proved;
			operation.WaterLegs.Add(new KingdomTradeWaterLeg
			{
				Delta = 25, State = KingdomTradePhysicalState.Proved
			});
			book.Manifest = new KingdomTradeManifestState
			{
				Id = operation.ManifestId, EscrowDrams = 36
			};
			operation.Phase = KingdomTradePhase.Quarantined;
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Quarantined, 20L, "mismatch"));
			ClassicAssert.AreSame(operation, book.OpenOperation);
			ClassicAssert.AreEqual(24, operation.ManifestEscrowDebit);
		}

		[Test]
		public void CodecDecode_IsStructuralUntilExplicitNormalizeQuarantinesUnresolvedCredit()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestDelivery);
			operation.RequestedWater = 25;
			operation.ProvedWater = 25;
			operation.WaterLegs.Add(new KingdomTradeWaterLeg
			{
				OwnerId = "owner-a", ZoneId = "zone-a", Capacity = 50,
				Before = 5, Delta = 25, After = 30,
				BeforeComposition = "fresh-water", AfterComposition = "fresh-water",
				State = KingdomTradePhysicalState.Proved
			});
			operation.ManifestEscrowBefore = 60;
			operation.ManifestEscrowDebit = 25;
			operation.ManifestEscrowAfter = 35;
			operation.ManifestEscrowState = KingdomTradePhysicalState.Lost;
			byte[] before = KingdomTradeCodec.EncodePayload(book);
			KingdomTradeBook decoded = KingdomTradeCodec.DecodeEnvelopeRaw(
				KingdomTradeCodec.EncodeEnvelope(book));
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(decoded));
			ClassicAssert.NotNull(decoded.OpenOperation);
			ClassicAssert.AreEqual(25, decoded.OpenOperation.ProvedWater);
			ClassicAssert.AreEqual(25, decoded.OpenOperation.ManifestEscrowDebit);
			ClassicAssert.AreEqual(35, decoded.OpenOperation.ManifestEscrowAfter);
			ClassicAssert.AreEqual(KingdomTradePhysicalState.Lost,
				decoded.OpenOperation.ManifestEscrowState);
			ClassicAssert.AreEqual(KingdomTradePhase.Prepared, decoded.OpenOperation.Phase);
			KingdomTradeRules.Normalize(decoded);
			ClassicAssert.AreEqual(KingdomTradePhase.Quarantined, decoded.OpenOperation.Phase);
		}

		[Test]
		public void CodecDecode_PreservesPartialRetirementUntilExplicitNormalize()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestTurnback);
			PublishTurnedBackManifest(book, operation, 10);
			operation.Phase = KingdomTradePhase.RetirementReady;
			ClassicAssert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = book.RecentProofs[0];
			book.RecentProofs.Clear();
			book.RetiredThrough = 0L;
			book.OpenOperation = operation;
			book.PendingRetirement = proof;
			byte[] before = KingdomTradeCodec.EncodePayload(book);

			KingdomTradeBook decoded = KingdomTradeCodec.DecodeEnvelopeRaw(
				KingdomTradeCodec.EncodeEnvelope(book));
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(decoded));
			ClassicAssert.NotNull(decoded.OpenOperation);
			ClassicAssert.NotNull(decoded.PendingRetirement);
			ClassicAssert.IsEmpty(decoded.RecentProofs);
			ClassicAssert.AreEqual(0L, decoded.RetiredThrough);

			KingdomTradeRules.Normalize(decoded);
			ClassicAssert.IsNull(decoded.OpenOperation);
			ClassicAssert.IsNull(decoded.PendingRetirement);
			ClassicAssert.AreEqual(1L, decoded.RetiredThrough);
			ClassicAssert.AreEqual(1, decoded.RecentProofs.Count);
		}

		[Test]
		public void StructuralTradeDecode_PrecedesCoreLegacyCoexistenceGate()
		{
			string state = KingdomTradeStateLogicalSource.Read();
			int raw = state.IndexOf("public static KingdomTradeBook DecodeEnvelopeRaw",
				StringComparison.Ordinal);
			int rawEnd = state.IndexOf("public static byte[] EncodePayload", raw,
				StringComparison.Ordinal);
			string rawBody = state.Substring(raw, rawEnd - raw);
			ClassicAssert.IsFalse(rawBody.Contains("KingdomTradeRules.Normalize"));
			int read = state.IndexOf("public void Read(SerializationReader Reader)",
				StringComparison.Ordinal);
			int copy = state.IndexOf("CopyFrom(KingdomTradeCodec.DecodeEnvelopeRaw", read,
				StringComparison.Ordinal);
			ClassicAssert.Greater(copy, read);

			string core = KingdomSystemLogicalSource.Read();
			int method = core.IndexOf("private void NormalizeTradeBook()", StringComparison.Ordinal);
			int end = core.IndexOf("\n\t}\n}", method, StringComparison.Ordinal);
			string body = core.Substring(method, end - method);
			int legacy = body.IndexOf("bool hasLegacyTrade", StringComparison.Ordinal);
			int legacyBranch = body.IndexOf("if (hasLegacyTrade)", legacy,
				StringComparison.Ordinal);
			int normalize = body.IndexOf("KingdomTradeRules.Normalize(TradeBook)",
				StringComparison.Ordinal);
			ClassicAssert.Greater(legacyBranch, legacy);
			ClassicAssert.Greater(normalize, legacyBranch);
			StringAssert.Contains("return;", body.Substring(legacyBranch,
				normalize - legacyBranch));
		}

		[Test]
		public void PartialRetirement_CompletesFromExactPendingReceipt()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestTurnback);
			PublishTurnedBackManifest(book, operation, 10);
			operation.Phase = KingdomTradePhase.RetirementReady;
			ClassicAssert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = book.RecentProofs[0];
			book.RecentProofs.Clear();
			book.RetiredThrough = 0L;
			book.OpenOperation = operation;
			book.PendingRetirement = proof;
			KingdomTradeRules.Normalize(book);
			ClassicAssert.IsNull(book.OpenOperation);
			ClassicAssert.IsNull(book.PendingRetirement);
			ClassicAssert.AreEqual(1L, book.RetiredThrough);
			ClassicAssert.AreSame(proof, book.RecentProofs[0]);
		}

		[Test]
		public void WrongRealmPendingRetirement_QuarantinesWithoutErasure()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestTurnback);
			PublishTurnedBackManifest(book, operation, 10);
			operation.Phase = KingdomTradePhase.RetirementReady;
			ClassicAssert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = book.RecentProofs[0];
			book.RecentProofs.Clear();
			book.RetiredThrough = 0L;
			book.OpenOperation = operation;
			book.PendingRetirement = proof;
			proof.RealmId = new string('d', 64);
			proof.Id = KingdomTradeRules.OperationId(proof.RealmId, proof.Sequence);
			KingdomTradeRules.Normalize(book);
			ClassicAssert.AreSame(operation, book.OpenOperation);
			ClassicAssert.AreSame(proof, book.PendingRetirement);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
		}

		[Test]
		public void AlteredPendingRetirement_CannotEraseExactOpenReceipt()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestTurnback);
			PublishTurnedBackManifest(book, operation, 10);
			operation.Phase = KingdomTradePhase.RetirementReady;
			ClassicAssert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = book.RecentProofs[0];
			book.RecentProofs.Clear();
			book.RetiredThrough = 0L;
			book.OpenOperation = operation;
			book.PendingRetirement = proof;
			proof.RequestedWater = 9;
			KingdomTradeRules.Normalize(book);
			ClassicAssert.AreSame(operation, book.OpenOperation);
			ClassicAssert.AreSame(proof, book.PendingRetirement);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
		}

		[Test]
		public void ReloadedSinkIntent_BecomesLostAndCannotRetire()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestTurnback);
			operation.Outbox = new KingdomTradeOutbox
			{
				EventId = operation.Id, LedgerNote = "note",
				ChronicleState = KingdomTradeSinkState.Skipped,
				LedgerState = KingdomTradeSinkState.Intent,
				MessageState = KingdomTradeSinkState.Skipped,
				DeedState = KingdomTradeSinkState.Skipped
			};
			KingdomTradeRules.Normalize(book);
			ClassicAssert.AreEqual(KingdomTradeSinkState.Lost, operation.Outbox.LedgerState);
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Quarantined, 20L, null));
			ClassicAssert.AreSame(operation, book.OpenOperation);
		}

		[Test]
		public void CharterScheduleEquation_TamperQuarantinesWithoutCorrection()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.DueAfter = 16L;
			KingdomTradeRules.Normalize(book);
			ClassicAssert.AreEqual(16L, operation.DueAfter);
			ClassicAssert.AreEqual(KingdomTradePhase.Quarantined, operation.Phase);
		}

		[Test]
		public void DerivedOperationIdTamper_QuarantinesWithoutReplacement()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestTurnback);
			string tampered = new string('d', 64);
			operation.Id = tampered;
			KingdomTradeRules.Normalize(book);
			ClassicAssert.AreEqual(tampered, operation.Id);
			ClassicAssert.AreEqual(KingdomTradePhase.Quarantined, operation.Phase);
		}

		[Test]
		public void ManifestAndProjectionOperationIds_AreRederivedFromRealmAndSequence()
		{
			KingdomTradeBook book = Book();
			string source = KingdomTradeRules.OperationId(Realm, 8L);
			book.Manifest = new KingdomTradeManifestState
			{
				OperationSequence = 8L, OperationId = source,
				Id = KingdomTradeRules.ManifestId(source), OriginId = CityA,
				OriginName = "City A", DestinationId = CityB, DestinationName = "City B",
				OriginalDrams = 10, EscrowDrams = 10, LoadedTick = 1L,
				DeadlineTick = 2L, Status = KingdomTradeManifestStatus.InFlight
			};
			string projectionOp = KingdomTradeRules.OperationId(Realm, 9L);
			book.Projections.Add(new KingdomTradeProjectionRow
			{
				OperationSequence = 10L, OperationId = projectionOp,
				SettlementId = CityA, ZoneId = "zone-a",
				ProjectionId = KingdomTradeRules.ProjectionId(projectionOp), ObjectId = "object-a"
			});
			book.Manifest.OperationId = KingdomTradeRules.OperationId(Realm, 7L);
			KingdomTradeRules.Normalize(book);
			ClassicAssert.AreEqual(KingdomTradeManifestStatus.Quarantined, book.Manifest.Status);
			ClassicAssert.IsTrue(book.Projections[0].Quarantined);
			ClassicAssert.AreEqual(8L, book.Manifest.OperationSequence);
			ClassicAssert.AreEqual(10L, book.Projections[0].OperationSequence);
		}

		[Test]
		public void DuplicateCharterCollision_QuarantinesExistingQuarantinedAndNewRows()
		{
			KingdomTradeBook book = Book();
			book.Charters.Add(new KingdomTradeCharter
			{
				Sequence = 1L, Id = KingdomTradeRules.CharterId(Realm, 1L),
				DealKey = "water", Faction = "villagers", CreatedTick = 1L,
				NextTick = 10L, Quarantined = true, Fault = "old"
			});
			book.Charters.Add(new KingdomTradeCharter
			{
				Sequence = 2L, Id = KingdomTradeRules.CharterId(Realm, 2L),
				DealKey = "water", Faction = "villagers", CreatedTick = 2L,
				NextTick = 11L
			});
			KingdomTradeRules.Normalize(book);
			ClassicAssert.IsTrue(book.Charters[0].Quarantined);
			ClassicAssert.IsTrue(book.Charters[1].Quarantined);
			StringAssert.Contains("duplicate", book.Charters[0].Fault);
			StringAssert.Contains("duplicate", book.Charters[1].Fault);
		}

		[Test]
		public void DuplicateProjectionCollision_QuarantinesEveryEvidenceRowSymmetrically()
		{
			KingdomTradeBook book = Book();
			string opA = KingdomTradeRules.OperationId(Realm, 1L);
			string opB = KingdomTradeRules.OperationId(Realm, 2L);
			book.Projections.Add(new KingdomTradeProjectionRow
			{
				OperationSequence = 1L, OperationId = opA,
				SettlementId = CityA, ZoneId = "zone-a",
				ProjectionId = KingdomTradeRules.ProjectionId(opA), ObjectId = "object-a",
				Quarantined = true, Fault = "old"
			});
			book.Projections.Add(new KingdomTradeProjectionRow
			{
				OperationSequence = 2L, OperationId = opB,
				SettlementId = CityA, ZoneId = "zone-b",
				ProjectionId = KingdomTradeRules.ProjectionId(opB), ObjectId = "object-b"
			});
			KingdomTradeRules.Normalize(book);
			ClassicAssert.IsTrue(book.Projections[0].Quarantined);
			ClassicAssert.IsTrue(book.Projections[1].Quarantined);
			StringAssert.Contains("duplicate", book.Projections[0].Fault);
			StringAssert.Contains("duplicate", book.Projections[1].Fault);
		}

		[Test]
		public void MalformedEvidence_IsQuarantinedInPlaceNotLaunderedAway()
		{
			KingdomTradeBook book = Book();
			KingdomTradeCharter row = new KingdomTradeCharter
			{
				Sequence = -1L, Id = "bad", DealKey = "", Faction = "", NextTick = -1L
			};
			book.Charters.Add(row);
			KingdomTradeRules.Normalize(book);
			ClassicAssert.AreEqual(1, book.Charters.Count);
			ClassicAssert.AreSame(row, book.Charters[0]);
			ClassicAssert.IsTrue(row.Quarantined);
		}

		[Test]
		public void OversizedFaultEvidence_IsPreservedExactlyWhenQuarantined()
		{
			KingdomTradeBook book = Book();
			string evidence = new string('x', KingdomTradeRules.MaxTextChars + 1);
			book.SchemaFault = evidence;
			KingdomTradeRules.Normalize(book);
			ClassicAssert.AreSame(evidence, book.SchemaFault);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
			Assert.Throws<InvalidDataException>(() => KingdomTradeCodec.EncodeEnvelope(new KingdomTradeBook
			{
				SchemaFault = new string('y', KingdomTradeCodec.MaxStringBytes + 1)
			}));
		}

		[Test]
		public void StaleOpenOperation_RequiresOneFullOperationDigestMatch()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.Phase = KingdomTradePhase.RetirementReady;
			ClassicAssert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			book.OpenOperation = operation;
			operation.DueAfter++;
			KingdomTradeRules.Normalize(book);
			ClassicAssert.AreSame(operation, book.OpenOperation);
			ClassicAssert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
		}

		[Test]
		public void Retirement_RejectsNonterminalFsmAndIntentLanes()
		{
			KingdomTradeBook preparedBook = Book();
			KingdomTradeOperation prepared = CharterOperation(preparedBook);
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(preparedBook, prepared,
				KingdomTradePhase.Terminal, 20L, null));
			ClassicAssert.AreSame(prepared, preparedBook.OpenOperation);

			KingdomTradeBook intentBook = Book();
			KingdomTradeOperation intent = CharterOperation(intentBook);
			intent.Phase = KingdomTradePhase.RetirementReady;
			intent.ProjectionState = KingdomTradePhysicalState.Intent;
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(intentBook, intent,
				KingdomTradePhase.Terminal, 20L, null));
			ClassicAssert.AreSame(intent, intentBook.OpenOperation);
		}

		[Test]
		public void QuarantinedOperation_CannotRetireAsTerminal()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.Phase = KingdomTradePhase.Quarantined;
			book.Charters[0].Quarantined = true;
			book.Charters[0].NextTick = operation.DueBefore;
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			ClassicAssert.AreSame(operation, book.OpenOperation);
			ClassicAssert.IsNull(book.PendingRetirement);
			ClassicAssert.AreEqual(KingdomTradePhase.Quarantined, operation.Phase);
		}

		[Test]
		public void Retirement_RequiresExactMandatoryOutboxReceipt()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.Phase = KingdomTradePhase.RetirementReady;
			operation.Outbox = null;
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			ClassicAssert.AreSame(operation, book.OpenOperation);
			ClassicAssert.IsNull(book.PendingRetirement);
			ClassicAssert.AreEqual(0, book.RecentProofs.Count);
		}

		[Test]
		public void CharterRetirement_RequiresOneExactLawfulScheduleDisposition()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.Phase = KingdomTradePhase.RetirementReady;
			book.Charters[0].NextTick = operation.DueAfter + 1L;
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			ClassicAssert.AreSame(operation, book.OpenOperation);
			ClassicAssert.IsNull(book.PendingRetirement);
			ClassicAssert.AreEqual(operation.DueAfter + 1L, book.Charters[0].NextTick);
		}

		[Test]
		public void Retirement_RejectsSkippedMandatoryCharterSinksAndWrongLedgerDelta()
		{
			for (int variant = 0; variant < 3; variant++)
			{
				KingdomTradeBook directBook = Book();
				KingdomTradeOperation direct = CharterOperation(directBook);
				direct.Phase = KingdomTradePhase.RetirementReady;
				CorruptTerminalCharterOutbox(direct, variant);
				ClassicAssert.IsFalse(KingdomTradeRules.Retire(directBook, direct,
					KingdomTradePhase.Terminal, 20L, null), "direct variant " + variant);
				ClassicAssert.AreSame(direct, directBook.OpenOperation, "direct variant " + variant);
				ClassicAssert.IsNull(directBook.PendingRetirement, "direct variant " + variant);

				KingdomTradeBook reloadBook = Book();
				KingdomTradeOperation reload = CharterOperation(reloadBook);
				reload.Phase = KingdomTradePhase.RetirementReady;
				CorruptTerminalCharterOutbox(reload, variant);
				KingdomTradeRules.Normalize(reloadBook);
				ClassicAssert.AreEqual(KingdomTradePhase.Quarantined, reload.Phase,
					"normalize variant " + variant);
				ClassicAssert.AreSame(reload, reloadBook.OpenOperation,
					"normalize variant " + variant);
			}
		}

		private static void CorruptTerminalCharterOutbox(
			KingdomTradeOperation operation, int variant)
		{
			if (variant == 0)
			{
				operation.Outbox = new KingdomTradeOutbox
				{
					EventId = operation.Id,
					LedgerDeliveredDelta = operation.ProvedWater,
					ChronicleState = KingdomTradeSinkState.Skipped,
					LedgerState = KingdomTradeSinkState.Skipped,
					MessageState = KingdomTradeSinkState.Skipped,
					DeedState = KingdomTradeSinkState.Skipped
				};
			}
			else if (variant == 1) operation.Outbox = null;
			else operation.Outbox.LedgerDeliveredDelta = operation.ProvedWater - 1;
		}

		[Test]
		public void CharterRetirement_RejectsNonCanonicalScheduleSequence()
		{
			KingdomTradeBook forgedBook = Book();
			KingdomTradeOperation forged = CharterOperation(forgedBook);
			forged.Phase = KingdomTradePhase.RetirementReady;
			KingdomTradeCharter forgedRow = forgedBook.Charters[0];
			forgedRow.Sequence = 2L;
			forgedBook.NextCharterSequence = 3L;
			ClassicAssert.AreEqual(forged.CharterId, forgedRow.Id);
			ClassicAssert.AreEqual(forged.DealKey, forgedRow.DealKey);
			ClassicAssert.AreEqual(forged.Faction, forgedRow.Faction);
			ClassicAssert.AreEqual(forged.DueAfter, forgedRow.NextTick);
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(forgedBook, forged,
				KingdomTradePhase.Terminal, 20L, null));
			ClassicAssert.AreSame(forged, forgedBook.OpenOperation);

			KingdomTradeBook counterBook = Book();
			KingdomTradeOperation counter = CharterOperation(counterBook);
			counter.Phase = KingdomTradePhase.RetirementReady;
			counterBook.NextCharterSequence = counterBook.Charters[0].Sequence;
			ClassicAssert.IsFalse(KingdomTradeRules.Retire(counterBook, counter,
				KingdomTradePhase.Terminal, 20L, null));
			ClassicAssert.AreSame(counter, counterBook.OpenOperation);
		}

		[Test]
		public void EveryRetirementAssignmentCut_ConvergesAndCleansTerminalManifest()
		{
			KingdomTradeBook source = Book();
			KingdomTradeManifestState manifest;
			KingdomTradeOperation operation = TerminalDelivery(source, out manifest);
			KingdomTradeManifestState saved = KingdomTradeRules.SnapshotManifest(manifest);
			ClassicAssert.IsTrue(KingdomTradeRules.Retire(source, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = source.RecentProofs[0];
			ClassicAssert.IsTrue(proof.ManifestCleanup);
			for (int cut = 0; cut < 5; cut++)
			{
				KingdomTradeBook book = Book();
				book.NextOperationSequence = proof.Sequence + 1L;
				book.OpenOperation = cut >= 3 ? null : operation;
				book.PendingRetirement = proof;
				if (cut >= 1) book.RecentProofs.Add(proof);
				if (cut >= 2) book.RetiredThrough = proof.Sequence;
				book.Manifest = cut >= 4 ? null : KingdomTradeRules.SnapshotManifest(saved);
				KingdomTradeRules.Normalize(book);
				ClassicAssert.AreEqual(KingdomTradeSchemaState.Compatible, book.SchemaState, "cut " + cut);
				ClassicAssert.IsNull(book.OpenOperation, "cut " + cut);
				ClassicAssert.IsNull(book.PendingRetirement, "cut " + cut);
				ClassicAssert.IsNull(book.Manifest, "cut " + cut);
				ClassicAssert.AreEqual(proof.Sequence, book.RetiredThrough, "cut " + cut);
				ClassicAssert.AreEqual(1, book.RecentProofs.Count, "cut " + cut);
			}
		}

		[Test]
		public void ProofCapacity_CompactsBeforePublishingNextOperation()
		{
			KingdomTradeBook book = Book();
			for (int i = 0; i < KingdomTradeRules.MaxRecentProofs; i++)
			{
				KingdomTradeOperation operation = CharterOperation(book);
				operation.Phase = KingdomTradePhase.RetirementReady;
				ClassicAssert.IsTrue(KingdomTradeRules.Retire(book, operation,
					KingdomTradePhase.Terminal, 20L + i, null), "retire " + i);
			}
			ClassicAssert.AreEqual(KingdomTradeRules.MaxRecentProofs, book.RecentProofs.Count);
			long sequence = book.NextOperationSequence;
			KingdomTradeOperation next = KingdomTradeRules.NewOperation(book,
				KingdomTradeOperationKind.CharterDelivery, 100L);
			ClassicAssert.NotNull(next);
			ClassicAssert.AreEqual(sequence, next.Sequence);
			ClassicAssert.Less(book.RecentProofs.Count, KingdomTradeRules.MaxRecentProofs);
			ClassicAssert.AreEqual(1, book.CompactedProofs.Count);
			ClassicAssert.AreEqual(KingdomTradeRules.MaxRecentProofs / 2,
				book.CompactedProofs[0].ProofCount);
		}

		[Test]
		public void ExilePreparation_IsAtomicAuthenticatedAndRetryIdempotent()
		{
			KingdomTradeBook book = Book();
			book.RetainedEscrowDrams = 9L;
			book.UnattributedArchivedEscrowDrams = 4L;
			book.Charters.Add(new KingdomTradeCharter
			{
				Sequence = 1L, Id = KingdomTradeRules.CharterId(Realm, 1L),
				DealKey = "water", Faction = "villagers", CreatedTick = 1L, NextTick = 20L
			});
			byte[] before = KingdomTradeCodec.EncodePayload(book);
			KingdomTradeBook replacement;
			long settledTick;
			string failure;
			ClassicAssert.IsFalse(KingdomTradeRules.TryPrepareExile(book, 50L,
				new string('d', 64), new List<string> { CityA, CityB }, out replacement,
				out settledTick, out failure));
			ClassicAssert.AreEqual(-1L, settledTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(book));
			ClassicAssert.IsTrue(KingdomTradeRules.TryPrepareExile(book, 50L, Realm,
				new List<string> { CityB, CityA }, out replacement, out settledTick, out failure),
				failure);
			ClassicAssert.AreEqual(50L, settledTick);
			ClassicAssert.AreNotSame(book, replacement);
			ClassicAssert.IsFalse(replacement.IdentityBound);
			ClassicAssert.AreEqual(1, replacement.Archives.Count);
			ClassicAssert.AreEqual(13L, replacement.Archives[0].RetainedEscrowDrams);
			ClassicAssert.AreEqual(0L, replacement.RetainedEscrowDrams);
			ClassicAssert.AreEqual(0L, replacement.UnattributedArchivedEscrowDrams);
			ClassicAssert.AreEqual(1, replacement.Archives[0].CharterCount);
			ClassicAssert.IsTrue(KingdomTradeRules.CanonicalSha256(
				replacement.Archives[0].AuthorityEvidenceHash));
			ClassicAssert.IsTrue(KingdomTradeRules.CanonicalSha256(
				replacement.Archives[0].ReceiptEvidenceHash));
			KingdomTradeBook roundTrip = KingdomTradeCodec.DecodeEnvelope(
				KingdomTradeCodec.EncodeEnvelope(replacement));
			ClassicAssert.AreEqual(replacement.Archives[0].ReceiptEvidenceHash,
				roundTrip.Archives[0].ReceiptEvidenceHash);
			ClassicAssert.IsTrue(KingdomTradeRules.TryAuthenticateExactExileClosedTick(roundTrip,
				Realm, new List<string> { CityA, CityB }, out long roundTripTick,
				out failure), failure);
			ClassicAssert.AreEqual(50L, roundTripTick);
			KingdomTradeBook retry;
			ClassicAssert.IsTrue(KingdomTradeRules.TryPrepareExile(replacement, 99L, Realm,
				new List<string> { CityA, CityB }, out retry, out settledTick, out failure),
				failure);
			ClassicAssert.AreEqual(50L, settledTick, "retry must recover original close tick");
			ClassicAssert.AreSame(replacement, retry);
			ClassicAssert.AreEqual(1, retry.Archives.Count);
		}

		[Test]
		public void ArchiveReceiptDigest_HasNoPublicMintingSurface()
		{
			ClassicAssert.IsNull(typeof(KingdomTradeRules).GetMethod("ArchiveReceiptDigest",
				System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.Static));
		}

		[Test]
		public void ExactExileReceiptAuthenticator_RecoversOriginalTickFromUnboundBookReadOnly()
		{
			KingdomTradeBook source = Book();
			KingdomTradeBook closed;
			long closedTick;
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			byte[] before = KingdomTradeCodec.EncodePayload(closed);
			ClassicAssert.IsTrue(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityB, CityA }, out closedTick, out failure), failure);
			ClassicAssert.AreEqual(50L, closedTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(closed));
		}

		[Test]
		public void ExactExileReceiptAuthenticator_ReboundProofKeepsOriginalTickAndRejectsDrift()
		{
			KingdomTradeBook source = Book();
			KingdomTradeBook closed;
			long closedTick;
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(closed, Realm,
				new[] { CityB, CityA }, out failure), failure);
			byte[] before = KingdomTradeCodec.EncodePayload(closed);
			ClassicAssert.IsTrue(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure), failure);
			ClassicAssert.AreEqual(50L, closedTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(closed));

			ClassicAssert.IsFalse(KingdomTradeRules.TryGetExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure),
				"legacy getter must remain strict-unbound");
			ClassicAssert.AreEqual(-1L, closedTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(closed));

			closed.OptionObservedTick = 51L;
			byte[] drifted = KingdomTradeCodec.EncodePayload(closed);
			ClassicAssert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure));
			ClassicAssert.AreEqual(-1L, closedTick);
			CollectionAssert.AreEqual(drifted, KingdomTradeCodec.EncodePayload(closed));
		}

		[Test]
		public void ExactExileReceiptAuthenticator_RejectsCoordinatedClosedTickForgery()
		{
			KingdomTradeBook source = Book();
			KingdomTradeBook closed;
			long closedTick;
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			closed.Archives[0].ClosedTick = 777L;
			closed.OptionObservedTick = 777L;
			byte[] forged = KingdomTradeCodec.EncodePayload(closed);
			ClassicAssert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure));
			ClassicAssert.AreEqual(-1L, closedTick);
			StringAssert.Contains("malformed", failure);
			CollectionAssert.AreEqual(forged, KingdomTradeCodec.EncodePayload(closed));
		}

		[Test]
		public void ExactExileReceiptAuthenticator_RejectsFabricatedPristineArchive()
		{
			KingdomTradeBook fabricated = new KingdomTradeBook();
			fabricated.Archives.Add(new KingdomTradeArchive
			{
				RealmId = Realm,
				SettlementIds = new List<string> { CityA, CityB },
				AuthorityEvidenceHash = "x",
				ClosedTick = 0L,
				ReceiptEvidenceHash = new string('0', 64)
			});
			byte[] before = KingdomTradeCodec.EncodePayload(fabricated);
			long closedTick;
			string failure;
			ClassicAssert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(fabricated,
				Realm, new List<string> { CityA, CityB }, out closedTick, out failure));
			ClassicAssert.AreEqual(-1L, closedTick);
			StringAssert.Contains("malformed", failure);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(fabricated));
		}

		[Test]
		public void ExactExileReceiptAuthenticator_ReboundRejectsMalformedReceiptDigest()
		{
			KingdomTradeBook source = Book();
			KingdomTradeBook closed;
			long closedTick;
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(closed, Realm,
				new[] { CityA, CityB }, out failure), failure);
			string wrong = new string('0', 64);
			ClassicAssert.AreNotEqual(closed.Archives[0].ReceiptEvidenceHash, wrong);
			closed.Archives[0].ReceiptEvidenceHash = wrong;
			byte[] before = KingdomTradeCodec.EncodePayload(closed);
			ClassicAssert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure));
			ClassicAssert.AreEqual(-1L, closedTick);
			StringAssert.Contains("malformed", failure);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(closed));
		}

		[Test]
		public void ExactExileReceiptLookup_FailsClosedOnTopologyCollisionAndMalformedEvidence()
		{
			KingdomTradeBook source = Book();
			KingdomTradeBook closed;
			long closedTick;
			string failure;
			ClassicAssert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			byte[] before = KingdomTradeCodec.EncodePayload(closed);
			ClassicAssert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, new string('d', 64) }, out closedTick, out failure));
			ClassicAssert.AreEqual(-1L, closedTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(closed));

			closed.Archives.Add(closed.Archives[0]);
			ClassicAssert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure));
			StringAssert.Contains("collides", failure);
			closed.Archives.RemoveAt(1);
			closed.Archives[0].AuthorityEvidenceHash = "";
			ClassicAssert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure));
			StringAssert.Contains("malformed", failure);
		}

		private sealed class HostileSettlementEnumerable : IEnumerable<string>
		{
			internal int Enumerations;

			public IEnumerator<string> GetEnumerator()
			{
				Enumerations++;
				throw new InvalidOperationException("hostile topology callback");
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		[Test]
		public void LogicalTradeAuthorityPinsNestedIdentityFieldsAndCompleteMethodOrder()
		{
			string source = KingdomTradeLogicalSource.Read();
			string declarations = ReadRepoSource("Trade/KingdomTrade.00.Declarations.cs");
			ClassicAssert.AreEqual(27, KingdomTradeLogicalSource.FileCount);
			ClassicAssert.AreEqual(27, Count(source, "public static partial class KingdomTrade"));
			CollectionAssert.AreEqual(new[]
			{
				"private const string ProjectionProperty = \"KingdomTradeProjectionId\";",
				"private const string MaterialProperty = \"KingdomTradeMaterialId\";",
				"private const int MaxProjectionCellProbes = 512;",
				"private static readonly object InFlightSync = new object();",
				"private static TradeLease InFlight;"
			}, TopLevelFieldRows(declarations));
			CollectionAssert.AreEqual(new[]
			{
				"private sealed class TradeLease : IDisposable",
				"private sealed class TradeExileCoreSeal",
				"private sealed class TradeLiveFrame",
				"private sealed class TradePhysicalFrame",
				"private sealed class WaterWitness",
				"private sealed class InventoryWitness",
				"private sealed class MaterialWitness",
				"private enum LoadedObjectResolution : byte",
				"private sealed class LoadedTopologyWitness",
				"private sealed class LoadedZoneWitness",
				"private sealed class LoadedObjectWitness",
				"private sealed class CellWitness",
				"private sealed class ProjectionRowWitness",
				"private sealed class ManifestWitness",
				"private sealed class CallbackWitness"
			}, NestedDeclarationRows(declarations));
			AssertOrdered(declarations, "private enum LoadedObjectResolution : byte",
				"Incomplete = 0", "Missing = 1", "ExactUnique = 2", "Ambiguous = 3");

			string[] methods = TopLevelMethodRows(source);
			ClassicAssert.AreEqual(144, methods.Length);
			ClassicAssert.AreEqual("d036ee978679118adcacf019f13c42282218a1f9b5df16f564fcc985680fd35e",
				Sha256(string.Join("\n", methods)),
				"all top-level method signatures and declaration order are metadata contract");
			AssertOrdered(source,
				"private static void ReconcilePhysicalFailure(TradeLiveFrame Frame,",
				"public static bool TryDeliverPolityConsignment(KingdomSystem System,",
				"private static bool TryDeliverPolityConsignmentCore(KingdomSystem System,",
				"private static bool TryPreflightPolityConsignmentGround(KingdomSystem System,",
				"private static bool PreparePolityConsignment(KingdomSystem System,",
				"private static bool SettlePolityConsignmentRetention(KingdomTradeBook Book,",
				"private static bool TryBindPolityConsignmentRecipient(KingdomSystem System,",
				"private static bool RequirePolityConsignmentRecipient(KingdomSystem System,",
				"private static bool ContinueOrQuarantinePolityRecipient(KingdomSystem System,");
			CollectionAssert.AreEqual(new[]
			{
				"public static KingdomTradeManifestState CurrentManifest(KingdomSystem System)",
				"public static bool ResetAuthority(KingdomSystem System, out string Failure)",
				"public static bool StrikeDeal(KingdomSystem System, string DealKey, string FactionName, out string Failure)",
				"public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Shared = null)",
				"public static bool TryLoadManifest(KingdomSystem System, Zone Z, int Amount, string OriginName, string DestinationName, out string Failure)",
				"public static KingdomManifest ExpireManifestIfStale(KingdomSystem System, Zone Here, long Now)",
				"public static bool TryOnExile(KingdomSystem System, long Now, string ExactRealmId, List<string> ExactSettlementIds, out long SettledTick, out string Failure)",
				"public static bool TryDeliverPolityConsignment(KingdomSystem System, Zone Ground, GameObject Recipient, KingdomPolityConsignmentRequest Request, out KingdomTradePolityConsignmentReceipt Receipt, out string Failure)",
				"internal static KingdomManifest LegacyManifestSnapshot( KingdomTradeManifestState Manifest)",
				"internal static KingdomManifest LegacyManifestSnapshot(KingdomManifest Manifest)",
				"internal static bool LegacyManifestMatches(KingdomManifest Legacy, KingdomTradeManifestState Authoritative)"
			}, PublicAndInternalMethods(methods));
			StringAssert.Contains(
				"public static bool Enabled => XRL.UI.Options.GetOption(\"r_TAF_OptionTrade\") != \"No\";",
				source);
		}

		[Test]
		public void LogicalTradeAuthorityPinsLifecycleAndSinkTransactionOrder()
		{
			string source = KingdomTradeLogicalSource.Read();
			string continuation = Between(source,
				"private static void ContinueOperation(KingdomSystem System",
				"private static bool SettleResources(");
			AssertOrdered(continuation,
				"TryBindTopologyGround(System, Z, Survey)",
				"TryBindFrame(System, Book, operation, Z",
				"SettleResources(operation, Z, Survey, frame)",
				"SettleProjection(operation, Z, frame)",
				"SettleDomain(System, Book, operation, frame)",
				"BuildOutbox(System, operation)",
				"DispatchOutbox(System, operation, frame)",
				"ContinuePatternBook(System, operation, frame)",
				"ExactPhysicalFrame(frame, operation, Z)",
				"SettleSchedule(Book, operation, frame)",
				"operation.Phase = KingdomTradePhase.RetirementReady",
				"KingdomTradeRules.Retire(Book, operation, disposition, Now, operation.Fault)",
				"System.SynchronizeLegacyManifestProjection()");

			string outbox = Between(source,
				"private static bool DispatchOutbox(KingdomSystem System",
				"private static bool ExactSinkFrame(");
			AssertOrdered(outbox,
				"box.ChronicleState = KingdomTradeSinkState.Intent",
				"KingdomChronicle.RecordOnce(System",
				"box.LedgerState = KingdomTradeSinkState.Intent",
				"Frame.Ledger.Delivered = KingdomTradeRules.SaturatingAdd",
				"box.MessageState = KingdomTradeSinkState.Intent",
				"MessageQueue.AddPlayerMessage(message)",
				"box.DeedState = KingdomTradeSinkState.Intent",
				"System.RecordDeed(deed)");
		}

		[Test]
		public void ExileMutationApis_RejectHostileEnumerableByConcreteSignature()
		{
			HostileSettlementEnumerable hostile = new HostileSettlementEnumerable();
			object[] arguments = { Book(), 50L, Realm, hostile, null, null };
			Assert.Throws<MissingMethodException>(() => typeof(KingdomTradeRules).InvokeMember(
				"TryPrepareExile", System.Reflection.BindingFlags.InvokeMethod
					| System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
				null, null, arguments));
			ClassicAssert.AreEqual(0, hostile.Enumerations);

			string source = KingdomTradeLogicalSource.Read();
			int method = source.IndexOf("public static bool TryOnExile", StringComparison.Ordinal);
			int end = source.IndexOf("private static KingdomTradeBook EnsureBook", method,
				StringComparison.Ordinal);
			string body = source.Substring(method, end - method);
			StringAssert.Contains("List<string> ExactSettlementIds", body);
			ClassicAssert.IsFalse(body.Contains("IEnumerable<string> ExactSettlementIds"));
			int snapshot = body.IndexOf("KingdomTradeCodec.EncodePayload(original)",
				StringComparison.Ordinal);
			int listAccess = body.IndexOf("ExactSettlementIds.Count", StringComparison.Ordinal);
			ClassicAssert.Greater(snapshot, 0);
			ClassicAssert.Greater(listAccess, snapshot,
				"Trade bytes must be frozen before any caller-owned topology access");
			int firstTopology = body.IndexOf("System.TryRetainedSettlementIds(true, false",
				StringComparison.Ordinal);
			int coreCapture = body.IndexOf("TryCaptureExileCoreSeal", StringComparison.Ordinal);
			int prepare = body.IndexOf("KingdomTradeRules.TryPrepareExile", StringComparison.Ordinal);
			int finalTopology = body.IndexOf("System.TryRetainedSettlementIds(true, false",
				firstTopology + 1, StringComparison.Ordinal);
			int finalCore = body.IndexOf("ExactExileCoreSeal", finalTopology,
				StringComparison.Ordinal);
			int finalSeal = body.IndexOf("KingdomTradeRules.ExactAuthoritySeal", finalTopology,
				StringComparison.Ordinal);
			int publish = body.IndexOf("System.TradeBook = replacement", StringComparison.Ordinal);
			ClassicAssert.Greater(firstTopology, listAccess);
			ClassicAssert.Greater(coreCapture, firstTopology);
			ClassicAssert.Greater(prepare, coreCapture);
			ClassicAssert.Greater(finalTopology, prepare);
			ClassicAssert.Greater(finalCore, finalTopology);
			ClassicAssert.Greater(finalSeal, finalTopology);
			ClassicAssert.Less(finalCore, finalSeal);
			ClassicAssert.Greater(publish, finalSeal);
			ClassicAssert.AreEqual(publish, body.LastIndexOf("System.TradeBook = replacement",
				StringComparison.Ordinal), "Trade exile must have one publish cut");
			StringAssert.Contains("ExactSettlementTopology(liveTopology, exact)", body);
			StringAssert.Contains("ExactSettlementTopology(finalTopology, exact)", body);
			StringAssert.Contains("ReferenceEquals(System.SettlementTopology,", source);
			StringAssert.Contains("Roots.Add(System.SettlementTopology);", source);
			StringAssert.Contains("Roots.Add(System.SettlementTopology.Get(i));", source);
			ClassicAssert.IsFalse(source.Contains("System.Away"));
			StringAssert.Contains("KingdomRealmArchive.TryCurrentGraphHash", source);
			StringAssert.Contains("KingdomTradeRules.ExactReferenceSeal", source);
		}

		private sealed class LookupRow
		{
			internal string Id;
		}

		private sealed class HostileAwayGraph
		{
			public string RealmId;
			public string SettlementId;
			public List<string> ClaimedZones;
			public HostileAwayNested Nested;
		}

		private sealed class HostileAwayNested
		{
			public List<string> Rows;
		}

		[Test]
		public void ExactReferenceSeal_RejectsSameRealmSameIdDistinctAwayReplacement()
		{
			HostileAwayGraph away = new HostileAwayGraph
			{
				RealmId = Realm,
				SettlementId = CityB,
				ClaimedZones = new List<string> { "zone-b" },
				Nested = new HostileAwayNested { Rows = new List<string> { "row" } }
			};
			ClassicAssert.IsTrue(KingdomTradeRules.TryCaptureExactReferenceSeal(
				new object[] { away }, out KingdomTradeReferenceSeal seal));
			ClassicAssert.IsTrue(KingdomTradeRules.ExactReferenceSeal(new object[] { away }, seal));

			HostileAwayGraph replacement = new HostileAwayGraph
			{
				RealmId = Realm,
				SettlementId = CityB,
				ClaimedZones = new List<string> { "zone-b" },
				Nested = new HostileAwayNested { Rows = new List<string> { "row" } }
			};
			ClassicAssert.IsFalse(KingdomTradeRules.ExactReferenceSeal(
				new object[] { replacement }, seal));

			ClassicAssert.IsTrue(KingdomTradeRules.TryCaptureExactReferenceSeal(
				new object[] { away }, out seal));
			away.Nested.Rows = new List<string> { "row" };
			ClassicAssert.IsFalse(KingdomTradeRules.ExactReferenceSeal(new object[] { away }, seal));
		}

		[Test]
		public void ExactReferenceSeal_RejectsGraphEquivalentTopologyMemberReplacement()
		{
			KingdomSettlementTopology topology = new KingdomSettlementTopology();
			KingdomSettlement b = new KingdomSettlement { SettlementName = "Basin" };
			b.City.SettlementId = KingdomIdentityRules.SettlementPrefix + new string('b', 64);
			KingdomSettlement c = new KingdomSettlement { SettlementName = "Cairn" };
			c.City.SettlementId = KingdomIdentityRules.SettlementPrefix + new string('c', 64);
			ClassicAssert.IsTrue(topology.TryAdd(b, out string failure), failure);
			ClassicAssert.IsTrue(topology.TryAdd(c, out failure), failure);
			ClassicAssert.IsTrue(KingdomTradeRules.TryCaptureExactReferenceSeal(
				new object[] { topology, topology.Get(0), topology.Get(1) },
				out KingdomTradeReferenceSeal seal));

			KingdomSettlement replacement = new KingdomSettlement { SettlementName = "Cairn" };
			replacement.City.SettlementId = c.City.SettlementId;
			ClassicAssert.IsTrue(topology.TryReplaceReference(c, replacement, out failure), failure);
			ClassicAssert.IsFalse(KingdomTradeRules.ExactReferenceSeal(
				new object[] { topology, topology.Get(0), topology.Get(1) }, seal));
		}

		[Test]
		public void ManifestDestinationUsesUniqueAuthoritativeNonSeatName()
		{
			string manifest = ReadRepoSource("Trade/KingdomTrade.08.ActivationAndManifest.cs");
			string topology = ReadRepoSource("Core/KingdomSettlementTopology.cs");
			StringAssert.Contains(
				"System.TryFindNonSeatSettlementByName(DestinationName,", manifest);
			StringAssert.Contains("destination.City.SettlementId", manifest);
			ClassicAssert.IsFalse(manifest.Contains("System.Away"));
			StringAssert.Contains("if (Settlement != null)", topology,
				"duplicate exact names must refuse rather than choose a city");
			StringAssert.Contains("Settlement = null;", topology);
		}

		[Test]
		public void ExactLookup_IsTriStateAndNeverChoosesCollision()
		{
			LookupRow exact;
			LookupRow row = new LookupRow { Id = Realm };
			ClassicAssert.AreEqual(KingdomTradeExactLookup.Incomplete,
				KingdomTradeRules.ResolveExactUnique<LookupRow>(null, Realm, x => x.Id, out exact));
			ClassicAssert.AreEqual(KingdomTradeExactLookup.Missing,
				KingdomTradeRules.ResolveExactUnique(new List<LookupRow>(), Realm, x => x.Id, out exact));
			ClassicAssert.AreEqual(KingdomTradeExactLookup.ExactUnique,
				KingdomTradeRules.ResolveExactUnique(new List<LookupRow> { row }, Realm,
					x => x.Id, out exact));
			ClassicAssert.AreSame(row, exact);
			ClassicAssert.AreEqual(KingdomTradeExactLookup.Ambiguous,
				KingdomTradeRules.ResolveExactUnique(new List<LookupRow>
					{ row, new LookupRow { Id = Realm } }, Realm, x => x.Id, out exact));
			ClassicAssert.IsNull(exact);
		}

		[Test]
		public void Codec_RejectsNoncanonicalBooleanByte()
		{
			byte[] envelope = KingdomTradeCodec.EncodeEnvelope(Book());
			const int firstPayloadBoolean = 12 + 4 + 1 + 4;
			envelope[firstPayloadBoolean] = 2;
			Assert.Throws<InvalidDataException>(() => KingdomTradeCodec.DecodeEnvelope(envelope));
		}

		[Test]
		public void OptionTransitions_OnlyEnableRequestsRestamp()
		{
			ClassicAssert.AreEqual(KingdomTradeOptionAction.Disable,
				KingdomTradeRules.ObserveOption(KingdomTradeOptionState.Unknown, false));
			ClassicAssert.AreEqual(KingdomTradeOptionAction.StayDisabled,
				KingdomTradeRules.ObserveOption(KingdomTradeOptionState.Disabled, false));
			ClassicAssert.AreEqual(KingdomTradeOptionAction.EnableAndRestamp,
				KingdomTradeRules.ObserveOption(KingdomTradeOptionState.Disabled, true));
			ClassicAssert.AreEqual(KingdomTradeOptionAction.None,
				KingdomTradeRules.ObserveOption(KingdomTradeOptionState.Enabled, true));
		}

		[Test]
		public void LiveLeaseFailureBranchesReturnBeforeAuthorityMutation()
		{
			string source = KingdomTradeLogicalSource.Read();
			int exile = source.IndexOf("public static bool TryOnExile", StringComparison.Ordinal);
			int reset = source.IndexOf("public static bool ResetAuthority", StringComparison.Ordinal);
			int exileEnd = source.IndexOf("private static KingdomTradeBook EnsureBook", exile,
				StringComparison.Ordinal);
			string exilePrefix = source.Substring(exile, exileEnd - exile);
			int failedLease = exilePrefix.IndexOf("if (!TryEnter(System, out lease))", StringComparison.Ordinal);
			int usingLease = exilePrefix.IndexOf("using (lease)", failedLease, StringComparison.Ordinal);
			string failureBranch = exilePrefix.Substring(failedLease, usingLease - failedLease);
			StringAssert.Contains("return false;", failureBranch);
			ClassicAssert.IsFalse(failureBranch.Contains("QuarantineBook"));
			ClassicAssert.IsFalse(failureBranch.Contains("System.TradeBook ="));

			int resetFailed = source.IndexOf("if (!TryEnter(System, out lease))", reset,
				StringComparison.Ordinal);
			int resetUsing = source.IndexOf("using (lease)", resetFailed, StringComparison.Ordinal);
			string resetBranch = source.Substring(resetFailed, resetUsing - resetFailed);
			ClassicAssert.IsFalse(resetBranch.Contains("System.TradeBook ="));
			ClassicAssert.Greater(source.IndexOf("System.TradeBook = new KingdomTradeBook()", resetUsing,
				StringComparison.Ordinal), resetUsing);
		}

		[Test]
		public void PatternSource_CharterPrepareOwnsFreezeAndExactCityCasBeforeRetirement()
		{
			string trade = KingdomTradeLogicalSource.Read();
			int prepare = trade.IndexOf("private static bool PrepareCharterDelivery",
				StringComparison.Ordinal);
			int prepareEnd = trade.IndexOf("private static bool TryProjectionRow", prepare,
				StringComparison.Ordinal);
			string prepareBody = trade.Substring(prepare, prepareEnd - prepare);
			StringAssert.Contains("KingdomCeremony.FreezePatternBook(System", prepareBody);
			StringAssert.Contains("operation.SettlementId, operation.Sequence", prepareBody);

			int continuation = trade.IndexOf("private static bool ContinuePatternBook",
				StringComparison.Ordinal);
			int schedule = trade.IndexOf("private static bool SettleSchedule", continuation,
				StringComparison.Ordinal);
			string patternBody = trade.Substring(continuation, schedule - continuation);
			StringAssert.Contains("System.KeepersRoster = receipt.RosterAfter", patternBody);
			StringAssert.Contains("System.City?.SettlementId, Operation.SettlementId", patternBody);
			StringAssert.Contains("KingdomChronicle.RecordOnce", patternBody);
			ClassicAssert.IsFalse(patternBody.Contains("KingdomZoning.Learn"));

			int schedulePhase = trade.IndexOf(
				"if (operation.Phase == KingdomTradePhase.ScheduleIntent)",
				StringComparison.Ordinal);
			int patternCall = trade.IndexOf("ContinuePatternBook(System, operation, frame)",
				schedulePhase, StringComparison.Ordinal);
			int retire = trade.IndexOf("KingdomTradeRules.Retire", patternCall,
				StringComparison.Ordinal);
			ClassicAssert.Greater(patternCall, schedulePhase);
			ClassicAssert.Greater(retire, patternCall);

			string ceremony = KingdomCeremonyLogicalSource.Read();
			StringAssert.Contains("DecodeRoster(System.KeepersRoster)", ceremony);
			ClassicAssert.IsFalse(ceremony.Contains("OnCaravanArrived"),
				"unreachable tick-owned ceremony must not remain as parallel authority");
			StringAssert.Contains("System.City.SettlementId", ceremony);

			int exactSettlement = trade.IndexOf("private static bool ExactSettlement",
				StringComparison.Ordinal);
			int exactLedger = trade.IndexOf("private static bool ExactLedger", exactSettlement,
				StringComparison.Ordinal);
			string exactSeat = trade.Substring(exactSettlement, exactLedger - exactSettlement);
			StringAssert.Contains("Frame.City.SettlementId, Frame.SettlementId", exactSeat);
			StringAssert.Contains("Frame.Operation.SettlementId, Frame.SettlementId", exactSeat);
			StringAssert.Contains("system.KeepersRoster ?? \"\", Frame.KeepersRoster", exactSeat);

			int quarantine = trade.IndexOf("private static bool SettlePatternForQuarantine",
				StringComparison.Ordinal);
			int settleSchedule = trade.IndexOf("private static bool SettleSchedule", quarantine,
				StringComparison.Ordinal);
			string quarantineBody = trade.Substring(quarantine,
				settleSchedule - quarantine);
			StringAssert.Contains("AlreadyApplied", quarantineBody);
			StringAssert.Contains("MarkLearned", quarantineBody);
			StringAssert.Contains("MarkConflict", quarantineBody);
			StringAssert.Contains("KingdomTradePhase.Quarantined", quarantineBody);
		}

		private static int Count(string Text, string Needle)
		{
			int count = 0;
			for (int at = 0; ; )
			{
				at = Text.IndexOf(Needle, at, StringComparison.Ordinal);
				if (at < 0) return count;
				count++;
				at += Needle.Length;
			}
		}

		private static string Between(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, Start);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(end, start, End);
			return Source.Substring(start, end - start);
		}

		private static string[] TopLevelFieldRows(string Source)
		{
			List<string> rows = new List<string>();
			foreach (string line in Source.Split('\n'))
			{
				if (line.StartsWith("\t\tprivate ", StringComparison.Ordinal)
					&& !line.StartsWith("\t\t\t", StringComparison.Ordinal)
					&& line.TrimEnd().EndsWith(";", StringComparison.Ordinal))
					rows.Add(line.Trim());
			}
			return rows.ToArray();
		}

		private static string[] NestedDeclarationRows(string Source)
		{
			List<string> rows = new List<string>();
			foreach (string line in Source.Split('\n'))
			{
				if (line.StartsWith("\t\tprivate sealed class ", StringComparison.Ordinal)
					|| line.StartsWith("\t\tprivate enum ", StringComparison.Ordinal))
					rows.Add(line.Trim());
			}
			return rows.ToArray();
		}

		private static string[] TopLevelMethodRows(string Source)
		{
			List<string> rows = new List<string>();
			string[] lines = Source.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				if (!(line.StartsWith("\t\tpublic static ", StringComparison.Ordinal)
					|| line.StartsWith("\t\tinternal static ", StringComparison.Ordinal)
					|| line.StartsWith("\t\tprivate static ", StringComparison.Ordinal))
					|| !line.Contains("(") || line.Contains(";") || line.Contains("=>")) continue;
				StringBuilder row = new StringBuilder(line.Trim());
				while (++i < lines.Length && !lines[i].Contains("{"))
					row.Append(' ').Append(lines[i].Trim());
				rows.Add(NormalizeWhitespace(row.ToString()));
			}
			return rows.ToArray();
		}

		private static string[] PublicAndInternalMethods(string[] Methods)
		{
			List<string> rows = new List<string>();
			for (int i = 0; i < Methods.Length; i++)
				if (Methods[i].StartsWith("public ", StringComparison.Ordinal)
					|| Methods[i].StartsWith("internal ", StringComparison.Ordinal))
					rows.Add(Methods[i]);
			return rows.ToArray();
		}

		private static string NormalizeWhitespace(string Text)
		{
			return string.Join(" ", Text.Split((char[])null,
				StringSplitOptions.RemoveEmptyEntries));
		}

		private static string Sha256(string Text)
		{
			using (SHA256 sha = SHA256.Create())
				return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Text)))
					.Replace("-", "").ToLowerInvariant();
		}

		private static void AssertOrdered(string Source, params string[] Needles)
		{
			int cursor = 0;
			for (int i = 0; i < Needles.Length; i++)
			{
				int next = Source.IndexOf(Needles[i], cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(next, cursor, Needles[i]);
				cursor = next + Needles[i].Length;
			}
		}
	}
}
#endif
