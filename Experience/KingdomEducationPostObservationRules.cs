using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure canonical payload rules for attended education-post observations.</summary>
	public static class KingdomEducationPostObservationRules
	{
		public const string Purpose = "taf.education-post";
		public const string SourceRevision = "taf.education-post.zone/v1";
		public const int MaxRows = 64;
		public const int MaxRootChars = 512;
		public const int MaxDesignationChars = 256;
		public const int MaxBlueprintChars = 256;
		private const string Prefix = "TAFED1:";
		private const int Magic = 0x31444554;
		private const int Version = 1;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		public static bool TryEncode(IList<KingdomEducationPostObservationRow> Rows,
			out string Payload)
		{
			Payload = null;
			if (Rows == null || Rows.Count > MaxRows) return false;
			List<KingdomEducationPostObservationRow> sorted = new List<KingdomEducationPostObservationRow>();
			for (int i = 0; i < Rows.Count; i++)
			{
				if (!Valid(Rows[i])) return false;
				sorted.Add(Copy(Rows[i]));
			}
			sorted.Sort(Compare);
			if (!Unique(sorted)) return false;
			try
			{
				byte[] bytes;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8))
				{
					writer.Write(Magic); writer.Write(Version); writer.Write(sorted.Count);
					for (int i = 0; i < sorted.Count; i++) Write(writer, sorted[i]);
					writer.Flush(); bytes = stream.ToArray();
				}
				string encoded = Prefix + Convert.ToBase64String(bytes);
				if (encoded.Length > KingdomZoneObservationRules.MaxPayloadChars) return false;
				Payload = encoded; return true;
			}
			catch { Payload = null; return false; }
		}

		public static bool TryDecode(string Payload,
			out List<KingdomEducationPostObservationRow> Rows)
		{
			Rows = null;
			if (string.IsNullOrEmpty(Payload)
				|| Payload.Length > KingdomZoneObservationRules.MaxPayloadChars
				|| !Payload.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			try
			{
				string encoded = Payload.Substring(Prefix.Length);
				byte[] bytes = Convert.FromBase64String(encoded);
				if (bytes.Length < 12 || Convert.ToBase64String(bytes) != encoded) return false;
				List<KingdomEducationPostObservationRow> parsed;
				using (MemoryStream stream = new MemoryStream(bytes, false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8))
				{
					if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) return false;
					int count = reader.ReadInt32();
					if (count < 0 || count > MaxRows) return false;
					parsed = new List<KingdomEducationPostObservationRow>(count);
					for (int i = 0; i < count; i++) parsed.Add(Read(reader));
					if (stream.Position != stream.Length) return false;
				}
				if (!TryEncode(parsed, out string canonical)
					|| !string.Equals(canonical, Payload, StringComparison.Ordinal)) return false;
				Rows = parsed; return true;
			}
			catch { Rows = null; return false; }
		}

		public static bool TryFindExact(string Payload, int WorkId, string ZoneId,
			int AnchorX, int AnchorY, string Blueprint,
			out KingdomEducationPostObservationRow Row)
		{
			Row = null;
			if (!TryDecode(Payload, out List<KingdomEducationPostObservationRow> rows)) return false;
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomEducationPostObservationRow candidate = rows[i];
				if (candidate.WorkId != WorkId) continue;
				if (!string.Equals(candidate.ZoneId, ZoneId, StringComparison.Ordinal)
					|| candidate.AnchorX != AnchorX || candidate.AnchorY != AnchorY
					|| !string.Equals(candidate.Blueprint, Blueprint,
						StringComparison.Ordinal)) return false;
				Row = candidate; return true;
			}
			return false;
		}

		public static bool Valid(KingdomEducationPostObservationRow Row)
		{
			return Row != null && Row.WorkId > 0 && Row.AnchorX >= 0 && Row.AnchorY >= 0
				&& Row.AnchorX <= short.MaxValue && Row.AnchorY <= short.MaxValue
				&& KingdomZoneObservationRules.Text(Row.RootId, MaxRootChars)
				&& KingdomZoneObservationRules.Text(Row.DesignationIdentity,
					MaxDesignationChars)
				&& KingdomZoneObservationRules.Text(Row.DesignationRevision,
					MaxDesignationChars)
				&& KingdomZoneObservationRules.Text(Row.ZoneId,
					KingdomZoneObservationRules.MaxIdentityChars)
				&& KingdomZoneObservationRules.Text(Row.Blueprint, MaxBlueprintChars);
		}

		private static bool Unique(List<KingdomEducationPostObservationRow> Rows)
		{
			HashSet<int> works = new HashSet<int>();
			HashSet<string> roots = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> designations = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Rows.Count; i++)
				if (!works.Add(Rows[i].WorkId) || !roots.Add(Rows[i].RootId)
					|| !designations.Add(Rows[i].DesignationIdentity)) return false;
			return true;
		}

		private static int Compare(KingdomEducationPostObservationRow A,
			KingdomEducationPostObservationRow B)
		{
			int compared = A.WorkId.CompareTo(B.WorkId);
			if (compared != 0) return compared;
			compared = string.CompareOrdinal(A.RootId, B.RootId);
			if (compared != 0) return compared;
			return string.CompareOrdinal(A.DesignationIdentity, B.DesignationIdentity);
		}

		private static KingdomEducationPostObservationRow Copy(
			KingdomEducationPostObservationRow Row)
		{
			return new KingdomEducationPostObservationRow { WorkId = Row.WorkId,
				RootId = Row.RootId, DesignationIdentity = Row.DesignationIdentity,
				DesignationRevision = Row.DesignationRevision, ZoneId = Row.ZoneId,
				AnchorX = Row.AnchorX, AnchorY = Row.AnchorY, Blueprint = Row.Blueprint };
		}

		private static void Write(BinaryWriter Writer, KingdomEducationPostObservationRow Row)
		{
			Writer.Write(Row.WorkId); KingdomZoneObservationRules.Write(Writer, Row.RootId);
			KingdomZoneObservationRules.Write(Writer, Row.DesignationIdentity);
			KingdomZoneObservationRules.Write(Writer, Row.DesignationRevision);
			KingdomZoneObservationRules.Write(Writer, Row.ZoneId);
			Writer.Write(Row.AnchorX); Writer.Write(Row.AnchorY);
			KingdomZoneObservationRules.Write(Writer, Row.Blueprint);
		}

		private static KingdomEducationPostObservationRow Read(BinaryReader Reader)
		{
			return new KingdomEducationPostObservationRow { WorkId = Reader.ReadInt32(),
				RootId = ReadText(Reader, MaxRootChars),
				DesignationIdentity = ReadText(Reader, MaxDesignationChars),
				DesignationRevision = ReadText(Reader, MaxDesignationChars),
				ZoneId = ReadText(Reader, KingdomZoneObservationRules.MaxIdentityChars),
				AnchorX = Reader.ReadInt32(), AnchorY = Reader.ReadInt32(),
				Blueprint = ReadText(Reader, MaxBlueprintChars) };
		}

		private static string ReadText(BinaryReader Reader, int MaximumChars)
		{
			int count = Reader.ReadInt32();
			if (count <= 0 || count > MaximumChars * 4
				|| Reader.BaseStream.Length - Reader.BaseStream.Position < count)
				throw new InvalidDataException();
			string value = Utf8.GetString(Reader.ReadBytes(count));
			if (value.Length > MaximumChars) throw new InvalidDataException(); return value;
		}
	}
}
