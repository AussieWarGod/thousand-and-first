#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomRemembranceRulesTests
	{
		private const string Realm = "taf:realm:remembrance-tests";

		private static KingdomExperienceLedger Bound()
		{
			KingdomExperienceLedger ledger = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger, Realm,
				out string failure), failure); return ledger;
		}

		[Test]
		public void ProjectionIsExactlyOnceCodecStableAndLossCannotDuplicate()
		{
			KingdomExperienceLedger ledger = Bound();
			Assert.IsTrue(Prepare(ledger, "one", 7, "Ari", 8, "Ula", "cairn-1", 20));
			Assert.IsTrue(KingdomExperienceRules.TryGetRemembrance(ledger,
				"taf:settlement:one", out KingdomRemembranceReceipt prepared,
				out string failure), failure);
			Assert.AreEqual(KingdomRemembrancePhase.ProjectionPrepared, prepared.Phase);
			byte[] once = Bytes(ledger);
			Assert.IsTrue(KingdomExperienceRules.TryPrepareRemembranceProjection(ledger,
				ledger.Revision - 1, prepared.SettlementId, prepared.SettlementName,
				prepared.SubjectResidentId, prepared.SubjectName, prepared.MournerResidentId,
				prepared.MournerName, prepared.CarrierObjectId, prepared.CarrierZoneId,
				prepared.DecidedTick, out failure), failure);
			CollectionAssert.AreEqual(once, Bytes(ledger));
			Assert.IsTrue(KingdomExperienceRules.TryCompleteRemembranceProjection(ledger,
				ledger.Revision, prepared.SettlementId, prepared.Generation, out failure), failure);
			KingdomExperienceLedger loaded = KingdomExperienceCodec.DecodeEnvelope(Bytes(ledger));
			Assert.IsTrue(KingdomExperienceRules.TryGetRemembrance(loaded,
				prepared.SettlementId, out KingdomRemembranceReceipt projected, out failure), failure);
			Assert.AreEqual(KingdomRemembrancePhase.Projected, projected.Phase);
			Assert.IsTrue(KingdomExperienceRules.TryMarkRemembranceLost(loaded,
				loaded.Revision, projected.SettlementId, projected.CarrierObjectId,
				out failure), failure);
			Assert.AreEqual(KingdomRemembrancePhase.Lost, loaded.Remembrances[0].Phase);
			byte[] lost = Bytes(loaded);
			Assert.IsFalse(Prepare(loaded, "one", 9, "Other", 8, "Ula", "cairn-2", 30));
			CollectionAssert.AreEqual(lost, Bytes(loaded));
		}

		[Test]
		public void PreparedCarrierLossRetainsPermanentSemanticRow()
		{
			KingdomExperienceLedger ledger = Bound();
			Assert.IsTrue(Prepare(ledger, "one", 7, "Ari", 8, "Ula", "cairn-1", 20));
			Assert.IsTrue(KingdomExperienceRules.TryMarkRemembranceLost(ledger,
				ledger.Revision, "taf:settlement:one", "cairn-1", out string failure), failure);
			Assert.AreEqual(KingdomRemembrancePhase.Lost, ledger.Remembrances[0].Phase);
			Assert.AreEqual(7, ledger.Remembrances[0].SubjectResidentId);
			Assert.AreEqual("Ari", ledger.Remembrances[0].SubjectName);
		}

		[Test]
		public void DeclineIsTerminalIdempotentAndNeverChangesDeathOrStandingTruth()
		{
			KingdomExperienceLedger ledger = Bound();
			Assert.IsTrue(Eligible(ledger, "one", 7, "Ari", 20));
			long beforeRevision = ledger.Revision;
			Assert.IsTrue(KingdomExperienceRules.TryDeclineRemembrance(ledger, ledger.Revision,
				"taf:settlement:one", "City one", 7, "Ari", 8, "Ula", 20,
				out string failure), failure);
			Assert.AreEqual(beforeRevision + 1, ledger.Revision);
			Assert.AreEqual(KingdomRemembrancePhase.Declined, ledger.Remembrances[0].Phase);
			byte[] declined = Bytes(ledger);
			Assert.IsTrue(KingdomExperienceRules.TryDeclineRemembrance(ledger,
				beforeRevision, "taf:settlement:one", "City one", 7, "Ari", 8, "Ula", 99,
				out failure), failure);
			CollectionAssert.AreEqual(declined, Bytes(ledger));
			Assert.IsFalse(KingdomExperienceRules.TryDeclineRemembrance(ledger,
				ledger.Revision, "taf:settlement:one", "City one", 9, "Other", 8, "Ula", 30,
				out failure));
			CollectionAssert.AreEqual(declined, Bytes(ledger));
		}

		[Test]
		public void RealmCapAndWrongRevisionRefuseWithoutMutation()
		{
			KingdomExperienceLedger ledger = Bound();
			Assert.IsTrue(Eligible(ledger, "one", 1, "A", 10));
			byte[] eligible = Bytes(ledger);
			Assert.IsFalse(KingdomExperienceRules.TryPrepareRemembranceProjection(ledger,
				ledger.Revision + 1, "taf:settlement:one", "One", 1, "A", 2, "B",
				"cairn", "zone", 10, out string _));
			CollectionAssert.AreEqual(eligible, Bytes(ledger));
			for (int i = 1; i < KingdomExperienceRules.MaxRemembranceReceipts; i++)
				Assert.IsTrue(Prepare(ledger, "city" + i, i + 1, "Dead" + i, i + 11,
					"Mourner" + i, "cairn-" + i, 20 + i));
			byte[] atCap = Bytes(ledger);
			Assert.IsFalse(Prepare(ledger, "city3", 4, "Dead3", 14, "Mourner3",
				"cairn-3", 24));
			CollectionAssert.AreEqual(atCap, Bytes(ledger));
		}

		[Test]
		public void MaximumCivicRowsStayInsideApprovedSixteenKibEnvelope()
		{
			KingdomExperienceLedger ledger = Bound();
			string text = new string('n', KingdomExperienceRules.MaxCivicTextBytes);
			for (int i = 0; i < 3; i++)
			{
				string settlement = "taf:settlement:" + new string((char)('a' + i), 80);
				string officeBody = new string((char)('g' + i),
					KingdomExperienceRules.MaxCivicTextBytes);
				string carrier = new string((char)('p' + i),
					KingdomExperienceRules.MaxCivicTextBytes);
				string zone = new string((char)('u' + i),
					KingdomExperienceRules.MaxCivicTextBytes);
				Assert.IsTrue(KingdomExperienceRules.TryPrepareOfficeAppointment(ledger,
					ledger.Revision, settlement, text, i + 1, i + 1, text, officeBody, true,
					10 + i, out string failure), failure);
				Assert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(ledger,
					ledger.Revision, settlement, 1, out failure), failure);
				Assert.IsTrue(KingdomExperienceRules.TryCreateRemembranceEligibility(ledger,
					ledger.Revision, settlement, text, i + 11, text, 20 + i, out failure), failure);
				Assert.IsTrue(KingdomExperienceRules.TryPrepareRemembranceProjection(ledger,
					ledger.Revision, settlement, text, i + 11, text, i + 21, text,
					carrier, zone, 30 + i, out failure), failure);
				Assert.IsTrue(KingdomExperienceRules.TryCompleteRemembranceProjection(ledger,
					ledger.Revision, settlement, 1, out failure), failure);
			}
			byte[] envelope = Bytes(ledger);
			Assert.LessOrEqual(envelope.Length, KingdomExperienceCodec.MaxEnvelopeBytes);
			Assert.LessOrEqual(envelope.Length,
				KingdomExperienceRules.MaxDeclaredPayloadBytes + 12);
			Assert.IsTrue(KingdomExperienceRules.TryValidate(ledger, out string valid), valid);
		}

		[Test]
		public void OnlyExactDirectWitnessEligibilityCanBeUsedAndItNeverExpires()
		{
			KingdomExperienceLedger ledger = Bound(); byte[] empty = Bytes(ledger);
			Assert.IsFalse(KingdomExperienceRules.TryPrepareRemembranceProjection(ledger,
				ledger.Revision, "taf:settlement:one", "City one", 7, "Ari", 8, "Ula",
				"cairn-1", "zone", 20, out string failure));
			StringAssert.Contains("witnessed", failure);
			CollectionAssert.AreEqual(empty, Bytes(ledger));

			Assert.IsTrue(Eligible(ledger, "one", 7, "Ari", 20));
			byte[] once = Bytes(ledger);
			KingdomExperienceLedger loaded = KingdomExperienceCodec.DecodeEnvelope(once);
			Assert.AreEqual(KingdomRemembrancePhase.Eligible, loaded.Remembrances[0].Phase);
			Assert.AreEqual(20L, loaded.Remembrances[0].DecidedTick);
			Assert.IsTrue(KingdomExperienceRules.TryCreateRemembranceEligibility(ledger,
				ledger.Revision - 1, "taf:settlement:one", "City one", 7, "Ari", 20,
				out failure), failure);
			CollectionAssert.AreEqual(once, Bytes(ledger));
			Assert.IsFalse(KingdomExperienceRules.TryCreateRemembranceEligibility(ledger,
				ledger.Revision, "taf:settlement:one", "City one", 9, "Other", 21,
				out failure));
			CollectionAssert.AreEqual(once, Bytes(ledger));
			Assert.IsTrue(KingdomExperienceRules.TryPrepareRemembranceProjection(ledger,
				ledger.Revision, "taf:settlement:one", "City one", 7, "Ari", 8, "Ula",
				"cairn-1", "zone", long.MaxValue, out failure), failure);
			Assert.AreEqual(20L, ledger.Remembrances[0].DecidedTick);
		}

		[Test]
		public void CivicRowsCannotShareResidentSubjectOrProjectionIdentity()
		{
			KingdomExperienceLedger ledger = Bound();
			Assert.IsTrue(KingdomExperienceRules.TryPrepareOfficeAppointment(ledger,
				ledger.Revision, "taf:settlement:one", "One", 1, 7, "Ari", "shared", true,
				10, out string failure), failure);
			Assert.IsTrue(Eligible(ledger, "two", 8, "Dead", 11));
			byte[] before = Bytes(ledger);
			Assert.IsFalse(KingdomExperienceRules.TryPrepareRemembranceProjection(ledger,
				ledger.Revision, "taf:settlement:two", "City two", 8, "Dead", 9, "Ula",
				"shared", "zone", 12, out failure));
			StringAssert.Contains("office and remembrance", failure);
			CollectionAssert.AreEqual(before, Bytes(ledger));

			KingdomExperienceLedger duplicate = KingdomExperienceRules.Clone(ledger);
			duplicate.Remembrances.Add(new KingdomRemembranceReceipt
			{
				Phase = KingdomRemembrancePhase.Eligible, Generation = 1,
				SettlementId = "taf:settlement:zzthree", SettlementName = "Three",
				SubjectResidentId = 8, SubjectName = "Dead", DecidedTick = 13
			});
			Assert.IsFalse(KingdomExperienceRules.TryValidate(duplicate, out failure));
			StringAssert.Contains("one death", failure);
		}

		private static bool Prepare(KingdomExperienceLedger L, string Suffix,
			int SubjectId, string Subject, int MournerId, string Mourner, string Carrier,
			long Tick)
		{
			if (!Eligible(L, Suffix, SubjectId, Subject, Tick)) return false;
			return KingdomExperienceRules.TryPrepareRemembranceProjection(L, L.Revision,
				"taf:settlement:" + Suffix, "City " + Suffix, SubjectId, Subject,
				MournerId, Mourner, Carrier, "JoppaWorld.1.1.1.1.10", Tick, out string _);
		}

		private static bool Eligible(KingdomExperienceLedger L, string Suffix,
			int SubjectId, string Subject, long Tick)
		{
			return KingdomExperienceRules.TryCreateRemembranceEligibility(L, L.Revision,
				"taf:settlement:" + Suffix, "City " + Suffix, SubjectId, Subject, Tick,
				out string _);
		}

		private static byte[] Bytes(KingdomExperienceLedger L)
		{
			return KingdomExperienceCodec.EncodeEnvelope(L);
		}
	}
}
#endif
