using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRuntime
	{
		internal static bool HandleSlate(KingdomSystem System, Zone Z, GameObject Building)
		{
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			if (!TryCanonicalOwner(System, Z, survey, false, out GameObject owner,
				out r_KingdomLabCivicFriction part, out _, out string failure)
				|| owner != Building) return false;
			ReconcileSavant(System, Z, survey, owner, part);
			KingdomLabCivicReceipt receipt = part.SavantPrice;
			if (Empty(receipt) || receipt.Phase == KingdomLabCivicPhase.Active
				|| receipt.Phase == KingdomLabCivicPhase.Closed) return false;
			if (!KingdomLabCivicRules.Valid(receipt, out failure)
				|| receipt.Phase == KingdomLabCivicPhase.Quarantined)
			{
				Popup.Show(KingdomLabCivicRules.StatusLine(receipt)); return true;
			}
			if (receipt.Phase == KingdomLabCivicPhase.ChoicePrepared)
			{
				int retry = Popup.PickOption(Title: "recover the exact rehouse",
					Intro: KingdomLabCivicRules.CauseLine(receipt) + "\n\n"
						+ KingdomLabCivicRules.RequestLine(receipt),
					Options: new string[] { "Retry the promised exact move.",
						"Leave the promise preserved." }, AllowEscape: true);
				if (retry == 0) TryResolveRehouse(System, Z, survey, part);
				return true;
			}
			int picked = Popup.PickOption(Title: "the savant's price",
				Intro: KingdomLabCivicRules.CauseLine(receipt) + "\n\n"
					+ KingdomLabCivicRules.RequestLine(receipt)
					+ "\n\nThis carries no reward, standing, value, or hidden grievance.",
				Options: new string[] { "Agree to the exact request.",
					"Refuse it; promise nothing.", "Leave it unanswered." },
				AllowEscape: true);
			if (picked < 0 || picked == 2) return true;
			if (!KingdomLabCivicRules.TryChoose(receipt, picked == 0, Now(),
				out KingdomLabCivicReceipt chosen, out failure))
			{
				Popup.ShowFail(failure); return true;
			}
			part.Stamp(chosen);
			KingdomGovernanceScope.Commit(picked == 0
				? "answer the savant's exact request" : "refuse the savant's exact request");
			if (chosen.Phase == KingdomLabCivicPhase.Closed)
			{
				RecordClose(System, part, chosen.Kind);
				Popup.Show(KingdomLabCivicRules.ClosureLine(part.SavantPrice));
			}
			else if (chosen.Request == KingdomLabCivicRequest.NeighbourRehoused)
				TryResolveRehouse(System, Z, survey, part);
			else Popup.Show("You granted the request. "
				+ KingdomLabCivicRules.RequestLine(chosen)
				+ " The choice is now immutable; it ends only when the exact lodged cause or hall owner is gone.");
			return true;
		}

		private static void TryResolveRehouse(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, r_KingdomLabCivicFriction Part)
		{
			KingdomLabCivicReceipt receipt = Part.SavantPrice;
			if (!KingdomLabCivicRules.Valid(receipt, out string failure)
				|| receipt.Phase != KingdomLabCivicPhase.ChoicePrepared)
			{
				Popup.ShowFail(failure ?? "No exact rehouse intent is prepared."); return;
			}
			GameObject neighbour = FindExact(Survey, receipt.TargetObjectId,
				out KingdomLabObjectMatch targetMatch);
			if (targetMatch == KingdomLabObjectMatch.Missing)
			{
				Close(System, Z, Part, receipt, KingdomLabCivicClosure.CauseGone, null);
				Popup.Show(KingdomLabCivicRules.ClosureLine(Part.SavantPrice)); return;
			}
			if (targetMatch == KingdomLabObjectMatch.Duplicate)
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt,
					"The exact named neighbour identity is duplicated on active ground."));
				Popup.ShowFail(KingdomLabCivicRules.StatusLine(Part.SavantPrice)); return;
			}
			if (!GameObject.Validate(neighbour)
				|| KingdomResidentsId(neighbour) != receipt.TargetResidentId
				|| neighbour.GetStringProperty("KingdomName") != receipt.TargetName)
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt,
					"The uniquely identified neighbour no longer matches the named resident receipt."));
				Popup.ShowFail(KingdomLabCivicRules.StatusLine(Part.SavantPrice)); return;
			}
			string held = neighbour.GetStringProperty(KingdomLodging.HomePlotIdProperty);
			if (held != receipt.SourcePlotId && held != receipt.TargetPlotId)
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt,
					"The neighbour moved to a third plot after the exact promise."));
				Popup.ShowFail(KingdomLabCivicRules.StatusLine(Part.SavantPrice)); return;
			}
			if (!KingdomLodging.TryApplyLabRehouse(System, Z, neighbour,
				receipt.SourcePlotId, receipt.TargetPlotId, receipt.TargetHomeObjectId,
				out failure))
			{
				Popup.ShowFail(failure + " The exact promise remains prepared; no substitute was chosen.");
				return;
			}
			if (!KingdomLabCivicRules.TryClose(receipt, KingdomLabCivicClosure.Rehoused,
				Now(), out KingdomLabCivicReceipt closed, out failure))
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt, failure));
				Popup.ShowFail(KingdomLabCivicRules.StatusLine(Part.SavantPrice)); return;
			}
			Part.Stamp(closed); RecordClose(System, Part, closed.Kind);
			Popup.Show(KingdomLabCivicRules.ClosureLine(Part.SavantPrice));
		}

		internal static string Status(GameObject Building)
		{
			r_KingdomLabCivicFriction part = Building?.GetPart<r_KingdomLabCivicFriction>();
			return part == null ? "" : KingdomLabCivicRules.StatusLine(part.SavantPrice);
		}

		internal static bool BlocksConsecration(KingdomSystem System, Zone Z,
			GameObject Target, out string Reason)
		{
			Reason = null;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			if (!TryCanonicalOwner(System, Z, survey, false, out GameObject owner,
				out r_KingdomLabCivicFriction part, out _, out _)) return false;
			ReconcileSavant(System, Z, survey, owner, part);
			KingdomLabCivicReceipt receipt = part.SavantPrice;
			if (receipt?.Request != KingdomLabCivicRequest.ShrineUnconsecrated
				|| receipt.TargetObjectId != Target?.IDIfAssigned
				|| receipt.Phase == KingdomLabCivicPhase.Closed) return false;
			if (!KingdomLabCivicRules.Valid(receipt, out string failure))
			{
				Reason = "This exact shrine is named by a malformed laboratory request. "
					+ "Inspect its canonical hall; no other shrine is affected."; return true;
			}
			string remedy = receipt.Phase == KingdomLabCivicPhase.Prepared
				? "Answer or refuse it at the canonical hall."
				: "The granted request remains until its exact lodged cause or hall owner is gone.";
			Reason = KingdomLabCivicRules.CauseLine(receipt) + " "
				+ KingdomLabCivicRules.RequestLine(receipt) + " Receipt " + receipt.EventId
				+ " / " + receipt.CauseDigest.Substring(0, 12)
				+ ". " + remedy + " No other faith action is blocked.";
			return true;
		}
	}
}
