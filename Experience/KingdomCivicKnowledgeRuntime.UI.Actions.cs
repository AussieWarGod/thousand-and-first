#if !TAF_TESTS
using XRL.UI;

namespace ThousandAndFirst
{
	internal static partial class KingdomCivicKnowledgeRuntime
	{
		private static void OpenCuriosity(KingdomSystem system,
			KingdomCivicMemorySystem memory, KingdomCuriosityReceipt row, bool paused)
		{
			if (row.State != KingdomCuriosityState.Available)
			{
				Popup.Show("This finite curation is " + CuriosityStatus(row)
					+ ". It stays quiet and cannot retarget."); return;
			}
			if (!TryReproveCuriosity(system, row, out string failure)
				|| !TryProveCuratorPresentation(system, row, out _, out _, out failure))
			{
				Popup.Show("The curation remains recorded, but its exact named curator and staffed "
					+ "civic locus are not present for presentation. Nothing changed.\n\n"
					+ KingdomPresentation.Rich(failure)); return;
			}
			if (!KingdomCuriosityRuntime.TryStanding(row,
				out KingdomCuriosityNoteStanding standing, out failure))
			{
				Popup.Show("The Journal could not be read exactly. Nothing changed.\n\n"
					+ KingdomPresentation.Rich(failure)); return;
			}
			if (standing == KingdomCuriosityNoteStanding.MissingOrChanged)
			{
				int invalid = Popup.PickOption(Title: "Curiosity Commons",
					Intro: "The exact Journal note frozen by this curation is now missing or "
						+ "changed. It will never retarget.",
					Options: new[] { "Record this curation as invalidated", "Leave it available" },
					Hotkeys: new[] { 'i', 'x' }, AllowEscape: true);
				if (invalid != 0) return;
				CloseCuriosity(system, memory, row, KingdomCuriosityState.Invalidated, paused);
				return;
			}
			int pick = Popup.PickOption(Title: "Curiosity Commons",
				Intro: KingdomPresentation.Rich(KingdomCuriosityRuntime.Rendering(row)),
				Options: new[] { "View this already-known destination", "Decline this curation",
					"Leave it available" }, Hotkeys: new[] { 'v', 'd', 'x' }, AllowEscape: true);
			if (pick == 0) CloseCuriosity(system, memory, row, KingdomCuriosityState.Viewed, paused);
			else if (pick == 1) CloseCuriosity(system, memory, row,
				KingdomCuriosityState.Declined, paused);
		}

		private static void CloseCuriosity(KingdomSystem system,
			KingdomCivicMemorySystem memory, KingdomCuriosityReceipt row,
			KingdomCuriosityState state, bool paused)
		{
			if (paused)
			{
				Popup.Show("Settlement simulation is paused. Curation remains readable, but its "
					+ "durable state was not changed."); return;
			}
			if (!KingdomCuriosityLeadTransactions.TryCloseCuriosity(memory, row.SourceId, state,
				Now(), out bool _, out KingdomCuriosityReceipt _, out string failure))
			{
				Popup.Show("The curation did not close. Nothing was reported as changed.\n\n"
					+ KingdomPresentation.Rich(failure)); return;
			}
			if (KingdomCuriosityLeadTransactions.TryRead(memory, out long _,
				out KingdomCuriosityBook fresh, out KingdomCivicLeadBook _, out string _))
				KingdomCuriosityRuntime.TryReleaseTerminalAttention(system.Experience, fresh,
					row.SourceId, out string _);
			Popup.Show("Curation recorded as " + state.ToString().ToLowerInvariant()
				+ ". No Journal entry, reward, or governance energy changed.");
		}

		private static void OpenLead(KingdomSystem system, KingdomCivicMemorySystem memory,
			long memoryRevision, KingdomCivicLeadBook leads, KingdomCivicLeadReceipt row,
			bool paused)
		{
			if (row.Phase != KingdomCivicLeadPhase.Prepared)
			{
				Popup.Show("This city-authored lead is " + LeadStatus(row)
					+ ". It has no repeat reward or continuing work."); return;
			}
			bool exact = TryReproveLead(system, row, out bool sourceMissing, out string failure);
			if (!exact && !sourceMissing)
			{
				Popup.Show("The physical source is not fully loaded for exact review. Nothing "
					+ "changed.\n\n" + KingdomPresentation.Rich(failure)); return;
			}
			if (sourceMissing)
			{
				int invalid = Popup.PickOption(Title: "City-authored lead",
					Intro: KingdomPresentation.Rich(failure),
					Options: new[] { "Record this lead as invalidated", "Leave it prepared" },
					Hotkeys: new[] { 'i', 'x' }, AllowEscape: true);
				if (invalid != 0) return;
				if (paused) { Popup.Show("Settlement simulation is paused. Nothing changed."); return; }
				if (!KingdomCuriosityLeadTransactions.TryInvalidateLead(memory, row.SourceId,
					out bool _, out failure))
				{ Popup.Show("Lead remains prepared.\n\n" + KingdomPresentation.Rich(failure)); return; }
				ReleaseLeadAttention(system, memory, row.SourceId);
				Popup.Show("Lead recorded as invalidated. No Journal entry was removed."); return;
			}
			int pick = Popup.PickOption(Title: "City-authored lead",
				Intro: KingdomPresentation.Rich(row.Title + "\n\n" + row.AuthoredReason
					+ "\n\nExact destination: " + row.Locator),
				Options: new[] { "Add this nontradable lead to the Journal", "Leave it prepared" },
				Hotkeys: new[] { 'a', 'x' }, AllowEscape: true);
			if (pick != 0) return;
			if (paused) { Popup.Show("Settlement simulation is paused. Nothing changed."); return; }
			if (!KingdomCivicLeadRuntime.TryProject(memory, memoryRevision, leads, row,
				out failure))
			{ Popup.Show("Lead remains prepared.\n\n" + KingdomPresentation.Rich(failure)); return; }
			ReleaseLeadAttention(system, memory, row.SourceId);
			Popup.Show("The exact nontradable destination is now in the Journal. No reward or "
				+ "governance energy was granted.");
		}

		private static void ReleaseLeadAttention(KingdomSystem system,
			KingdomCivicMemorySystem memory, string sourceId)
		{
			if (KingdomCuriosityLeadTransactions.TryRead(memory, out long _,
				out KingdomCuriosityBook _, out KingdomCivicLeadBook fresh, out string _))
				KingdomCivicLeadRuntime.TryReleaseTerminalAttention(system.Experience, fresh,
					sourceId, out string _);
		}
	}
}
#endif
