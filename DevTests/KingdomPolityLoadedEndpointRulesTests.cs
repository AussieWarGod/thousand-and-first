#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityLoadedEndpointRulesTests
	{
		private static string Id(char value)
		{
			return KingdomIdentityRules.SettlementPrefix + new string(value, 64);
		}

		[TestCase("seat-zone", 'a')]
		[TestCase("first-zone", 'b')]
		[TestCase("second-zone", 'c')]
		public void ResolvesSeatAndBothNonSeatEndpointsFromExactClaims(string zone,
			char expected)
		{
			Assert.That(KingdomPolityLoadedEndpointRules.TryResolve(zone, Id('a'),
				new List<string> { "seat-zone" }, new List<string> { Id('b'), Id('c') },
				new List<IList<string>>
				{
					new List<string> { "first-zone" },
					new List<string> { "second-zone" }
				}, out string settlement, out bool owned, out string failure),
				Is.True, failure);
			Assert.That(owned, Is.True);
			Assert.That(settlement, Is.EqualTo(Id(expected)));
		}

		[Test]
		public void UnownedLoadedZoneIsAnEmptyObservationNotRemoteWork()
		{
			Assert.That(KingdomPolityLoadedEndpointRules.TryResolve("outside", Id('a'),
				new List<string> { "seat-zone" }, new List<string> { Id('b') },
				new List<IList<string>> { new List<string> { "away-zone" } },
				out string settlement, out bool owned, out string failure), Is.True, failure);
			Assert.That(owned, Is.False);
			Assert.That(settlement, Is.Null);
		}

		[Test]
		public void AmbiguousOrMalformedTopologyFailsClosed()
		{
			Assert.That(KingdomPolityLoadedEndpointRules.TryResolve("shared", Id('a'),
				new List<string> { "shared" }, new List<string> { Id('b') },
				new List<IList<string>> { new List<string> { "shared" } },
				out _, out _, out string failure), Is.False);
			StringAssert.Contains("ambiguous", failure);
			Assert.That(KingdomPolityLoadedEndpointRules.TryResolve("zone", Id('a'),
				new List<string>(), new List<string> { Id('b') },
				new List<IList<string>>(), out _, out _, out failure), Is.False);
			Assert.That(KingdomPolityLoadedEndpointRules.TryResolve("zone", Id('a'),
				new List<string>(), new List<string> { Id('b') },
				new List<IList<string>> { null }, out _, out _, out failure), Is.False);
		}
	}
}
#endif
