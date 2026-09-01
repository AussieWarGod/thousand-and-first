using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private static bool HasCap(KingdomBenefitDesignation Row, string Kind)
		{
			for (int i = 0; i < Row.Caps.Count; i++) if (Row.Caps[i].Kind == Kind) return true;
			return false;
		}

		private static bool AcceptsTag(KingdomBenefitDesignation Row, string Tag)
		{
			for (int i = 0; i < Row.AcceptedTags.Count; i++) if (Row.AcceptedTags[i] == Tag) return true;
			return false;
		}

		private static int CreditAmount(Aggregate Row, KingdomBenefitDesignation Designation,
			string Kind, int Amount, out bool Limited)
		{
			Limited = false;
			int cap = 0;
			for (int i = 0; i < Designation.Caps.Count; i++)
				if (Designation.Caps[i].Kind == Kind) { cap = Designation.Caps[i].Amount; break; }
			if (cap <= 0 || Amount <= 0) { Limited = Amount > 0; return 0; }
			Row.Amounts.TryGetValue(Kind, out int prior);
			int room = cap > prior ? cap - prior : 0;
			int credited = Amount < room ? Amount : room;
			Limited = credited < Amount;
			if (credited > 0) Row.Amounts[Kind] = SaturatingAdd(prior, credited);
			return credited;
		}

		private static bool CreditTag(Aggregate Row, string Tag)
		{
			if (Row.Tags.Contains(Tag)) return false;
			Row.Tags.Add(Tag); return true;
		}

		private void AddStructuralTags(Zone Z)
		{
			bool underground = KingdomPlotRules.IsUnderground(Z.Z);
			foreach (var pair in ByIdentity)
			{
				Aggregate row = pair.Value;
				List<string> offered = new List<string>(); bool shellFault = false;
				for (int i = 0; i < row.Reading.Designation.Cells.Count; i++)
				{
					KingdomBenefitCell cell = row.Reading.Designation.Cells[i];
					if ((cell.Use & KingdomBenefitCellUse.Building) == 0) continue;
					KingdomDesignationMatch match = new KingdomDesignationMatch {
						Designation = row.Reading.Designation, Use = cell.Use,
						Cover = cell.Cover, X = cell.X, Y = cell.Y };
					if (cell.Cover != KingdomBenefitCover.Open && !ShellValid(row, match, Z))
					{
						shellFault = true; continue;
					}
					string[] tags = KingdomDesignationIndex.StructuralTags(cell.Cover, underground);
					for (int t = 0; t < tags.Length; t++)
						if (!offered.Contains(tags[t])) offered.Add(tags[t]);
				}
				bool expectsCover = AcceptsTag(row.Reading.Designation, KingdomQolRules.TagDark)
					|| AcceptsTag(row.Reading.Designation, KingdomQolRules.TagSky);
				if (offered.Count == 0 && !expectsCover && !shellFault) continue;
				offered.Sort(StringComparer.Ordinal);
				KingdomBenefitInspection inspection = new KingdomBenefitInspection {
					ProviderKey = "taf:physical-cover", DesignationIdentity = pair.Key,
					OperationPercent = offered.Count > 0 ? 100 : 0 };
				row.Reading.Providers.Add(inspection);
				TrackInspection(inspection, row.Reading.Designation.RootId + "#physical-cover",
					"structural-cover|" + pair.Key);
				for (int i = 0; i < offered.Count; i++)
				{
					string tag = offered[i]; inspection.Tags.Add(tag);
					if (!AcceptsTag(row.Reading.Designation, tag))
						inspection.OutsideDesignationContract = true;
					else if (CreditTag(row, tag)) inspection.CreditedTags.Add(tag);
					else inspection.SaturatedByDesignation = true;
				}
				inspection.LimitedByDesignation = inspection.OutsideDesignationContract
					|| inspection.SaturatedByDesignation;
				if (inspection.CreditedTags.Count == 0 && inspection.OutsideDesignationContract)
					Fault(inspection, KingdomBenefitFault.UnacceptedBenefit,
						"physical cover does not fit this building role's accepted qualities");
				else if (inspection.CreditedTags.Count == 0 && inspection.SaturatedByDesignation)
					Fault(inspection, KingdomBenefitFault.ProviderCap,
						"this physical cover quality is already supplied");
				else if (inspection.CreditedTags.Count == 0 && (shellFault || expectsCover))
					Fault(inspection, KingdomBenefitFault.WrongScope,
						"current physical structure proves no accepted cover quality");
				else if (shellFault) inspection.Detail =
					"some covered cells fail current physical shell proof";
			}
		}

		private void AddStructuralDefence(Zone Z)
		{
			foreach (var pair in ByIdentity)
			{
				Aggregate row = pair.Value;
				if (!HasCap(row.Reading.Designation, "defence")
					|| row.Reading.Designation.ProviderId != "taf.architecture") continue;
				KingdomBenefitInspection inspection = new KingdomBenefitInspection {
					ProviderKey = "taf:physical-shell", DesignationIdentity = pair.Key };
				row.Reading.Providers.Add(inspection);
				TrackInspection(inspection,
					row.Reading.Designation.RootId + "#physical-shell",
					"structural-defence|" + pair.Key);
				if (!GameObject.Validate(row.Root) || row.Root.IsBroken())
				{
					Fault(inspection, KingdomBenefitFault.Inoperable,
						"defensive structure root is absent or broken"); continue;
				}
				if (!KingdomArchitectureStamper.TryVerifyBenefitShell(row.Root, Z,
					out string shellFailure))
				{
					Fault(inspection, KingdomBenefitFault.Inoperable,
						shellFailure ?? "defensive shell is incomplete"); continue;
				}
				int physical = KingdomBenefitEmbodimentRules.StructuralShellCells(
					row.Reading.Designation.Cells);
				inspection.Offered.Add(new KindAmount("defence", physical));
				if (physical <= 0)
				{
					Fault(inspection, KingdomBenefitFault.WrongScope,
						"designation has no proved defensive shell cells"); continue;
				}
				// Includes current staffing stretch, condition/wear, and identity affinity.
				int effectiveness = KingdomWear.EffectivenessOf(row.Root);
				int active = KingdomBenefitEmbodimentRules.OperationalStructureAmount(
					physical, effectiveness);
				if (active <= 0)
				{
					Fault(inspection, KingdomBenefitFault.Inoperable,
						"defensive structure has no current crew effectiveness"); continue;
				}
				inspection.Detail = "physical shell " + physical + "; operational "
					+ effectiveness + "%";
				int credited = CreditAmount(row, row.Reading.Designation,
					"defence", active, out bool limited);
				inspection.OperationPercent = effectiveness;
				inspection.LimitedByDesignation = limited;
				inspection.SaturatedByDesignation = limited;
				if (credited > 0) inspection.Credited.Add(new KindAmount("defence", credited));
				else Fault(inspection, KingdomBenefitFault.ProviderCap,
					"the designation's defence cap is already supplied");
			}
		}
	}
}
