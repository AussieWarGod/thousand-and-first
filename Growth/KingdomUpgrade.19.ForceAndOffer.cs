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
		public static bool Force(KingdomSystem System, Zone Z, GameObject Work, Assessment A, KingdomSurvey Survey)
		{
			if (!TryPrepareImprovement(System, Z, Work, A, out PreparedImprovement prepared,
				out _)) return false;
			return ForcePrepared(System, Z, Work, A, Survey, prepared);
		}

		private static bool ForcePrepared(KingdomSystem System, Zone Z, GameObject Work,
			Assessment A, KingdomSurvey Survey, PreparedImprovement Prepared)
		{
			if (!A.Valid || !KingdomUpgradeRules.IsOffer(A.Verdict) || A.Successor == null)
			{
				return false;
			}
			Assessment consented = A;
			consented.Verdict = KingdomUpgradeRules.UpgradeVerdict.Ready;
			if (!BeginPrepared(System, Z, Work, consented, Survey, Prepared))
			{
				return false;
			}
			string standing = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string forced = KingdomUpgradeRules.ForcedLine(standing, A.Successor.Name, A.Margin);
			MessageQueue.AddPlayerMessage("{{W|" + forced + "}}");
			System.Ledger.Note("{{W|" + forced + "}}");
			KingdomChronicle.Record(System, "the " + standing + " at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " was set to be raised on the founder's word, and the settlement went into its reserve to do it");
			KingdomLog.Log("improvement forced: " + A.Key + " -> " + A.SuccessorKey + " outage=" + A.OutputLost + " margin=" + A.Margin);
			return true;
		}

		/// <summary>
		/// Puts one held offer to the founder with the dip disclosed BEFORE consent, and forces it
		/// only if they say so. Answers whether the work was started, so the caller knows the
		/// listing behind it is stale.
		/// </summary>
		public static bool OpenHeldOffer(KingdomSystem System, Zone Z, GameObject Work, Assessment A, KingdomSurvey Survey)
		{
			if (!KingdomUpgradeRules.IsOffer(A.Verdict))
			{
				return false;
			}
			string standing = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string successor = (A.Successor != null) ? A.Successor.Name : DisplayNameOf(A.SuccessorKey);
			if (!TryPrepareImprovement(System, Z, Work, A,
				out PreparedImprovement prepared, out string prepareFailure)
				|| prepared.Legacy || prepared.Architecture == null
				|| !KingdomArchitecturePreview.TryRenderImprovement(prepared.Architecture,
					A.Successor, A, prepared.Delta, out string preview, out prepareFailure))
			{
				Popup.Show(prepareFailure
					?? "This save-era improvement has no exact successor map to preview.");
				return false;
			}
			int picked = Popup.PickOption(
				Title: standing,
				Intro: preview + "\n" + KingdomUpgradeRules.DipLine(standing, successor,
					A.SupportPerDay, A.BuildTicks, A.Margin),
				Options: new string[2] { "Raise it anyway, and go into the reserve", "Leave it as it is for now" },
				AllowEscape: true);
			if (picked != 0)
			{
				return false;
			}
			return ForcePrepared(System, Z, Work, A, Survey, prepared);
		}
	}
}
