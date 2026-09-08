#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
            ClassicAssert.IsFalse(accepted, "Malformed durable storage must not project a healthy city.");
            ClassicAssert.IsNull(state);
            ClassicAssert.AreNotEqual(KingdomCityFault.None, fault);
            ClassicAssert.AreEqual("broken", book.SubsidenceModel);
            ClassicAssert.IsTrue(book.SubsidenceReadFailed);
            ClassicAssert.AreEqual(ragged ? 1 : 0, book.WorkIds.Count);
            ClassicAssert.AreEqual(0, book.WorkAnchorsX.Count);
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
            ClassicAssert.IsFalse(accepted);
            ClassicAssert.IsFalse(stands);
            ClassicAssert.AreEqual(0, reached);
            ClassicAssert.AreEqual(0, warned);
            ClassicAssert.IsNull(toward);
            ClassicAssert.AreEqual(0, channel);
            ClassicAssert.AreEqual(1, book.ResidentIds.Count);
            ClassicAssert.AreEqual(0, book.ResidentRoofStanding.Count);
            ClassicAssert.AreEqual(0, book.ResidentCreedStanding.Count);
        }

        [TestCase(BrinkKind.Roof)]
        [TestCase(BrinkKind.Creed)]
        public void BrinkWriteRefusesWhenResidentNormalizationCannotComplete(BrinkKind kind)
        {
            KingdomCityBook book = Broken();
            book.ResidentIds.Add(1);
            bool accepted = true;
            Assert.DoesNotThrow(() => accepted = book.TryWriteBrink(1, kind, true, 10, 20, "Mechanimists", 1));
            ClassicAssert.IsFalse(accepted);
            ClassicAssert.AreEqual("broken", book.SubsidenceModel);
            ClassicAssert.AreEqual(1, book.ResidentIds.Count);
            ClassicAssert.AreEqual(0, book.ResidentRoofStanding.Count);
            ClassicAssert.AreEqual(0, book.ResidentCreedStanding.Count);
        }

        [Test]
        public void ExactProjectionRemainsAvailableToNamedLoadValidationWhileLatchIsSet()
        {
            KingdomCityBook book = new KingdomCityBook { SettlementId = "taf:city:loading", SubsidenceReadFailed = true };
            KingdomCityState state;
            KingdomCityFault fault;
            ClassicAssert.IsTrue(book.TryReadExact(out state, out fault));
            ClassicAssert.IsNotNull(state);
            ClassicAssert.AreEqual(KingdomCityFault.None, fault);
            ClassicAssert.IsTrue(book.SubsidenceReadFailed);
        }
    }
}
#endif
