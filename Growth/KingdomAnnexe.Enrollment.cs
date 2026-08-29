using System;
using System.Collections.Generic;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomAnnexe
	{
#if TAF_TESTS
		internal static Action<string> EnrollmentFaultInjection;
#endif

		private static void EnrollmentCut(string cut)
		{
#if TAF_TESTS
			EnrollmentFaultInjection?.Invoke(cut);
#endif
		}

		/// <summary>
		/// The whole cost, then the answer, then the act. The disclosure is not a courtesy: it is
		/// the &sect;1.5 lesson applied, which is that what players will not forgive is a
		/// consequence nobody told them about.
		/// </summary>
		private static void Offer(KingdomSystem Realm, GameObject Building, GameObject Actor, GameObject Who)
		{
			string city = CityAt(Realm, Building);
			string named = PlainName(Who);
			string shownCity = KingdomPresentation.Rich(city);
			string shownName = KingdomPresentation.Rich(named);
			KingdomEnrolVerdict verdict = JudgeFor(Realm, Building, Who);
			if (verdict != KingdomEnrolVerdict.Allowed)
			{
				Popup.Show(KingdomAnnexeRules.RefusalLine(verdict, shownName, shownCity,
					StoredWater(Realm, Building)));
				return;
			}
			int consent = Popup.PickOption(
				Title: "Enter " + shownName + " on the rolls of " + shownCity,
				Intro: KingdomAnnexeRules.DisclosureLines(shownCity),
				Options: KingdomAnnexeRules.ConsentOptions, AllowEscape: true, RespectOptionNewlines: true);
			if (consent != 0)
			{
				return;
			}
			Enrol(Realm, Building, Who);
		}

		/// <summary>
		/// Takes the water, writes the roll, grants the licenses the terminal budgets in, pays the
		/// standing, and puts the part on the body.
		/// <para>
		/// <b>The verdict is asked AGAIN here</b>, for the reason the lab states one lane over: a
		/// founder may have opened this screen, walked away, come back a season later and had the
		/// answer change under them. A commit that trusts the screen that opened it will one day
		/// take a city's water for a thing it cannot do.
		/// </para>
		/// </summary>
		private static void Enrol(KingdomSystem Realm, GameObject Building, GameObject Who)
		{
			string city = Realm.SeatName;
			string named = PlainName(Who);
			string shownCity = KingdomPresentation.Rich(city);
			string shownName = KingdomPresentation.Rich(named);
			KingdomEnrolVerdict verdict = JudgeFor(Realm, Building, Who);
			if (verdict != KingdomEnrolVerdict.Allowed)
			{
				Popup.Show(KingdomAnnexeRules.RefusalLine(verdict, shownName, shownCity,
					StoredWater(Realm, Building)));
				return;
			}
			string id = Who.GeneID;
			if (string.IsNullOrEmpty(id) || KingdomAnnexeRules.EnrolmentKey(id) == null)
			{
				// Hostile-input discipline (STANDARDS 9): an identity that could not survive the
				// store disables one enrolment and says so, rather than writing a key that would
				// corrupt the city's whole roster.
				Popup.Show("The register cannot get a clean hand on " + shownName
					+ ". Nothing was written and nothing was spent.");
				return;
			}
			Zone zone = Building?.CurrentZone;
			KingdomSurvey survey = (zone == null) ? null : KingdomSurvey.Take(zone, Realm);
			KingdomWaterDebit debit;
			if (survey == null || !survey.TryReserveExactWater(KingdomAnnexeRules.EnrolmentDrams, out debit))
			{
				Popup.Show(KingdomAnnexeRules.RefusalLine(KingdomEnrolVerdict.Unpaid,
					shownName, shownCity, StoredWater(Realm, Building)));
				return;
			}

			// One transaction, in reversible-first order. KeepersRoster, the person's enrolment
			// part, licenses and standing all have exact snapshots. Water is committed only after
			// those snapshots exist; any refusal or exception restores every one of them and the
			// same physical vessels named by this survey's receipt.
			// Force the one-time legacy migration, then copy only physically stored entries. Roster
			// also derives citizens' origins; persisting that view would turn temporary population
			// knowledge into permanent keeper knowledge.
			KingdomZoning.Roster(Realm);
			List<string> roster = KingdomZoningRules.DecodeRoster(Realm.KeepersRoster);
			string roll = KingdomAnnexeRules.EnrolmentKey(id);
			string oldRoster = Realm.KeepersRoster;
			List<string> proposedRoster = new List<string>(roster);
			proposedRoster.Add(roll);
			string proposedEncodedRoster;
			if (roll == null || roster.Contains(roll)
				|| !KingdomZoningRules.TryEncodeRoster(proposedRoster, out proposedEncodedRoster))
			{
				Popup.Show("The keepers' permanent register has no bounded room for another name. "
					+ "Nothing was written and nothing was spent.");
				return;
			}
			r_KingdomEnrolled oldRecord = Who.GetPart<r_KingdomEnrolled>();
			string oldWho = (oldRecord == null) ? null : oldRecord.Who;
			string oldNamed = (oldRecord == null) ? null : oldRecord.Named;
			string oldCity = (oldRecord == null) ? null : oldRecord.City;
			long oldTick = (oldRecord == null) ? 0L : oldRecord.Tick;
			bool oldLapse = oldRecord != null && oldRecord.LapseAnnounced;
			string oldPurposePair = oldRecord?.PurposePairId;
			long oldPurposeEpoch = oldRecord?.PurposePairEpoch ?? 0L;
			string oldPurposeOperation = oldRecord?.PurposeOperationId;
			string oldPurposeAuthority = oldRecord?.PurposeAuthorityId;
			bool hadLicenses = Who.HasIntProperty(LicenseProperty);
			int oldLicenses = Who.GetIntProperty(LicenseProperty);
			List<KeyValuePair<string, int>> standing = KingdomAnnexeRules.StandingCost();
			if (!Realm.TryCaptureRegardLedger(out KingdomRegardLedgerSnapshot oldStanding))
			{
				debit.Rollback();
				Popup.Show("The realm's standing ledger is not canonical. Nothing was written and "
					+ "nothing was spent.");
				return;
			}
			try
			{
				if (!debit.Commit())
				{
					if (!debit.RestorationExact) Realm.QuarantineIdentity(
						"annexe water refusal could not prove exact vessel restoration");
					Popup.Show(debit.RestorationExact
						? KingdomAnnexeRules.RefusalLine(KingdomEnrolVerdict.Unpaid,
							shownName, shownCity,
							KingdomSurvey.Take(zone, Realm).StoredWater)
						: "The ceremony could not prove its casks exact. Civic work is "
							+ "quarantined; inspect the stores.");
					return;
				}
				if (roll == null || roster.Contains(roll))
				{
					throw new InvalidOperationException("The roll changed before it could be written.");
				}
				Realm.KeepersRoster = proposedEncodedRoster;
				if (!KingdomAnnexeRules.Enrolled(KingdomZoning.Roster(Realm), id))
				{
					throw new InvalidOperationException("The register did not retain the enrolment.");
				}

				r_KingdomEnrolled record = Who.RequirePart<r_KingdomEnrolled>();
				record.Who = id;
				record.Named = named;
				record.City = city;
				record.Tick = (The.Game != null) ? The.Game.TimeTicks : 0L;
				record.LapseAnnounced = false;
				if (TryPurposeEnrollmentIntent(Building, Who,
					out KingdomPurposeBodyAuthority purpose))
				{
					record.PurposePairId = purpose.PairId;
					record.PurposePairEpoch = purpose.PairEpoch;
					record.PurposeOperationId = purpose.OperationId;
					record.PurposeAuthorityId = purpose.AuthorityId;
				}
				else
				{
					record.PurposePairId = "";
					record.PurposePairEpoch = 0L;
					record.PurposeOperationId = "";
					record.PurposeAuthorityId = "";
				}
				Who.ModIntProperty(LicenseProperty, KingdomAnnexeRules.EnrolmentLicenses);
				if (!Realm.TryAdjustRegardForRealmBatch(standing, mirror: false))
					throw new InvalidOperationException(
						"The realm could not publish the complete standing cost.");
				EnrollmentCut("annexe:standings");
			}
			catch (Exception ex)
			{
				// Water first: no engine callback from compensating body/standing state may strand a
				// physical debit by throwing before the same-vessel receipt is restored.
				bool waterRestored = false;
				bool standingRestored = false;
				try { waterRestored = debit.Rollback() || debit.RestorationExact; }
				catch (Exception waterEx) { KingdomLog.Log(
					"annexe: water compensation threw (" + waterEx.Message + ")"); }
				try { standingRestored = Realm.TryRestoreRegardLedger(oldStanding); }
				catch (Exception standingEx) { KingdomLog.Log(
					"annexe: standing compensation threw (" + standingEx.Message + ")"); }
				bool rosterRestored = false;
				bool recordRestored = false;
				bool licensesRestored = false;
				try
				{
					Realm.KeepersRoster = oldRoster ?? "";
					rosterRestored = Realm.KeepersRoster == (oldRoster ?? "");
				}
				catch { rosterRestored = false; }
				try
				{
					if (oldRecord == null)
					{
						Who.RemovePart("r_KingdomEnrolled");
						recordRestored = Who.GetPart<r_KingdomEnrolled>() == null;
					}
					else if (ReferenceEquals(Who.GetPart<r_KingdomEnrolled>(), oldRecord))
					{
						oldRecord.Who = oldWho;
						oldRecord.Named = oldNamed;
						oldRecord.City = oldCity;
						oldRecord.Tick = oldTick;
						oldRecord.LapseAnnounced = oldLapse;
						oldRecord.PurposePairId = oldPurposePair ?? "";
						oldRecord.PurposePairEpoch = oldPurposeEpoch;
						oldRecord.PurposeOperationId = oldPurposeOperation ?? "";
						oldRecord.PurposeAuthorityId = oldPurposeAuthority ?? "";
						recordRestored = oldRecord.Who == oldWho && oldRecord.Named == oldNamed &&
							oldRecord.City == oldCity && oldRecord.Tick == oldTick &&
							oldRecord.LapseAnnounced == oldLapse &&
							oldRecord.PurposePairId == (oldPurposePair ?? "") &&
							oldRecord.PurposePairEpoch == oldPurposeEpoch &&
							oldRecord.PurposeOperationId == (oldPurposeOperation ?? "") &&
							oldRecord.PurposeAuthorityId == (oldPurposeAuthority ?? "");
					}
				}
				catch { recordRestored = false; }
				try
				{
					if (hadLicenses) Who.SetIntProperty(LicenseProperty, oldLicenses);
					else Who.RemoveIntProperty(LicenseProperty);
					licensesRestored = Who.HasIntProperty(LicenseProperty) == hadLicenses &&
						(!hadLicenses || Who.GetIntProperty(LicenseProperty) == oldLicenses);
				}
				catch { licensesRestored = false; }
				bool restored = waterRestored && standingRestored && rosterRestored &&
					recordRestored && licensesRestored;
				if (!restored)
				{
					try { Realm.QuarantineIdentity(
						"annexe enrolment compensation did not restore every exact snapshot"); }
					catch (Exception quarantineEx) { KingdomLog.Log(
						"annexe: quarantine threw (" + quarantineEx.Message + ")"); }
				}
				KingdomLog.Log("annexe: enrolment transaction refused for " + id + " (" + ex.Message
					+ "; exact compensation=" + restored + ")");
				Popup.Show(restored
					? "The book would not take the entry. Nothing remains on the rolls and the "
						+ "ceremony's water was returned to its casks."
					: "The enrolment could not restore every exact snapshot. Civic work is "
						+ "quarantined; inspect the rolls, body, standings, and stores.");
				return;
			}

			// External faction mirrors and authored telling happen after the durable core. A broken
			// notification cannot turn a completed enrolment into a second charge on retry.
			for (int i = 0; i < standing.Count; i++)
			{
				Realm.MirrorFeeling(standing[i].Key);
			}
			MessageQueue.AddPlayerMessage(KingdomAnnexeRules.DoneLine(shownName, shownCity));
			KingdomChronicle.Record(Realm,
				KingdomAnnexeRules.DoneTelling(shownName, shownCity), Accomplishment: true);
			Realm.RecordDeed(KingdomAnnexeRules.DoneTelling(shownName, shownCity));
			KingdomLog.Log("annexe: enrolled " + id + " (" + named + ") at " + city);
			Speak(Realm);
		}

	}
}
