using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		private static bool HasLiquidIntentEvidence(GameObject Owner)
		{
			if (Owner == null) return false;
			string[] names = { "SourceVolumeBefore", "SourceVolumeAfter", "TargetVolumeBefore",
				"TargetVolumeAfter", "TargetCapacity", "SourceComposition",
				"TargetCompositionBefore", "TargetCompositionAfter",
				"TargetCompositionExpected", "LiquidIntentDigest" };
			for (int i = 0; i < names.Length; i++)
				if (Owner.HasIntProperty(HandoverPrefix + names[i])
					|| Owner.HasStringProperty(HandoverPrefix + names[i])) return true;
			return false;
		}

		private static string LiquidIntentDigest(params string[] Terms)
		{
			StringBuilder canonical = new StringBuilder();
			for (int i = 0; i < Terms.Length; i++)
			{
				string term = Terms[i] ?? string.Empty;
				canonical.Append(term.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
					.Append(term);
			}
			using (SHA256 hash = SHA256.Create())
				return Convert.ToBase64String(hash.ComputeHash(
					Encoding.UTF8.GetBytes(canonical.ToString())));
		}

		private static bool RetryLiquid(r_KingdomImprovement Receipt, string Failure)
		{
			Receipt.HandoverFailure = Failure != null && Failure.Length > 2048
				? Failure.Substring(0, 2048) : Failure;
			return false;
		}
	}
}
