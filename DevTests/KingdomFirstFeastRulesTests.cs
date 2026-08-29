#if TAF_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFirstFeastRulesTests
	{
		private const string Realm = "taf:realm:first-feast";
		private const string Settlement = "taf:settlement:first-feast";
		private const string Transaction = "0123456789abcdef0123456789abcdef";

		private static KingdomExperienceLedger Enabled(long tick = 10L)
		{
			KingdomExperienceLedger ledger = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger, Realm,
				out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				true, true, true, tick, out failure), failure);
			return ledger;
		}

		private static KingdomFirstFeastReceipt Offer(string settlement = Settlement,
			string transaction = Transaction, bool reverse = false, long deedTick = 10L,
			long offeredTick = 10L, long epoch = 1L)
		{
			KingdomFirstFeastDeed deed = new KingdomFirstFeastDeed {
				SettlementId = settlement, SettlementName = "Tamsketh",
				DeedText = KingdomFirstFeastRules.AuthoredDeed, DeedTick = deedTick,
				GuestTerminalReceiptId = "taf:growth-first-guest-terminal:" + new string('a', 64),
				GuestTerminalDigest = new string('b', 64), GuestTerminalTick = deedTick - 1L,
				AdventureEventId = "taf:adventure:" + transaction,
				AdventureFingerprint = new string('c', 64) };
			Assert.IsTrue(KingdomFirstFeastRules.TryBuildDeedId(deed, out deed.DeedId));
			KingdomFirstFeastCandidate[] people = reverse
				? new KingdomFirstFeastCandidate[] {
					new KingdomFirstFeastCandidate(9, "Yla"),
					new KingdomFirstFeastCandidate(4, "Ava") }
				: new KingdomFirstFeastCandidate[] {
					new KingdomFirstFeastCandidate(4, "Ava"),
					new KingdomFirstFeastCandidate(9, "Yla") };
			Assert.IsTrue(KingdomFirstFeastRules.TryPrepare(deed, people,
				offeredTick, epoch, out KingdomFirstFeastReceipt receipt, out string failure),
				failure);
			return receipt;
		}

		[Test]
		public void OfferFreezesExactDeedPeopleAndAuthoredContentBeforeDisplay()
		{
			KingdomFirstFeastReceipt first = Offer(reverse: false);
			KingdomFirstFeastReceipt second = Offer(reverse: true);
			Assert.IsTrue(KingdomFirstFeastRules.Valid(first));
			Assert.IsTrue(KingdomFirstFeastRules.SameOfferSource(first, second));
			Assert.AreEqual(4, first.ProposerResidentId);
			Assert.AreEqual("Ava", first.ProposerName);
			Assert.AreEqual(9, first.WitnessResidentId);
			Assert.AreEqual(KingdomFirstFeastRules.AuthoredDish, first.DishName);
			Assert.AreEqual(KingdomFirstFeastRules.AuthoredIngredients, first.Ingredients);
			Assert.AreEqual(KingdomFirstFeastRules.OfferedDedication,
				first.OfferedDedication);
			StringAssert.Contains(first.DeedText,
				KingdomFirstFeastRules.RenderOffer(first, true, true));
			StringAssert.Contains(first.ProposerName,
				KingdomFirstFeastRules.RenderOffer(first, false, false));
			StringAssert.DoesNotContain(first.WitnessName + ": \"",
				KingdomFirstFeastRules.RenderOffer(first, false, false));
		}

		[Test]
		public void DeferIsIndefiniteFreeAndByteStable()
		{
			KingdomExperienceLedger ledger = Enabled();
			KingdomFirstFeastReceipt offer = Offer();
			Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, offer, out string failure), failure);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			long revision = ledger.Revision;
			Assert.IsTrue(KingdomExperienceRules.TryDecideFirstFeast(ledger, 0L, Settlement,
				KingdomFirstFeastChoice.Defer, null, long.MaxValue, out bool committed,
				out KingdomFirstFeastReceipt unchanged, out failure), failure);
			Assert.IsFalse(committed); Assert.AreEqual(revision, ledger.Revision);
			Assert.AreEqual(KingdomFirstFeastPhase.Offered, unchanged.Phase);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));
			StringAssert.Contains("without a deadline",
				KingdomFirstFeastRules.DecisionDisclosure(KingdomFirstFeastChoice.Defer, null));
		}

		[Test]
		public void AdoptAdaptAndRefuseCloseExactlyOnceWithoutIdentityDrift()
		{
			KingdomFirstFeastReceipt offer = Offer();
			Assert.IsTrue(KingdomFirstFeastRules.TryDecide(offer,
				KingdomFirstFeastChoice.Adopt, null, 11L, out KingdomFirstFeastReceipt adopted,
				out bool changed, out string failure), failure);
			Assert.IsTrue(changed); Assert.IsTrue(KingdomFirstFeastRules.IsAffirmative(adopted));
			Assert.IsTrue(KingdomFirstFeastRules.TryBuildPracticeId(offer.DeedId,
				out string practice));
			Assert.AreEqual(practice, adopted.PracticeId);
			Assert.IsTrue(KingdomFirstFeastRules.TryDecide(adopted,
				KingdomFirstFeastChoice.Adopt, null, long.MaxValue, out KingdomFirstFeastReceipt retry,
				out changed, out failure), failure);
			Assert.IsFalse(changed); Assert.AreEqual(adopted.DecidedTick, retry.DecidedTick);
			Assert.IsFalse(KingdomFirstFeastRules.TryDecide(adopted,
				KingdomFirstFeastChoice.Refuse, null, 12L, out _, out _, out _));

			Assert.IsTrue(KingdomFirstFeastRules.TryDecide(offer,
				KingdomFirstFeastChoice.Adapt, KingdomFirstFeastRules.TravelerDedication, 12L,
				out KingdomFirstFeastReceipt adapted, out changed, out failure), failure);
			Assert.IsTrue(changed); Assert.AreEqual(practice, adapted.PracticeId);
			Assert.AreEqual(KingdomFirstFeastRules.TravelerDedication,
				KingdomFirstFeastRules.EffectiveDedication(adapted));
			Assert.IsFalse(KingdomFirstFeastRules.TryDecide(offer,
				KingdomFirstFeastChoice.Adapt, "arbitrary player text", 12L, out _, out _, out _));

			Assert.IsTrue(KingdomFirstFeastRules.TryDecide(offer,
				KingdomFirstFeastChoice.Refuse, null, 12L,
				out KingdomFirstFeastReceipt refused, out changed, out failure), failure);
			Assert.IsTrue(changed); Assert.AreEqual(KingdomFirstFeastPhase.Refused, refused.Phase);
			Assert.IsNull(refused.PracticeId);
			Assert.IsNull(KingdomFirstFeastRules.ChronicleEventId(refused));
		}

		[Test]
		public void LedgerOwnsOneCanonicalRowPerCityAndPreservesAttributionAfterSave()
		{
			KingdomExperienceLedger ledger = Enabled();
			KingdomFirstFeastReceipt offer = Offer();
			Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, offer, out string failure), failure);
			long stable = ledger.Revision;
			Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger, 0L,
				offer.Copy(), out failure), failure);
			Assert.AreEqual(stable, ledger.Revision);
			KingdomFirstFeastReceipt mismatch = Offer(); mismatch.ProposerName = "Other";
			Assert.IsFalse(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, mismatch, out failure));
			Assert.AreEqual(stable, ledger.Revision);
			Assert.IsTrue(KingdomExperienceRules.TryDecideFirstFeast(ledger, ledger.Revision,
				Settlement, KingdomFirstFeastChoice.Adapt,
				KingdomFirstFeastRules.ResidentDedication, 11L, out bool committed,
				out KingdomFirstFeastReceipt decided, out failure), failure);
			Assert.IsTrue(committed);
			byte[] wire = KingdomExperienceCodec.EncodeEnvelope(ledger);
			KingdomExperienceLedger read = KingdomExperienceCodec.DecodeEnvelope(wire);
			Assert.AreEqual(1, read.FirstFeasts.Count);
			Assert.AreEqual(decided.ProposerName, read.FirstFeasts[0].ProposerName);
			Assert.AreEqual(decided.PracticeId, read.FirstFeasts[0].PracticeId);
			CollectionAssert.AreEqual(wire, KingdomExperienceCodec.EncodeEnvelope(read));
		}

		[Test]
		public void OptionEpochBlocksOldDeedsButCannotEraseAnOwnedPractice()
		{
			KingdomExperienceLedger ledger = Enabled(20L);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(deedTick: 10L, offeredTick: 20L), out string failure));
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));

			ledger = Enabled(10L);
			Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(), out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryDecideFirstFeast(ledger, ledger.Revision,
				Settlement, KingdomFirstFeastChoice.Adopt, null, 11L, out _, out _, out failure),
				failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				false, true, true, 20L, out failure), failure);
			Assert.AreEqual(1, ledger.FirstFeasts.Count);
			Assert.IsTrue(KingdomExperienceRules.TryValidate(ledger, out failure), failure);
		}

		[Test]
		public void DisablingStoriesArchivesOnlyUnacceptedOfferWithoutReenableBacklog()
		{
			KingdomExperienceLedger ledger = Enabled(10L);
			Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(), out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				false, true, true, 20L, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryArchiveFirstFeastOffer(ledger,
				ledger.Revision, Settlement, 20L, out bool committed,
				out KingdomFirstFeastReceipt archived, out failure), failure);
			Assert.IsTrue(committed);
			Assert.AreEqual(KingdomFirstFeastPhase.Archived, archived.Phase);
			Assert.AreEqual(KingdomFirstFeastChoice.None, archived.Choice);
			Assert.IsNull(archived.PracticeId);
			byte[] frozen = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomExperienceRules.TryArchiveFirstFeastOffer(ledger, 0L,
				Settlement, long.MaxValue, out committed, out archived, out failure), failure);
			Assert.IsFalse(committed);
			CollectionAssert.AreEqual(frozen, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Assert.IsFalse(KingdomExperienceRules.TryDecideFirstFeast(ledger, ledger.Revision,
				Settlement, KingdomFirstFeastChoice.Adopt, null, 21L, out _, out _, out _));
		}

		[Test]
		public void MasterResumeRejectsFeastWorkCommittedDuringPause()
		{
			KingdomExperienceLedger ledger = Enabled(10L);
			Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(offeredTick: 25L), out string failure), failure);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomExperienceRules.TryPrepareMasterResume(ledger, Realm,
				20L, 30L, true, true, true, out KingdomExperienceMasterResumePlan _,
				out failure));
			StringAssert.Contains("during the master pause", failure);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));

			ledger = Enabled(10L);
			Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(offeredTick: 20L), out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryPrepareMasterResume(ledger, Realm,
				20L, 30L, true, true, true, out KingdomExperienceMasterResumePlan plan,
				out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryPublishMasterResume(ledger, plan,
				out failure), failure);
			Assert.AreEqual(1, ledger.FirstFeasts.Count);
		}

		[Test]
		public void V3MigrationAndMalformedV4PreserveExactWireLaw()
		{
			KingdomExperienceLedger v3 = Enabled();
			byte[] legacy = KingdomExperienceCodec.EncodeLegacyV3Fixture(v3);
			KingdomExperienceLedger migrated = KingdomExperienceCodec.DecodeEnvelope(legacy);
			Assert.AreEqual(4, migrated.FormatVersion); Assert.AreEqual(0, migrated.FirstFeasts.Count);
			Assert.IsTrue(KingdomExperienceRules.TryValidate(migrated, out string failure), failure);

			KingdomExperienceLedger current = Enabled();
			Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(current,
				current.Revision, Offer(), out failure), failure);
			byte[] malformed = KingdomExperienceCodec.EncodeEnvelope(current);
			byte[] needle = Encoding.UTF8.GetBytes(KingdomFirstFeastRules.DeedPrefix);
			int at = Find(malformed, needle); Assert.Greater(at, 0); malformed[at] = (byte)'x';
			KingdomExperienceLedger quarantined = KingdomExperienceCodec.DecodeEnvelope(malformed);
			Assert.AreEqual(KingdomExperienceSchemaState.Quarantined, quarantined.SchemaState);
			CollectionAssert.AreEqual(malformed, KingdomExperienceCodec.EncodeEnvelope(quarantined));
		}

		[Test]
		public void ExactV4BudgetLeavesFiveHundredThirtySixBytesAndMaximumFeastsFit()
		{
			Assert.AreEqual(2554, ExactWorstCaseRowBytes());
			Assert.AreEqual(2584, KingdomExperienceRules.FirstFeastRowByteBudget);
			Assert.AreEqual(24028, KingdomExperienceRules.MaxDeclaredPayloadBytes);
			Assert.AreEqual(24576, KingdomExperienceCodec.MaxEnvelopeBytes);
			Assert.AreEqual(536, KingdomExperienceCodec.MaxEnvelopeBytes
				- (KingdomExperienceRules.MaxDeclaredPayloadBytes + 12));
			KingdomExperienceLedger ledger = Enabled();
			string[] settlements = { "taf:settlement:feast-a", "taf:settlement:feast-b",
				"taf:settlement:feast-c" };
			for (int i = 0; i < 3; i++)
			{
				string transaction = new string("abc"[i], 32);
				KingdomFirstFeastReceipt offer = Offer(settlements[i], transaction);
				Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
					ledger.Revision, offer, out string failure), failure);
			}
			byte[] envelope = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.LessOrEqual(envelope.Length,
				KingdomExperienceRules.MaxDeclaredPayloadBytes + 12);
			CollectionAssert.AreEqual(envelope, KingdomExperienceCodec.EncodeEnvelope(
				KingdomExperienceCodec.DecodeEnvelope(envelope)));
		}

		private static int ExactWorstCaseRowBytes()
		{
			const int primitives = 4 + 1 + 1 + 4 + 4 + 4 + (5 * 8);
			const int sixBoundedIds = 6 * (4 + 256);
			const int eightCivicStrings = 8 * (4 + 96);
			const int twoDigests = 2 * (4 + 64);
			return primitives + sixBoundedIds + eightCivicStrings + twoDigests;
		}

		private static int Find(byte[] Haystack, byte[] Needle)
		{
			for (int i = 0; i <= Haystack.Length - Needle.Length; i++)
			{
				int j = 0;
				for (; j < Needle.Length && Haystack[i + j] == Needle[j]; j++) { }
				if (j == Needle.Length) return i;
			}
			return -1;
		}
	}
}
#endif
