using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Genkit;
using XRL;

namespace ThousandAndFirst.Integrations.Hearthpyre223
{
	/// <summary>The persisted adapter contract keeps its original identity across compatible
	/// package upgrades. Foreign type/member resolution is deferred until an enabled mod is read.</summary>
	[HasModSensitiveStaticCache]
	internal static class RealmSystem
	{
		internal const string ContractVersion = "2.2.3";
		[ModSensitiveStaticCache]
		private static KingdomHearthpyreRealmBinding Cached;
		[ModSensitiveStaticCache]
		private static ModInfo CachedMod;
		[ModSensitiveStaticCache]
		private static Type CachedRealm;

		internal static bool IsAvailable => EnabledMod() != null;
		internal static string PackageVersion => EnabledMod()?.Manifest.Version.ToString() ?? "absent";
		internal static IReadOnlyDictionary<Guid, Settlement> Settlements => Current.SettlementMap("realm.settlements");
		internal static IReadOnlyDictionary<string, Settlement> SettlementsByCellID => Current.SettlementCellMap();
		internal static IReadOnlyDictionary<Guid, Sector> Sectors => Current.SectorMap();
		internal static IReadOnlyDictionary<string, Sector> SectorsByZoneID => Current.SectorZoneMap();
		internal static IReadOnlyDictionary<Guid, Home> Homes => Current.HomeMap();

		private static ModInfo EnabledMod()
		{
			return ModManager.ModMap.TryGetValue("Hearthpyre", out ModInfo mod) && mod.IsEnabled ? mod : null;
		}

		private static KingdomHearthpyreRealmBinding Current
		{
			get
			{
				ModInfo mod = EnabledMod();
				if (mod == null) throw new InvalidOperationException("Hearthpyre is absent or disabled");
				Type realm = ModManager.ResolveType("Hearthpyre.RealmSystem");
				if (Cached != null && ReferenceEquals(CachedMod, mod) && CachedRealm == realm) return Cached;
				Cached = null; CachedRealm = null; CachedMod = null;
				if (realm == null || !ReferenceEquals(ModManager.GetMod(realm.Assembly), mod))
					throw new InvalidOperationException("enabled Hearthpyre does not own its realm capability type");
				if (!KingdomHearthpyreContract.TryBind(realm, ModManager.ResolveType("Hearthpyre.Realm.Settlement"),
					ModManager.ResolveType("Hearthpyre.Realm.Sector"), ModManager.ResolveType("Hearthpyre.Realm.Home"),
					typeof(Location2D), out var contract, out string failure))
					throw new InvalidOperationException("Hearthpyre " + PackageVersion + ": " + failure);
				Cached = new KingdomHearthpyreRealmBinding(contract); CachedRealm = realm; CachedMod = mod;
				return Cached;
			}
		}
	}

	internal sealed class KingdomHearthpyreRealmBinding
	{
		internal readonly KingdomHearthpyreContract Contract;
		private readonly ConditionalWeakTable<object, Settlement> Settlements = new ConditionalWeakTable<object, Settlement>();
		private readonly ConditionalWeakTable<object, Sector> Sectors = new ConditionalWeakTable<object, Sector>();
		private readonly ConditionalWeakTable<object, Home> Homes = new ConditionalWeakTable<object, Home>();
		private readonly ConditionalWeakTable<object, object> Collections = new ConditionalWeakTable<object, object>();
		internal KingdomHearthpyreRealmBinding(KingdomHearthpyreContract Contract) { this.Contract = Contract; }
		internal Settlement Settlement(object Body)
		{
			if (Body == null) return null;
			RequireType(Body, Contract.SettlementType);
			return Settlements.GetValue(Body, value => new Settlement(this, value));
		}
		internal Sector Sector(object Body)
		{
			if (Body == null) return null;
			RequireType(Body, Contract.SectorType);
			return Sectors.GetValue(Body, value => new Sector(this, value));
		}
		internal Home Home(object Body)
		{
			if (Body == null) return null;
			RequireType(Body, Contract.HomeType);
			return Homes.GetValue(Body, value => new Home(this, value));
		}
		internal IReadOnlyDictionary<TKey, TValue> Map<TKey, TValue>(object Body, Type Foreign, Func<object, TValue> Wrap)
		{
			return Body == null ? null : (IReadOnlyDictionary<TKey, TValue>)Collections.GetValue(Body,
				value => KingdomHearthpyreCollectionViews.Map<TKey, TValue>(value, Foreign, Wrap));
		}
		internal IReadOnlyList<Home> HomeList(object Body)
		{
			return Body == null ? null : (IReadOnlyList<Home>)Collections.GetValue(Body,
				value => KingdomHearthpyreCollectionViews.List(value, Contract.HomeType, Home));
		}
		internal IReadOnlyDictionary<Guid, Settlement> SettlementMap(string Key)
			=> Map<Guid, Settlement>(Contract.Read(Key), Contract.SettlementType, Settlement);
		internal IReadOnlyDictionary<string, Settlement> SettlementCellMap()
			=> Map<string, Settlement>(Contract.Read("realm.cells"), Contract.SettlementType, Settlement);
		internal IReadOnlyDictionary<Guid, Sector> SectorMap()
			=> Map<Guid, Sector>(Contract.Read("realm.sectors"), Contract.SectorType, Sector);
		internal IReadOnlyDictionary<string, Sector> SectorZoneMap()
			=> Map<string, Sector>(Contract.Read("realm.zones"), Contract.SectorType, Sector);
		internal IReadOnlyDictionary<Guid, Home> HomeMap()
			=> Map<Guid, Home>(Contract.Read("realm.homes"), Contract.HomeType, Home);
		private static void RequireType(object Body, Type Type)
		{
			if (!Type.IsInstanceOfType(Body)) throw new InvalidOperationException("realm registry contains a foreign row type");
		}
	}
}
