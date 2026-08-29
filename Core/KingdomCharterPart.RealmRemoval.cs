using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		private static void OpenRealmRemoval(KingdomSystem System, GameObject Founder)
		{
			if (System == null || Founder == null || !GameObject.Validate(Founder)
				|| !Founder.IsPlayer())
			{
				Popup.Show("The current realm-removal authority is unavailable. Nothing changed.");
				return;
			}
			if (string.IsNullOrEmpty(System.RealmRetirementWire))
			{
				OfferRealmRemovalPlan(System); return;
			}
			if (!System.TryReadRealmRetirement(out KingdomRealmRetirementState state,
				out string failure) || state == null)
			{
				Popup.Show("The retained realm-removal receipt is unreadable. It was left unchanged, "
					+ "and no cleanup was attempted.\n\n" + (failure ?? "No readable receipt."));
				return;
			}
			ContinueRealmRemoval(System, Founder, state);
		}

		private static void OfferRealmRemovalPlan(KingdomSystem System)
		{
			if (!KingdomRealmRetirementAuthority.TryInspect(System,
				out KingdomRealmRetirementReport report,
				out List<KingdomRemovalLocator> _, out string failure))
			{
				Popup.Show((report?.Render() ?? "Removal inspection failed closed.")
					+ "\n\n" + (failure ?? "Nothing changed.")); return;
			}
			if (!report.CanBegin)
			{
				Popup.Show(report.Render()); return;
			}
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Popup.Show(report.Render()
					+ "\n\nThe master option is paused. Resume ordinary settlement work before "
					+ "starting this terminal plan."); return;
			}
			int pick = Popup.PickOption(Title: "Prepare this save for mod removal",
				Intro: report.Render(), Options: new[]
				{
					"Begin the attended removal plan", "Close without changing anything"
				}, Hotkeys: new[] { 'b', 'x' }, AllowEscape: true);
			if (pick != 0) return;
			if (Popup.ShowYesNo("Begin terminal preparation for this save?\n\nThis immediately "
				+ "stops new realm work. You must visit every listed ground normally and explicitly "
				+ "clean only TAF-owned projections. There is no remote cleanup and no promise that "
				+ "an old save can become perfectly mod-free.") != DialogResult.Yes) return;
			if (Popup.ShowYesNo("Confirm the irreversible removal plan.\n\nKeep the mod enabled "
				+ "until the Charter reports preparation complete. Back up the save first. Ordinary "
				+ "items, bodies, and completed history are preserved.") != DialogResult.Yes) return;
			if (!KingdomRealmRetirementAuthority.TryBegin(System, out _, out report,
				out failure))
			{
				Popup.Show("The plan did not begin.\n\n" + (failure ?? report?.Render()
					?? "Nothing changed.")); return;
			}
			KingdomGovernanceScope.Commit("begin attended realm removal");
			Popup.Show("{{G|The attended removal plan is now active.}}\n\n"
				+ report.Render() + "\n\nTravel normally to each listed ground, then return to "
				+ "Dynasty and retirement. No ground is loaded or changed at a distance.");
		}

		private static void ContinueRealmRemoval(KingdomSystem System, GameObject Founder,
			KingdomRealmRetirementState State)
		{
			KingdomRealmRetirementReport report =
				KingdomRealmRetirementAuthority.FromState(State);
			if (State.Phase == KingdomRealmRetirementPhase.Quarantined)
			{
				Popup.Show(report.Render() + "\n\nThe receipt is quarantined. No cleanup was guessed.");
				return;
			}
			Zone zone = Founder.CurrentZone;
			KingdomRemovalLocator active = FindLocator(State, zone?.ZoneID);
			bool cleanHere = State.Phase == KingdomRealmRetirementPhase.CleaningGround
				&& active != null && active.State != KingdomRemovalLocatorState.Cleaned;
			bool finalize = report.OutstandingGround.Count == 0
				&& (State.Phase == KingdomRealmRetirementPhase.CleaningGround
					|| State.Phase == KingdomRealmRetirementPhase.ReadyForFence
					|| State.Phase == KingdomRealmRetirementPhase.FenceCommitted
					|| State.Phase == KingdomRealmRetirementPhase.PreparedForRemoval);
			List<string> options = new List<string>();
			List<char> keys = new List<char>();
			if (cleanHere)
			{
				options.Add(active.State == KingdomRemovalLocatorState.OutstandingVisit
					? "Clean exact TAF-owned projections on this ground"
					: "Retry exact cleanup on this contested ground"); keys.Add('c');
			}
			if (finalize)
			{
				options.Add("Finalize known projections and write the removal fence"); keys.Add('f');
			}
			options.Add("Close"); keys.Add('x');
			int pick = Popup.PickOption(Title: "Attended realm removal",
				Intro: report.Render() + ActiveGroundText(active, zone),
				Options: options.ToArray(), Hotkeys: keys.ToArray(), AllowEscape: true);
			if (pick < 0 || pick == options.Count - 1) return;
			if (cleanHere && pick == 0)
			{
				CleanCurrentRemovalGround(System, zone); return;
			}
			if (finalize) FinalizeRealmRemoval(System);
		}

		private static void CleanCurrentRemovalGround(KingdomSystem System, Zone Zone)
		{
			if (Popup.ShowYesNo("Clean this exact loaded ground?\n\nOnly objects, parts, and "
				+ "properties proven to belong to this realm are changed. Foreign or ambiguous "
				+ "evidence refuses; ordinary people and item value are preserved.")
				!= DialogResult.Yes) return;
			if (!KingdomRealmRetirementAuthority.TryCleanActiveGround(System, Zone,
				out _, out KingdomRealmRetirementReport report, out string failure))
			{
				Popup.Show("Cleanup did not complete. Exact recovery evidence was retained.\n\n"
					+ (failure ?? report?.Render() ?? "Nothing was reported as clean.")); return;
			}
			KingdomGovernanceScope.Commit("clean attended realm ground");
			Popup.Show("{{G|This ground's exact realm projections are clean.}}\n\n"
				+ report.Render());
		}

		private static void FinalizeRealmRemoval(KingdomSystem System)
		{
			if (Popup.ShowYesNo("Finalize this save for mod removal?\n\nThis retires realm "
				+ "factions, converts or closes known projections, writes a permanent identity fence, "
				+ "and removes the Charter and TAF systems. This cannot be undone.")
				!= DialogResult.Yes) return;
			if (Popup.ShowYesNo("Final confirmation.\n\nAfter success: SAVE IMMEDIATELY, QUIT, "
				+ "then remove the mod before loading that save again. Legacy unknowns remain "
				+ "disclosed; this is not a clean-uninstall promise.") != DialogResult.Yes) return;
			if (!KingdomRealmRetirementAuthority.TryFinalizeForRemoval(System,
				out KingdomRealmRetirementReport report, out string failure))
			{
				Popup.Show("Final preparation did not complete. Keep the mod enabled and return to "
					+ "this workflow; exact recovery state was retained.\n\n"
					+ (failure ?? report?.Render() ?? "No terminal receipt was published.")); return;
			}
			KingdomGovernanceScope.Commit("finalize realm removal");
			Popup.Show("{{G|" + report.Summary + "}}");
		}

		private static KingdomRemovalLocator FindLocator(KingdomRealmRetirementState State,
			string ZoneId)
		{
			for (int i = 0; i < State.Locators.Count; i++)
				if (string.Equals(State.Locators[i].ZoneId, ZoneId,
					StringComparison.Ordinal)) return State.Locators[i];
			return null;
		}

		private static string ActiveGroundText(KingdomRemovalLocator Active, Zone Zone)
		{
			if (Zone == null) return "\n\n{{K|No loaded player ground can be cleaned here.}}";
			if (Active == null) return "\n\n{{K|This ground is outside the frozen removal plan.}}";
			return "\n\nCurrent ground: " + Zone.ZoneID + " [" + Active.State + "]";
		}
	}
}
