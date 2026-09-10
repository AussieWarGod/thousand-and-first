namespace ThousandAndFirst
{
	/// <summary>Clean first-attempt developer fixture only. Lost records physical net debit,
	/// not additional waste: this fixture requires Lost == Spent == Requested.
	/// General jobs can retain greater loss from surplus bits, callbacks or earlier attempts.
	/// Mirrors KingdomConstructionRules.ValidateClaims and the measured funding merge.</summary>
	internal static class KingdomQuickstartBuildClaims
	{
		internal static bool CleanFirstPayment(KingdomConstructionClaims claims, int water,
			KingdomMaterialDebitCost expected)
		{
			if (claims == null || expected == null || water < 0 || !claims.Exact
				|| !KingdomConstructionRules.ValidateClaims(claims)) return false;
			string material = expected.ToClaimString();
			return claims.WaterRequested == water && claims.WaterSpent == water
				&& claims.WaterLost == water && claims.WaterOutstanding == 0
				&& claims.MaterialRequested == material && claims.MaterialSpent == material
				&& claims.MaterialLost == material
				&& claims.MaterialOutstanding == new KingdomMaterialDebitCost().ToClaimString();
		}
	}
}
