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
		public void EnabledPreparationFreezesOneBoundedDeterministicProjection()
		{
			KingdomFounderHistoryReceipt first = Prepare(true);
			KingdomFounderHistoryReceipt second = Prepare(true);
			Assert.AreEqual(KingdomFounderHistoryPhase.Prepared, first.Phase);
			Assert.IsTrue(first.PublicationEnabled);
			Assert.AreEqual(first.EntityId, second.EntityId);
			Assert.AreEqual(first.NoteId, second.NoteId);
			Assert.AreEqual(first.ProofId, second.ProofId);
			StringAssert.StartsWith("taf:founder-memory:v1:entity:", first.EntityId);
			StringAssert.Contains("Ari founded New Grit Gate in the salt dunes.", first.Gospel);
			Assert.IsTrue(KingdomFounderHistoryRules.Owns(first, Realm, SealBlob));
			Assert.IsFalse(KingdomFounderHistoryRules.Owns(first, Realm, SealBlob + "x"));
		}

		[Test]
		public void DisabledAtRiteIsTerminalAndCreatesNoDeferredPublication()
		{
			KingdomFounderHistoryReceipt receipt = Prepare(false);
			Assert.AreEqual(KingdomFounderHistoryPhase.Suppressed, receipt.Phase);
			Assert.IsFalse(receipt.PublicationEnabled);
			Assert.AreEqual(0L, receipt.EventId);
			Assert.AreEqual(receipt.PreparedTick, receipt.CommittedTick);
			AssertValid(receipt);
		}

		[Test]
		public void EveryRecoverableBoundaryHasOneExactValidShape()
		{
			KingdomFounderHistoryReceipt receipt = Prepare(true);
			AssertValid(receipt);
			receipt.Phase = KingdomFounderHistoryPhase.EntityPublished;
			AssertValid(receipt);
			receipt.Phase = KingdomFounderHistoryPhase.EventPublished;
			receipt.EventId = 44L;
			AssertValid(receipt);
			receipt.Phase = KingdomFounderHistoryPhase.NotePublished;
			AssertValid(receipt);
			receipt.Phase = KingdomFounderHistoryPhase.Committed;
			receipt.CommittedTick = 900L;
			AssertValid(receipt);
			receipt.Phase = KingdomFounderHistoryPhase.Quarantined;
			receipt.CommittedTick = 0L;
			receipt.Fault = "external evidence diverged";
			AssertValid(receipt);
		}

		[Test]
		public void IdentityTellingAndPhaseTamperingFailClosed()
		{
			KingdomFounderHistoryReceipt receipt = Prepare(true);
			receipt.EntityId += "x";
			AssertInvalid(receipt);
			receipt = Prepare(true);
			receipt.Gospel += " invented";
			AssertInvalid(receipt);
			receipt = Prepare(true);
			receipt.Phase = KingdomFounderHistoryPhase.EventPublished;
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
			Assert.AreNotEqual(normal.EntityId, other.EntityId);
			Assert.AreEqual("Ari", other.FounderName);
			Assert.AreEqual("New Grit Gate", other.CityName);
		}

		[Test]
		public void RuntimeUsesIsolatedHistoryTypeAndPostResetNonForgettableNote()
		{
			string entity = Read("Experience", "KingdomFounderHistory.Entity.cs");
			string note = Read("Experience", "KingdomFounderHistoryNote.cs");
			string runtime = Read("Experience", "KingdomFounderHistory.cs");
			string accession = Read("Experience", "KingdomSuccession.Accession.cs");
			string load = Read("Core", "KingdomSystem.z19.PersistenceAndCallbacks.cs");
			string options = Read("RuntimeData", "Options.xml");
			StringAssert.Contains("KingdomFounderHistoryRules.EntityType", entity);
			StringAssert.Contains("{ \"period\", \"0\" }", entity);
			StringAssert.DoesNotContain("isCandidate", entity);
			StringAssert.DoesNotContain("PlayerCult", entity + runtime + note);
			StringAssert.DoesNotContain("CodaSultan", entity + runtime + note);
			StringAssert.DoesNotContain("CodaVillage", entity + runtime + note);
			StringAssert.Contains("return false;", note);
			StringAssert.Contains("Tradable = false", runtime + Read("Experience",
				"KingdomFounderHistory.Note.cs"));
			int reset = accession.IndexOf("TryResetPersonalKnowledge", StringComparison.Ordinal);
			int publish = accession.IndexOf("KingdomFounderHistory.PublishBestEffort",
				StringComparison.Ordinal);
			Assert.Greater(publish, reset);
			StringAssert.Contains("KingdomFounderHistory.ReconcileBestEffort(this);", load);
			StringAssert.Contains("r_TAF_OptionFounderHistory", options);
		}

		[Test]
		public void PhaseEnumIsAppendOnly()
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
