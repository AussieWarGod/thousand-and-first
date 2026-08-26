using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		private static void OpenChronicleAndDynasty(KingdomSystem System)
		{
			int chapter = Popup.PickOption(
				Title: "The Chronicle and dynasty of " + KingdomPresentation.Rich(System.KingdomDisplayName),
				Options: new string[2] { "Read the Chronicle", "Dynasty and retirement" },
				Hotkeys: new char[2] { 'c', 'd' }, AllowEscape: true);
			if (chapter == 0)
			{
				Popup.Show(KingdomReports.Chronicle(System));
			}
			else if (chapter == 1)
			{
				OpenDynastyChapter(System);
			}
		}

		private static void OpenDynastyChapter(KingdomSystem System)
		{
			KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
			if (seal == null)
			{
				Popup.Show("The profile seal is unavailable. Nothing has been retired.");
				return;
			}
			bool retired = !string.IsNullOrEmpty(seal.CurrentLegacyId)
				&& string.Equals(seal.RetiredLegacyId, seal.CurrentLegacyId,
					StringComparison.Ordinal);
			string generation = seal.CurrentGeneration == 0
				? "the founder's generation" : "generation " + seal.CurrentGeneration;
			bool paused = !KingdomMaster.NewWorkAllowed(System);
			int choice = Popup.PickOption(
				Title: "The dynasty of " + KingdomPresentation.Rich(System.KingdomDisplayName),
				Intro: retired
					? "This generation is already an immutable legacy in your profile. The current save continues, but play cannot rewrite that retired generation."
					: "You are playing " + generation + ". Retirement seals this generation without ending the current save.",
				Options: new string[1]
				{
					retired ? "{{G|This generation is already retired}}"
						: (paused ? "{{K|Retirement is paused by the master option}}"
							: "Retire this generation into the profile legacy")
				}, AllowEscape: true);
			if (choice != 0 || retired || paused)
			{
				return;
			}
			if (Popup.ShowYesNo("Retire " + generation + " of {{C|" + KingdomPresentation.Rich(System.KingdomDisplayName)
				+ "}}?\n\nThis writes an immutable legacy to your profile. The current save continues, but future play cannot rewrite this generation's legacy.") != DialogResult.Yes)
			{
				return;
			}
			if (Popup.ShowYesNo("Seal this generation now?\n\nThe profile legacy cannot be withdrawn or changed. This save remains playable; a later succession begins a new generation instead of rewriting the retired one.") != DialogResult.Yes)
			{
				return;
			}
			string failure;
			if (!KingdomSeal.TryRetireGeneration(out failure))
			{
				Popup.Show("Retirement did not finish. The current save continues. The seal may hold a pending retirement, but no completed profile legacy is being reported; return here to retry.\n\n"
					+ (string.IsNullOrEmpty(failure) ? "The profile seal could not be completed." : failure));
				return;
			}
			KingdomGovernanceScope.Commit("retire generation");
			Popup.Show("{{G|This generation is retired.}} Its immutable legacy is written to your profile. The current save continues, and later play cannot rewrite what was sealed.");
		}

	}
}
