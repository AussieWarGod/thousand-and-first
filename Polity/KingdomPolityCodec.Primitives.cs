using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private delegate void RowWriter<T>(BinaryWriter Writer, T Value);
		private delegate T RowReader<T>(BinaryReader Reader);

		private static void WriteString(BinaryWriter W, string Value)
		{
			if (Value == null) { W.Write(-1); return; }
			byte[] bytes;
			try { bytes = StrictUtf8.GetBytes(Value); }
			catch (System.Text.EncoderFallbackException e)
			{ throw new InvalidDataException("Polity text is not strict UTF-8.", e); }
			if (bytes.Length > KingdomPolityRules.MaxTextBytes)
				throw new InvalidDataException("Polity text exceeds hard bound.");
			W.Write(bytes.Length); W.Write(bytes, 0, bytes.Length);
		}

		private static string ReadString(BinaryReader R)
		{
			int count = R.ReadInt32(); if (count == -1) return null;
			if (count < 0 || count > KingdomPolityRules.MaxTextBytes)
				throw new InvalidDataException("Polity text length is invalid.");
			byte[] bytes = R.ReadBytes(count);
			if (bytes.Length != count) throw new EndOfStreamException("Truncated polity text.");
			try { return StrictUtf8.GetString(bytes); }
			catch (System.Text.DecoderFallbackException e)
			{ throw new InvalidDataException("Polity text is not strict UTF-8.", e); }
		}

		private static void WriteBool(BinaryWriter W, bool Value) { W.Write(Value ? (byte)1 : (byte)0); }
		private static bool ReadBool(BinaryReader R)
		{
			byte value = R.ReadByte();
			if (value > 1) throw new InvalidDataException("Polity bool is noncanonical.");
			return value == 1;
		}

		private static int ReadCount(BinaryReader R, int Maximum, string Name)
		{
			int count = R.ReadInt32();
			if (count < 0 || count > Maximum)
				throw new InvalidDataException("Polity " + Name + " count is invalid.");
			return count;
		}

		private static void WriteList<T>(BinaryWriter W, IList<T> Values, int Maximum,
			RowWriter<T> Write)
		{
			if (Values == null || Values.Count > Maximum)
				throw new InvalidDataException("Polity collection exceeds hard bound.");
			W.Write(Values.Count);
			for (int i = 0; i < Values.Count; i++)
			{
				if (Values[i] == null) throw new InvalidDataException("Polity row is null.");
				Write(W, Values[i]);
			}
		}

		private static List<T> ReadList<T>(BinaryReader R, int Maximum, RowReader<T> Read)
		{
			int count = ReadCount(R, Maximum, "row"); List<T> result = new List<T>(count);
			for (int i = 0; i < count; i++) result.Add(Read(R));
			return result;
		}

		private static void WriteStrings(BinaryWriter W, IList<string> Values, int Maximum)
		{
			WriteList(W, Values, Maximum, delegate(BinaryWriter writer, string value)
			{
				WriteString(writer, value);
			});
		}

		private static List<string> ReadStrings(BinaryReader R, int Maximum)
		{
			return ReadList(R, Maximum, ReadString);
		}

		private static void WriteNullable<T>(BinaryWriter W, T Value, RowWriter<T> Write)
			where T : class
		{
			WriteBool(W, Value != null); if (Value != null) Write(W, Value);
		}

		private static T ReadNullable<T>(BinaryReader R, RowReader<T> Read) where T : class
		{
			return ReadBool(R) ? Read(R) : null;
		}

		private static void RequireEnd(MemoryStream Stream)
		{
			if (Stream.Position != Stream.Length)
				throw new InvalidDataException("Trailing polity payload bytes.");
		}
	}
}
