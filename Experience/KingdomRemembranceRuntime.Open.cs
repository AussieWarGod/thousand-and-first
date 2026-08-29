using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRemembranceRuntime
	{
		public static void Open(KingdomSystem System, GameObject Founder)
		{
			Zone zone = Founder?.CurrentZone;
			if (Founder == null || !Founder.IsPlayer() || zone == null)
			{
				Popup.Show("Stand on the held ground of the city whose dead you mean to remember.");
				return;
			}
			if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(System, Now(),
				out string failure)) { Popup.Show(failure); return; }
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone) ?? KingdomSurvey.Take(zone, System);
			if (!TryReconcile(System, zone, survey, out failure))
			{
				Popup.Show("The remembrance needs exact recovery. Nothing foreign was changed.\n\n"
					+ (failure ?? "Return to its recorded ground and ask again.")); return;
			}
			if (!TryContext(System, zone, survey, out CityContext context, out failure))
			{
				Popup.Show(failure); return;
			}
			if (!KingdomExperienceRules.TryGetRemembrance(System.Experience,
				context.SettlementId, out KingdomRemembranceReceipt receipt, out failure))
			{
				Popup.Show(failure); return;
			}
			if (receipt == null)
			{
				Popup.Show("This city has no directly witnessed remembrance opportunity. "
					+ "Older or remote death rows are not promoted after the fact.");
				return;
			}
			if (receipt.Phase != KingdomRemembrancePhase.Eligible)
			{
				ShowExisting(receipt); return;
			}
			if (!TryExactDeath(context, receipt.SubjectResidentId, out DeathChoice subject)
				|| subject.Row.Name != receipt.SubjectName)
			{
				Popup.Show("The directly witnessed remembrance no longer matches its exact terminal "
					+ "resident row. Nothing was changed."); return;
			}
			if (!TryMourner(context, out Simulation.City.KingdomResidentRow mourner))
			{
				Popup.Show("The witnessed remembrance remains eligible, but no exact living named resident is "
					+ "present to offer remembrance. Nothing expires."); return;
			}
			KingdomExperienceRuntime.TryRecord(System, KingdomExperienceExperiment.Memorial,
				KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceObservationKind.Viewed, 1);
			ChooseDisposition(System, context, subject, mourner);
		}

		private static void ChooseDisposition(KingdomSystem System, CityContext Context,
			DeathChoice Subject, Simulation.City.KingdomResidentRow Mourner)
		{
			List<GameObject> fixtures = new List<GameObject>();
			for (int i = 0; i < Context.Survey.Cairns.Count; i++)
				if (Unlinked(Context.Survey, Context.Survey.Cairns[i]))
					fixtures.Add(Context.Survey.Cairns[i]);
			string intro = KingdomPresentation.Rich(Mourner.Name) + " remembers "
				+ KingdomPresentation.Rich(Subject.Row.Name) + ", of "
				+ (string.IsNullOrEmpty(Subject.Row.Origin) ? "an unrecorded origin" : Subject.Row.Origin)
				+ ", who " + KingdomOfficeRules.CauseClause(Subject.Cause)
				+ ". Deferral never expires and decline changes no standing or death truth. "
				+ "Choosing a fixture records that exact fixture's durable physical identity.";
			if (fixtures.Count == 0)
			{
				string key = FixtureDesign(System, Context);
				int pick = Popup.PickOption(Title: "Remember "
					+ KingdomPresentation.Rich(Subject.Row.Name), Intro: intro + "\n\n"
					+ "No completed unlinked fixture stands here. " + FixtureCost(key),
					Options: new string[] { "Commission that fixture now", "Defer without limit",
						"Decline this remembrance" }, Hotkeys: new char[] { 'c', 'd', 'x' },
					AllowEscape: true);
				if (pick == 0)
				{
					if (!KingdomCommission.Commission(System, key, out string failure))
						Popup.Show(failure ?? "The disclosed commission was refused.");
				}
				else if (pick == 2)
				{
					if (!TryDecline(System, Context, Subject, Mourner, out string failure))
						Popup.Show(failure); else Popup.Show("The remembrance is declined. The death row "
							+ "is unchanged, and no prompt or penalty will follow.");
				}
				return;
			}
			string[] options = new string[fixtures.Count + 2];
			for (int i = 0; i < fixtures.Count; i++) options[i] = FixtureLine(fixtures[i]);
			options[fixtures.Count] = "Defer without limit";
			options[fixtures.Count + 1] = "Decline this remembrance";
			int chosen = Popup.PickOption(Title: "Remember "
				+ KingdomPresentation.Rich(Subject.Row.Name), Intro: intro,
				Options: options, AllowEscape: true);
			if (chosen >= 0 && chosen < fixtures.Count)
			{
				if (!TryDedicate(System, Context, Subject, Mourner, fixtures[chosen],
					out string failure)) Popup.Show(failure);
			}
			else if (chosen == fixtures.Count + 1)
			{
				if (!TryDecline(System, Context, Subject, Mourner, out string failure))
					Popup.Show(failure); else Popup.Show("The remembrance is declined. The death row "
						+ "is unchanged, and no prompt or penalty will follow.");
			}
		}

		private static void ShowExisting(KingdomRemembranceReceipt R)
		{
			string line = R.Phase == KingdomRemembrancePhase.Declined
				? "This remembrance was declined. The terminal row remains unchanged."
				: R.Phase == KingdomRemembrancePhase.Projected
					? "A remembrance for " + KingdomPresentation.Rich(R.SubjectName)
						+ " stands at " + R.CarrierZoneId + "."
					: R.Phase == KingdomRemembrancePhase.Lost
						? "The remembrance carrier for " + KingdomPresentation.Rich(R.SubjectName)
							+ " is lost. The permanent row remains; no replacement was duplicated."
						: "The remembrance receipt is quarantined or awaiting exact projection: "
							+ (R.Fault ?? "return to its recorded ground");
			Popup.Show(line);
		}

		private static string FixtureLine(GameObject Item)
		{
			return Item.ShortDisplayName + (Item.CurrentCell == null ? ""
				: " {{K|[" + Item.CurrentCell.X + "," + Item.CurrentCell.Y + "]}}");
		}

		private static string FixtureDesign(KingdomSystem System, CityContext C)
		{
			if (KingdomZoning.StratumOf(C.Zone.ZoneID)) return "nichetomb";
			return System != null && System.Style == "verdant" ? "gravegrove" : "cairn";
		}

		private static string FixtureCost(string Key)
		{
			if (Key == "gravegrove")
				return "A grave-grove costs 5 drams, 3 timber and 2 mud through the ordinary commission.";
			if (Key == "nichetomb")
				return "A niche tomb costs 5 drams and 5 stone through the ordinary commission.";
			return "A settler's cairn costs 5 drams and 5 stone through the ordinary commission.";
		}
	}
}
