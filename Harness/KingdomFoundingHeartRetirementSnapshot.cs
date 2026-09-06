using System;
using System.Collections.Generic;
using XRL;
using XRL.Collections;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomFoundingHeartRetirementSnapshot
	{
		private static readonly List<KingdomFoundingHeartRetirementSnapshot> Retained = new List<KingdomFoundingHeartRetirementSnapshot>();
		internal readonly KingdomFoundingHeartLifecycleWorld World;
		internal readonly Graveyard Graveyard;
		internal readonly RingDeque<GameObject> OriginalQueue;
		internal readonly BodyState Predecessor, Final;
		private readonly XRLGame Game;
		private readonly ZoneManager Manager;
		private readonly GameObject Player;
		private readonly KingdomSystem System;
		private readonly object City;
		private readonly string Realm, Settlement, Transaction, ZoneId, GameId;
		private readonly long Tick;
		private readonly int Population, Crew, Stage, ProbeCount;
		private readonly Table<string> Strings;
		private readonly Table<int> Ints;
		private readonly Table<long> Longs;
		private readonly Table<object> Objects, ZoneValues;
		private readonly Table<bool> Booleans;
		private readonly Table<Zone> Cached;
		private readonly Table<Dictionary<string, object>> ZoneProperties;
		private readonly Dictionary<Zone, Graveyard> ZoneGraveyards = new Dictionary<Zone, Graveyard>();
		private readonly Dictionary<Graveyard, QueueState> Queues = new Dictionary<Graveyard, QueueState>();
		private readonly Graveyard GlobalGraveyard;
		private readonly GameObject[] Roster;
		private readonly BodyState PlayerState;
		private readonly KingdomLedger Ledger;
		private readonly List<string>[] News;
		private readonly string[][] NewsRows;
		private readonly int[] Counters;

		internal KingdomFoundingHeartRetirementSnapshot(KingdomFoundingHeartLifecycleWorld world, GameObject predecessor, GameObject final)
		{
			Retained.Add(this); World = world;
			Check(world != null, "retirement world is absent"); world.Current();
			Game = world.Game; Manager = The.ZoneManager; Player = The.Player; System = world.System;
			Check(Game != null && Manager != null && System != null && GameObject.Validate(Player), "retirement owner is absent");
			Tick = Game.TimeTicks; GameId = Game.GameID; City = System.City; Realm = System.CurrentRealmId; Settlement = System.CurrentSettlementId;
			Transaction = System.SettlementIdentityTransactionId; ZoneId = world.Zone.ZoneID;
			Population = System.Population; Crew = System.AssignedCrew; Stage = (int)System.Stage;
			Cached = new Table<Zone>(Manager.CachedZones);
			// Fresh fixture setup may allocate only already-loaded zones' lazy graveyards, before baseline capture.
			HashSet<Zone> zones = new HashSet<Zone> { world.Zone };
			foreach (Zone zone in Cached.Source.Values) if (zone != null) zones.Add(zone);
			Check(zones.Count <= 65536, "loaded-zone graveyard warmup exceeds its bound");
			GlobalGraveyard = Manager.Graveyard;
			foreach (Zone zone in zones) ZoneGraveyards.Add(zone, zone.Graveyard);
			Graveyard = world.Zone.Graveyard; OriginalQueue = Graveyard.Objects;
			Check(!ReferenceEquals(Graveyard, GlobalGraveyard), "owned zone graveyard aliases global custody");
			foreach (var row in ZoneGraveyards)
				Check(ReferenceEquals(row.Key, world.Zone) || !ReferenceEquals(row.Value, Graveyard), "owned graveyard aliases another zone");
			Queues.Add(GlobalGraveyard, new QueueState(GlobalGraveyard));
			foreach (Graveyard graveyard in ZoneGraveyards.Values)
				if (!Queues.ContainsKey(graveyard)) Queues.Add(graveyard, new QueueState(graveyard));
			int entries = 0;
			foreach (QueueState queue in Queues.Values) { entries += queue.Rows.Length; Check(entries <= 65536, "baseline graveyards exceed collector bound"); }
			Strings = new Table<string>(Game.StringGameState); Ints = new Table<int>(Game.IntGameState);
			Longs = new Table<long>(Game.Int64GameState); Objects = new Table<object>(Game.ObjectGameState);
			Booleans = new Table<bool>(Game.BooleanGameState);
			ZoneProperties = new Table<Dictionary<string, object>>(Manager.ZoneProperties);
			Check(Manager.ZoneProperties.TryGetValue(ZoneId, out var zoneValues), "retirement zone property map is absent");
			ZoneValues = new Table<object>(zoneValues);
			Predecessor = new BodyState(predecessor); Final = new BodyState(final); PlayerState = new BodyState(Player);
			Check(!GameObject.Validate(predecessor) && GameObject.Validate(final) && ReferenceEquals(final.CurrentZone, world.Zone),
				"retirement snapshot requires actual completed predecessor and final");
			Check(KingdomFoundingHeartRules.TryDecode(world.Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null), out var plan)
				&& KingdomFoundingHeartRules.Complete(plan) && plan.TransactionId == Transaction && plan.ZoneId == ZoneId
				&& predecessor.IDIfAssigned == KingdomFoundingHeartRules.SlotId(plan, KingdomFoundingHeartRules.WorksSlot)
				&& predecessor.HasStringProperty(KingdomPlots.FoundingHeartOwnerProperty) && !predecessor.HasIntProperty(KingdomPlots.FoundingHeartOwnerProperty)
				&& predecessor.HasIntProperty(KingdomPlots.FoundingHeartSlotProperty) && !predecessor.HasStringProperty(KingdomPlots.FoundingHeartSlotProperty)
				&& predecessor.GetStringProperty(KingdomPlots.FoundingHeartOwnerProperty) == Transaction
				&& predecessor.GetIntProperty(KingdomPlots.FoundingHeartSlotProperty) == KingdomFoundingHeartRules.WorksSlot + 1,
				"retained predecessor has foreign heart identity");
			int exact = 0;
			foreach (QueueState queue in Queues.Values)
			foreach (GameObject body in queue.Rows)
				if (body != null && body.IDIfAssigned == Predecessor.Id)
				{ Check(ReferenceEquals(queue.Owner, Graveyard) && ReferenceEquals(body, predecessor), "foreign baseline tombstone"); exact++; }
			Check(exact == 1, "baseline needs one actual originating-zone tombstone");
			string terminalWire = world.Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null);
			Check(KingdomFoundingHeartTerminalRules.TryDecode(terminalWire, out var terminal)
				&& terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled
				&& terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled && terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled
				&& terminal.TransactionId == Transaction && terminal.ZoneId == ZoneId && terminal.PredecessorId == Predecessor.Id
				&& terminal.FinalId == final.IDIfAssigned && terminal.Blueprint == final.Blueprint
				&& terminal.FinalId == KingdomFoundingHeartRules.StableId(Transaction, ZoneId, "final")
				&& terminal.CompletionSeal == KingdomFoundingHeartRules.CompletionSeal(plan)
				&& final.CurrentCell != null && ReferenceEquals(final.CurrentCell, world.Zone.GetCell(terminal.X, terminal.Y))
				&& final.HasStringProperty(KingdomPlots.FoundingHeartTerminalProperty) && !final.HasIntProperty(KingdomPlots.FoundingHeartTerminalProperty)
				&& final.GetStringProperty(KingdomPlots.FoundingHeartTerminalProperty) == terminalWire
				&& r_KingdomScaffold.HasRemovalProof(final, Predecessor.Id), "baseline completed terminal/final proof is absent");
			Roster = BoundedRoster(world.Zone); Ledger = System.Ledger;
			Check(Ledger != null, "retirement ledger is absent");
			News = new[] { Ledger.Notes, Ledger.BrinkLines, Ledger.ExpeditionLines }; NewsRows = new string[3][];
			for (int i = 0; i < News.Length; i++) { Check(News[i] != null && News[i].Count <= 65536, "retirement ledger shape is unbounded"); NewsRows[i] = News[i].ToArray(); }
			Counters = ReadCounters(Ledger); ProbeCount = r_TAF_FoundingHeartMintProbe.Count;
			VerifyBaseline();
		}

		internal void VerifyBaseline()
		{
			VerifyInjection(OriginalQueue, Predecessor.Strings.Source, Predecessor.Ints.Source, null, null);
		}

		internal void VerifyInjection(RingDeque<GameObject> queue, Dictionary<string, string> predecessorStrings,
			Dictionary<string, int> predecessorInts, string scratchKey, GameObject scratchBody)
		{
			World.Current();
			Check(ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.ZoneManager, Manager) && ReferenceEquals(The.ZoneManager, Manager)
				&& ReferenceEquals(The.Player, Player) && ReferenceEquals(World.System, System) && ReferenceEquals(System.City, City)
				&& ReferenceEquals(Manager.ActiveZone, World.Zone) && Game.TimeTicks == Tick && World.Zone.ZoneID == ZoneId
				&& !string.IsNullOrEmpty(GameId) && Game.GameID == GameId
				&& System.Founded && System.CurrentRealmId == Realm && System.CurrentSettlementId == Settlement
				&& System.SettlementIdentityTransactionId == Transaction && System.Population == Population
				&& System.AssignedCrew == Crew && (int)System.Stage == Stage, "retirement owner, clock or settlement changed");
			Strings.Verify(Game.StringGameState); Ints.Verify(Game.IntGameState); Longs.Verify(Game.Int64GameState);
			Objects.Verify(Game.ObjectGameState, scratchKey, scratchBody); Booleans.Verify(Game.BooleanGameState);
			Cached.Verify(Manager.CachedZones); ZoneProperties.Verify(Manager.ZoneProperties);
			Check(Manager.ZoneProperties.TryGetValue(ZoneId, out var values), "retirement zone map disappeared"); ZoneValues.Verify(values);
			Check(ReferenceEquals(Manager.Graveyard, GlobalGraveyard), "global graveyard changed");
			foreach (var row in ZoneGraveyards) Check(ReferenceEquals(row.Key.Graveyard, row.Value), "loaded zone graveyard changed");
			foreach (var row in Queues) row.Value.Verify(ReferenceEquals(row.Key, Graveyard) ? queue : row.Value.Queue);
			Predecessor.Verify(predecessorStrings, predecessorInts); Final.Verify(); PlayerState.Verify();
			SameReferences(Roster, BoundedRoster(World.Zone), "retirement physical roster changed");
			Check(ReferenceEquals(System.Ledger, Ledger) && ReferenceEquals(Ledger.Notes, News[0])
				&& ReferenceEquals(Ledger.BrinkLines, News[1]) && ReferenceEquals(Ledger.ExpeditionLines, News[2]), "retirement ledger replaced");
			for (int i = 0; i < News.Length; i++)
			{
				Check(News[i].Count == NewsRows[i].Length, "retirement ledger length changed");
				for (int j = 0; j < NewsRows[i].Length; j++) Check(News[i][j] == NewsRows[i][j], "retirement ledger text changed");
			}
			int[] counters = ReadCounters(Ledger);
			for (int i = 0; i < counters.Length; i++) Check(counters[i] == Counters[i], "retirement ledger accounting changed");
			Check(r_TAF_FoundingHeartMintProbe.Callback == null && r_TAF_FoundingHeartMintProbe.Error == null
				&& r_TAF_FoundingHeartMintProbe.Count == ProbeCount, "retirement probe allocated or retained an error");
		}

		internal sealed class Table<T>
		{
			internal readonly Dictionary<string, T> Source;
			private readonly Dictionary<string, T> Rows;
			internal Table(Dictionary<string, T> source)
			{
				Check(source != null && source.Count <= 65536, "retirement dictionary is absent or over bound");
				Source = source; Rows = new Dictionary<string, T>(source, source.Comparer);
			}
			internal void Verify(Dictionary<string, T> current, string extraKey = null, T extra = default(T))
			{
				Check(ReferenceEquals(current, Source) && current.Count == Rows.Count + (extraKey == null ? 0 : 1), "retirement dictionary identity/count changed");
				foreach (var row in Rows) Check(current.TryGetValue(row.Key, out T value) && Equal(row.Value, value), "retirement dictionary row changed: " + row.Key);
				if (extraKey != null) Check(!Rows.ContainsKey(extraKey) && current.TryGetValue(extraKey, out T extraValue) && Equal(extra, extraValue), "owned scratch root changed");
			}
			private static bool Equal(T a, T b) { return typeof(T) == typeof(object) ? ReferenceEquals(a, b) : EqualityComparer<T>.Default.Equals(a, b); }
		}

		internal sealed class BodyState
		{
			internal readonly GameObject Body;
			internal readonly string Id;
			internal readonly Table<string> Strings;
			internal readonly Table<int> Ints;
			private readonly string Blueprint;
			private readonly bool Valid, Live;
			private readonly int Flags, BaseId, Count;
			private readonly object[] Custody, Parts;
			internal BodyState(GameObject body)
			{
				Check(body != null && body.PartsList != null && body.PartsList.Count <= 4096, "retirement body shape unreadable");
				Body = body; Id = body.IDIfAssigned; Blueprint = body.Blueprint; Valid = GameObject.Validate(body);
				Live = body.Live; Flags = body.Flags; BaseId = body._BaseID; Count = body.Count;
				Strings = new Table<string>(body.Property); Ints = new Table<int>(body.IntProperty); Custody = ReadCustody(body);
				Parts = new object[body.PartsList.Count]; for (int i = 0; i < Parts.Length; i++) Parts[i] = body.PartsList[i];
			}
			internal void Verify() { Verify(Strings.Source, Ints.Source); }
			internal void Verify(Dictionary<string, string> strings, Dictionary<string, int> ints)
			{
				Check(Body.IDIfAssigned == Id && Body.Blueprint == Blueprint && GameObject.Validate(Body) == Valid
					&& Body.Live == Live && Body.Flags == Flags && Body._BaseID == BaseId && Body.Count == Count
					&& ReferenceEquals(Body.Property, strings) && ReferenceEquals(Body.IntProperty, ints), "retirement body identity/maps changed");
				Strings.Verify(Strings.Source); Ints.Verify(Ints.Source); SameReferences(Custody, ReadCustody(Body), "retirement body custody changed");
				Check(Body.PartsList.Count == Parts.Length, "retirement part count changed");
				for (int i = 0; i < Parts.Length; i++) Check(ReferenceEquals(Parts[i], Body.PartsList[i]), "retirement part attachment changed");
			}
			private static object[] ReadCustody(GameObject body)
			{
				return new object[] { body.CurrentZone, body.CurrentCell, body.InInventory, body.Equipped,
					body.Physics, body.PartsList, body.Inventory, body.Body };
			}
		}

		private sealed class QueueState
		{
			internal readonly Graveyard Owner;
			internal readonly RingDeque<GameObject> Queue;
			internal readonly GameObject[] Rows;
			private readonly int MaxCount;
			internal QueueState(Graveyard owner)
			{
				Check(owner != null && owner.Objects != null && owner.Objects.Count <= 65536, "baseline graveyard unreadable or over bound");
				Owner = owner; Queue = owner.Objects; MaxCount = owner.MaxCount; Rows = new GameObject[Queue.Count];
				for (int i = 0; i < Rows.Length; i++) Rows[i] = Queue[i];
			}
			internal void Verify(RingDeque<GameObject> expected)
			{
				Check(ReferenceEquals(Owner.Objects, expected) && Owner.MaxCount == MaxCount && Queue.Count == Rows.Length, "retirement graveyard identity/shape changed");
				for (int i = 0; i < Rows.Length; i++) Check(ReferenceEquals(Queue[i], Rows[i]), "original graveyard row changed");
			}
		}

		private static GameObject[] BoundedRoster(Zone zone)
		{
			List<GameObject> rows = zone.GetObjects(); Check(rows != null && rows.Count <= 65536, "retirement zone roster over bound"); return rows.ToArray();
		}
		private static int[] ReadCounters(KingdomLedger ledger)
		{
			return new[] { ledger.Fetched, ledger.UpkeepDrawn, ledger.ArrivalCost, ledger.Delivered, ledger.Harvested, ledger.Foraged,
				ledger.RationsDrawn, ledger.Milled, ledger.HarvestLost, ledger.Plundered, ledger.Arrivals, ledger.Departures };
		}
		private static void SameReferences<T>(T[] a, T[] b, string failure) where T : class
		{
			Check(a.Length == b.Length, failure); for (int i = 0; i < a.Length; i++) Check(ReferenceEquals(a[i], b[i]), failure);
		}
		internal static void Check(bool condition, string failure)
		{
			if (!condition) throw new InvalidOperationException(failure);
		}
	}
}
