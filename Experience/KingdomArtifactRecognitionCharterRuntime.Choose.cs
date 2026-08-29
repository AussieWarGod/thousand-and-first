using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The choosing half: which object, which form of recognition, and whose voice. Every step here
	/// is a question, and cancelling any of them leaves the save byte-identical.
	/// </summary>
	public static partial class KingdomArtifactRecognitionCharterRuntime
	{
		/// <summary>The living roll of exactly this settlement, capped at what one page can name.</summary>
		internal const int MaxAttributionRows = 60;

		private static void Recognize(Ground Ground, KingdomCivicMemorySectionLease Lease,
			KingdomCivicArtifactsEnvelope Held)
		{
			// Capacity is not asked here. A register with all eight rows kept can still answer an
			// exact repeat of something it already holds, and refusing at the door would turn a
			// free retry into a dead end. The transition decides: it refuses a genuinely new row
			// and hands back the existing one, without ever giving up a row to make space.
			if (!KingdomArtifactRecognitionSelectionRuntime.TryCollectNearby(Ground.Founder,
				out List<KingdomArtifactRecognitionChoice> choices, out int unidentified,
				out string failure))
			{
				Popup.Show(KingdomPresentation.Rich(failure));
				return;
			}
			if (choices.Count == 0)
			{
				Popup.Show("Put the one thing you want remembered at your feet or beside you. "
					+ "Nothing is taken from you, and nothing you are carrying is looked at."
					+ Unnamed(unidentified));
				return;
			}
			string[] options = new string[choices.Count];
			for (int i = 0; i < choices.Count; i++) options[i] = choices[i].Label;
			int pick = Popup.PickOption(
				Title: "Recognize one thing, in "
					+ KingdomPresentation.Rich(Ground.SettlementName),
				Intro: "The object stays exactly where it is and exactly whose it is. The city "
					+ "writes a sentence; it does not take a keepsake."
					+ (Held.Recognitions.Rows.Count >= KingdomArtifactRecognitionRules.MaxRows
						? "\n\n{{K|All " + KingdomArtifactRecognitionRules.MaxRows + " recognitions "
							+ "are kept. Nothing already written will be given up to make room, so "
							+ "only something the city has already recorded can be confirmed "
							+ "again.}}"
						: "")
					+ Unnamed(unidentified),
				Options: options, AllowEscape: true);
			if (pick < 0 || pick >= choices.Count) return;
			GameObject selected = choices[pick].Object;
			if (!TryChooseKind(out KingdomArtifactRecognitionKind kind)) return;
			if (!TryChooseAttribution(Ground, out int residentId, out string residentName)) return;
			Disclose(Ground, Lease, Held, selected, kind, residentId, residentName);
		}

		private static bool TryChooseKind(out KingdomArtifactRecognitionKind Kind)
		{
			Kind = KingdomArtifactRecognitionKind.None;
			int pick = Popup.PickOption(Title: "How should it be remembered?",
				Intro: "None of these is worth anything, and none of them is the thing itself.",
				Options: new string[3]
				{
					"A remark, spoken and written down",
					"An inscription, kept in the city's hand",
					"A fixed representation, of no commerce value"
				},
				Hotkeys: new char[3] { 'r', 'i', 'f' }, AllowEscape: true);
			switch (pick)
			{
			case 0: Kind = KingdomArtifactRecognitionKind.Remark; return true;
			case 1: Kind = KingdomArtifactRecognitionKind.Inscription; return true;
			case 2: Kind = KingdomArtifactRecognitionKind.Representation; return true;
			default: return false;
			}
		}

		/// <summary>
		/// Whose voice the sentence carries. The city speaking for itself is the first option and a
		/// complete answer; a named settler must be on this settlement's living roll right now.
		/// </summary>
		private static bool TryChooseAttribution(Ground Ground, out int ResidentId,
			out string ResidentName)
		{
			ResidentId = 0;
			ResidentName = null;
			List<KingdomResidentRow> roll = KingdomResidents.RollRows(Ground.System);
			int rows = roll.Count > MaxAttributionRows ? MaxAttributionRows : roll.Count;
			string[] options = new string[rows + 1];
			options[0] = "{{K|The city itself, with no settler named}}";
			for (int i = 0; i < rows; i++)
				options[i + 1] = KingdomPresentation.Rich(roll[i].Name);
			int pick = Popup.PickOption(Title: "Who says so?",
				Intro: "A named settler is optional. Whoever is named must be on this city's roll "
					+ "when the words are written, and cannot be changed afterwards.",
				Options: options, AllowEscape: true);
			if (pick < 0 || pick > rows) return false;
			if (pick == 0) return true;
			KingdomResidentRow chosen = roll[pick - 1];
			if (chosen.ResidentId <= 0 || string.IsNullOrEmpty(chosen.Name))
			{
				Popup.Show("That settler has no exact roll identity to attribute anything to.");
				return false;
			}
			ResidentId = chosen.ResidentId;
			ResidentName = chosen.Name;
			return true;
		}

		/// <summary>
		/// Reads the object exactly, builds the durable row against a private copy, and shows the
		/// founder the exact words before asking. Saying no here changes nothing at all.
		/// </summary>
		private static void Disclose(Ground Ground, KingdomCivicMemorySectionLease Lease,
			KingdomCivicArtifactsEnvelope Held, GameObject Selected,
			KingdomArtifactRecognitionKind Kind, int ResidentId, string ResidentName)
		{
			if (!KingdomArtifactRecognitionSelectionRuntime.TrySnapshotNearby(Ground.Founder,
				Selected, null, null, Ground.Tick, out KingdomArtifactSnapshot snapshot,
				out string failure))
			{
				Popup.Show(KingdomPresentation.Rich(failure));
				return;
			}
			if (!ProveResident(Ground, ResidentId, ResidentName, out failure)
				|| !KingdomArtifactRecognitionCommit.TryPlan(Held, Ground.SettlementName, snapshot,
					Kind, ResidentId, ResidentName, Ground.Tick,
					out KingdomArtifactRecognitionPlan plan, out failure))
			{
				Popup.Show("Nothing was changed.\n\n" + KingdomPresentation.Rich(failure));
				return;
			}
			if (Popup.ShowYesNo(KingdomPresentation.Rich(plan.Disclosure())
				+ "\n\n{{W|Write exactly this into the city's memory?}}") != DialogResult.Yes)
				return;
			Commit(Ground, Lease, Selected, plan);
		}

		/// <summary>
		/// Says plainly that some nearby things were passed over because the world has never given
		/// them an identity, rather than letting them be silently missing from the page.
		/// </summary>
		private static string Unnamed(int Count)
		{
			if (Count <= 0) return "";
			return "\n\n{{K|" + Count + (Count == 1 ? " thing beside you has" : " things beside you "
				+ "have") + " never been given an exact identity by the world, so the city has "
				+ "nothing to name " + (Count == 1 ? "it" : "them") + " by. Asking for one would "
				+ "create it, so they are left alone and cannot be recognized.}}";
		}

		/// <summary>
		/// Whether the named settler is still exactly this settlement's, asked against the roll
		/// rather than against what the popup remembered a moment ago.
		/// </summary>
		internal static bool ProveResident(Ground Ground, int ResidentId, string ResidentName,
			out string Failure)
		{
			List<KingdomResidentRow> roll = KingdomResidents.RollRows(Ground.System);
			List<int> ids = new List<int>();
			List<string> names = new List<string>();
			for (int i = 0; i < roll.Count; i++)
			{
				ids.Add(roll[i].ResidentId);
				names.Add(roll[i].Name);
			}
			return KingdomArtifactRecognitionAttribution.TryProveResident(ResidentId,
				ResidentName, ids, names, out Failure);
		}
	}
}
