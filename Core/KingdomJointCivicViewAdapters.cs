using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Typed, read-only adapters for D9's four independent owners.</summary>
	public static class KingdomJointCivicViewAdapters
	{
		public static KingdomJointCivicOwnerView CreedDeclaration(string RealmId,
			long RealmFoundedTick, string DeclaredCreed, string NativeReport)
		{
			if (string.IsNullOrEmpty(DeclaredCreed))
				return Missing("creed", "No realm creed declaration is recorded.");
			string receipt = StableId("taf:creed-declaration:v1:",
				"TAF-CREED-DECLARATION-V1", RealmId,
				RealmFoundedTick.ToString(CultureInfo.InvariantCulture), DeclaredCreed);
			if (RealmFoundedTick < 0 || !SemanticOwner(RealmId)
				|| string.IsNullOrEmpty(receipt) || !KingdomJointCivicViewRules.Report(NativeReport))
				return Invalid("creed", "The creed declaration owner or native report is invalid.");
			return Valid("creed", 1, receipt, NativeReport);
		}

		/// <summary>
		/// The answer for a save that carries no covenant archive at all.
		/// <para>
		/// This was once the only answer this owner had, because nothing durable survived a
		/// village charter and the alternative &mdash; reading today's standing and calling it a
		/// covenant &mdash; would have been an inference rather than a record. It is now one answer
		/// among several: <see cref="KingdomVillageCovenantView"/> returns it for an older save
		/// whose civic memory predates the archive, and returns something more specific whenever
		/// the archive is actually there. The reasoning has not changed. Standing is a projection
		/// and never authority, so a save with no archive reports explicit absence rather than an
		/// empty archive pretending to be a completed lookup.
		/// </para>
		/// </summary>
		public static KingdomJointCivicOwnerView CovenantMissing()
		{
			return Missing("covenant",
				"No durable exact village-covenant owner exists in this save.");
		}

		public static KingdomJointCivicOwnerView Moot(KingdomAssentingMootReceipt Receipt,
			string NativeReport)
		{
			if (Receipt == null || Receipt.Phase == KingdomAssentingMootPhase.None)
				return Missing("moot", "No assenting moot is recorded.");
			KingdomAssentingMootReceipt copy = Receipt.Copy();
			if (!KingdomAssentingMootRules.Validate(copy, out string failure))
				return Invalid("moot", failure);
			string receipt = StableId("taf:assenting-moot-view:v1:",
				"TAF-ASSENTING-MOOT-VIEW-V1", copy.AuthorityId,
				copy.MembershipFingerprint,
				((int)copy.Phase).ToString(CultureInfo.InvariantCulture),
				copy.Strength.ToString(CultureInfo.InvariantCulture),
				copy.PreparedTick.ToString(CultureInfo.InvariantCulture),
				copy.AppliedTick.ToString(CultureInfo.InvariantCulture),
				copy.SuspendedTick.ToString(CultureInfo.InvariantCulture));
			if (copy.Phase == KingdomAssentingMootPhase.Quarantined
				|| string.IsNullOrEmpty(receipt)
				|| !KingdomJointCivicViewRules.Report(NativeReport))
				return Invalid("moot", string.IsNullOrEmpty(failure)
					? "The assenting-moot authority or native report is invalid." : failure);
			return Valid("moot", copy.Version, receipt, NativeReport);
		}

		public static KingdomJointCivicOwnerView Enclave(
			KingdomHostedArcologyAuthority Authority, string NativeReport)
		{
			if (Authority == null)
				return Missing("enclave", "No hosted enclave is recorded.");
			string receipt = StableId("taf:hosted-enclave:v1:",
				"TAF-HOSTED-ENCLAVE-AUTHORITY-V1",
				Authority.Version.ToString(CultureInfo.InvariantCulture),
				((int)Authority.Phase).ToString(CultureInfo.InvariantCulture),
				Authority.RealmId, Authority.SettlementId, Authority.ZoneId,
				Authority.CarrierId, Authority.ConstructionJobId);
			if (!Authority.Valid() || Authority.Phase == KingdomHostedAuthorityPhase.Quarantined
				|| string.IsNullOrEmpty(receipt)
				|| !KingdomJointCivicViewRules.Report(NativeReport))
				return Invalid("enclave", string.IsNullOrEmpty(Authority.Fault)
					? "The hosted-enclave authority or native report is invalid."
					: Authority.Fault);
			return Valid("enclave", Authority.Version, receipt, NativeReport);
		}

		internal static KingdomJointCivicOwnerView Missing(string Key, string Failure)
		{
			return new KingdomJointCivicOwnerView
			{
				OwnerKey = Key,
				State = KingdomJointOwnerState.Absent,
				Failure = BoundedFailure(Failure)
			};
		}

		internal static KingdomJointCivicOwnerView Invalid(string Key, string Failure)
		{
			return new KingdomJointCivicOwnerView
			{
				OwnerKey = Key,
				State = KingdomJointOwnerState.Invalid,
				Failure = BoundedFailure(Failure)
			};
		}

		private static KingdomJointCivicOwnerView Valid(string Key, int Version,
			string Receipt, string Text)
		{
			return new KingdomJointCivicOwnerView
			{
				OwnerKey = Key,
				State = KingdomJointOwnerState.Valid,
				SourceVersion = Version,
				SourceReceiptId = Receipt,
				Text = Text
			};
		}

		private static string StableId(string Prefix, params string[] Fields)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					for (int i = 0; i < Fields.Length; i++)
					{
						if (!OwnerField(Fields[i])) return null;
						writer.Write(Fields[i]);
					}
					writer.Flush();
					using (SHA256 sha = SHA256.Create())
					{
						byte[] bytes = sha.ComputeHash(stream.ToArray());
						StringBuilder result = new StringBuilder(Prefix);
						for (int i = 0; i < bytes.Length; i++)
							result.Append(bytes[i].ToString("x2",
								CultureInfo.InvariantCulture));
						return result.ToString();
					}
				}
			}
			catch (EncoderFallbackException) { return null; }
		}

		private static bool SemanticOwner(string Value)
		{
			return Value != null && Value.StartsWith("taf:", StringComparison.Ordinal)
				&& OwnerField(Value);
		}

		private static bool OwnerField(string Value)
		{
			try
			{
				return !string.IsNullOrEmpty(Value) && Value.IndexOf('\0') < 0
					&& new UTF8Encoding(false, true).GetByteCount(Value) <= 512;
			}
			catch (EncoderFallbackException) { return false; }
		}

		private static string BoundedFailure(string Failure)
		{
			string value = string.IsNullOrWhiteSpace(Failure)
				? "The owning record is invalid." : Failure;
			if (KingdomJointCivicViewRules.FailureText(value)) return value;
			return "The owning record has an unbounded failure.";
		}
	}
}
