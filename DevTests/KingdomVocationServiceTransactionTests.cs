#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomVocationServiceTransactionTests
	{
		private const int Practice = KingdomCivicMemoryLimits.SectionCivicPractice;
		private const string Realm =
			"taf:realm:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

		[Test]
		public void C18RecordRetryHistoryAndReadAreDurableAndMutationBounded()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Port port = new Port(authority);
			Publication publication = new Publication();
			KingdomVocationServiceOffer offer = Offer("waystation", 1);
			string source = offer.SourceDescription;
			Assert.IsTrue(KingdomVocationServiceTransactions.TryReadView(port, Realm,
				offer.SettlementId, offer.Vocation, offer, out string emptyHistory,
				out KingdomVocationServiceStatus available, out string failure), failure);
			StringAssert.Contains("No durable", emptyHistory);
			Assert.AreEqual(KingdomVocationServiceActionState.Available, available.State);
			Assert.AreEqual(0, available.SeriesCount);
			Assert.AreEqual(0, available.RealmCount);
			Assert.AreEqual(0, port.CommitCalls, "pre-choice read is save-pure");
			Assert.IsTrue(KingdomVocationServiceTransactions.TryRecordGoverned(port, Realm,
				offer, 40L, publication, out KingdomVocationServiceCommitResult result,
				out failure), failure);
			Assert.IsTrue(result.Changed);
			Assert.IsTrue(publication.Committed);
			Assert.AreEqual(1, publication.Calls);
			Assert.AreEqual(1, port.CommitCalls);
			Assert.AreEqual(source, offer.SourceDescription, "source view is immutable");
			Assert.AreEqual(0L, result.CadenceOrdinal);
			StringAssert.Contains(result.SourceReceiptId, result.ReceiptText);
			StringAssert.Contains(result.SinkReceiptId, result.ReceiptText);
			long outerRevision = authority.Revision;

			Publication retryPublication = new Publication();
			Assert.IsTrue(KingdomVocationServiceTransactions.TryRecordGoverned(port, Realm,
				offer, 90L, retryPublication,
				out KingdomVocationServiceCommitResult retry, out failure), failure);
			Assert.IsFalse(retry.Changed);
			Assert.IsFalse(retryPublication.Committed);
			Assert.AreEqual(0, retryPublication.Calls);
			Assert.AreEqual(result.ServiceId, retry.ServiceId);
			Assert.AreEqual(result.CompletedTick, retry.CompletedTick);
			Assert.AreEqual(outerRevision, authority.Revision);
			Assert.AreEqual(1, port.CommitCalls);

			Assert.IsTrue(KingdomVocationServiceTransactions.TryReadHistory(port, Realm,
				offer.SettlementId, offer.Vocation, out string history, out failure), failure);
			StringAssert.Contains(result.SourceReceiptId, history);
			StringAssert.Contains(result.SinkReceiptId, history);
			Assert.AreEqual(outerRevision, authority.Revision, "read/cancel path is save-pure");
			Assert.AreEqual(1, port.CommitCalls);
			Assert.IsTrue(KingdomVocationServiceTransactions.TryReadView(port, Realm,
				offer.SettlementId, offer.Vocation, offer, out history,
				out KingdomVocationServiceStatus recorded, out failure), failure);
			Assert.AreEqual(KingdomVocationServiceActionState.AlreadyRecorded, recorded.State);
			StringAssert.Contains("useful result waystation 1", recorded.ExistingReceiptText);
			Assert.IsTrue(KingdomVocationServiceTransactions.TryReadRealmResults(port, Realm,
				out List<string> realmPages, out failure), failure);
			Assert.AreEqual(1, realmPages.Count);
			StringAssert.Contains("useful result waystation 1", realmPages[0]);
			Assert.AreEqual(outerRevision, authority.Revision);
			Assert.AreEqual(1, port.CommitCalls);
		}

		[Test]
		public void OuterStaleCasRefusesWithoutOverwritingSiblingMove()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			StalePort port = new StalePort(authority);
			Publication publication = new Publication();
			Assert.IsFalse(KingdomVocationServiceTransactions.TryRecordGoverned(port, Realm,
				Offer("refuge", 2), 40L, publication,
				out KingdomVocationServiceCommitResult _,
				out string failure));
			StringAssert.Contains("revision", failure);
			Assert.AreEqual(1, publication.Calls);
			Assert.IsFalse(publication.Committed);
			Assert.IsNull(authority.Read().Section(Practice));
			Assert.IsNotNull(authority.Read().Section(
				KingdomCivicMemoryLimits.SectionCivicArtifacts));
		}

		[Test]
		public void FutureAndMalformedC18AreReadOnlyAndNeverCommitted()
		{
			KingdomCivicPracticeEnvelope future = new KingdomCivicPracticeEnvelope
			{
				OpaqueFutureVersion = KingdomCivicPracticeCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 9, 8, 7 }
			};
			AssertRefused(AuthorityFromSaved(KingdomCivicPracticeCodec.Encode(future)), "newer");

			KingdomCivicMemoryAuthority malformed = Authority();
			byte[] outer = KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				One(Practice, new byte[] { 1, 2, 3 }), 0L));
			malformed.AdoptSaved(outer);
			Assert.IsTrue(malformed.Quarantined);
			AssertRefused(malformed, "read-only");
		}

		private static void AssertRefused(KingdomCivicMemoryAuthority authority,
			string expected)
		{
			long revision = authority.Revision;
			Port port = new Port(authority);
			Publication publication = new Publication();
			Assert.IsFalse(KingdomVocationServiceTransactions.TryRecordGoverned(port, Realm,
				Offer("reliquary", 3), 40L, publication,
				out KingdomVocationServiceCommitResult _,
				out string failure));
			StringAssert.Contains(expected, failure.ToLowerInvariant());
			Assert.AreEqual(revision, authority.Revision);
			Assert.AreEqual(0, port.CommitCalls);
			Assert.AreEqual(0, publication.Calls);
		}

		private static KingdomVocationServiceOffer Offer(string vocation, int ordinal)
		{
			KingdomVocationServiceKind kind = KingdomVocationServiceRules.KindFor(vocation);
			KingdomVocationServiceAuthority authority = kind == KingdomVocationServiceKind.RouteBrief
				? KingdomVocationServiceAuthority.PolityRoute :
				kind == KingdomVocationServiceKind.SanctuaryTitle
					? KingdomVocationServiceAuthority.BuiltShelter :
					KingdomVocationServiceAuthority.ArtifactRecognition;
			KingdomVocationServiceSource source = new KingdomVocationServiceSource(
				"taf:settlement:seat", vocation, kind, authority,
				"taf:source:" + vocation + ":" + ordinal, "exact source " + ordinal,
				"useful result " + vocation + " " + ordinal);
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out KingdomVocationServiceOffer offer, out string failure), failure);
			return offer;
		}

		private static KingdomCivicMemoryAuthority Authority()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, id == Practice ?
					(KingdomCivicMemoryFamilyReader)ReadPractice : ReadAnything);
			return new KingdomCivicMemoryAuthority(table);
		}

		private static KingdomCivicMemoryAuthority AuthorityFromSaved(byte[] practiceBytes)
		{
			KingdomCivicMemoryAuthority authority = Authority();
			authority.AdoptSaved(KingdomCivicMemoryCodec.Encode(
				KingdomCivicMemoryState.Of(One(Practice, practiceBytes), 0L)));
			return authority;
		}

		private static List<KingdomCivicMemorySection> One(int id, byte[] payload) =>
			new List<KingdomCivicMemorySection> { new KingdomCivicMemorySection(id, payload) };

		private static KingdomCivicMemoryNested ReadPractice(byte[] payload,
			out string failure)
		{
			try
			{
				KingdomCivicPracticeEnvelope envelope = KingdomCivicPracticeCodec.Decode(payload);
				if (envelope.IsOpaqueFuture) { failure = ""; return KingdomCivicMemoryNested.Future; }
				if (!KingdomCivicPracticeStore.TryValidateIdentity(envelope, out failure))
					return KingdomCivicMemoryNested.Malformed;
				failure = ""; return KingdomCivicMemoryNested.Current;
			}
			catch (Exception error)
			{
				failure = error.Message; return KingdomCivicMemoryNested.Malformed;
			}
		}

		private static KingdomCivicMemoryNested ReadAnything(byte[] payload,
			out string failure)
		{
			failure = payload == null || payload.Length == 0 ? "empty test payload" : "";
			return string.IsNullOrEmpty(failure) ? KingdomCivicMemoryNested.Current :
				KingdomCivicMemoryNested.Malformed;
		}

		private class Port : IKingdomCivicPracticeSectionPort
		{
			protected readonly KingdomCivicMemoryAuthority Authority;
			internal int CommitCalls;
			internal Port(KingdomCivicMemoryAuthority authority) { Authority = authority; }

			public bool TryReadSection(int sectionId,
				out KingdomCivicMemorySectionLease lease, out string failure)
			{
				return Authority.TryReadSection(sectionId, out lease, out failure);
			}

			public virtual bool TryCommitSection(KingdomCivicMemorySectionLease lease,
				byte[] payload, out string failure)
			{
				CommitCalls++;
				return Authority.TryCommitSection(lease, payload, out failure);
			}
		}

		private sealed class StalePort : Port
		{
			private bool Moved;
			internal StalePort(KingdomCivicMemoryAuthority authority) : base(authority) { }

			public override bool TryCommitSection(KingdomCivicMemorySectionLease lease,
				byte[] payload, out string failure)
			{
				if (!Moved)
				{
					Moved = true;
					if (!Authority.TryCommit(One(KingdomCivicMemoryLimits.SectionCivicArtifacts,
						new byte[] { 1 }), Authority.Revision, out failure)) return false;
				}
				return base.TryCommitSection(lease, payload, out failure);
			}
		}

		private sealed class Publication : IKingdomVocationServicePublication
		{
			internal int Calls;
			internal bool Committed;

			public bool TryPublish(Func<bool> publish)
			{
				Calls++;
				Committed = publish();
				return Committed;
			}
		}
	}
}
#endif
