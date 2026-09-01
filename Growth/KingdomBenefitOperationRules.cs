namespace ThousandAndFirst
{
	/// <summary>Pure percentage law for independent physical-benefit operation gates.</summary>
	public static class KingdomBenefitOperationRules
	{
		/// <summary>Semantic fixtures may serve only their declared design. Untagged native
		/// capabilities remain generic and are constrained by designation caps and scope.</summary>
		public static bool ProviderMatchesDesign(string ProviderBuildKey,
			string DesignationBuildKey)
		{
			// No affinity tag means a generic/native provider. A present tag is authority,
			// including whitespace-only or malformed values: those must fail closed.
			if (ProviderBuildKey == null) return true;
			if (ProviderBuildKey.Length > 128
				|| (DesignationBuildKey ?? "").Length > 128) return false;
			string provider = Fold(ProviderBuildKey);
			string designation = Fold(DesignationBuildKey);
			return KingdomDesignationRules.SafeToken(provider, 128)
				&& KingdomDesignationRules.SafeToken(designation, 128)
				&& provider == designation;
		}

		public static bool IsPercent(int Value)
		{
			return Value >= 0 && Value <= 100;
		}

		/// <summary>Composes two independent 0-100 gates. Malformed inputs fail closed.</summary>
		public static int Compose(int First, int Second)
		{
			if (!IsPercent(First) || !IsPercent(Second)) return 0;
			return (int)((long)First * Second / 100L);
		}

		private static string Fold(string Value)
		{
			return (Value ?? "").Trim().ToLowerInvariant();
		}
	}
}
