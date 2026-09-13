#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;
using Unit = ThousandAndFirst.Harness.KingdomCampHeartNativeCensus.Unit;

namespace ThousandAndFirst.Tests
{
	/// <summary>Real execution against plain custody records, not source pins: every predicate
	/// the camp heart native seam uses to prove a physical unit survived a real paid upgrade
	/// unchanged - same identity, same blueprint, same holder, same raw count - is engine-free
	/// and runs here directly.</summary>
	public class KingdomCampHeartNativeCensusTests
	{
		private static Unit Brush(string id, string holder = "inv:store", int raw = 1)
		{
			return new Unit(id, "r_KingdomBrush", holder, raw);
		}

		private static List<Unit> Three()
		{
			return new List<Unit> { Brush("u1"), Brush("u2"), Brush("u3") };
		}

		[Test]
		public void DescribeSumsRawCountPerBlueprintInOrdinalOrder()
		{
			var rows = new List<Unit> { Brush("u1", raw: 2), Brush("u2", raw: 3),
				new Unit("u3", "r_KingdomStone", "inv:store", 4) };
			Assert.That(KingdomCampHeartNativeCensus.Describe(rows),
				Is.EqualTo("r_KingdomBrush=5,r_KingdomStone=4"));
			// Ordinal, not case-folded: two blueprints differing only in case are two blueprints.
			var mixed = new List<Unit> { new Unit("a", "Brush", "inv:store", 2),
				new Unit("b", "brush", "inv:store", 1) };
			Assert.That(KingdomCampHeartNativeCensus.Describe(mixed),
				Is.EqualTo("Brush=2,brush=1"));
		}

		[Test]
		public void DescribeIsEmptyForNoUnitsAndNamesANullEntry()
		{
			Assert.That(KingdomCampHeartNativeCensus.Describe(null), Is.EqualTo("empty"));
			Assert.That(KingdomCampHeartNativeCensus.Describe(new List<Unit>()),
				Is.EqualTo("empty"));
			Assert.That(KingdomCampHeartNativeCensus.Describe(new List<Unit> { null }),
				Is.EqualTo("(null)=0"));
		}

		[Test]
		public void UnchangedCustodyHasNoFaults()
		{
			Assert.That(KingdomCampHeartNativeCensus.Faults(Three(), Three()), Is.Empty);
		}

		[Test]
		public void AMissingIdentityIsAFault()
		{
			var present = new List<Unit> { Brush("u1"), Brush("u3") };
			Assert.That(KingdomCampHeartNativeCensus.Faults(Three(), present),
				Is.EqualTo(new List<string> { "taf-camp-store-unit-missing:u2" }));
			Assert.That(KingdomCampHeartNativeCensus.Faults(Three(), null),
				Has.Count.EqualTo(3));
		}

		[Test]
		public void AReplacedIdentityWithTheSameBlueprintIsAFault()
		{
			// Same blueprint, same holder, same count, DIFFERENT object: not the same unit.
			var present = new List<Unit> { Brush("u1"), Brush("u9"), Brush("u3") };
			Assert.That(KingdomCampHeartNativeCensus.Faults(Three(), present),
				Is.EqualTo(new List<string> { "taf-camp-store-unit-missing:u2" }));
		}

		[Test]
		public void AChangedRawCountIsAFault()
		{
			var present = new List<Unit> { Brush("u1"), Brush("u2", raw: 4), Brush("u3") };
			Assert.That(KingdomCampHeartNativeCensus.Faults(Three(), present),
				Is.EqualTo(new List<string> { "taf-camp-store-unit-count-changed:u2:1->4" }));
		}

		[Test]
		public void AChangedHolderIsAFault()
		{
			var present = new List<Unit> { Brush("u1"), Brush("u2", holder: "cell:(4,5)"),
				Brush("u3") };
			Assert.That(KingdomCampHeartNativeCensus.Faults(Three(), present),
				Is.EqualTo(new List<string> {
					"taf-camp-store-unit-holder-changed:u2:inv:store->cell:(4,5)" }));
		}

		[Test]
		public void AChangedBlueprintIsAFault()
		{
			var present = new List<Unit> { Brush("u1"),
				new Unit("u2", "r_KingdomStone", "inv:store", 1), Brush("u3") };
			Assert.That(KingdomCampHeartNativeCensus.Faults(Three(), present),
				Is.EqualTo(new List<string> {
					"taf-camp-store-unit-blueprint-changed:u2:r_KingdomBrush->r_KingdomStone" }));
		}

		[Test]
		public void ADuplicateIdentityInTheReadingIsAFaultAndIsListedOnItsOwn()
		{
			var doubled = new List<Unit> { Brush("u1"), Brush("u1"), Brush("u2"), Brush("u3") };
			Assert.That(KingdomCampHeartNativeCensus.Duplicates(doubled),
				Is.EqualTo(new List<string> { "taf-camp-store-duplicate-identity:u1" }));
			Assert.That(KingdomCampHeartNativeCensus.Duplicates(Three()), Is.Empty);
			Assert.That(KingdomCampHeartNativeCensus.Faults(Three(), doubled),
				Is.EqualTo(new List<string> { "taf-camp-store-duplicate-identity:u1" }));
		}

		[Test]
		public void ANullWantedIdentityIsRefusedRatherThanSilentlyPassing()
		{
			Assert.That(KingdomCampHeartNativeCensus.Faults(new List<Unit> { null }, Three()),
				Is.EqualTo(new List<string> { "taf-camp-store-unit-missing:(null)" }));
		}

