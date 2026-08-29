using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Narrow named-field seam. A missing additive field is exactly an empty authority.</summary>
	public static class KingdomCivicArtifactsStore
	{
		public static KingdomCivicArtifactsEnvelope Copy(KingdomCivicArtifactsEnvelope Value)
		{
			if (Value == null) return null;
			return new KingdomCivicArtifactsEnvelope { RealmId = Value.RealmId,
				IdentityBound = Value.IdentityBound, WitnessWorks = Value.WitnessWorks == null
					? null : KingdomWitnessWorkCodec.Decode(KingdomWitnessWorkCodec.Encode(Value.WitnessWorks)),
				Recognitions = Value.Recognitions == null ? null : KingdomArtifactRecognitionCodec.Decode(
					KingdomArtifactRecognitionCodec.Encode(Value.Recognitions)),
				OpaqueFutureVersion = Value.OpaqueFutureVersion,
				OpaqueFuturePayload = Clone(Value.OpaqueFuturePayload), Quarantined = Value.Quarantined,
				Fault = Value.Fault };
		}

		public static bool IsAuthorityEmpty(KingdomCivicArtifactsEnvelope Value)
		{
			return Value != null && Value.WitnessWorks != null && Value.Recognitions != null &&
				Value.WitnessWorks.Revision == 0L && Value.WitnessWorks.Rows != null &&
				Value.WitnessWorks.Rows.Count == 0 && Value.Recognitions.Revision == 0L &&
				Value.Recognitions.Rows != null && Value.Recognitions.Rows.Count == 0;
		}

		public static bool TryValidateIdentity(KingdomCivicArtifactsEnvelope Value,
			out string Failure)
		{
			Failure = null;
			string nestedFailure = null;
			if (Value == null || Value.Quarantined || Value.IsOpaqueFuture ||
				Value.OpaqueFutureVersion != 0 || Value.OpaqueFuturePayload != null ||
				!string.IsNullOrEmpty(Value.Fault) ||
				!KingdomWitnessWorkRules.TryValidate(Value.WitnessWorks, out nestedFailure) ||
				!KingdomArtifactRecognitionRules.TryValidate(Value.Recognitions, out nestedFailure))
				return Fail(nestedFailure ?? "civic artifact envelope is invalid", out Failure);
			if (!Value.IdentityBound) return Value.RealmId == null && IsAuthorityEmpty(Value)
				|| Fail("unbound civic artifacts carry authority", out Failure);
			return ExactRealm(Value.RealmId) || Fail("civic artifact realm is invalid", out Failure);
		}

		public static bool TryBindEmptyIdentity(KingdomCivicArtifactsEnvelope Value,
			string ExactRealmId, out string Failure)
		{
			Failure = null;
			if (!TryValidateIdentity(Value, out Failure) || !ExactRealm(ExactRealmId))
				return Fail(Failure ?? "civic artifact realm is invalid", out Failure);
			if (Value.IdentityBound) return string.Equals(Value.RealmId, ExactRealmId,
				StringComparison.Ordinal) || Fail("civic artifact realm mismatch", out Failure);
			Value.RealmId = ExactRealmId; Value.IdentityBound = true; return true;
		}

		public static KingdomCivicArtifactsEnvelope ReadForRealm(byte[] Stored,
			string ExactRealmId, out string Failure)
		{
			KingdomCivicArtifactsEnvelope value = ReadOrEmpty(Stored, out Failure);
			if (Failure != null || value.IsOpaqueFuture) return value;
			if (TryBindEmptyIdentity(value, ExactRealmId, out Failure)) return value;
			value.Quarantined = true; value.Fault = Failure; return value;
		}

		public static KingdomCivicArtifactsEnvelope ReadOrEmpty(byte[] Stored,
			out string Failure)
		{
			Failure = null;
			if (Stored == null || Stored.Length == 0) return new KingdomCivicArtifactsEnvelope();
			try
			{
				KingdomCivicArtifactsEnvelope value =
					KingdomCivicArtifactsCodec.Decode(Stored);
				if (!value.IsOpaqueFuture && !value.IdentityBound && !IsAuthorityEmpty(value))
				{
					Failure = "Unbound legacy civic artifacts carry authority and require quarantine.";
					value.Quarantined = true; value.Fault = Failure;
				}
				return value;
			}
			catch (Exception e) when (e is InvalidDataException || e is ArgumentException ||
				e is NotSupportedException)
			{
				Failure = "Civic artifact authority is unreadable: " + e.Message;
				return new KingdomCivicArtifactsEnvelope { Quarantined = true, Fault = Failure };
			}
		}

		public static bool TryWrite(KingdomCivicArtifactsEnvelope Value,
			out byte[] Stored, out string Failure)
		{
			Stored = null; Failure = null;
			try { Stored = KingdomCivicArtifactsCodec.Encode(Value); return true; }
			catch (Exception e) when (e is InvalidDataException || e is ArgumentException ||
				e is NotSupportedException)
			{
				Failure = "Civic artifact authority cannot be saved: " + e.Message; return false;
			}
		}

		private static bool ExactRealm(string Value)
		{
			try { return KingdomIdentityRules.IsRealmId(Value) &&
				new UTF8Encoding(false, true).GetByteCount(Value) <=
				KingdomCivicArtifactsCodec.MaxRealmIdBytes; }
			catch (EncoderFallbackException) { return false; }
		}
		private static byte[] Clone(byte[] Value) { return Value == null ? null : (byte[])Value.Clone(); }
		private static bool Fail(string Text, out string Failure) { Failure = Text; return false; }
	}
}
