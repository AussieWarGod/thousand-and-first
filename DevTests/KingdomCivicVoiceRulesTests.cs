#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCivicVoiceRulesTests
	{
		private const string Realm = "taf:realm:civic-voices";
		private const string Settlement = "taf:settlement:civic-voices";

		private static KingdomExperienceLedger Enabled(string realm = Realm)
		{
			KingdomExperienceLedger ledger = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger, realm,
				out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				true, true, true, 10L, out failure), failure);
			return ledger;
		}

		private static KingdomCivicVoiceCandidate[] Six(bool reverse = false,
			bool longNames = false)
		{
			KingdomCivicVoiceCandidate[] rows = new KingdomCivicVoiceCandidate[6];
			for (int i = 0; i < rows.Length; i++)
			{
				int id = reverse ? rows.Length - i : i + 1;
				string name = longNames ? new string((char)('a' + id), 96) : "resident-" + id;
				rows[i] = new KingdomCivicVoiceCandidate(id, name);
			}
			return rows;
		}

		private static KingdomCivicDecisionPreview Preview(KingdomCivicVoiceFixture fixture,
			string facts = null, string settlement = Settlement, string source = null)
		{
			return new KingdomCivicDecisionPreview
			{
				Fixture = fixture, SourceVersion = 1,
				SourceId = source ?? "taf:civic-source:" + (int)fixture,
				SettlementId = settlement, Facts = facts ?? "exact owner preview " + (int)fixture,
				CauseTick = 10L, EnableEpoch = 1L
			};
		}

		private static KingdomCivicVoiceReceipt Add(KingdomExperienceLedger ledger,
			KingdomCivicVoiceFixture fixture, bool reverse = false, string facts = null,
			string settlement = Settlement, string source = null, bool longNames = false)
		{
			Assert.IsTrue(KingdomCivicVoiceRules.TryPrepare(ledger,
				Preview(fixture, facts, settlement, source), Six(reverse, longNames),
				out KingdomCivicVoiceReceipt receipt, out string failure), failure);
			Assert.IsTrue(KingdomCivicVoiceRules.TryPublish(ledger, ledger.Revision,
				receipt, out failure), failure);
			return receipt;
		}

		[Test]
		public void ThreeFixturesFreezeSixExactResidentsAndOwnerFacts()
		{
			KingdomExperienceLedger ledger = Enabled();
			for (int i = 1; i <= 3; i++)
			{
				KingdomCivicVoiceFixture fixture = (KingdomCivicVoiceFixture)i;
				string facts = "owner-preview-" + i;
				KingdomCivicVoiceReceipt row = Add(ledger, fixture, reverse: i % 2 == 0,
					facts: facts);
				Assert.AreEqual(facts, row.Facts);
				Assert.AreEqual(i * 2 - 1, row.FirstResidentId);
				Assert.AreEqual(i * 2, row.SecondResidentId);
				string named = KingdomCivicVoiceRules.Render(row, true, true);
				StringAssert.StartsWith(facts, named);
				StringAssert.Contains(row.FirstName, named);
				StringAssert.Contains(row.SecondName, named);
			}
			Assert.AreEqual(3, ledger.Voices.Count);
		}

		[Test]
		public void MissingDeadOrUnloadedWitnessesAreFactsOnlyAndReadIsByteStable()
		{
			KingdomExperienceLedger ledger = Enabled();
			KingdomCivicVoiceReceipt row = Add(ledger,
				KingdomCivicVoiceFixture.CreedDeclaration, facts: "exact choice remains");
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.AreEqual(row.Facts, KingdomCivicVoiceRules.Render(row, false, false));
			Assert.AreEqual(row.Facts, KingdomCivicVoiceRules.Render(row, true, false));
			Assert.AreEqual(row.Facts, KingdomCivicVoiceRules.Render(row, false, true));
			Assert.AreEqual(row.SourceId, ledger.Voices[0].SourceId);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void SaveAndPartitionBoundariesCannotRecastWitnessesOrFacts()
		{
			KingdomExperienceLedger continuous = Enabled();
			KingdomExperienceLedger partitioned = Enabled();
			for (int i = 1; i <= 3; i++)
			{
				KingdomCivicVoiceFixture fixture = (KingdomCivicVoiceFixture)i;
				Add(continuous, fixture, reverse: true);
				Add(partitioned, fixture, reverse: i % 2 == 0);
				partitioned = KingdomExperienceCodec.DecodeEnvelope(
					KingdomExperienceCodec.EncodeEnvelope(partitioned));
			}
			CollectionAssert.AreEqual(KingdomExperienceCodec.EncodeEnvelope(continuous),
				KingdomExperienceCodec.EncodeEnvelope(partitioned));
		}

		[Test]
		public void CallbackRequiresPresentExactWitnessAndExhaustsOnce()
		{
			KingdomExperienceLedger ledger = Enabled();
			KingdomCivicVoiceReceipt row = Add(ledger,
				KingdomCivicVoiceFixture.VillageCovenant);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomCivicVoiceRules.TryConsumeCallback(ledger, ledger.Revision,
				row.SourceId, row.FirstResidentId, false, 20L, out _, out _));
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Assert.IsTrue(KingdomCivicVoiceRules.TryConsumeCallback(ledger, ledger.Revision,
				row.SourceId, row.FirstResidentId, true, 20L, out string text,
				out string failure), failure);
			StringAssert.Contains(row.Facts, text);
			byte[] consumed = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomCivicVoiceRules.TryConsumeCallback(ledger, ledger.Revision,
				row.SourceId, row.FirstResidentId, true, 21L, out _, out _));
			CollectionAssert.AreEqual(consumed, KingdomExperienceCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void DuplicateCapAndRetirementFailWithoutEviction()
		{
			KingdomExperienceLedger ledger = Enabled();
			KingdomCivicVoiceReceipt first = Add(ledger,
				KingdomCivicVoiceFixture.CreedDeclaration);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			KingdomCivicVoiceReceipt mismatch = first.Copy(); mismatch.Facts = "different";
			Assert.IsFalse(KingdomCivicVoiceRules.TryPublish(ledger, ledger.Revision,
				mismatch, out _));
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Add(ledger, KingdomCivicVoiceFixture.VillageCovenant);
			Add(ledger, KingdomCivicVoiceFixture.AssentingMoot);
			before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomCivicVoiceRules.TryPrepare(ledger,
				Preview(KingdomCivicVoiceFixture.CreedDeclaration, "fourth"), Six(),
				out _, out _));
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Assert.IsFalse(KingdomExperienceRules.TryRetireCivicVoices(ledger,
				"taf:realm:foreign", ledger.Revision, out _));
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Assert.IsTrue(KingdomExperienceRules.TryRetireCivicVoices(ledger, Realm,
				ledger.Revision, out string failure), failure);
			Assert.AreEqual(0, ledger.Voices.Count);
		}

		[Test]
		public void MasterPauseAndStoryOffCannotCreateOrReanchorVoiceWork()
		{
			KingdomExperienceLedger ledger = Enabled();
			Add(ledger, KingdomCivicVoiceFixture.CreedDeclaration);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomExperienceRules.TryPrepareMasterResume(ledger, Realm,
				9L, 20L, true, true, true, out _, out _));
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				false, true, true, 20L, out string failure), failure);
			before = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomCivicVoiceRules.TryPrepare(ledger,
				Preview(KingdomCivicVoiceFixture.VillageCovenant), Six(), out _, out _));
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void CompactV4MaximumAndAuthenticV1ToV3MigrationsStayBounded()
		{
			string realm = Id("taf:realm:", 0); KingdomExperienceLedger full = Enabled(realm);
			string[] settlements = { CivicId("taf:settlement:", 1),
				CivicId("taf:settlement:", 2), CivicId("taf:settlement:", 3) };
			for (int i = 0; i < 3; i++) ReserveAudience(full, realm, settlements[i], i);
			for (int i = 0; i < 16; i++) ReserveBody(full, realm, settlements[i % 3], i);
			AddRichCivicRows(full, settlements);
			for (int i = 1; i <= 3; i++) Add(full, (KingdomCivicVoiceFixture)i,
				facts: new string('f', KingdomCivicVoiceRules.MaxFactsBytes),
				settlement: settlements[0], source: Id("taf:voice:", i), longNames: true);
			AddRichFirstFeasts(full, settlements);
			byte[] current = KingdomExperienceCodec.EncodeEnvelope(full);
			Assert.AreEqual(3, full.Audiences.Count); Assert.AreEqual(16, full.BodyReservations.Count);
			Assert.AreEqual(3, full.Offices.Count); Assert.AreEqual(3, full.Remembrances.Count);
			Assert.AreEqual(3, full.Voices.Count);
			Assert.AreEqual(3, full.FirstFeasts.Count);
			Assert.LessOrEqual(current.Length, KingdomExperienceCodec.MaxEnvelopeBytes);
			Assert.LessOrEqual(current.Length - 12, KingdomExperienceRules.MaxDeclaredPayloadBytes);
			Assert.Less(KingdomExperienceRules.MaxDeclaredPayloadBytes + 12,
				KingdomExperienceCodec.MaxEnvelopeBytes);

			KingdomExperienceLedger v2 = Enabled(realm);
			for (int i = 0; i < 3; i++) ReserveAudience(v2, realm, settlements[i], i);
			for (int i = 0; i < 16; i++) ReserveBody(v2, realm, settlements[i % 3], i);
			AddRichCivicRows(v2, settlements);
			byte[] legacy = KingdomExperienceCodec.EncodeLegacyV2Fixture(v2);
			CollectionAssert.AreEqual(legacy, KingdomExperienceCodec.EncodeLegacyV2Fixture(v2));
			KingdomExperienceLedger migrated = KingdomExperienceCodec.DecodeEnvelope(legacy);
			Assert.AreEqual(KingdomExperienceRules.CurrentFormatVersion, migrated.FormatVersion);
			Assert.AreEqual(0, migrated.Voices.Count);
			Assert.AreEqual(realm, migrated.Audiences[0].RealmId);
			Assert.AreEqual(realm, migrated.BodyReservations[0].RealmId);
			Assert.AreEqual(3, migrated.Offices.Count);
			Assert.AreEqual(3, migrated.Remembrances.Count);
			Assert.IsTrue(KingdomExperienceRules.TryValidate(migrated, out string failure), failure);
			KingdomExperienceLedger v1 = Enabled();
			KingdomExperienceLedger migratedV1 = KingdomExperienceCodec.DecodeEnvelope(
				KingdomExperienceCodec.EncodeLegacyV1Fixture(v1));
			Assert.AreEqual(KingdomExperienceRules.CurrentFormatVersion, migratedV1.FormatVersion);
			Assert.AreEqual(0, migratedV1.Voices.Count);

			KingdomExperienceLedger v3 = Enabled(realm);
			for (int i = 1; i <= 3; i++) Add(v3, (KingdomCivicVoiceFixture)i,
				facts: "wire-v3-owner-facts-" + i, settlement: settlements[0],
				source: "taf:voice:v3-" + i);
			byte[] legacyV3 = KingdomExperienceCodec.EncodeLegacyV3Fixture(v3);
			CollectionAssert.AreEqual(legacyV3,
				KingdomExperienceCodec.EncodeLegacyV3Fixture(v3));
			KingdomExperienceLedger migratedV3 =
				KingdomExperienceCodec.DecodeEnvelope(legacyV3);
			Assert.AreEqual(KingdomExperienceRules.CurrentFormatVersion,
				migratedV3.FormatVersion);
			Assert.AreEqual(3, migratedV3.Voices.Count);
			Assert.AreEqual(0, migratedV3.FirstFeasts.Count);
			Assert.IsTrue(KingdomExperienceRules.TryValidate(migratedV3,
				out failure), failure);
		}

		[Test]
		public void MalformedCompactVoiceQuarantinesAndPreservesExactBytes()
		{
			KingdomExperienceLedger ledger = Enabled();
			Add(ledger, KingdomCivicVoiceFixture.AssentingMoot,
				source: "taf:voice:badtarget");
			byte[] malformed = KingdomExperienceCodec.EncodeEnvelope(ledger);
			byte[] needle = System.Text.Encoding.UTF8.GetBytes("taf:voice:badtarget");
			int at = Find(malformed, needle); Assert.Greater(at, 0); malformed[at] = (byte)'x';
			KingdomExperienceLedger read = KingdomExperienceCodec.DecodeEnvelope(malformed);
			Assert.AreEqual(KingdomExperienceSchemaState.Quarantined, read.SchemaState);
			CollectionAssert.AreEqual(malformed, KingdomExperienceCodec.EncodeEnvelope(read));
		}

		#if !TAF_CONSTRUCTION_INPUT_PORTABLE
		[Test]
		public void MootTagIsExactReadOnlyAndFailsClosed()
		{
			Assert.IsTrue(KingdomAssentingMootRules.TryPrepare("realm", "settlement", "City",
				"zone", "building", "lot", 100, 1, 10L,
				out KingdomAssentingMootReceipt moot, out string failure), failure);
			Assert.IsTrue(KingdomAssentingMootRules.TryChangeMember(moot,
				KingdomAssentingMootRole.Assent, true, 7, "Ava", "body-7", 11L,
				out moot, out failure), failure);
			string authority = moot.AuthorityId, fingerprint = moot.MembershipFingerprint;
			Assert.IsTrue(KingdomDecisionTagRules.TryDerive(moot,
				out KingdomDecisionTagView tag));
			Assert.AreEqual(authority, tag.SourceId); Assert.AreEqual(fingerprint,
				tag.MembershipFingerprint); Assert.AreEqual(1, tag.Assents);
			StringAssert.Contains("do not decide this declaration",
				KingdomDecisionTagRules.CreedScene(moot));
			StringAssert.Contains("do not decide this covenant",
				KingdomDecisionTagRules.CovenantScene(moot));
			Assert.AreEqual(authority, moot.AuthorityId);
			Assert.AreEqual(fingerprint, moot.MembershipFingerprint);
			KingdomAssentingMootReceipt corrupt = moot.Copy(); corrupt.MembershipFingerprint += "x";
			Assert.IsFalse(KingdomDecisionTagRules.TryDerive(corrupt, out _));
			Assert.AreEqual("", KingdomDecisionTagRules.CreedScene(corrupt));
		}

		[Test]
		public void OwnerPreviewsNameExactActionAndResultIdentity()
		{
			string creed = KingdomCreedRules.DeclarationPreview("Issachari", 2, 90);
			StringAssert.Contains("each changes realm standing by -150", creed);
			StringAssert.Contains("Dissent changes from 90 to 100", creed);
			string covenant = KingdomFoundingTransaction.VillageCharterPreview("Joppa", 50);
			StringAssert.Contains("exactly 8 drams", covenant);
			StringAssert.Contains("standing changes from 50 to 600", covenant);
			Assert.IsTrue(KingdomAssentingMootRules.TryPrepare("realm", "settlement", "City",
				"zone", "building", "lot", 100, 1, 10L, out KingdomAssentingMootReceipt moot,
				out string failure), failure);
			string mootFacts = KingdomAssentingMootRules.MembershipPreview(moot,
				KingdomAssentingMootRole.Assent, true, "Ava");
			StringAssert.Contains("assents 0 to 1", mootFacts);
			StringAssert.Contains("ward strength 0 to 10", mootFacts);
		}
		#endif

		private static void ReserveAudience(KingdomExperienceLedger l, string realm,
			string settlement, int i)
		{
			Assert.IsTrue(KingdomExperienceRules.TryReserveAudience(l, l.Revision,
				new KingdomExperienceAudienceReceipt {
					ReservationId = Id("taf:experience-audience:", i),
					RealmId = realm, SettlementId = settlement, SourceId = Id("taf:event:", i),
					Lane = KingdomExperienceLane.CivicVoices,
					OptionKind = KingdomExperienceOptionKind.CivicStory, CauseTick = 10L,
					ReservedTick = 10L, EnableEpoch = 1L }, out _, out string failure), failure);
		}

		private static void ReserveBody(KingdomExperienceLedger l, string realm,
			string settlement, int i)
		{
			Assert.IsTrue(KingdomExperienceRules.TryReserveBodies(l, l.Revision,
				new KingdomExperienceBodyReservation {
					ReservationId = Id("taf:experience-body:", i),
					RealmId = realm, SettlementId = settlement, SourceId = Id("taf:cause:", i),
					Lane = KingdomExperienceLane.CivicVoices,
					OptionKind = KingdomExperienceOptionKind.CivicStory, CauseTick = 10L,
					ReservedTick = 10L, EnableEpoch = 1L, BodyCount = 1 }, 0,
				out _, out string failure), failure);
		}

		private static void AddRichCivicRows(KingdomExperienceLedger l, string[] settlements)
		{
			for (int i = 0; i < 3; i++)
			{
				string name = new string((char)('a' + i), KingdomExperienceRules.MaxCivicTextBytes);
				l.Offices.Add(new KingdomCivicOfficeReceipt { Phase = KingdomCivicOfficePhase.Held,
					Generation = 1, SettlementId = settlements[i], SettlementName = name,
					WorkId = i + 1, HolderResidentId = i + 1, HolderName = name,
					HolderObjectId = CivicId("body-holder-", i), OwnsRole = true,
					ChangedTick = 10L });
				l.Remembrances.Add(new KingdomRemembranceReceipt {
					Phase = KingdomRemembrancePhase.Projected, Generation = 1,
					SettlementId = settlements[i], SettlementName = name,
					SubjectResidentId = i + 10, SubjectName = name,
					MournerResidentId = i + 20, MournerName = name,
					CarrierObjectId = CivicId("body-carrier-", i),
					CarrierZoneId = CivicId("zone-carrier-", i), DecidedTick = 10L });
			}
		}

		private static void AddRichFirstFeasts(KingdomExperienceLedger l,
			string[] settlements)
		{
			for (int i = 0; i < settlements.Length; i++)
			{
				string transaction = new string("abc"[i], 32);
				KingdomFirstFeastDeed deed = new KingdomFirstFeastDeed {
					SettlementId = settlements[i], SettlementName = new string((char)('k' + i), 96),
					DeedText = KingdomFirstFeastRules.AuthoredDeed, DeedTick = 10L,
					GuestTerminalReceiptId = "taf:growth-first-guest-terminal:"
						+ new string("def"[i], 64), GuestTerminalDigest = new string("abc"[i], 64),
					GuestTerminalTick = 9L, AdventureEventId = "taf:adventure:" + transaction,
					AdventureFingerprint = new string("fed"[i], 64) };
				Assert.IsTrue(KingdomFirstFeastRules.TryBuildDeedId(deed, out deed.DeedId));
				KingdomFirstFeastCandidate[] people = new KingdomFirstFeastCandidate[] {
					new KingdomFirstFeastCandidate(100 + i * 2, new string((char)('p' + i), 96)),
					new KingdomFirstFeastCandidate(101 + i * 2, new string((char)('u' + i), 96)) };
				Assert.IsTrue(KingdomFirstFeastRules.TryPrepare(deed, people, 10L, 1L,
					out KingdomFirstFeastReceipt offer, out string failure), failure);
				Assert.IsTrue(KingdomExperienceRules.TryPublishFirstFeastOffer(l, l.Revision,
					offer, out failure), failure);
				Assert.IsTrue(KingdomExperienceRules.TryDecideFirstFeast(l, l.Revision,
					settlements[i], KingdomFirstFeastChoice.Adapt,
					KingdomFirstFeastRules.RemembranceDedication, 10L, out bool committed,
					out KingdomFirstFeastReceipt _, out failure), failure);
				Assert.IsTrue(committed);
			}
		}

		private static string CivicId(string prefix, int ordinal)
		{
			string suffix = ordinal.ToString("x");
			return prefix + new string('c', KingdomExperienceRules.MaxCivicTextBytes
				- prefix.Length - suffix.Length) + suffix;
		}

		private static string Id(string prefix, int ordinal)
		{
			string suffix = ordinal.ToString("x");
			return prefix + new string('x', 128 - prefix.Length - suffix.Length) + suffix;
		}

		private static int Find(byte[] haystack, byte[] needle)
		{
			for (int i = 0; i <= haystack.Length - needle.Length; i++)
			{
				int j = 0; for (; j < needle.Length && haystack[i + j] == needle[j]; j++) { }
				if (j == needle.Length) return i;
			}
			return -1;
		}
	}
}
#endif
