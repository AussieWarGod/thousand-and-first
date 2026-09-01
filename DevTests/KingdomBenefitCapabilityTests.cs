using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomBenefitCapabilityTests
	{
		[Test]
		public void CatalogueAcceptanceNeverCreatesAFunctionalCapability()
		{
			KingdomBenefitReading reading = Reading();
			reading.Designation.AcceptedTags.Add(KingdomBenefitCapabilities.Education);
			Assert.That(KingdomBenefitCapabilities.Accepts(reading,
				KingdomBenefitCapabilities.Education), Is.True);
			Assert.That(KingdomBenefitCapabilities.Has(reading,
				KingdomBenefitCapabilities.Education), Is.False);
		}

		[Test]
		public void OnlyCreditedLiveProviderTagsBecomeCapabilities()
		{
			KingdomBenefitReading reading = Reading();
			reading.Provides.Add(" TAF:EDUCATION ");
			Assert.That(KingdomBenefitCapabilities.Has(reading,
				KingdomBenefitCapabilities.Education), Is.True);
			Assert.That(KingdomBenefitCapabilities.Has(reading,
				KingdomBenefitCapabilities.Inquiry), Is.False);
		}

		[Test]
		public void CountIsPerDesignationAndFurnitureSpamCannotMultiplyIt()
		{
			KingdomBenefitReading first = Reading();
			first.Provides.Add(KingdomBenefitCapabilities.Cooking);
			first.Provides.Add(KingdomBenefitCapabilities.Cooking);
			KingdomBenefitReading second = Reading();
			second.Provides.Add(KingdomBenefitCapabilities.Cooking);
			Assert.That(KingdomBenefitCapabilities.Count(
				new List<KingdomBenefitReading> { first, second },
				KingdomBenefitCapabilities.Cooking), Is.EqualTo(2));
		}

		[Test]
		public void ExtensionCapabilitiesRemainOpen()
		{
			KingdomBenefitReading reading = Reading();
			reading.Provides.Add("anothermod:observatory");
			Assert.That(KingdomBenefitCapabilities.Has(reading,
				"ANOTHERMOD:OBSERVATORY"), Is.True);
		}

		[Test]
		public void PhysicalMarketIsARegisteredSemanticCapability()
		{
			CollectionAssert.Contains(KingdomBenefitCapabilities.BuiltIn,
				KingdomBenefitCapabilities.Market);
			Assert.That(KingdomBenefitCapabilities.Market, Is.EqualTo("taf:market"));
		}

		private static KingdomBenefitReading Reading()
		{
			return new KingdomBenefitReading {
				Designation = new KingdomBenefitDesignation()
			};
		}
	}
}
