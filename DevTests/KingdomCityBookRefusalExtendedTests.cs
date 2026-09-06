#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
    public sealed class KingdomCityBookRefusalExtendedTests
    {
        private static KingdomCityBook Healthy(bool frozen)
        {
            var resident = new KingdomResidentRow(11, "guard fixture", 0, 0, 1,
                KingdomCityRules.StableId(RungFixture.ObjectId(0)), 0, 0, KingdomDayShape.Field,
                KingdomResidentStanding.Resident, KingdomStandingCause.None, RungFixture.Zone,
                new KingdomBrinkWindow(false, 0, 0), new KingdomBrinkWindow(false, 0, 0), null, 0, null, "fixture", "");
            var stocks = new KingdomStocks(new KingdomStockPair(0, 0), new KingdomStockPair(0, 0), new KingdomStockPair(0, 0));
            Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
                RungFixture.Settlement, 777, stocks, new KingdomZoneRow[0], new KingdomWorkRow[0],
                new[] { resident }, new KingdomClockRow[0], out KingdomCityState state, out _));
            var city = new KingdomCityBook();
            Assert.IsTrue(city.TryPublish(state, out _));
            if (frozen)
            {
                KingdomSubsidenceStepBook book = RungFixture.Settling(false);
                KingdomSubsidenceRungWork work = RungFixture.Work(roofs: new[] { RungFixture.Roof(11, false) });
                var plan = new KingdomSubsidenceRungPlan(book.Active.Id, book.RealmId, book.SettlementId,
                    RungFixture.Zone, GrowthStage.City, GrowthStage.Town, book.Active.DueTick,
                    RungFixture.Prepared, book.Active.Completed, new[] { work });
                Assert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
                Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
                city.SubsidenceModel = wire;
            }
            Assert.IsTrue(city.HasValidSubsidenceStorage());
            Assert.IsTrue(city.TryReadExact(out _, out _));
            return city;
        }

        [TestCase(false)] [TestCase(true)]
        public void OrdinaryReadRefusesAnExistingFailedLoadLatchEvenWhenColumnsAreHealthy(bool frozen)
        {
            KingdomCityBook city = Healthy(frozen);
            city.SubsidenceReadFailed = true;
            string wire = city.SubsidenceModel;
            var names = city.ResidentNames;
            Assert.IsFalse(city.TryRead(out KingdomCityState state, out KingdomCityFault fault));
            Assert.IsNull(state); Assert.AreNotEqual(KingdomCityFault.None, fault);
            Assert.IsTrue(city.SubsidenceReadFailed);
            Assert.AreEqual(wire, city.SubsidenceModel); Assert.AreSame(names, city.ResidentNames);
            Assert.IsTrue(city.TryReadExact(out _, out _), "Named-load exact validation is a separate capability.");
        }

        [TestCase(BrinkKind.Roof, false)] [TestCase(BrinkKind.Creed, false)]
        [TestCase(BrinkKind.Roof, true)] [TestCase(BrinkKind.Creed, true)]
        public void FailedLoadLatchRefusesEveryBrinkReadWithoutPublishingValues(BrinkKind kind, bool frozen)
        {
            KingdomCityBook city = Healthy(frozen);
            city.SubsidenceReadFailed = true;
            Assert.IsFalse(city.TryReadBrink(11, kind, out bool stands, out long reached,
                out long warned, out string toward, out int channel));
            Assert.IsFalse(stands); Assert.AreEqual(0, reached); Assert.AreEqual(0, warned);
            Assert.IsNull(toward); Assert.AreEqual(0, channel); Assert.IsTrue(city.SubsidenceReadFailed);
        }

        [TestCase(BrinkKind.Roof, false)] [TestCase(BrinkKind.Creed, false)]
        [TestCase(BrinkKind.Roof, true)] [TestCase(BrinkKind.Creed, true)]
        public void FailedLoadLatchRefusesEveryBrinkWriteWithoutMutatingColumns(BrinkKind kind, bool frozen)
        {
            KingdomCityBook city = Healthy(frozen);
            city.SubsidenceReadFailed = true;
            string wire = city.SubsidenceModel;
            Assert.IsFalse(city.TryWriteBrink(11, kind, true, 10, 20, "Mechanimists", 1));
            Assert.AreEqual(0, city.ResidentRoofStanding[0]); Assert.AreEqual(0, city.ResidentCreedStanding[0]);
            Assert.AreEqual(0, city.ResidentRoofTicks[0]); Assert.AreEqual(0, city.ResidentCreedTicks[0]);
            Assert.AreEqual(wire, city.SubsidenceModel); Assert.IsTrue(city.SubsidenceReadFailed);
        }

        [TestCase(BrinkKind.Roof)] [TestCase(BrinkKind.Creed)]
        public void HealthySquareFastPathDoesNotRepairUnrelatedWork(BrinkKind kind)
        {
            KingdomCityBook city = Healthy(false);
            city.WorkIds.Add(1);
            var names = city.ResidentNames;
            Assert.IsTrue(city.TryWriteBrink(11, kind, true, 10, 20, "Mechanimists", 1));
            Assert.IsTrue(city.TryReadBrink(11, kind, out bool stands, out long reached,
                out long warned, out string toward, out int channel));
            Assert.IsTrue(stands); Assert.AreEqual(10, reached); Assert.AreEqual(20, warned);
            Assert.AreEqual(kind == BrinkKind.Creed ? "Mechanimists" : null, toward);
            Assert.AreEqual(kind == BrinkKind.Creed ? 1 : 0, channel);
            Assert.AreSame(names, city.ResidentNames);
            Assert.AreEqual(1, city.WorkIds.Count); Assert.AreEqual(0, city.WorkAnchorsX.Count);
            Assert.IsFalse(city.SubsidenceReadFailed);
        }

        [TestCase(BrinkKind.Roof)] [TestCase(BrinkKind.Creed)]
        public void FrozenRaggedColumnsRefuseReadWithoutRepair(BrinkKind kind)
        {
            KingdomCityBook city = Healthy(true);
            city.ResidentRoofWarnedTicks.Clear();
            string wire = city.SubsidenceModel;
            bool accepted = true;
            Assert.DoesNotThrow(() => accepted = city.TryReadBrink(11, kind, out _, out _, out _, out _, out _));
            Assert.IsFalse(accepted); Assert.IsTrue(city.SubsidenceReadFailed);
            Assert.AreEqual(1, city.ResidentIds.Count); Assert.AreEqual(0, city.ResidentRoofWarnedTicks.Count);
            Assert.AreEqual(wire, city.SubsidenceModel);
        }

        [Test]
        public void FrozenRaggedColumnsRefuseCreedWriteWithoutRepair()
        {
            KingdomCityBook city = Healthy(true);
            city.ResidentRoofWarnedTicks.Clear();
            string wire = city.SubsidenceModel;
            bool accepted = true;
            Assert.DoesNotThrow(() => accepted = city.TryWriteBrink(11, BrinkKind.Creed, true, 10, 20, "Mechanimists", 1));
            Assert.IsFalse(accepted); Assert.IsTrue(city.SubsidenceReadFailed);
            Assert.AreEqual(1, city.ResidentIds.Count); Assert.AreEqual(0, city.ResidentRoofWarnedTicks.Count);
            Assert.AreEqual(0, city.ResidentCreedStanding[0]); Assert.AreEqual(wire, city.SubsidenceModel);
        }
    }
}
#endif
