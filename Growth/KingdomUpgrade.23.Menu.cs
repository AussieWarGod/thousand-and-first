using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		public static void ShowImprovements(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			if (!Enabled)
			{
				Popup.Show("The settlement is not bettering its own works. (That module is switched off in the options.)");
				return;
			}
			Zone zone = The.Player?.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Your works are looked over on the kingdom's own ground.");
				return;
			}
			while (true)
			{
				KingdomSurvey survey = KingdomSurvey.Take(zone, System);
				int freeHands = System.Population - System.AssignedCrew;
				if (freeHands < 0)
				{
					freeHands = 0;
				}
				List<GameObject> works = new List<GameObject>();
				List<Assessment> assessments = new List<Assessment>();
				List<string> lines = new List<string>();
				bool otherWorkUnderway = false;
				for (int i = 0; i < survey.Improvements.Count; i++)
				{
					GameObject item = survey.Improvements[i];
					r_KingdomImprovement improvement = item.GetPart<r_KingdomImprovement>();
					if ((improvement != null && improvement.Working) || HasActiveConstruction(item))
					{
						otherWorkUnderway = true;
					}
				}
				for (int i = 0; i < survey.Built.Count; i++)
				{
					GameObject item = survey.Built[i];
					Assessment assessment = Assess(System, zone, item, survey, freeHands, otherWorkUnderway);
					if (!assessment.Valid || assessment.Verdict == KingdomUpgradeRules.UpgradeVerdict.NoSuccessor)
					{
						continue;
					}
					works.Add(item);
					assessments.Add(assessment);
					lines.Add(EntryLine(item, assessment));
				}
				bool groundHeld = IsGroundHeld(zone);
				lines.Add(groundHeld
					? "{{W|Let this ground improve itself again}}"
					: "{{K|Leave this ground as it is}}");
				if (works.Count == 0)
				{
					Popup.Show("Nothing standing here is built to grow into anything else yet.");
					return;
				}
				int picked = Popup.PickOption(Title: "The works of " + KingdomPresentation.Rich(System.SeatName), Intro: "Pick a work to leave as it is, or to let grow again.", Options: lines, AllowEscape: true);
				if (picked < 0)
				{
					return;
				}
				if (picked >= works.Count)
				{
					SetGroundHeld(zone, !groundHeld);
					KingdomGovernanceScope.Commit("set ground improvements");
					return;
				}
				Assessment picking = assessments[picked];
				if (KingdomRelocation.CanOffer(System, zone, works[picked], picking))
				{
					KingdomRelocation.OpenHeartRingCall(System, zone, works[picked], picking);
					return;
				}
				r_KingdomImprovement held = works[picked].RequirePart<r_KingdomImprovement>();
				held.Held = !held.Held;
				held.AnnouncedReason = 0;
				KingdomGovernanceScope.Commit("set work improvement");
				return;
			}
		}

		/// <summary>One work's line in the Charter listing: what it is, what it would become, and
		/// its state or the reason it has none.</summary>
		public static string EntryLine(GameObject Work, Assessment A)
		{
			string name = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string successor = (A.Successor != null) ? A.Successor.Name : DisplayNameOf(A.SuccessorKey);
			switch (A.Verdict)
			{
			case KingdomUpgradeRules.UpgradeVerdict.AlreadyWorking:
				return "{{G|" + name + "}} - being raised into " + successor;
			case KingdomUpgradeRules.UpgradeVerdict.Ready:
				return "{{G|" + name + "}} - ready to be raised into " + successor + " for {{C|" + A.CostDrams + " drams}}";
			case KingdomUpgradeRules.UpgradeVerdict.NoGroundToGrow:
				if (KingdomPlots.IsHeartPlot(Work) && A.Reason != null
					&& A.Reason.IndexOf("marked to yield", StringComparison.Ordinal) >= 0)
					return "{{W|" + name + "}} - " + (A.Reason ?? "its next ring needs ground")
						+ " Pick it to review the complete ring-call plan.";
				return "{{K|" + name + "}} - " + (A.Reason ?? ("would become " + successor));
			case KingdomUpgradeRules.UpgradeVerdict.NotOurWork:
				return "{{K|" + name + "}} - yours, not the settlement's. It is left exactly as you made it.";
			case KingdomUpgradeRules.UpgradeVerdict.StyleForbids:
				return "{{K|" + name + "}} - " + successor + " is not built in a city of this kind";
			default:
				return "{{K|" + name + "}} - " + (A.Reason ?? ("would become " + successor));
			}
		}
	}
}
