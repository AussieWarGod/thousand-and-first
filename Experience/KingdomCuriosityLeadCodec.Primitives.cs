using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The smallest pieces of the wire, and the exceptions that mean "these bytes are not what
	/// they claim". Every string is read against the bound its own field actually has rather
	/// than one shared ceiling, so a payload cannot spend a settlement id's budget on a curator's
	/// name and still be inside the cap the family declared.
	/// </summary>
	public static partial class KingdomCuriosityLeadCodec
	{
		/// <summary>Throwing on invalid UTF-8 in both directions is the point: a decoder that
		/// substitutes replacement characters would hand back a receipt that no longer matches
		/// the journal entry it was cut from, and would call that success.</summary>
		internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static BinaryWriter Writer(MemoryStream stream)
		{
			return new BinaryWriter(stream, Utf8, true);
		}

		internal static BinaryReader Reader(byte[] bytes, int bodyEnd)
		{
			return new BinaryReader(new MemoryStream(bytes, 0, bodyEnd, false), Utf8, false);
		}

		/// <summary>Writes the frame's opening four fields. The digest closes it later.</summary>
		internal static void WriteHeader(BinaryWriter w, int magic, int wireVersion,
			long bookRevision, int rowCount)
		{
			w.Write(magic); w.Write(wireVersion); w.Write(bookRevision); w.Write(rowCount);
		}

		/// <summary>Seals a revision 2 or later payload with the digest of everything written.</summary>
		internal static byte[] Seal(MemoryStream stream)
		{
			byte[] body = stream.ToArray();
			byte[] digest = Digest(body, body.Length);
			byte[] sealed_ = new byte[body.Length + DigestBytes];
			Buffer.BlockCopy(body, 0, sealed_, 0, body.Length);
			Buffer.BlockCopy(digest, 0, sealed_, body.Length, DigestBytes);
			return sealed_;
		}

		internal static void PutString(BinaryWriter w, string value)
		{
			if (value == null) throw Bad("a required string was absent");
			byte[] bytes = Utf8.GetBytes(value);
			w.Write(bytes.Length); w.Write(bytes);
		}

		internal static void PutAbsent(BinaryWriter w) { w.Write(-1); }

		/// <summary>Reads one length-prefixed string, held to the exact character bound of the
		/// field it belongs to rather than to a shared maximum.</summary>
		internal static string GetString(BinaryReader r, int maxChars)
		{
			int length = r.ReadInt32();
			if (length < 0 || length > maxChars * MaxUtf8BytesPerChar)
				throw Bad("a string field declares " + length + " bytes");
			byte[] bytes = r.ReadBytes(length);
			if (bytes.Length != length) throw Bad("a string field was cut short");
			return Utf8.GetString(bytes);
		}

		/// <summary>Reads a field that may lawfully be absent. Only the absence marker is
		/// accepted as absence; a zero-length string is a present empty one, and this family
		/// has no field that may be empty.</summary>
		internal static string GetOptionalString(BinaryReader r, int maxChars)
		{
			long at = r.BaseStream.Position;
			int length = r.ReadInt32();
			if (length == -1) return null;
			r.BaseStream.Position = at;
			return GetString(r, maxChars);
		}

		internal static int GetRowCount(BinaryReader r, int maxRows)
		{
			int count = r.ReadInt32();
			if (count < 0 || count > maxRows)
				throw Bad("the row count is " + count + " against a maximum of " + maxRows);
			return count;
		}

		/// <summary>Trailing bytes are a refusal, not a shrug: a payload longer than its own
		/// contents is either a writer that lost count or a reader about to be lied to.</summary>
		internal static void RequireEnd(BinaryReader r)
		{
			if (r.BaseStream.Position != r.BaseStream.Length)
				throw Bad("there are " + (r.BaseStream.Length - r.BaseStream.Position)
					+ " bytes past the end of the rows");
		}

		internal static InvalidDataException Bad(string why)
		{
			return new InvalidDataException(why);
		}

		internal static bool WireFault(Exception e) => e is IOException
			|| e is InvalidDataException || e is EncoderFallbackException
			|| e is DecoderFallbackException || e is ArgumentException
			|| e is OverflowException;
	}
}
