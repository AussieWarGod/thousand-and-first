using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposeRules
	{

		public static string EncodeCommitment(KingdomPurposeCommitment Commitment)
		{
			if (!ValidCommitment(Commitment)) return null;
			return Encode(new string[CommitmentFieldCount]
			{
				Commitment.Manifest, Commitment.ConsignmentId, Commitment.CargoItemId,
				Commitment.SiteProof, Encode(new string[2]
					{ Commitment.SpecialistId, Commitment.SpecialistName })
			});
		}

		public static bool TryDecodeCommitment(string Receipt,
			out KingdomPurposeCommitment Commitment)
		{
			Commitment = null;
			if (!TryDecode(Receipt, CommitmentFieldCount, out string[] f)
				|| !TryDecode(f[4], 2, out string[] specialist)) return false;
			Commitment = new KingdomPurposeCommitment
			{
				Manifest = f[0], ConsignmentId = f[1], CargoItemId = f[2],
				SiteProof = f[3], SpecialistId = specialist[0], SpecialistName = specialist[1]
			};
			return ValidCommitment(Commitment) && EncodeCommitment(Commitment) == Receipt;
		}

		public static bool ValidCommitment(KingdomPurposeCommitment C)
		{
			return C != null && TryDecodeManifest(C.Manifest, out _)
				&& Identity(C.ConsignmentId) && Identity(C.CargoItemId)
				&& Text(C.SiteProof, 1, 720) && Identity(C.SpecialistId)
				&& Text(C.SpecialistName, 1, 180);
		}

		public static string PurposeName(KingdomPurposeKind Kind)
		{
			return Kind == KingdomPurposeKind.Flesh ? "the flesh-city"
				: Kind == KingdomPurposeKind.Chrome ? "the chrome-city" : "no purpose";
		}

		private static bool TryKind(string Raw, out KingdomPurposeKind Kind)
		{
			string value = (Raw ?? "").Trim().ToLowerInvariant();
			Kind = value == "flesh" ? KingdomPurposeKind.Flesh
				: value == "chrome" ? KingdomPurposeKind.Chrome : KingdomPurposeKind.None;
			return Kind != KingdomPurposeKind.None;
		}

		private static bool TrySite(string Raw, out KingdomPurposeSite Site)
		{
			string value = (Raw ?? "").Trim().ToLowerInvariant();
			Site = value == "living-surgery" ? KingdomPurposeSite.LivingSurgery
				: value == "ruin-enrollment" ? KingdomPurposeSite.RuinEnrollment
				: KingdomPurposeSite.None;
			return Site != KingdomPurposeSite.None;
		}

		private static string Encode(IList<string> Fields)
		{
			StringBuilder text = new StringBuilder("v1");
			for (int i = 0; i < Fields.Count; i++)
			{
				string value = Fields[i] ?? "";
				text.Append(';').Append(value.Length.ToString(CultureInfo.InvariantCulture))
					.Append(':').Append(value);
				if (text.Length > MaxReceiptChars) return null;
			}
			return text.ToString();
		}

		private static bool TryDecode(string Text, int Count, out string[] Fields)
		{
			Fields = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxReceiptChars
				|| !Text.StartsWith("v1", StringComparison.Ordinal)) return false;
			string[] values = new string[Count];
			int at = 2;
			for (int i = 0; i < Count; i++)
			{
				if (at >= Text.Length || Text[at++] != ';') return false;
				int colon = Text.IndexOf(':', at);
				if (colon < at || colon - at > 8
					|| !int.TryParse(Text.Substring(at, colon - at), NumberStyles.None,
						CultureInfo.InvariantCulture, out int length)
					|| length < 0 || length > MaxReceiptChars || colon + 1 + length > Text.Length)
					return false;
				values[i] = Text.Substring(colon + 1, length);
				at = colon + 1 + length;
			}
			if (at != Text.Length) return false;
			Fields = values;
			return true;
		}

		private static bool Token(string Value, int Max)
		{
			if (!Text(Value, 1, Max)) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ':' || c == '.'))
					return false;
			}
			return true;
		}

		private static bool Identity(string Value)
		{
			return Text(Value, 1, 256) && Value.Trim() == Value;
		}

		private static bool Text(string Value, int Min, int Max)
		{
			return Value != null && Value.Length >= Min && Value.Length <= Max
				&& Value.IndexOf('\0') < 0;
		}

		private static bool Fail(string Message, out string Error)
		{
			Error = Message;
			return false;
		}
	}
}
