namespace ThousandAndFirst
{
	/// <summary>Engine-free faction facts used by the open CanBeCreed derivation. This is a rule,
	/// not a faction catalogue: third-party factions are judged from their own ordinary data.</summary>
	public readonly struct CreedFactionFacts
	{
		public readonly string Name;
		public readonly bool Visible;
		public readonly bool HatesPlayer;
		public readonly bool Old;
		public readonly int HistoricalSignificance;
		public readonly bool FormatWithArticle;

		public CreedFactionFacts(string Name, bool Visible, bool HatesPlayer, bool Old,
			int HistoricalSignificance, bool FormatWithArticle)
		{
			this.Name = Name;
			this.Visible = Visible;
			this.HatesPlayer = HatesPlayer;
			this.Old = Old;
			this.HistoricalSignificance = HistoricalSignificance;
			this.FormatWithArticle = FormatWithArticle;
		}
	}

	public static class KingdomCreedContentRules
	{
		public static bool CanBeCreed(CreedFactionFacts Candidate, int Significance)
		{
			if (!Candidate.Visible || Candidate.HatesPlayer || string.IsNullOrEmpty(Candidate.Name)
				|| Candidate.Name.StartsWith("SultanCult", System.StringComparison.Ordinal))
			{
				return false;
			}
			return !Candidate.Old || Candidate.HistoricalSignificance >= Significance
				|| Candidate.FormatWithArticle;
		}
	}
}
