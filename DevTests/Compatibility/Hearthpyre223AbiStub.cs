// Compile-only ABI fixture. Never stage this file into the mod.
using System;
using System.Collections;
using System.Collections.Generic;
using Genkit;

namespace Hearthpyre
{
	using Hearthpyre.Realm;

	public static class RealmSystem
	{
		public static Dictionary<Guid, Settlement> Settlements = new();
		public static Dictionary<string, Settlement> SettlementsByCellID = new();
		public static Dictionary<Guid, Sector> Sectors = new();
		public static Dictionary<string, Sector> SectorsByZoneID = new();
		public static Dictionary<Guid, Home> Homes = new();
	}
}

namespace Hearthpyre.Realm
{
	public class Settlement
	{
		public Guid ID { get; private set; }
		public Dictionary<string, Sector> SectorsByZoneID { get; } = new();
	}

	public class Sector
	{
		public Guid ID { get; private set; }
		public Settlement Settlement { get; private set; }
		public string ZoneID { get; private set; }
		public List<Home> Homes { get; } = new();
	}

	public class Home : IEnumerable<Location2D>
	{
		private readonly List<Location2D> locations = new();

		public Guid ID { get; private set; }
		public Sector Sector { get; set; }
		public int Count => locations.Count;
		public Location2D Origin;

		public IEnumerator<Location2D> GetEnumerator() => locations.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => locations.GetEnumerator();
	}
}
