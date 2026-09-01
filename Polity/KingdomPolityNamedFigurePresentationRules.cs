using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Safe, bounded roll projection for deed-promoted resident figures.</summary>
	internal static class KingdomPolityNamedFigurePresentationRules
	{
		internal static bool TryActiveDeeds(KingdomPolityLedger Ledger, string PolityId,
			string SettlementId, out List<KingdomPolityNamedFigureView> Views,
			out string Failure)
		{
			Views = new List<KingdomPolityNamedFigureView>(); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!KingdomPolityRules.SemanticId(PolityId) ||
				!KingdomPolityRules.TypedId(SettlementId, "taf:settlement:v1:")) return false;
			KingdomPolityRecord polity = KingdomPolityAuthority.Polity(Ledger, PolityId);
			if (polity == null || polity.Source != KingdomPolitySource.CurrentRealm ||
				polity.Lifecycle != KingdomPolityLifecycle.Active)
			{
				Failure = "current polity authority is unavailable"; return false;
			}
			for (int i = 0; i < Ledger.NamedFigures.Count && Views.Count <
				KingdomPolityAttentionRules.MaximumActiveNamedFigures; i++)
			{
				KingdomPolityNamedFigureRecord row = Ledger.NamedFigures[i];
				if (row.PolityId != PolityId || row.Phase != KingdomPolityFigurePhase.Active ||
					row.Origin != KingdomPolityFigureOrigin.PromotedByDeed ||
					row.ResidentSettlementId != SettlementId) continue;
				string role = Role(row.RoleKey);
				if (role == null || !KingdomPolityAmbientTransactionRules.SafeText(
					row.DisplayName, true) || !KingdomPolityAmbientTransactionRules.SafeText(
					row.DeedSummary, true))
				{
					Failure = "named deed record is not safe to present";
					Views.Clear(); return false;
				}
				Views.Add(new KingdomPolityNamedFigureView(row.DisplayName, role, row.DeedSummary));
			}
			return true;
		}

		private static string Role(string Key)
		{
			switch (Key)
			{
			case "guard": return "guard";
			case "patrol": return "patrol";
			case "courier": return "courier";
			case "trader": return "trader";
			case "migrant": return "migrant envoy";
			case "envoy": return "envoy";
			default: return null;
			}
		}
	}

	internal sealed class KingdomPolityNamedFigureView
	{
		internal readonly string DisplayName;
		internal readonly string Role;
		internal readonly string DeedSummary;

		internal KingdomPolityNamedFigureView(string DisplayName, string Role, string DeedSummary)
		{
			this.DisplayName = DisplayName; this.Role = Role; this.DeedSummary = DeedSummary;
		}
	}
}
