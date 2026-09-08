using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ThousandAndFirst.Harness
{
	// Explicit raw-field whitelist: no getters, constructors, native serialization or normalization.
	// Unknown fields/types refuse. Null, empty, ordered collections and nested identities stay distinct.
	internal static class KingdomUpgradeGraph
	{
		internal const int MaxBytes = 2097152;
		private const int Magic = 0x31475554;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
		private const string Inheritance = "SerializationVersion PhaseValue LegacyText ReceiptText CommittedReceiptText "
			+ "TargetZoneId TargetTerrainBlueprint TargetTerrainRank SecretId SiteName FailureDetail ApplyStatusValue "
			+ "ApplyFaultValue ApplicationMarker FailureAnnounced ReleasePending OwnsSkipTerrainBuilders OwnsNoBiomes "
			+ "OwnsZoneName RecoveryDisabled RetryAuthorized";
		private const string Transition = "Version Phase Revision TransitionId CauseRef OldRealmId OldCurrentPolityId "
			+ "OldCurrentFactionId OldCurrentProjectionId OldCurrentProjectionDigest OldImportedPolityId OldImportedFactionId "
			+ "OldImportedProjectionId OldImportedProjectionDigest OldImportedWasVisible ClosedTick SourceRevision "
			+ "RetiredRevision DetachedRevision ReboundRevision ReturnLedgerDigest RetiredLedgerDigest ReturnLedgerEnvelope "
			+ "Legacy ReboundRealmId ReboundPolityId ReboundFactionId Fault";
		private const string Legacy = "ProfileSchema TechnologyBand CanonicalBodyKeys SourceProfileDigest ProfileProvenanceDigest "
			+ "LegacyToken LineageToken FounderName RealmName SettlementName Vocation Style Stage Population Defence "
			+ "StoredWater InheritedState RollNames OriginKeys OriginCounts CreedKeys CreedCounts";

		internal static string Capture(object value, List<object> references = null)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
			{
				writer.Write(Magic); Write(writer, value, references, 0); writer.Flush();
				if (stream.Length > MaxBytes) throw new InvalidDataException("upgrade graph exceeds bound");
				return Convert.ToBase64String(stream.ToArray());
			}
		}

		internal static bool Valid(string wire)
		{
			if (wire == null || wire.Length < 8 || wire.Length > ((MaxBytes + 2) / 3) * 4) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(wire);
				if (bytes.Length > MaxBytes || Convert.ToBase64String(bytes) != wire) return false;
				using (MemoryStream stream = new MemoryStream(bytes, false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic) return false;
					Read(reader, 0); return stream.Position == stream.Length;
				}
			}
			catch (Exception) { return false; }
		}

		private static void Write(BinaryWriter w, object value, List<object> refs, int depth)
		{
			if (depth > 3) throw new InvalidDataException("upgrade graph depth");
			if (value == null) { w.Write((byte)0); return; }
			if (value is string text) { w.Write((byte)1); Text(w, text); return; }
			if (value is int number) { w.Write((byte)2); w.Write(number); return; }
			if (value is long clock) { w.Write((byte)3); w.Write(clock); return; }
			if (value is bool bit) { w.Write((byte)4); w.Write((byte)(bit ? 1 : 0)); return; }
			if (value.GetType().IsEnum) { w.Write((byte)5); Text(w, value.GetType().FullName); w.Write(Convert.ToInt64(value)); return; }
			refs?.Add(value);
			if (value is byte[] bytes)
			{
				if (bytes.Length > 1048576) throw new InvalidDataException("upgrade byte array bound");
				w.Write((byte)6); w.Write(bytes.Length); w.Write(bytes); return;
			}
			if (value is List<string> || value is List<int>)
			{
				IList list = (IList)value;
				if (list.Count > 128) throw new InvalidDataException("upgrade list bound");
				w.Write((byte)(value is List<string> ? 7 : 8)); w.Write(list.Count);
				foreach (object item in list) Write(w, item, refs, depth + 1);
				return;
			}
			byte kind = Kind(value.GetType().FullName);
			string[] names = Names(kind);
			FieldInfo[] fields = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public
				| BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(f => !f.IsDefined(typeof(NonSerializedAttribute), false)).ToArray();
			if (fields.Length != names.Length || fields.Any(f => Array.BinarySearch(names, f.Name, StringComparer.Ordinal) < 0))
				throw new InvalidDataException("upgrade observed schema changed");
			foreach (FieldInfo field in fields)
			{
				Type type = field.FieldType;
				if (type != typeof(string) && type != typeof(int) && type != typeof(long) && type != typeof(bool)
					&& type != typeof(byte[]) && type != typeof(List<string>) && type != typeof(List<int>)
					&& !type.IsEnum && type.FullName != "ThousandAndFirst.KingdomPolityLegacySnapshot")
					throw new InvalidDataException("upgrade observed field type changed");
			}
			w.Write((byte)9); w.Write(kind); w.Write(names.Length);
			foreach (string name in names)
			{
				Text(w, name); Write(w, fields.Single(f => f.Name == name).GetValue(value), refs, depth + 1);
				if (w.BaseStream.Length > MaxBytes) throw new InvalidDataException("upgrade graph exceeds bound");
			}
		}

		private static void Read(BinaryReader r, int depth)
		{
			if (depth > 3) throw new InvalidDataException();
			byte tag = r.ReadByte();
			switch (tag)
			{
				case 0: return;
				case 1: Text(r); return;
				case 2: r.ReadInt32(); return;
				case 3: r.ReadInt64(); return;
				case 4: if (r.ReadByte() > 1) throw new InvalidDataException(); return;
				case 5: Text(r); r.ReadInt64(); return;
				case 6:
					int length = Count(r, 1048576);
					if (r.ReadBytes(length).Length != length) throw new EndOfStreamException(); return;
				case 7: case 8:
					int count = Count(r, 128);
					for (int i = 0; i < count; i++) { RequireTag(r, tag == 7 ? (byte)1 : (byte)2); Read(r, depth + 1); }
					return;
				case 9:
					byte kind = r.ReadByte(); string[] names = Names(kind);
					if (r.ReadInt32() != names.Length) throw new InvalidDataException();
					foreach (string name in names)
					{
						if (Text(r) != name) throw new InvalidDataException();
						RequireTag(r, FieldTag(kind, name)); Read(r, depth + 1);
					}
					return;
				default: throw new InvalidDataException();
			}
		}

		private static void RequireTag(BinaryReader reader, byte expected)
		{
			long at = reader.BaseStream.Position; byte actual = reader.ReadByte();
			if (actual != expected && !(actual == 0 && (expected == 1 || expected >= 6 && expected <= 9)))
				throw new InvalidDataException();
			if (actual == 9 && reader.ReadByte() != 3) throw new InvalidDataException();
			reader.BaseStream.Position = at;
		}

		private static byte FieldTag(byte kind, string name)
		{
			if (kind == 1)
			{
				if (Has("SerializationVersion PhaseValue TargetTerrainRank ApplyStatusValue ApplyFaultValue", name)) return 2;
				if (Has("FailureAnnounced ReleasePending OwnsSkipTerrainBuilders OwnsNoBiomes OwnsZoneName RecoveryDisabled RetryAuthorized", name)) return 4;
			}
			else if (kind == 2)
			{
				if (name == "Version") return 2;
				if (name == "Phase") return 5;
				if (Has("Revision ClosedTick SourceRevision RetiredRevision DetachedRevision ReboundRevision", name)) return 3;
				if (name == "OldImportedWasVisible") return 4;
				if (name == "ReturnLedgerEnvelope") return 6;
				if (name == "Legacy") return 9;
			}
			else
			{
				if (Has("ProfileSchema TechnologyBand Stage Population Defence StoredWater InheritedState", name)) return 2;
				if (Has("CanonicalBodyKeys RollNames OriginKeys CreedKeys", name)) return 7;
				if (Has("OriginCounts CreedCounts", name)) return 8;
			}
			return 1;
		}

		private static bool Has(string fields, string name) { return (" " + fields + " ").Contains(" " + name + " "); }

		private static byte Kind(string name)
		{
			if (name == "ThousandAndFirst.KingdomInheritanceState") return 1;
			if (name == "ThousandAndFirst.KingdomPolityRealmTransition") return 2;
			if (name == "ThousandAndFirst.KingdomPolityLegacySnapshot") return 3;
			throw new InvalidDataException("unrecognized upgrade graph type");
		}

		private static string[] Names(byte kind)
		{
			string text = kind == 1 ? Inheritance : kind == 2 ? Transition : kind == 3 ? Legacy : null;
			if (text == null) throw new InvalidDataException();
			string[] names = text.Split(' '); Array.Sort(names, StringComparer.Ordinal); return names;
		}

		private static void Text(BinaryWriter writer, string text)
		{
			byte[] bytes = Utf8.GetBytes(text);
			if (bytes.Length > 1048576) throw new InvalidDataException("upgrade text bound");
			writer.Write(bytes.Length); writer.Write(bytes);
		}

		private static string Text(BinaryReader reader)
		{
			int length = Count(reader, 1048576); byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return Utf8.GetString(bytes);
		}

		private static int Count(BinaryReader reader, int maximum)
		{
			int count = reader.ReadInt32();
			if (count < 0 || count > maximum || count > reader.BaseStream.Length - reader.BaseStream.Position)
				throw new InvalidDataException();
			return count;
		}
	}
}
