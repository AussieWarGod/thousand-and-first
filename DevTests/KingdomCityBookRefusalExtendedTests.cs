#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
            ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
                RungFixture.Settlement, 777, stocks, new KingdomZoneRow[0], new KingdomWorkRow[0],
                new[] { resident }, new KingdomClockRow[0], out KingdomCityState state, out _));
            var city = new KingdomCityBook();
            ClassicAssert.IsTrue(city.TryPublish(state, out _));
            if (frozen)
            {
                KingdomSubsidenceStepBook book = RungFixture.Settling(false);
                KingdomSubsidenceRungWork work = RungFixture.Work(roofs: new[] { RungFixture.Roof(11, false) });
                var plan = new KingdomSubsidenceRungPlan(book.Active.Id, book.RealmId, book.SettlementId,
                    RungFixture.Zone, GrowthStage.City, GrowthStage.Town, book.Active.DueTick,
                    RungFixture.Prepared, book.Active.Completed, new[] { work });
                ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
                ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
                city.SubsidenceModel = wire;
            }
            ClassicAssert.IsTrue(city.HasValidSubsidenceStorage());
            ClassicAssert.IsTrue(city.TryReadExact(out _, out _));
            return city;
        }

        [TestCase(false)] [TestCase(true)]
        public void OrdinaryReadRefusesAnExistingFailedLoadLatchEvenWhenColumnsAreHealthy(bool frozen)
        {
            KingdomCityBook city = Healthy(frozen);
            city.SubsidenceReadFailed = true;
            string wire = city.SubsidenceModel;
            var names = city.ResidentNames;
            ClassicAssert.IsFalse(city.TryRead(out KingdomCityState state, out KingdomCityFault fault));
            ClassicAssert.IsNull(state); ClassicAssert.AreNotEqual(KingdomCityFault.None, fault);
            ClassicAssert.IsTrue(city.SubsidenceReadFailed);
            ClassicAssert.AreEqual(wire, city.SubsidenceModel); ClassicAssert.AreSame(names, city.ResidentNames);
            ClassicAssert.IsTrue(city.TryReadExact(out _, out _), "Named-load exact validation is a separate capability.");
        }

        [TestCase(BrinkKind.Roof, false)] [TestCase(BrinkKind.Creed, false)]
        [TestCase(BrinkKind.Roof, true)] [TestCase(BrinkKind.Creed, true)]
        public void FailedLoadLatchRefusesEveryBrinkReadWithoutPublishingValues(BrinkKind kind, bool frozen)
        {
            KingdomCityBook city = Healthy(frozen);
            city.SubsidenceReadFailed = true;
            ClassicAssert.IsFalse(city.TryReadBrink(11, kind, out bool stands, out long reached,
                out long warned, out string toward, out int channel));
            ClassicAssert.IsFalse(stands); ClassicAssert.AreEqual(0, reached); ClassicAssert.AreEqual(0, warned);
            ClassicAssert.IsNull(toward); ClassicAssert.AreEqual(0, channel); ClassicAssert.IsTrue(city.SubsidenceReadFailed);
        }

        [TestCase(BrinkKind.Roof, false)] [TestCase(BrinkKind.Creed, false)]
        [TestCase(BrinkKind.Roof, true)] [TestCase(BrinkKind.Creed, true)]
        public void FailedLoadLatchRefusesEveryBrinkWriteWithoutMutatingColumns(BrinkKind kind, bool frozen)
        {
            KingdomCityBook city = Healthy(frozen);
            city.SubsidenceReadFailed = true;
            string wire = city.SubsidenceModel;
            ClassicAssert.IsFalse(city.TryWriteBrink(11, kind, true, 10, 20, "Mechanimists", 1));
            ClassicAssert.AreEqual(0, city.ResidentRoofStanding[0]); ClassicAssert.AreEqual(0, city.ResidentCreedStanding[0]);
            ClassicAssert.AreEqual(0, city.ResidentRoofTicks[0]); ClassicAssert.AreEqual(0, city.ResidentCreedTicks[0]);
            ClassicAssert.AreEqual(wire, city.SubsidenceModel); ClassicAssert.IsTrue(city.SubsidenceReadFailed);
        }

        [TestCase(BrinkKind.Roof)] [TestCase(BrinkKind.Creed)]
        public void HealthySquareFastPathDoesNotRepairUnrelatedWork(BrinkKind kind)
        {
            KingdomCityBook city = Healthy(false);
            city.WorkIds.Add(1);
            var names = city.ResidentNames;
            ClassicAssert.IsTrue(city.TryWriteBrink(11, kind, true, 10, 20, "Mechanimists", 1));
            ClassicAssert.IsTrue(city.TryReadBrink(11, kind, out bool stands, out long reached,
                out long warned, out string toward, out int channel));
            ClassicAssert.IsTrue(stands); ClassicAssert.AreEqual(10, reached); ClassicAssert.AreEqual(20, warned);
            ClassicAssert.AreEqual(kind == BrinkKind.Creed ? "Mechanimists" : null, toward);
            ClassicAssert.AreEqual(kind == BrinkKind.Creed ? 1 : 0, channel);
            ClassicAssert.AreSame(names, city.ResidentNames);
            ClassicAssert.AreEqual(1, city.WorkIds.Count); ClassicAssert.AreEqual(0, city.WorkAnchorsX.Count);
            ClassicAssert.IsFalse(city.SubsidenceReadFailed);
        }

        [TestCase(BrinkKind.Roof)] [TestCase(BrinkKind.Creed)]
        public void FrozenRaggedColumnsRefuseReadWithoutRepair(BrinkKind kind)
        {
            KingdomCityBook city = Healthy(true);
            city.ResidentRoofWarnedTicks.Clear();
            string wire = city.SubsidenceModel;
            bool accepted = true;
            Assert.DoesNotThrow(() => accepted = city.TryReadBrink(11, kind, out _, out _, out _, out _, out _));
            ClassicAssert.IsFalse(accepted); ClassicAssert.IsTrue(city.SubsidenceReadFailed);
            ClassicAssert.AreEqual(1, city.ResidentIds.Count); ClassicAssert.AreEqual(0, city.ResidentRoofWarnedTicks.Count);
            ClassicAssert.AreEqual(wire, city.SubsidenceModel);
        }

        [Test]
        public void FrozenRaggedColumnsRefuseCreedWriteWithoutRepair()
        {
            KingdomCityBook city = Healthy(true);
            city.ResidentRoofWarnedTicks.Clear();
            string wire = city.SubsidenceModel;
            bool accepted = true;
            Assert.DoesNotThrow(() => accepted = city.TryWriteBrink(11, BrinkKind.Creed, true, 10, 20, "Mechanimists", 1));
            ClassicAssert.IsFalse(accepted); ClassicAssert.IsTrue(city.SubsidenceReadFailed);
            ClassicAssert.AreEqual(1, city.ResidentIds.Count); ClassicAssert.AreEqual(0, city.ResidentRoofWarnedTicks.Count);
            ClassicAssert.AreEqual(0, city.ResidentCreedStanding[0]); ClassicAssert.AreEqual(wire, city.SubsidenceModel);
        }
    }
}
#endif
