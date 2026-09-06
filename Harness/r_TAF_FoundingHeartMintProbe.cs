using System;
using System.Collections.Generic;
using XRL.World;

namespace XRL.World.Parts
{
	[Serializable]
	public sealed class r_TAF_FoundingHeartMintProbe : IPart
	{
		private const int MaximumObserved = 64;
		private static readonly List<GameObject> Observed = new List<GameObject>();
		internal static Action<GameObject, BeforeObjectCreatedEvent> Callback;
		internal static int Count { get { return Observed.Count; } }
		internal static string Error { get; private set; }

		internal static GameObject[] Snapshot()
		{
			return Observed.ToArray();
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeObjectCreatedEvent.ID;
		}

		public override bool HandleEvent(BeforeObjectCreatedEvent E)
		{
			try
			{
				Observe(E);
				return base.HandleEvent(E);
			}
			catch (Exception error)
			{
				RecordError("mint probe callback or handler failed: " + error.GetType().Name);
				return true;
			}
		}

		private void Observe(BeforeObjectCreatedEvent E)
		{
			Action<GameObject, BeforeObjectCreatedEvent> callback = Callback;
			if (callback == null) return;
			if (Observed.Count >= MaximumObserved)
			{
				RecordError("mint probe observation limit reached; additional original not retained");
				return;
			}
			GameObject original = ParentObject;
			if (original != null) Observed.Add(original);
			if (E == null || original == null || !ReferenceEquals(E.Object, original))
			{
				RecordError("mint probe creation event does not name its exact parent");
				return;
			}
			// Creation events are pooled; only original object references enter retained evidence.
			if (Error == null) callback(original, E);
		}

		private static void RecordError(string detail)
		{
			if (Error == null) Error = detail.Length <= 512 ? detail : detail.Substring(0, 512);
		}
	}
}
