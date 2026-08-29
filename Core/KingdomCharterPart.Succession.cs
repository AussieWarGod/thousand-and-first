using System;
using XRL;
using XRL.UI;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		private static void OpenSuccessionCustom(KingdomSystem System)
		{
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Popup.Show("Settlement simulation is paused by the master option. Resume the realm before changing its succession custom.");
				return;
			}
			KingdomSuccession succession = The.Game?.GetSystem<KingdomSuccession>();
			KingdomSuccessionConfiguration current;
			string failure = null;
			if (succession == null || !succession.TryGetCurrentConfiguration(System,
				out current, out failure))
			{
				Popup.Show(failure ?? "The succession record is unavailable.");
				return;
			}
			string currentLine;
			if (!succession.TryDescribeCurrentSuccession(System, out currentLine, out failure))
			{
				Popup.Show(failure ?? "The succession custom could not be read.");
				return;
			}
			int choice = Popup.PickOption(
				Title: "Succession custom of " + KingdomPresentation.Rich(System.KingdomDisplayName),
				Intro: currentLine,
				Options: new string[3]
				{
					current.Choice == HeirChoice.Groomed
						? "Seniority — revoke grooming and restore the oldest eligible resident"
						: "Seniority — longest-serving eligible resident",
					"Choose an exact eligible resident for the next life",
					current.Choice == HeirChoice.Groomed
						? "Review, replace, or revoke the groomed successor"
						: "Groom a resident as the realm's lawful successor"
				},
				Hotkeys: new char[3] { 's', 'c', 'g' }, AllowEscape: true);
			if (choice < 0) return;
			if (choice == 0)
			{
				PreviewAndConfirmSuccession(System, succession, HeirChoice.Law, 0, true);
				return;
			}
			if (choice == 1)
			{
				ChooseSuccessionResident(System, succession, false);
				return;
			}
			if (current.Choice == HeirChoice.Groomed)
				ReviewGroomedSuccession(System, succession);
			else
				ChooseSuccessionResident(System, succession, true);
		}

		private static void ChooseSuccessionResident(KingdomSystem System,
			KingdomSuccession Succession, bool Groomed)
		{
			KingdomSuccession.SuccessionResidentView[] residents;
			string failure;
			if (!Succession.TryGetSuccessionResidents(System, out residents, out failure))
			{
				Popup.Show(failure);
				return;
			}
			if (residents.Length == 0)
			{
				Popup.Show("No eligible resident is on either city's roll.");
				return;
			}
			string[] labels = new string[residents.Length];
			for (int i = 0; i < labels.Length; i++) labels[i] = Groomed
				? residents[i].GroomingLabel : residents[i].Label;
			int pick = Popup.PickOption(
				Title: Groomed ? "Who should the realm prepare?" : "Who carries the next life?",
				Intro: Groomed
					? "Identity is the resident number. Service is proved by a month on the roll or office; schooling by the city's schooling knowledge and this resident's knowledge post."
					: "Identity is the resident number, not the name. Homes, cities, and tenure are shown for recognition.",
				Options: labels, AllowEscape: true);
			if (pick < 0 || pick >= residents.Length) return;
			if (Groomed)
			{
				PreviewAndConfirmSuccession(System, Succession, HeirChoice.Groomed,
					residents[pick].ResidentId, true);
				return;
			}
			int cost = Popup.PickOption(
				Title: "Who keeps the Charter?",
				Intro: "The default price preserves the realm's seniority law: you wake as the chosen citizen under the senior heir, and must earn trusted regard before claiming the Charter.",
				Options: new string[2]
				{
					"Senior heir keeps the Charter {{G|[default]}}",
					"Chosen citizen inherits the Charter {{K|[sandbox]}}"
				},
				Hotkeys: new char[2] { 'k', 'i' }, AllowEscape: true);
			if (cost < 0) return;
			PreviewAndConfirmSuccession(System, Succession, HeirChoice.Chosen,
				residents[pick].ResidentId, cost == 0);
		}

		private static void ReviewGroomedSuccession(KingdomSystem System,
			KingdomSuccession Succession)
		{
			string status;
			string failure;
			if (!Succession.TryDescribeCurrentSuccession(System, out status, out failure))
			{
				Popup.Show(failure);
				return;
			}
			int choice = Popup.PickOption(
				Title: "Groomed succession of " + KingdomPresentation.Rich(System.KingdomDisplayName),
				Intro: status,
				Options: new string[2]
				{
					"Nominate a different resident",
					"Revoke the nomination and restore seniority"
				}, Hotkeys: new char[2] { 'n', 'r' }, AllowEscape: true);
			if (choice == 0)
				ChooseSuccessionResident(System, Succession, true);
			else if (choice == 1)
				PreviewAndConfirmSuccession(System, Succession, HeirChoice.Law, 0, true);
		}

		private static void PreviewAndConfirmSuccession(KingdomSystem System,
			KingdomSuccession Succession, HeirChoice Choice, int ResidentId,
			bool SeatCostEnabled)
		{
			string preview;
			string failure;
			if (!Succession.TryDescribeSuccessionCustom(System, Choice, ResidentId,
				SeatCostEnabled, out preview, out failure))
			{
				Popup.Show(failure);
				return;
			}
			if (Popup.ShowYesNo(preview
				+ "\n\nWrite this exact custom into the Charter? This changes one durable law revision and records it in the Chronicle.")
				!= DialogResult.Yes) return;
			if (!Succession.TryChangeSuccessionCustom(System, Choice, ResidentId,
				SeatCostEnabled, out failure))
			{
				Popup.Show(failure);
				return;
			}
			KingdomGovernanceScope.Commit("set succession custom");
			Popup.Show("{{G|The succession custom is written.}}"
				+ (string.IsNullOrEmpty(failure) ? "" : "\n\n" + failure));
		}
	}
}