		[Test]
		public void SurvivingNamesEveryUnitTheBillRecordedAsSpentThatIsStillInTheStore()
		{
			var spent = new List<string> { "s1", "s2" };
			Assert.That(KingdomCampHeartNativeCensus.Surviving(spent,
					new List<Unit> { Brush("s2"), Brush("b1") }),
				Is.EqualTo(new List<string> { "taf-camp-store-unit-still-present:s2" }));
			Assert.That(KingdomCampHeartNativeCensus.Surviving(spent,
				new List<Unit> { Brush("b1") }), Is.Empty);
			Assert.That(KingdomCampHeartNativeCensus.Surviving(spent, null), Is.Empty);
			Assert.That(KingdomCampHeartNativeCensus.Surviving(null, Three()), Is.Empty);
		}

		private static List<KingdomCampHeartNativeCensus.Charge> Bill(params object[] pairs)
		{
			var rows = new List<KingdomCampHeartNativeCensus.Charge>();
			for (int i = 0; i + 1 < pairs.Length; i += 2)
				rows.Add(new KingdomCampHeartNativeCensus.Charge((string)pairs[i], (int)pairs[i + 1]));
			return rows;
		}

		[Test]
		public void AnExactlyPaidBillHasNoFaults()
		{
			Assert.That(KingdomCampHeartNativeCensus.BillFaults(Bill("Stone", 24, "Timber", 1),
				Bill("Stone", 24, "Timber", 1)), Is.Empty);
		}

		[Test]
		public void AShortBillIsAFault()
		{
			Assert.That(KingdomCampHeartNativeCensus.BillFaults(Bill("Stone", 24, "Timber", 1),
					Bill("Stone", 23, "Timber", 1)),
				Is.EqualTo(new List<string> { "taf-camp-bill-short:Stone:23<24" }));
			Assert.That(KingdomCampHeartNativeCensus.BillFaults(Bill("Stone", 24), Bill()),
				Is.EqualTo(new List<string> { "taf-camp-bill-short:Stone:0<24" }));
		}

		[Test]
		public void AnOverchargeIsAFaultAndNeverPasses()
		{
			Assert.That(KingdomCampHeartNativeCensus.BillFaults(Bill("Stone", 24, "Timber", 1),
					Bill("Stone", 25, "Timber", 1)),
				Is.EqualTo(new List<string> { "taf-camp-bill-over:Stone:25>24" }));
		}

		[Test]
		public void AChargedKindTheAuthoredBillNeverNamesIsAFault()
		{
			Assert.That(KingdomCampHeartNativeCensus.BillFaults(Bill("Stone", 24),
					Bill("Stone", 24, "Marble", 3)),
				Is.EqualTo(new List<string> { "taf-camp-bill-extra:Marble:3" }));
			// A zero charge of an unnamed kind is not an extra charge.
			Assert.That(KingdomCampHeartNativeCensus.BillFaults(Bill("Stone", 24),
				Bill("Stone", 24, "Marble", 0)), Is.Empty);
		}

		[Test]
		public void BillFaultsAreOrderedAndToleratesEmptySides()
		{
			Assert.That(KingdomCampHeartNativeCensus.BillFaults(Bill("Timber", 1, "Stone", 24),
					Bill("Timber", 2, "Stone", 20, "Marble", 1)),
				Is.EqualTo(new List<string> { "taf-camp-bill-extra:Marble:1",
					"taf-camp-bill-over:Timber:2>1", "taf-camp-bill-short:Stone:20<24" }));
			Assert.That(KingdomCampHeartNativeCensus.BillFaults(null, null), Is.Empty);
			Assert.That(KingdomCampHeartNativeCensus.BillFaults(Bill(), Bill()), Is.Empty);
		}

		[Test]
		public void DescribeChargesRendersTheBillKindByKind()
		{
			Assert.That(KingdomCampHeartNativeCensus.DescribeCharges(Bill("Timber", 1, "Stone", 24)),
				Is.EqualTo("Stone=24,Timber=1"));
			Assert.That(KingdomCampHeartNativeCensus.DescribeCharges(Bill()), Is.EqualTo("empty"));
			Assert.That(KingdomCampHeartNativeCensus.DescribeCharges(null), Is.EqualTo("empty"));
		}

		[Test]
		public void JoinIsBoundedAndSaysHowManyItDidNotShow()
		{
			var rows = new List<string> { "a", "b", "c", "d" };
			Assert.That(KingdomCampHeartNativeCensus.Join(rows, 2), Is.EqualTo("a,b,+2 more"));
			Assert.That(KingdomCampHeartNativeCensus.Join(rows, 4), Is.EqualTo("a,b,c,d"));
			Assert.That(KingdomCampHeartNativeCensus.Join(rows, 0), Is.EqualTo("+4 more"));
			Assert.That(KingdomCampHeartNativeCensus.Join(null, 4), Is.EqualTo("-"));
			Assert.That(KingdomCampHeartNativeCensus.Join(new List<string>(), 4), Is.EqualTo("-"));
		}

		[Test]
		public void AUnitDescribesItsWholeCustodyNotJustItsIdentity()
		{
			Assert.That(Brush("u1", "inv:store", 3).Describe(),
				Is.EqualTo("u1[r_KingdomBrush]@inv:storex3"));
			Assert.That(new Unit(null, null, null, 0).Describe(),
				Is.EqualTo("(null)[(null)]@(null)x0"));
		}
	}
}
#endif
