using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static class KingdomCivicPracticeStore
	{
		public static KingdomCivicPracticeEnvelope Copy(KingdomCivicPracticeEnvelope value)
		{
			if (value == null) return null;
			return new KingdomCivicPracticeEnvelope { RealmId = value.RealmId,
				IdentityBound = value.IdentityBound,
				SitePractices = KingdomCivicPracticeCodec.CloneSites(value.SitePractices),
				VocationServices = KingdomCivicPracticeCodec.CloneServices(value.VocationServices),
				OpaqueFutureVersion = value.OpaqueFutureVersion,
				OpaqueFuturePayload = Clone(value.OpaqueFuturePayload),
				Quarantined = value.Quarantined, Fault = value.Fault };
		}

		public static bool IsAuthorityEmpty(KingdomCivicPracticeEnvelope value)
		{
			return value != null && value.SitePractices != null && value.VocationServices != null &&
				value.SitePractices.Revision == 0L && value.SitePractices.Rows != null &&
				value.SitePractices.Rows.Count == 0 && value.VocationServices.Revision == 0L &&
				value.VocationServices.Rows != null && value.VocationServices.Rows.Count == 0;
		}

		public static bool TryValidateIdentity(KingdomCivicPracticeEnvelope value,
			out string failure)
		{
			failure = null; string nestedFailure = null;
			if (value == null || value.Quarantined || value.IsOpaqueFuture ||
				value.OpaqueFutureVersion != 0 || value.OpaqueFuturePayload != null ||
				!string.IsNullOrEmpty(value.Fault) ||
				!KingdomSitePracticeRules.TryValidate(value.SitePractices, out nestedFailure) ||
				!KingdomVocationServiceRules.TryValidate(value.VocationServices, out nestedFailure))
				return Fail(nestedFailure ?? "civic practice envelope is invalid", out failure);
			if (!value.IdentityBound) return value.RealmId == null && IsAuthorityEmpty(value)
				|| Fail("unbound civic practice carries authority", out failure);
			return ExactRealm(value.RealmId) || Fail("civic practice realm is invalid", out failure);
		}

		public static bool TryBindEmptyIdentity(KingdomCivicPracticeEnvelope value,
			string exactRealmId, out string failure)
		{
			failure = null;
			if (!TryValidateIdentity(value, out failure) || !ExactRealm(exactRealmId))
				return Fail(failure ?? "civic practice realm is invalid", out failure);
			if (value.IdentityBound) return string.Equals(value.RealmId, exactRealmId,
				StringComparison.Ordinal) || Fail("civic practice realm mismatch", out failure);
			value.RealmId = exactRealmId; value.IdentityBound = true; return true;
		}

		public static KingdomCivicPracticeEnvelope ReadForRealm(byte[] stored,
			string exactRealmId, out string failure)
		{
			KingdomCivicPracticeEnvelope value = ReadOrEmpty(stored, out failure);
			if (failure != null || value.IsOpaqueFuture) return value;
			if (TryBindEmptyIdentity(value, exactRealmId, out failure)) return value;
			value.Quarantined = true; value.Fault = failure; return value;
		}

		public static KingdomCivicPracticeEnvelope ReadOrEmpty(byte[] stored,
			out string failure)
		{
			failure = null;
			if (stored == null || stored.Length == 0) return new KingdomCivicPracticeEnvelope();
			try
			{
				KingdomCivicPracticeEnvelope value =
					KingdomCivicPracticeCodec.Decode(stored);
				if (!value.IsOpaqueFuture && !value.IdentityBound && !IsAuthorityEmpty(value))
				{
					failure = "Unbound legacy civic practice carries authority and requires quarantine.";
					value.Quarantined = true; value.Fault = failure;
				}
				return value;
			}
			catch (Exception error) when (WireFault(error))
			{
				failure = "Civic practice authority is unreadable: " + error.Message;
				return new KingdomCivicPracticeEnvelope { Quarantined = true, Fault = failure };
			}
		}

		public static bool TryWrite(KingdomCivicPracticeEnvelope value,
			out byte[] stored, out string failure)
		{
			stored = null;
			failure = null;
			try
			{
				stored = KingdomCivicPracticeCodec.Encode(value);
				return true;
			}
			catch (Exception error) when (WireFault(error))
			{
				failure = "Civic practice authority cannot be saved: " + error.Message;
				return false;
			}
		}

		private static bool WireFault(Exception error)
		{
			return error is InvalidDataException || error is ArgumentException ||
				error is NotSupportedException;
		}
		private static bool ExactRealm(string value)
		{
			try { return KingdomIdentityRules.IsRealmId(value) &&
				new UTF8Encoding(false, true).GetByteCount(value) <=
				KingdomCivicPracticeCodec.MaxRealmIdBytes; }
			catch (EncoderFallbackException) { return false; }
		}
		private static byte[] Clone(byte[] value) { return value == null ? null : (byte[])value.Clone(); }
		private static bool Fail(string text, out string failure) { failure = text; return false; }
	}
}
