#if TAF_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger, Realm,
				out string failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
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
			ClassicAssert.IsTrue(KingdomFirstFeastRules.TryBuildDeedId(deed, out deed.DeedId));
			KingdomFirstFeastCandidate[] people = reverse
				? new KingdomFirstFeastCandidate[] {
					new KingdomFirstFeastCandidate(9, "Yla"),
					new KingdomFirstFeastCandidate(4, "Ava") }
				: new KingdomFirstFeastCandidate[] {
					new KingdomFirstFeastCandidate(4, "Ava"),
					new KingdomFirstFeastCandidate(9, "Yla") };
			ClassicAssert.IsTrue(KingdomFirstFeastRules.TryPrepare(deed, people,
				offeredTick, epoch, out KingdomFirstFeastReceipt receipt, out string failure),
				failure);
			return receipt;
		}

		[Test]
		public void OfferFreezesExactDeedPeopleAndAuthoredContentBeforeDisplay()
		{
			KingdomFirstFeastReceipt first = Offer(reverse: false);
			KingdomFirstFeastReceipt second = Offer(reverse: true);
			ClassicAssert.IsTrue(KingdomFirstFeastRules.Valid(first));
			ClassicAssert.IsTrue(KingdomFirstFeastRules.SameOfferSource(first, second));
			ClassicAssert.AreEqual(4, first.ProposerResidentId);
			ClassicAssert.AreEqual("Ava", first.ProposerName);
			ClassicAssert.AreEqual(9, first.WitnessResidentId);
			ClassicAssert.AreEqual(KingdomFirstFeastRules.AuthoredDish, first.DishName);
			ClassicAssert.AreEqual(KingdomFirstFeastRules.AuthoredIngredients, first.Ingredients);
			ClassicAssert.AreEqual(KingdomFirstFeastRules.OfferedDedication,
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
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, offer, out string failure), failure);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			long revision = ledger.Revision;
			ClassicAssert.IsTrue(KingdomExperienceRules.TryDecideFirstFeast(ledger, 0L, Settlement,
				KingdomFirstFeastChoice.Defer, null, long.MaxValue, out bool committed,
				out KingdomFirstFeastReceipt unchanged, out failure), failure);
			ClassicAssert.IsFalse(committed); ClassicAssert.AreEqual(revision, ledger.Revision);
			ClassicAssert.AreEqual(KingdomFirstFeastPhase.Offered, unchanged.Phase);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));
			StringAssert.Contains("without a deadline",
				KingdomFirstFeastRules.DecisionDisclosure(KingdomFirstFeastChoice.Defer, null));
		}

		[Test]
		public void AdoptAdaptAndRefuseCloseExactlyOnceWithoutIdentityDrift()
		{
			KingdomFirstFeastReceipt offer = Offer();
			ClassicAssert.IsTrue(KingdomFirstFeastRules.TryDecide(offer,
				KingdomFirstFeastChoice.Adopt, null, 11L, out KingdomFirstFeastReceipt adopted,
				out bool changed, out string failure), failure);
			ClassicAssert.IsTrue(changed); ClassicAssert.IsTrue(KingdomFirstFeastRules.IsAffirmative(adopted));
			ClassicAssert.IsTrue(KingdomFirstFeastRules.TryBuildPracticeId(offer.DeedId,
				out string practice));
			ClassicAssert.AreEqual(practice, adopted.PracticeId);
			ClassicAssert.IsTrue(KingdomFirstFeastRules.TryDecide(adopted,
				KingdomFirstFeastChoice.Adopt, null, long.MaxValue, out KingdomFirstFeastReceipt retry,
				out changed, out failure), failure);
			ClassicAssert.IsFalse(changed); ClassicAssert.AreEqual(adopted.DecidedTick, retry.DecidedTick);
			ClassicAssert.IsFalse(KingdomFirstFeastRules.TryDecide(adopted,
				KingdomFirstFeastChoice.Refuse, null, 12L, out _, out _, out _));

			ClassicAssert.IsTrue(KingdomFirstFeastRules.TryDecide(offer,
				KingdomFirstFeastChoice.Adapt, KingdomFirstFeastRules.TravelerDedication, 12L,
				out KingdomFirstFeastReceipt adapted, out changed, out failure), failure);
			ClassicAssert.IsTrue(changed); ClassicAssert.AreEqual(practice, adapted.PracticeId);
			ClassicAssert.AreEqual(KingdomFirstFeastRules.TravelerDedication,
				KingdomFirstFeastRules.EffectiveDedication(adapted));
			ClassicAssert.IsFalse(KingdomFirstFeastRules.TryDecide(offer,
				KingdomFirstFeastChoice.Adapt, "arbitrary player text", 12L, out _, out _, out _));

			ClassicAssert.IsTrue(KingdomFirstFeastRules.TryDecide(offer,
				KingdomFirstFeastChoice.Refuse, null, 12L,
				out KingdomFirstFeastReceipt refused, out changed, out failure), failure);
			ClassicAssert.IsTrue(changed); ClassicAssert.AreEqual(KingdomFirstFeastPhase.Refused, refused.Phase);
			ClassicAssert.IsNull(refused.PracticeId);
			ClassicAssert.IsNull(KingdomFirstFeastRules.ChronicleEventId(refused));
		}

		[Test]
		public void LedgerOwnsOneCanonicalRowPerCityAndPreservesAttributionAfterSave()
		{
			KingdomExperienceLedger ledger = Enabled();
			KingdomFirstFeastReceipt offer = Offer();
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, offer, out string failure), failure);
			long stable = ledger.Revision;
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger, 0L,
				offer.Copy(), out failure), failure);
			ClassicAssert.AreEqual(stable, ledger.Revision);
			KingdomFirstFeastReceipt mismatch = Offer(); mismatch.ProposerName = "Other";
			ClassicAssert.IsFalse(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, mismatch, out failure));
			ClassicAssert.AreEqual(stable, ledger.Revision);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryDecideFirstFeast(ledger, ledger.Revision,
				Settlement, KingdomFirstFeastChoice.Adapt,
				KingdomFirstFeastRules.ResidentDedication, 11L, out bool committed,
				out KingdomFirstFeastReceipt decided, out failure), failure);
			ClassicAssert.IsTrue(committed);
			byte[] wire = KingdomExperienceCodec.EncodeEnvelope(ledger);
			KingdomExperienceLedger read = KingdomExperienceCodec.DecodeEnvelope(wire);
			ClassicAssert.AreEqual(1, read.FirstFeasts.Count);
			ClassicAssert.AreEqual(decided.ProposerName, read.FirstFeasts[0].ProposerName);
			ClassicAssert.AreEqual(decided.PracticeId, read.FirstFeasts[0].PracticeId);
			CollectionAssert.AreEqual(wire, KingdomExperienceCodec.EncodeEnvelope(read));
		}

		[Test]
		public void OptionEpochBlocksOldDeedsButCannotEraseAnOwnedPractice()
		{
			KingdomExperienceLedger ledger = Enabled(20L);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(deedTick: 10L, offeredTick: 20L), out string failure));
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));

			ledger = Enabled(10L);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(), out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryDecideFirstFeast(ledger, ledger.Revision,
				Settlement, KingdomFirstFeastChoice.Adopt, null, 11L, out _, out _, out failure),
				failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				false, true, true, 20L, out failure), failure);
			ClassicAssert.AreEqual(1, ledger.FirstFeasts.Count);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryValidate(ledger, out failure), failure);
		}

		[Test]
		public void DisablingStoriesArchivesOnlyUnacceptedOfferWithoutReenableBacklog()
		{
			KingdomExperienceLedger ledger = Enabled(10L);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(), out string failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				false, true, true, 20L, out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryArchiveFirstFeastOffer(ledger,
				ledger.Revision, Settlement, 20L, out bool committed,
				out KingdomFirstFeastReceipt archived, out failure), failure);
			ClassicAssert.IsTrue(committed);
			ClassicAssert.AreEqual(KingdomFirstFeastPhase.Archived, archived.Phase);
			ClassicAssert.AreEqual(KingdomFirstFeastChoice.None, archived.Choice);
			ClassicAssert.IsNull(archived.PracticeId);
			byte[] frozen = KingdomExperienceCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryArchiveFirstFeastOffer(ledger, 0L,
				Settlement, long.MaxValue, out committed, out archived, out failure), failure);
			ClassicAssert.IsFalse(committed);
			CollectionAssert.AreEqual(frozen, KingdomExperienceCodec.EncodeEnvelope(ledger));
			ClassicAssert.IsFalse(KingdomExperienceRules.TryDecideFirstFeast(ledger, ledger.Revision,
				Settlement, KingdomFirstFeastChoice.Adopt, null, 21L, out _, out _, out _));
		}

		[Test]
		public void MasterResumeRejectsFeastWorkCommittedDuringPause()
		{
			KingdomExperienceLedger ledger = Enabled(10L);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(offeredTick: 25L), out string failure), failure);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomExperienceRules.TryPrepareMasterResume(ledger, Realm,
				20L, 30L, true, true, true, out KingdomExperienceMasterResumePlan _,
				out failure));
			StringAssert.Contains("during the master pause", failure);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));

			ledger = Enabled(10L);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
				ledger.Revision, Offer(offeredTick: 20L), out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPrepareMasterResume(ledger, Realm,
				20L, 30L, true, true, true, out KingdomExperienceMasterResumePlan plan,
				out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishMasterResume(ledger, plan,
				out failure), failure);
			ClassicAssert.AreEqual(1, ledger.FirstFeasts.Count);
		}

		[Test]
		public void V3MigrationAndMalformedV4PreserveExactWireLaw()
		{
			KingdomExperienceLedger v3 = Enabled();
			byte[] legacy = KingdomExperienceCodec.EncodeLegacyV3Fixture(v3);
			KingdomExperienceLedger migrated = KingdomExperienceCodec.DecodeEnvelope(legacy);
			ClassicAssert.AreEqual(4, migrated.FormatVersion); ClassicAssert.AreEqual(0, migrated.FirstFeasts.Count);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryValidate(migrated, out string failure), failure);

			KingdomExperienceLedger current = Enabled();
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(current,
				current.Revision, Offer(), out failure), failure);
			byte[] malformed = KingdomExperienceCodec.EncodeEnvelope(current);
			byte[] needle = Encoding.UTF8.GetBytes(KingdomFirstFeastRules.DeedPrefix);
			int at = Find(malformed, needle); ClassicAssert.Greater(at, 0); malformed[at] = (byte)'x';
			KingdomExperienceLedger quarantined = KingdomExperienceCodec.DecodeEnvelope(malformed);
			ClassicAssert.AreEqual(KingdomExperienceSchemaState.Quarantined, quarantined.SchemaState);
			CollectionAssert.AreEqual(malformed, KingdomExperienceCodec.EncodeEnvelope(quarantined));
		}

		[Test]
		public void ExactV4BudgetLeavesFiveHundredThirtySixBytesAndMaximumFeastsFit()
		{
			ClassicAssert.AreEqual(2554, ExactWorstCaseRowBytes());
			ClassicAssert.AreEqual(2584, KingdomExperienceRules.FirstFeastRowByteBudget);
			ClassicAssert.AreEqual(24028, KingdomExperienceRules.MaxDeclaredPayloadBytes);
			ClassicAssert.AreEqual(24576, KingdomExperienceCodec.MaxEnvelopeBytes);
			ClassicAssert.AreEqual(536, KingdomExperienceCodec.MaxEnvelopeBytes
				- (KingdomExperienceRules.MaxDeclaredPayloadBytes + 12));
			KingdomExperienceLedger ledger = Enabled();
			string[] settlements = { "taf:settlement:feast-a", "taf:settlement:feast-b",
				"taf:settlement:feast-c" };
			for (int i = 0; i < 3; i++)
			{
				string transaction = new string("abc"[i], 32);
				KingdomFirstFeastReceipt offer = Offer(settlements[i], transaction);
				ClassicAssert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(ledger,
					ledger.Revision, offer, out string failure), failure);
			}
			byte[] envelope = KingdomExperienceCodec.EncodeEnvelope(ledger);
			ClassicAssert.LessOrEqual(envelope.Length,
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
