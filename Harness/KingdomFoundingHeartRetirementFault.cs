using System;
using System.Collections.Generic;
using XRL.Collections;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomFoundingHeartRetirementFault
	{
		private const string ScratchKey = "r_TAF_ScenarioHeartRetirementLive_v1";
		private static readonly List<KingdomFoundingHeartRetirementFault> Retained = new List<KingdomFoundingHeartRetirementFault>();
		private readonly KingdomFoundingHeartRetirementSnapshot Snapshot;
		private readonly List<GameObject> Bodies = new List<GameObject>();
		private RingDeque<GameObject> Queue;
		private GameObject[] QueueRows;
		private KingdomFoundingHeartRetirementSnapshot.Table<string> Strings;
		private KingdomFoundingHeartRetirementSnapshot.Table<int> Ints;
		private KingdomFoundingHeartRetirementSnapshot.BodyState LiveState;
		private GameObject LiveBody;
		private string Kind;
		private bool Installed, Restored, Failed;

		internal KingdomFoundingHeartRetirementFault(KingdomFoundingHeartLifecycleWorld world, GameObject predecessor, GameObject final)
		{
			Retained.Add(this);
			Snapshot = new KingdomFoundingHeartRetirementSnapshot(world, predecessor, final);
		}

		internal void Install(string kind)
		{
			Guard(() => {
				Check(!Installed && !Restored && Kind == null, "retirement injection cannot be reused");
				Snapshot.VerifyBaseline();
				Check(kind == "duplicate" || kind == "null-collection" || kind == "overbound"
					|| kind == "wrong-owner" || kind == "wrong-slot" || kind == "live-conflict", "unknown retirement fault");
				Kind = kind;
				if (kind == "duplicate" || kind == "null-collection" || kind == "overbound") InstallQueue();
				else if (kind == "wrong-owner" || kind == "wrong-slot") InstallMaps();
				else InstallLiveConflict();
				Installed = true;
				VerifyInstalled();
			});
		}

		private void InstallQueue()
		{
			if (Kind != "null-collection")
			{
				int count = Kind == "overbound" ? 65537 : Snapshot.OriginalQueue.Count + 1;
				Queue = new RingDeque<GameObject>(count); QueueRows = new GameObject[count];
				for (int i = 0; i < count; i++)
				{
					GameObject body = Kind == "duplicate" && i < Snapshot.OriginalQueue.Count
						? Snapshot.OriginalQueue[i] : Snapshot.Predecessor.Body;
					QueueRows[i] = body; Queue.Enqueue(body);
				}
				Check(Queue.Count == count, "retirement queue was not actually populated");
			}
			Snapshot.VerifyBaseline();
			Snapshot.Graveyard.Objects = Queue;
		}

		private void InstallMaps()
		{
			var strings = new Dictionary<string, string>(Snapshot.Predecessor.Strings.Source, Snapshot.Predecessor.Strings.Source.Comparer);
			var ints = new Dictionary<string, int>(Snapshot.Predecessor.Ints.Source, Snapshot.Predecessor.Ints.Source.Comparer);
			if (Kind == "wrong-owner") strings[KingdomPlots.FoundingHeartOwnerProperty] = "retirement-fixture-foreign-owner";
			else ints[KingdomPlots.FoundingHeartSlotProperty] = 0;
			Strings = new KingdomFoundingHeartRetirementSnapshot.Table<string>(strings);
			Ints = new KingdomFoundingHeartRetirementSnapshot.Table<int>(ints);
			Snapshot.VerifyBaseline();
			Snapshot.Predecessor.Body.Property = strings;
			Snapshot.Predecessor.Body.IntProperty = ints;
		}

		private void InstallLiveConflict()
		{
			Check(!KingdomNativeRegressionContext.HasAnyState(Snapshot.World.Game, ScratchKey), "retirement scratch key is occupied");
			GameObject captured = null;
			int captures = 0;
			GameObject returned = GameObject.Create("Chest", BeforeObjectCreated: body => {
				Bodies.Add(body); captured = body; captures++;
				Check(captures == 1 && body != null, "live conflict factory repeated its original");
				body.SetIntProperty("NoLoot", 1);
			});
			if (returned != null) Bodies.Add(returned);
			Check(captures == 1 && ReferenceEquals(returned, captured) && GameObject.Validate(returned)
				&& returned.Blueprint == "Chest" && returned.CurrentCell == null && returned.CurrentZone == null
				&& returned.InInventory == null && returned.Equipped == null && returned.Count == 1,
				"live conflict factory changed exact fresh custody");
			Snapshot.VerifyBaseline();
			LiveBody = returned;
			LiveBody.IDIfAssigned = Snapshot.Predecessor.Id;
			LiveState = new KingdomFoundingHeartRetirementSnapshot.BodyState(LiveBody);
			Snapshot.VerifyBaseline();
			Check(!KingdomNativeRegressionContext.HasAnyState(Snapshot.World.Game, ScratchKey), "retirement scratch key appeared during allocation");
			Snapshot.World.Game.ObjectGameState.Add(ScratchKey, LiveBody);
		}

		internal void VerifyInstalled()
		{
			Guard(() => {
				Check(Installed && !Restored, "retirement injection is not installed");
				Snapshot.VerifyInjection(IsQueue ? Queue : Snapshot.OriginalQueue,
					Strings == null ? Snapshot.Predecessor.Strings.Source : Strings.Source,
					Ints == null ? Snapshot.Predecessor.Ints.Source : Ints.Source,
					Kind == "live-conflict" ? ScratchKey : null, LiveBody);
				if (IsQueue && Queue != null)
				{
					Check(Queue.Count == QueueRows.Length, "owned replacement queue count changed");
					for (int i = 0; i < QueueRows.Length; i++) Check(ReferenceEquals(Queue[i], QueueRows[i]), "owned replacement queue row changed");
				}
				if (Strings != null) { Strings.Verify(Snapshot.Predecessor.Body.Property); Ints.Verify(Snapshot.Predecessor.Body.IntProperty); }
				if (LiveState != null) LiveState.Verify();
			});
		}

		internal void VerifyAndRestore()
		{
			Guard(() => {
				VerifyInstalled();
				if (IsQueue) Snapshot.Graveyard.Objects = Snapshot.OriginalQueue;
				else if (Strings != null)
				{
					Snapshot.Predecessor.Body.Property = Snapshot.Predecessor.Strings.Source;
					Snapshot.Predecessor.Body.IntProperty = Snapshot.Predecessor.Ints.Source;
				}
				else Check(Snapshot.World.Game.ObjectGameState.Remove(ScratchKey), "owned live-conflict root could not be removed");
				Snapshot.VerifyBaseline();
				if (LiveState != null) LiveState.Verify();
				Restored = true;
			});
		}

		internal void VerifyBaseline()
		{
			Guard(() => { Check(!Installed || Restored, "retirement injection is still installed"); Snapshot.VerifyBaseline(); });
		}
		private bool IsQueue { get { return Kind == "duplicate" || Kind == "null-collection" || Kind == "overbound"; } }
		private void Guard(Action action)
		{
			Check(!Failed, "prior retirement proof failed; unknown evidence retained");
			try { action(); } catch { Failed = true; throw; }
		}
		private static void Check(bool condition, string failure) { KingdomFoundingHeartRetirementSnapshot.Check(condition, failure); }
	}
}
