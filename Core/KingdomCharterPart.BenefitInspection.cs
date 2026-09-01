using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		/// <summary>Reads one exact active-zone snapshot. No identities are minted, no remote
		/// zone is loaded, and no governance publication is made.</summary>
		private void InspectBuildingBenefits(KingdomSystem System)
		{
			Zone zone = ParentObject?.CurrentZone;
			if (System == null || zone == null || !System.OwnedZone(zone.ZoneID))
			{
				Popup.Show("Building benefits can only be inspected on exact loaded realm ground.");
				return;
			}
			KingdomBenefitIndex index;
			string failure;
			KingdomSurvey survey = KingdomSurvey.TakeCustodyOnly(zone);
			using (KingdomSurvey.PassScope pass = survey.BindPass())
				if (!survey.TryBenefits(out index, out failure))
				{
					Popup.Show("Physical benefits are unavailable. Nothing changed.\n\n"
						+ KingdomPresentation.Rich(failure)); return;
				}
			IReadOnlyList<KingdomBenefitReading> readings = index.Readings;
			List<KingdomBenefitInspection> loose = Loose(index.Inspections);
			if (readings.Count == 0 && loose.Count == 0)
			{
				Popup.Show("No building space is designated on this ground."); return;
			}
			while (true)
			{
				string[] options = new string[readings.Count + (loose.Count > 0 ? 1 : 0)];
				for (int i = 0; i < readings.Count; i++) options[i] = KingdomPresentation.Rich(
					KingdomBenefitInspectionText.BuildingLabel(readings[i],
						ExactName(zone, readings[i].Designation.RootId,
							readings[i].Designation.BuildingKey)));
				if (loose.Count > 0) options[options.Length - 1] = "{{r|Unassigned or source faults: "
					+ loose.Count + "}}";
				int pick = Popup.PickOption(Title: "Physical benefits on "
					+ KingdomPresentation.Rich(System.SeatName),
					Intro: "Choose designated building space. Capacity is only a ceiling; "
						+ "the listed furnishings are what count now.",
					Options: options, AllowEscape: true);
				if (pick < 0 || pick >= options.Length) return;
				if (pick < readings.Count) OpenBenefitBuilding(zone, readings[pick]);
				else OpenLooseBenefits(zone, loose);
			}
		}

		private static void OpenBenefitBuilding(Zone Zone, KingdomBenefitReading Reading)
		{
			string name = ExactName(Zone, Reading.Designation.RootId,
				Reading.Designation.BuildingKey);
			string intro = KingdomPresentation.Rich(
				KingdomBenefitInspectionText.BuildingDetail(Reading, name));
			if (Reading.Providers == null || Reading.Providers.Count == 0)
			{
				Popup.Show(intro); return;
			}
			while (true)
			{
				string[] options = new string[Reading.Providers.Count];
				for (int i = 0; i < options.Length; i++)
				{
					KingdomBenefitInspection row = Reading.Providers[i];
					options[i] = KingdomPresentation.Rich(KingdomBenefitInspectionText.ProviderLabel(
						row, ExactProviderName(Zone, row)));
				}
				int pick = Popup.PickOption(Title: KingdomPresentation.Rich(name), Intro: intro,
					Options: options, AllowEscape: true);
				if (pick < 0 || pick >= options.Length) return;
				KingdomBenefitInspection selected = Reading.Providers[pick];
				Popup.Show(KingdomPresentation.Rich(KingdomBenefitInspectionText.ProviderDetail(
					selected, ExactProviderName(Zone, selected))));
			}
		}

		private static void OpenLooseBenefits(Zone Zone,
			IList<KingdomBenefitInspection> Rows)
		{
			while (true)
			{
				string[] options = new string[Rows.Count];
				for (int i = 0; i < Rows.Count; i++) options[i] = KingdomPresentation.Rich(
					KingdomBenefitInspectionText.ProviderLabel(Rows[i],
						ExactProviderName(Zone, Rows[i])));
				int pick = Popup.PickOption(Title: "Unassigned physical-benefit faults",
					Intro: "These providers could not be bound to one exact designated building.",
					Options: options, AllowEscape: true);
				if (pick < 0 || pick >= Rows.Count) return;
				Popup.Show(KingdomPresentation.Rich(KingdomBenefitInspectionText.ProviderDetail(
					Rows[pick], ExactProviderName(Zone, Rows[pick]))));
			}
		}

		private static List<KingdomBenefitInspection> Loose(
			IReadOnlyList<KingdomBenefitInspection> Rows)
		{
			List<KingdomBenefitInspection> result = new List<KingdomBenefitInspection>();
			for (int i = 0; i < Rows.Count; i++)
				if (string.IsNullOrEmpty(Rows[i].DesignationIdentity)) result.Add(Rows[i]);
			return result;
		}

		private static string ExactProviderName(Zone Zone, KingdomBenefitInspection Row)
		{
			string identity = Row?.ProviderIdentity;
			int suffix = identity?.IndexOf('#') ?? -1;
			return ExactName(Zone, suffix > 0 ? identity.Substring(0, suffix) : identity,
				Row?.ProviderKey);
		}

		private static string ExactName(Zone Zone, string Id, string Fallback)
		{
			if (!string.IsNullOrEmpty(Id) && Id[0] != '<'
				&& KingdomConstruction.FindExactId(Zone, Id, out GameObject exact)
					== KingdomPhysicalLookupState.Exact && GameObject.Validate(exact)
				&& exact.CurrentZone == Zone) return exact.ShortDisplayNameStripped;
			return string.IsNullOrEmpty(Fallback) ? "physical provider" : Fallback;
		}
	}
}
