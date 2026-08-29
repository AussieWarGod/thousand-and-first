using System;
using System.Text;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomExternalOwnershipRules
	{
		public static string Encode(KingdomExternalOwnershipBinding Value)
		{
			if (!ValidBinding(Value)) return null;
			string[] fields = Value.Mode == KingdomExternalOwnershipMode.None
				? new string[] { Prefix, "0" }
				: new string[]
				{
					Prefix, "1", Value.Observation.ProviderId,
					Value.Observation.ProviderVersion, Value.Observation.OwnerGuid,
					Value.Observation.SectorGuid ?? "", Value.Observation.Evidence,
					Value.Observation.ZoneId, Value.Observation.ParasangId
				};
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < fields.Length; i++)
			{
				if (i > 0) result.Append('.');
				result.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(fields[i])));
			}
			return result.Length <= MaximumEncodedLength ? result.ToString() : null;
		}

		public static bool TryDecode(string Encoded,
			out KingdomExternalOwnershipBinding Value)
		{
			Value = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaximumEncodedLength)
				return false;
			string[] encoded = Encoded.Split('.');
			if (encoded.Length != 2 && encoded.Length != 9) return false;
			string[] fields = new string[encoded.Length];
			try
			{
				for (int i = 0; i < encoded.Length; i++)
				{
					byte[] bytes = Convert.FromBase64String(encoded[i]);
					fields[i] = new UTF8Encoding(false, true).GetString(bytes);
				}
			}
			catch (Exception)
			{
				return false;
			}
			if (fields[0] != Prefix) return false;
			if (fields[1] == "0" && fields.Length == 2)
				Value = None();
			else if (fields[1] == "1" && fields.Length == 9)
				Value = Bind(new KingdomExternalOwnershipObservation
				{
					ProviderId = fields[2], ProviderVersion = fields[3],
					OwnerGuid = fields[4], SectorGuid = fields[5], Evidence = fields[6],
					ZoneId = fields[7], ParasangId = fields[8]
				});
			return ValidBinding(Value) && Encode(Value) == Encoded;
		}
	}
}
