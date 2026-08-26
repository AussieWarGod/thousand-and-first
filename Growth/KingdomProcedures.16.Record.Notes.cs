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
		/// <summary>Records one procedure. Idempotent on the latch, so nothing anywhere has to
		/// remember whether it already asked.</summary>
		public void Note(string Key, string Place, bool OnWeapon)
		{
			NoteLegacy(Key, Place, OnWeapon, 0, "", "");
		}

		public void Note(string Key, string Place, bool OnWeapon, int BodyPartId,
			string BearerId, string JobId)
		{
			NoteLegacy(Key, Place, OnWeapon, BodyPartId, BearerId, JobId);
		}

		private void NoteLegacy(string Key, string Place, bool OnWeapon, int BodyPartId,
			string BearerId, string JobId)
		{
			if (string.IsNullOrEmpty(Key)) return;
			Normalize();
			if (Keys.Count >= KingdomLabRules.MaxEffectRows)
			{
				RegistryQuarantined = true;
				RegistryFault = "The patient ownership receipt registry is full.";
				return;
			}
			Keys.Add(Key);
			Places.Add(Place ?? "");
			this.OnWeapon.Add(OnWeapon);
			BodyPartIds.Add(BodyPartId);
			BearerIds.Add(BearerId ?? "");
			JobIds.Add(JobId ?? "");
			DisplayNames.Add("");
			Grants.Add("");
			Sources.Add(-1);
			Attaches.Add(-1);
			Managers.Add("");
			Details.Add("");
			Fingerprints.Add("");
			PartOrdinals.Add(-1);
			EffectNonces.Add("");
			LabProcedure procedure;
			if (KingdomProcedures.TryGet(Key, out procedure) && procedure.IsNamed)
			{
				NamedLatch = KingdomProcedureRules.Latch(NamedLatch, Key);
			}
		}

		public void Note(string Key, string Place, bool OnWeapon, int BodyPartId,
			string BearerId, string JobId, string DisplayName, string Grants, int Source,
			int Attach, string Manager, string Detail, string Fingerprint, int PartOrdinal,
			string EffectNonce = "")
		{
			Normalize();
			if (RegistryQuarantined || string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(JobId)
				|| BodyPartId <= 0 || string.IsNullOrEmpty(BearerId)
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					Key, Grants, Source, Attach, Manager, Fingerprint, Detail)
				|| string.IsNullOrEmpty(EffectNonce) || EffectNonce.Length != 32)
			{
				throw new InvalidOperationException("invalid or quarantined patient ownership receipt");
			}
			for (int i = 0; i < Keys.Count; i++)
			{
				if (!string.Equals(Keys[i], Key, StringComparison.OrdinalIgnoreCase)
					|| !string.Equals(JobIds[i], JobId, StringComparison.Ordinal)) continue;
				if (BodyPartIds[i] == BodyPartId
					&& string.Equals(BearerIds[i], BearerId, StringComparison.Ordinal)
					&& string.Equals(this.Grants[i], Grants, StringComparison.Ordinal)
					&& Sources[i] == Source && Attaches[i] == Attach
					&& string.Equals(Managers[i], Manager, StringComparison.Ordinal)
					&& string.Equals(Details[i], Detail, StringComparison.Ordinal)
					&& string.Equals(Fingerprints[i], Fingerprint, StringComparison.Ordinal)
					&& PartOrdinals[i] == PartOrdinal
					&& string.Equals(EffectNonces[i], EffectNonce, StringComparison.Ordinal))
				{
					return;
				}
				RegistryQuarantined = true;
				RegistryFault = "An ownership receipt reused a job ID with different physical identity.";
				throw new InvalidOperationException(RegistryFault);
			}
			if (Keys.Count >= KingdomLabRules.MaxEffectRows)
			{
				RegistryQuarantined = true;
				RegistryFault = "The patient ownership receipt registry is full.";
				throw new InvalidOperationException(RegistryFault);
			}
			Keys.Add(Key);
			Places.Add(Place ?? "");
			this.OnWeapon.Add(OnWeapon);
			BodyPartIds.Add(BodyPartId);
			BearerIds.Add(BearerId ?? "");
			JobIds.Add(JobId ?? "");
			DisplayNames.Add(DisplayName ?? Key);
			this.Grants.Add(Grants ?? "");
			Sources.Add(Source);
			Attaches.Add(Attach);
			Managers.Add(Manager ?? "");
			Details.Add(Detail ?? "");
			Fingerprints.Add(Fingerprint ?? "");
			PartOrdinals.Add(PartOrdinal);
			EffectNonces.Add(EffectNonce);
			LabProcedure procedure;
			if (KingdomProcedures.TryGet(Key, out procedure) && procedure.IsNamed)
			{
				NamedLatch = KingdomProcedureRules.Latch(NamedLatch, Key);
			}
		}

		/// <summary>Forgets a graft that came off. The named latch is untouched, on purpose.</summary>
		public void Forget(string Key)
		{
			Normalize();
			for (int i = Keys.Count - 1; i >= 0; i--)
			{
				if (!string.Equals(Keys[i], Key, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				RemoveAt(i);
				return;
			}
		}

		/// <summary>Forgets only the record minted by one exact commission.</summary>
		public void ForgetOwned(string Key, string JobId)
		{
			Normalize();
			for (int i = Keys.Count - 1; i >= 0; i--)
			{
				if (string.Equals(Keys[i], Key, StringComparison.OrdinalIgnoreCase)
					&& i < JobIds.Count
					&& string.Equals(JobIds[i], JobId, StringComparison.Ordinal))
				{
					RemoveAt(i);
					return;
				}
			}
		}

		private void RemoveAt(int At)
		{
			Keys.RemoveAt(At);
			Places.RemoveAt(At);
			OnWeapon.RemoveAt(At);
			BodyPartIds.RemoveAt(At);
			BearerIds.RemoveAt(At);
			JobIds.RemoveAt(At);
			DisplayNames.RemoveAt(At);
			Grants.RemoveAt(At);
			Sources.RemoveAt(At);
			Attaches.RemoveAt(At);
			Managers.RemoveAt(At);
			Details.RemoveAt(At);
			Fingerprints.RemoveAt(At);
			PartOrdinals.RemoveAt(At);
			EffectNonces.RemoveAt(At);
		}
	}
}
