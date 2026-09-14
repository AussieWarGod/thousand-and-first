using System;
using System.Collections;
using System.Collections.Generic;
using Genkit;

namespace ThousandAndFirst.Integrations.Hearthpyre223
{
	internal sealed class Settlement
	{
		private readonly KingdomHearthpyreRealmBinding Binding;
		private readonly object Body;
		internal Settlement(KingdomHearthpyreRealmBinding Binding, object Body) { this.Binding = Binding; this.Body = Body; }
		internal Guid ID => (Guid)Binding.Contract.Read("settlement.id", Body);
		internal IReadOnlyDictionary<string, Sector> SectorsByZoneID => Binding.Map<string, Sector>(
			Binding.Contract.Read("settlement.zones", Body), Binding.Contract.SectorType, Binding.Sector);
	}

	internal sealed class Sector
	{
		private readonly KingdomHearthpyreRealmBinding Binding;
		private readonly object Body;
		internal Sector(KingdomHearthpyreRealmBinding Binding, object Body) { this.Binding = Binding; this.Body = Body; }
		internal Guid ID => (Guid)Binding.Contract.Read("sector.id", Body);
		internal string ZoneID => (string)Binding.Contract.Read("sector.zone", Body);
		internal Settlement Settlement => Binding.Settlement(Binding.Contract.Read("sector.settlement", Body));
		internal IReadOnlyList<Home> Homes => Binding.HomeList(Binding.Contract.Read("sector.homes", Body));
	}

	internal sealed class Home : IEnumerable<Location2D>
	{
		private readonly KingdomHearthpyreRealmBinding Binding;
		private readonly object Body;
		internal Home(KingdomHearthpyreRealmBinding Binding, object Body) { this.Binding = Binding; this.Body = Body; }
		internal Guid ID => (Guid)Binding.Contract.Read("home.id", Body);
		internal Sector Sector => Binding.Sector(Binding.Contract.Read("home.sector", Body));
		internal int Count => (int)Binding.Contract.Read("home.count", Body);
		internal Location2D Origin => (Location2D)Binding.Contract.Read("home.origin", Body);
		public IEnumerator<Location2D> GetEnumerator() => ((IEnumerable<Location2D>)Body).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
