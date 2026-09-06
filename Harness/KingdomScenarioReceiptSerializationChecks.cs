using System;
using System.Reflection;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using XRL;
using XRL.Serialization;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomScenarioReceiptSerializationChecks
	{
		// Legacy cases generate trusted OtherType fixtures, not historical save artifacts.
		internal static int Run()
		{
			Type[] roots = { typeof(KingdomResidentDepartureOperation), typeof(KingdomResidentAdmissionOperation),
				typeof(KingdomCivicOfficeReceipt), typeof(KingdomPolityNamedFigureRecord) };
			int passed = 0;
			foreach (Type type in roots)
			{
				object fixture = Populate(type, type.Name, 0);
				Roundtrip(fixture, false);
				passed++;
				object legacy = Roundtrip(fixture, true);
				Roundtrip(legacy, false);
				passed++;
			}
			return passed;
		}

		private static object Roundtrip(object expected, bool legacy)
		{
			string label = expected.GetType().Name + (legacy ? " legacy-format" : " named");
			try
			{
				ValidateGraph(expected, 0);
				Type codes = typeof(SerializationWriter).Assembly.GetType("XRL.Serialization.SerializedType", true);
				byte other = Convert.ToByte(Enum.Parse(codes, "OtherType"));
				byte composite = Convert.ToByte(Enum.Parse(codes, "ICompositeType"));
				Check(other == 132, "engine OtherType code changed");
				byte[] bytes;
				long start, end;
				FastSerialization.Cache writing = new FastSerialization.Cache(0);
				using (SerializationWriter writer = new SerializationWriter(writing))
				{
					writer.Start(XRLGame.SaveVersion, SerializePlayer: true);
					// Start seeds the player; this rack belongs only to this record-only writer.
					writing.GameObjects.Clear();
					start = writing.MemoryStream.Position;
					if (legacy)
					{
						writer.Write(other);
						BinaryFormatter formatter = new BinaryFormatter
						{
							AssemblyFormat = FormatterAssemblyStyle.Simple,
							TypeFormat = FormatterTypeStyle.TypesWhenNeeded
						};
						formatter.Serialize(writing.MemoryStream, expected);
					}
					else writer.WriteObject(expected);
					end = writing.MemoryStream.Position;
					CheckEmptyCounts(writing);
					writer.FinalizeWrite();
					CheckEmptyCounts(writing);
					bytes = writing.MemoryStream.ToArray();
				}
				Check(start >= 0 && end > start && end < bytes.Length && bytes.Length < 1048576,
					"invalid record payload bounds");
				Check(bytes[(int)start] == (legacy ? other : composite), "wrong engine record dispatch");
				FastSerialization.Cache reading = new FastSerialization.Cache(0);
				using (reading.MemoryStream)
				using (SerializationReader reader = new SerializationReader(reading))
				{
					reading.MemoryStream.Write(bytes, 0, bytes.Length);
					reading.MemoryStream.Position = 0;
					reader.Start(SerializePlayer: true);
					Check(reader.FileVersion == XRLGame.SaveVersion && reading.MemoryStream.Position == start,
						"reader did not reach the exact record start");
					CheckNoReadObjects(reading);
					object actual = reader.ReadObject();
					Check(reader.Errors == 0 && reading.MemoryStream.Position == end,
						"record read failed or did not consume its exact payload");
					EqualGraph(expected, actual, 0);
					// FinalizeRead clears global load bindings; this graph has no objects to finalize.
					reader.ReadGameObjects();
					reader.ReadEventRegistries();
					Check(reader.Errors == 0, "empty record trailers failed");
					CheckNoReadObjects(reading);
					return actual;
				}
			}
			catch (Exception error)
			{
				throw new InvalidOperationException("Receipt serialization check failed: " + label + "; " + error.Message, error);
			}
		}

		private static object Populate(Type type, string path, int depth)
		{
			Check(depth <= 1 && RecordType(type), "fixture record type is not whitelisted");
			object value = Activator.CreateInstance(type);
			int marker = 10;
			foreach (FieldInfo field in Fields(type))
			{
				Type item = field.FieldType;
				object data;
				if (item == typeof(string)) data = path + "." + field.Name + ":\u00e9\u03a9";
				else if (item == typeof(int)) data = field.Name == "Version" ? 1 : ++marker;
				else if (item == typeof(long)) data = 4294967296L + ++marker;
				else if (item == typeof(bool)) data = true;
				else if (item.IsEnum) data = NonzeroEnum(item);
				else data = Populate(item, path + "." + field.Name, depth + 1);
				field.SetValue(value, data);
			}
			return value;
		}

		private static object NonzeroEnum(Type type)
		{
			foreach (object value in Enum.GetValues(type))
				if (Convert.ToInt64(value) != 0) return value;
			throw new InvalidOperationException("Fixture enum has no nonzero value: " + type.Name);
		}

		private static void ValidateGraph(object value, int depth)
		{
			Check(value != null && depth <= 1 && RecordType(value.GetType()), "record graph is not whitelisted");
			Check(value is IComposite composite && !composite.WantFieldReflection, "record is not named composite");
			foreach (FieldInfo field in Fields(value.GetType()))
			{
				object item = field.GetValue(value);
				Check(item != null && item.GetType() == field.FieldType, "fixture field is null or has a foreign type");
				if (RecordType(field.FieldType)) ValidateGraph(item, depth + 1);
				else if (field.FieldType == typeof(string)) Check(((string)item).Length > 0, "empty fixture string");
				else if (field.FieldType.IsEnum)
					Check(Enum.IsDefined(field.FieldType, item) && Convert.ToInt64(item) != 0, "invalid fixture enum");
			}
		}

		private static void EqualGraph(object expected, object actual, int depth)
		{
			Check(actual != null && actual.GetType() == expected.GetType() && !ReferenceEquals(expected, actual),
				"record type, presence, or fresh reference differs");
			ValidateGraph(actual, depth);
			foreach (FieldInfo field in Fields(expected.GetType()))
			{
				object before = field.GetValue(expected), after = field.GetValue(actual);
				if (RecordType(field.FieldType)) EqualGraph(before, after, depth + 1);
				else Check(object.Equals(before, after), "field changed: " + expected.GetType().Name + "." + field.Name);
			}
		}

		private static FieldInfo[] Fields(Type type)
		{
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Check(fields.Length > 0, "empty fixture field set");
			foreach (FieldInfo field in fields)
			{
				Type item = field.FieldType;
				Check(field.IsPublic && !field.IsInitOnly && (field.Attributes & FieldAttributes.NotSerialized) == 0
					&& (item == typeof(string) || item == typeof(int) || item == typeof(long) || item == typeof(bool)
						|| item.IsEnum || RecordType(item)), "unreviewed serialized field: " + type.Name + "." + field.Name);
			}
			Array.Sort(fields, (left, right) => string.CompareOrdinal(left.Name, right.Name));
			return fields;
		}

		private static bool RecordType(Type type)
		{
			return type == typeof(KingdomResidentDepartureOperation) || type == typeof(KingdomResidentAdmissionOperation)
				|| type == typeof(KingdomNamedCookReceipt) || type == typeof(KingdomCivicOfficeReceipt)
				|| type == typeof(KingdomPolityNamedFigureRecord);
		}

		private static void CheckEmptyCounts(FastSerialization.Cache cache)
		{
			Check(cache.GameObjects.Count == 0 && cache.GameObjectReferences.Count == 0
				&& cache.EventRegistries.Count == 0 && cache.Tokenized.Count == 0 && cache.Objects.Count == 0,
				"record graph introduced an object, reference, event registry, or token object");
		}

		private static void CheckNoReadObjects(FastSerialization.Cache cache)
		{
			CheckEmptyCounts(cache);
			Check(cache.GameObjects.Capacity == 0 && cache.GameObjectReferences.Capacity == 0
				&& cache.EventRegistries.Capacity == 0 && cache.Tokenized.Capacity == 0 && cache.Objects.Capacity == 0,
				"record reader allocated an object, reference, event registry, or token object");
		}

		private static void Check(bool condition, string message)
		{
			if (!condition) throw new InvalidOperationException(message);
		}
	}
}
