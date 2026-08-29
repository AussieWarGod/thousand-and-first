namespace ThousandAndFirst
{
	/// <summary>Removes legacy office-to-capability bridges; civic offices remain title metadata.</summary>
	internal static class KingdomPolityPromotionRuntime
	{
		internal static bool TryReconcile(KingdomSystem System, out string Failure)
		{
			Failure = null;
			if (System?.PolityLedger == null) return false;
			string cause = KingdomPolityRules.ActivationId(
				"taf:fact:office-retirement:v1:", "polity-title-only-office-retirement-v1",
				System.PolityLedger.RealmId ?? "detached");
			return KingdomPolityRules.TryRetireAllOfficeFigures(System.PolityLedger,
				System.PolityLedger.Revision, cause,
				out KingdomPolityPublicationResult _, out Failure);
		}
	}
}
