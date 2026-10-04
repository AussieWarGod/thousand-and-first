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

		/// <summary>The same clean first attempt for a COMPOSITE price whose bits are paid in
		/// whole bodies. A body is broken up whole, so the bits lost can exceed the bits priced by
		/// the surplus on the bodies production chose (Growth/KingdomMaterialDebitRules.Planning.cs,
		/// AddLost). The price must still be requested and answered exactly and in full, materials
		/// and exotics must be lost exactly as priced, and the bits lost must equal
		/// <paramref name="lostBits"/>, which the caller witnesses physically for itself and never
		/// reads off the claim. With the priced bits witnessed this is exactly CleanFirstPayment.</summary>
		internal static bool CleanFirstCompositePayment(KingdomConstructionClaims claims, int water,
			KingdomMaterialDebitCost expected, KingdomBitTally lostBits)
		{
			if (claims == null || expected == null || lostBits == null || water < 0 || !claims.Exact
				|| !KingdomConstructionRules.ValidateClaims(claims)) return false;
			string material = expected.ToClaimString();
			string lost = new KingdomMaterialDebitCost(expected.Materials, lostBits, expected.Exotics)
				.ToClaimString();
			return claims.WaterRequested == water && claims.WaterSpent == water
				&& claims.WaterLost == water && claims.WaterOutstanding == 0
				&& claims.MaterialRequested == material && claims.MaterialSpent == material
				&& claims.MaterialLost == lost
				&& claims.MaterialOutstanding == new KingdomMaterialDebitCost().ToClaimString();
		}
	}
}
