#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
			ClassicAssert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger, Realm,
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
			ClassicAssert.IsTrue(KingdomOfficeOfferRules.TryOffer(rows, out KingdomOfficeCandidate first,
				out KingdomOfficeCandidate second));
			ClassicAssert.AreEqual(1, first.ResidentId);
			ClassicAssert.AreEqual(2, second.ResidentId);
			first.Name = "mutated copy";
			ClassicAssert.AreEqual("First", rows[1].Name);
			ClassicAssert.IsFalse(KingdomOfficeOfferRules.TryOffer(new KingdomOfficeCandidate[]
				{ Candidate(1, "Only", 1) }, out first, out second));
			ClassicAssert.IsNull(first); ClassicAssert.IsNull(second);
		}

		[Test]
		public void AppointmentIsCasBoundIdempotentAndCodecStable()
		{
			KingdomExperienceLedger ledger = Bound();
			long expected = ledger.Revision;
			ClassicAssert.IsTrue(Prepare(ledger, "one", 11, "Ari", "body-11", true, 20),
				"appointment must prepare");
			ClassicAssert.IsTrue(KingdomExperienceRules.TryGetOffice(ledger,
				"taf:settlement:one", out KingdomCivicOfficeReceipt prepared,
				out string failure), failure);
			ClassicAssert.AreEqual(KingdomCivicOfficePhase.AppointmentPrepared, prepared.Phase);
			byte[] once = Bytes(ledger);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPrepareOfficeAppointment(ledger, expected,
				prepared.SettlementId, prepared.SettlementName, prepared.WorkId,
				prepared.HolderResidentId, prepared.HolderName, prepared.HolderObjectId,
				prepared.OwnsRole, prepared.ChangedTick, out failure), failure);
			CollectionAssert.AreEqual(once, Bytes(ledger), "exact retry is a no-write success");
			ClassicAssert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(ledger,
				ledger.Revision, prepared.SettlementId, prepared.Generation, out failure), failure);
			KingdomExperienceLedger loaded = KingdomExperienceCodec.DecodeEnvelope(Bytes(ledger));
			ClassicAssert.IsTrue(KingdomExperienceRules.TryGetOffice(loaded, prepared.SettlementId,
				out KingdomCivicOfficeReceipt held, out failure), failure);
			ClassicAssert.AreEqual(KingdomCivicOfficePhase.Held, held.Phase);
			ClassicAssert.AreEqual(11, held.HolderResidentId);
			ClassicAssert.AreEqual(7, held.WorkId);
			ClassicAssert.IsTrue(held.OwnsRole);
		}

		[Test]
		public void WrongRevisionAndWrongHolderAreByteStable()
		{
			KingdomExperienceLedger ledger = Bound(); byte[] before = Bytes(ledger);
			ClassicAssert.IsFalse(KingdomExperienceRules.TryPrepareOfficeAppointment(ledger,
				ledger.Revision + 1, "taf:settlement:one", "One", 7, 11, "Ari",
				"body-11", true, 20, out string _));
			CollectionAssert.AreEqual(before, Bytes(ledger));
			ClassicAssert.IsTrue(Prepare(ledger, "one", 11, "Ari", "body-11", true, 20));
			ClassicAssert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(ledger,
				ledger.Revision, "taf:settlement:one", 1, out string failure), failure);
			before = Bytes(ledger);
			ClassicAssert.IsFalse(KingdomExperienceRules.TryPrepareOfficeVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 12,
				KingdomCivicOfficeVacancyCause.Death, 30, out failure));
			CollectionAssert.AreEqual(before, Bytes(ledger));
		}

		[Test]
		public void HolderLossCreatesPlainVacancyAndPredecessorWithoutSuccessor()
		{
			KingdomExperienceLedger ledger = Bound();
			ClassicAssert.IsTrue(Prepare(ledger, "one", 11, "Ari", "body-11", false, 20));
			ClassicAssert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(ledger,
				ledger.Revision, "taf:settlement:one", 1, out string failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPrepareOfficeVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 11,
				KingdomCivicOfficeVacancyCause.Death, 30, out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1,
				City("taf:settlement:one", Resident(11, "Ari",
					KingdomResidentStanding.Dead, KingdomStandingCause.Violence)), out failure),
				failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryGetOffice(ledger, "taf:settlement:one",
				out KingdomCivicOfficeReceipt row, out failure), failure);
			ClassicAssert.AreEqual(KingdomCivicOfficePhase.Vacant, row.Phase);
			ClassicAssert.AreEqual(0, row.HolderResidentId);
			ClassicAssert.IsNull(row.HolderName);
			ClassicAssert.AreEqual(11, row.PredecessorResidentId);
			ClassicAssert.AreEqual("Ari", row.PredecessorName);
			ClassicAssert.IsFalse(row.OwnsRole, "borrowed and owned roles both leave no removal claim");
		}

		[Test]
		public void DeathVacancyNeedsExactTerminalRowThenAllowsNewAppointment()
		{
			KingdomExperienceLedger ledger = Bound();
			ClassicAssert.IsTrue(Prepare(ledger, "one", 11, "Ari", "body-11", true, 20));
			ClassicAssert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(ledger,
				ledger.Revision, "taf:settlement:one", 1, out string failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPrepareOfficeVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 11,
				KingdomCivicOfficeVacancyCause.Death, 30, out failure), failure);
			byte[] prepared = Bytes(ledger);

			ClassicAssert.IsFalse(KingdomExperienceRules.TryCompleteOfficeVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1, out failure));
			CollectionAssert.AreEqual(prepared, Bytes(ledger));
			ClassicAssert.IsFalse(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1,
				City("taf:settlement:one", Resident(11, "Ari",
					KingdomResidentStanding.Abroad, KingdomStandingCause.Astray)), out failure));
			CollectionAssert.AreEqual(prepared, Bytes(ledger));
			ClassicAssert.IsFalse(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1,
				City("taf:settlement:other", Resident(11, "Ari",
					KingdomResidentStanding.Dead, KingdomStandingCause.Violence)), out failure));
			CollectionAssert.AreEqual(prepared, Bytes(ledger));

			ClassicAssert.IsTrue(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(ledger,
				ledger.Revision, "taf:settlement:one", 1,
				City("taf:settlement:one", Resident(11, "Ari",
					KingdomResidentStanding.Dead, KingdomStandingCause.Violence)), out failure),
				failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryGetOffice(ledger, "taf:settlement:one",
				out KingdomCivicOfficeReceipt vacant, out failure), failure);
			ClassicAssert.AreEqual(KingdomCivicOfficePhase.Vacant, vacant.Phase);
			ClassicAssert.AreEqual(KingdomCivicOfficeVacancyCause.Death, vacant.VacancyCause);
			ClassicAssert.AreEqual(11, vacant.PredecessorResidentId);

			ClassicAssert.IsTrue(Prepare(ledger, "one", 12, "Bex", "body-12", false, 40));
			ClassicAssert.IsTrue(KingdomExperienceRules.TryGetOffice(ledger, "taf:settlement:one",
				out KingdomCivicOfficeReceipt successor, out failure), failure);
			ClassicAssert.AreEqual(KingdomCivicOfficePhase.AppointmentPrepared, successor.Phase);
			ClassicAssert.AreEqual(2, successor.Generation);
			ClassicAssert.AreEqual(12, successor.HolderResidentId);

			KingdomExperienceLedger departure = Bound();
			ClassicAssert.IsTrue(Prepare(departure, "two", 21, "Cai", "body-21", true, 20));
			ClassicAssert.IsTrue(KingdomExperienceRules.TryCompleteOfficeAppointment(departure,
				departure.Revision, "taf:settlement:two", 1, out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryPrepareOfficeVacancy(departure,
				departure.Revision, "taf:settlement:two", 21,
				KingdomCivicOfficeVacancyCause.Departure, 30, out failure), failure);
			byte[] departurePrepared = Bytes(departure);
			ClassicAssert.IsFalse(KingdomExperienceRules.TryCompleteOfficeDeathVacancy(departure,
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
				ClassicAssert.IsTrue(Prepare(ledger, "city" + i, i + 1, "Holder" + i,
					"body-" + i, true, 10 + i));
			ClassicAssert.AreEqual(3, ledger.Offices.Count);
			byte[] atCap = Bytes(ledger);
			ClassicAssert.IsFalse(Prepare(ledger, "city3", 4, "Holder3", "body-3", true, 14));
			CollectionAssert.AreEqual(atCap, Bytes(ledger));
			ClassicAssert.IsFalse(KingdomExperienceRules.TryRebindEmptyIdentity(ledger,
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
			ClassicAssert.AreEqual(KingdomExperienceRules.CurrentFormatVersion, migrated.FormatVersion);
			ClassicAssert.AreEqual(0, migrated.Offices.Count);
			ClassicAssert.AreEqual(0, migrated.Remembrances.Count);
			byte[] current = KingdomExperienceCodec.EncodeEnvelope(migrated);
			ClassicAssert.AreEqual(KingdomExperienceCodec.CurrentWireVersion,
				BitConverter.ToInt32(current, 4));

			legacy[16] = 99;
			KingdomExperienceLedger quarantined = KingdomExperienceCodec.DecodeEnvelope(legacy);
			ClassicAssert.AreEqual(KingdomExperienceSchemaState.Quarantined,
				quarantined.SchemaState);
			ClassicAssert.AreEqual(1, quarantined.OpaqueWireVersion);
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
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion,
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
