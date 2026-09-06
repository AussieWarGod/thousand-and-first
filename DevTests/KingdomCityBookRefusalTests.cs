#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
    public sealed class KingdomCityBookRefusalTests
    {
        private static KingdomCityBook Broken()
        {
            return new KingdomCityBook { SettlementId = "taf:city:refusal", SubsidenceModel = "broken" };
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OrdinaryReadRefusesMalformedStorageWithoutProjectionOrRepair(bool ragged)
        {
            KingdomCityBook book = Broken();
            if (ragged) book.WorkIds.Add(1);
            KingdomCityState state = null;
            KingdomCityFault fault = KingdomCityFault.None;
            bool accepted = true;
            Assert.DoesNotThrow(() => accepted = book.TryRead(out state, out fault));
            Assert.IsFalse(accepted, "Malformed durable storage must not project a healthy city.");
            Assert.IsNull(state);
            Assert.AreNotEqual(KingdomCityFault.None, fault);
            Assert.AreEqual("broken", book.SubsidenceModel);
            Assert.IsTrue(book.SubsidenceReadFailed);
            Assert.AreEqual(ragged ? 1 : 0, book.WorkIds.Count);
            Assert.AreEqual(0, book.WorkAnchorsX.Count);
        }

        [TestCase(BrinkKind.Roof)]
        [TestCase(BrinkKind.Creed)]
        public void BrinkReadRefusesWhenResidentNormalizationCannotComplete(BrinkKind kind)
        {
            KingdomCityBook book = Broken();
            book.ResidentIds.Add(1);
            bool stands = true, accepted = true;
            long reached = -1, warned = -1;
            string toward = "unset";
            int channel = -1;
            Assert.DoesNotThrow(() => accepted = book.TryReadBrink(1, kind,
                out stands, out reached, out warned, out toward, out channel));
            Assert.IsFalse(accepted);
            Assert.IsFalse(stands);
            Assert.AreEqual(0, reached);
            Assert.AreEqual(0, warned);
            Assert.IsNull(toward);
            Assert.AreEqual(0, channel);
            Assert.AreEqual(1, book.ResidentIds.Count);
            Assert.AreEqual(0, book.ResidentRoofStanding.Count);
            Assert.AreEqual(0, book.ResidentCreedStanding.Count);
        }

        [TestCase(BrinkKind.Roof)]
        [TestCase(BrinkKind.Creed)]
        public void BrinkWriteRefusesWhenResidentNormalizationCannotComplete(BrinkKind kind)
        {
            KingdomCityBook book = Broken();
            book.ResidentIds.Add(1);
            bool accepted = true;
            Assert.DoesNotThrow(() => accepted = book.TryWriteBrink(1, kind, true, 10, 20, "Mechanimists", 1));
            Assert.IsFalse(accepted);
            Assert.AreEqual("broken", book.SubsidenceModel);
            Assert.AreEqual(1, book.ResidentIds.Count);
            Assert.AreEqual(0, book.ResidentRoofStanding.Count);
            Assert.AreEqual(0, book.ResidentCreedStanding.Count);
        }

        [Test]
        public void ExactProjectionRemainsAvailableToNamedLoadValidationWhileLatchIsSet()
        {
            KingdomCityBook book = new KingdomCityBook { SettlementId = "taf:city:loading", SubsidenceReadFailed = true };
            KingdomCityState state;
            KingdomCityFault fault;
            Assert.IsTrue(book.TryReadExact(out state, out fault));
            Assert.IsNotNull(state);
            Assert.AreEqual(KingdomCityFault.None, fault);
            Assert.IsTrue(book.SubsidenceReadFailed);
        }
    }
}
#endif
