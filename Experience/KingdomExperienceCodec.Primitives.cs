using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceCodec
	{
		private delegate void RowWriter<T>(BinaryWriter Writer, T Value);
		private delegate T RowReader<T>(BinaryReader Reader);
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		private static void WriteString(BinaryWriter W, string Value)
		{
			if (Value == null) { W.Write(-1); return; }
			byte[] bytes;
			try { bytes = StrictUtf8.GetBytes(Value); }
			catch (EncoderFallbackException e)
			{ throw new InvalidDataException("Experience text is not strict UTF-8.", e); }
			if (bytes.Length > KingdomExperienceRules.MaxFaultTextBytes)
				throw new InvalidDataException("Experience text exceeds hard bound.");
			W.Write(bytes.Length); W.Write(bytes, 0, bytes.Length);
		}

		private static string ReadString(BinaryReader R)
		{
			int count = R.ReadInt32(); if (count == -1) return null;
			if (count < 0 || count > KingdomExperienceRules.MaxFaultTextBytes)
				throw new InvalidDataException("Experience text length is invalid.");
			byte[] bytes = R.ReadBytes(count);
			if (bytes.Length != count) throw new EndOfStreamException("Truncated experience text.");
			try { return StrictUtf8.GetString(bytes); }
			catch (DecoderFallbackException e)
			{ throw new InvalidDataException("Experience text is not strict UTF-8.", e); }
		}

		private static void WriteVoiceText(BinaryWriter W, string Value)
		{
			byte[] bytes;
			try { bytes = StrictUtf8.GetBytes(Value ?? ""); }
			catch (EncoderFallbackException e)
			{ throw new InvalidDataException("Civic voice facts are not strict UTF-8.", e); }
			if (bytes.Length < 1 || bytes.Length > KingdomCivicVoiceRules.MaxFactsBytes)
				throw new InvalidDataException("Civic voice facts exceed their hard bound.");
			W.Write(bytes.Length); W.Write(bytes, 0, bytes.Length);
		}

		private static string ReadVoiceText(BinaryReader R)
		{
			int count = ReadCount(R, KingdomCivicVoiceRules.MaxFactsBytes, "voice-fact byte");
			if (count == 0) throw new InvalidDataException("Civic voice facts are empty.");
			byte[] bytes = R.ReadBytes(count);
			if (bytes.Length != count) throw new EndOfStreamException("Truncated civic voice facts.");
			try { return StrictUtf8.GetString(bytes); }
			catch (DecoderFallbackException e)
			{ throw new InvalidDataException("Civic voice facts are not strict UTF-8.", e); }
		}

		private static void WriteBool(BinaryWriter W, bool Value)
		{
			W.Write(Value ? (byte)1 : (byte)0);
		}

		private static bool ReadBool(BinaryReader R)
		{
			byte value = R.ReadByte();
			if (value > 1) throw new InvalidDataException("Experience bool is noncanonical.");
			return value == 1;
		}

		private static int ReadCount(BinaryReader R, int Maximum, string Name)
		{
			int count = R.ReadInt32();
			if (count < 0 || count > Maximum)
				throw new InvalidDataException("Experience " + Name + " count is invalid.");
			return count;
		}

		private static void WriteList<T>(BinaryWriter W, IList<T> Values, int Maximum,
			RowWriter<T> Write)
		{
			if (Values == null || Values.Count > Maximum)
				throw new InvalidDataException("Experience collection exceeds hard bound.");
			W.Write(Values.Count);
			for (int i = 0; i < Values.Count; i++)
			{
				if (Values[i] == null) throw new InvalidDataException("Experience row is null.");
				Write(W, Values[i]);
			}
		}

		private static List<T> ReadList<T>(BinaryReader R, int Maximum, RowReader<T> Read)
		{
			int count = ReadCount(R, Maximum, "row"); List<T> result = new List<T>(count);
			for (int i = 0; i < count; i++) result.Add(Read(R));
			return result;
		}

		private static void RequireEnd(MemoryStream Stream)
		{
			if (Stream.Position != Stream.Length)
				throw new InvalidDataException("Trailing experience payload bytes.");
		}
	}
}
