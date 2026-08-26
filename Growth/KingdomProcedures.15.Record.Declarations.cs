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

	/// <summary>
	/// What the lab has done to one founder, and what it may never do to them again.
	/// <para>
	/// <b>Named fields from version one, deliberately</b> (STANDARDS &sect;1). The precedent's own
	/// repertoire carries a hand-rolled magic header because it learned that positional reflection
	/// silently drops a part whose field layout moved between mod versions &mdash; and with it the
	/// player's entire collection. This mod's answer to the same lesson is the one the rest of the
	/// codebase already keeps: <c>WantFieldReflection</c> off and named fields on, which are
	/// self-describing, so an unknown name is skipped and a missing one keeps its default. Every
	/// schema at or below the current one is readable, and adding a field is free.
	/// </para>
	/// <para>
	/// Three parallel lists rather than a list of composites, for the reason every register in this
	/// mod is written that way: primitives round-trip through the engine's own writer without a
	/// custom composite reader, and a list that grows a fourth column costs a name and nothing else.
	/// </para>
	/// </summary>
	[Serializable]
	public partial class r_KingdomLabRecord : IPart
	{
		/// <summary>Procedure keys performed, in the order they were performed.</summary>
		public List<string> Keys = new List<string>();

		/// <summary>The <c>BodyPart.Type</c> each one was performed at, index for index. Empty for a
		/// mutation, which is performed on the whole of a person.</summary>
		public List<string> Places = new List<string>();

		/// <summary>Whether each one rode a natural weapon rather than the founder themselves, index
		/// for index. What <c>KingdomProcedures.Remove</c> would otherwise have to guess.</summary>
		public List<bool> OnWeapon = new List<bool>();

		/// <summary>Stable selected body-part identity, index for index.</summary>
		public List<int> BodyPartIds = new List<int>();

		/// <summary>Stable exact effect bearer identity, index for index.</summary>
		public List<string> BearerIds = new List<string>();

		/// <summary>Commission identity written into the ownership marker.</summary>
		public List<string> JobIds = new List<string>();

		/// <summary>Frozen execution contract. DisplayNames is presentation only; the remaining
		/// columns authorize exact recovery and removal.</summary>
		public List<string> DisplayNames = new List<string>();
		public List<string> Grants = new List<string>();
		public List<int> Sources = new List<int>();
		public List<int> Attaches = new List<int>();
		public List<string> Managers = new List<string>();
		public List<string> Details = new List<string>();
		public List<string> Fingerprints = new List<string>();
		public List<int> PartOrdinals = new List<int>();
		public List<string> EffectNonces = new List<string>();
		public bool RegistryQuarantined;
		public string RegistryFault = "";

		/// <summary>
		/// Named procedures this founder has had, ever, whether or not the graft is still on them.
		/// <para>
		/// Separate from <see cref="Keys"/> and it must stay separate: taking the Weeping Graft off
		/// does not un-weep it, and a founder who could have it re-done by removing it would have a
		/// once-ever procedure that was neither.
		/// </para>
		/// </summary>
		public string NamedLatch = "";

		/// <summary>Procedures this founder never wants offered again. The third answer of the
		/// three-way consent prompt, and it is permanent because that is what it promised.</summary>
		public List<string> Excluded = new List<string>();

		/// <summary>Whether the city has already spoken against the hall. Once is the whole of it.</summary>
		public bool SpokenAgainst;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
		{
			r_KingdomLabRecord copy = (r_KingdomLabRecord)base.DeepCopy(Parent, MapInv);
			copy.Keys = new List<string>(Keys ?? new List<string>());
			copy.Places = new List<string>(Places ?? new List<string>());
			copy.OnWeapon = new List<bool>(OnWeapon ?? new List<bool>());
			copy.BodyPartIds = new List<int>(BodyPartIds ?? new List<int>());
			copy.BearerIds = new List<string>(BearerIds ?? new List<string>());
			copy.JobIds = new List<string>(JobIds ?? new List<string>());
			copy.DisplayNames = new List<string>(DisplayNames ?? new List<string>());
			copy.Grants = new List<string>(Grants ?? new List<string>());
			copy.Sources = new List<int>(Sources ?? new List<int>());
			copy.Attaches = new List<int>(Attaches ?? new List<int>());
			copy.Managers = new List<string>(Managers ?? new List<string>());
			copy.Details = new List<string>(Details ?? new List<string>());
			copy.Fingerprints = new List<string>(Fingerprints ?? new List<string>());
			copy.PartOrdinals = new List<int>(PartOrdinals ?? new List<int>());
			copy.EffectNonces = new List<string>(EffectNonces ?? new List<string>());
			copy.Excluded = new List<string>(Excluded ?? new List<string>());
			return copy;
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			for (int i = 0; i < EffectNonces.Count; i++)
				EffectNonces[i] = Guid.NewGuid().ToString("N");
			RegistryQuarantined = true;
			RegistryFault = "Copied patient receipt has fresh nonces and no procedure authority.";
		}
	}
}
