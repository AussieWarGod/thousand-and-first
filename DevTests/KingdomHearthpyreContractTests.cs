#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Integrations.Hearthpyre223;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomHearthpyreContractTests
	{
		public sealed class Point { public int X; public int Y; }
		public sealed class Settlement
		{
			public Guid ID { get; } = Guid.NewGuid();
			public Dictionary<string, Sector> SectorsByZoneID { get; } = new Dictionary<string, Sector>();
		}
		public sealed class Sector
		{
			public Guid ID { get { Reads++; if (Throw) throw new InvalidOperationException("fault"); return Identity; } }
			public Guid Identity = Guid.NewGuid();
			public bool Throw;
			public int Reads;
			public Settlement Settlement { get; set; }
			public string ZoneID { get; set; }
			public List<Home> Homes { get; } = new List<Home>();
		}
		public sealed class Home : IEnumerable<Point>
		{
			public Guid ID { get; } = Guid.NewGuid();
			public Sector Sector { get; set; }
			public int Count => Cells.Count;
			public Point Origin;
			public List<Point> Cells = new List<Point>();
			public int Mutations;
			public void Add(Point Value) { Mutations++; Cells.Add(Value); }
			public IEnumerator<Point> GetEnumerator() => Cells.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
		public static class Realm
		{
			public static string PackageVersion;
			public static Dictionary<Guid, Settlement> Settlements = new Dictionary<Guid, Settlement>();
			public static Dictionary<string, Settlement> SettlementsByCellID = new Dictionary<string, Settlement>();
			public static Dictionary<Guid, Sector> Sectors = new Dictionary<Guid, Sector>();
			public static Dictionary<string, Sector> SectorsByZoneID = new Dictionary<string, Sector>();
			public static Dictionary<Guid, Home> Homes = new Dictionary<Guid, Home>();
		}
		public static class ReadOnlyRealm
		{
			public static IReadOnlyDictionary<Guid, Settlement> Settlements => Realm.Settlements;
			public static IReadOnlyDictionary<string, Settlement> SettlementsByCellID => Realm.SettlementsByCellID;
			public static IReadOnlyDictionary<Guid, Sector> Sectors => Realm.Sectors;
			public static IReadOnlyDictionary<string, Sector> SectorsByZoneID => Realm.SectorsByZoneID;
			public static IReadOnlyDictionary<Guid, Home> Homes => Realm.Homes;
		}
		public static class BrokenRealm
		{
			public static Dictionary<string, Settlement> Settlements = new Dictionary<string, Settlement>();
		}

		private static KingdomHearthpyreContract Bind(Type RealmType = null)
		{
			Assert.That(KingdomHearthpyreContract.TryBind(RealmType ?? typeof(Realm), typeof(Settlement),
				typeof(Sector), typeof(Home), typeof(Point), out var contract, out string failure), Is.True, failure);
			return contract;
		}

		[TestCase("2.2.3")]
		[TestCase("2.2.4")]
		[TestCase("2.2.5-preview")]
		[TestCase("99.0.0")]
		public void CompatibleMetadataIsAdmittedIndependentlyOfPackageVersion(string Version)
		{
			Realm.PackageVersion = Version;
			var contract = Bind();
			Assert.That(contract.Read("realm.homes"), Is.SameAs(Realm.Homes));
			Assert.That(contract.HomeType, Is.EqualTo(typeof(Home)));
		}

		[Test]
		public void ReadOnlyDictionaryApiDoesNotRequireMutableRegistryExposure()
		{
			Assert.That(Bind(typeof(ReadOnlyRealm)).Read("realm.sectors"), Is.SameAs(Realm.Sectors));
		}

		[Test]
		public void BindingOnlyInspectsMetadataAndGetterFailuresRemainLocal()
		{
			var sector = new Sector { Throw = true };
			var contract = Bind();
			Assert.That(sector.Reads, Is.Zero);
			Assert.That(contract.TryRead("sector.id", sector, out var value, out string failure), Is.False);
			Assert.That(value, Is.Null);
			Assert.That(failure, Does.Contain("realm read refused for sector.id"));
			sector.Throw = false;
			Assert.That(contract.Read("sector.id", sector), Is.EqualTo(sector.Identity));
		}

		[Test]
		public void ReadsKeepOriginalObjectsAndNeverExposeMutationMethods()
		{
			var point = new Point { X = 7, Y = 9 };
			var home = new Home { Origin = point, Sector = new Sector() };
			home.Cells.Add(point);
			var contract = Bind();
			Assert.That(contract.Read("home.origin", home), Is.SameAs(point));
			Assert.That(contract.Read("home.sector", home), Is.SameAs(home.Sector));
			Assert.That(contract.Read("home.count", home), Is.EqualTo(1));
			Assert.That(contract.Read("point.x", point), Is.EqualTo(7));
			Assert.That(contract.TryRead("Add", home, out var value, out _), Is.False);
			Assert.That(value, Is.Null);
			Assert.That(home.Mutations, Is.Zero);
			Assert.That(home.Cells, Is.EqualTo(new[] { point }));
		}

		[Test]
		public void MissingChangedAliasedAndForeignAssemblyTypesRefuseWithoutPartialContract()
		{
			foreach (Type realm in new[] { null, typeof(BrokenRealm), typeof(Sector), typeof(object) })
			{
				Assert.That(KingdomHearthpyreContract.TryBind(realm, typeof(Settlement), typeof(Sector), typeof(Home),
					typeof(Point), out var contract, out string failure), Is.False);
				Assert.That(contract, Is.Null);
				Assert.That(failure, Is.Not.Null.And.Not.Empty);
			}
		}

		[Test]
		public void WrongReceiverAndUnknownCapabilityCannotInvokeAnotherRead()
		{
			var contract = Bind();
			foreach (var request in new[] { Tuple.Create("home.id", (object)new Sector()),
				Tuple.Create("home.id", (object)null), Tuple.Create("realm.homes", (object)new Home()),
				Tuple.Create((string)null, (object)new Home()), Tuple.Create("home.Add", (object)new Home()) })
			{
				Assert.That(contract.TryRead(request.Item1, request.Item2, out var value, out string failure), Is.False);
				Assert.That(value, Is.Null);
				Assert.That(failure, Is.Not.Null.And.Not.Empty);
			}
		}
	}
}
#endif
