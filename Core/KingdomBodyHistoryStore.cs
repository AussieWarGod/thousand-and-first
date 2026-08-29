using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static class KingdomBodyHistoryStore
	{
		public static KingdomBodyHistoryEnvelope Copy(KingdomBodyHistoryEnvelope Value)
		{
			if (Value == null) return null;
			return new KingdomBodyHistoryEnvelope { RealmId = Value.RealmId,
				IdentityBound = Value.IdentityBound,
				Book = KingdomBodyHistoryCodec.CloneBook(Value.Book),
				OpaqueFutureVersion = Value.OpaqueFutureVersion,
				OpaqueFuturePayload = Clone(Value.OpaqueFuturePayload),
				Quarantined = Value.Quarantined, Fault = Value.Fault };
		}

		public static bool IsAuthorityEmpty(KingdomBodyHistoryEnvelope Value)
		{
			return Value != null && Value.Book != null && Value.Book.Revision == 0L &&
				Value.Book.Rows != null && Value.Book.Rows.Count == 0;
		}

		public static bool TryValidateIdentity(KingdomBodyHistoryEnvelope Value,
			out string Failure)
		{
			Failure = null; string nestedFailure = null;
			if (Value == null || Value.Quarantined || Value.IsOpaqueFuture ||
				Value.OpaqueFutureVersion != 0 || Value.OpaqueFuturePayload != null ||
				!string.IsNullOrEmpty(Value.Fault) ||
				!KingdomBodyHistoryRules.TryValidate(Value.Book, out nestedFailure))
				return Fail(nestedFailure ?? "body history envelope is invalid", out Failure);
			if (!Value.IdentityBound) return Value.RealmId == null && IsAuthorityEmpty(Value)
				|| Fail("unbound body history carries authority", out Failure);
			return ExactRealm(Value.RealmId) || Fail("body history realm is invalid", out Failure);
		}

		public static bool TryBindEmptyIdentity(KingdomBodyHistoryEnvelope Value,
			string ExactRealmId, out string Failure)
		{
			Failure = null;
			if (!TryValidateIdentity(Value, out Failure) || !ExactRealm(ExactRealmId))
				return Fail(Failure ?? "body history realm is invalid", out Failure);
			if (Value.IdentityBound) return string.Equals(Value.RealmId, ExactRealmId,
				StringComparison.Ordinal) || Fail("body history realm mismatch", out Failure);
			Value.RealmId = ExactRealmId; Value.IdentityBound = true; return true;
		}

		public static KingdomBodyHistoryEnvelope ReadForRealm(byte[] Bytes,
			string ExactRealmId, out string Failure)
		{
			KingdomBodyHistoryEnvelope value = ReadOrEmpty(Bytes, out Failure);
			if (Failure != null || value.IsOpaqueFuture) return value;
			if (TryBindEmptyIdentity(value, ExactRealmId, out Failure)) return value;
			value.Quarantined = true; value.Fault = Failure; return value;
		}

		public static KingdomBodyHistoryEnvelope ReadOrEmpty(byte[] Bytes,
			out string Failure)
		{
			Failure = null;
			if (Bytes == null || Bytes.Length == 0) return new KingdomBodyHistoryEnvelope();
			try
			{
				KingdomBodyHistoryEnvelope value =
					KingdomBodyHistoryCodec.Decode(Bytes);
				if (!value.IsOpaqueFuture && !value.IdentityBound && !IsAuthorityEmpty(value))
				{
					Failure = "Unbound legacy body history carries authority and requires quarantine.";
					value.Quarantined = true; value.Fault = Failure;
				}
				return value;
			}
			catch (Exception error) when (error is InvalidDataException
				|| error is ArgumentException || error is NotSupportedException)
			{
				Failure = "Body history is unreadable: " + error.Message;
				return new KingdomBodyHistoryEnvelope
				{
					Quarantined = true,
					Fault = Failure
				};
			}
		}

		public static bool TryWrite(KingdomBodyHistoryEnvelope Value,
			out byte[] Bytes, out string Failure)
		{
			Bytes = null;
			Failure = null;
			try
			{
				Bytes = KingdomBodyHistoryCodec.Encode(Value);
				return true;
			}
			catch (Exception error) when (error is InvalidDataException
				|| error is ArgumentException || error is NotSupportedException)
			{
				Failure = error.Message;
				return false;
			}
		}

		internal static void WriteIdentity(BinaryWriter Writer,
			KingdomBodyHistoryEnvelope Value)
		{
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value.RealmId);
			if (bytes.Length > KingdomBodyHistoryCodec.MaxRealmIdBytes)
				throw new InvalidDataException("body history realm exceeds its cap");
			Writer.Write(bytes.Length); Writer.Write(bytes); Writer.Write(Value.IdentityBound);
		}

		internal static void ReadIdentity(BinaryReader Reader,
			KingdomBodyHistoryEnvelope Value)
		{
			int length = Reader.ReadInt32();
			if (length < 0 || length > KingdomBodyHistoryCodec.MaxRealmIdBytes)
				throw new InvalidDataException("body history realm exceeds its cap");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			Value.RealmId = new UTF8Encoding(false, true).GetString(bytes);
			byte bound = Reader.ReadByte();
			if (bound > 1) throw new InvalidDataException("invalid body history identity flag");
			Value.IdentityBound = bound == 1;
		}

		private static bool ExactRealm(string Value)
		{
			try { return KingdomIdentityRules.IsRealmId(Value) &&
				new UTF8Encoding(false, true).GetByteCount(Value) <=
				KingdomBodyHistoryCodec.MaxRealmIdBytes; }
			catch (EncoderFallbackException) { return false; }
		}
		private static byte[] Clone(byte[] Value) { return Value == null ? null : (byte[])Value.Clone(); }
		private static bool Fail(string Text, out string Failure) { Failure = Text; return false; }
	}
}
