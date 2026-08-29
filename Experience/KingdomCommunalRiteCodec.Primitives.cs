using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomCommunalRiteCodec
	{
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		private static BinaryWriter Writer(Stream stream)
		{
			return new BinaryWriter(stream, StrictUtf8, true);
		}

		private static BinaryReader Reader(Stream stream)
		{
			return new BinaryReader(stream, StrictUtf8, true);
		}

		private static void WriteString(BinaryWriter writer, string value, int maximum)
		{
			if (value == null) { writer.Write(-1); return; }
			byte[] bytes = StrictUtf8.GetBytes(value);
			if (bytes.Length > maximum) throw new InvalidDataException(
				"communal-rite string exceeds cap");
			writer.Write(bytes.Length); writer.Write(bytes);
		}

		private static string ReadString(BinaryReader reader, int maximum)
		{
			int length = reader.ReadInt32();
			if (length == -1) return null;
			if (length < 0 || length > maximum)
				throw new InvalidDataException("communal-rite string exceeds cap");
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return StrictUtf8.GetString(bytes);
		}

		private static bool ReadBool(BinaryReader reader)
		{
			byte value = reader.ReadByte();
			if (value > 1) throw new InvalidDataException("communal-rite boolean is invalid");
			return value == 1;
		}
	}
}
