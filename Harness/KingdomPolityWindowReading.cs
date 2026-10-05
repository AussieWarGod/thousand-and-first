using System.Globalization;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// #244/#257 positive native witness grammar. One <c>polity-dispatch</c> journal row reads the
	/// realm's dispatch receipt exactly as production left it after a daily pass:
	/// <c>window=N revision=R count=C mask=M intents=I</c> (the frozen window ordinal, the receipt
	/// revision, the frozen endpoint count, the completed-endpoint mask and the open intent count).
	/// Pure and engine-free so both public suites execute it; Tools/personas/persona_polity.py
	/// owns the host grammar that judges a run's readings together.
	/// </summary>
	internal static class KingdomPolityWindowReading
	{
		/// <summary>The journal verb column of the observation row.</summary>
		internal const string Row = "polity-dispatch";

		/// <summary>The exact observation, or null with a fixed failure. Reads only: it never
		/// repairs, recovers, reconciles or writes dispatch state.</summary>
		internal static string Describe(KingdomPolityDispatchState State, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityDispatchRules.ValidState(State, out string invalid))
			{
				Failure = "the polity dispatch receipt is invalid (" + invalid + ")";
				return null;
			}
			if (!State.HasWindow)
			{
				Failure = "the polity dispatch receipt has not opened a window";
				return null;
			}
			int intents = 0;
			for (int i = 0; i < State.DirectRecords.Count; i++)
				if (KingdomPolityDispatchRules.IsKind(State.DirectRecords[i],
					KingdomPolityDispatchRules.IntentPrefix)) intents++;
			return "window=" + State.LastWindowOrdinal.ToString(CultureInfo.InvariantCulture)
				+ " revision=" + State.Revision.ToString(CultureInfo.InvariantCulture)
				+ " count=" + State.EndpointCount.ToString(CultureInfo.InvariantCulture)
				+ " mask=" + State.CompletedMask.ToString(CultureInfo.InvariantCulture)
				+ " intents=" + intents.ToString(CultureInfo.InvariantCulture);
		}
	}
}
