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

	public partial class r_KingdomLabRecord
	{
		/// <summary>Whether a named procedure has already been performed on this founder, ever.</summary>
		public bool AlreadyHad(string Key)
		{
			return KingdomProcedureRules.Latched(NamedLatch, Key);
		}

		/// <summary>Whether the founder has asked never to be offered this again.</summary>
		public bool Refuses(string Key)
		{
			Normalize();
			for (int i = 0; i < Excluded.Count; i++)
			{
				if (string.Equals(Excluded[i], Key, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Never offer this again. Permanent, because that is what the third answer
		/// promised.</summary>
		public void Exclude(string Key)
		{
			Normalize();
			if (!string.IsNullOrEmpty(Key) && !Refuses(Key))
			{
				Excluded.Add(Key.Trim().ToLowerInvariant());
			}
		}

		/// <summary>What is grafted at one place, or null. What the slate's rows are drawn from.</summary>
		public string GraftedAt(string Place)
		{
			Normalize();
			for (int i = 0; i < Keys.Count && i < Places.Count; i++)
			{
				if (BodyPartIds[i] <= 0
					&& string.Equals(Places[i], Place, StringComparison.OrdinalIgnoreCase))
				{
					return Keys[i];
				}
			}
			return null;
		}

		/// <summary>Exact identity lookup for current records; type is only a legacy fallback.</summary>
		public string GraftedAt(int BodyPartId, string LegacyPlace)
		{
			Normalize();
			for (int i = 0; i < Keys.Count; i++)
			{
				if (BodyPartId > 0 && i < BodyPartIds.Count && BodyPartIds[i] == BodyPartId)
				{
					return Keys[i];
				}
			}
			return GraftedAt(LegacyPlace);
		}

		internal bool ContractAt(int At, out KingdomLabOwnershipSnapshot Snapshot, string PatientId)
		{
			Normalize();
			Snapshot = default(KingdomLabOwnershipSnapshot);
			if (RegistryQuarantined || At < 0 || At >= Keys.Count || BodyPartIds[At] <= 0
				|| string.IsNullOrEmpty(BearerIds[At]) || string.IsNullOrEmpty(JobIds[At])
				|| EffectNonces[At].Length != 32
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					Keys[At], Grants[At], Sources[At], Attaches[At], Managers[At],
					Fingerprints[At], Details[At]))
			{
				return false;
			}
			Snapshot = new KingdomLabOwnershipSnapshot(Keys[At], JobIds[At], PatientId,
				BodyPartIds[At], BearerIds[At], Grants[At], Sources[At], Attaches[At],
				Managers[At], Details[At], Fingerprints[At], PartOrdinals[At],
				EffectNonces[At]);
			return true;
		}

		internal bool UpgradeLegacyLimbAt(int At, int BodyPartId, string BearerId,
			string JobId, string DisplayName, string Grants, int Attach, string Manager,
			string Detail, string Fingerprint)
		{
			Normalize();
			if (RegistryQuarantined || At < 0 || At >= Keys.Count || BodyPartId <= 0
				|| string.IsNullOrEmpty(BearerId) || string.IsNullOrEmpty(JobId)
				|| !string.IsNullOrEmpty(Fingerprints[At])
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					Keys[At], Grants, (int)LabSource.Limb, Attach, Manager,
					Fingerprint, Detail)) return false;
			BodyPartIds[At] = BodyPartId;
			BearerIds[At] = BearerId;
			JobIds[At] = JobId;
			DisplayNames[At] = DisplayName ?? Keys[At];
			this.Grants[At] = Grants;
			Sources[At] = (int)LabSource.Limb;
			Attaches[At] = Attach;
			Managers[At] = Manager;
			Details[At] = Detail;
			Fingerprints[At] = Fingerprint;
			PartOrdinals[At] = -1;
			return true;
		}

		public int IndexOf(string Key)
		{
			Normalize();
			for (int i = 0; i < Keys.Count; i++)
			{
				if (string.Equals(Keys[i], Key, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
			return -1;
		}
	}
}
