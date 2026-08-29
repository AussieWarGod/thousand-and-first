using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static string Preview(KingdomRelocationReceipt Receipt)
		{
			StringBuilder text = new StringBuilder();
			text.Append("The heart calls for ").Append(Receipt.Moves.Count)
				.Append(Receipt.Moves.Count == 1 ? " yielding plot" : " yielding plots")
				.Append(". The settlement will move one whole lot at a time:\n\n");
			for (int i = 0; i < Receipt.Moves.Count; i++)
			{
				KingdomRelocationMove move = Receipt.Moves[i];
				text.Append(i + 1).Append(". ").Append(string.IsNullOrEmpty(move.DisplayName)
					? move.BuildKey : move.DisplayName).Append(" — ")
					.Append(Corners(move.Source)).Append(" → ").Append(Corners(move.Destination))
					.Append("; about ").Append(KingdomRelocationRules.Days(move.RequiredTicks,
						KingdomRules.TicksPerDay)).Append(" days\n");
			}
			text.Append("\nTotal: about ").Append(KingdomRelocationRules.Days(
				KingdomRelocationRules.TotalTicks(Receipt), KingdomRules.TicksPerDay))
				.Append(" days. Cost: {{C|0 drams, 0 materials}}. Only labour and world-time pass. ")
				.Append("Each old plot remains standing and working until its destination frame is complete; ")
				.Append("then the same lot, contents, people, history, wear, and declarations cross whole.");
			return text.ToString();
		}

		private static string Corners(KingdomRelocationRect Rect)
		{
			return "(" + Rect.X1 + "," + Rect.Y1 + ")–(" + Rect.X2 + "," + Rect.Y2 + ")";
		}
	}
}
