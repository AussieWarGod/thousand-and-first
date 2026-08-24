#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomTradeRulesTests
	{
		private static readonly string Realm = new string('a', 64);
		private static readonly string CityA = new string('b', 64);
		private static readonly string CityB = new string('c', 64);

		private static KingdomTradeBook Book()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			string failure;
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { CityB, CityA }, out failure), failure);
			return book;
		}

		private static KingdomTradeOperation ManifestOperation(KingdomTradeBook book,
			KingdomTradeOperationKind kind, long tick = 10L)
		{
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(book, kind, tick);
			Assert.NotNull(operation);
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
			Assert.NotNull(operation);
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

		[Test]
		public void ExactIdentityBind_SortsUpToProductCapAndRejectsChanges()
		{
			KingdomTradeBook book = Book();
			Assert.AreEqual(CityA, book.SettlementIds[0]);
			Assert.AreEqual(CityB, book.SettlementIds[1]);
			Assert.AreEqual(4, KingdomTradeRules.MaxSettlementIds);
			string failure;
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { CityA, CityB }, out failure));
			Assert.IsFalse(KingdomTradeRules.BindExactIdentity(book, new string('d', 64),
				new[] { CityA, CityB }, out failure));
			Assert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
		}

		[Test]
		public void ExactIdentityExpansion_GrowsOneToTwoToFourAndEqualIsIdempotent()
		{
			string cityC = new string('d', 64);
			string cityD = new string('e', 64);
			KingdomTradeBook book = new KingdomTradeBook();
			string failure;
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { CityA }, out failure), failure);
			Assert.IsTrue(KingdomTradeRules.ExpandExactIdentity(book, Realm,
				new[] { CityB, CityA }, out failure), failure);
			Assert.IsTrue(KingdomTradeRules.ExpandExactIdentity(book, Realm,
				new[] { cityD, CityB, CityA, cityC }, out failure), failure);
			List<string> expanded = book.SettlementIds;
			Assert.IsTrue(KingdomTradeRules.ExpandExactIdentity(book, Realm,
				new[] { CityA, CityB, cityC, cityD }, out failure), failure);
			Assert.AreSame(expanded, book.SettlementIds);
			CollectionAssert.AreEqual(new[] { CityA, CityB, cityC, cityD }, book.SettlementIds);
		}

		[Test]
		public void ExactIdentityExpansion_AllowsSettledManifestAuthority()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			string failure;
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
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
			Assert.IsTrue(KingdomTradeRules.ExpandExactIdentity(book, Realm,
				new[] { CityA, CityB }, out failure), failure);
			Assert.NotNull(book.Manifest);
			Assert.AreEqual(2, book.SettlementIds.Count);
		}

		[Test]
		public void ExactIdentityExpansion_BusyReceiptDefersWithoutMutationOrQuarantine()
		{
			KingdomTradeBook operationBook = new KingdomTradeBook();
			string failure;
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(operationBook, Realm,
				new[] { CityA }, out failure));
			KingdomTradeRules.NewOperation(operationBook,
				KingdomTradeOperationKind.ManifestTurnback, 1L);
			List<string> original = operationBook.SettlementIds;
			Assert.IsFalse(KingdomTradeRules.ExpandExactIdentity(operationBook, Realm,
				new[] { CityA, CityB }, out failure));
			Assert.AreSame(original, operationBook.SettlementIds);
			Assert.AreEqual(KingdomTradeSchemaState.Compatible, operationBook.SchemaState);

			KingdomTradeBook pendingBook = new KingdomTradeBook();
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(pendingBook, Realm,
				new[] { CityA }, out failure));
			pendingBook.PendingRetirement = new KingdomTradeProof();
			original = pendingBook.SettlementIds;
			Assert.IsFalse(KingdomTradeRules.ExpandExactIdentity(pendingBook, Realm,
				new[] { CityA, CityB }, out failure));
			Assert.AreSame(original, pendingBook.SettlementIds);
			Assert.AreEqual(KingdomTradeSchemaState.Compatible, pendingBook.SchemaState);
		}

		[Test]
		public void ExactIdentityExpansion_ImmutableViolationsQuarantineWithoutReplacement()
		{
			string failure;
			KingdomTradeBook removal = Book();
			List<string> original = removal.SettlementIds;
			Assert.IsFalse(KingdomTradeRules.ExpandExactIdentity(removal, Realm,
				new[] { CityA }, out failure));
			Assert.AreSame(original, removal.SettlementIds);
			Assert.AreEqual(KingdomTradeSchemaState.Quarantined, removal.SchemaState);

			KingdomTradeBook replacement = Book();
			original = replacement.SettlementIds;
			Assert.IsFalse(KingdomTradeRules.ExpandExactIdentity(replacement, Realm,
				new[] { CityA, new string('d', 64) }, out failure));
			Assert.AreSame(original, replacement.SettlementIds);
			Assert.AreEqual(KingdomTradeSchemaState.Quarantined, replacement.SchemaState);

			KingdomTradeBook wrongRealm = Book();
			original = wrongRealm.SettlementIds;
			Assert.IsFalse(KingdomTradeRules.ExpandExactIdentity(wrongRealm,
				new string('e', 64), new[] { CityA, CityB }, out failure));
			Assert.AreSame(original, wrongRealm.SettlementIds);
			Assert.AreEqual(KingdomTradeSchemaState.Quarantined, wrongRealm.SchemaState);
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
				Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
					new[] { CityA }, out failure));
				List<string> original = book.SettlementIds;
				Assert.IsFalse(KingdomTradeRules.ExpandExactIdentity(book, Realm,
					invalid, out failure));
				Assert.AreSame(original, book.SettlementIds);
				Assert.AreEqual(KingdomTradeSchemaState.Compatible, book.SchemaState);
			}
		}

		[Test]
		public void UnboundEvidence_CannotBePromotedToLiveAuthority()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			book.Charters.Add(new KingdomTradeCharter { Sequence = 1L, Id = "legacy-name-id" });
			string failure;
			Assert.IsFalse(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { CityA }, out failure));
			Assert.IsFalse(book.IdentityBound);
			Assert.AreEqual("legacy-name-id", book.Charters[0].Id);
			Assert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
		}

		[Test]
		public void StableIds_AreSha256DomainSeparatedAndLengthPrefixed()
		{
			string operation = KingdomTradeRules.OperationId(Realm, 7L);
			Assert.AreEqual(operation, KingdomTradeRules.OperationId(Realm, 7L));
			Assert.AreNotEqual(operation, KingdomTradeRules.CharterId(Realm, 7L));
			Assert.AreNotEqual(operation, KingdomTradeRules.OperationId(Realm, 8L));
			Assert.AreEqual(64, operation.Length);
			StringAssert.IsMatch("^[0-9a-f]{64}$", operation);
			Assert.AreNotEqual(
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
			Assert.IsTrue(KingdomTradeRules.BookUsable(decoded));
			Assert.AreEqual(99L, decoded.OptionObservedTick);
			Assert.AreEqual(7L, decoded.OptionEpoch);
			Assert.IsTrue(decoded.RestampPending);
			Assert.AreEqual(44L, decoded.RetainedEscrowDrams);
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
			Assert.AreEqual(KingdomTradeSchemaState.Unknown, book.SchemaState);
			Assert.IsFalse(KingdomTradeRules.BookUsable(book));
			CollectionAssert.AreEqual(original, KingdomTradeCodec.EncodeEnvelope(book));
		}

		[Test]
		public void Codec_PriorBoundedWireStaysOpaqueButUnsafeV1IsRefused()
		{
			byte[] prior = Envelope(KingdomTradeCodec.CurrentWireVersion - 1,
				new byte[] { 4, 3, 2, 1 });
			KingdomTradeBook book = KingdomTradeCodec.DecodeEnvelope(prior);
			Assert.AreEqual(KingdomTradeSchemaState.Unknown, book.SchemaState);
			Assert.IsFalse(KingdomTradeRules.BookUsable(book));
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
			Assert.AreEqual(2, decoded.FormatVersion);
			Assert.AreEqual("old evidence", decoded.SchemaFault);
			Assert.IsFalse(KingdomTradeRules.BookUsable(decoded));
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
			Assert.IsTrue(KingdomTradeRules.ExactAuthoritySeal(book, claimed, city, seal));
			operation.DueAfter++;
			Assert.IsFalse(KingdomTradeRules.ExactAuthoritySeal(book, claimed, city, seal));
			operation.DueAfter--;
			seal = KingdomTradeRules.CaptureAuthoritySeal(book, claimed, city);
			operation.Outbox.LedgerNote = "hostile";
			Assert.IsFalse(KingdomTradeRules.ExactAuthoritySeal(book, claimed, city, seal));
		}

		[Test]
		public void AuthoritySeal_RejectsZoneReplacementAndContentMutation()
		{
			KingdomTradeBook book = Book();
			List<string> claimed = new List<string> { "zone-a" };
			List<string> city = new List<string> { "zone-a" };
			KingdomTradeAuthoritySeal seal = KingdomTradeRules.CaptureAuthoritySeal(book, claimed, city);
			Assert.IsFalse(KingdomTradeRules.ExactAuthoritySeal(book, claimed,
				new List<string> { "zone-a" }, seal));
			seal = KingdomTradeRules.CaptureAuthoritySeal(book, claimed, city);
			city[0] = "zone-b";
			Assert.IsFalse(KingdomTradeRules.ExactAuthoritySeal(book, claimed, city, seal));
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
			Assert.AreNotSame(authority, snapshot);
			snapshot.EscrowDrams = 0;
			snapshot.OriginId = CityB;
			Assert.AreEqual(20, authority.EscrowDrams);
			Assert.AreEqual(CityA, authority.OriginId);
		}

		[Test]
		public void ManifestCredit_ReconcilesExactlyOnceAndRejectsUnknownCurrentValue()
		{
			int after;
			bool apply;
			Assert.IsTrue(KingdomTradeRules.TryReconcileEscrow(100, 60, 100, out after, out apply));
			Assert.AreEqual(40, after);
			Assert.IsTrue(apply);
			Assert.IsTrue(KingdomTradeRules.TryReconcileEscrow(100, 60, 40, out after, out apply));
			Assert.IsFalse(apply);
			Assert.IsFalse(KingdomTradeRules.TryReconcileEscrow(100, 60, 70, out after, out apply));
		}

		[Test]
		public void RetainedEscrow_ReconcilesExactlyOnceAndRejectsOverflowAlias()
		{
			long after;
			bool apply;
			Assert.IsTrue(KingdomTradeRules.TryReconcileRetained(20L, 15L, 20L, out after, out apply));
			Assert.AreEqual(35L, after);
			Assert.IsTrue(apply);
			Assert.IsTrue(KingdomTradeRules.TryReconcileRetained(20L, 15L, 35L, out after, out apply));
			Assert.IsFalse(apply);
			Assert.IsFalse(KingdomTradeRules.TryReconcileRetained(20L, 15L, 30L, out after, out apply));
			Assert.IsFalse(KingdomTradeRules.TryReconcileRetained(long.MaxValue, 1L,
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
			Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, "closed"));
			KingdomTradeProof proof = book.RecentProofs[0];
			Assert.AreEqual(60, proof.ManifestEscrowBefore);
			Assert.AreEqual(25, proof.ManifestEscrowDebit);
			Assert.AreEqual(35, proof.ManifestEscrowAfter);
			Assert.AreEqual(KingdomTradePhysicalState.Proved, proof.ManifestEscrowState);
			Assert.AreEqual(0L, proof.RetainedBefore);
			Assert.AreEqual(0L, proof.RetainedDelta);
			Assert.AreEqual(0L, proof.RetainedAfter);
			Assert.AreEqual(KingdomTradePhysicalState.None, proof.RetainedState);
			Assert.AreEqual(KingdomTradeSinkState.Delivered, proof.ChronicleState);
			Assert.AreEqual(KingdomTradeSinkState.Skipped, proof.LedgerState);
			Assert.AreEqual(KingdomTradeSinkState.Delivered, proof.MessageState);
			Assert.AreEqual(KingdomTradeSinkState.Skipped, proof.DeedState);
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
			Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = book.RecentProofs[0];
			Assert.AreEqual(5L, proof.RetainedBefore);
			Assert.AreEqual(12L, proof.RetainedDelta);
			Assert.AreEqual(17L, proof.RetainedAfter);
			Assert.AreEqual(KingdomTradePhysicalState.Proved, proof.RetainedState);
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
			Assert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Quarantined, 20L, "uncertain credit"));
			Assert.AreSame(operation, book.OpenOperation);
			Assert.IsNull(book.PendingRetirement);
			Assert.AreEqual(0, book.RecentProofs.Count);
			Assert.AreEqual(KingdomTradePhase.Quarantined, operation.Phase);
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
			Assert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Quarantined, 20L, "mismatch"));
			Assert.AreSame(operation, book.OpenOperation);
			Assert.AreEqual(24, operation.ManifestEscrowDebit);
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
			Assert.NotNull(decoded.OpenOperation);
			Assert.AreEqual(25, decoded.OpenOperation.ProvedWater);
			Assert.AreEqual(25, decoded.OpenOperation.ManifestEscrowDebit);
			Assert.AreEqual(35, decoded.OpenOperation.ManifestEscrowAfter);
			Assert.AreEqual(KingdomTradePhysicalState.Lost,
				decoded.OpenOperation.ManifestEscrowState);
			Assert.AreEqual(KingdomTradePhase.Prepared, decoded.OpenOperation.Phase);
			KingdomTradeRules.Normalize(decoded);
			Assert.AreEqual(KingdomTradePhase.Quarantined, decoded.OpenOperation.Phase);
		}

		[Test]
		public void CodecDecode_PreservesPartialRetirementUntilExplicitNormalize()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestTurnback);
			PublishTurnedBackManifest(book, operation, 10);
			operation.Phase = KingdomTradePhase.RetirementReady;
			Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
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
			Assert.NotNull(decoded.OpenOperation);
			Assert.NotNull(decoded.PendingRetirement);
			Assert.IsEmpty(decoded.RecentProofs);
			Assert.AreEqual(0L, decoded.RetiredThrough);

			KingdomTradeRules.Normalize(decoded);
			Assert.IsNull(decoded.OpenOperation);
			Assert.IsNull(decoded.PendingRetirement);
			Assert.AreEqual(1L, decoded.RetiredThrough);
			Assert.AreEqual(1, decoded.RecentProofs.Count);
		}

		[Test]
		public void StructuralTradeDecode_PrecedesCoreLegacyCoexistenceGate()
		{
			string state = ReadRepoSource("Trade/KingdomTradeState.cs");
			int raw = state.IndexOf("public static KingdomTradeBook DecodeEnvelopeRaw",
				StringComparison.Ordinal);
			int rawEnd = state.IndexOf("public static byte[] EncodePayload", raw,
				StringComparison.Ordinal);
			string rawBody = state.Substring(raw, rawEnd - raw);
			Assert.IsFalse(rawBody.Contains("KingdomTradeRules.Normalize"));
			int read = state.IndexOf("public void Read(SerializationReader Reader)",
				StringComparison.Ordinal);
			int copy = state.IndexOf("CopyFrom(KingdomTradeCodec.DecodeEnvelopeRaw", read,
				StringComparison.Ordinal);
			Assert.Greater(copy, read);

			string core = ReadRepoSource("Core/KingdomSystem.cs");
			int method = core.IndexOf("private void NormalizeTradeBook()", StringComparison.Ordinal);
			int end = core.IndexOf("\n\t}\n}", method, StringComparison.Ordinal);
			string body = core.Substring(method, end - method);
			int legacy = body.IndexOf("bool hasLegacyTrade", StringComparison.Ordinal);
			int legacyBranch = body.IndexOf("if (hasLegacyTrade)", legacy,
				StringComparison.Ordinal);
			int normalize = body.IndexOf("KingdomTradeRules.Normalize(TradeBook)",
				StringComparison.Ordinal);
			Assert.Greater(legacyBranch, legacy);
			Assert.Greater(normalize, legacyBranch);
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
			Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = book.RecentProofs[0];
			book.RecentProofs.Clear();
			book.RetiredThrough = 0L;
			book.OpenOperation = operation;
			book.PendingRetirement = proof;
			KingdomTradeRules.Normalize(book);
			Assert.IsNull(book.OpenOperation);
			Assert.IsNull(book.PendingRetirement);
			Assert.AreEqual(1L, book.RetiredThrough);
			Assert.AreSame(proof, book.RecentProofs[0]);
		}

		[Test]
		public void WrongRealmPendingRetirement_QuarantinesWithoutErasure()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestTurnback);
			PublishTurnedBackManifest(book, operation, 10);
			operation.Phase = KingdomTradePhase.RetirementReady;
			Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = book.RecentProofs[0];
			book.RecentProofs.Clear();
			book.RetiredThrough = 0L;
			book.OpenOperation = operation;
			book.PendingRetirement = proof;
			proof.RealmId = new string('d', 64);
			proof.Id = KingdomTradeRules.OperationId(proof.RealmId, proof.Sequence);
			KingdomTradeRules.Normalize(book);
			Assert.AreSame(operation, book.OpenOperation);
			Assert.AreSame(proof, book.PendingRetirement);
			Assert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
		}

		[Test]
		public void AlteredPendingRetirement_CannotEraseExactOpenReceipt()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = ManifestOperation(book,
				KingdomTradeOperationKind.ManifestTurnback);
			PublishTurnedBackManifest(book, operation, 10);
			operation.Phase = KingdomTradePhase.RetirementReady;
			Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = book.RecentProofs[0];
			book.RecentProofs.Clear();
			book.RetiredThrough = 0L;
			book.OpenOperation = operation;
			book.PendingRetirement = proof;
			proof.RequestedWater = 9;
			KingdomTradeRules.Normalize(book);
			Assert.AreSame(operation, book.OpenOperation);
			Assert.AreSame(proof, book.PendingRetirement);
			Assert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
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
			Assert.AreEqual(KingdomTradeSinkState.Lost, operation.Outbox.LedgerState);
			Assert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Quarantined, 20L, null));
			Assert.AreSame(operation, book.OpenOperation);
		}

		[Test]
		public void CharterScheduleEquation_TamperQuarantinesWithoutCorrection()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.DueAfter = 16L;
			KingdomTradeRules.Normalize(book);
			Assert.AreEqual(16L, operation.DueAfter);
			Assert.AreEqual(KingdomTradePhase.Quarantined, operation.Phase);
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
			Assert.AreEqual(tampered, operation.Id);
			Assert.AreEqual(KingdomTradePhase.Quarantined, operation.Phase);
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
			Assert.AreEqual(KingdomTradeManifestStatus.Quarantined, book.Manifest.Status);
			Assert.IsTrue(book.Projections[0].Quarantined);
			Assert.AreEqual(8L, book.Manifest.OperationSequence);
			Assert.AreEqual(10L, book.Projections[0].OperationSequence);
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
			Assert.IsTrue(book.Charters[0].Quarantined);
			Assert.IsTrue(book.Charters[1].Quarantined);
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
			Assert.IsTrue(book.Projections[0].Quarantined);
			Assert.IsTrue(book.Projections[1].Quarantined);
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
			Assert.AreEqual(1, book.Charters.Count);
			Assert.AreSame(row, book.Charters[0]);
			Assert.IsTrue(row.Quarantined);
		}

		[Test]
		public void OversizedFaultEvidence_IsPreservedExactlyWhenQuarantined()
		{
			KingdomTradeBook book = Book();
			string evidence = new string('x', KingdomTradeRules.MaxTextChars + 1);
			book.SchemaFault = evidence;
			KingdomTradeRules.Normalize(book);
			Assert.AreSame(evidence, book.SchemaFault);
			Assert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
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
			Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			book.OpenOperation = operation;
			operation.DueAfter++;
			KingdomTradeRules.Normalize(book);
			Assert.AreSame(operation, book.OpenOperation);
			Assert.AreEqual(KingdomTradeSchemaState.Quarantined, book.SchemaState);
		}

		[Test]
		public void Retirement_RejectsNonterminalFsmAndIntentLanes()
		{
			KingdomTradeBook preparedBook = Book();
			KingdomTradeOperation prepared = CharterOperation(preparedBook);
			Assert.IsFalse(KingdomTradeRules.Retire(preparedBook, prepared,
				KingdomTradePhase.Terminal, 20L, null));
			Assert.AreSame(prepared, preparedBook.OpenOperation);

			KingdomTradeBook intentBook = Book();
			KingdomTradeOperation intent = CharterOperation(intentBook);
			intent.Phase = KingdomTradePhase.RetirementReady;
			intent.ProjectionState = KingdomTradePhysicalState.Intent;
			Assert.IsFalse(KingdomTradeRules.Retire(intentBook, intent,
				KingdomTradePhase.Terminal, 20L, null));
			Assert.AreSame(intent, intentBook.OpenOperation);
		}

		[Test]
		public void QuarantinedOperation_CannotRetireAsTerminal()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.Phase = KingdomTradePhase.Quarantined;
			book.Charters[0].Quarantined = true;
			book.Charters[0].NextTick = operation.DueBefore;
			Assert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			Assert.AreSame(operation, book.OpenOperation);
			Assert.IsNull(book.PendingRetirement);
			Assert.AreEqual(KingdomTradePhase.Quarantined, operation.Phase);
		}

		[Test]
		public void Retirement_RequiresExactMandatoryOutboxReceipt()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.Phase = KingdomTradePhase.RetirementReady;
			operation.Outbox = null;
			Assert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			Assert.AreSame(operation, book.OpenOperation);
			Assert.IsNull(book.PendingRetirement);
			Assert.AreEqual(0, book.RecentProofs.Count);
		}

		[Test]
		public void CharterRetirement_RequiresOneExactLawfulScheduleDisposition()
		{
			KingdomTradeBook book = Book();
			KingdomTradeOperation operation = CharterOperation(book);
			operation.Phase = KingdomTradePhase.RetirementReady;
			book.Charters[0].NextTick = operation.DueAfter + 1L;
			Assert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Terminal, 20L, null));
			Assert.AreSame(operation, book.OpenOperation);
			Assert.IsNull(book.PendingRetirement);
			Assert.AreEqual(operation.DueAfter + 1L, book.Charters[0].NextTick);
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
				Assert.IsFalse(KingdomTradeRules.Retire(directBook, direct,
					KingdomTradePhase.Terminal, 20L, null), "direct variant " + variant);
				Assert.AreSame(direct, directBook.OpenOperation, "direct variant " + variant);
				Assert.IsNull(directBook.PendingRetirement, "direct variant " + variant);

				KingdomTradeBook reloadBook = Book();
				KingdomTradeOperation reload = CharterOperation(reloadBook);
				reload.Phase = KingdomTradePhase.RetirementReady;
				CorruptTerminalCharterOutbox(reload, variant);
				KingdomTradeRules.Normalize(reloadBook);
				Assert.AreEqual(KingdomTradePhase.Quarantined, reload.Phase,
					"normalize variant " + variant);
				Assert.AreSame(reload, reloadBook.OpenOperation,
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
			Assert.AreEqual(forged.CharterId, forgedRow.Id);
			Assert.AreEqual(forged.DealKey, forgedRow.DealKey);
			Assert.AreEqual(forged.Faction, forgedRow.Faction);
			Assert.AreEqual(forged.DueAfter, forgedRow.NextTick);
			Assert.IsFalse(KingdomTradeRules.Retire(forgedBook, forged,
				KingdomTradePhase.Terminal, 20L, null));
			Assert.AreSame(forged, forgedBook.OpenOperation);

			KingdomTradeBook counterBook = Book();
			KingdomTradeOperation counter = CharterOperation(counterBook);
			counter.Phase = KingdomTradePhase.RetirementReady;
			counterBook.NextCharterSequence = counterBook.Charters[0].Sequence;
			Assert.IsFalse(KingdomTradeRules.Retire(counterBook, counter,
				KingdomTradePhase.Terminal, 20L, null));
			Assert.AreSame(counter, counterBook.OpenOperation);
		}

		[Test]
		public void EveryRetirementAssignmentCut_ConvergesAndCleansTerminalManifest()
		{
			KingdomTradeBook source = Book();
			KingdomTradeManifestState manifest;
			KingdomTradeOperation operation = TerminalDelivery(source, out manifest);
			KingdomTradeManifestState saved = KingdomTradeRules.SnapshotManifest(manifest);
			Assert.IsTrue(KingdomTradeRules.Retire(source, operation,
				KingdomTradePhase.Terminal, 20L, null));
			KingdomTradeProof proof = source.RecentProofs[0];
			Assert.IsTrue(proof.ManifestCleanup);
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
				Assert.AreEqual(KingdomTradeSchemaState.Compatible, book.SchemaState, "cut " + cut);
				Assert.IsNull(book.OpenOperation, "cut " + cut);
				Assert.IsNull(book.PendingRetirement, "cut " + cut);
				Assert.IsNull(book.Manifest, "cut " + cut);
				Assert.AreEqual(proof.Sequence, book.RetiredThrough, "cut " + cut);
				Assert.AreEqual(1, book.RecentProofs.Count, "cut " + cut);
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
				Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
					KingdomTradePhase.Terminal, 20L + i, null), "retire " + i);
			}
			Assert.AreEqual(KingdomTradeRules.MaxRecentProofs, book.RecentProofs.Count);
			long sequence = book.NextOperationSequence;
			KingdomTradeOperation next = KingdomTradeRules.NewOperation(book,
				KingdomTradeOperationKind.CharterDelivery, 100L);
			Assert.NotNull(next);
			Assert.AreEqual(sequence, next.Sequence);
			Assert.Less(book.RecentProofs.Count, KingdomTradeRules.MaxRecentProofs);
			Assert.AreEqual(1, book.CompactedProofs.Count);
			Assert.AreEqual(KingdomTradeRules.MaxRecentProofs / 2,
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
			Assert.IsFalse(KingdomTradeRules.TryPrepareExile(book, 50L,
				new string('d', 64), new List<string> { CityA, CityB }, out replacement,
				out settledTick, out failure));
			Assert.AreEqual(-1L, settledTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(book));
			Assert.IsTrue(KingdomTradeRules.TryPrepareExile(book, 50L, Realm,
				new List<string> { CityB, CityA }, out replacement, out settledTick, out failure),
				failure);
			Assert.AreEqual(50L, settledTick);
			Assert.AreNotSame(book, replacement);
			Assert.IsFalse(replacement.IdentityBound);
			Assert.AreEqual(1, replacement.Archives.Count);
			Assert.AreEqual(13L, replacement.Archives[0].RetainedEscrowDrams);
			Assert.AreEqual(0L, replacement.RetainedEscrowDrams);
			Assert.AreEqual(0L, replacement.UnattributedArchivedEscrowDrams);
			Assert.AreEqual(1, replacement.Archives[0].CharterCount);
			Assert.IsTrue(KingdomTradeRules.CanonicalSha256(
				replacement.Archives[0].AuthorityEvidenceHash));
			Assert.IsTrue(KingdomTradeRules.CanonicalSha256(
				replacement.Archives[0].ReceiptEvidenceHash));
			KingdomTradeBook roundTrip = KingdomTradeCodec.DecodeEnvelope(
				KingdomTradeCodec.EncodeEnvelope(replacement));
			Assert.AreEqual(replacement.Archives[0].ReceiptEvidenceHash,
				roundTrip.Archives[0].ReceiptEvidenceHash);
			Assert.IsTrue(KingdomTradeRules.TryAuthenticateExactExileClosedTick(roundTrip,
				Realm, new List<string> { CityA, CityB }, out long roundTripTick,
				out failure), failure);
			Assert.AreEqual(50L, roundTripTick);
			KingdomTradeBook retry;
			Assert.IsTrue(KingdomTradeRules.TryPrepareExile(replacement, 99L, Realm,
				new List<string> { CityA, CityB }, out retry, out settledTick, out failure),
				failure);
			Assert.AreEqual(50L, settledTick, "retry must recover original close tick");
			Assert.AreSame(replacement, retry);
			Assert.AreEqual(1, retry.Archives.Count);
		}

		[Test]
		public void ArchiveReceiptDigest_HasNoPublicMintingSurface()
		{
			Assert.IsNull(typeof(KingdomTradeRules).GetMethod("ArchiveReceiptDigest",
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
			Assert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			byte[] before = KingdomTradeCodec.EncodePayload(closed);
			Assert.IsTrue(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityB, CityA }, out closedTick, out failure), failure);
			Assert.AreEqual(50L, closedTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(closed));
		}

		[Test]
		public void ExactExileReceiptAuthenticator_ReboundProofKeepsOriginalTickAndRejectsDrift()
		{
			KingdomTradeBook source = Book();
			KingdomTradeBook closed;
			long closedTick;
			string failure;
			Assert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(closed, Realm,
				new[] { CityB, CityA }, out failure), failure);
			byte[] before = KingdomTradeCodec.EncodePayload(closed);
			Assert.IsTrue(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure), failure);
			Assert.AreEqual(50L, closedTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(closed));

			Assert.IsFalse(KingdomTradeRules.TryGetExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure),
				"legacy getter must remain strict-unbound");
			Assert.AreEqual(-1L, closedTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(closed));

			closed.OptionObservedTick = 51L;
			byte[] drifted = KingdomTradeCodec.EncodePayload(closed);
			Assert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure));
			Assert.AreEqual(-1L, closedTick);
			CollectionAssert.AreEqual(drifted, KingdomTradeCodec.EncodePayload(closed));
		}

		[Test]
		public void ExactExileReceiptAuthenticator_RejectsCoordinatedClosedTickForgery()
		{
			KingdomTradeBook source = Book();
			KingdomTradeBook closed;
			long closedTick;
			string failure;
			Assert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			closed.Archives[0].ClosedTick = 777L;
			closed.OptionObservedTick = 777L;
			byte[] forged = KingdomTradeCodec.EncodePayload(closed);
			Assert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure));
			Assert.AreEqual(-1L, closedTick);
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
			Assert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(fabricated,
				Realm, new List<string> { CityA, CityB }, out closedTick, out failure));
			Assert.AreEqual(-1L, closedTick);
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
			Assert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(closed, Realm,
				new[] { CityA, CityB }, out failure), failure);
			string wrong = new string('0', 64);
			Assert.AreNotEqual(closed.Archives[0].ReceiptEvidenceHash, wrong);
			closed.Archives[0].ReceiptEvidenceHash = wrong;
			byte[] before = KingdomTradeCodec.EncodePayload(closed);
			Assert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure));
			Assert.AreEqual(-1L, closedTick);
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
			Assert.IsTrue(KingdomTradeRules.TryPrepareExile(source, 50L, Realm,
				new List<string> { CityA, CityB }, out closed, out closedTick, out failure), failure);
			byte[] before = KingdomTradeCodec.EncodePayload(closed);
			Assert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, new string('d', 64) }, out closedTick, out failure));
			Assert.AreEqual(-1L, closedTick);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodePayload(closed));

			closed.Archives.Add(closed.Archives[0]);
			Assert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
				new List<string> { CityA, CityB }, out closedTick, out failure));
			StringAssert.Contains("collides", failure);
			closed.Archives.RemoveAt(1);
			closed.Archives[0].AuthorityEvidenceHash = "";
			Assert.IsFalse(KingdomTradeRules.TryAuthenticateExactExileClosedTick(closed, Realm,
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
		public void ExileMutationApis_RejectHostileEnumerableByConcreteSignature()
		{
			HostileSettlementEnumerable hostile = new HostileSettlementEnumerable();
			object[] arguments = { Book(), 50L, Realm, hostile, null, null };
			Assert.Throws<MissingMethodException>(() => typeof(KingdomTradeRules).InvokeMember(
				"TryPrepareExile", System.Reflection.BindingFlags.InvokeMethod
					| System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
				null, null, arguments));
			Assert.AreEqual(0, hostile.Enumerations);

			string source = ReadRepoSource("Trade/KingdomTrade.cs");
			int method = source.IndexOf("public static bool TryOnExile", StringComparison.Ordinal);
			int end = source.IndexOf("private static KingdomTradeBook EnsureBook", method,
				StringComparison.Ordinal);
			string body = source.Substring(method, end - method);
			StringAssert.Contains("List<string> ExactSettlementIds", body);
			Assert.IsFalse(body.Contains("IEnumerable<string> ExactSettlementIds"));
			int snapshot = body.IndexOf("KingdomTradeCodec.EncodePayload(original)",
				StringComparison.Ordinal);
			int listAccess = body.IndexOf("ExactSettlementIds.Count", StringComparison.Ordinal);
			Assert.Greater(snapshot, 0);
			Assert.Greater(listAccess, snapshot,
				"Trade bytes must be frozen before any caller-owned topology access");
			int firstTopology = body.IndexOf("System.TryExactSettlementIds(true",
				StringComparison.Ordinal);
			int coreCapture = body.IndexOf("TryCaptureExileCoreSeal", StringComparison.Ordinal);
			int prepare = body.IndexOf("KingdomTradeRules.TryPrepareExile", StringComparison.Ordinal);
			int finalTopology = body.IndexOf("System.TryExactSettlementIds(true",
				firstTopology + 1, StringComparison.Ordinal);
			int finalCore = body.IndexOf("ExactExileCoreSeal", finalTopology,
				StringComparison.Ordinal);
			int finalSeal = body.IndexOf("KingdomTradeRules.ExactAuthoritySeal", finalTopology,
				StringComparison.Ordinal);
			int publish = body.IndexOf("System.TradeBook = replacement", StringComparison.Ordinal);
			Assert.Greater(firstTopology, listAccess);
			Assert.Greater(coreCapture, firstTopology);
			Assert.Greater(prepare, coreCapture);
			Assert.Greater(finalTopology, prepare);
			Assert.Greater(finalCore, finalTopology);
			Assert.Greater(finalSeal, finalTopology);
			Assert.Less(finalCore, finalSeal);
			Assert.Greater(publish, finalSeal);
			Assert.AreEqual(publish, body.LastIndexOf("System.TradeBook = replacement",
				StringComparison.Ordinal), "Trade exile must have one publish cut");
			StringAssert.Contains("ExactSettlementTopology(liveTopology, exact)", body);
			StringAssert.Contains("ExactSettlementTopology(finalTopology, exact)", body);
			StringAssert.Contains("ReferenceEquals(System.Away, Seal.Away)", source);
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
			Assert.IsTrue(KingdomTradeRules.TryCaptureExactReferenceSeal(
				new object[] { away }, out KingdomTradeReferenceSeal seal));
			Assert.IsTrue(KingdomTradeRules.ExactReferenceSeal(new object[] { away }, seal));

			HostileAwayGraph replacement = new HostileAwayGraph
			{
				RealmId = Realm,
				SettlementId = CityB,
				ClaimedZones = new List<string> { "zone-b" },
				Nested = new HostileAwayNested { Rows = new List<string> { "row" } }
			};
			Assert.IsFalse(KingdomTradeRules.ExactReferenceSeal(
				new object[] { replacement }, seal));

			Assert.IsTrue(KingdomTradeRules.TryCaptureExactReferenceSeal(
				new object[] { away }, out seal));
			away.Nested.Rows = new List<string> { "row" };
			Assert.IsFalse(KingdomTradeRules.ExactReferenceSeal(new object[] { away }, seal));
		}

		[Test]
		public void ExactLookup_IsTriStateAndNeverChoosesCollision()
		{
			LookupRow exact;
			LookupRow row = new LookupRow { Id = Realm };
			Assert.AreEqual(KingdomTradeExactLookup.Incomplete,
				KingdomTradeRules.ResolveExactUnique<LookupRow>(null, Realm, x => x.Id, out exact));
			Assert.AreEqual(KingdomTradeExactLookup.Missing,
				KingdomTradeRules.ResolveExactUnique(new List<LookupRow>(), Realm, x => x.Id, out exact));
			Assert.AreEqual(KingdomTradeExactLookup.ExactUnique,
				KingdomTradeRules.ResolveExactUnique(new List<LookupRow> { row }, Realm,
					x => x.Id, out exact));
			Assert.AreSame(row, exact);
			Assert.AreEqual(KingdomTradeExactLookup.Ambiguous,
				KingdomTradeRules.ResolveExactUnique(new List<LookupRow>
					{ row, new LookupRow { Id = Realm } }, Realm, x => x.Id, out exact));
			Assert.IsNull(exact);
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
			Assert.AreEqual(KingdomTradeOptionAction.Disable,
				KingdomTradeRules.ObserveOption(KingdomTradeOptionState.Unknown, false));
			Assert.AreEqual(KingdomTradeOptionAction.StayDisabled,
				KingdomTradeRules.ObserveOption(KingdomTradeOptionState.Disabled, false));
			Assert.AreEqual(KingdomTradeOptionAction.EnableAndRestamp,
				KingdomTradeRules.ObserveOption(KingdomTradeOptionState.Disabled, true));
			Assert.AreEqual(KingdomTradeOptionAction.None,
				KingdomTradeRules.ObserveOption(KingdomTradeOptionState.Enabled, true));
		}

		[Test]
		public void LiveLeaseFailureBranchesReturnBeforeAuthorityMutation()
		{
			string source = ReadRepoSource("Trade/KingdomTrade.cs");
			int exile = source.IndexOf("public static bool TryOnExile", StringComparison.Ordinal);
			int reset = source.IndexOf("public static bool ResetAuthority", StringComparison.Ordinal);
			int exileEnd = source.IndexOf("private static KingdomTradeBook EnsureBook", exile,
				StringComparison.Ordinal);
			string exilePrefix = source.Substring(exile, exileEnd - exile);
			int failedLease = exilePrefix.IndexOf("if (!TryEnter(System, out lease))", StringComparison.Ordinal);
			int usingLease = exilePrefix.IndexOf("using (lease)", failedLease, StringComparison.Ordinal);
			string failureBranch = exilePrefix.Substring(failedLease, usingLease - failedLease);
			StringAssert.Contains("return false;", failureBranch);
			Assert.IsFalse(failureBranch.Contains("QuarantineBook"));
			Assert.IsFalse(failureBranch.Contains("System.TradeBook ="));

			int resetFailed = source.IndexOf("if (!TryEnter(System, out lease))", reset,
				StringComparison.Ordinal);
			int resetUsing = source.IndexOf("using (lease)", resetFailed, StringComparison.Ordinal);
			string resetBranch = source.Substring(resetFailed, resetUsing - resetFailed);
			Assert.IsFalse(resetBranch.Contains("System.TradeBook ="));
			Assert.Greater(source.IndexOf("System.TradeBook = new KingdomTradeBook()", resetUsing,
				StringComparison.Ordinal), resetUsing);
		}
	}
}
#endif
