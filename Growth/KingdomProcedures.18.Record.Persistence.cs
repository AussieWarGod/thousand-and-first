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
		/// <summary>Repairs a record read from a save written by an older build: null containers
		/// become empty ones, and lists that fell out of step are trimmed to their shortest, because
		/// a record that says a graft is at a place it cannot name is worse than one that says
		/// nothing.</summary>
		public void Normalize()
		{
			Keys = Keys ?? new List<string>();
			Places = Places ?? new List<string>();
			OnWeapon = OnWeapon ?? new List<bool>();
			Excluded = Excluded ?? new List<string>();
			BodyPartIds = BodyPartIds ?? new List<int>();
			BearerIds = BearerIds ?? new List<string>();
			JobIds = JobIds ?? new List<string>();
			DisplayNames = DisplayNames ?? new List<string>();
			Grants = Grants ?? new List<string>();
			Sources = Sources ?? new List<int>();
			Attaches = Attaches ?? new List<int>();
			Managers = Managers ?? new List<string>();
			Details = Details ?? new List<string>();
			Fingerprints = Fingerprints ?? new List<string>();
			PartOrdinals = PartOrdinals ?? new List<int>();
			EffectNonces = EffectNonces ?? new List<string>();
			NamedLatch = NamedLatch ?? "";
			RegistryFault = RegistryFault ?? "";

			if (Keys.Count > KingdomLabRules.MaxEffectRows)
			{
				RegistryQuarantined = true;
				RegistryFault = "Patient ownership receipt registry exceeded its bound.";
				Keys.RemoveRange(KingdomLabRules.MaxEffectRows,
					Keys.Count - KingdomLabRules.MaxEffectRows);
			}
			int count = Keys.Count;
			bool anyContract = DisplayNames.Count > 0 || Grants.Count > 0 || Sources.Count > 0
				|| Attaches.Count > 0 || Managers.Count > 0 || Details.Count > 0
				|| Fingerprints.Count > 0 || PartOrdinals.Count > 0 || EffectNonces.Count > 0;
			if (anyContract && (DisplayNames.Count != count || Grants.Count != count
				|| Sources.Count != count || Attaches.Count != count || Managers.Count != count
				|| Details.Count != count || Fingerprints.Count != count
				|| PartOrdinals.Count != count || EffectNonces.Count != count))
			{
				RegistryQuarantined = true;
				RegistryFault = "Patient ownership receipt columns disagree.";
			}
			Pad(Places, count, "");
			Pad(OnWeapon, count, false);
			Pad(BodyPartIds, count, 0);
			Pad(BearerIds, count, "");
			Pad(JobIds, count, "");
			Pad(DisplayNames, count, "");
			Pad(Grants, count, "");
			Pad(Sources, count, -1);
			Pad(Attaches, count, -1);
			Pad(Managers, count, "");
			Pad(Details, count, "");
			Pad(Fingerprints, count, "");
			Pad(PartOrdinals, count, -1);
			Pad(EffectNonces, count, "");
			for (int i = 0; i < count; i++)
			{
				bool claimsContract = !string.IsNullOrEmpty(Fingerprints[i])
					|| !string.IsNullOrEmpty(Grants[i]) || Sources[i] >= 0 || Attaches[i] >= 0;
				if (claimsContract && (!KingdomLabRules.ValidEffectContract(
					KingdomLabRules.EffectContractVersion, Keys[i], Grants[i], Sources[i],
					Attaches[i], Managers[i], Fingerprints[i], Details[i])
					|| EffectNonces[i].Length != 32))
				{
					RegistryQuarantined = true;
					RegistryFault = "A patient ownership receipt has an invalid effect contract.";
				}
				if (string.IsNullOrEmpty(JobIds[i])) continue;
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(Keys[i], Keys[j], StringComparison.OrdinalIgnoreCase)
						&& string.Equals(JobIds[i], JobIds[j], StringComparison.Ordinal))
					{
						RegistryQuarantined = true;
						RegistryFault = "Patient ownership receipts duplicate one job identity.";
					}
				}
			}
			if (Excluded.Count > 256)
			{
				Excluded.RemoveRange(256, Excluded.Count - 256);
			}
		}

		private static void Pad<T>(List<T> Values, int Count, T Value)
		{
			if (Values.Count > Count) Values.RemoveRange(Count, Values.Count - Count);
			while (Values.Count < Count) Values.Add(Value);
		}

#if !TAF_TESTS
		/// <summary>
		/// Named fields, replacing the positional path outright.
		/// <para>
		/// <c>IComponent&lt;T&gt;.Write</c> reflects over fields IN DECLARATION ORDER by default
		/// (<c>D/XRL/World/IComponent.cs:4396-4425</c>), which is the trap the precedent's own
		/// repertoire wrote its warning about: a field-layout change between mod versions silently
		/// drops the part, and with it everything the founder had done to them. Named fields are
		/// self-describing &mdash; an unknown name is skipped, a missing one keeps its default
		/// &mdash; so every schema at or below this one reads, and adding a field costs a name.
		/// (<c>IPart</c> has no <c>WantFieldReflection</c> knob; only <c>IComposite</c> does. For a
		/// part, overriding these two IS the opt-out.)
		/// </para>
		/// </summary>
		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomLabRecord));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomLabRecord));
			Normalize();
		}
#endif
	}
}
