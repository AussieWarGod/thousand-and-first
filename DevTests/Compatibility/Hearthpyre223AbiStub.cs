// Compile-only ABI fixture. Never stage this file into the mod.
using System;
using System.Collections.Generic;

namespace Hearthpyre
{
	using Hearthpyre.Realm;

	public static class RealmSystem
	{
		public static Dictionary<Guid, Settlement> Settlements = new();
		public static Dictionary<string, Settlement> SettlementsByCellID = new();
		public static Dictionary<Guid, Sector> Sectors = new();
		public static Dictionary<string, Sector> SectorsByZoneID = new();
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
	}
}
