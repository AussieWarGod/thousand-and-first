using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRuntime
	{
		internal const string RefusalEventProperty = "r_TAF_LabRefusalEvent_v1";
		internal const string RefusalOwnerProperty = "r_TAF_LabRefusalOwner_v1";
		internal const string RefusalDigestProperty = "r_TAF_LabRefusalDigest_v1";

		internal static void Observe(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			bool allowNew = KingdomMaster.NewWorkAllowed(System);
			if (System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID)) return;
			if (!TryCanonicalOwner(System, Z, Survey, allowNew, out GameObject owner,
				out r_KingdomLabCivicFriction part, out _, out string failure))
			{
				if (!string.IsNullOrEmpty(failure)) KingdomLog.Log("lab civic: " + failure);
				return;
			}
			Reconcile(System, Z, Survey, owner, part);
			if (!allowNew) return;
			if (Empty(part.SavantPrice))
			{
				KingdomLabCivicReceipt savant = PrepareSavantPrice(System, Z, Survey, owner);
				if (savant != null)
				{
					part.Stamp(savant); RecordOpen(System, part, savant.Kind); return;
				}
			}
			if (Empty(part.RefusalDeparture))
			{
				KingdomLabCivicReceipt leaving = PrepareRefusalDeparture(System, Z, Survey, owner);
				if (leaving != null) StartDeparture(System, Z, Survey, owner, part, leaving);
			}
		}

		private static bool StartDeparture(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			GameObject Owner, r_KingdomLabCivicFriction Part, KingdomLabCivicReceipt Receipt)
		{
			GameObject resident = Survey.FindCitizen(Receipt.SubjectResidentId);
			if (!ExactDepartureCause(System, Z, Survey, Owner, resident, Receipt,
				out _, out string failure)) return false;
			if (!CanStampMarker(resident, Receipt, out failure)) return false;
			Part.Stamp(Receipt);
			if (TryCompleteDepartureProjection(System, Z, Survey, Owner, Part,
				resident, Receipt, out failure)) return true;
			KingdomLog.Log("lab civic departure commit remains retryable: " + failure);
			return false;
		}

		internal static bool RefusesHome(KingdomSystem System, Zone Z, GameObject Resident,
			GameObject Home, out string RefusedTag)
		{
			RefusedTag = null;
			string eventId = Resident?.GetStringProperty(RefusalEventProperty);
			if (string.IsNullOrEmpty(eventId)) return false;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z);
			if (!TryCanonicalOwner(System, Z, survey, false, out GameObject owner,
				out r_KingdomLabCivicFriction part, out _, out _))
			{
				RefusedTag = "an unresolved laboratory cause"; return true;
			}
			KingdomLabCivicReceipt receipt = part.RefusalDeparture;
			if (!KingdomLabCivicRules.Valid(receipt, out _)
				|| receipt.Phase != KingdomLabCivicPhase.Active
				|| !MarkerMatches(Resident, receipt)
				|| KingdomResidentsId(Resident) != receipt.SubjectResidentId)
			{
				RefusedTag = "a quarantined laboratory cause"; return true;
			}
			string[] authored = KingdomQolRules.ParseTags(Resident.GetPropertyOrTag(
				KingdomQolRules.RefusesTagName, ""));
			if (!TryPhysicalOffer(survey, owner, out string[] offer, out _))
			{
				RefusedTag = "an unresolved physical laboratory benefit"; return true;
			}
			bool stands = KingdomQolRules.Has(authored, receipt.RefusedTag)
				&& KingdomQolRules.Has(offer, receipt.RefusedTag);
			if (!stands) return false;
			RefusedTag = receipt.RefusedTag;
			return KingdomReach.Reaches(System, Z, owner, Home);
		}

		internal static void ObserveRehoused(KingdomSystem System, Zone Z,
			GameObject Resident, string PlotId)
		{
			if (string.IsNullOrEmpty(PlotId) || string.IsNullOrEmpty(
				Resident?.GetStringProperty(RefusalEventProperty))) return;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z);
			if (!TryCanonicalOwner(System, Z, survey, false, out GameObject owner,
				out r_KingdomLabCivicFriction part, out _, out _)) return;
			KingdomLabCivicReceipt receipt = part.RefusalDeparture;
			if (KingdomLabCivicRules.Valid(receipt, out _)
				&& receipt.Phase == KingdomLabCivicPhase.Active
				&& receipt.SubjectResidentId == KingdomResidentsId(Resident)
				&& MarkerMatches(Resident, receipt)
				&& LawfullyRehoused(System, Z, owner, Resident, receipt, PlotId))
				Close(System, Z, part, receipt, KingdomLabCivicClosure.Rehoused, Resident);
		}

		internal static void ObserveDeparture(KingdomSystem System, Zone Z,
			GameObject Resident, int ResidentId)
		{
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z);
			if (!TryCanonicalOwner(System, Z, survey, false, out _,
				out r_KingdomLabCivicFriction part, out _, out _)) return;
			KingdomLabCivicReceipt receipt = part.RefusalDeparture;
			if (KingdomLabCivicRules.Valid(receipt, out _)
				&& receipt.Phase == KingdomLabCivicPhase.Active
				&& receipt.SubjectResidentId == ResidentId)
				Close(System, Z, part, receipt, KingdomLabCivicClosure.Departed, null);
		}

		private static bool LawfullyRehoused(KingdomSystem System, Zone Z,
			GameObject Owner, GameObject Resident, KingdomLabCivicReceipt Receipt,
			string ReportedPlot)
		{
			return GameObject.Validate(Owner) && Receipt != null
				&& !string.IsNullOrEmpty(ReportedPlot)
				&& !string.Equals(ReportedPlot, Receipt.SourcePlotId,
					StringComparison.Ordinal)
				&& KingdomLodging.TryLabHome(Z, Resident, out GameObject home,
					out string currentPlot)
				&& string.Equals(currentPlot, ReportedPlot, StringComparison.Ordinal)
				&& !KingdomReach.Reaches(System, Z, Owner, home);
		}

		private static int KingdomResidentsId(GameObject Resident)
		{
			return Simulation.City.KingdomResidents.IdOf(Resident);
		}

		private static long Now()
		{
			return The.Game == null ? 0L : Math.Max(0L, The.Game.TimeTicks);
		}

		private static bool Empty(KingdomLabCivicReceipt Receipt)
		{
			return Receipt == null || Receipt.Kind == KingdomLabCivicKind.None;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
