namespace ThousandAndFirst.Harness
{
	/// <summary>Cold reload belongs to the receipt-owned host, never the live script cursor.</summary>
	internal static class KingdomScenarioReload
	{
		internal static string Refuse(out bool Ok)
		{
			Ok = false;
			return "taf-reload-requires-cold-process: this live scenario cannot prove a full unload. "
				+ "The sealed script is one-shot and cannot resume after loading. Frame yields and "
				+ "focus holds do not unload engine state. Use Tools/run-personas.sh with a persona "
				+ "declaring SCRIPT=reload-descendant quickstart <marsh|canyon|dunes> <yes|no>: the host owns "
				+ "real save, exact process stop, a fresh sealed load profile and unchanged-state checks. "
				+ "This refusal saved, loaded and advanced nothing.";
		}
	}
}
