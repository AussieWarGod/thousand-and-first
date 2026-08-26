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
		public void Track(string ProcedureKey, string JobId, string PatientId, int BodyPartId,
			int Source, string ClassName, IPart RuntimePart)
		{
			string manager = KingdomProcedures.ManagerFor(ProcedureKey);
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, ProcedureKey, ClassName, Source,
				(int)LabAttach.Body, manager, "");
			TrackIntent(ProcedureKey, JobId, PatientId, BodyPartId, Source,
				(int)LabAttach.Body, ClassName, manager, "", fingerprint,
				KingdomProcedures.ReferencePartOrdinal(ParentObject, RuntimePart), RuntimePart);
			CommitBinding(ProcedureKey, JobId,
				KingdomProcedures.ReferencePartOrdinal(ParentObject, RuntimePart), RuntimePart);
		}

		public void TrackIntent(string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, int Source, int Attach, string ClassName, string Manager,
			string Detail, string Fingerprint, int PartOrdinal, IPart RuntimePart,
			string EffectNonce = "")
		{
			Normalize();
			string nonce = EffectNonce ?? "";
			if (LedgerQuarantined)
			{
				LedgerQuarantined = true;
				throw new InvalidOperationException("lab effect ledger is quarantined");
			}
			if (!KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
				ProcedureKey, ClassName, Source, Attach, Manager, Fingerprint, Detail))
			{
				throw new InvalidOperationException("invalid lab effect contract");
			}
			int existing = IndexOf(ProcedureKey, JobId);
			if (existing >= 0)
			{
				if (string.IsNullOrEmpty(nonce)) nonce = EffectNonces[existing];
				if (!EntryMatches(existing, ProcedureKey, JobId, PatientId, BodyPartId,
					Source, Attach, ClassName, Manager, Detail, Fingerprint, PartOrdinal,
					IgnoreOrdinal: true)
					|| nonce.Length != 32
					|| !string.Equals(EffectNonces[existing], nonce, StringComparison.Ordinal))
				{
					throw new InvalidOperationException("lab effect identity collision");
				}
				RuntimeParts[existing] = RuntimePart;
				PartOrdinals[existing] = PartOrdinal;
				BindingStates[existing] = 0;
				return;
			}
			if (string.IsNullOrEmpty(nonce)) nonce = Guid.NewGuid().ToString("N");
			if (nonce.Length != 32)
				throw new InvalidOperationException("invalid lab effect nonce");
			if (ProcedureKeys.Count >= KingdomLabRules.MaxEffectRows)
			{
				LedgerQuarantined = true;
				throw new InvalidOperationException("lab effect ledger is full");
			}
			ProcedureKeys.Add(ProcedureKey ?? "");
			JobIds.Add(JobId ?? "");
			PatientIds.Add(PatientId ?? "");
			BodyPartIds.Add(BodyPartId);
			Sources.Add(Source);
			ClassNames.Add(ClassName ?? "");
			Attaches.Add(Attach);
			Managers.Add(Manager ?? "");
			Details.Add(Detail ?? "");
			Fingerprints.Add(Fingerprint ?? "");
			PartOrdinals.Add(PartOrdinal);
			BindingStates.Add(0);
			EffectNonces.Add(nonce);
			RuntimeParts.Add(RuntimePart);
		}

		public string NonceAt(int At)
		{
			Normalize();
			return At >= 0 && At < EffectNonces.Count ? EffectNonces[At] : "";
		}

		public void CommitBinding(string ProcedureKey, string JobId, int PartOrdinal,
			IPart RuntimePart)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at < 0) throw new InvalidOperationException("lab effect intent is absent");
			if (Sources[at] == (int)LabSource.Limb)
			{
				BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartIds[at]);
				if (limb == null || !KingdomProcedures.BodyOwnsPart(ParentObject, limb)
					|| !string.Equals(limb.Manager, Managers[at], StringComparison.Ordinal))
				{
					BindingStates[at] = 2;
					throw new InvalidOperationException("exact limb binding is not present");
				}
			}
			else
			{
				if (RuntimePart == null || !ReferenceEquals(RuntimePart.ParentObject, ParentObject)
					|| KingdomProcedures.ReferencePartOrdinal(ParentObject, RuntimePart) != PartOrdinal)
				{
					BindingStates[at] = 2;
					throw new InvalidOperationException("exact part binding is not present at its ordinal");
				}
			}
			PartOrdinals[at] = PartOrdinal;
			RuntimeParts[at] = RuntimePart;
			BindingStates[at] = 1;
		}

		public void Quarantine(string ProcedureKey, string JobId)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at >= 0) BindingStates[at] = 2;
		}

		public bool BeginRemoval(string ProcedureKey, string JobId)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at < 0 || BindingStates[at] == 2 || BindingStates[at] == 3) return false;
			BindingStates[at] = 4;
			return true;
		}

		public void MarkRemoved(string ProcedureKey, string JobId)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at >= 0 && BindingStates[at] != 2) BindingStates[at] = 3;
		}

		public void CancelRemoval(string ProcedureKey, string JobId)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at >= 0 && BindingStates[at] == 4) BindingStates[at] = 1;
		}

		public int BindingStateAt(int At)
		{
			Normalize();
			return At < 0 || At >= BindingStates.Count ? 2 : BindingStates[At];
		}

		internal bool RearmPresent(int At, IPart Exact)
		{
			Normalize();
			if (At < 0 || At >= ProcedureKeys.Count || BindingStates[At] != 3) return false;
			if (Sources[At] == (int)LabSource.Limb)
			{
				BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartIds[At]);
				if (limb == null || !KingdomProcedures.BodyOwnsPart(ParentObject, limb)
					|| !string.Equals(limb.Manager, Managers[At], StringComparison.Ordinal))
					return false;
			}
			else
			{
				int ordinal = KingdomProcedures.ReferencePartOrdinal(ParentObject, Exact);
				if (Exact == null || !ReferenceEquals(Exact.ParentObject, ParentObject)
					|| ordinal != PartOrdinals[At]
					|| !string.Equals(Exact.Name, ClassNames[At], StringComparison.Ordinal)
					|| (Sources[At] == (int)LabSource.Mutation
						&& !KingdomProcedures.MutationListed(ParentObject.GetPart<Mutations>(),
							Exact as XRL.World.Parts.Mutation.BaseMutation))) return false;
				RuntimeParts[At] = Exact;
			}
			BindingStates[At] = 1;
			return true;
		}

		internal KingdomLabOwnedTargetState ClassifyTombstone(int At, out IPart Exact)
		{
			Exact = null;
			Normalize();
			if (At < 0 || At >= ProcedureKeys.Count || BindingStates[At] != 3)
				return KingdomLabOwnedTargetState.Uncertain;
			IPart runtime = RuntimeParts[At];
			if (runtime == null || runtime.ParentObject == null)
			{
				int frozenOrdinal = PartOrdinals[At];
				IPart candidate = frozenOrdinal >= 0 && ParentObject?.PartsList != null
					&& frozenOrdinal < ParentObject.PartsList.Count
					? ParentObject.PartsList[frozenOrdinal] : null;
				if (candidate == null || !string.Equals(candidate.Name, ClassNames[At],
					StringComparison.Ordinal)) return KingdomLabOwnedTargetState.Absent;
				if (Sources[At] == (int)LabSource.Mutation
					&& !KingdomProcedures.MutationListed(ParentObject.GetPart<Mutations>(),
						candidate as XRL.World.Parts.Mutation.BaseMutation))
				{
					return KingdomLabOwnedTargetState.Absent;
				}
				return KingdomLabOwnedTargetState.Uncertain;
			}
			int ordinal = KingdomProcedures.ReferencePartOrdinal(ParentObject, runtime);
			if (!ReferenceEquals(runtime.ParentObject, ParentObject) || ordinal < 0
				|| ordinal != PartOrdinals[At]
				|| !string.Equals(runtime.Name, ClassNames[At], StringComparison.Ordinal))
				return KingdomLabOwnedTargetState.Uncertain;
			if (Sources[At] == (int)LabSource.Mutation
				&& !KingdomProcedures.MutationListed(ParentObject.GetPart<Mutations>(),
					runtime as XRL.World.Parts.Mutation.BaseMutation))
				return KingdomLabOwnedTargetState.Absent;
			Exact = runtime;
			return KingdomLabOwnedTargetState.Present;
		}
	}
}
