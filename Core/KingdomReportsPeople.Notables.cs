using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomReports
	{
		private static void AppendDeedNotables(StringBuilder Text, KingdomSystem System,
			string SettlementId)
		{
			if (!KingdomPolityNamedFigurePresentationRules.TryActiveDeeds(System?.PolityLedger,
				System?.RealmId, SettlementId, out List<KingdomPolityNamedFigureView> figures,
				out string _))
			{
				Text.Append("\n\n{{K|Notable-deed records are unavailable.}}"); return;
			}
			if (figures.Count == 0) return;
			Text.Append("\n\nNotable deeds:");
			for (int i = 0; i < figures.Count; i++)
			{
				KingdomPolityNamedFigureView figure = figures[i];
				Text.Append("\n").Append(KingdomPresentation.Rich(figure.DisplayName))
					.Append(" — ").Append(figure.Role).Append("; ")
					.Append(KingdomPresentation.Rich(figure.DeedSummary));
				if (!figure.DeedSummary.EndsWith(".", global::System.StringComparison.Ordinal))
					Text.Append(".");
			}
		}
	}
}
