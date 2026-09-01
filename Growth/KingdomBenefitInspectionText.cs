using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Engine-free, bounded wording for the live physical-benefit inspector.</summary>
	public static class KingdomBenefitInspectionText
	{
		public static string BuildingLabel(KingdomBenefitReading Reading, string Name)
		{
			if (Reading?.Designation == null) return "malformed building designation";
			return Named(Name, Reading.Designation.BuildingKey) + "  "
				+ Coordinates(Reading.Designation.Cells) + "  —  "
				+ ActiveSupply(Reading);
		}

		public static string BuildingDetail(KingdomBenefitReading Reading, string Name)
		{
			if (Reading?.Designation == null) return "This building designation is malformed.";
			KingdomBenefitDesignation row = Reading.Designation;
			StringBuilder text = new StringBuilder();
			text.Append(Named(Name, row.BuildingKey)).Append('\n');
			text.Append("Role: ").Append(Value(row.BuildingKey)).Append('\n');
			text.Append("Designation: ").Append(Value(row.Identity)).Append('\n');
			text.Append("Source: ").Append(Value(row.ProviderId)).Append(" v")
				.Append(Value(row.ProviderVersion)).Append('\n');
			text.Append("Root: ").Append(Value(row.RootId));
			if (!string.IsNullOrEmpty(row.LotId)) text.Append("; lot ").Append(row.LotId);
			text.Append('\n').Append("Ground: ").Append(Value(row.ZoneId)).Append("; ")
				.Append(Coordinates(row.Cells)).Append('\n');
			text.Append("Active now: ").Append(ActiveSupply(Reading)).Append('\n');
			text.Append("Capacity ceiling: ").Append(Amounts(row.Caps, "none")).Append('\n');
			text.Append("Unfilled capacity: ").Append(UnfilledAmounts(Reading)).Append('\n');
			text.Append("Accepted qualities: ").Append(TagValues(row.AcceptedTags, "none")).Append('\n');
			text.Append("Missing qualities: ").Append(MissingTags(Reading)).Append('\n');
			text.Append("Physical provider rows: ").Append(Reading.Providers?.Count ?? 0);
			if (!HasActive(Reading))
				text.Append("\n\nThe designation alone provides zero. Place or repair eligible physical furnishings here.");
			return text.ToString();
		}

		public static string ProviderLabel(KingdomBenefitInspection Row, string Name)
		{
			if (Row == null) return "fault — malformed provider row";
			return Status(Row) + " — " + Named(Name, Row.ProviderKey) + " — "
				+ CreditedSupply(Row);
		}

		public static string ProviderDetail(KingdomBenefitInspection Row, string Name)
		{
			if (Row == null) return "This provider row is malformed.";
			StringBuilder text = new StringBuilder();
			text.Append(Named(Name, Row.ProviderKey)).Append('\n');
			text.Append("Status: ").Append(Status(Row)).Append('\n');
			text.Append("Provider: ").Append(Value(Row.ProviderKey)).Append('\n');
			text.Append("Identity: ").Append(Value(Row.ProviderIdentity)).Append('\n');
			text.Append("Designation: ").Append(Value(Row.DesignationIdentity)).Append('\n');
			text.Append("Operating now: ").Append(Row.OperationPercent).Append("%\n");
			text.Append("Nominal offer: ").Append(Amounts(Row.Offered, "none"));
			if (Row.Tags != null && Row.Tags.Count > 0)
				text.Append("; ").Append(Tags(Row.Tags, "none"));
			text.Append('\n').Append("Counted now: ").Append(CreditedSupply(Row));
			if (Row.Fault != KingdomBenefitFault.None)
				text.Append("\nFault: ").Append(Row.Fault);
			if (!string.IsNullOrEmpty(Row.Detail)) text.Append("\nReason: ").Append(Row.Detail);
			if (Row.OutsideDesignationContract)
				text.Append("\nRole mismatch: some live offer is not accepted by this building.");
			if (Row.SaturatedByDesignation)
				text.Append("\nAt capacity: some accepted live offer is already fully supplied.");
			return text.ToString();
		}

		public static string Status(KingdomBenefitInspection Row)
		{
			if (Row == null) return "fault";
			switch (Row.Fault)
			{
			case KingdomBenefitFault.None:
				if (Row.OutsideDesignationContract && Row.SaturatedByDesignation)
					return "partly active; wrong role and capped";
				if (Row.OutsideDesignationContract) return "partly active; wrong role";
				if (Row.SaturatedByDesignation || Row.LimitedByDesignation)
					return "partly active; capped";
				return "active";
			case KingdomBenefitFault.ProviderCap: return "capped";
			case KingdomBenefitFault.UnacceptedBenefit:
				return Row.SaturatedByDesignation ? "wrong role and capped" : "wrong role";
			case KingdomBenefitFault.MissingIdentity:
			case KingdomBenefitFault.MissingDesignation: return "missing";
			case KingdomBenefitFault.Inoperable:
			case KingdomBenefitFault.UnsupportedOperation: return "inactive";
			case KingdomBenefitFault.SourceFault: return "source fault";
			case KingdomBenefitFault.ObservationLimit: return "over limit";
			default: return "ineligible";
			}
		}

		public static string ActiveSupply(KingdomBenefitReading Reading)
		{
			if (Reading == null) return "none (designation alone provides zero)";
			string amounts = Amounts(Reading.Carries, "");
			string tags = Tags(Reading.Provides, "");
			if (amounts.Length == 0 && tags.Length == 0)
				return "none (designation alone provides zero)";
			return amounts.Length == 0 ? tags : tags.Length == 0 ? amounts : amounts + "; " + tags;
		}

		private static string CreditedSupply(KingdomBenefitInspection Row)
		{
			string amounts = Amounts(Row?.Credited, "");
			string tags = Tags(Row?.CreditedTags, "");
			if (amounts.Length == 0 && tags.Length == 0) return "nothing";
			return amounts.Length == 0 ? tags : tags.Length == 0 ? amounts : amounts + "; " + tags;
		}

		private static bool HasActive(KingdomBenefitReading Reading)
		{
			return Reading != null && ((Reading.Carries?.Count ?? 0) > 0
				|| (Reading.Provides?.Count ?? 0) > 0);
		}

		private static string UnfilledAmounts(KingdomBenefitReading Reading)
		{
			if (Reading?.Designation?.Caps == null) return "none";
			List<KindAmount> missing = new List<KindAmount>();
			for (int i = 0; i < Reading.Designation.Caps.Count; i++)
			{
				KindAmount cap = Reading.Designation.Caps[i]; int active = 0;
				for (int a = 0; Reading.Carries != null && a < Reading.Carries.Count; a++)
					if (Reading.Carries[a].Kind == cap.Kind) { active = Reading.Carries[a].Amount; break; }
				if (cap.Amount > active) missing.Add(new KindAmount(cap.Kind, cap.Amount - active));
			}
			return Amounts(missing, "none");
		}

		private static string MissingTags(KingdomBenefitReading Reading)
		{
			if (Reading?.Designation?.AcceptedTags == null) return "none";
			List<string> missing = new List<string>();
			for (int i = 0; i < Reading.Designation.AcceptedTags.Count; i++)
			{
				string tag = Reading.Designation.AcceptedTags[i]; bool active = false;
				for (int a = 0; Reading.Provides != null && a < Reading.Provides.Count; a++)
					if (Reading.Provides[a] == tag) { active = true; break; }
				if (!active) missing.Add(tag);
			}
			return TagValues(missing, "none");
		}

		private static string Amounts(IList<KindAmount> Rows, string Empty)
		{
			if (Rows == null || Rows.Count == 0) return Empty;
			List<string> parts = new List<string>();
			for (int i = 0; i < Rows.Count; i++)
				parts.Add(Value(Rows[i].Kind) + " " + Rows[i].Amount);
			parts.Sort(StringComparer.Ordinal); return string.Join(", ", parts.ToArray());
		}

		private static string Tags(IList<string> Rows, string Empty)
		{
			string values = TagValues(Rows, Empty);
			return values == Empty ? Empty : "qualities: " + values;
		}

		private static string TagValues(IList<string> Rows, string Empty)
		{
			if (Rows == null || Rows.Count == 0) return Empty;
			List<string> parts = new List<string>();
			for (int i = 0; i < Rows.Count; i++) parts.Add(Value(Rows[i]));
			parts.Sort(StringComparer.Ordinal); return string.Join(", ", parts.ToArray());
		}

		private static string Coordinates(IList<KingdomBenefitCell> Cells)
		{
			if (Cells == null || Cells.Count == 0) return "no exact cells";
			int minX = Cells[0].X, maxX = minX, minY = Cells[0].Y, maxY = minY;
			for (int i = 1; i < Cells.Count; i++)
			{
				minX = Math.Min(minX, Cells[i].X); maxX = Math.Max(maxX, Cells[i].X);
				minY = Math.Min(minY, Cells[i].Y); maxY = Math.Max(maxY, Cells[i].Y);
			}
			return Cells.Count + " exact cell" + (Cells.Count == 1 ? "" : "s") + " at "
				+ minX + "," + minY + (minX == maxX && minY == maxY ? ""
					: "–" + maxX + "," + maxY);
		}

		private static string Named(string Name, string Fallback)
		{
			return string.IsNullOrWhiteSpace(Name) ? Value(Fallback) : Name.Trim();
		}

		private static string Value(string Text)
		{
			return string.IsNullOrWhiteSpace(Text) ? "<none>" : Text.Trim();
		}
	}
}
