#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomFounderHistoryRulesTests
	{
		private const string Realm = "taf:realm:test-founder-memory";
		private const string SealBlob = "v1:1:120:Zm91bmRlcg==";

		[Test]
		public void Schema2PreparationFreezesOneLocalProjectionWithoutLegacyIds()
		{
			KingdomFounderHistoryReceipt first = Prepare(true);
			KingdomFounderHistoryReceipt second = Prepare(true);
			Assert.AreEqual(2, first.Version);
			Assert.AreEqual(KingdomFounderHistoryPhase.Prepared, first.Phase);
			Assert.IsTrue(first.PublicationEnabled);
			Assert.AreEqual(first.ProjectionId, second.ProjectionId);
			Assert.AreEqual(first.ProjectionProofId, second.ProjectionProofId);
			StringAssert.StartsWith(KingdomFounderHistoryRules.ProjectionPrefix,
				first.ProjectionId);
			StringAssert.Contains("Ari founded New Grit Gate in the salt dunes.", first.Gospel);
			Assert.AreEqual(KingdomFounderHistoryLegacyCleanupState.None,
				first.LegacyCleanupState);
			Assert.AreEqual(KingdomFounderHistoryPhase.None, first.LegacyPhase);
			Assert.AreEqual("", first.EntityId);
			Assert.AreEqual("", first.NoteId);
			Assert.AreEqual("", first.ProofId);
			Assert.AreEqual(0L, first.EventId);
			Assert.IsTrue(KingdomFounderHistoryRules.Owns(first, Realm, SealBlob));
			Assert.IsFalse(KingdomFounderHistoryRules.Owns(first, Realm, SealBlob + "x"));
		}

		[Test]
		public void DisabledPreparationIsTerminalAndOwnsNoVanillaCleanupEvidence()
		{
			KingdomFounderHistoryReceipt receipt = Prepare(false);
			Assert.AreEqual(KingdomFounderHistoryPhase.Suppressed, receipt.Phase);
			Assert.IsFalse(receipt.PublicationEnabled);
			Assert.AreEqual(KingdomFounderHistoryLegacyCleanupState.None,
				receipt.LegacyCleanupState);
			Assert.AreEqual(0L, receipt.EventId);
			Assert.AreEqual(receipt.PreparedTick, receipt.CommittedTick);
			AssertValid(receipt);
		}

		[Test]
		public void Schema2HasOnlyPreparedAndCommittedActiveShapes()
		{
			KingdomFounderHistoryReceipt receipt = Prepare(true);
			AssertValid(receipt);
			receipt.Phase = KingdomFounderHistoryPhase.Committed;
			receipt.CommittedTick = 900L;
			AssertValid(receipt);
			Assert.AreEqual(0L, receipt.EventId,
				"a local projection must not need a vanilla HistoryKit event id");
			foreach (KingdomFounderHistoryPhase legacy in new[]
			{
				KingdomFounderHistoryPhase.EntityPublished,
				KingdomFounderHistoryPhase.EventPublished,
				KingdomFounderHistoryPhase.NotePublished
			})
			{
				receipt = Prepare(true);
				receipt.Phase = legacy;
				AssertInvalid(receipt);
			}
		}

		[Test]
		public void IdentityTellingLegacyEvidenceAndPhaseTamperingFailClosed()
		{
			KingdomFounderHistoryReceipt receipt = Prepare(true);
			receipt.ProjectionId += "x";
			AssertInvalid(receipt);
			receipt = Prepare(true);
			receipt.Gospel += " invented";
			AssertInvalid(receipt);
			receipt = Prepare(true);
			receipt.EntityId = "taf:foreign";
			AssertInvalid(receipt);
			receipt = Prepare(true);
			receipt.CommittedTick = receipt.PreparedTick;
			AssertInvalid(receipt);
		}

		[Test]
		public void FramingSeparatesFieldBoundariesAndWhitespaceIsCanonical()
		{
			KingdomFounderHistoryReceipt normal = Prepare(true);
			KingdomFounderHistoryReceipt other;
			string failure;
			Assert.IsTrue(KingdomFounderHistoryRules.TryPrepare("taf:realm:test-founder",
				"-memory" + SealBlob, 120L, 700L, 1001L, "  Ari\n", "New   Grit Gate",
				"salt dunes", "was lost", true, out other, out failure), failure);
			Assert.AreNotEqual(normal.ProjectionId, other.ProjectionId);
			Assert.AreEqual("Ari", other.FounderName);
			Assert.AreEqual("New Grit Gate", other.CityName);
		}

		[TestCase(KingdomFounderHistoryPhase.EntityPublished, 0L)]
		[TestCase(KingdomFounderHistoryPhase.EventPublished, 44L)]
		[TestCase(KingdomFounderHistoryPhase.NotePublished, 44L)]
		[TestCase(KingdomFounderHistoryPhase.Committed, 44L)]
		[TestCase(KingdomFounderHistoryPhase.Quarantined, 0L)]
		[TestCase(KingdomFounderHistoryPhase.Quarantined, 44L)]
		public void ExactSchema1PublishedReceiptsRetainBoundedCleanupEvidence(
			KingdomFounderHistoryPhase legacyPhase, long eventId)
		{
			KingdomFounderHistoryReceipt receipt = Legacy(legacyPhase, eventId);
			receipt.Normalize();
			Assert.AreEqual(2, receipt.Version);
			Assert.AreEqual(KingdomFounderHistoryPhase.Prepared, receipt.Phase);
			Assert.AreEqual(KingdomFounderHistoryLegacyCleanupState.Required,
				receipt.LegacyCleanupState);
			Assert.AreEqual(legacyPhase, receipt.LegacyPhase);
			Assert.AreEqual(eventId, receipt.EventId);
			StringAssert.StartsWith(KingdomFounderHistoryRules.LegacyEntityPrefix,
				receipt.EntityId);
			StringAssert.StartsWith(KingdomFounderHistoryRules.ProjectionPrefix,
				receipt.ProjectionId);
			AssertValid(receipt);
			receipt.LegacyCleanupState = KingdomFounderHistoryLegacyCleanupState.Complete;
			receipt.Phase = KingdomFounderHistoryPhase.Committed;
			receipt.CommittedTick = 901L;
			AssertValid(receipt);
		}

		[Test]
		public void Schema1PrefixStatesMigrateWithoutInventingCleanup()
		{
			KingdomFounderHistoryReceipt prepared = Legacy(
				KingdomFounderHistoryPhase.Prepared, 0L);
			prepared.Normalize();
			Assert.AreEqual(KingdomFounderHistoryLegacyCleanupState.None,
				prepared.LegacyCleanupState);
			Assert.AreEqual("", prepared.EntityId);
			AssertValid(prepared);

			KingdomFounderHistoryReceipt suppressed = Legacy(
				KingdomFounderHistoryPhase.Suppressed, 0L);
			suppressed.Normalize();
			Assert.AreEqual(KingdomFounderHistoryPhase.Suppressed, suppressed.Phase);
			Assert.AreEqual(KingdomFounderHistoryLegacyCleanupState.None,
				suppressed.LegacyCleanupState);
			AssertValid(suppressed);
		}

		[Test]
		public void MalformedSchema1AndFutureReceiptsFailInert()
		{
			KingdomFounderHistoryReceipt malformed = Legacy(
				KingdomFounderHistoryPhase.Committed, 44L);
			malformed.EntityId += "foreign";
			malformed.Normalize();
			Assert.AreEqual(2, malformed.Version);
			Assert.AreEqual(KingdomFounderHistoryPhase.Quarantined, malformed.Phase);
			Assert.IsNotEmpty(malformed.Fault);
			Assert.AreEqual("", malformed.EntityId,
				"unproved ids must not be retained as cleanup authority");
			AssertValid(malformed);

			KingdomFounderHistoryReceipt future = Prepare(true);
			string futureProjection = future.ProjectionId;
			future.Version = 99;
			future.Normalize();
			Assert.AreEqual(99, future.Version);
			Assert.AreEqual(futureProjection, future.ProjectionId,
				"unknown future fields must not be reinterpreted as schema 2");
			AssertInvalid(future);
		}

		[Test]
		public void RuntimeNeverPublishesToSharedHistoryOrJournalPools()
		{
			string entity = Read("Experience", "KingdomFounderHistory.Entity.cs")
				+ Read("Experience", "KingdomFounderHistory.EntityPlan.cs");
			string journal = Read("Experience", "KingdomFounderHistory.Note.cs");
			string model = Read("Experience", "KingdomFounderHistoryNote.cs");
			string runtime = Read("Experience", "KingdomFounderHistory.cs");
			string all = entity + journal + model + runtime;

			StringAssert.DoesNotContain("CreateEntity(", all);
			StringAssert.DoesNotContain("ApplyEvent(", all);
			StringAssert.DoesNotContain("SultanNotes.Add", all);
			StringAssert.DoesNotContain("AddedNote(", all);
			StringAssert.DoesNotContain("new r_KingdomFounderHistoryNote", all);
			StringAssert.DoesNotContain("sultanHistory", runtime + journal + model);
			StringAssert.DoesNotContain("Options.GetOption", runtime);
			StringAssert.Contains("LegacyCleanupState == KingdomFounderHistoryLegacyCleanupState.None",
				journal);
			StringAssert.Contains("History.entities.RemoveAt", entity);
			StringAssert.Contains("History.events.RemoveAt", entity);
			StringAssert.Contains("JournalAPI.SultanNotes.RemoveAt", journal);
			StringAssert.Contains("Rollback()", entity + journal);
			StringAssert.Contains("TryGetProjection", journal);
			StringAssert.Contains("Schema-1 deserialization carrier only", model);
			StringAssert.DoesNotContain("r_TAF_OptionFounderHistory",
				Read("RuntimeData", "Options.xml"));
		}

		[Test]
		public void FounderRemainsVisibleThroughOwnedChronicleAndRebuildsOnLoad()
		{
			string accession = Read("Experience", "KingdomSuccession.Accession.cs");
			string pending = Read("Experience", "KingdomSuccession.PendingSeal.cs");
			string load = Read("Core", "KingdomSystem.z19.PersistenceAndCallbacks.cs");
			StringAssert.Contains("KingdomFounderHistory.PublishBestEffort", accession);
			StringAssert.Contains("KingdomChronicle.RecordOnce(system", pending);
			StringAssert.Contains("KingdomFounderHistory.ReconcileBestEffort(this);", load);
		}

		[Test]
		public void InstalledQudHasBroadRelicAndDungeonConsumersOfGlobalHistory()
		{
			string root = LocateDecompiledQud();
			if (root == null)
			{
				Assert.Ignore("Installed/decompiled Qud source is unavailable for native consumer proof.");
				return;
			}
			string gameObject = File.ReadAllText(Path.Combine(root, "XRL", "World", "GameObject.cs"));
			string relic = File.ReadAllText(Path.Combine(root, "XRL", "World", "RelicGenerator.cs"));
			string dungeon = File.ReadAllText(Path.Combine(root, "XRL", "World", "ZoneBuilders",
				"SultanDungeon.cs"));
			StringAssert.Contains("sultanHistory.entities.GetRandomElement()", gameObject);
			StringAssert.Contains("sultanHistory.entities.GetRandomElement()", relic);
			StringAssert.Contains("sultanHistory.entities.GetRandomElement()", dungeon);
		}

		[Test]
		public void PhaseEnumRemainsAppendOnly()
		{
			Assert.AreEqual("0,1,2,3,4,5,6,7", JoinValues(
				typeof(KingdomFounderHistoryPhase)));
		}

		private static KingdomFounderHistoryReceipt Prepare(bool enabled)
		{
			KingdomFounderHistoryReceipt receipt;
			string failure;
			Assert.IsTrue(KingdomFounderHistoryRules.TryPrepare(Realm, SealBlob, 120L,
				700L, 1001L, "Ari", "New Grit Gate", "the salt dunes", "was lost",
				enabled, out receipt, out failure), failure);
			return receipt;
		}

		private static KingdomFounderHistoryReceipt Legacy(KingdomFounderHistoryPhase phase,
			long eventId)
		{
			KingdomFounderHistoryReceipt receipt = Prepare(true);
			string digest = receipt.ProjectionId.Substring(
				KingdomFounderHistoryRules.ProjectionPrefix.Length);
			receipt.Version = 1;
			receipt.Phase = phase;
			receipt.ProjectionId = "";
			receipt.ProjectionProofId = "";
			receipt.LegacyCleanupState = KingdomFounderHistoryLegacyCleanupState.None;
			receipt.LegacyPhase = KingdomFounderHistoryPhase.None;
			receipt.EntityId = KingdomFounderHistoryRules.LegacyEntityPrefix + digest;
			receipt.NoteId = KingdomFounderHistoryRules.LegacyNotePrefix + digest;
			receipt.ProofId = KingdomFounderHistoryRules.LegacyProofPrefix + digest;
			receipt.EventId = eventId;
			receipt.CommittedTick = phase == KingdomFounderHistoryPhase.Committed
				|| phase == KingdomFounderHistoryPhase.Suppressed ? 900L : 0L;
			receipt.PublicationEnabled = phase != KingdomFounderHistoryPhase.Suppressed;
			receipt.Fault = phase == KingdomFounderHistoryPhase.Quarantined
				? "legacy evidence diverged" : "";
			return receipt;
		}

		private static void AssertValid(KingdomFounderHistoryReceipt receipt)
		{
			string failure;
			Assert.IsTrue(KingdomFounderHistoryRules.Validate(receipt, out failure), failure);
		}

		private static void AssertInvalid(KingdomFounderHistoryReceipt receipt)
		{
			string failure;
			Assert.IsFalse(KingdomFounderHistoryRules.Validate(receipt, out failure));
			Assert.IsNotEmpty(failure);
		}

		private static string LocateDecompiledQud()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_DECOMPILED");
			string[] candidates = new[]
			{
				supplied,
				"/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1"
			};
			for (int i = 0; i < candidates.Length; i++)
				if (!string.IsNullOrWhiteSpace(candidates[i])
					&& File.Exists(Path.Combine(candidates[i], "XRL", "World",
						"RelicGenerator.cs"))) return candidates[i];
			return null;
		}

		private static string JoinValues(Type type)
		{
			Array values = Enum.GetValues(type);
			string[] rows = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
				rows[i] = Convert.ToInt32(values.GetValue(i)).ToString();
			return string.Join(",", rows);
		}

		private static string Read(params string[] parts)
		{
			string path = TestMain.RepositoryRoot;
			for (int i = 0; i < parts.Length; i++) path = Path.Combine(path, parts[i]);
			return File.ReadAllText(path);
		}
	}
}
#endif
