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
		public void Normalize()
		{
			ProcedureKeys = ProcedureKeys ?? new List<string>();
			JobIds = JobIds ?? new List<string>();
			PatientIds = PatientIds ?? new List<string>();
			BodyPartIds = BodyPartIds ?? new List<int>();
			Sources = Sources ?? new List<int>();
			ClassNames = ClassNames ?? new List<string>();
			Attaches = Attaches ?? new List<int>();
			Managers = Managers ?? new List<string>();
			Details = Details ?? new List<string>();
			Fingerprints = Fingerprints ?? new List<string>();
			PartOrdinals = PartOrdinals ?? new List<int>();
			BindingStates = BindingStates ?? new List<int>();
			EffectNonces = EffectNonces ?? new List<string>();
			int original = ProcedureKeys.Count;
			int count = original;
			count = Math.Min(count, JobIds.Count);
			count = Math.Min(count, PatientIds.Count);
			count = Math.Min(count, BodyPartIds.Count);
			count = Math.Min(count, Sources.Count);
			count = Math.Min(count, ClassNames.Count);
			if (count != original || JobIds.Count != original || PatientIds.Count != original
				|| BodyPartIds.Count != original || Sources.Count != original
				|| ClassNames.Count != original)
			{
				LedgerQuarantined = true;
			}
			if (Attaches.Count != count || Managers.Count != count || Details.Count != count
				|| Fingerprints.Count != count || PartOrdinals.Count != count
				|| BindingStates.Count != count)
			{
				// Pre-contract ledgers cannot prove which authored effect they own. Keep the
				// rows as individually quarantined; a unique manager-owned legacy limb may be
				// upgraded later without making a class-only inference.
			}
			if (count > KingdomLabRules.MaxEffectRows)
			{
				LedgerQuarantined = true;
				count = KingdomLabRules.MaxEffectRows;
			}
			Trim(ProcedureKeys, count);
			Trim(JobIds, count);
			Trim(PatientIds, count);
			Trim(BodyPartIds, count);
			Trim(Sources, count);
			Trim(ClassNames, count);
			Pad(Attaches, count, -1);
			Pad(Managers, count, "");
			Pad(Details, count, "");
			Pad(Fingerprints, count, "");
			Pad(PartOrdinals, count, -1);
			Pad(BindingStates, count, 2);
			Pad(EffectNonces, count, "");
			for (int i = 0; i < count; i++)
			{
				if (BindingStates[i] < 0 || BindingStates[i] > 4
					|| EffectNonces[i].Length != 32
					|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
						ProcedureKeys[i], ClassNames[i], Sources[i], Attaches[i], Managers[i],
						Fingerprints[i], Details[i]))
				{
					BindingStates[i] = 2;
				}
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(ProcedureKeys[i], ProcedureKeys[j], StringComparison.OrdinalIgnoreCase)
						&& string.Equals(JobIds[i], JobIds[j], StringComparison.Ordinal))
					{
						BindingStates[i] = BindingStates[j] = 2;
						LedgerQuarantined = true;
					}
				}
			}
			RuntimeParts = RuntimeParts ?? new List<IPart>();
			Trim(RuntimeParts, count);
			while (RuntimeParts.Count < count)
			{
				RuntimeParts.Add(null);
			}
		}

		private static void Pad<T>(List<T> Values, int Count, T Value)
		{
			Trim(Values, Count);
			while (Values.Count < Count) Values.Add(Value);
		}

		private static void Trim<T>(List<T> Values, int Count)
		{
			if (Values.Count > Count)
			{
				Values.RemoveRange(Count, Values.Count - Count);
			}
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Normalize();
			for (int i = 0; i < ProcedureKeys.Count; i++)
			{
				if (BindingStates[i] == 2 || BindingStates[i] == 3) continue;
				if (BindingStates[i] == 4)
				{
					BindingStates[i] = 2;
					continue;
				}
				if (Sources[i] == (int)LabSource.Limb)
				{
					BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartIds[i]);
					if (limb == null || !KingdomProcedures.BodyOwnsPart(ParentObject, limb)
						|| !string.Equals(limb.Manager, Managers[i], StringComparison.Ordinal))
					{
						BindingStates[i] = 2;
					}
					continue;
				}
				IPart exact = RuntimeParts[i];
				int ordinal = KingdomProcedures.ReferencePartOrdinal(ParentObject, exact);
				if (exact == null || ordinal < 0 || ordinal != PartOrdinals[i]
					|| !ReferenceEquals(exact.ParentObject, ParentObject)
					|| !string.Equals(exact.Name, ClassNames[i], StringComparison.Ordinal)
					|| (Sources[i] == (int)LabSource.Mutation
						&& !KingdomProcedures.MutationListed(
							ParentObject?.GetPart<XRL.World.Parts.Mutations>(),
							exact as XRL.World.Parts.Mutation.BaseMutation)))
				{
					BindingStates[i] = 2;
				}
			}
			Writer.WriteNamedFields(this, typeof(r_KingdomLabEffectLedger));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomLabEffectLedger));
			RuntimeParts = null;
			Normalize();
		}
	}
}
