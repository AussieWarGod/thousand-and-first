using System;
using System.Collections.Generic;
using System.Reflection;

namespace ThousandAndFirst.Integrations.Hearthpyre223
{
	/// <summary>Read-only capability binding for the public realm/home contract. Package version
	/// is not an admission condition. Binding inspects metadata only; observation invokes only
	/// the explicitly named public getters or reads fields, never constructors or write methods.</summary>
	internal sealed class KingdomHearthpyreContract
	{
		private readonly Dictionary<string, MemberInfo> Members = new Dictionary<string, MemberInfo>(StringComparer.Ordinal);
		internal Type SettlementType { get; private set; }
		internal Type SectorType { get; private set; }
		internal Type HomeType { get; private set; }
		internal Type PointType { get; private set; }

		internal static bool TryBind(Type Realm, Type Settlement, Type Sector, Type Home, Type Point,
			out KingdomHearthpyreContract Contract, out string Failure)
		{
			Contract = null; Failure = null;
			try
			{
				if (Realm == null || Settlement == null || Sector == null || Home == null || Point == null
					|| !Realm.IsClass || !Settlement.IsClass || !Sector.IsClass || !Home.IsClass
					|| Realm.ContainsGenericParameters || Settlement.ContainsGenericParameters
					|| Sector.ContainsGenericParameters || Home.ContainsGenericParameters
					|| Realm.Assembly != Settlement.Assembly || Realm.Assembly != Sector.Assembly || Realm.Assembly != Home.Assembly
					|| Realm == Settlement || Realm == Sector || Realm == Home || Settlement == Sector
					|| Settlement == Home || Sector == Home)
					return Fail("realm capability types are absent, aliased or from different assemblies", out Failure);
				var result = new KingdomHearthpyreContract {
					SettlementType = Settlement, SectorType = Sector, HomeType = Home, PointType = Point
				};
				result.Map("realm.settlements", Realm, "Settlements", true, typeof(Guid), Settlement);
				result.Map("realm.cells", Realm, "SettlementsByCellID", true, typeof(string), Settlement);
				result.Map("realm.sectors", Realm, "Sectors", true, typeof(Guid), Sector);
				result.Map("realm.zones", Realm, "SectorsByZoneID", true, typeof(string), Sector);
				result.Map("realm.homes", Realm, "Homes", true, typeof(Guid), Home);
				result.Member("settlement.id", Settlement, "ID", false, type => type == typeof(Guid));
				result.Map("settlement.zones", Settlement, "SectorsByZoneID", false, typeof(string), Sector);
				result.Member("sector.id", Sector, "ID", false, type => type == typeof(Guid));
				result.Member("sector.settlement", Sector, "Settlement", false, type => type == Settlement);
				result.Member("sector.zone", Sector, "ZoneID", false, type => type == typeof(string));
				result.Member("sector.homes", Sector, "Homes", false, type => ListOf(type, Home));
				result.Member("home.id", Home, "ID", false, type => type == typeof(Guid));
				result.Member("home.sector", Home, "Sector", false, type => type == Sector);
				result.Member("home.count", Home, "Count", false, type => type == typeof(int));
				result.Member("home.origin", Home, "Origin", false, type => type == Point);
				result.Member("point.x", Point, "X", false, type => type == typeof(int));
				result.Member("point.y", Point, "Y", false, type => type == typeof(int));
				if (!typeof(IEnumerable<>).MakeGenericType(Point).IsAssignableFrom(Home))
					return Fail("Home does not expose the required typed cell enumeration", out Failure);
				Contract = result; return true;
			}
			catch (Exception error)
			{
				return Fail("realm capability binding refused: " + Bound(error.Message), out Failure);
			}
		}

		internal bool TryRead(string Key, object Target, out object Value, out string Failure)
		{
			Value = null; Failure = null;
			if (Key == null || !Members.TryGetValue(Key, out MemberInfo member))
				return Fail("unknown realm read capability", out Failure);
			try
			{
				bool isStatic = member is FieldInfo field ? field.IsStatic : ((PropertyInfo)member).GetGetMethod().IsStatic;
				if (isStatic ? Target != null : Target == null || !member.DeclaringType.IsInstanceOfType(Target))
					return Fail("realm read target does not match " + Key, out Failure);
				Value = member is FieldInfo info ? info.GetValue(Target) : ((PropertyInfo)member).GetValue(Target, null);
				return true;
			}
			catch (Exception error)
			{
				Value = null;
				return Fail("realm read refused for " + Key + ": " + error.GetType().Name, out Failure);
			}
		}

		internal object Read(string Key, object Target = null)
		{
			if (TryRead(Key, Target, out object value, out string failure)) return value;
			throw new InvalidOperationException(failure);
		}

		private void Map(string Key, Type Owner, string Name, bool Static, Type Keys, Type Values)
		{
			Member(Key, Owner, Name, Static, type =>
				typeof(IDictionary<,>).MakeGenericType(Keys, Values).IsAssignableFrom(type)
				|| typeof(IReadOnlyDictionary<,>).MakeGenericType(Keys, Values).IsAssignableFrom(type));
		}

		private static bool ListOf(Type Candidate, Type Values)
		{
			return typeof(IList<>).MakeGenericType(Values).IsAssignableFrom(Candidate)
				|| typeof(IReadOnlyList<>).MakeGenericType(Values).IsAssignableFrom(Candidate);
		}

		private void Member(string Key, Type Owner, string Name, bool Static, Func<Type, bool> Valid)
		{
			MemberInfo chosen = null;
			BindingFlags flags = BindingFlags.Public | BindingFlags.FlattenHierarchy
				| (Static ? BindingFlags.Static : BindingFlags.Instance);
			foreach (MemberInfo member in Owner.GetMember(Name, MemberTypes.Field | MemberTypes.Property, flags))
			{
				Type valueType;
				if (member is FieldInfo field) valueType = field.FieldType;
				else if (member is PropertyInfo property && property.GetIndexParameters().Length == 0
					&& property.GetGetMethod() != null && property.GetGetMethod().IsStatic == Static)
					valueType = property.PropertyType;
				else continue;
				if (chosen != null || !Valid(valueType))
					throw new InvalidOperationException("ambiguous or changed public member " + Key);
				chosen = member;
			}
			if (chosen == null) throw new InvalidOperationException("missing public member " + Key);
			Members.Add(Key, chosen);
		}

		private static string Bound(string Text)
		{
			string value = Text ?? "unknown capability failure";
			return value.Length <= 192 ? value : value.Substring(0, 192);
		}
		private static bool Fail(string Message, out string Failure) { Failure = Message; return false; }
	}
}
