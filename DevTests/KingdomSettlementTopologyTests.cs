#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomSettlementTopologyTests
	{
		private static string Id(char Value)
		{
			return KingdomIdentityRules.SettlementPrefix + new string(Value, 64);
		}

		private static KingdomSettlement Row(char IdValue, string Zone)
		{
			KingdomSettlement row = new KingdomSettlement { SettlementName = IdValue.ToString() };
			row.City.SettlementId = Id(IdValue);
			row.ClaimedZones.Add(Zone);
			return row;
		}

		[Test]
		public void CollectionSortsFindsAndCapsWithoutReplacingExactReferences()
		{
			KingdomSettlementTopology topology = new KingdomSettlementTopology();
			KingdomSettlement c = Row('c', "zone-c");
			KingdomSettlement b = Row('b', "zone-b");
			Assert.That(topology.TryAdd(c, out string failure), Is.True, failure);
			Assert.That(topology.TryAdd(b, out failure), Is.True, failure);
			Assert.That(topology.Count, Is.EqualTo(2));
			Assert.That(topology.Get(0), Is.SameAs(b));
			Assert.That(topology.Get(1), Is.SameAs(c));
			Assert.That(topology.FindById(Id('c')), Is.SameAs(c));
			Assert.That(topology.FindByZone("zone-b"), Is.SameAs(b));
			Assert.That(topology.TryAdd(Row('d', "zone-d"), out failure), Is.False);
		}

		[Test]
		public void MutationUsesReferenceCasAndPreservesDeterministicOrder()
		{
			KingdomSettlementTopology topology = new KingdomSettlementTopology();
			KingdomSettlement b = Row('b', "zone-b");
			KingdomSettlement c = Row('c', "zone-c");
			Assert.That(topology.TryAdd(b, out string failure), Is.True, failure);
			Assert.That(topology.TryAdd(c, out failure), Is.True, failure);
			Assert.That(topology.TryRemoveReference(Row('b', "zone-b"), out failure), Is.False);
			KingdomSettlement d = Row('d', "zone-d");
			Assert.That(topology.TryReplaceReference(b, d, out failure), Is.True, failure);
			Assert.That(topology.Get(0), Is.SameAs(c));
			Assert.That(topology.Get(1), Is.SameAs(d));
			Assert.That(topology.TryRemoveReference(c, out failure), Is.True, failure);
			Assert.That(topology.Get(0), Is.SameAs(d));
		}

		[Test]
		public void DuplicateIdentityAndInvalidRowsFailClosed()
		{
			KingdomSettlementTopology topology = new KingdomSettlementTopology();
			Assert.That(topology.TryAdd(Row('b', "zone-b"), out string failure), Is.True,
				failure);
			Assert.That(topology.TryAdd(Row('b', "other-zone"), out failure), Is.False);
			Assert.That(topology.TryAdd(null, out failure), Is.False);
			KingdomSettlement invalid = new KingdomSettlement();
			Assert.That(topology.TryAdd(invalid, out failure), Is.False);
		}

		[Test]
		public void NameLookupSupportsMultipleCitiesAndRefusesAmbiguity()
		{
			KingdomSettlementTopology topology = new KingdomSettlementTopology();
			KingdomSettlement b = Row('b', "zone-b");
			KingdomSettlement c = Row('c', "zone-c");
			b.SettlementName = "Basin";
			c.SettlementName = "Cairn";
			Assert.That(topology.TryAdd(c, out string failure), Is.True, failure);
			Assert.That(topology.TryAdd(b, out failure), Is.True, failure);
			Assert.That(topology.TryFindByName("Cairn", out KingdomSettlement found),
				Is.True);
			Assert.That(found, Is.SameAs(c));
			Assert.That(topology.TryFindByName("Missing", out found), Is.False);
			Assert.That(found, Is.Null);

			b.SettlementName = c.SettlementName;
			Assert.That(topology.TryFindByName("Cairn", out found), Is.False);
			Assert.That(found, Is.Null);
		}
	}
}
#endif
