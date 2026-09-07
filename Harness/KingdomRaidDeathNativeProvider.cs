using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using XRL;
using XRL.Collections;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomRaidDeathNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "raid-death-native-check";
		internal const string Receipt = "r_TAF_ScenarioRaidDeathNative_v1";
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			if (verb != Verb || !string.IsNullOrEmpty(argument)) return Verb + " takes no arguments";
			XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
			try
			{
				if (!Eligible(game, zone, out string failure)) return "native raid death refused: " + failure;
				game.SetStringGameState(Receipt, "intent");
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "death intent did not persist exactly");
				string report = KingdomRaidDeathNativeChecks.Run(game, zone, out Ok);
				Require(ReferenceEquals(The.Game, game) && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"death intent or game changed");
				game.SetStringGameState(Receipt, report);
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, report), "death report did not persist exactly");
				return report;
			}
			catch (Exception error)
			{
				Ok = false;
				return "native raid death refused; evidence retained: "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
		private static bool Eligible(XRLGame game, Zone zone, out string failure)
		{
			failure = "requires fresh active unfounded marsh ground, enabled raids and native messages";
			if (game == null || zone == null || !ReferenceEquals(The.ZoneManager?.ActiveZone, zone)
				|| !MessageQueue.Enabled || !KingdomRaids.Enabled || !KingdomMaster.ConfiguredEnabled
				|| (game.GetSystem<KingdomSystem>()?.Founded ?? false)
				|| KingdomNativeRegressionContext.HasQuickstartState(game)
				|| KingdomNativeRegressionContext.HasAnyState(game, Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidContactNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptA)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptB1)
				|| KingdomRaidLaunchNativeFixture.LastAttempt != null || !r_TAF_RaidMintProbe.Vacant
				|| r_TAF_RaidMintProbe.Armed || r_TAF_RaidMintProbe.Book != null) return false;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out failure)) return false;
			if (plan.Key != "founding-first-city" || plan.AuthorityClass != KingdomScenarioFoundingStep.FoundingAuthority
				|| !KingdomScenarioScript.TryRead(out var script, out failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{ failure = "requires exact sealed death script and stamped founding plan"; return false; }
			failure = "requires unspent transaction and exact marsh zone";
			if (KingdomScenarioTransactionMarker.Observe(out _) != KingdomScenarioTransactionShape.None
				|| !KingdomQuickstartRules.TryProfile("marsh", out var profile) || zone.ZoneID != profile.ZoneId) return false;
			failure = null; return true;
		}
		internal static void Require(bool condition, string failure)
		{
			if (!condition) throw new InvalidOperationException(failure ?? "native death evidence refused");
		}
	}

	// Always calls the real handler. Observation is inert outside the retained, armed attempt.
	[HarmonyPatch(typeof(KingdomRaids), "RaiderDying", new Type[] { typeof(GameObject), typeof(r_KingdomRaiderObjective) })]
	internal static class KingdomRaidDeathNativeObserver
	{
		[HarmonyPrefix]
		internal static void Prefix(GameObject actor, r_KingdomRaiderObjective part)
		{ KingdomRaidDeathNativeChecks.Observe(true, actor, part); }
		[HarmonyPostfix]
		internal static void Postfix(GameObject actor, r_KingdomRaiderObjective part)
		{ KingdomRaidDeathNativeChecks.Observe(false, actor, part); }
	}

	// Retains boundary-visible roots, including native corpses/drops; not a whole-world effect proof.
	internal sealed class KingdomRaidDeathZoneEvidence
	{
		internal readonly Zone Zone;
		private readonly Cell[] Cells = new Cell[2000];
		private readonly Cell.ObjectRack[] Racks = new Cell.ObjectRack[2000];
		private readonly GameObject[][] Rows = new GameObject[2000][];
		private readonly HashSet<GameObject> Originals = new HashSet<GameObject>(ReferenceComparer.Instance);
		private readonly HashSet<GameObject> Retained = new HashSet<GameObject>(ReferenceComparer.Instance);
		private readonly Dictionary<GameObject, Cell> Present = new Dictionary<GameObject, Cell>(ReferenceComparer.Instance);
		private readonly Graveyard Graveyard;
		private readonly RingDeque<GameObject> Queue;
		private readonly GameObject[] Graves;
		private readonly int MaxCount;
		// Synthetic retention setup only: preserve every existing entry, never lower the limit.
		internal static void PrepareRetention(XRLGame game, ZoneManager manager, Zone zone, StringBuilder evidence)
		{
			Require(ReferenceEquals(The.Game, game) && ReferenceEquals(The.ZoneManager, manager) && zone != null
				&& manager.CachedZones.TryGetValue(zone.ZoneID, out var cached) && ReferenceEquals(cached, zone)
				&& evidence != null, "retention preparation lacks exact owned cached zone");
			Graveyard graveyard = zone.Graveyard; RingDeque<GameObject> queue = graveyard?.Objects;
			Require(queue != null, "retention preparation lacks original graveyard queue");
			int count = queue.Count, oldMax = graveyard.MaxCount;
			Require(count >= 0 && count <= 65520 && oldMax > 0 && oldMax <= 65536 && count <= oldMax,
				"retention preparation exceeds bounded graveyard shape");
			var rows = new GameObject[count];
			for (int i = 0; i < count; i++) { rows[i] = queue[i]; Require(rows[i] != null, "retention queue has null entry"); }
			int nextMax = Math.Max(oldMax, checked(count + 16));
			Require(ReferenceEquals(The.Game, game) && ReferenceEquals(The.ZoneManager, manager)
				&& manager.CachedZones.TryGetValue(zone.ZoneID, out cached) && ReferenceEquals(cached, zone)
				&& ReferenceEquals(zone.Graveyard, graveyard) && ReferenceEquals(graveyard.Objects, queue)
				&& queue.Count == count && graveyard.MaxCount == oldMax && nextMax <= 65536,
				"retention owner or queue changed before capacity preparation");
			for (int i = 0; i < count; i++) Require(ReferenceEquals(queue[i], rows[i]), "retention entry changed before preparation");
			graveyard.MaxCount = nextMax;
			Require(ReferenceEquals(zone.Graveyard, graveyard) && ReferenceEquals(graveyard.Objects, queue)
				&& queue.Count == count && graveyard.MaxCount == nextMax, "retention preparation changed queue or count");
			for (int i = 0; i < count; i++) Require(ReferenceEquals(queue[i], rows[i]), "retention preparation changed original entry");
			evidence.Append("\nsynthetic-graveyard-retention zone=").Append(KingdomScenarioRules.Bounded(zone.ZoneID))
				.Append(" entries=").Append(count).Append(" old-max=").Append(oldMax).Append(" new-max=").Append(nextMax)
				.Append(" originals=unchanged retained=true");
		}
		internal KingdomRaidDeathZoneEvidence(Zone zone)
		{
			Zone = zone; Require(zone != null && zone.Width == 80 && zone.Height == 25, "death zone dimensions differ");
			Graveyard = zone.Graveyard; Queue = Graveyard.Objects; MaxCount = Graveyard.MaxCount;
			Require(Queue != null && Queue.Count <= 65536 && MaxCount >= 16 && MaxCount <= 65536
				&& MaxCount - Queue.Count >= 16, "graveyard lacks bounded retention headroom");
			Graves = new GameObject[Queue.Count];
			for (int i = 0; i < Graves.Length; i++) { Graves[i] = Queue[i]; Retained.Add(Graves[i]); }
			for (int x = 0; x < 80; x++) for (int y = 0; y < 25; y++)
			{
				int i = x * 25 + y; Cells[i] = zone.GetCell(x, y); Racks[i] = Cells[i].Objects; Rows[i] = Racks[i].ToArray();
				Require(Rows[i].Length <= 512, "death cell exceeds bound");
				foreach (GameObject body in Rows[i])
				{
					Require(GameObject.Validate(body) && Originals.Count < 20000 && Originals.Add(body), "death baseline repeats or loses a body");
					Retained.Add(body); Present.Add(body, Cells[i]);
				}
			}
			Require(Retained.Count <= 24000, "death baseline retained roots exceed bound");
		}
		internal void Record(GameObject[] actors)
		{
			Present.Clear();
			for (int x = 0; x < 80; x++) for (int y = 0; y < 25; y++)
			{
				int i = x * 25 + y; Cell cell = Cells[i]; Cell.ObjectRack rack = cell.Objects;
				Require(ReferenceEquals(Zone.GetCell(x, y), cell) && ReferenceEquals(rack, Racks[i]) && rack.Count <= 512,
					"death cell or rack changed");
				int original = 0;
				foreach (GameObject body in rack)
				{
					Require(body != null && Present.Count < 20000 && !Present.ContainsKey(body), "death live root repeats or exceeds bound");
					Present.Add(body, cell); Retained.Add(body);
					Require(Retained.Count <= 24000 && GameObject.Validate(body)
						&& (body.Physics == null || ReferenceEquals(body.Physics._CurrentCell, cell)), "death live custody differs");
					if (Actor(body, actors) || !Originals.Contains(body)) continue;
					while (original < Rows[i].Length && Actor(Rows[i][original], actors)) original++;
					Require(original < Rows[i].Length && ReferenceEquals(Rows[i][original++], body), "unrelated death cell rows changed");
				}
				while (original < Rows[i].Length && Actor(Rows[i][original], actors)) original++;
				Require(original == Rows[i].Length, "an unrelated original disappeared during death");
			}
			Require(ReferenceEquals(Zone.Graveyard, Graveyard) && ReferenceEquals(Graveyard.Objects, Queue)
				&& Graveyard.MaxCount == MaxCount && Queue.Count >= Graves.Length && Queue.Count < MaxCount,
				"originating graveyard changed or reached eviction boundary");
			for (int i = 0; i < Queue.Count; i++)
			{
				Retained.Add(Queue[i]);
				if (i < Graves.Length) Require(ReferenceEquals(Queue[i], Graves[i]), "an original graveyard root was evicted");
			}
		}
		internal void At(GameObject body, Cell cell)
		{ Require(Present.TryGetValue(body, out var actual) && ReferenceEquals(actual, cell), "original live root has wrong membership"); }
		internal void Dead(GameObject body)
		{
			int count = 0; foreach (GameObject row in Queue) if (ReferenceEquals(row, body)) count++;
			Require(!Present.ContainsKey(body) && count == 1 && !GameObject.Validate(body) && body.IsInGraveyard()
				&& body.Physics?._CurrentCell == null && body.Physics?._InInventory == null && body.Physics?._Equipped == null,
				"Die did not remove the original into its originating-zone graveyard exactly once");
		}
		private static bool Actor(GameObject body, GameObject[] actors)
		{ foreach (GameObject actor in actors) if (ReferenceEquals(body, actor)) return true; return false; }
		private static void Require(bool value, string failure) { KingdomRaidDeathNativeProvider.Require(value, failure); }
		private sealed class ReferenceComparer : IEqualityComparer<GameObject>
		{
			internal static readonly ReferenceComparer Instance = new ReferenceComparer();
			public bool Equals(GameObject a, GameObject b) { return ReferenceEquals(a, b); }
			public int GetHashCode(GameObject value) { return RuntimeHelpers.GetHashCode(value); }
		}
	}
}
