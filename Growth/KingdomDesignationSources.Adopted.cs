using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomDesignationSources
	{
		internal static bool TryAdopted(Zone Z, KingdomSurvey Survey,
			List<KingdomBenefitDesignation> Rows, List<string> Faults, out string Failure)
		{
			Failure = null;
			if (Z == null || Survey == null || Rows == null || Faults == null)
				return Fail("adopted designation source has no active survey", out Failure);
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject root = Survey.Built[i];
				if (root.GetIntProperty(KingdomAdopt.AdoptedProperty) != 1) continue;
				if (!root.HasIntProperty(KingdomAdoptionDesignation.SchemaProperty))
				{
					string key = root.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
					if (KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry old)
						&& HasBenefit(root, old))
						SourceFault(Faults, "legacy adoption " + (root.IDIfAssigned ?? "<unassigned>")
							+ " has no exact room receipt; release and adopt it again");
					continue;
				}
				if (!KingdomAdoptionDesignation.TryRead(root,
					out KingdomAdoptionDesignationReceipt receipt, out string rowFailure))
				{
					SourceFault(Faults, "adoption designation refused: " + rowFailure); continue;
				}
				if (receipt.ZoneId != Z.ZoneID
					|| receipt.BuildingKey != root.GetStringProperty(KingdomAdopt.AdoptedKeyProperty))
				{
					SourceFault(Faults, "adoption designation moved ground or changed civic role"); continue;
				}
				if (!TryReproveAdoption(root, Z, receipt, out rowFailure))
				{
					SourceFault(Faults, "adoption designation paused: " + rowFailure); continue;
				}
				KingdomBenefitDesignation row = new KingdomBenefitDesignation {
					ProviderId = "taf.adoption", ProviderVersion = "d2",
					Identity = "adopt:" + receipt.RootId, Revision = receipt.Revision,
					ZoneId = receipt.ZoneId, RootId = receipt.RootId,
					BuildingKey = receipt.BuildingKey,
					LotId = root.GetStringProperty(KingdomPlots.PlotIdProperty) ?? ""
				};
					KingdomBenefitCellUse use = KingdomBenefitCellUse.Plot
						| (receipt.OpenPlot ? KingdomBenefitCellUse.Yard
							: KingdomBenefitCellUse.Building);
					if (!receipt.ContainerOnly && !receipt.OpenPlot)
						use |= KingdomBenefitCellUse.Interior | KingdomBenefitCellUse.Covered;
				for (int c = 0; c < receipt.Cells.Count; c++)
					row.Cells.Add(new KingdomBenefitCell(receipt.Cells[c].X,
								receipt.Cells[c].Y, use, receipt.ContainerOnly || receipt.OpenPlot
							? KingdomBenefitCover.Open : KingdomBenefitCover.ObservedEnclosure));
				if (!KingdomDesignationIndex.CompleteForSource(row, Z, out rowFailure))
				{
					SourceFault(Faults, "adoption designation refused: " + rowFailure); continue;
				}
				if (!SourceRow(Rows, row, Faults)) break;
			}
			return true;
		}

		private static bool TryReproveAdoption(GameObject Root, Zone Z,
			KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Root) || Root.CurrentCell == null || Root.CurrentZone != Z)
				return Fail("adoption designation marker is absent from its exact ground", out Failure);
			if (!string.IsNullOrEmpty(Receipt.ForeignProviderId)
				&& !KingdomForeignFootprints.TryReprove(Z, Receipt, out Failure)) return false;
			return KingdomAdoptionDesignation.TryReproveLocal(Root, Receipt, out Failure);
		}

		private static bool HasBenefit(GameObject Root, KingdomRules.BuildEntry Entry)
		{
			return Entry != null && (!string.IsNullOrWhiteSpace(Entry.Carries)
				|| !string.IsNullOrWhiteSpace(KingdomQol.DeclaredProvides(Entry.Key))
				|| Entry.Defence > 0 || Root.GetIntProperty("KingdomDefence") > 0
				|| Root.GetIntProperty("KingdomStaffNeeded") > 0);
		}
	}
}
