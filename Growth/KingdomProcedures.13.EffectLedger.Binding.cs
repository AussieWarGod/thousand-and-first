using System;
using System.Collections.Generic;
using System.Reflection;

using Qud.API;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

namespace XRL.World.Parts
{
	using ThousandAndFirst;

	public partial class r_KingdomLabEffectLedger
	{
		public bool UpgradeLegacyLimb(string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, int Attach, string ClassName, string Manager, string Detail,
			string Fingerprint)
		{
			Normalize();
			if (LedgerQuarantined || string.IsNullOrEmpty(JobId)
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					ProcedureKey, ClassName, (int)LabSource.Limb, Attach, Manager,
					Fingerprint, Detail)) return false;
			int at = IndexOf(ProcedureKey, JobId);
			if (at < 0 || !string.Equals(PatientIds[at], PatientId, StringComparison.Ordinal)
				|| Sources[at] != (int)LabSource.Limb || BodyPartIds[at] != BodyPartId
				|| (!string.IsNullOrEmpty(ClassNames[at])
					&& !string.Equals(ClassNames[at], ClassName, StringComparison.Ordinal)))
			{
				return false;
			}
			BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartId);
			if (limb == null || !KingdomProcedures.BodyOwnsPart(ParentObject, limb)
				|| !string.Equals(limb.Manager, Manager, StringComparison.Ordinal)
				|| !string.Equals(ParentObject?.GetStringProperty(
					KingdomProcedures.OwnerProperty(ProcedureKey)), JobId, StringComparison.Ordinal))
			{
				return false;
			}
			ClassNames[at] = ClassName;
			Attaches[at] = Attach;
			Managers[at] = Manager;
			Details[at] = Detail;
			Fingerprints[at] = Fingerprint;
			PartOrdinals[at] = -1;
			BindingStates[at] = 1;
			return true;
		}

		public int IndexOf(string ProcedureKey, string JobId)
		{
			Normalize();
			for (int i = 0; i < ProcedureKeys.Count; i++)
			{
				if (string.Equals(ProcedureKeys[i], ProcedureKey,
					StringComparison.OrdinalIgnoreCase)
					&& string.Equals(JobIds[i], JobId, StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		public bool EntryMatches(int At, string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, int Source, int Attach, string ClassName, string Manager,
			string Detail, string Fingerprint, int PartOrdinal, bool IgnoreOrdinal = false)
		{
			Normalize();
			return At >= 0 && At < ProcedureKeys.Count
				&& string.Equals(ProcedureKeys[At], ProcedureKey,
					StringComparison.OrdinalIgnoreCase)
				&& string.Equals(JobIds[At], JobId, StringComparison.Ordinal)
				&& string.Equals(PatientIds[At], PatientId, StringComparison.Ordinal)
				&& BodyPartIds[At] == BodyPartId && Sources[At] == Source && Attaches[At] == Attach
				&& string.Equals(ClassNames[At], ClassName, StringComparison.Ordinal)
				&& string.Equals(Managers[At], Manager, StringComparison.Ordinal)
				&& string.Equals(Details[At], Detail, StringComparison.Ordinal)
				&& string.Equals(Fingerprints[At], Fingerprint, StringComparison.Ordinal)
				&& (IgnoreOrdinal || PartOrdinals[At] == PartOrdinal);
		}

		public bool EntryMatches(int At, string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, int Source, string ClassName)
		{
			Normalize();
			return At >= 0 && At < ProcedureKeys.Count
				&& string.Equals(ProcedureKeys[At], ProcedureKey, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(JobIds[At], JobId, StringComparison.Ordinal)
				&& string.Equals(PatientIds[At], PatientId, StringComparison.Ordinal)
				&& BodyPartIds[At] == BodyPartId && Sources[At] == Source
				&& string.Equals(ClassNames[At], ClassName, StringComparison.Ordinal);
		}

		public IPart ResolvePart(int At)
		{
			Normalize();
			if (LedgerQuarantined || At < 0 || At >= ProcedureKeys.Count
				|| Sources[At] == (int)LabSource.Limb || BindingStates[At] == 2
				|| BindingStates[At] == 3 || BindingStates[At] == 4)
			{
				return null;
			}
			IPart runtime = RuntimeParts[At];
			if (runtime != null && ReferenceEquals(runtime.ParentObject, ParentObject)
				&& KingdomProcedures.ReferencePartOrdinal(ParentObject, runtime) == PartOrdinals[At]
				&& string.Equals(runtime.Name, ClassNames[At], StringComparison.Ordinal))
			{
				return runtime;
			}
			RuntimeParts[At] = null;
			return RebindAt(At);
		}

		private IPart RebindAt(int At)
		{
			if (LedgerQuarantined || At < 0 || At >= ProcedureKeys.Count
				|| BindingStates[At] == 2
				|| !string.Equals(ParentObject?.GetStringProperty(
					KingdomProcedures.OwnerProperty(ProcedureKeys[At])), JobIds[At],
					StringComparison.Ordinal)
				|| !string.Equals(ParentObject?.GetStringProperty(
					KingdomProcedures.OwnerNonceProperty(ProcedureKeys[At])), EffectNonces[At],
					StringComparison.Ordinal))
			{
				return null;
			}
			if (BindingStates[At] == 3) return null;
			if (BindingStates[At] == 4)
			{
				BindingStates[At] = 2;
				return null;
			}
			if (Sources[At] == (int)LabSource.Limb)
			{
				BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartIds[At]);
				if (limb != null && KingdomProcedures.BodyOwnsPart(ParentObject, limb)
					&& string.Equals(limb.Manager, Managers[At], StringComparison.Ordinal)
					&& KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
						ProcedureKeys[At], ClassNames[At], Sources[At], Attaches[At], Managers[At],
						Fingerprints[At], Details[At]))
				{
					BindingStates[At] = 1;
					return null;
				}
				BindingStates[At] = 2;
				return null;
			}
			int ordinal = PartOrdinals[At];
			if (ordinal < 0 || ParentObject?.PartsList == null || ordinal >= ParentObject.PartsList.Count)
			{
				BindingStates[At] = 2;
				return null;
			}
			IPart candidate = ParentObject.PartsList[ordinal];
			if (candidate == null || !ReferenceEquals(candidate.ParentObject, ParentObject)
				|| !string.Equals(candidate.Name, ClassNames[At], StringComparison.Ordinal)
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					ProcedureKeys[At], ClassNames[At], Sources[At], Attaches[At], Managers[At],
					Fingerprints[At], Details[At]))
			{
				BindingStates[At] = 2;
				return null;
			}
			if (Sources[At] == (int)LabSource.Mutation
				&& !KingdomProcedures.MutationListed(
					ParentObject.GetPart<XRL.World.Parts.Mutations>(),
					candidate as XRL.World.Parts.Mutation.BaseMutation))
			{
				BindingStates[At] = 2;
				return null;
			}
			RuntimeParts[At] = candidate;
			BindingStates[At] = 1;
			return candidate;
		}

		public void Forget(string ProcedureKey, string JobId, bool CleanupPatient)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at >= 0)
			{
				ForgetAt(at, CleanupPatient);
			}
		}

		private void ForgetAt(int At, bool CleanupPatient)
		{
			string key = ProcedureKeys[At];
			string job = JobIds[At];
			string nonce = EffectNonces[At];
			string patientId = PatientIds[At];
			ProcedureKeys.RemoveAt(At);
			JobIds.RemoveAt(At);
			PatientIds.RemoveAt(At);
			BodyPartIds.RemoveAt(At);
			Sources.RemoveAt(At);
			ClassNames.RemoveAt(At);
			Attaches.RemoveAt(At);
			Managers.RemoveAt(At);
			Details.RemoveAt(At);
			Fingerprints.RemoveAt(At);
			PartOrdinals.RemoveAt(At);
			BindingStates.RemoveAt(At);
			EffectNonces.RemoveAt(At);
			RuntimeParts.RemoveAt(At);
			try
			{
				if (string.Equals(ParentObject.GetStringProperty(
					KingdomProcedures.OwnerProperty(key)), job, StringComparison.Ordinal))
				{
					ParentObject.RemoveStringProperty(KingdomProcedures.OwnerProperty(key));
				}
				if (string.Equals(ParentObject.GetStringProperty(
					KingdomProcedures.OwnerNonceProperty(key)), nonce, StringComparison.Ordinal))
				{
					ParentObject.RemoveStringProperty(KingdomProcedures.OwnerNonceProperty(key));
				}
				if (CleanupPatient)
				{
					GameObject patient = GameObject.FindByID(patientId);
					patient?.GetPart<r_KingdomLabRecord>()?.ForgetOwned(key, job);
				}
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: effect-ledger cleanup threw (" + ex.Message + ")");
			}
		}
	}
}
