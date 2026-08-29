using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL.World;
	using XRL.World.Parts;

	internal enum KingdomPurposeBodyDriveState : byte
	{
		Waiting = 1,
		Applied = 2,
		Invalid = 3
	}

	internal static partial class KingdomLab
	{
		internal static KingdomPurposeBodyDriveState DrivePurposeProcedure(GameObject Building,
			KingdomSystem System, KingdomPurposeBodyAuthority Authority, out string Failure)
		{
			Failure = null;
			GameObject actor = GameObject.FindByID(Authority?.SubjectObjectId);
			if (!GameObject.Validate(Building) || !GameObject.Validate(actor) || System == null
				|| Authority == null || Authority.Kind != KingdomPurposeKind.Flesh)
				return Invalid("The frozen theatre authority cannot resolve its exact patient.",
					out Failure);
			if (PurposeProcedureApplied(actor, Authority))
			{
				ClearPurposeIntent(Building, Authority);
				return KingdomPurposeBodyDriveState.Applied;
			}
			r_KingdomLabJob job = Building.GetPart<r_KingdomLabJob>();
			if (job == null)
			{
				if (!PurposeSelectionStands(Building, actor, System, Authority,
					out LabProcedure procedure, out List<GameObject> kept, out int at))
					return Invalid("The selected procedure no longer answers its exact patient, slot, or source.",
						out Failure);
				Building.SetStringProperty(PurposeIntentProperty,
					KingdomPurposeBodyAuthorityRules.Encode(Authority));
				string city = KingdomCrown.CityOf(System, Building.CurrentZone?.ZoneID)
					?? System.SeatName;
				Commission(Building, actor, System, procedure,
					KingdomProcedures.Census(actor, new List<string>()), at, kept, city);
				job = Building.GetPart<r_KingdomLabJob>();
				if (job == null)
				{
					if (PurposeProcedureApplied(actor, Authority))
						return KingdomPurposeBodyDriveState.Applied;
					Failure = "The exact theatre commission did not publish; retry the preserved operation.";
					return KingdomPurposeBodyDriveState.Waiting;
				}
			}
			job.Normalize();
			if (!PurposeJobMatches(job, Authority))
				return Invalid("A different theatre commission occupies the purpose work.", out Failure);
			LabProcedure frozen = FrozenProcedure(job);
			if (frozen == null)
				return Invalid("The purpose commission's frozen procedure contract is invalid.",
					out Failure);
			if (job.State == KingdomLabJobPhase.Funding
				|| job.State == KingdomLabJobPhase.FundingRecovery)
				RecoverFunding(Building, actor, System, job, frozen);
			if (job.State == KingdomLabJobPhase.Ready || job.State == KingdomLabJobPhase.Applying
				|| job.State == KingdomLabJobPhase.ApplicationRecovery)
				ApplyJob(Building, actor, System, job, frozen);
			if (PurposeProcedureApplied(actor, Authority))
			{
				ClearPurposeIntent(Building, Authority);
				return KingdomPurposeBodyDriveState.Applied;
			}
			if (job.State == KingdomLabJobPhase.Cancelled || job.SchemaQuarantined)
				return Invalid(job.Fault ?? "The selected theatre commission is terminal.",
					out Failure);
			Failure = job.State == KingdomLabJobPhase.Working
				? "The selected theatre procedure has " + job.RemainingTicks
					+ " staffed ticks remaining; return when it is ready."
				: string.IsNullOrEmpty(job.Fault)
					? "The selected theatre procedure waits on its exact receipt."
					: job.Fault;
			return KingdomPurposeBodyDriveState.Waiting;
		}

		internal static string PurposeCommissionJobId(GameObject Building, GameObject Actor,
			LabProcedure Procedure, XRL.World.Anatomy.BodyPart Part)
		{
			string receipt = Building?.GetStringProperty(PurposeIntentProperty);
			if (!KingdomPurposeBodyAuthorityRules.TryDecode(receipt,
				out KingdomPurposeBodyAuthority authority)
				|| authority.Kind != KingdomPurposeKind.Flesh
				|| authority.SubjectObjectId != Actor?.IDIfAssigned || authority.ProcedureKey != Procedure?.Key
				|| authority.BodyPartId != Part?.ID) return null;
			GameObject bearer = Procedure.Attach == LabAttach.Weapon
				? Part?.DefaultBehavior : Actor;
			return GameObject.Validate(bearer) && bearer.IDIfAssigned == authority.BearerId
				? authority.AuthorityId : null;
		}

		private static bool PurposeJobMatches(r_KingdomLabJob Job,
			KingdomPurposeBodyAuthority Authority)
		{
			return Job != null && Authority != null && Job.JobId == Authority.AuthorityId
				&& Job.ProcedureKey == Authority.ProcedureKey
				&& Job.PatientId == Authority.SubjectObjectId
				&& Job.BodyPartId == Authority.BodyPartId && Job.BearerId == Authority.BearerId
				&& Job.WaterOwed == Authority.WaterCost
				&& Job.KeptOwed == Authority.PreservedCost
				&& Job.BitClaim == Authority.BitCost;
		}

		private static bool PurposeProcedureApplied(GameObject Actor,
			KingdomPurposeBodyAuthority Authority)
		{
			r_KingdomLabRecord record = Actor?.GetPart<r_KingdomLabRecord>();
			if (record == null || Authority == null) return false;
			record.Normalize();
			for (int i = 0; i < record.Keys.Count; i++)
			{
				if (record.Keys[i] != Authority.ProcedureKey
					|| record.JobIds[i] != Authority.AuthorityId
					|| record.BodyPartIds[i] != Authority.BodyPartId
					|| record.BearerIds[i] != Authority.BearerId
					|| !record.ContractAt(i, out KingdomLabOwnershipSnapshot snapshot, Actor.IDIfAssigned))
					continue;
				KingdomLabOwnedTarget ignored;
				return KingdomProcedures.ClassifyOwned(Actor, snapshot, out ignored)
					== KingdomLabOwnedTargetState.Present;
			}
			return false;
		}

		private static void ClearPurposeIntent(GameObject Building,
			KingdomPurposeBodyAuthority Authority)
		{
			if (Building != null && KingdomPurposeBodyAuthorityRules.TryDecode(
				Building.GetStringProperty(PurposeIntentProperty), out var held)
				&& held.AuthorityId == Authority?.AuthorityId)
				Building.RemoveStringProperty(PurposeIntentProperty);
		}

		private static KingdomPurposeBodyDriveState Invalid(string Text, out string Failure)
		{
			Failure = Text;
			return KingdomPurposeBodyDriveState.Invalid;
		}
	}
}
