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
		private static object ReadValue(BinaryReader Reader, Type Type, int Depth,
			Budget Budget)
		{
			return ReadValue(Reader, Type, Depth, Budget, CurrentVersion);
		}

		private static object ReadValue(BinaryReader Reader, Type Type, int Depth,
			Budget Budget, int SchemaVersion)
		{
			if (Depth > MaxDepth) throw new InvalidDataException("Archived settlement graph is too deep.");
			if (Type == typeof(string)) return ReadString(Reader, MaxStringBytes, Required: false);
			if (Type.IsEnum)
			{
				long raw = Reader.ReadInt64();
				if (!EnumRawFits(Type, raw))
					throw new InvalidDataException(
						"Archived settlement enum encoding is noncanonical.");
				object value = Enum.ToObject(Type, raw);
				if (!Enum.IsDefined(Type, value))
					throw new InvalidDataException("Archived settlement enum value is unknown.");
				if (SchemaVersion == LegacyVersion
					&& Type == typeof(KingdomLifecycleResourceKind) &&
					raw > (long)KingdomLifecycleResourceKind.Raid)
					throw new InvalidDataException(
						"Archived settlement v1 resource kind is unknown.");
				if (SchemaVersion < RaidVersion
					&& Type == typeof(KingdomLifecycleAction)
					&& raw > (long)KingdomLifecycleAction.PetitionExpire)
					throw new InvalidDataException(
						"Archived settlement historical lifecycle action is unknown.");
				if (SchemaVersion >= RaidVersion && SchemaVersion < PhysicalHappeningVersion
					&& Type == typeof(KingdomLifecycleAction)
					&& raw > (long)KingdomLifecycleAction.RaidResolve)
					throw new InvalidDataException(
						"Archived settlement historical raid action is unknown.");
				if (SchemaVersion < PhysicalHappeningVersion
					&& Type == typeof(KingdomRaidIncidentState)
					&& raw > (long)KingdomRaidIncidentState.Queued)
					throw new InvalidDataException(
						"Archived settlement historical raid state is unknown.");
				if (SchemaVersion < FirstGuestVersion
					&& Type == typeof(KingdomGrowthArrivalCandidatePhase)
					&& (raw < (long)KingdomGrowthArrivalCandidatePhase.None
						|| raw > (long)KingdomGrowthArrivalCandidatePhase.Quarantined))
					throw new InvalidDataException(
						"Archived settlement historical arrival phase is unknown.");
				if (SchemaVersion < FirstGuestVersion
					&& Type == typeof(KingdomGrowthArrivalDisposition)
					&& raw > (long)KingdomGrowthArrivalDisposition.SupportCap)
					throw new InvalidDataException(
						"Archived settlement historical arrival disposition is unknown.");
				if (SchemaVersion >= FirstGuestVersion
					&& SchemaVersion < PhysicalFirstGuestVersion
					&& Type == typeof(KingdomGrowthArrivalCandidatePhase)
					&& raw > (long)KingdomGrowthArrivalCandidatePhase.Declined)
					throw new InvalidDataException(
						"Archived settlement historical physical guest phase is unknown.");
				if (SchemaVersion >= FirstGuestVersion
					&& SchemaVersion < PhysicalFirstGuestVersion
					&& Type == typeof(KingdomGrowthArrivalDisposition)
					&& raw > (long)KingdomGrowthArrivalDisposition.Declined)
					throw new InvalidDataException(
						"Archived settlement historical physical guest disposition is unknown.");
				return value;
			}
			if (Type == typeof(bool))
			{
				byte raw = Reader.ReadByte();
				if (raw > 1) throw new InvalidDataException("Archived settlement bool is noncanonical.");
				return raw == 1;
			}
			if (Type == typeof(byte)) return Reader.ReadByte();
			if (Type == typeof(short)) return Reader.ReadInt16();
			if (Type == typeof(int)) return Reader.ReadInt32();
			if (Type == typeof(long)) return Reader.ReadInt64();
			if (Type == typeof(ushort)) return Reader.ReadUInt16();
			if (Type == typeof(uint)) return Reader.ReadUInt32();
			if (Type == typeof(ulong)) return Reader.ReadUInt64();
			if (Type == typeof(byte[]))
			{
				int count = ReadCount(Reader, MaxByteArrayBytes, AllowNull: true);
				if (count == -1) return null;
				byte[] bytes = Reader.ReadBytes(count);
				if (bytes.Length != count)
					throw new EndOfStreamException("Archived settlement byte array is truncated.");
				return bytes;
			}
			if (IsList(Type))
			{
				int count = ReadCount(Reader, MaxCollectionCount, AllowNull: true);
				if (count == -1) return null;
				IList list = (IList)Activator.CreateInstance(Type, count);
				Type itemType = Type.GetGenericArguments()[0];
				for (int i = 0; i < count; i++)
					list.Add(ReadValue(Reader, itemType, Depth + 1, Budget, SchemaVersion));
				return list;
			}
			if (IsDictionary(Type))
			{
				int count = ReadCount(Reader, MaxCollectionCount, AllowNull: true);
				if (count == -1) return null;
				Type[] arguments = Type.GetGenericArguments();
				if (arguments[0] != typeof(string))
					throw new InvalidDataException("Archived settlement dictionary key type is unsupported.");
				IDictionary dictionary = (IDictionary)Activator.CreateInstance(Type, count);
				string previous = null;
				for (int i = 0; i < count; i++)
				{
					string key = ReadString(Reader, MaxStringBytes, Required: true);
					if (previous != null && string.CompareOrdinal(previous, key) >= 0)
						throw new InvalidDataException("Archived settlement dictionary order is noncanonical.");
					dictionary.Add(key, ReadValue(Reader, arguments[1], Depth + 1, Budget,
						SchemaVersion));
					previous = key;
				}
				return dictionary;
			}
			if (!Approved(Type))
				throw new InvalidDataException("Archived settlement field type is unsupported: " + Type.FullName);
			byte present = Reader.ReadByte();
			if (present > 1) throw new InvalidDataException("Archived settlement object flag is noncanonical.");
			if (present == 0) return null;
			if (++Budget.Objects > MaxObjects)
				throw new InvalidDataException("Archived settlement object count exceeds cap.");
			object result = Activator.CreateInstance(Type);
			FieldInfo[] fields = Fields(Type, SchemaVersion);
			for (int i = 0; i < fields.Length; i++)
			{
				if (Type == typeof(Simulation.City.KingdomCityBook)
					&& (string.Equals(fields[i].Name, "ExtensionModel", StringComparison.Ordinal)
						|| string.Equals(fields[i].Name, "HappeningModel", StringComparison.Ordinal)
						|| string.Equals(fields[i].Name, "ExtensionHappeningCursors", StringComparison.Ordinal)))
				{
					int maximum = string.Equals(fields[i].Name, "ExtensionModel",
						StringComparison.Ordinal)
						? Simulation.City.KingdomCityBook.MaxExtensionModelChars
						: string.Equals(fields[i].Name, "HappeningModel", StringComparison.Ordinal)
							? Simulation.City.KingdomCityBook.MaxHappeningModelChars
							: Simulation.City.KingdomCityBook.MaxExtensionHappeningCursorChars;
					fields[i].SetValue(result, ReadString(Reader, maximum, Required: false));
					continue;
				}
				fields[i].SetValue(result, ReadValue(Reader, fields[i].FieldType,
					Depth + 1, Budget, SchemaVersion));
			}
			if (Type == typeof(KingdomGrowthFirstGuestOpportunity)
				&& !HistoricalPhysicalFirstGuestOpportunity(
					(KingdomGrowthFirstGuestOpportunity)result, SchemaVersion))
				throw new InvalidDataException(
					"Archived settlement historical physical first-guest evidence is unknown.");
			if (SchemaVersion < PhysicalFirstGuestVersion
				&& Type == typeof(KingdomGrowthFirstGuestTerminalReceipt)
				&& ((KingdomGrowthFirstGuestTerminalReceipt)result).Version !=
					KingdomGrowthFirstGuestTerminalReceipt.LegacyVersion)
				throw new InvalidDataException(
					"Archived settlement historical first-guest terminal version is unknown.");
			if (Type == typeof(Simulation.City.KingdomJobRegistry)
				&& SchemaVersion < ExpeditionResultVersion)
				((Simulation.City.KingdomJobRegistry)result).Normalize();
			if (SchemaVersion >= ExactLogisticsVersion
				&& Type == typeof(Simulation.City.KingdomJobRegistry)
				&& !ValidDeliveryDomain(
					(Simulation.City.KingdomJobRegistry)result, SchemaVersion))
				throw new InvalidDataException(
					"Archived settlement delivery enum domain is invalid for its version.");
			if (Type == typeof(Simulation.City.KingdomJobRegistry)
				&& !ValidExpeditionResultDomain(
					(Simulation.City.KingdomJobRegistry)result))
				throw new InvalidDataException(
					"Archived settlement expedition-result domain is invalid for its version.");
			if (SchemaVersion >= CivicAuthorityVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& !ValidCivicAuthority((Simulation.City.KingdomCityBook)result))
				throw new InvalidDataException(
					"Archived settlement civic authority is invalid for its version.");
			return result;
		}

		private static int ReadCount(BinaryReader Reader, int Maximum, bool AllowNull)
		{
			int count = Reader.ReadInt32();
			if ((AllowNull && count == -1) || (count >= 0 && count <= Maximum)) return count;
			throw new InvalidDataException("Archived settlement collection count exceeds cap.");
		}

		private static bool EnumRawFits(Type EnumType, long Raw)
		{
			Type underlying = Enum.GetUnderlyingType(EnumType);
			if (underlying == typeof(byte)) return Raw >= byte.MinValue && Raw <= byte.MaxValue;
			if (underlying == typeof(sbyte)) return Raw >= sbyte.MinValue && Raw <= sbyte.MaxValue;
			if (underlying == typeof(short)) return Raw >= short.MinValue && Raw <= short.MaxValue;
			if (underlying == typeof(ushort)) return Raw >= ushort.MinValue && Raw <= ushort.MaxValue;
			if (underlying == typeof(int)) return Raw >= int.MinValue && Raw <= int.MaxValue;
			if (underlying == typeof(uint)) return Raw >= uint.MinValue && Raw <= uint.MaxValue;
			if (underlying == typeof(long)) return true;
			if (underlying == typeof(ulong)) return Raw >= 0L;
			return false;
		}

		private static void WriteString(BinaryWriter Writer, string Value, int MaximumBytes)
		{
			if (Value == null) { Writer.Write(-1); return; }
			int count = StrictUtf8.GetByteCount(Value);
			if (count > MaximumBytes) throw new InvalidDataException("Archived settlement string exceeds cap.");
			Writer.Write(count);
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(bytes);
		}

		private static string ReadString(BinaryReader Reader, int MaximumBytes, bool Required)
		{
			int length = Reader.ReadInt32();
			if (!Required && length == -1) return null;
			if (length < 0 || length > MaximumBytes)
				throw new InvalidDataException("Archived settlement string length exceeds cap.");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException("Archived settlement string is truncated.");
			return StrictUtf8.GetString(bytes);
		}

	}
}
