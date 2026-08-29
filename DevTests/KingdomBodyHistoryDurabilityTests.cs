#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomBodyHistoryDurabilityTests
	{
		private const string Realm =
			"taf:realm:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string OtherRealm =
			"taf:realm:v1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		[Test]
		public void RulerLifeBindsRealmOrdinalAndExactBody()
		{
			string body = "taf:object:body-one";
			string first = KingdomBodyHistoryRulerLifeRules.Identity(Realm, 0, body);
			Assert.That(first, Does.StartWith("taf:ruler-life:v1:"));
			Assert.That(KingdomBodyHistoryRulerLifeRules.ValidIdentity(
				Realm, 0, body, first), Is.True);
			Assert.That(KingdomBodyHistoryRulerLifeRules.Identity(Realm, 1, body),
				Is.Not.EqualTo(first));
			Assert.That(KingdomBodyHistoryRulerLifeRules.Identity(
				OtherRealm, 0, body), Is.Not.EqualTo(first));
			Assert.That(KingdomBodyHistoryRulerLifeRules.Identity(
				Realm, 0, "taf:object:body-two"), Is.Not.EqualTo(first));
			Assert.That(KingdomBodyHistoryRulerLifeRules.Identity(
				Realm, -1, body), Is.Null);
			Assert.That(KingdomBodyHistoryRulerLifeRules.Identity(
				Realm, 0, "foreign-body"), Is.Null);
		}

		[Test]
		public void SectionPreparationIsAtomicIdempotentAndRealmBound()
		{
			KingdomWitnessedBodyEventEvidence evidence = Evidence(0);
			Assert.That(KingdomBodyHistoryTransactions.TryPrepare(null, Realm, evidence,
				out KingdomBodyHistoryPreparation first, out string failure), Is.True, failure);
			Assert.That(first.AlreadyDurable, Is.False);
			Assert.IsNotNull(first.ReplacementPayload);
			Assert.That(first.ReplacementPayload.Length, Is.GreaterThan(0));
			byte[] durable = (byte[])first.ReplacementPayload.Clone();

			Assert.That(KingdomBodyHistoryTransactions.TryPrepare(durable, Realm, evidence,
				out KingdomBodyHistoryPreparation retry, out failure), Is.True, failure);
			Assert.That(retry.AlreadyDurable, Is.True);
			Assert.That(retry.ReplacementPayload, Is.Null);
			Assert.That(retry.Receipt.ReceiptId, Is.EqualTo(first.Receipt.ReceiptId));
			CollectionAssert.AreEqual(durable, first.ReplacementPayload);
			Assert.That(KingdomBodyHistoryTransactions.TryPrepare(durable, OtherRealm,
				evidence, out _, out failure), Is.False);
			StringAssert.Contains("mismatch", failure);
			CollectionAssert.AreEqual(durable, first.ReplacementPayload);
		}

		[Test]
		public void FutureAndCapRefusalsPreserveHeldBytes()
		{
			KingdomBodyHistoryEnvelope future = new KingdomBodyHistoryEnvelope
			{
				OpaqueFutureVersion = KingdomBodyHistoryCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 1, 2, 3 }
			};
			byte[] futureBytes = KingdomBodyHistoryCodec.Encode(future);
			byte[] futureExact = (byte[])futureBytes.Clone();
			Assert.That(KingdomBodyHistoryTransactions.TryPrepare(futureBytes, Realm,
				Evidence(0), out _, out string failure), Is.False);
			StringAssert.Contains("newer", failure);
			CollectionAssert.AreEqual(futureExact, futureBytes);
			Assert.That(KingdomBodyHistoryTransactions.TryPrepare(futureBytes, Realm,
				Evidence(0), out _, out KingdomBodyHistoryPreparationBlock futureBlock,
				out failure), Is.False);
			Assert.AreEqual(KingdomBodyHistoryPreparationBlock.OpaqueFuture, futureBlock);
			KingdomLabBodyHistoryPhase futurePhase =
				KingdomLabBodyHistoryContractRules.AfterFailure(futureBlock);
			Assert.AreEqual(KingdomLabBodyHistoryPhase.OmittedPreservingMemory, futurePhase);
			Assert.IsTrue(KingdomLabBodyHistoryContractRules.AllowsPhysicalCleanup(futurePhase));

			KingdomBodyHistoryBook full = new KingdomBodyHistoryBook();
			for (int i = 0; i < KingdomBodyHistoryRules.MaxRows; i++)
				Assert.That(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(full,
					full.Revision, Evidence(i), out _, out failure), Is.True, failure);
			KingdomBodyHistoryEnvelope envelope = new KingdomBodyHistoryEnvelope();
			Assert.That(envelope.TryBindEmptyIdentity(Realm, out failure), Is.True, failure);
			envelope.Book = full;
			byte[] capped = KingdomBodyHistoryCodec.Encode(envelope);
			byte[] exact = (byte[])capped.Clone();
			Assert.That(KingdomBodyHistoryTransactions.TryPrepare(capped, Realm,
				Evidence(99), out _, out KingdomBodyHistoryPreparationBlock capBlock,
				out failure), Is.False);
			StringAssert.Contains("capacity", failure);
			Assert.AreEqual(KingdomBodyHistoryPreparationBlock.Capacity, capBlock);
			Assert.IsTrue(KingdomLabBodyHistoryContractRules.AllowsPhysicalCleanup(
				KingdomLabBodyHistoryContractRules.AfterFailure(capBlock)));
			CollectionAssert.AreEqual(exact, capped);
			Assert.That(KingdomBodyHistoryCodec.Decode(capped).Book.Rows.Count,
				Is.EqualTo(KingdomBodyHistoryRules.MaxRows));
		}

		[Test]
		public void LegacyPhysicalJobsStayExplicitAndBoundJobsUpgradeWithoutInventingIdentity()
		{
			Assert.IsTrue(KingdomLabBodyHistoryContractRules.TryResolveLoaded(0,
				(int)KingdomLabBodyHistoryPhase.LegacyPhysicalOnly, false,
				out int version, out KingdomLabBodyHistoryPhase phase));
			Assert.AreEqual(0, version);
			Assert.AreEqual(KingdomLabBodyHistoryPhase.LegacyPhysicalOnly, phase);
			Assert.IsTrue(KingdomLabBodyHistoryContractRules.AllowsPhysicalCleanup(phase));
			Assert.IsTrue(KingdomLabBodyHistoryContractRules.TryResolveLoaded(0,
				(int)KingdomLabBodyHistoryPhase.LegacyPhysicalOnly, true,
				out version, out phase));
			Assert.AreEqual(KingdomBodyHistoryRules.LabContractVersion, version);
			Assert.AreEqual(KingdomLabBodyHistoryPhase.Pending, phase);
			Assert.IsFalse(KingdomLabBodyHistoryContractRules.AllowsPhysicalCleanup(phase));
			Assert.IsFalse(KingdomLabBodyHistoryContractRules.TryResolveLoaded(
				KingdomBodyHistoryRules.LabContractVersion,
				(int)KingdomLabBodyHistoryPhase.Pending, false, out _, out _));
		}

		[Test]
		public void FrozenNonceKeepsOwnerStableAcrossRetry()
		{
			string nonce = "0123456789abcdef0123456789abcdef";
			Assert.IsTrue(KingdomBodyHistoryRules.ValidEffectNonce(nonce));
			string first = KingdomBodyHistoryRules.CompletedLabProcedureReceiptId(
				"game", Realm, "5", "0", "life", "hall", "patient", "job", "key",
				"fingerprint", nonce, "4", "2");
			string retry = KingdomBodyHistoryRules.CompletedLabProcedureReceiptId(
				"game", Realm, "5", "0", "life", "hall", "patient", "job", "key",
				"fingerprint", nonce, "4", "2");
			string changed = KingdomBodyHistoryRules.CompletedLabProcedureReceiptId(
				"game", Realm, "5", "0", "life", "hall", "patient", "job", "key",
				"fingerprint", "1123456789abcdef0123456789abcdef", "4", "2");
			Assert.AreEqual(first, retry);
			Assert.AreNotEqual(first, changed);
			Assert.IsTrue(KingdomBodyHistoryRules.ValidCompletedLabOwner(first));
		}

		[Test]
		public void CurrentAnatomyRemainsLegibleWithoutHistoryAuthority()
		{
			KingdomLiveAnatomySnapshot anatomy = Anatomy(
				"taf:ruler-life:current", "taf:object:current");
			Assert.IsTrue(KingdomBodyHistoryViewRules.TryComposeWithoutHistory(anatomy,
				"section unavailable", out string view, out string failure), failure);
			StringAssert.Contains("Current anatomy: left hand", view);
			StringAssert.Contains("history: unavailable", view);
			StringAssert.Contains("section unavailable", view);
		}

		[Test]
		public void SectionThreeLeasePreservesSiblingAndReadbackIsExact()
		{
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(Table());
			authority.AdoptAbsent();
			byte[] sibling = new byte[] { 1, 9, 8, 7 };
			Assert.That(authority.TryCommit(new List<KingdomCivicMemorySection>
			{
				new KingdomCivicMemorySection(
					KingdomCivicMemoryLimits.SectionCivicArtifacts, sibling)
			}, authority.Revision, out string failure), Is.True, failure);
			Assert.That(authority.TryReadSection(
				KingdomCivicMemoryLimits.SectionBodyHistory,
				out KingdomCivicMemorySectionLease lease, out failure), Is.True, failure);
			Assert.That(KingdomBodyHistoryTransactions.TryPrepare(null, Realm, Evidence(0),
				out KingdomBodyHistoryPreparation prepared, out failure), Is.True, failure);
			Assert.That(authority.TryCommitSection(lease, prepared.ReplacementPayload,
				out failure), Is.True, failure);
			Assert.That(authority.TryReadSection(
				KingdomCivicMemoryLimits.SectionBodyHistory,
				out KingdomCivicMemorySectionLease readback, out failure), Is.True, failure);
			Assert.That(KingdomBodyHistoryTransactions.ContainsExact(readback.Payload(),
				Realm, Evidence(0), out KingdomBodyHistoryReceipt row, out failure),
				Is.True, failure);
			Assert.That(row.ReceiptId, Is.EqualTo(prepared.Receipt.ReceiptId));
			CollectionAssert.AreEqual(sibling, authority.Read().Section(
				KingdomCivicMemoryLimits.SectionCivicArtifacts).Payload());
		}

		[Test]
		public void CurrentViewMarksOnlyExactCurrentBody()
		{
			KingdomWitnessedBodyEventEvidence evidence = Evidence(0);
			KingdomBodyHistoryBook book = new KingdomBodyHistoryBook();
			Assert.That(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(book, 0,
				evidence, out _, out string failure), Is.True, failure);
			KingdomLiveAnatomySnapshot anatomy = Anatomy(evidence.ResidentIdentity,
				evidence.BodyObjectId);
			Assert.That(KingdomBodyHistoryViewRules.TryCompose(anatomy, book,
				out string view, out failure), Is.True, failure);
			StringAssert.Contains("Current anatomy: left hand", view);
			StringAssert.Contains("Witnessed procedure", view);
			StringAssert.Contains("[current form]", view);
			anatomy = Anatomy(evidence.ResidentIdentity, "taf:object:another-body");
			Assert.That(KingdomBodyHistoryViewRules.TryCompose(anatomy, book,
				out view, out failure), Is.True, failure);
			StringAssert.Contains("[former form]", view);
		}

		private static KingdomLiveAnatomySnapshot Anatomy(string resident, string body)
		{
			List<KingdomLiveAnatomyPart> parts = new List<KingdomLiveAnatomyPart>
			{
				new KingdomLiveAnatomyPart { NativeOrderIndex = 0, NativePath = "0/0",
					BodyPartId = 0, Type = "Hand",
					OrdinalName = "left hand", Category = 1 }
			};
			KingdomLiveAnatomySnapshot snapshot = new KingdomLiveAnatomySnapshot
			{
				ResidentIdentity = resident, BodyObjectId = body,
				ObservedTick = 50, OrderedParts = parts
			};
			snapshot.BodyIdentityDigest = KingdomBodyHistoryRules.AnatomyDigest(
				resident, body, parts);
			return snapshot;
		}

		private static KingdomWitnessedBodyEventEvidence Evidence(int index)
		{
			string body = "taf:object:body-" + index;
			string life = KingdomBodyHistoryRulerLifeRules.Identity(Realm, index, body);
			return new KingdomWitnessedBodyEventEvidence
			{
				OwnerKind = KingdomBodyHistoryRules.CompletedLabProcedureKind,
				OwnerReceiptId = KingdomBodyHistoryRules.CompletedLabProcedureReceiptId(
					"game", Realm, "5", index.ToString(), life, "building", "patient",
					"job-" + index, "grafted-hand", "fingerprint",
					"0123456789abcdef0123456789abcdef", index.ToString(), "0"),
				ResidentIdentity = life, BodyObjectId = body,
				ProcedureKey = "grafted-hand", BodyPartFact = "graft at left hand",
				WitnessedTick = 20 + index
			};
		}

		private static KingdomCivicMemoryFamilyTable Table()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, id == KingdomCivicMemoryLimits.SectionBodyHistory
					? (KingdomCivicMemoryFamilyReader)BodyHistory : AlwaysCurrent);
			return table;
		}

		private static KingdomCivicMemoryNested BodyHistory(byte[] payload, out string fault)
		{
			fault = "";
			try
			{
				KingdomBodyHistoryEnvelope value = KingdomBodyHistoryCodec.Decode(payload);
				if (value.IsOpaqueFuture) return KingdomCivicMemoryNested.Future;
				if (value.Quarantined) { fault = value.Fault; return KingdomCivicMemoryNested.Malformed; }
				return KingdomCivicMemoryNested.Current;
			}
			catch (Exception error) { fault = error.Message; return KingdomCivicMemoryNested.Malformed; }
		}

		private static KingdomCivicMemoryNested AlwaysCurrent(byte[] payload, out string fault)
		{
			fault = "";
			return payload == null || payload.Length == 0
				? KingdomCivicMemoryNested.Malformed : KingdomCivicMemoryNested.Current;
		}
	}
}
#endif
