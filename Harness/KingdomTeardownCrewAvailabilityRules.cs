namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free verdict behind KingdomTeardownCrewEnrollment.RequireAvailable, proved by value
	/// in DevTests/KingdomTeardownCrewAvailabilityRulesTests.cs (both public projects).
	/// <para>
	/// RUN 39 RETRY 2 (2fa563c, investigation-run39-deaths-crew.md Q2). The fire COMPLETED for the
	/// first time in a teardown run; its raising ceremony queued a physical happening whose
	/// attendees are the settlers, and staging stamps the happening token on each body
	/// (Simulation/City/KingdomPhysicalHappenings.02.ObservePrepareAndUse.cs:60-77) until the
	/// happening is driven to its end on later passes. The 2400-tick check landed inside that
	/// window, and the fixture's strict "present in AvailableSettlers" refused crew body 2 as if
	/// it had left. A staged body is a HEALTHY crew body doing lawful settlement work; only a body
	/// that is not Resident on the roll, or not grounded on this zone, is a refusal.
	/// </para>
	/// </summary>
	internal static class KingdomTeardownCrewAvailabilityRules
	{
		internal enum Verdict
		{
			Accepted = 0,
			RefusedNotResident = 1,
			RefusedUngrounded = 2,
		}

		/// <summary>Standing and ground are asserted; staging is disclosed, never refused.</summary>
		internal static Verdict Judge(bool ResidentOnRoll, bool Grounded, bool Staged)
		{
			if (!ResidentOnRoll) return Verdict.RefusedNotResident;
			if (!Grounded) return Verdict.RefusedUngrounded;
			return Verdict.Accepted;
		}

		/// <summary>The per-body journal clause, the same spelling for every check.</summary>
		internal static string Describe(bool ResidentOnRoll, bool Grounded, bool Staged,
			bool InProjection)
		{
			return "staged=" + Staged + " standing=" + (ResidentOnRoll ? "Resident" : "NotResident")
				+ " grounded=" + Grounded + " available=" + InProjection;
		}

		/// <summary>Refusal text for a verdict, per body; null when accepted.</summary>
		internal static string Refusal(Verdict Verdict, int Index, int Count)
		{
			string body = "crew body " + Index + " of " + Count;
			switch (Verdict)
			{
				case Verdict.RefusedNotResident:
					return body + " is not present in the production AvailableSettlers projection "
						+ "because it is not Resident standing on the roll";
				case Verdict.RefusedUngrounded:
					return body + " is not present in the production AvailableSettlers projection "
						+ "because it is not grounded on this zone";
				default:
					return null;
			}
		}
	}
}
