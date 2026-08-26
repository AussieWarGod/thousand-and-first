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
	/// Bearer-side proof of which exact live effect one lab commission owns. Primitive named fields
	/// survive saves; runtime references are rebuilt only while this proof remains present. A
	/// PartRemovedEvent erases the proof before a same-class replacement can inherit it.
	/// </summary>
	[Serializable]
	public partial class r_KingdomLabEffectLedger : IPart
	{
		public List<string> ProcedureKeys = new List<string>();
		public List<string> JobIds = new List<string>();
		public List<string> PatientIds = new List<string>();
		public List<int> BodyPartIds = new List<int>();
		public List<int> Sources = new List<int>();
		public List<string> ClassNames = new List<string>();
		public List<int> Attaches = new List<int>();
		public List<string> Managers = new List<string>();
		public List<string> Details = new List<string>();
		public List<string> Fingerprints = new List<string>();
		public List<int> PartOrdinals = new List<int>();
		public List<int> BindingStates = new List<int>();
		public List<string> EffectNonces = new List<string>();
		public bool LedgerQuarantined;

		[NonSerialized]
		private List<IPart> RuntimeParts;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
		{
			r_KingdomLabEffectLedger copy = (r_KingdomLabEffectLedger)base.DeepCopy(Parent, MapInv);
			copy.ProcedureKeys = new List<string>(ProcedureKeys ?? new List<string>());
			copy.JobIds = new List<string>(JobIds ?? new List<string>());
			copy.PatientIds = new List<string>(PatientIds ?? new List<string>());
			copy.BodyPartIds = new List<int>(BodyPartIds ?? new List<int>());
			copy.Sources = new List<int>(Sources ?? new List<int>());
			copy.ClassNames = new List<string>(ClassNames ?? new List<string>());
			copy.Attaches = new List<int>(Attaches ?? new List<int>());
			copy.Managers = new List<string>(Managers ?? new List<string>());
			copy.Details = new List<string>(Details ?? new List<string>());
			copy.Fingerprints = new List<string>(Fingerprints ?? new List<string>());
			copy.PartOrdinals = new List<int>(PartOrdinals ?? new List<int>());
			copy.BindingStates = new List<int>(BindingStates ?? new List<int>());
			copy.EffectNonces = new List<string>(EffectNonces ?? new List<string>());
			copy.RuntimeParts = null;
			return copy;
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			Normalize();
			for (int i = 0; i < EffectNonces.Count; i++)
			{
				EffectNonces[i] = Guid.NewGuid().ToString("N");
				BindingStates[i] = 2;
			}
			LedgerQuarantined = true;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == PooledEvent<PartRemovedEvent>.ID;
		}

		public override bool HandleEvent(PartRemovedEvent E)
		{
			Normalize();
			for (int i = ProcedureKeys.Count - 1; i >= 0; i--)
			{
				IPart runtime = RuntimeParts[i];
				if (ReferenceEquals(runtime, E.Part))
				{
					if (BindingStates[i] == 4 || BindingStates[i] == 3)
					{
						BindingStates[i] = 3;
					}
					else
					{
						ForgetAt(i, CleanupPatient: true);
					}
				}
			}
			return base.HandleEvent(E);
		}

		public override void ObjectLoaded()
		{
			base.ObjectLoaded();
			Normalize();
			for (int i = 0; i < ProcedureKeys.Count; i++)
			{
				RebindAt(i);
			}
		}
	}
}
