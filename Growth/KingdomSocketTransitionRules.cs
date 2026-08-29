using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure validation for the mergeable socket-transition schema.</summary>
	public static class KingdomSocketTransitionRules
	{
		public const int Schema = 1;
		public const int MaxTransitions = 256;
		public const int MaxKeyChars = 128;
		public const long MaxWorkTicks = 100000000L;
		public const int LegacyReceiptSchema = 1;
		public const int ReceiptSchema = 2;

		public static bool TryParse(string Key, string From, string To, string Type,
			string Size, string Water, string Materials, string Ticks,
			out KingdomSocketTransition Transition, out string Failure)
		{
			Transition = null;
			Failure = null;
			int water;
			long ticks;
			ArchitectureLotSize size;
			KingdomMaterialTally materials;
			string materialFailure;
			string type = Fold(Type);
			if (!ValidKey(Key) || !ValidKey(From) || !ValidKey(To) || From == To
				|| !ValidKey(type) || !TrySize(Size, out size)
				|| !int.TryParse(Water, NumberStyles.None, CultureInfo.InvariantCulture, out water)
				|| water < 0
				|| !long.TryParse(Ticks, NumberStyles.None, CultureInfo.InvariantCulture, out ticks)
				|| ticks < 1L || ticks > MaxWorkTicks
				|| !KingdomMaterialRules.TryParseMaterialCost(Materials, out materials,
					out materialFailure))
			{
				Failure = "transition " + (Key ?? "<unnamed>")
					+ " has malformed identity, typed lot, water, materials, or work";
				return false;
			}
			Transition = new KingdomSocketTransition(Key, From, To, type, size, water,
				materials, ticks);
			return true;
		}

		/// <summary>Returns a detached, deep snapshot only for a complete declaration.</summary>
		public static bool TrySnapshot(KingdomSocketTransition Source,
			out KingdomSocketTransition Snapshot)
		{
			Snapshot = null;
			if (!ValidDeclaration(Source)) return false;
			Snapshot = new KingdomSocketTransition(Source.Key, Source.FromBuildKey,
				Source.ToBuildKey, Source.LotType, Source.LotSize, Source.WaterDrams,
				Source.Materials, Source.WorkTicks);
			return true;
		}

		/// <summary>Exact declaration equality, including key and all priced work.</summary>
		public static bool SameDeclaration(KingdomSocketTransition Left,
			KingdomSocketTransition Right)
		{
			if (!ValidDeclaration(Left) || !ValidDeclaration(Right)
				|| Left.Key != Right.Key || Left.FromBuildKey != Right.FromBuildKey
				|| Left.ToBuildKey != Right.ToBuildKey || Left.LotType != Right.LotType
				|| Left.LotSize != Right.LotSize || Left.WaterDrams != Right.WaterDrams
				|| Left.WorkTicks != Right.WorkTicks) return false;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				if (Left.MaterialUnits((KingdomMaterial)i)
					!= Right.MaterialUnits((KingdomMaterial)i)) return false;
			return true;
		}

		/// <summary>Canonical digest binds key, route, typed lot, water, materials, and ticks.</summary>
		public static bool TryDeclarationDigest(KingdomSocketTransition Declaration,
			out string Digest)
		{
			Digest = null;
			if (!ValidDeclaration(Declaration)) return false;
			StringBuilder canonical = new StringBuilder("socket-transition-v1");
			AppendTerm(canonical, Declaration.Key);
			AppendTerm(canonical, Declaration.FromBuildKey);
			AppendTerm(canonical, Declaration.ToBuildKey);
			AppendTerm(canonical, Declaration.LotType);
			AppendTerm(canonical, ((int)Declaration.LotSize).ToString(
				CultureInfo.InvariantCulture));
			AppendTerm(canonical, Declaration.WaterDrams.ToString(CultureInfo.InvariantCulture));
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				AppendTerm(canonical, Declaration.MaterialUnits((KingdomMaterial)i).ToString(
					CultureInfo.InvariantCulture));
			AppendTerm(canonical, Declaration.WorkTicks.ToString(CultureInfo.InvariantCulture));
			try
			{
				byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
				byte[] hash;
				using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(bytes);
				StringBuilder encoded = new StringBuilder(hash.Length * 2);
				for (int i = 0; i < hash.Length; i++)
					encoded.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
				Digest = encoded.ToString();
				return true;
			}
			catch
			{
				Digest = null;
				return false;
			}
		}

		public static string IndexKey(string From, string To, string Type,
			ArchitectureLotSize Size)
		{
			string type = Fold(Type);
			return !ValidKey(From) || !ValidKey(To) || !ValidKey(type)
				? null : From + "\n" + To + "\n" + type + "\n"
					+ ((int)Size).ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Pure fixed-lot authorization used by preflight and paid retry. Ordinary improvements
		/// stay inside both frozen plan and binding. A declared plan change may cross those two
		/// identities only while preflight explicitly owns that declaration, or after its exact
		/// durable transition receipt has been rebound. Neither authority may move or retype the lot.
		/// </summary>
		public static bool AuthorizesFixedLotTransition(bool SamePlan, bool SameBinding,
			bool SameType, bool SameSize, bool SameRect, bool SameFacing, bool SameMainRoot,
			bool ExactLotIdentity, bool AllowPlanChange, bool DurableRouteAuthority)
		{
			return SameType && SameSize && SameRect && SameFacing && SameMainRoot &&
				ExactLotIdentity && ((SamePlan && SameBinding) || AllowPlanChange ||
					DurableRouteAuthority);
		}

		/// <summary>Directional typed endpoint match. Declaration authority also needs exact equality.</summary>
		public static bool MatchesRoute(KingdomSocketTransition Transition, string From,
			string To, string Type, ArchitectureLotSize Size)
		{
			string expected = IndexKey(From, To, Type, Size);
			return ValidDeclaration(Transition) && expected != null &&
				IndexKey(Transition.FromBuildKey, Transition.ToBuildKey,
					Transition.LotType, Transition.LotSize) == expected;
		}

		/// <summary>Authority match against one current declaration, including all priced work.</summary>
		public static bool MatchesRoute(KingdomSocketTransition Supplied,
			KingdomSocketTransition CurrentDeclaration)
		{
			return SameDeclaration(Supplied, CurrentDeclaration);
		}

		/// <summary>
		/// Pure receipt law. Every committed field has exactly one engine type. Schema 2 binds the
		/// canonical declaration digest; exact schema 1 remains adoptable only while that field is absent.
		/// </summary>
		public static bool ReceiptAuthorizes(KingdomSocketTransitionReceiptShape Receipt,
			string ExpectedKey, string ExpectedDeclarationDigest, string ExpectedBeforeHash,
			string ExpectedAfterHash, string ExpectedJobId, out bool Legacy)
		{
			Legacy = false;
			if (!Receipt.SchemaHasInt || Receipt.SchemaHasString
				|| !ValidKey(ExpectedKey) || !CanonicalHash(ExpectedDeclarationDigest)
				|| !CanonicalHash(ExpectedBeforeHash) || !CanonicalHash(ExpectedAfterHash)
				|| string.IsNullOrEmpty(ExpectedJobId)
				|| !ExactString(Receipt.KeyHasInt, Receipt.KeyHasString, Receipt.Key,
					ExpectedKey)
				|| !ExactString(Receipt.BeforeHasInt, Receipt.BeforeHasString,
					Receipt.BeforeHash, ExpectedBeforeHash)
				|| !ExactString(Receipt.AfterHasInt, Receipt.AfterHasString,
					Receipt.AfterHash, ExpectedAfterHash)
				|| !ExactString(Receipt.JobHasInt, Receipt.JobHasString, Receipt.JobId,
					ExpectedJobId)) return false;
			if (Receipt.Schema == ReceiptSchema)
				return ExactString(Receipt.DeclarationHasInt, Receipt.DeclarationHasString,
					Receipt.DeclarationDigest, ExpectedDeclarationDigest);
			if (Receipt.Schema != LegacyReceiptSchema || Receipt.DeclarationHasInt
				|| Receipt.DeclarationHasString || Receipt.DeclarationDigest != null) return false;
			Legacy = true;
			return true;
		}

		public static string RefuseUndeclared(string FromName, string ToName)
		{
			return "The pattern-book declares no safe same-set change from the "
				+ (FromName ?? "standing work") + " to " + (ToName ?? "that design")
				+ ". Strike it and commission fresh, or add an explicit transition declaration.";
		}

		private static bool TrySize(string Text, out ArchitectureLotSize Size)
		{
			Size = 0;
			if (string.Equals(Text, "S", StringComparison.OrdinalIgnoreCase))
				Size = ArchitectureLotSize.Small;
			else if (string.Equals(Text, "M", StringComparison.OrdinalIgnoreCase))
				Size = ArchitectureLotSize.Medium;
			else if (string.Equals(Text, "L", StringComparison.OrdinalIgnoreCase))
				Size = ArchitectureLotSize.Large;
			else if (string.Equals(Text, "XL", StringComparison.OrdinalIgnoreCase))
				Size = ArchitectureLotSize.Huge;
			return Size != 0;
		}

		private static string Fold(string Value)
		{
			return string.IsNullOrWhiteSpace(Value) ? null : Value.Trim().ToLowerInvariant();
		}

		private static bool ValidKey(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxKeyChars) return false;
			for (int i = 0; i < Value.Length; i++)
				if (char.IsControl(Value[i]) || char.IsWhiteSpace(Value[i])) return false;
			return true;
		}

		private static bool ValidDeclaration(KingdomSocketTransition Declaration)
		{
			return Declaration != null && ValidKey(Declaration.Key)
				&& ValidKey(Declaration.FromBuildKey) && ValidKey(Declaration.ToBuildKey)
				&& Declaration.FromBuildKey != Declaration.ToBuildKey
				&& ValidKey(Declaration.LotType) && Fold(Declaration.LotType) == Declaration.LotType
				&& (int)Declaration.LotSize >= (int)ArchitectureLotSize.Small
				&& (int)Declaration.LotSize <= (int)ArchitectureLotSize.Huge
				&& Declaration.WaterDrams >= 0 && Declaration.WorkTicks >= 1L
				&& Declaration.WorkTicks <= MaxWorkTicks && Declaration.HasMaterials();
		}

		private static void AppendTerm(StringBuilder Builder, string Value)
		{
			Builder.Append('|').Append(Value.Length.ToString(CultureInfo.InvariantCulture))
				.Append(':').Append(Value);
		}

		private static bool ExactString(bool HasInt, bool HasString, string Value,
			string Expected)
		{
			return !HasInt && HasString && Value == Expected;
		}

		private static bool CanonicalHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}
	}
}
