using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		private static int CompareCanonical(KingdomConstructionJob A, KingdomConstructionJob B)
		{
			int compare = string.CompareOrdinal(A.OwnerKey, B.OwnerKey);
			if (compare != 0) return compare;
			compare = string.CompareOrdinal(A.ZoneId, B.ZoneId);
			if (compare != 0) return compare;
			compare = A.CreatedTick.CompareTo(B.CreatedTick);
			return compare != 0 ? compare : string.CompareOrdinal(A.Id, B.Id);
		}

		private static int CompareNewest(KingdomConstructionJob A, KingdomConstructionJob B)
		{
			int compare = B.UpdatedTick.CompareTo(A.UpdatedTick);
			return compare != 0 ? compare : string.CompareOrdinal(B.Id, A.Id);
		}

		private static KingdomConstructionJob Compact(KingdomConstructionJob Job)
		{
			KingdomConstructionJob compact = Job.Copy();
			compact.Payload = null;
			compact.PhysicalReceipt = null;
			compact.InputReceipt = null;
			compact.Failure = null;
			compact.Outbox = null;
			compact.Compacted = true;
			compact.CompactHash = CompactIdentityHash(compact);
			return compact;
		}

		private static string CompactIdentityHash(KingdomConstructionJob Job)
		{
			if (Job == null || Job.Claims == null) return null;
			bool buildTruth = Job.BuildTruthSchema == BuildTruthSchema;
			bool routedInput = !string.IsNullOrEmpty(Job.InputReceiptHash);
			StringBuilder text = new StringBuilder(routedInput
				? "TAF-CONSTRUCTION-PROOF-3"
				: (buildTruth ? "TAF-CONSTRUCTION-PROOF-2" : "TAF-CONSTRUCTION-PROOF-1"));
			text.Append('|').Append(Job.Id)
				.Append('|').Append(EncodeText(Job.OwnerKey)).Append('|').Append(EncodeText(Job.ZoneId))
				.Append('|').Append((int)Job.Route).Append('|').Append((int)Job.Phase)
				.Append('|').Append((int)Job.Projection).Append('|').Append(Job.X).Append('|').Append(Job.Y)
				.Append('|').Append(EncodeText(Job.SubjectId)).Append('|').Append(EncodeText(Job.SourceId))
				.Append('|').Append(EncodeText(Job.OutputId)).Append('|').Append((int)Job.PhysicalPhase)
				.Append('|').Append(Job.PhysicalIndex).Append('|').Append(Job.PhysicalAmount)
				.Append('|').Append(Job.PhysicalSpilled).Append('|').Append(EncodeText(Job.PhysicalItemId))
				.Append('|').Append(EncodeText(Job.PhysicalDestinationId)).Append('|').Append(EncodeText(Job.TargetKey))
				.Append('|').Append(Job.CreatedTick).Append('|').Append(Job.StartedTick)
				.Append('|').Append(Job.DueTick).Append('|').Append(Job.UpdatedTick).Append('|').Append(Job.Revision)
				.Append('|').Append(Job.Claims.WaterRequested).Append('|').Append(Job.Claims.WaterSpent)
				.Append('|').Append(Job.Claims.WaterOutstanding).Append('|').Append(Job.Claims.WaterLost)
				.Append('|').Append(Job.Claims.Exact ? '1' : '0')
				.Append('|').Append(EncodeText(Job.Claims.MaterialRequested))
				.Append('|').Append(EncodeText(Job.Claims.MaterialSpent))
				.Append('|').Append(EncodeText(Job.Claims.MaterialOutstanding))
				.Append('|').Append(EncodeText(Job.Claims.MaterialLost));
			if (buildTruth)
				text.Append('|').Append(Job.BuildTruthSchema)
					.Append('|').Append(Job.BuildHasPlot ? '1' : '0')
					.Append('|').Append(Job.BuildFrontier ? '1' : '0')
					.Append('|').Append(Job.BuildDefence);
			if (routedInput) text.Append('|').Append(Job.InputReceiptHash);
			return Sha256(text.ToString());
		}

		private static string Sha256(string Text)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Text ?? ""));
				StringBuilder encoded = new StringBuilder(64);
				for (int i = 0; i < hash.Length; i++)
					encoded.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
				return encoded.ToString();
			}
		}

		private static bool IsSha256(string Text)
		{
			if (Text == null || Text.Length != 64) return false;
			for (int i = 0; i < Text.Length; i++)
				if ((Text[i] < '0' || Text[i] > '9') && (Text[i] < 'a' || Text[i] > 'f'))
					return false;
			return true;
		}

		private static bool TextLength(string Text, int Min, int Max)
		{
			int length = Text == null ? 0 : Text.Length;
			return length >= Min && length <= Max;
		}

		private static string Limit(string Text, int Max)
		{
			if (Text == null || Text.Length <= Max) return Text;
			return Text.Substring(0, Max);
		}

		private static string EncodeText(string Text)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? ""));
		}

		private static bool TryDecodeText(string Encoded, int Max, out string Text)
		{
			Text = null;
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded);
				string decoded = Encoding.UTF8.GetString(bytes);
				if (decoded.Length > Max || EncodeText(decoded) != Encoded)
				{
					return false;
				}
				Text = decoded.Length == 0 ? null : decoded;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryInt(string Text, int Min, int Max, out int Value)
		{
			return int.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out Value)
				&& Value >= Min && Value <= Max && Value.ToString(CultureInfo.InvariantCulture) == Text;
		}

		private static bool TryLong(string Text, out long Value)
		{
			return long.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value >= 0L && Value.ToString(CultureInfo.InvariantCulture) == Text;
		}
	}
}
