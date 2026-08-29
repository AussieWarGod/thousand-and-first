using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomArchivedSettlementCodec
	{
		private static string Shape(Type Root)
		{
			return Shape(Root, CurrentVersion);
		}

		private static string Shape(Type Root, bool Legacy)
		{
			return Shape(Root, Legacy ? LegacyVersion : CurrentVersion);
		}

		private static string Shape(Type Root, int SchemaVersion)
		{
			StringBuilder shape = new StringBuilder();
			HashSet<Type> visited = new HashSet<Type>();
			AppendShape(shape, Root, visited, SchemaVersion);
			if (StrictUtf8.GetByteCount(shape.ToString()) > MaxShapeBytes)
				throw new InvalidDataException("Archived settlement schema shape exceeds cap.");
			return shape.ToString();
		}

		private static void AppendShape(StringBuilder Shape, Type Type,
			HashSet<Type> Visited, int SchemaVersion)
		{
			if (Type.IsEnum)
			{
				if (SchemaVersion == LegacyVersion)
				{
					Shape.Append(Type.FullName).Append(';');
					return;
				}
				Type underlying = Enum.GetUnderlyingType(Type);
				Shape.Append("enum:").Append(Type.FullName).Append('<')
					.Append(underlying.FullName).Append(">{");
				string[] names = Enum.GetNames(Type);
				Array.Sort(names, StringComparer.Ordinal);
				bool unsigned = underlying == typeof(byte) || underlying == typeof(ushort) ||
					underlying == typeof(uint) || underlying == typeof(ulong);
				for (int i = 0; i < names.Length; i++)
				{
					object value = Enum.Parse(Type, names[i]);
					if (SchemaVersion < RaidVersion
						&& Type == typeof(KingdomLifecycleAction)
						&& Convert.ToInt64(value) > (long)KingdomLifecycleAction.PetitionExpire)
						continue;
					if (SchemaVersion >= RaidVersion && SchemaVersion < PhysicalHappeningVersion
						&& Type == typeof(KingdomLifecycleAction)
						&& Convert.ToInt64(value) > (long)KingdomLifecycleAction.RaidResolve)
						continue;
					if (SchemaVersion < PhysicalHappeningVersion
						&& Type == typeof(KingdomRaidIncidentState)
						&& Convert.ToInt64(value) > (long)KingdomRaidIncidentState.Queued)
						continue;
					if (SchemaVersion < FirstGuestVersion
						&& Type == typeof(KingdomGrowthArrivalCandidatePhase)
						&& (Convert.ToInt64(value) <
								(long)KingdomGrowthArrivalCandidatePhase.Prepared
							|| Convert.ToInt64(value) >
								(long)KingdomGrowthArrivalCandidatePhase.Quarantined))
						continue;
					if (SchemaVersion < FirstGuestVersion
						&& Type == typeof(KingdomGrowthArrivalDisposition)
						&& Convert.ToInt64(value) >
							(long)KingdomGrowthArrivalDisposition.SupportCap)
						continue;
					if (SchemaVersion >= FirstGuestVersion
						&& SchemaVersion < PhysicalFirstGuestVersion
						&& Type == typeof(KingdomGrowthArrivalCandidatePhase)
						&& Convert.ToInt64(value) >
							(long)KingdomGrowthArrivalCandidatePhase.Declined)
						continue;
					if (SchemaVersion >= FirstGuestVersion
						&& SchemaVersion < PhysicalFirstGuestVersion
						&& Type == typeof(KingdomGrowthArrivalDisposition)
						&& Convert.ToInt64(value) >
							(long)KingdomGrowthArrivalDisposition.Declined)
						continue;
					Shape.Append(names[i]).Append('=');
					if (unsigned)
						Shape.Append(Convert.ToUInt64(value).ToString(CultureInfo.InvariantCulture));
					else
						Shape.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture));
					Shape.Append(';');
				}
				Shape.Append("};");
				return;
			}
			if (Type.IsPrimitive || Type == typeof(string))
			{
				Shape.Append(Type.FullName).Append(';');
				return;
			}
			if (Type == typeof(byte[]))
			{
				Shape.Append("bytes;");
				return;
			}
			if (IsList(Type))
			{
				Shape.Append("list<"); AppendShape(Shape, Type.GetGenericArguments()[0],
					Visited, SchemaVersion);
				Shape.Append(">;"); return;
			}
			if (IsDictionary(Type))
			{
				Type[] arguments = Type.GetGenericArguments();
				Shape.Append("map<"); AppendShape(Shape, arguments[0], Visited, SchemaVersion);
				AppendShape(Shape, arguments[1], Visited, SchemaVersion); Shape.Append(">;"); return;
			}
			if (!Approved(Type)) throw new InvalidDataException(
				"Archived settlement schema includes unsupported type " + Type.FullName + ".");
			if (!Visited.Add(Type)) { Shape.Append("ref:").Append(Type.FullName).Append(';'); return; }
			Shape.Append("object:").Append(Type.FullName).Append('{');
			FieldInfo[] fields = Fields(Type, SchemaVersion);
			for (int i = 0; i < fields.Length; i++)
			{
				Shape.Append(fields[i].Name).Append(':');
				AppendShape(Shape, fields[i].FieldType, Visited, SchemaVersion);
			}
			Shape.Append("};");
		}

		private static string Bound(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "Archived settlement codec failed.";
			return Value.Length <= 512 ? Value : Value.Substring(0, 512);
		}
	}
}
