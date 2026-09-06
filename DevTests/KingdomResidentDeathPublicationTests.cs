#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomResidentDeathPublicationTests
	{
		[TestCase(false)]
		[TestCase(true)]
		public void ExactWitnessPublishesOnlyStandingCauseDespiteFrozenRung(bool selected)
		{
			var city = Fixture(selected, out var death);
			string wire = city.SubsidenceModel;
			var names = city.ResidentNames; var roofs = city.ResidentRoofStanding;
			Assert.IsTrue(KingdomSubsidenceRungRules.BlocksProjection(wire));
			Assert.IsFalse(city.TryPublish(Read(city), out _));
			Assert.IsTrue(city.TryPublishWitnessedDeath(death, wire, () => true));
			Assert.AreEqual(1, KingdomResidentDeathRules.RowCut(death, Resident(city)));
			Assert.AreEqual(wire, city.SubsidenceModel);
			Assert.AreSame(names, city.ResidentNames); Assert.AreSame(roofs, city.ResidentRoofStanding);
			Assert.AreEqual(selected, KingdomSubsidenceRungRules.BlocksRoof(wire, 11));
			Assert.IsTrue(KingdomSubsidenceRungRules.BlocksProjection(wire));
			Assert.IsTrue(city.TryPublishWitnessedDeath(death, wire, () => true));
			Assert.AreEqual(1, KingdomResidentDeathRules.RowCut(death, Resident(city)));
		}
		[TestCase("foreign-parent")]
		[TestCase("foreign-owner")]
		[TestCase("foreign-name")]
		[TestCase("foreign-cause")]
		[TestCase("missing-row")]
		[TestCase("late-phase")]
		[TestCase("missing-intent")]
		public void ForeignOrUnprovedAuthorityDoesNotAcquireADeathCut(string change)
		{
			var city = Fixture(false, out var death); string wire = city.SubsidenceModel;
			if (change == "foreign-parent") death.StepWire = KingdomSubsidenceStepCodec.FreshWire;
			if (change == "foreign-owner") death.Settlement = KingdomIdentityRules.SettlementPrefix + new string('e', 64);
			if (change == "foreign-name") city.ResidentNames[0] = "Changed resident";
			if (change == "foreign-cause") city.ResidentCauses[0] = (int)KingdomStandingCause.Violence;
			if (change == "missing-row") city.ResidentIds[0] = 12;
			if (change == "late-phase") death.Phase = KingdomResidentDeathPhase.BindingRetired;
			string before = Shape(city);
			Assert.IsFalse(city.TryPublishWitnessedDeath(death, wire, () => change != "missing-intent"));
			Assert.AreEqual(before, Shape(city)); Assert.AreEqual(wire, city.SubsidenceModel);
		}
		[Test]
		public void AuthorityCallbackChangingNameRefusesWithoutStandingWrite()
		{
			var city = Fixture(false, out var death);
			Assert.IsFalse(city.TryPublishWitnessedDeath(death, city.SubsidenceModel,
				() => { city.ResidentNames[0] = "Callback changed name"; return true; }));
			Assert.AreEqual((int)KingdomResidentStanding.Resident, city.ResidentStandings[0]);
			Assert.AreEqual((int)KingdomStandingCause.None, city.ResidentCauses[0]);
		}
		[Test]
		public void LostPostWriteProofRetainsAfterCutForExactRetry()
		{
			var city = Fixture(false, out var death); int checks = 0;
			Assert.IsFalse(city.TryPublishWitnessedDeath(death, city.SubsidenceModel, () => ++checks == 1));
			Assert.AreEqual(1, KingdomResidentDeathRules.RowCut(death, Resident(city)));
			Assert.IsTrue(city.TryPublishWitnessedDeath(death, city.SubsidenceModel, () => true));
		}
		[Test]
		public void StandingWrittenReceiptRequiresItsOwnAfterCut()
		{
			var city = Fixture(false, out var death); death.Phase = KingdomResidentDeathPhase.StandingWritten;
			Assert.IsFalse(city.TryPublishWitnessedDeath(death, city.SubsidenceModel, () => true));
			death.Phase = KingdomResidentDeathPhase.Witnessed;
			Assert.IsTrue(city.TryPublishWitnessedDeath(death, city.SubsidenceModel, () => true));
			death.Phase = KingdomResidentDeathPhase.StandingWritten;
			Assert.IsTrue(city.TryPublishWitnessedDeath(death, city.SubsidenceModel, () => true));
		}
		private static KingdomCityBook Fixture(bool selected, out KingdomResidentDeathReceipt death)
		{
			var resident = new KingdomResidentRow(11, "Fixture resident", 0, 0, 1,
				KingdomCityRules.StableId(RungFixture.ObjectId(0)), 0, 0, KingdomDayShape.Field,
				KingdomResidentStanding.Resident, KingdomStandingCause.None, RungFixture.Zone,
				new KingdomBrinkWindow(false, 0, 0), new KingdomBrinkWindow(false, 0, 0), null, 0, null, "fixture", "");
			var stocks = new KingdomStocks(new KingdomStockPair(0, 0), new KingdomStockPair(0, 0), new KingdomStockPair(0, 0));
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				RungFixture.Settlement, 0, stocks, new KingdomZoneRow[0], new KingdomWorkRow[0], new[] { resident },
				new KingdomClockRow[0], out var state, out _));
			var city = new KingdomCityBook(); Assert.IsTrue(city.TryPublish(state, out _));
			var step = RungFixture.Settling(false);
			var work = RungFixture.Work(roofs: selected ? new[] { RungFixture.Roof(11) } : new KingdomSubsidenceRungRoof[0]);
			var plan = new KingdomSubsidenceRungPlan(step.Active.Id, step.RealmId, step.SettlementId, RungFixture.Zone,
				GrowthStage.City, GrowthStage.Town, step.Active.DueTick, RungFixture.Prepared, step.Active.Completed, new[] { work });
			Assert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(step, plan, out step));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(step, out string wire)); city.SubsidenceModel = wire;
			death = new KingdomResidentDeathReceipt { Realm = RungFixture.Realm, Settlement = RungFixture.Settlement,
				SettlementName = "Fixture town", Before = resident, Body = "roof-body-11", Zone = RungFixture.Zone,
				Tick = RungFixture.Prepared, MintedTick = 1, Cause = KingdomStandingCause.Violence,
				StepWire = wire, RoofWork = selected ? work.ObjectId : "", RoofIndex = selected ? 0 : -1,
				RoofBlocked = selected, Memory = false, Telling = KingdomResidentDeathTelling.Disabled };
			Assert.IsTrue(KingdomResidentDeathRules.Valid(death)); return city;
		}
		private static KingdomCityState Read(KingdomCityBook city)
		{ Assert.IsTrue(city.TryReadExact(out var state, out _)); return state; }
		private static KingdomResidentRow Resident(KingdomCityBook city)
		{ var state = Read(city); Assert.IsTrue(state.TryResident(0, out var row)); return row; }
		private static string Shape(KingdomCityBook city)
		{ return string.Join("|", city.ResidentIds) + ":" + string.Join("|", city.ResidentNames) + ":"
			+ string.Join("|", city.ResidentStandings) + ":" + string.Join("|", city.ResidentCauses); }
	}
}
#endif
