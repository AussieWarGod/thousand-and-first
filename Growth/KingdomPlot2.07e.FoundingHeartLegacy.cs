using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>
		/// Read-only compatibility classifier. Only a wholly correct old survey and old heart root
		/// pass; partial or foreign evidence is never stamped, adopted, moved, or removed.
		/// </summary>
		private static FoundingHeartLegacyState ClassifyLegacyHeart(Zone Z,
			FoundingHeartContext Context)
		{
			if (Z == null || Context == null) return FoundingHeartLegacyState.PartialOrForeign;
			if (HasCurrentFoundingHeartEvidence(Z, Context.Plan))
				return FoundingHeartLegacyState.PartialOrForeign;
			string[] survey = new string[4]
			{
				Z.GetZoneProperty(SurveyX1Property, null),
				Z.GetZoneProperty(SurveyY1Property, null),
				Z.GetZoneProperty(SurveyX2Property, null),
				Z.GetZoneProperty(SurveyY2Property, null)
			};
			int present = 0;
			for (int i = 0; i < survey.Length; i++)
				if (!string.IsNullOrEmpty(survey[i])) present++;
			bool evidence = HasLegacyHeartObjectEvidence(Z);
			if (present == 0 && !evidence) return FoundingHeartLegacyState.Empty;
			if (present != 4 || !TrySurveyedHeart(Z,
				out KingdomPlotRules.PlotRect legacySurvey)
				|| !SameRect(legacySurvey, Context.Survey)
				|| !TryRiteGround(Z, out int riteX, out int riteY)
				|| riteX != Context.Plan.RiteX || riteY != Context.Plan.RiteY)
				return FoundingHeartLegacyState.PartialOrForeign;
			return ExactLegacyHeartMarks(Z, Context.Plan)
				&& ExactLegacyHeartRoot(Z, Context)
				? FoundingHeartLegacyState.Complete
				: FoundingHeartLegacyState.PartialOrForeign;
		}

		private static bool HasCurrentFoundingHeartEvidence(Zone Z,
			KingdomFoundingHeartPlan Plan)
		{
			if (HasGlobalFoundingHeartTransactionEvidence(Plan.TransactionId, Plan.ZoneId))
				return true;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item == null) continue;
				if (item.HasStringProperty(FoundingHeartOwnerProperty)
					|| item.HasIntProperty(FoundingHeartOwnerProperty)
					|| item.HasStringProperty(FoundingHeartSlotProperty)
					|| item.HasIntProperty(FoundingHeartSlotProperty)) return true;
				for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
					if (item.IDIfAssigned == KingdomFoundingHeartRules.SlotId(Plan, slot))
						return true;
			}
			return false;
		}

		private static bool HasLegacyHeartObjectEvidence(Zone Z)
		{
			return HasFoundingHeartEvidenceInZone(Z);
		}

		private static bool ExactLegacyHeartMarks(Zone Z, KingdomFoundingHeartPlan Plan)
		{
			int relics = 0;
			int stakes = 0;
			bool[] corners = new bool[4];
			foreach (GameObject item in Z.GetObjects())
			{
				if (!GameObject.Validate(item)) continue;
				bool relic = item.GetIntProperty(HeartRelicProperty) == 1;
				bool stake = item.GetIntProperty(HeartStakeProperty) == 1;
				if (!relic && !stake) continue;
				if (relic == stake || item.CurrentZone != Z || item.CurrentCell == null) return false;
				if (string.IsNullOrEmpty(item.IDIfAssigned)
					|| FindGlobalFoundingHeartId(item.IDIfAssigned, out GameObject global,
						out bool graveyard) != KingdomPhysicalLookupState.Exact
					|| graveyard || !ReferenceEquals(global, item)) return false;
				if (relic)
				{
					if (item.Blueprint != HeartRelicBlueprint || item.CurrentCell.X != Plan.RiteX
						|| item.CurrentCell.Y != Plan.RiteY) return false;
					relics++;
					continue;
				}
				if (item.Blueprint != SurveyStakeBlueprint) return false;
				int corner = LegacyHeartCorner(Plan, item.CurrentCell.X, item.CurrentCell.Y);
				if (corner < 0 || corners[corner]) return false;
				corners[corner] = true;
				stakes++;
			}
			return relics == 1 && stakes == 4
				&& corners[0] && corners[1] && corners[2] && corners[3];
		}

		private static int LegacyHeartCorner(KingdomFoundingHeartPlan Plan, int X, int Y)
		{
			if (X == Plan.SurveyX1 && Y == Plan.SurveyY1) return 0;
			if (X == Plan.SurveyX2 && Y == Plan.SurveyY1) return 1;
			if (X == Plan.SurveyX1 && Y == Plan.SurveyY2) return 2;
			return X == Plan.SurveyX2 && Y == Plan.SurveyY2 ? 3 : -1;
		}

		private static bool ExactLegacyHeartRoot(Zone Z, FoundingHeartContext Context)
		{
			GameObject root = null;
			int count = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (!GameObject.Validate(item)) continue;
				r_KingdomPlotWorks works = item.GetPart<r_KingdomPlotWorks>();
				bool candidate = works?.DesignKey == "heartbasin"
					|| (item.GetIntProperty(HeartPlotProperty) == 1
						&& item.GetIntProperty(PlotPartProperty) != 1);
				if (!candidate) continue;
				root = item;
				count++;
			}
			if (count != 1 || root == null || string.IsNullOrEmpty(root.IDIfAssigned)
				|| root.GetIntProperty(HeartPlotProperty) != 1
				|| string.IsNullOrEmpty(root.GetStringProperty(PlotIdProperty))
				|| !string.IsNullOrEmpty(root.GetStringProperty(KingdomConstruction.ReceiptProperty))
				|| root.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != "heartbasin"
				|| !TryReadRect(root, out KingdomPlotRules.PlotRect rect)
				|| !SameRect(rect, Context.Rect)) return false;
			Cell cell = Z.GetCell(Context.Architecture.MainWorldX,
				Context.Architecture.MainWorldY);
			if (root.CurrentCell != cell || root.CurrentZone != Z
				|| !ExpectedArchitectureReceipt(root, cell, "heartbasin",
					Context.Architecture, false)) return false;
			r_KingdomPlotWorks part = root.GetPart<r_KingdomPlotWorks>();
			if (part == null)
			{
				if (root.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
					|| root.Blueprint != Context.Entry.Blueprint) return false;
			}
			else if (root.Blueprint != WorksBlueprint || part.DesignKey != "heartbasin")
				return false;
			KingdomPhysicalLookupState state = FindGlobalFoundingHeartId(root.IDIfAssigned,
				out GameObject exact, out bool graveyard);
			return state == KingdomPhysicalLookupState.Exact && !graveyard
				&& ReferenceEquals(exact, root);
		}
	}
}
