namespace ThousandAndFirst
{
	public static partial class KingdomPolityDispatchRules
	{
		/// <summary>Refuses unreadable or foreign raw authority without rewriting it.</summary>
		public static bool TryRecover(KingdomPolityDispatchState State, string RealmId,
			string Fault, out string Failure)
		{
			Failure = null;
			if (State == null || !KingdomPolityRules.TypedId(RealmId, "taf:realm:") ||
				!KingdomPolityRules.Text(Fault, true)) return Fail(
					"polity dispatch recovery evidence is invalid", out Failure);
			return Fail("polity dispatch authority is quarantined and was preserved: " + Fault,
				out Failure);
		}
	}
}
