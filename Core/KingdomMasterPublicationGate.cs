namespace ThousandAndFirst
{
	/// <summary>Pure all-or-nothing gate used immediately before master-resume publication.</summary>
	public static class KingdomMasterPublicationGate
	{
		public const int MaxParticipants = 9;

		public static bool TryOpen(bool[] SourceMatches, int Count, int InjectedBoundary,
			out string Failure)
		{
			Failure = null;
			if (SourceMatches == null || Count < 1 || Count > MaxParticipants
				|| Count > SourceMatches.Length || InjectedBoundary < -1
				|| InjectedBoundary >= Count)
			{
				Failure = "master-resume publication gate is invalid"; return false;
			}
			for (int i = 0; i < Count; i++)
				if (i == InjectedBoundary || !SourceMatches[i])
				{
					Failure = "master-resume source boundary " + i + " changed"; return false;
				}
			return true;
		}
	}
}
