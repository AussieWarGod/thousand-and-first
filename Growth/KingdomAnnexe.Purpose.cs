using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomAnnexe
	{
		internal const string PurposeEnrollmentIntentProperty =
			"r_TAF_PurposeEnrollmentIntent";

		internal static bool TrySelectPurposeEnrollment(KingdomSystem Realm,
			GameObject Building, GameObject Actor, string PairId, long PairEpoch,
			string OperationId, out string ProcedureKey, out string Receipt,
			out string Quote, out string Failure)
		{
			ProcedureKey = null;
			Receipt = null;
			Quote = null;
			Failure = null;
			if (!GameObject.Validate(Building) || !Building.HasPart("r_KingdomBecomingAnnexe")
				|| !GameObject.Validate(Actor) || Realm == null)
				return Refuse("The exact becoming annexe or registrar is unavailable.", out Failure);
			List<GameObject> candidates = Candidates(Realm, Building, Actor);
			if (candidates.Count == 0)
				return Refuse("No eligible realm citizen stands within the annexe register's reach.",
					out Failure);
			List<string> rows = new List<string>();
			for (int i = 0; i < candidates.Count; i++)
				rows.Add("{{W|Enter " + KingdomPresentation.Rich(PlainName(candidates[i]))
					+ " on the rolls}}");
			int picked = Popup.PickOption(Title: "Choose the operation's authorization",
				Intro: KingdomAnnexeRules.DisclosureLines(
					KingdomPresentation.Rich(CityAt(Realm, Building)))
					+ "\n\nThis existing authorization is additional to the reciprocal recipe.",
				Options: rows, AllowEscape: true, RespectOptionNewlines: true);
			if (picked < 0) return false;
			GameObject subject = candidates[picked];
			KingdomPurposeBodyAuthority authority = new KingdomPurposeBodyAuthority
			{
				Kind = KingdomPurposeKind.Chrome, PairId = PairId, PairEpoch = PairEpoch,
				OperationId = OperationId, AuthorityId = Guid.NewGuid().ToString("N"),
				SubjectObjectId = subject.ID, SubjectGeneId = subject.GeneID,
				ProcedureKey = "annexe-enrolment", BodyPartId = 0, BearerId = "",
				WaterCost = KingdomAnnexeRules.EnrolmentDrams,
				BitCost = new KingdomMaterialDebitCost().ToClaimString(), PreservedCost = 0
			};
			Receipt = KingdomPurposeBodyAuthorityRules.Encode(authority);
			if (Receipt == null)
				return Refuse("The exact enrollment selection could not be encoded.", out Failure);
			ProcedureKey = authority.ProcedureKey;
			Quote = KingdomAnnexeRules.DisclosureLines(
				KingdomPresentation.Rich(CityAt(Realm, Building)));
			return true;
		}

		internal static bool TryPreflightPurposeEnrollment(KingdomSystem Realm,
			GameObject Building, KingdomPurposeBodyAuthority Authority, int PortfolioWater,
			out string Failure)
		{
			Failure = null;
			GameObject subject = GameObject.FindByID(Authority?.SubjectObjectId);
			if (!PurposeEnrollmentMatches(Building, subject, Authority)
				|| JudgeFor(Realm, Building, subject) != KingdomEnrolVerdict.Allowed)
				return Refuse("The frozen enrolment subject or annexe authorization changed.",
					out Failure);
			KingdomSurvey survey = KingdomSurvey.Take(Building.CurrentZone, Realm);
			return survey != null
				&& survey.StoredWater >= Authority.WaterCost + PortfolioWater
				|| Refuse("The stores cannot cover both enrollment and purpose water.", out Failure);
		}

		internal static KingdomPurposeBodyDriveState DrivePurposeEnrollment(KingdomSystem Realm,
			GameObject Building, KingdomPurposeBodyAuthority Authority, out string Failure)
		{
			Failure = null;
			GameObject subject = GameObject.FindByID(Authority?.SubjectObjectId);
			if (!PurposeEnrollmentMatches(Building, subject, Authority) || Realm == null)
				return Invalid("The frozen enrollment authority cannot resolve its exact subject.",
					out Failure);
			if (PurposeEnrollmentApplied(Realm, subject, Authority))
			{
				ClearPurposeIntent(Building, Authority);
				return KingdomPurposeBodyDriveState.Applied;
			}
			if (HeldBy(Realm, subject.GeneID))
				return Invalid("A different roll enrolled the exact subject before this operation.",
					out Failure);
			Building.SetStringProperty(PurposeEnrollmentIntentProperty,
				KingdomPurposeBodyAuthorityRules.Encode(Authority));
			Enrol(Realm, Building, subject);
			if (PurposeEnrollmentApplied(Realm, subject, Authority))
			{
				ClearPurposeIntent(Building, Authority);
				return KingdomPurposeBodyDriveState.Applied;
			}
			Failure = "The enrollment transaction did not complete; retry its exact preserved authority.";
			return KingdomPurposeBodyDriveState.Waiting;
		}

		internal static bool TryPurposeEnrollmentIntent(GameObject Building, GameObject Subject,
			out KingdomPurposeBodyAuthority Authority)
		{
			return KingdomPurposeBodyAuthorityRules.TryDecode(
				Building?.GetStringProperty(PurposeEnrollmentIntentProperty), out Authority)
				&& PurposeEnrollmentMatches(Building, Subject, Authority);
		}

		private static bool PurposeEnrollmentMatches(GameObject Building, GameObject Subject,
			KingdomPurposeBodyAuthority Authority)
		{
			return GameObject.Validate(Building) && Building.HasPart("r_KingdomBecomingAnnexe")
				&& GameObject.Validate(Subject) && Authority != null
				&& Authority.Kind == KingdomPurposeKind.Chrome
				&& Authority.WaterCost == KingdomAnnexeRules.EnrolmentDrams
				&& Subject.ID == Authority.SubjectObjectId
				&& Subject.GeneID == Authority.SubjectGeneId;
		}

		private static bool PurposeEnrollmentApplied(KingdomSystem Realm, GameObject Subject,
			KingdomPurposeBodyAuthority Authority)
		{
			r_KingdomEnrolled record = Subject?.GetPart<r_KingdomEnrolled>();
			return record != null && HeldBy(Realm, Authority.SubjectGeneId)
				&& record.Who == Authority.SubjectGeneId
				&& record.PurposePairId == Authority.PairId
				&& record.PurposePairEpoch == Authority.PairEpoch
				&& record.PurposeOperationId == Authority.OperationId
				&& record.PurposeAuthorityId == Authority.AuthorityId;
		}

		private static void ClearPurposeIntent(GameObject Building,
			KingdomPurposeBodyAuthority Authority)
		{
			if (KingdomPurposeBodyAuthorityRules.TryDecode(
				Building?.GetStringProperty(PurposeEnrollmentIntentProperty), out var held)
				&& held.AuthorityId == Authority?.AuthorityId)
				Building.RemoveStringProperty(PurposeEnrollmentIntentProperty);
		}

		private static KingdomPurposeBodyDriveState Invalid(string Text, out string Failure)
		{
			Failure = Text;
			return KingdomPurposeBodyDriveState.Invalid;
		}

		private static bool Refuse(string Text, out string Failure)
		{
			Failure = Text;
			return false;
		}
	}
}
