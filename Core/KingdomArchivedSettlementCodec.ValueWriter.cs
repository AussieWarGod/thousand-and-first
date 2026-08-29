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
		private static void WriteValue(BinaryWriter Writer, Type Type, object Value,
			int Depth, Budget Budget)
		{
			WriteValue(Writer, Type, Value, Depth, Budget, CurrentVersion);
		}

		private static void WriteValue(BinaryWriter Writer, Type Type, object Value,
			int Depth, Budget Budget, bool Legacy)
		{
			WriteValue(Writer, Type, Value, Depth, Budget,
				Legacy ? LegacyVersion : CurrentVersion);
		}

		private static void WriteValue(BinaryWriter Writer, Type Type, object Value,
			int Depth, Budget Budget, int SchemaVersion)
		{
			if (Depth > MaxDepth) throw new InvalidDataException("Archived settlement graph is too deep.");
			if (Value != null && !Type.IsValueType && Value.GetType() != Type)
				throw new InvalidDataException(
					"Archived settlement runtime type is not exact: " + Type.FullName + ".");
			if (Type == typeof(string))
			{
				WriteString(Writer, (string)Value, MaxStringBytes);
				return;
			}
			if (Type.IsEnum)
			{
				long raw = Convert.ToInt64(Value);
				if (!EnumRawFits(Type, raw) || !Enum.IsDefined(Type, Value))
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
				Writer.Write(raw);
				return;
			}
			if (Type == typeof(bool)) { Writer.Write((bool)Value ? (byte)1 : (byte)0); return; }
			if (Type == typeof(byte)) { Writer.Write((byte)Value); return; }
			if (Type == typeof(short)) { Writer.Write((short)Value); return; }
			if (Type == typeof(int)) { Writer.Write((int)Value); return; }
			if (Type == typeof(long)) { Writer.Write((long)Value); return; }
			if (Type == typeof(ushort)) { Writer.Write((ushort)Value); return; }
			if (Type == typeof(uint)) { Writer.Write((uint)Value); return; }
			if (Type == typeof(ulong)) { Writer.Write((ulong)Value); return; }
			if (Type == typeof(byte[]))
			{
				if (Value == null) { Writer.Write(-1); return; }
				byte[] bytes = (byte[])Value;
				if (bytes.Length > MaxByteArrayBytes)
					throw new InvalidDataException("Archived settlement byte array exceeds cap.");
				Writer.Write(bytes.Length);
				Writer.Write(bytes);
				return;
			}
			if (IsList(Type))
			{
				if (Value == null) { Writer.Write(-1); return; }
				IList list = (IList)Value;
				if (list.Count > MaxCollectionCount)
					throw new InvalidDataException("Archived settlement list exceeds cap.");
				Writer.Write(list.Count);
				Type itemType = Type.GetGenericArguments()[0];
				for (int i = 0; i < list.Count; i++)
					WriteValue(Writer, itemType, list[i], Depth + 1, Budget, SchemaVersion);
				return;
			}
			if (IsDictionary(Type))
			{
				if (Value == null) { Writer.Write(-1); return; }
				IDictionary dictionary = (IDictionary)Value;
				if (!CanonicalDictionaryComparer(Type, dictionary))
					throw new InvalidDataException(
						"Archived settlement dictionary comparer is noncanonical.");
				if (dictionary.Count > MaxCollectionCount)
					throw new InvalidDataException("Archived settlement dictionary exceeds cap.");
				Type[] arguments = Type.GetGenericArguments();
				if (arguments[0] != typeof(string))
					throw new InvalidDataException("Archived settlement dictionary key type is unsupported.");
				List<string> keys = new List<string>(dictionary.Count);
				foreach (object key in dictionary.Keys)
				{
					if (!(key is string)) throw new InvalidDataException("Archived dictionary key is null.");
					keys.Add((string)key);
				}
				keys.Sort(StringComparer.Ordinal);
				Writer.Write(keys.Count);
				for (int i = 0; i < keys.Count; i++)
				{
					WriteString(Writer, keys[i], MaxStringBytes);
					WriteValue(Writer, arguments[1], dictionary[keys[i]], Depth + 1, Budget,
						SchemaVersion);
				}
				return;
			}
			if (!Approved(Type))
				throw new InvalidDataException("Archived settlement field type is unsupported: " + Type.FullName);
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			if (++Budget.Objects > MaxObjects)
				throw new InvalidDataException("Archived settlement object count exceeds cap.");
			if (Type == typeof(KingdomGrowthFirstGuestOpportunity)
				&& !HistoricalPhysicalFirstGuestOpportunity(
					(KingdomGrowthFirstGuestOpportunity)Value, SchemaVersion))
				throw new InvalidDataException(
					"Archived settlement historical physical first-guest evidence is unknown.");
			if (SchemaVersion < ArrivalCadenceVersion
				&& !HistoricalArrivalCadenceValue(Type, Value))
				throw new InvalidDataException(
					"Archived settlement historical arrival cadence is unknown.");
			if (SchemaVersion >= ExactLogisticsVersion
				&& Type == typeof(Simulation.City.KingdomJobRegistry)
				&& !ValidDeliveryDomain(
					(Simulation.City.KingdomJobRegistry)Value, SchemaVersion))
				throw new InvalidDataException(
					"Archived settlement delivery enum domain is invalid for its version.");
			if (SchemaVersion >= CivicAuthorityVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& !ValidCivicAuthority((Simulation.City.KingdomCityBook)Value))
				throw new InvalidDataException(
					"Archived settlement civic authority is invalid for its version.");
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
					WriteString(Writer, (string)fields[i].GetValue(Value), maximum);
					continue;
				}
				object fieldValue = fields[i].GetValue(Value);
				if (Type == typeof(KingdomLifecycleBook)
					&& string.Equals(fields[i].Name, "FormatVersion", StringComparison.Ordinal)
					&& SchemaVersion < DefensiveReservationVersion)
					fieldValue = SchemaVersion == LegacyVersion
						? KingdomLifecycleRules.LegacyLifecycleFormatVersion
						: SchemaVersion == PreviousVersion
							? KingdomLifecycleRules.PreviousLifecycleFormatVersion
							: KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion;
				if (Type == typeof(KingdomRaidLedger)
					&& string.Equals(fields[i].Name, "Version", StringComparison.Ordinal)
					&& SchemaVersion < DefensiveReservationVersion)
					fieldValue = SchemaVersion < PhysicalHappeningVersion ? 1 : 2;
				if (Type == typeof(Simulation.City.KingdomCityBook)
					&& string.Equals(fields[i].Name, "SchemaVersion", StringComparison.Ordinal)
					&& SchemaVersion < SemanticSelectionVersion)
					fieldValue = 2;
				if (Type == typeof(KingdomGrowthBook)
					&& string.Equals(fields[i].Name, "FormatVersion", StringComparison.Ordinal)
					&& SchemaVersion < PhysicalFirstGuestVersion)
					fieldValue = SchemaVersion < FirstGuestVersion
						? SchemaVersion < SemanticSelectionVersion
							? KingdomLifecycleRules.PreviousGrowthFormatVersion
							: KingdomLifecycleRules.SemanticGrowthFormatVersion
						: KingdomLifecycleRules.TerminalReceiptGrowthFormatVersion;
				if (Type == typeof(KingdomGrowthFirstGuestTerminalReceipt)
					&& string.Equals(fields[i].Name, "Version", StringComparison.Ordinal)
					&& SchemaVersion < PhysicalFirstGuestVersion)
					fieldValue = KingdomGrowthFirstGuestTerminalReceipt.LegacyVersion;
				WriteValue(Writer, fields[i].FieldType, fieldValue,
					Depth + 1, Budget, SchemaVersion);
			}
		}

	}
}
