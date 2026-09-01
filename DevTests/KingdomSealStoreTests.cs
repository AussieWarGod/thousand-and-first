#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomSealStoreTests
	{
		private sealed class FailingReplaceFileOps : IKingdomSealFileOps
		{
			private readonly IKingdomSealFileOps _inner = SystemKingdomSealFileOps.Instance;

			private readonly int _failureCall;

			private int _replaceCalls;

			public FailingReplaceFileOps(int failureCall = 1)
			{
				_failureCall = failureCall;
			}

			public bool Exists(string path)
			{
				return _inner.Exists(path);
			}

			public FileAttributes Attributes(string path)
			{
				return _inner.Attributes(path);
			}

			public long Length(string path)
			{
				return _inner.Length(path);
			}

			public string ReadAllText(string path)
			{
				return _inner.ReadAllText(path);
			}

			public void WriteAllTextDurable(string path, string text)
			{
				_inner.WriteAllTextDurable(path, text);
			}

			public void MoveNew(string source, string destination)
			{
				_inner.MoveNew(source, destination);
			}

			public void ReplaceAtomic(string source, string destination, string backup)
			{
				_replaceCalls++;
				if (_replaceCalls == _failureCall)
				{
					throw new IOException("injected replacement failure");
				}
				_inner.ReplaceAtomic(source, destination, backup);
			}

			public void DeleteIfExists(string path)
			{
				_inner.DeleteIfExists(path);
			}
		}

		private sealed class BlockingMoveNewFileOps : IKingdomSealFileOps
		{
			private readonly IKingdomSealFileOps _inner = SystemKingdomSealFileOps.Instance;

			internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim(false);

			internal readonly ManualResetEventSlim Release = new ManualResetEventSlim(false);

			public bool Exists(string path) => _inner.Exists(path);

			public FileAttributes Attributes(string path) => _inner.Attributes(path);

			public long Length(string path) => _inner.Length(path);

			public string ReadAllText(string path) => _inner.ReadAllText(path);

			public void WriteAllTextDurable(string path, string text)
			{
				_inner.WriteAllTextDurable(path, text);
			}

			public void MoveNew(string source, string destination)
			{
				Entered.Set();
				Release.Wait();
				_inner.MoveNew(source, destination);
			}

			public void ReplaceAtomic(string source, string destination, string backup)
			{
				_inner.ReplaceAtomic(source, destination, backup);
			}

			public void DeleteIfExists(string path)
			{
				_inner.DeleteIfExists(path);
			}
		}

		private static KingdomSealRecord StageRecord(string lineage, string legacy, string origin, int generation, int revision)
		{
			KingdomSealRecord record = new KingdomSealRecord();
			record.WriterVersion = "test";
			record.EngineVersion = "test";
			record.Status = KingdomSealStatus.Living;
			record.LineageId = lineage;
			record.LegacyId = legacy;
			record.OriginGameId = origin;
			record.Generation = generation;
			record.Revision = revision;
			record.WrittenTick = 10 + revision;
			record.FounderName = "Abram";
			record.RealmName = "Realm";
			record.SettlementName = "Kavvat";
			record.SettlementId = "kavvat-id";
			record.Vocation = "holding";
			record.Style = "common";
			record.GroundZoneId = "JoppaWorld.1.1.1.1.10";
			record.RegionName = "Salt";
			record.TerrainBlueprint = "TerrainSaltMarsh";
			record.FoundedTick = 1;
			record.Depth = 10;
			record.Stage = (int)GrowthStage.Camp;
			record.Population = 2;
			record.Defence = 1;
			record.StoredWater = 5;
			record.Vigour = KingdomRules.SealedVigour((GrowthStage)record.Stage, record.Population,
				record.Defence, record.StoredWater, false);
			return KingdomSealTestIdentity.Bind(record);
		}

		private static KingdomSealRecord PromotedRecord(string lineage, string legacy, string origin, int generation, int revision)
		{
			return KingdomSealRules.PromoteRetirement(KingdomSealRules.WithRetirement(
				StageRecord(lineage, legacy, origin, generation, revision)));
		}

		private static string NewRoot()
		{
			return Path.Combine(Path.GetTempPath(), "taf-seal-store-" + Guid.NewGuid().ToString("N"));
		}

		private static void DeleteRoot(string root)
		{
			if (Directory.Exists(root))
			{
				Directory.Delete(root, true);
			}
		}

		[Test]
		public void StageRoundTripPrefersLaterRevisionAndRejectsRegression()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				string failure;
				Assert.IsTrue(store.TryStage(StageRecord("dynasty", "legacy-a", "game-a", 1, 1), out failure), failure);
				Assert.IsTrue(store.TryStage(StageRecord("dynasty", "legacy-a", "game-a", 1, 2), out failure), failure);
				Assert.IsFalse(store.TryStage(StageRecord("dynasty", "legacy-a", "game-a", 1, 1), out failure));

				KingdomSealRecord read = store.ReadStage("game-a");
				Assert.IsNotNull(read);
				Assert.AreEqual(2, read.Revision);
				Assert.AreEqual("legacy-a", read.LegacyId);

				KingdomSealRecord retired = KingdomSealRules.WithRetirement(
					StageRecord("dynasty", "legacy-a", "game-a", 1, 2));
				Assert.IsTrue(store.TryStage(retired, out failure), failure);
				Assert.IsFalse(store.TryStage(StageRecord("dynasty", "legacy-a", "game-a", 1, 4), out failure));
				Assert.AreEqual(KingdomSealStatus.Retired, store.ReadStage("game-a").Status);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void StageEnumerationExcludesJournalLocksBeforeApplyingItsBound()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				string stages = Path.Combine(root, KingdomSealStore.StagesFolder);
				Directory.CreateDirectory(stages);
				for (int i = 0; i < 100; i++)
				{
					string origin = "game-" + i;
					File.WriteAllText(store.StagePath(origin, 'a'),
						StageRecord("dynasty", "legacy-" + i, origin, 1, 1).Compose());
				}
				for (int i = 0; i < 200; i++)
				{
					File.WriteAllText(Path.Combine(stages, ".journal-noise-" + i + ".lock"), "");
				}
				int refused;
				List<string> origins = store.StagedOrigins(out refused);
				Assert.AreEqual(0, refused);
				Assert.AreEqual(100, origins.Count);
				Assert.IsTrue(origins.Contains("game-0"));
				Assert.IsTrue(origins.Contains("game-99"));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void StageEnumerationReportsMalformedNamesAndOverflow()
		{
			string malformedRoot = NewRoot();
			string overflowRoot = NewRoot();
			try
			{
				string malformedStages = Path.Combine(malformedRoot, KingdomSealStore.StagesFolder);
				Directory.CreateDirectory(malformedStages);
				File.WriteAllText(Path.Combine(malformedStages, "game-bad.x.seal"), "bad");
				int refused;
				Assert.AreEqual(0, new KingdomSealStore(malformedRoot)
					.StagedOrigins(out refused).Count);
				Assert.AreEqual(1, refused);

				KingdomSealStore overflow = new KingdomSealStore(overflowRoot);
				Directory.CreateDirectory(Path.Combine(overflowRoot, KingdomSealStore.StagesFolder));
				for (int i = 0; i <= KingdomSealStore.MaxStageFilesScanned; i++)
				{
					string origin = "game-" + (i / 2);
					char slot = (i % 2 == 0) ? 'a' : 'b';
					File.WriteAllText(overflow.StagePath(origin, slot),
						StageRecord("dynasty", "legacy-" + (i / 2), origin, 1, 1).Compose());
				}
				overflow.StagedOrigins(out refused);
				Assert.Greater(refused, 0);
			}
			finally
			{
				DeleteRoot(malformedRoot);
				DeleteRoot(overflowRoot);
			}
		}

		[Test]
		public void StageEnumerationBoundsOperationalJunkAndReportsUnknownJunkSeparately()
		{
			string knownRoot = NewRoot();
			string unknownRoot = NewRoot();
			try
			{
				string knownStages = Path.Combine(knownRoot, KingdomSealStore.StagesFolder);
				Directory.CreateDirectory(knownStages);
				for (int i = 0; i <= KingdomSealStore.MaxStageFilesScanned
					+ KingdomSealStore.MaxFilesScanned; i++)
				{
					File.WriteAllText(Path.Combine(knownStages,
						".journal-bounded-" + i + ".lock"), "");
				}
				int refused;
				new KingdomSealStore(knownRoot).StagedOrigins(out refused);
				Assert.Greater(refused, 0);

				KingdomSealStore unknown = new KingdomSealStore(unknownRoot);
				string unknownStages = Path.Combine(unknownRoot, KingdomSealStore.StagesFolder);
				Directory.CreateDirectory(unknownStages);
				for (int i = 0; i < 100; i++)
				{
					string origin = "game-" + i;
					File.WriteAllText(unknown.StagePath(origin, 'a'),
						StageRecord("dynasty", "legacy-" + i, origin, 1, 1).Compose());
				}
				for (int i = 0; i < 200; i++)
				{
					File.WriteAllText(Path.Combine(unknownStages, "foreign-" + i + ".junk"), "");
				}
				List<string> origins = unknown.StagedOrigins(out refused);
				Assert.AreEqual(100, origins.Count);
				Assert.AreEqual(200, refused);
			}
			finally
			{
				DeleteRoot(knownRoot);
				DeleteRoot(unknownRoot);
			}
		}

		[Test]
		public void GenerationHandoffPublishesSuccessorToBothSlotsAndRetriesExactly()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				KingdomSealRecord previous = StageRecord("dynasty", "legacy-one", "game-same", 1, 2);
				KingdomSealRecord successor = StageRecord("dynasty", "legacy-two", "game-same", 2, 3);
				string failure;
				Assert.IsTrue(store.TryStage(previous, out failure), failure);
				Assert.IsTrue(store.TryAdvanceGeneration(previous, successor, out failure), failure);
				KingdomSealRecord recaptured = KingdomSealRules.Copy(successor);
				recaptured.SettlementName = "mutated after handoff";
				Assert.IsFalse(store.TryAdvanceGeneration(previous, recaptured, out failure));
				Assert.IsTrue(store.TryAdvanceGeneration(previous, successor, out failure), failure);
				Assert.AreEqual(successor.Compose(), ReadRecord(store.StagePath("game-same", 'a')).Compose());
				Assert.AreEqual(successor.Compose(), ReadRecord(store.StagePath("game-same", 'b')).Compose());
				Assert.AreEqual("legacy-two", store.ReadStage("game-same").LegacyId);
				Assert.IsFalse(store.TryStage(StageRecord("dynasty", "legacy-three", "game-same", 3, 4), out failure));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void GenerationHandoffRecoversHalfPublishedPair()
		{
			string root = NewRoot();
			try
			{
				KingdomSealRecord previous = StageRecord("dynasty", "legacy-one", "game-same", 1, 2);
				KingdomSealRecord successor = StageRecord("dynasty", "legacy-two", "game-same", 2, 3);
				KingdomSealStore normal = new KingdomSealStore(root);
				string failure;
				Assert.IsTrue(normal.TryStage(previous, out failure), failure);

				KingdomSealStore failing = new KingdomSealStore(root, new FailingReplaceFileOps());
				Assert.IsFalse(failing.TryAdvanceGeneration(previous, successor, out failure));
				Assert.AreEqual("legacy-two", normal.ReadStage("game-same").LegacyId);
				KingdomSealRecord recaptured = KingdomSealRules.Copy(successor);
				recaptured.SettlementName = "mutated after partial write";
				Assert.IsFalse(normal.TryAdvanceGeneration(previous, recaptured, out failure));
				Assert.IsTrue(normal.TryAdvanceGeneration(previous, successor, out failure), failure);
				Assert.AreEqual(successor.Compose(), ReadRecord(normal.StagePath("game-same", 'a')).Compose());
				Assert.AreEqual(successor.Compose(), ReadRecord(normal.StagePath("game-same", 'b')).Compose());
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void GenerationHandoffRecoversOppositeSlotOnlyFromExactSuccessor()
		{
			string root = NewRoot();
			try
			{
				KingdomSealRecord previous = StageRecord("dynasty", "legacy-one", "game-same", 1, 2);
				KingdomSealRecord successor = StageRecord("dynasty", "legacy-two", "game-same", 2, 3);
				KingdomSealStore normal = new KingdomSealStore(root);
				Directory.CreateDirectory(Path.Combine(root, KingdomSealStore.StagesFolder));
				File.WriteAllText(normal.StagePath("game-same", 'b'), previous.Compose());
				string failure;
				Assert.IsFalse(new KingdomSealStore(root, new FailingReplaceFileOps())
					.TryAdvanceGeneration(previous, successor, out failure));

				KingdomSealRecord recaptured = KingdomSealRules.Copy(successor);
				recaptured.SettlementName = "same version, different facts";
				Assert.IsFalse(normal.TryAdvanceGeneration(previous, recaptured, out failure));
				Assert.IsTrue(normal.TryAdvanceGeneration(previous, successor, out failure), failure);
				Assert.AreEqual(successor.Compose(), ReadRecord(normal.StagePath("game-same", 'a')).Compose());
				Assert.AreEqual(successor.Compose(), ReadRecord(normal.StagePath("game-same", 'b')).Compose());
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void GenerationCompletionUsesExactDurableSuccessorAndRejectsRecapture()
		{
			string root = NewRoot();
			try
			{
				KingdomSealRecord previous = StageRecord("dynasty", "legacy-one", "game-same", 1, 2);
				KingdomSealRecord successor = StageRecord("dynasty", "legacy-two", "game-same", 2, 3);
				KingdomSealStore normal = new KingdomSealStore(root);
				string failure;
				Assert.IsTrue(normal.TryStage(previous, out failure), failure);
				Assert.IsFalse(new KingdomSealStore(root, new FailingReplaceFileOps())
					.TryAdvanceGeneration(previous, successor, out failure));

				KingdomSealRecord durable = normal.ReadStage("game-same");
				KingdomSealRecord recaptured = KingdomSealRules.Copy(durable);
				recaptured.SettlementName = "changed after reload";
				Assert.IsFalse(normal.TryCompleteGenerationAdvance(recaptured, out failure));
				Assert.IsTrue(normal.TryCompleteGenerationAdvance(durable, out failure), failure);
				Assert.IsTrue(normal.TryCompleteGenerationAdvance(durable, out failure), failure);
				Assert.AreEqual(durable.Compose(), ReadRecord(normal.StagePath("game-same", 'a')).Compose());
				Assert.AreEqual(durable.Compose(), ReadRecord(normal.StagePath("game-same", 'b')).Compose());
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void AdjacentGenerationPairWithRegressedWrittenTickIsNotRecoverable()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				Directory.CreateDirectory(Path.Combine(root, KingdomSealStore.StagesFolder));
				KingdomSealRecord older = StageRecord("dynasty", "legacy-one", "game-same", 1, 2);
				older.WrittenTick = 100;
				KingdomSealRecord newer = StageRecord("dynasty", "legacy-two", "game-same", 2, 3);
				newer.WrittenTick = 99;
				File.WriteAllText(store.StagePath("game-same", 'a'), older.Compose());
				File.WriteAllText(store.StagePath("game-same", 'b'), newer.Compose());
				Assert.IsNull(store.ReadStage("game-same"));
				string failure;
				Assert.IsFalse(store.TryCompleteGenerationAdvance(newer, out failure));
				Assert.IsFalse(store.TryRestoreLivingGeneration(older, out failure));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void ConcurrentGenerationHandoffsCannotPublishTwoLegacyIds()
		{
			string root = NewRoot();
			try
			{
				KingdomSealRecord previous = StageRecord("dynasty", "legacy-one", "game-same", 1, 2);
				KingdomSealRecord successorA = StageRecord("dynasty", "legacy-two-a", "game-same", 2, 3);
				KingdomSealRecord successorB = StageRecord("dynasty", "legacy-two-b", "game-same", 2, 3);
				string failure;
				Assert.IsTrue(new KingdomSealStore(root).TryStage(previous, out failure), failure);
				ManualResetEventSlim start = new ManualResetEventSlim(false);
				bool first = false;
				bool second = false;
				Task a = Task.Run(delegate
				{
					start.Wait();
					string handoffFailure;
					first = new KingdomSealStore(root).TryAdvanceGeneration(previous, successorA, out handoffFailure);
				});
				Task b = Task.Run(delegate
				{
					start.Wait();
					string handoffFailure;
					second = new KingdomSealStore(root).TryAdvanceGeneration(previous, successorB, out handoffFailure);
				});
				start.Set();
				Task.WaitAll(a, b);
				Assert.AreEqual(1, (first ? 1 : 0) + (second ? 1 : 0));
				Assert.AreEqual(first ? "legacy-two-a" : "legacy-two-b",
					new KingdomSealStore(root).ReadStage("game-same").LegacyId);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void GenerationHandoffAllowsRetiredButRejectsSkipRegressionAndCollision()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				KingdomSealRecord living = StageRecord("dynasty", "legacy-one", "game-same", 1, 1);
				KingdomSealRecord retired = KingdomSealRules.WithRetirement(living);
				string failure;
				Assert.IsTrue(store.TryStage(living, out failure), failure);
				Assert.IsTrue(store.TryStage(retired, out failure), failure);

				Assert.IsFalse(store.TryAdvanceGeneration(retired,
					StageRecord("dynasty", "legacy-one", "game-same", 2, 3), out failure));
				Assert.IsFalse(store.TryAdvanceGeneration(retired,
					StageRecord("dynasty", "legacy-three", "game-same", 3, 3), out failure));
				Assert.IsFalse(store.TryAdvanceGeneration(retired,
					StageRecord("dynasty", "legacy-two", "game-same", 2, 4), out failure));
				Assert.IsFalse(store.TryAdvanceGeneration(retired,
					StageRecord("other", "legacy-two", "game-same", 2, 3), out failure));
				Assert.IsFalse(store.TryAdvanceGeneration(retired,
					StageRecord("dynasty", "legacy-two", "game-other", 2, 3), out failure));
				KingdomSealRecord terminal = KingdomSealRules.WithTerminalCause(living, "fell", "combat", 9);
				Assert.IsFalse(store.TryAdvanceGeneration(terminal,
					StageRecord("dynasty", "legacy-two", "game-same", 2, 3), out failure));

				KingdomSealRecord successor = StageRecord("dynasty", "legacy-two", "game-same", 2, 3);
				Assert.IsTrue(store.TryAdvanceGeneration(retired, successor, out failure), failure);
				Assert.AreEqual(2, store.ReadStage("game-same").Generation);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void PrimaryLivingGenerationRestoresOverUncommittedSuccessorAndRetryIsExact()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				KingdomSealRecord saved = StageRecord("dynasty", "legacy-one", "game-same", 1, 2);
				KingdomSealRecord successor = StageRecord("dynasty", "legacy-two", "game-same", 2, 3);
				string failure;
				Assert.IsTrue(store.TryStage(saved, out failure), failure);
				Assert.IsTrue(store.TryAdvanceGeneration(saved, successor, out failure), failure);
				Assert.IsTrue(store.TryRestoreLivingGeneration(saved, out failure), failure);
				Assert.IsTrue(store.TryRestoreLivingGeneration(saved, out failure), failure);
				Assert.AreEqual(saved.Compose(), ReadRecord(store.StagePath("game-same", 'a')).Compose());
				Assert.AreEqual(saved.Compose(), ReadRecord(store.StagePath("game-same", 'b')).Compose());
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void PrimaryRestoreRecoversHalfWriteAndRefusesTerminalStage()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore normal = new KingdomSealStore(root);
				KingdomSealRecord saved = StageRecord("dynasty", "legacy-one", "game-same", 1, 2);
				KingdomSealRecord successor = StageRecord("dynasty", "legacy-two", "game-same", 2, 3);
				string failure;
				Assert.IsTrue(normal.TryStage(saved, out failure), failure);
				Assert.IsTrue(normal.TryAdvanceGeneration(saved, successor, out failure), failure);

				KingdomSealStore failing = new KingdomSealStore(root, new FailingReplaceFileOps(2));
				Assert.IsFalse(failing.TryRestoreLivingGeneration(saved, out failure));
				Assert.AreEqual("legacy-two", normal.ReadStage("game-same").LegacyId);
				Assert.IsTrue(normal.TryRestoreLivingGeneration(saved, out failure), failure);

				KingdomSealRecord terminal = KingdomSealRules.WithTerminalCause(saved, "fell", "combat", 9);
				Assert.IsTrue(normal.TryStage(terminal, out failure), failure);
				Assert.IsFalse(normal.TryRestoreLivingGeneration(saved, out failure));
				Assert.AreEqual(KingdomSealStatus.Terminal, normal.ReadStage("game-same").Status);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void PrimaryRestoreAllowsNewerLivingButRejectsCollisionAndMalformedJournal()
		{
			string newerRoot = NewRoot();
			string collisionRoot = NewRoot();
			string malformedRoot = NewRoot();
			try
			{
				KingdomSealRecord saved = StageRecord("dynasty", "legacy-one", "game-same", 1, 2);
				string failure;
				KingdomSealStore newer = new KingdomSealStore(newerRoot);
				Assert.IsTrue(newer.TryStage(StageRecord("dynasty", "legacy-four", "game-same", 4, 8), out failure), failure);
				Assert.IsTrue(newer.TryRestoreLivingGeneration(saved, out failure), failure);
				Assert.AreEqual(saved.Compose(), ReadRecord(newer.StagePath("game-same", 'a')).Compose());
				Assert.AreEqual(saved.Compose(), ReadRecord(newer.StagePath("game-same", 'b')).Compose());

				KingdomSealStore collision = new KingdomSealStore(collisionRoot);
				Assert.IsTrue(collision.TryStage(StageRecord("dynasty", "legacy-other", "game-same", 1, 2), out failure), failure);
				Assert.IsFalse(collision.TryRestoreLivingGeneration(saved, out failure));

				KingdomSealStore malformed = new KingdomSealStore(malformedRoot);
				Assert.IsTrue(malformed.TryStage(StageRecord("dynasty", "legacy-three", "game-same", 3, 6), out failure), failure);
				File.WriteAllText(malformed.StagePath("game-same", 'b'),
					StageRecord("dynasty", "legacy-five", "game-same", 5, 8).Compose());
				Assert.IsFalse(malformed.TryRestoreLivingGeneration(saved, out failure));
			}
			finally
			{
				DeleteRoot(newerRoot);
				DeleteRoot(collisionRoot);
				DeleteRoot(malformedRoot);
			}
		}

		[Test]
		public void RetiredPrimaryRestoresTornSuccessorHandoffOnlyWithImmutableProof()
		{
			string root = NewRoot();
			string noProofRoot = NewRoot();
			try
			{
				KingdomSealRecord living = StageRecord("dynasty", "legacy-retired", "game-same", 1, 2);
				KingdomSealRecord retired = KingdomSealRules.WithRetirement(living);
				KingdomSealRecord promoted = KingdomSealRules.PromoteRetirement(retired);
				KingdomSealRecord successor = StageRecord("dynasty", "legacy-next", "game-same", 2, 4);
				KingdomSealStore normal = new KingdomSealStore(root);
				string failure;
				Assert.IsTrue(normal.TryStage(living, out failure), failure);
				Assert.IsTrue(normal.TryStage(retired, out failure), failure);
				Assert.IsTrue(normal.TryWriteLegacy(promoted, out failure), failure);
				Assert.IsFalse(new KingdomSealStore(root, new FailingReplaceFileOps(2))
					.TryAdvanceGeneration(retired, successor, out failure));
				Assert.AreEqual("legacy-next", normal.ReadStage("game-same").LegacyId);

				KingdomSealLineage saved = new KingdomSealLineage(retired.LineageId,
					retired.LegacyId, retired.OriginGameId, retired.Generation, retired.Revision);
				Assert.IsTrue(normal.TryRestoreRetiredGeneration(saved, out failure), failure);
				Assert.IsTrue(normal.TryRestoreRetiredGeneration(saved, out failure), failure);
				Assert.AreEqual(retired.Compose(), ReadRecord(normal.StagePath("game-same", 'a')).Compose());
				Assert.AreEqual(retired.Compose(), ReadRecord(normal.StagePath("game-same", 'b')).Compose());

				KingdomSealStore noProof = new KingdomSealStore(noProofRoot);
				Assert.IsTrue(noProof.TryStage(living, out failure), failure);
				Assert.IsTrue(noProof.TryStage(retired, out failure), failure);
				Assert.IsFalse(noProof.TryRestoreRetiredGeneration(saved, out failure));
			}
			finally
			{
				DeleteRoot(root);
				DeleteRoot(noProofRoot);
			}
		}

		[Test]
		public void LegacyFilesUseGenerationIdentityNotDynastyIdentity()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				string failure;
				KingdomSealRecord founder = PromotedRecord("dynasty", "legacy-founder", "game-founder", 0, 1);
				KingdomSealRecord heir = PromotedRecord("dynasty", "legacy-heir", "game-heir", 1, 1);
				Assert.IsTrue(store.TryWriteLegacy(founder, out failure), failure);
				Assert.IsTrue(store.TryWriteLegacy(heir, out failure), failure);
				Assert.IsTrue(File.Exists(store.LegacyPath("legacy-founder")));
				Assert.IsTrue(File.Exists(store.LegacyPath("legacy-heir")));
				Assert.IsFalse(File.Exists(store.LegacyPath("dynasty")));

				int refused;
				List<KingdomSealRecord> records = store.ReadLegacies(out refused);
				Assert.AreEqual(0, refused);
				Assert.AreEqual(2, records.Count);
				Assert.AreEqual("dynasty", records[0].LineageId);
				Assert.AreEqual("dynasty", records[1].LineageId);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void LegacyCreationIsIdempotentAndNeverOverwritesConcurrently()
		{
			string root = NewRoot();
			try
			{
				KingdomSealRecord one = PromotedRecord("dynasty", "legacy-race", "game-one", 1, 1);
				KingdomSealRecord two = PromotedRecord("dynasty", "legacy-race", "game-two", 1, 1);
				ManualResetEventSlim start = new ManualResetEventSlim(false);
				bool first = false;
				bool second = false;
				Task a = Task.Run(delegate
				{
					start.Wait();
					string failure;
					first = new KingdomSealStore(root).TryWriteLegacy(one, out failure);
				});
				Task b = Task.Run(delegate
				{
					start.Wait();
					string failure;
					second = new KingdomSealStore(root).TryWriteLegacy(two, out failure);
				});
				start.Set();
				Task.WaitAll(a, b);
				Assert.AreEqual(1, (first ? 1 : 0) + (second ? 1 : 0));

				KingdomSealStore store = new KingdomSealStore(root);
				int refused;
				List<KingdomSealRecord> records = store.ReadLegacies(out refused);
				Assert.AreEqual(0, refused);
				Assert.AreEqual(1, records.Count);
				KingdomSealRecord winner = first ? one : two;
				string retryFailure;
				Assert.IsTrue(store.TryWriteLegacy(winner, out retryFailure), retryFailure);
				Assert.AreEqual(winner.OriginGameId, records[0].OriginGameId);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void LegacyPublicationLockRefusesASecondWriterBeforeInstall()
		{
			string root = NewRoot();
			BlockingMoveNewFileOps blocking = new BlockingMoveNewFileOps();
			Task<bool> first = null;
			try
			{
				KingdomSealRecord one = PromotedRecord("dynasty", "legacy-gate", "game-one", 1, 1);
				KingdomSealRecord two = PromotedRecord("dynasty", "legacy-gate", "game-two", 1, 1);
				first = Task.Run(delegate
				{
					string failure;
					return new KingdomSealStore(root, blocking).TryWriteLegacy(one, out failure);
				});
				Assert.IsTrue(blocking.Entered.Wait(TimeSpan.FromSeconds(5)),
					"first writer never reached its atomic install");

				string secondFailure;
				Assert.IsFalse(new KingdomSealStore(root).TryWriteLegacy(two, out secondFailure));
				StringAssert.Contains("publication lock", secondFailure);

				blocking.Release.Set();
				Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)),
					"first writer did not leave its atomic install");
				Assert.IsTrue(first.GetAwaiter().GetResult());
				KingdomSealRecord stored = ReadRecord(
					new KingdomSealStore(root).LegacyPath("legacy-gate"));
				Assert.AreEqual(one.OriginGameId, stored.OriginGameId);
			}
			finally
			{
				blocking.Release.Set();
				bool writerStopped = first == null || first.IsCompleted;
				if (!writerStopped)
				{
					try
					{
						writerStopped = first.Wait(TimeSpan.FromSeconds(5));
					}
					catch (AggregateException)
					{
						writerStopped = true;
					}
				}
				if (writerStopped)
				{
					blocking.Entered.Dispose();
					blocking.Release.Dispose();
					DeleteRoot(root);
				}
			}
		}

		[Test]
		public void ReceiptDeclarationsAndCanonicalWireStayExact()
		{
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomSealReceiptState)));
			Assert.AreEqual("0:Reserved,1:Committed,2:Declined",
				string.Join(",", Array.ConvertAll((KingdomSealReceiptState[])Enum.GetValues(
					typeof(KingdomSealReceiptState)), value => ((int)value) + ":" + value)));

			Type receiptType = typeof(KingdomSealReceipt);
			Assert.IsTrue(receiptType.IsNotPublic);
			Assert.IsTrue(receiptType.IsSealed);
			string[] fields = new string[]
				{ "LineageId", "LegacyId", "TargetGameId", "State", "WrittenTick" };
			Type[] fieldTypes = new Type[]
				{ typeof(string), typeof(string), typeof(string), typeof(KingdomSealReceiptState), typeof(long) };
			Assert.AreEqual(fields.Length, receiptType.GetFields(
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Length);
			for (int i = 0; i < fields.Length; i++)
			{
				System.Reflection.FieldInfo field = receiptType.GetField(fields[i],
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
				Assert.IsNotNull(field, fields[i]);
				Assert.AreEqual(fieldTypes[i], field.FieldType, fields[i]);
			}

			KingdomSealReceipt defaults = new KingdomSealReceipt();
			Assert.AreEqual("", defaults.LineageId);
			Assert.AreEqual("", defaults.LegacyId);
			Assert.AreEqual("", defaults.TargetGameId);
			Assert.AreEqual(KingdomSealReceiptState.Reserved, defaults.State);
			Assert.AreEqual(0L, defaults.WrittenTick);

			KingdomSealReceipt receipt = new KingdomSealReceipt
			{
				LineageId = "dynasty",
				LegacyId = "legacy",
				TargetGameId = "target",
				State = KingdomSealReceiptState.Reserved,
				WrittenTick = 1
			};
			const string expected = "taf-seal 6\nsha256 41e278d968db5766d26f22cdda991100d8c35734eefd1c551922a2c6c23911df\nlength 105\n{\"kind\":\"receipt\",\"lineage\":\"dynasty\",\"legacy\":\"legacy\",\"target\":\"target\",\"state\":\"reserved\",\"written\":1}\n";
			Assert.AreEqual(expected, receipt.Compose());

			KingdomSealReceipt parsed;
			const string schemaFive = "taf-seal 5\nsha256 41e278d968db5766d26f22cdda991100d8c35734eefd1c551922a2c6c23911df\nlength 105\n{\"kind\":\"receipt\",\"lineage\":\"dynasty\",\"legacy\":\"legacy\",\"target\":\"target\",\"state\":\"reserved\",\"written\":1}\n";
			Assert.IsTrue(KingdomSealReceipt.TryParse(schemaFive, out parsed));
			Assert.AreEqual(schemaFive, parsed.Compose());
			const string schemaFour = "taf-seal 4\nsha256 41e278d968db5766d26f22cdda991100d8c35734eefd1c551922a2c6c23911df\nlength 105\n{\"kind\":\"receipt\",\"lineage\":\"dynasty\",\"legacy\":\"legacy\",\"target\":\"target\",\"state\":\"reserved\",\"written\":1}\n";
			Assert.IsTrue(KingdomSealReceipt.TryParse(schemaFour,
				out KingdomSealReceipt schemaFourParsed));
			Assert.AreEqual(schemaFour, schemaFourParsed.Compose());
			Assert.AreEqual("dynasty", parsed.LineageId);
			Assert.AreEqual("legacy", parsed.LegacyId);
			Assert.AreEqual("target", parsed.TargetGameId);
			Assert.AreEqual(KingdomSealReceiptState.Reserved, parsed.State);
			Assert.AreEqual(1L, parsed.WrittenTick);
		}

		[Test]
		public void ReceiptParserRejectsWrongKindEmptyTargetAndWrongWrittenKind()
		{
			KingdomSealBody wrongKind = ReceiptBody("record", "target", false);
			KingdomSealBody emptyTarget = ReceiptBody("receipt", "", false);
			KingdomSealBody wrongWritten = ReceiptBody("receipt", "target", true);
			KingdomSealReceipt receipt;
			Assert.IsFalse(KingdomSealReceipt.TryParse(KingdomSealFormat.Compose(
				KingdomSealRecord.CurrentSchema, wrongKind), out receipt));
			Assert.IsFalse(KingdomSealReceipt.TryParse(KingdomSealFormat.Compose(
				KingdomSealRecord.CurrentSchema, emptyTarget), out receipt));
			Assert.IsFalse(KingdomSealReceipt.TryParse(KingdomSealFormat.Compose(
				KingdomSealRecord.CurrentSchema, wrongWritten), out receipt));
			Assert.IsTrue(KingdomSealReceipt.ValidId("safe-ID_2"));
			Assert.IsFalse(KingdomSealReceipt.ValidId("unsafe.id"));
			Assert.IsFalse(KingdomSealReceipt.ValidId("unsafe:id"));
			Assert.IsFalse(KingdomSealReceipt.ValidId("trailing."));
		}

		[Test]
		public void StoreRejectsUnsafeFilesystemIdentitiesBeforeWriting()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				string failure;
				Assert.IsFalse(store.TryStage(
					StageRecord("dynasty", "legacy-safe", "game.aliased", 1, 1), out failure));
				Assert.IsFalse(store.TryWriteLegacy(
					PromotedRecord("dynasty", "legacy.aliased", "game-safe", 1, 1), out failure));

				KingdomSealRecord safe = PromotedRecord("dynasty", "legacy-safe", "game-safe", 1, 1);
				Assert.IsTrue(store.TryWriteLegacy(safe, out failure), failure);
				KingdomSealReceipt receipt;
				KingdomSealReservationLease lease;
				Assert.IsFalse(store.TryClaimReservation(safe, "target:stream", 1,
					out receipt, out lease, out failure));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void StoreRejectsRedirectedRootAndEveryFixedDirectoryWithoutTraversal()
		{
			string rootLink = NewRoot();
			string rootTarget = NewRoot();
			string fixedRoot = NewRoot();
			string fixedTarget = NewRoot();
			try
			{
				Directory.CreateDirectory(rootTarget);
				if (!TryDirectoryLink(rootLink, rootTarget)) return;
				string failure;
				Assert.IsFalse(new KingdomSealStore(rootLink).TryStage(
					StageRecord("dynasty", "legacy", "game", 1, 1), out failure));
				Assert.IsFalse(Directory.Exists(Path.Combine(rootTarget,
					KingdomSealStore.StagesFolder)));
				DeleteLink(rootLink);

				string[] folders = new[] { KingdomSealStore.StagesFolder,
					KingdomSealStore.LegaciesFolder, KingdomSealStore.ReceiptsFolder,
					KingdomSealStore.ClaimsFolder };
				for (int i = 0; i < folders.Length; i++)
				{
					string root = fixedRoot + "-" + i;
					string target = fixedTarget + "-" + i;
					Directory.CreateDirectory(root);
					Directory.CreateDirectory(target);
					string link = Path.Combine(root, folders[i]);
					Assert.IsTrue(TryDirectoryLink(link, target));
					KingdomSealStore store = new KingdomSealStore(root);
					if (folders[i] == KingdomSealStore.StagesFolder)
					{
						Assert.IsFalse(store.TryStage(StageRecord("dynasty", "legacy", "game", 1, 1),
							out failure));
					}
					else if (folders[i] == KingdomSealStore.LegaciesFolder)
					{
						Assert.IsFalse(store.TryWriteLegacy(PromotedRecord("dynasty", "legacy", "game", 1, 1),
							out failure));
					}
					else
					{
						int refused;
						store.ReadReceipts(out refused);
						if (folders[i] == KingdomSealStore.ReceiptsFolder) Assert.Greater(refused, 0);
						else
						{
							KingdomSealRecord legacy = PromotedRecord("dynasty", "legacy", "game", 1, 1);
							Directory.CreateDirectory(Path.Combine(root, KingdomSealStore.LegaciesFolder));
							File.WriteAllText(store.LegacyPath("legacy"), legacy.Compose());
							KingdomSealReceipt receipt;
							KingdomSealReservationLease lease;
							Assert.IsFalse(store.TryClaimReservation(legacy, "target", 1,
								out receipt, out lease, out failure));
						}
					}
					Assert.AreEqual(0, Directory.GetFileSystemEntries(target).Length);
					DeleteLink(link);
					DeleteRoot(root);
					DeleteRoot(target);
				}
			}
			finally
			{
				DeleteLink(rootLink);
				DeleteRoot(rootTarget);
				for (int i = 0; i < 4; i++)
				{
					DeleteRoot(fixedRoot + "-" + i);
					DeleteRoot(fixedTarget + "-" + i);
				}
			}
		}

		[Test]
		public void StoreRejectsRedirectedDataReceiptClaimAndLockLeaves()
		{
			string root = NewRoot();
			string targetRoot = NewRoot();
			try
			{
				Directory.CreateDirectory(root);
				Directory.CreateDirectory(targetRoot);
				string target = Path.Combine(targetRoot, "sentinel.txt");
				File.WriteAllText(target, "untouched");
				string probe = Path.Combine(root, "probe.link");
				if (!TryFileLink(probe, target)) return;
				DeleteLink(probe);

				KingdomSealStore store = new KingdomSealStore(root);
				string failure;
				Directory.CreateDirectory(Path.Combine(root, KingdomSealStore.StagesFolder));
				string stage = store.StagePath("game-stage", 'a');
				Assert.IsTrue(TryFileLink(stage, target));
				Assert.IsFalse(store.TryStage(StageRecord("dynasty", "legacy-stage", "game-stage", 1, 1),
					out failure));
				Assert.IsNull(store.ReadStage("game-stage"));
				int refused;
				store.StagedOrigins(out refused);
				Assert.Greater(refused, 0);
				DeleteLink(stage);

				string stageLock = Path.Combine(root, KingdomSealStore.StagesFolder,
					".journal-game-lock.lock");
				Assert.IsTrue(TryFileLink(stageLock, target));
				Assert.IsFalse(store.TryStage(StageRecord("dynasty", "legacy-lock", "game-lock", 1, 1),
					out failure));
				DeleteLink(stageLock);

				Directory.CreateDirectory(Path.Combine(root, KingdomSealStore.LegaciesFolder));
				string legacyLeaf = store.LegacyPath("legacy-leaf");
				Assert.IsTrue(TryFileLink(legacyLeaf, target));
				Assert.IsFalse(store.TryWriteLegacy(PromotedRecord("dynasty", "legacy-leaf", "game", 1, 1),
					out failure));
				DeleteLink(legacyLeaf);

				KingdomSealRecord legacy = PromotedRecord("dynasty", "legacy-good", "game", 1, 1);
				Assert.IsTrue(store.TryWriteLegacy(legacy, out failure), failure);
				Directory.CreateDirectory(Path.Combine(root, KingdomSealStore.ReceiptsFolder));
				string receiptLeaf = store.ReceiptPath("legacy-good", "target-receipt");
				Assert.IsTrue(TryFileLink(receiptLeaf, target));
				KingdomSealReceipt receipt;
				KingdomSealReservationLease lease;
				Assert.IsFalse(store.TryClaimReservation(legacy, "target-receipt", 1,
					out receipt, out lease, out failure));
				DeleteLink(receiptLeaf);

				string receiptLock = Path.Combine(root, KingdomSealStore.ReceiptsFolder, ".claims.lock");
				// The preceding claim safely created the ordinary persistent mutex leaf before it
				// rejected the redirected receipt. Remove that regular test-owned leaf so this
				// next case can independently replace the same pathname with a hostile link.
				Assert.IsTrue(File.Exists(receiptLock));
				Assert.IsFalse((File.GetAttributes(receiptLock) & FileAttributes.ReparsePoint) != 0);
				File.Delete(receiptLock);
				Assert.IsTrue(TryFileLink(receiptLock, target));
				Assert.IsFalse(store.TryClaimReservation(legacy, "target-lock", 1,
					out receipt, out lease, out failure));
				DeleteLink(receiptLock);

				Directory.CreateDirectory(Path.Combine(root, KingdomSealStore.ClaimsFolder));
				string claimLeaf = Path.Combine(root, KingdomSealStore.ClaimsFolder,
					Path.GetFileName(store.ReceiptPath("legacy-good", "target-live")) + ".live");
				Assert.IsTrue(TryFileLink(claimLeaf, target));
				Assert.IsFalse(store.TryClaimReservation(legacy, "target-live", 1,
					out receipt, out lease, out failure));
				Assert.AreEqual("untouched", File.ReadAllText(target));
				DeleteLink(claimLeaf);
			}
			finally
			{
				DeleteRoot(root);
				DeleteRoot(targetRoot);
			}
		}

		[Test]
		public void ReceiptFilenameTupleMustMatchBody()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				Directory.CreateDirectory(Path.Combine(root, KingdomSealStore.ReceiptsFolder));
				KingdomSealReceipt receipt = new KingdomSealReceipt
				{
					LineageId = "dynasty",
					LegacyId = "legacy-body",
					TargetGameId = "target-body",
					State = KingdomSealReceiptState.Reserved,
					WrittenTick = 1
				};
				File.WriteAllText(store.ReceiptPath("legacy-filename", "target-body"), receipt.Compose());
				File.WriteAllText(Path.Combine(root, KingdomSealStore.ReceiptsFolder, "bad-tuple.receipt"), receipt.Compose());
				int refused;
				Assert.AreEqual(0, store.ReadReceipts(out refused).Count);
				Assert.AreEqual(2, refused);

				KingdomSealRecord legacy = PromotedRecord("dynasty", "legacy-new", "game-new", 1, 1);
				string failure;
				Assert.IsTrue(store.TryWriteLegacy(legacy, out failure), failure);
				KingdomSealReceipt claimed;
				KingdomSealReservationLease lease;
				Assert.IsFalse(store.TryClaimReservation(legacy, "target-new", 1,
					out claimed, out lease, out failure));
				Assert.IsTrue(failure.Length > 0);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void ReceiptReadReportsFolderOverflow()
		{
			string root = NewRoot();
			try
			{
				string receipts = Path.Combine(root, KingdomSealStore.ReceiptsFolder);
				Directory.CreateDirectory(receipts);
				for (int i = 0; i <= KingdomSealStore.MaxFilesScanned; i++)
				{
					File.WriteAllText(Path.Combine(receipts, "malformed-" + i + ".receipt"), "not a seal");
				}
				int refused;
				Assert.AreEqual(0, new KingdomSealStore(root).ReadReceipts(out refused).Count);
				Assert.Greater(refused, KingdomSealStore.MaxFilesScanned);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void ExclusiveReservationAllowsExactlyOneTargetAndRetryIsIdempotent()
		{
			string root = NewRoot();
			try
			{
				KingdomSealRecord legacy = PromotedRecord("dynasty", "legacy-claim", "game-origin", 1, 1);
				string failure;
				Assert.IsTrue(new KingdomSealStore(root).TryWriteLegacy(legacy, out failure), failure);
				ManualResetEventSlim start = new ManualResetEventSlim(false);
				bool first = false;
				bool second = false;
				KingdomSealReservationLease firstLease = null;
				KingdomSealReservationLease secondLease = null;
				Task a = Task.Run(delegate
				{
					start.Wait();
					KingdomSealReceipt receipt;
					string claimFailure;
					first = new KingdomSealStore(root).TryClaimReservation(legacy, "target-one", 1,
						out receipt, out firstLease, out claimFailure);
				});
				Task b = Task.Run(delegate
				{
					start.Wait();
					KingdomSealReceipt receipt;
					string claimFailure;
					second = new KingdomSealStore(root).TryClaimReservation(legacy, "target-two", 1,
						out receipt, out secondLease, out claimFailure);
				});
				start.Set();
				Task.WaitAll(a, b);
				Assert.AreEqual(1, (first ? 1 : 0) + (second ? 1 : 0));

				string winner = first ? "target-one" : "target-two";
				string loser = first ? "target-two" : "target-one";
				(first ? firstLease : secondLease).Dispose();
				KingdomSealReceipt retried;
				KingdomSealReservationLease retryLease;
				Assert.IsTrue(new KingdomSealStore(root).TryClaimReservation(legacy, winner, 2,
					out retried, out retryLease, out failure), failure);
				Assert.AreEqual(KingdomSealReceiptState.Reserved, retried.State);
				KingdomSealReservationLease loserLease;
				Assert.IsFalse(new KingdomSealStore(root).TryClaimReservation(legacy, loser, 2,
					out retried, out loserLease, out failure));
				Assert.AreEqual(1, new KingdomSealStore(root).ReadReceipts().Count);
				retryLease.Dispose();
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void ReceiptLifecycleIsMonotoneAndSpentKeysLegacyNotLineage()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				KingdomSealRecord legacy = PromotedRecord("dynasty", "legacy-used", "game-origin", 1, 1);
				string failure;
				Assert.IsTrue(store.TryWriteLegacy(legacy, out failure), failure);
				KingdomSealReceipt reserved;
				KingdomSealReservationLease lease;
				Assert.IsTrue(store.TryClaimReservation(legacy, "target", 1,
					out reserved, out lease, out failure), failure);
				Assert.IsFalse(store.SpentLegacyIds().Contains("legacy-used"));

				KingdomSealReceipt requested = CopyReceipt(reserved, KingdomSealReceiptState.Committed, 2);
				Assert.IsFalse(store.TryWriteReceipt(requested, out failure));
				KingdomSealReceipt committed;
				Assert.IsTrue(store.TryCommitReservation(reserved, lease, 2,
					out committed, out failure), failure);
				Assert.IsFalse(lease.IsHeld);
				KingdomSealReceipt inspected;
				Assert.IsTrue(store.TryInspectReceipt(reserved, out inspected, out failure), failure);
				Assert.AreEqual(committed.Compose(), inspected.Compose());
				Assert.IsTrue(store.SpentLegacyIds().Contains("legacy-used"));
				Assert.IsFalse(store.SpentLegacyIds().Contains("dynasty"));
				Assert.IsFalse(store.TryWriteReceipt(CopyReceipt(reserved, KingdomSealReceiptState.Reserved, 3), out failure));
				Assert.AreEqual(KingdomSealReceiptState.Committed, store.ReadReceipts()[0].State);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void ExactReservedClaimReleaseIsAtomicIdempotentAndRefusesFinalReceipt()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				KingdomSealRecord legacy = PromotedRecord("dynasty", "legacy-release", "game-origin", 1, 1);
				string failure;
				Assert.IsTrue(store.TryWriteLegacy(legacy, out failure), failure);
				KingdomSealReceipt reserved;
				KingdomSealReservationLease lease;
				Assert.IsTrue(store.TryClaimReservation(legacy, "target-one", 1,
					out reserved, out lease, out failure), failure);
				Assert.IsFalse(store.TryReleaseReservation(
					CopyReceipt(reserved, KingdomSealReceiptState.Reserved, 2), lease, out failure));
				Assert.IsTrue(store.TryReleaseReservation(reserved, lease, out failure), failure);
				Assert.IsTrue(store.TryReleaseReservation(reserved, out failure), failure);
				Assert.AreEqual(0, store.ReadReceipts().Count);

				KingdomSealReceipt reclaimed;
				KingdomSealReservationLease reclaimedLease;
				Assert.IsTrue(store.TryClaimReservation(legacy, "target-two", 3,
					out reclaimed, out reclaimedLease, out failure), failure);
				KingdomSealReceipt committed;
				Assert.IsTrue(store.TryCommitReservation(reclaimed, reclaimedLease, 4,
					out committed, out failure), failure);
				Assert.IsFalse(store.TryReleaseReservation(reclaimed, out failure));
				Assert.AreEqual(KingdomSealReceiptState.Committed, store.ReadReceipts()[0].State);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void ReconciliationReleasesNoPrimaryReservationOnlyAfterLiveClaimEnds()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore store = new KingdomSealStore(root);
				KingdomSealRecord legacy = PromotedRecord("dynasty", "legacy-live", "game-origin", 1, 1);
				string failure;
				Assert.IsTrue(store.TryWriteLegacy(legacy, out failure), failure);
				KingdomSealReceipt reserved;
				KingdomSealReservationLease lease;
				Assert.IsTrue(store.TryClaimReservation(legacy, "target-live", 1,
					out reserved, out lease, out failure), failure);

				bool released;
				Assert.IsTrue(store.TryReleaseAbandonedReservation(reserved,
					out released, out failure), failure);
				Assert.IsFalse(released);
				Assert.AreEqual(1, store.ReadReceipts().Count);
				Assert.IsFalse(store.TryReleaseReservation(reserved, out failure));

				lease.Dispose();
				Assert.IsTrue(store.TryReleaseAbandonedReservation(reserved,
					out released, out failure), failure);
				Assert.IsTrue(released);
				Assert.AreEqual(0, store.ReadReceipts().Count);

				KingdomSealReceipt reclaimed;
				KingdomSealReservationLease reclaimedLease;
				Assert.IsTrue(store.TryClaimReservation(legacy, "target-next", 2,
					out reclaimed, out reclaimedLease, out failure), failure);
				Assert.IsTrue(store.TryReleaseReservation(reclaimed, reclaimedLease,
					out failure), failure);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Test]
		public void FailedReceiptUpgradePreservesOldReservation()
		{
			string root = NewRoot();
			try
			{
				KingdomSealStore normal = new KingdomSealStore(root);
				KingdomSealRecord legacy = PromotedRecord("dynasty", "legacy-safe", "game-origin", 1, 1);
				string failure;
				Assert.IsTrue(normal.TryWriteLegacy(legacy, out failure), failure);
				KingdomSealReceipt reserved;
				KingdomSealReservationLease lease;
				Assert.IsTrue(normal.TryClaimReservation(legacy, "target", 1,
					out reserved, out lease, out failure), failure);

				KingdomSealStore failing = new KingdomSealStore(root, new FailingReplaceFileOps());
				KingdomSealReceipt committed;
				Assert.IsFalse(failing.TryCommitReservation(reserved, lease, 2,
					out committed, out failure));
				List<KingdomSealReceipt> receipts = normal.ReadReceipts();
				Assert.AreEqual(1, receipts.Count);
				Assert.AreEqual(KingdomSealReceiptState.Reserved, receipts[0].State);
				Assert.AreEqual("target", receipts[0].TargetGameId);
				Assert.IsTrue(lease.IsHeld);
				Assert.IsTrue(normal.TryCommitReservation(reserved, lease, 2,
					out committed, out failure), failure);
				Assert.AreEqual(KingdomSealReceiptState.Committed, normal.ReadReceipts()[0].State);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		private static KingdomSealBody ReceiptBody(string kind, string target, bool writtenAsText)
		{
			KingdomSealBody body = new KingdomSealBody();
			body.Put("kind", kind);
			body.Put("lineage", "dynasty");
			body.Put("legacy", "legacy");
			body.Put("target", target);
			body.Put("state", "reserved");
			if (writtenAsText)
			{
				body.Put("written", "1");
			}
			else
			{
				body.Put("written", 1L);
			}
			return body;
		}

		private static KingdomSealReceipt CopyReceipt(KingdomSealReceipt source, KingdomSealReceiptState state, long written)
		{
			return new KingdomSealReceipt
			{
				LineageId = source.LineageId,
				LegacyId = source.LegacyId,
				TargetGameId = source.TargetGameId,
				State = state,
				WrittenTick = written
			};
		}

		private static bool TryDirectoryLink(string link, string target)
		{
			try
			{
				Directory.CreateSymbolicLink(link, target);
				return (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryFileLink(string link, string target)
		{
			try
			{
				File.CreateSymbolicLink(link, target);
				return (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0;
			}
			catch
			{
				return false;
			}
		}

		private static void DeleteLink(string path)
		{
			try
			{
				FileAttributes attributes = File.GetAttributes(path);
				if ((attributes & FileAttributes.ReparsePoint) == 0) return;
				if ((attributes & FileAttributes.Directory) != 0) Directory.Delete(path);
				else File.Delete(path);
			}
			catch
			{
			}
		}

		private static KingdomSealRecord ReadRecord(string path)
		{
			KingdomSealRecord record;
			KingdomSealFault fault;
			string detail;
			Assert.IsTrue(KingdomSealRecord.TryParse(File.ReadAllText(path), out record, out fault, out detail), detail);
			return record;
		}
	}
}
#endif
