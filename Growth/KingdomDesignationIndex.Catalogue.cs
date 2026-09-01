using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomDesignationIndex
	{
		internal static bool CompleteForSource(KingdomBenefitDesignation Row, Zone Z,
			out string Failure)
		{
			return CompleteCatalogueContract(Row, Z, out Failure);
		}

		/// <summary>Catalogue data is a designation ceiling only. External rows cannot raise it.</summary>
		private static bool CompleteCatalogueContract(KingdomBenefitDesignation Row, Zone Z,
			out string Failure)
		{
			Failure = null;
			if (Row == null || !KingdomData.TryGetBuilding(Row.BuildingKey,
				out KingdomRules.BuildEntry entry))
				return Fail("designation names no registered building", out Failure);
			List<KindAmount> parsed;
			if (!KingdomCatalogueRules.TryParseTally(entry.Carries, out parsed, out Failure))
				return false;
			Dictionary<string, int> folded = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < parsed.Count; i++)
			{
				if (parsed[i].Amount <= 0) continue;
				if (parsed[i].Kind == KingdomCatalogueRules.SupportWater
					|| parsed[i].Kind == KingdomCatalogueRules.SupportFood) continue;
				int prior; folded.TryGetValue(parsed[i].Kind, out prior);
				long next = (long)prior + parsed[i].Amount;
				folded[parsed[i].Kind] = next >= int.MaxValue ? int.MaxValue : (int)next;
			}
			if (!AppendExactYardCaps(Row, Z, folded, out Failure)) return false;
			Row.Caps = new List<KindAmount>();
			foreach (KeyValuePair<string, int> pair in folded)
				Row.Caps.Add(new KindAmount(pair.Key, pair.Value));
			if (entry.Defence > 0) Row.Caps.Add(new KindAmount("defence", entry.Defence));
			if (!KingdomBenefitProviderRules.TryPositiveTags(
				KingdomQol.DeclaredProvides(entry.Key), out Row.AcceptedTags, out Failure))
				return false;
			bool underground = Z != null && KingdomPlotRules.IsUnderground(Z.Z);
			for (int i = 0; i < Row.Cells.Count; i++)
			{
				if ((Row.Cells[i].Use & KingdomBenefitCellUse.Building) == 0) continue;
				string[] physical = StructuralTags(Row.Cells[i].Cover, underground);
				for (int t = 0; t < physical.Length; t++)
					if (!Row.AcceptedTags.Contains(physical[t])) Row.AcceptedTags.Add(physical[t]);
			}
			if (Row.Caps.Count > KingdomDesignationRules.MaxCapsPerDesignation
				|| Row.AcceptedTags.Count > KingdomDesignationRules.MaxTagsPerDesignation)
				return Fail("designation catalogue contract exceeds its row bound", out Failure);
			return true;
		}

		private static bool AppendExactYardCaps(KingdomBenefitDesignation Row, Zone Z,
			Dictionary<string, int> Folded, out string Failure)
		{
			Failure = null;
			if (KingdomConstruction.FindExactId(Z, Row.RootId, out GameObject root)
				!= KingdomPhysicalLookupState.Exact || !GameObject.Validate(root)
				|| !ReferenceEquals(root.CurrentZone, Z)) return true;
			string yardKey = root.GetStringProperty(KingdomYards.YardKeyProperty);
			if (string.IsNullOrEmpty(yardKey)) return true;
			if (!KingdomYards.TryReadHouse(root, out KingdomRules.BuildEntry house,
				out KingdomPlotRules.PlotSpec plot, out _)
				|| house.Key != Row.BuildingKey
				|| !KingdomYardRules.IsEligibleDesign(plot.Size, plot.Open, house.Category)
				|| string.IsNullOrEmpty(Row.LotId)
				|| root.GetStringProperty(KingdomPlots.PlotIdProperty) != Row.LotId)
				return Fail("designation has malformed yard-work authority", out Failure);
			if (!KingdomYards.TryGetSpec(yardKey, out KingdomYardRules.YardWorkSpec spec)
				|| spec == null || spec.Shades == null)
				return Fail("designation names unknown yard-work authority", out Failure);
			for (int i = 0; i < spec.Shades.Count; i++)
			{
				KindAmount row = spec.Shades[i];
				if (row.Amount <= 0 || row.Kind == KingdomCatalogueRules.SupportFood
					|| row.Kind == KingdomCatalogueRules.SupportWater) continue;
				Folded.TryGetValue(row.Kind, out int prior);
				long next = (long)prior + row.Amount;
				Folded[row.Kind] = next >= int.MaxValue ? int.MaxValue : (int)next;
			}
			return true;
		}
	}
}
