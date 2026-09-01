#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCivicOfficeRulesTests
	{
		private const string Realm = "taf:realm:civic-office-tests";

		private static KingdomExperienceLedger Bound()
		{
			KingdomExperienceLedger ledger = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger, Realm,
				out string failure), failure); return ledger;
		}

		[Test]
		public void OfferIsExactlyTwoDeterministicEligibleCopiesOrVacancy()
		{
			KingdomOfficeCandidate[] rows = new KingdomOfficeCandidate[]
			{
				Candidate(3, "Third", 30), Candidate(1, "First", 10),
				Candidate(2, "Second", 10), Candidate(4, "Away", 1, false)
			};
			Assert.IsTrue(KingdomOfficeOfferRules.TryOffer(rows, out KingdomOfficeCandidate first,
				out KingdomOfficeCandidate second));
			Assert.AreEqual(1, first.ResidentId);
			Assert.AreEqual(2, second.ResidentId);
			first.Name = "mutated copy";
			Assert.AreEqual("First", rows[1].Name);
			Assert.IsFalse(KingdomOfficeOfferRules.TryOffer(new KingdomOfficeCandidate[]
				{ Candidate(1, "Only", 1) }, out first, out second));
			Assert.IsNull(first); Assert.IsNull(second);
		}

		[Test]
		public void AppointmentIsCasBoundIdempotentAndCodecStable()
		{
			KingdomExperienceLedger ledger = Bound();
			long expected = ledger.Revision;
			Assert.IsTrue(Prepare(ledger, "one", 11, "Ari", "body-11", true, 20),
				"appointment must prepare");
			Assert.IsTrue(KingdomExperienceRules.TryGetOffice(ledger,
				"taf:settlement:one", out KingdomCivicOfficeReceipt prepared,
				out string failure), failure);
			Assert.AreEqual(KingdomCivicOfficePhase.AppointmentPrepared, prepared.Phase);
			byte[] once = Bytes(ledger);
			Assert.IsTrue(KingdomExperienceRules.TryPrepareOfficeAppointment(ledger, expected,
				prepared.SettlementId, prepared.SettlementName, prepared.WorkId,
				prepared.HolderResidentId, prepared.HolderName, prepared.HolderObjectId,
				prepared.OwnsRole, prepared.ChangedTick, out failure), failure);
			CollectionAssert.AreEqual(once, Bytes(ledger), "exact retry is a no-write success");
			Assert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(ledger,
				ledger.Revision, prepared.SettlementId, prepared.Generation, out failure), failure);
			KingdomExperienceLedger loaded = KingdomExperienceCodec.DecodeEnvelope(Bytes(ledger));
			Assert.IsTrue(KingdomExperienceRules.TryGetOffice(loaded, prepared.SettlementId,
				out KingdomCivicOfficeReceipt held, out failure), failure);
			Assert.AreEqual(KingdomCivicOfficePhase.Held, held.Phase);
			Assert.AreEqual(11, held.HolderResidentId);
			Assert.AreEqual(7, held.WorkId);
			Assert.IsTrue(held.OwnsRole);
		}

		[Test]
		public void WrongRevisionAndWrongHolderAreByteStable()
		{
			KingdomExperienceLedger ledger = Bound(); byte[] before = Bytes(ledger);
			Assert.IsFalse(KingdomExperienceRules.TryPrepareOfficeAppointment(ledger,
				ledger.Revision + 1, "taf:settlement:one", "One", 7, 11, "Ari",
				"body-11", true, 20, out string _));
			CollectionAssert.AreEqual(before, Bytes(ledger));
			Assert.IsTrue(Prepare(ledger, "one", 11, "Ari", "body-11", true, 20));
			Assert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(ledger,
				ledger.Revision, "taf:settlement:one", 1, out string failure), failure);
			before = Bytes(ledger);
			Assert.IsFalse(KingdomExperienceRules.TryPrepareOfficeVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 12,
				KingdomCivicOfficeVacancyCause.Death, 30, out failure));
			CollectionAssert.AreEqual(before, Bytes(ledger));
		}

		[Test]
		public void HolderLossCreatesPlainVacancyAndPredecessorWithoutSuccessor()
		{
			KingdomExperienceLedger ledger = Bound();
			Assert.IsTrue(Prepare(ledger, "one", 11, "Ari", "body-11", false, 20));
			Assert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(ledger,
				ledger.Revision, "taf:settlement:one", 1, out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryPrepareOfficeVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 11,
				KingdomCivicOfficeVacancyCause.Death, 30, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1,
				City("taf:settlement:one", Resident(11, "Ari",
					KingdomResidentStanding.Dead, KingdomStandingCause.Violence)), out failure),
				failure);
			Assert.IsTrue(KingdomExperienceRules.TryGetOffice(ledger, "taf:settlement:one",
				out KingdomCivicOfficeReceipt row, out failure), failure);
			Assert.AreEqual(KingdomCivicOfficePhase.Vacant, row.Phase);
			Assert.AreEqual(0, row.HolderResidentId);
			Assert.IsNull(row.HolderName);
			Assert.AreEqual(11, row.PredecessorResidentId);
			Assert.AreEqual("Ari", row.PredecessorName);
			Assert.IsFalse(row.OwnsRole, "borrowed and owned roles both leave no removal claim");
		}

		[Test]
		public void DeathVacancyNeedsExactTerminalRowThenAllowsNewAppointment()
		{
			KingdomExperienceLedger ledger = Bound();
			Assert.IsTrue(Prepare(ledger, "one", 11, "Ari", "body-11", true, 20));
			Assert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(ledger,
				ledger.Revision, "taf:settlement:one", 1, out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryPrepareOfficeVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 11,
				KingdomCivicOfficeVacancyCause.Death, 30, out failure), failure);
			byte[] prepared = Bytes(ledger);

			Assert.IsFalse(KingdomExperienceRules.TryCompleteOfficeVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1, out failure));
			CollectionAssert.AreEqual(prepared, Bytes(ledger));
			Assert.IsFalse(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1,
				City("taf:settlement:one", Resident(11, "Ari",
					KingdomResidentStanding.Abroad, KingdomStandingCause.Astray)), out failure));
			CollectionAssert.AreEqual(prepared, Bytes(ledger));
			Assert.IsFalse(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1,
				City("taf:settlement:other", Resident(11, "Ari",
					KingdomResidentStanding.Dead, KingdomStandingCause.Violence)), out failure));
			CollectionAssert.AreEqual(prepared, Bytes(ledger));

			Assert.IsTrue(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1,
				City("taf:settlement:one", Resident(11, "Ari",
					KingdomResidentStanding.Dead, KingdomStandingCause.Violence)), out failure),
				failure);
			Assert.IsTrue(KingdomExperienceRules.TryGetOffice(ledger, "taf:settlement:one",
				out KingdomCivicOfficeReceipt vacant, out failure), failure);
			Assert.AreEqual(KingdomCivicOfficePhase.Vacant, vacant.Phase);
			Assert.AreEqual(KingdomCivicOfficeVacancyCause.Death, vacant.VacancyCause);
			Assert.AreEqual(11, vacant.PredecessorResidentId);

			Assert.IsTrue(Prepare(ledger, "one", 12, "Bex", "body-12", false, 40));
			Assert.IsTrue(KingdomExperienceRules.TryGetOffice(ledger, "taf:settlement:one",
				out KingdomCivicOfficeReceipt successor, out failure), failure);
			Assert.AreEqual(KingdomCivicOfficePhase.AppointmentPrepared, successor.Phase);
			Assert.AreEqual(2, successor.Generation);
			Assert.AreEqual(12, successor.HolderResidentId);

			KingdomExperienceLedger departure = Bound();
			Assert.IsTrue(Prepare(departure, "two", 21, "Cai", "body-21", true, 20));
			Assert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(departure,
				departure.Revision, "taf:settlement:two", 1, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryPrepareOfficeVacancy(departure,
				departure.Revision, "taf:settlement:two", 21,
				KingdomCivicOfficeVacancyCause.Departure, 30, out failure), failure);
			byte[] departurePrepared = Bytes(departure);
			Assert.IsFalse(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(departure,
				departure.Revision, "taf:settlement:two", 1,
				City("taf:settlement:two", Resident(21, "Cai",
					KingdomResidentStanding.Dead, KingdomStandingCause.Violence)), out failure));
			CollectionAssert.AreEqual(departurePrepared, Bytes(departure));
		}

		[Test]
		public void OneOfficePerSettlementAndRealmCapRefuseFourthWithoutMutation()
		{
			KingdomExperienceLedger ledger = Bound();
			for (int i = 0; i < KingdomExperienceRules.MaxOfficeReceipts; i++)
				Assert.IsTrue(Prepare(ledger, "city" + i, i + 1, "Holder" + i,
					"body-" + i, true, 10 + i));
			Assert.AreEqual(3, ledger.Offices.Count);
			byte[] atCap = Bytes(ledger);
			Assert.IsFalse(Prepare(ledger, "city3", 4, "Holder3", "body-3", true, 14));
			CollectionAssert.AreEqual(atCap, Bytes(ledger));
			Assert.IsFalse(KingdomExperienceRules.TryRebindEmptyIdentity(ledger,
				"taf:realm:other", out string failure));
			StringAssert.Contains("explicit realm retirement", failure);
			CollectionAssert.AreEqual(atCap, Bytes(ledger));
		}

		[Test]
		public void LegacyWireMigratesEmptyCivicRowsAndMalformedV1RoundTripsExactly()
		{
			KingdomExperienceLedger source = Bound();
			byte[] legacy = KingdomExperienceCodec.EncodeLegacyV1Fixture(source);
			KingdomExperienceLedger migrated = KingdomExperienceCodec.DecodeEnvelope(legacy);
			Assert.AreEqual(KingdomExperienceRules.CurrentFormatVersion, migrated.FormatVersion);
			Assert.AreEqual(0, migrated.Offices.Count);
			Assert.AreEqual(0, migrated.Remembrances.Count);
			byte[] current = KingdomExperienceCodec.EncodeEnvelope(migrated);
			Assert.AreEqual(KingdomExperienceCodec.CurrentWireVersion,
				BitConverter.ToInt32(current, 4));

			legacy[16] = 99;
			KingdomExperienceLedger quarantined = KingdomExperienceCodec.DecodeEnvelope(legacy);
			Assert.AreEqual(KingdomExperienceSchemaState.Quarantined,
				quarantined.SchemaState);
			Assert.AreEqual(1, quarantined.OpaqueWireVersion);
			CollectionAssert.AreEqual(legacy, KingdomExperienceCodec.EncodeEnvelope(quarantined));
		}

		private static KingdomOfficeCandidate Candidate(int Id, string Name, long Arrived,
			bool Eligible = true)
		{
			return new KingdomOfficeCandidate { ResidentId = Id, Name = Name,
				Origin = "Kyakukya", ArrivedTick = Arrived, Eligible = Eligible };
		}

		private static bool Prepare(KingdomExperienceLedger L, string Suffix, int Resident,
			string Name, string Body, bool Owns, long Tick)
		{
			return KingdomExperienceRules.TryPrepareOfficeAppointment(L, L.Revision,
				"taf:settlement:" + Suffix, "City " + Suffix, 7, Resident, Name, Body,
				Owns, Tick, out string _);
		}

		private static KingdomResidentRow Resident(int Id, string Name,
			KingdomResidentStanding Standing, KingdomStandingCause Cause)
		{
			return new KingdomResidentRow(Id, Name, 0, 0, 10L, 0, 0, 0,
				KingdomDayShape.Hearth, Standing, Cause, "taf:zone:one",
				KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);
		}

		private static KingdomCityState City(string SettlementId,
			params KingdomResidentRow[] Residents)
		{
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion,
				KingdomCityRules.RulesVersion, SettlementId, 30L, default(KingdomStocks),
				null, null, Residents, null, out KingdomCityState state,
				out KingdomCityFault fault), fault.ToString());
			return state;
		}

		private static byte[] Bytes(KingdomExperienceLedger L)
		{
			return KingdomExperienceCodec.EncodeEnvelope(L);
		}
	}
}
#endif
