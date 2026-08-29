using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Bounded canonical wire codec for zone-owned relocation authority.</summary>
	public static partial class KingdomRelocationCodec
	{
		private const int Magic = 0x52464154; // TAFR, little-endian.
		private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool TryEncode(KingdomRelocationReceipt Receipt, out string Encoded,
			out string Failure)
		{
			Encoded = null;
			if (!KingdomRelocationRules.Valid(Receipt, out Failure)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					Write(writer, Receipt);
					writer.Flush();
					Encoded = Convert.ToBase64String(stream.ToArray());
				}
			}
			catch (Exception exception)
			{
				Failure = "relocation receipt encode failed: " + exception.Message;
				Encoded = null;
				return false;
			}
			if (Encoded.Length > KingdomRelocationRules.MaxReceiptChars)
			{
				Failure = "relocation receipt exceeds its encoded bound";
				Encoded = null;
				return false;
			}
			Failure = null;
			return true;
		}

		public static bool TryDecode(string Encoded, out KingdomRelocationReceipt Receipt,
			out string Failure)
		{
			Receipt = null;
			Failure = null;
			if (string.IsNullOrEmpty(Encoded)
				|| Encoded.Length > KingdomRelocationRules.MaxReceiptChars)
				return Fail("relocation receipt is absent or over its encoded bound", out Failure);
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded);
				if (bytes.Length > KingdomRelocationRules.MaxReceiptChars * 3L / 4L)
					return Fail("relocation receipt payload exceeds its bound", out Failure);
				using (MemoryStream stream = new MemoryStream(bytes, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					if (!Read(reader, out Receipt, out Failure)) return false;
					if (stream.Position != stream.Length)
						return Fail("relocation receipt has trailing bytes", out Failure);
				}
			}
			catch (Exception exception)
			{
				Receipt = null;
				return Fail("relocation receipt decode failed: " + exception.Message, out Failure);
			}
			if (!KingdomRelocationRules.Valid(Receipt, out Failure))
			{
				Receipt = null;
				return false;
			}
			return true;
		}

		private static void WriteRect(BinaryWriter Writer, KingdomRelocationRect Rect)
		{
			Writer.Write(Rect.X1); Writer.Write(Rect.Y1);
			Writer.Write(Rect.X2); Writer.Write(Rect.Y2);
		}

		private static KingdomRelocationRect ReadRect(BinaryReader Reader)
		{
			return new KingdomRelocationRect(Reader.ReadInt32(), Reader.ReadInt32(),
				Reader.ReadInt32(), Reader.ReadInt32());
		}

		private static void WriteText(BinaryWriter Writer, string Value)
		{
			byte[] bytes = StrictUtf8.GetBytes(Value ?? "");
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static string ReadText(BinaryReader Reader, int Maximum)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > Maximum * 4) throw new InvalidDataException(
				"text byte count is outside its bound");
			byte[] bytes = Reader.ReadBytes(count);
			if (bytes.Length != count) throw new EndOfStreamException();
			string value = StrictUtf8.GetString(bytes);
			if (value.Length > Maximum) throw new InvalidDataException(
				"decoded text is outside its character bound");
			return value;
		}

		private static void WriteOptional(BinaryWriter Writer, string Value)
		{
			Writer.Write(Value != null);
			if (Value != null) WriteText(Writer, Value);
		}

		private static string ReadOptional(BinaryReader Reader, int Maximum)
		{
			return Reader.ReadBoolean() ? ReadText(Reader, Maximum) : null;
		}

		private static bool Fail(string Text, out string Failure)
		{
			Failure = Text; return false;
		}
	}
}
