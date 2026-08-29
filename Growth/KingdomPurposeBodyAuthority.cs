using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Frozen selection of one existing body service inside a purpose operation.</summary>
	public sealed class KingdomPurposeBodyAuthority
	{
		public const int Schema = 1;
		public KingdomPurposeKind Kind;
		public string PairId;
		public long PairEpoch;
		public string OperationId;
		public string AuthorityId;
		public string SubjectObjectId;
		public string SubjectGeneId;
		public string ProcedureKey;
		public int BodyPartId;
		public string BearerId;
		public int WaterCost;
		public string BitCost;
		public int PreservedCost;
	}

	/// <summary>Engine-free, canonical codec for the body service chosen before any debit.</summary>
	public static class KingdomPurposeBodyAuthorityRules
	{
		private const int FieldCount = 15;
		private const int MaxChars = 4096;

		public static bool Valid(KingdomPurposeBodyAuthority Value)
		{
			if (Value == null || !Id(Value.PairId) || Value.PairEpoch < 1L
				|| !Id(Value.OperationId) || !Id(Value.AuthorityId)
				|| !Id(Value.SubjectObjectId) || !Id(Value.ProcedureKey)
				|| Value.WaterCost < 0 || Value.PreservedCost < 0
				|| !KingdomMaterialDebitCost.TryParseClaim(Value.BitCost,
					out KingdomMaterialDebitCost bits)
				|| bits.ToClaimString() != Value.BitCost) return false;
			if (Value.Kind == KingdomPurposeKind.Flesh)
				return Value.BodyPartId > 0 && Id(Value.BearerId)
					&& string.IsNullOrEmpty(Value.SubjectGeneId);
			if (Value.Kind == KingdomPurposeKind.Chrome)
				return Value.ProcedureKey == "annexe-enrolment"
					&& Value.WaterCost > 0 && Value.PreservedCost == 0 && bits.IsEmpty
					&& Value.BodyPartId == 0 && string.IsNullOrEmpty(Value.BearerId)
					&& Id(Value.SubjectGeneId);
			return false;
		}

		public static string Encode(KingdomPurposeBodyAuthority Value)
		{
			if (!Valid(Value)) return null;
			return EncodeFields(new string[]
			{
				"1", ((int)Value.Kind).ToString(CultureInfo.InvariantCulture), Value.PairId,
				Value.PairEpoch.ToString(CultureInfo.InvariantCulture), Value.OperationId,
				Value.AuthorityId, Value.SubjectObjectId, Value.SubjectGeneId,
				Value.ProcedureKey, Value.BodyPartId.ToString(CultureInfo.InvariantCulture),
				Value.BearerId, Value.WaterCost.ToString(CultureInfo.InvariantCulture),
				Value.BitCost, Value.PreservedCost.ToString(CultureInfo.InvariantCulture),
				"purpose-body-authority"
			});
		}

		public static bool TryDecode(string Text, out KingdomPurposeBodyAuthority Value)
		{
			Value = null;
			if (!TryDecodeFields(Text, out string[] f) || f[0] != "1"
				|| f[14] != "purpose-body-authority"
				|| !int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture,
					out int kind)
				|| !long.TryParse(f[3], NumberStyles.None, CultureInfo.InvariantCulture,
					out long epoch)
				|| !int.TryParse(f[9], NumberStyles.None, CultureInfo.InvariantCulture,
					out int bodyPart)
				|| !int.TryParse(f[11], NumberStyles.None, CultureInfo.InvariantCulture,
					out int water)
				|| !int.TryParse(f[13], NumberStyles.None, CultureInfo.InvariantCulture,
					out int preserved)) return false;
			Value = new KingdomPurposeBodyAuthority
			{
				Kind = (KingdomPurposeKind)kind, PairId = f[2], PairEpoch = epoch,
				OperationId = f[4], AuthorityId = f[5], SubjectObjectId = f[6],
				SubjectGeneId = f[7], ProcedureKey = f[8], BodyPartId = bodyPart,
				BearerId = f[10], WaterCost = water, BitCost = f[12],
				PreservedCost = preserved
			};
			return Valid(Value) && Encode(Value) == Text;
		}

		private static string EncodeFields(IList<string> Fields)
		{
			StringBuilder text = new StringBuilder("pb1");
			for (int i = 0; i < Fields.Count; i++)
			{
				string value = Fields[i] ?? "";
				text.Append(';').Append(value.Length.ToString(CultureInfo.InvariantCulture))
					.Append(':').Append(value);
				if (text.Length > MaxChars) return null;
			}
			return text.ToString();
		}

		private static bool TryDecodeFields(string Text, out string[] Fields)
		{
			Fields = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxChars
				|| !Text.StartsWith("pb1", StringComparison.Ordinal)) return false;
			string[] values = new string[FieldCount];
			int at = 3;
			for (int i = 0; i < values.Length; i++)
			{
				if (at >= Text.Length || Text[at++] != ';') return false;
				int colon = Text.IndexOf(':', at);
				if (colon < at || colon - at > 8
					|| !int.TryParse(Text.Substring(at, colon - at), NumberStyles.None,
						CultureInfo.InvariantCulture, out int length)
					|| length < 0 || colon + 1 + length > Text.Length) return false;
				values[i] = Text.Substring(colon + 1, length);
				at = colon + 1 + length;
			}
			if (at != Text.Length) return false;
			Fields = values;
			return true;
		}

		private static bool Id(string Value)
		{
			return Value != null && Value.Length > 0 && Value.Length <= 256
				&& Value.Trim() == Value && Value.IndexOf('\0') < 0;
		}
	}
}
