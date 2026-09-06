using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Clean developer setup through native movement and production heart completion.
	/// The future calendar argument is synthetic; clock, position and failed effects are never reset.</summary>
	internal sealed class KingdomScenarioCompletedHeart
	{
		private static readonly List<KingdomScenarioCompletedHeart> Retained = new List<KingdomScenarioCompletedHeart>();
		private readonly XRLGame Game;
		private readonly KingdomSystem System;
		private readonly Zone Zone;
		private readonly GameObject Player;
		private readonly long Tick;
		private readonly string RealmId;
		private readonly string SettlementId;
		private readonly string FactionName;
		private readonly string ZoneId;
		private KingdomFoundingHeartPlan Plan;
		private string PlanWire;
		private GameObject Predecessor;
		private Cell WorkCell;
		private r_KingdomPlotWorks Works;
		private GameObject Final;

		private KingdomScenarioCompletedHeart(XRLGame game, KingdomSystem system, Zone zone)
		{
			Retained.Add(this);
			Game = game; System = system; Zone = zone; Player = The.Player;
			Check(game != null && system != null && zone != null, "completed-heart setup lacks its exact owner");
			Tick = game.TimeTicks; RealmId = system.CurrentRealmId;
			SettlementId = system.CurrentSettlementId; FactionName = system.KingdomFactionName; ZoneId = zone.ZoneID;
		}

		internal static void Complete(XRLGame game, KingdomSystem system, Zone zone)
		{
			var witness = new KingdomScenarioCompletedHeart(game, system, zone);
			witness.Prepare();
			witness.WalkWest();
			long completionTick = checked(witness.Plan.StartedTick + witness.Plan.TotalTicks);
			Check(completionTick > witness.Tick, "completed-heart setup needs its explicit future calendar frontier");
			witness.Current(); witness.Authority();
			Check(ReferenceEquals(witness.Live(KingdomFoundingHeartRules.SlotId(witness.Plan,
				KingdomFoundingHeartRules.WorksSlot)), witness.Predecessor)
				&& ReferenceEquals(witness.Predecessor.CurrentCell, witness.WorkCell)
				&& ReferenceEquals(witness.Predecessor.GetPart<r_KingdomPlotWorks>(), witness.Works)
				&& witness.Works.StageApplied == (int)KingdomPlotRules.PlotStage.Staked,
				"completed-heart predecessor changed while the founder walked clear");
			KingdomPlots.Advance(witness.Works, system, completionTick);
			witness.Final = witness.Completed();
			witness.RecoverWithoutReplay();
		}

		private void Current()
		{
			Check(ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
				&& ReferenceEquals(The.Player, Player) && GameObject.Validate(Player)
				&& ReferenceEquals(Player.CurrentZone, Zone) && ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)
				&& Tick >= 0 && Game.TimeTicks == Tick && Zone.ZoneID == ZoneId
				&& System.Founded && System.OwnedZone(ZoneId)
				&& !string.IsNullOrEmpty(RealmId) && System.CurrentRealmId == RealmId
				&& !string.IsNullOrEmpty(SettlementId) && System.CurrentSettlementId == SettlementId
				&& System.KingdomFactionName == FactionName && Game.StringGameState != null
				&& Game.IntGameState != null && Game.Int64GameState != null
				&& Game.BooleanGameState != null && Game.ObjectGameState != null,
				"completed-heart game, player, owner, tables, ground or real clock changed");
			Faction realm = Factions.GetIfExists(FactionName);
			Check(realm != null && realm.IntProperties != null
				&& realm.IntProperties.TryGetValue("TAFFoundingPending", out int pending) && pending == 0
				&& (realm.Properties == null || !realm.Properties.ContainsKey("TAFFoundingPending"))
				&& Zone.GetZoneProperty(KingdomFoundingTransaction.SiteReservationProperty, null) == null
				&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.Committed,
				"completed-heart setup lacks committed, nonpending founding authority");
		}

		private void Prepare()
		{
			Current();
			PlanWire = Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null);
			Check(KingdomFoundingHeartRules.TryDecode(PlanWire, out Plan), "completed-heart plan is unreadable");
			Authority();
			Check(Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null) == null
				&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalFailureProperty, null) == null,
				"completed-heart setup found prior terminal evidence");
			string finalId = KingdomFoundingHeartRules.StableId(Plan.TransactionId, Plan.ZoneId, "final");
			Absent(KingdomPlots.FoundingHeartFinalRootPrefix + finalId);
			Check(KingdomPlots.FindGlobalFoundingHeartId(finalId, out _, out _) == KingdomPhysicalLookupState.Absent,
				"completed-heart final identity already exists");
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
			{
				GameObject body = Live(KingdomFoundingHeartRules.SlotId(Plan, slot));
				if (slot == KingdomFoundingHeartRules.WorksSlot) Predecessor = body;
			}
			Check(Predecessor != null && Predecessor.Blueprint == "r_KingdomPlotWorks"
				&& !Predecessor.HasIntProperty(KingdomPlots.PlotWorkSchemaProperty)
				&& !Predecessor.HasStringProperty(KingdomPlots.PlotWorkSchemaProperty),
				"completed-heart predecessor lost its schema-zero calendar contract");
			WorkCell = Predecessor.CurrentCell;
			Works = Predecessor.GetPart<r_KingdomPlotWorks>();
			Check(Works != null && ReferenceEquals(Works.ParentObject, Predecessor) && Works.DesignKey == "heartbasin"
				&& Works.StartTick == Plan.StartedTick && Works.TotalTicks == Plan.TotalTicks
				&& Works.StageApplied == (int)KingdomPlotRules.PlotStage.Staked,
				"completed-heart predecessor is not its exact fresh staked work");
			Check(KingdomPlots.AuditFoundingHeartReservations(System, Zone), "completed-heart initial audit refused");
			Current(); Authority();
		}

		private void Authority()
		{
			Current();
			Check(KingdomFoundingHeartRules.Complete(Plan) && Plan.ZoneId == ZoneId
				&& KingdomFoundingHeartRules.Encode(Plan) == PlanWire
				&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null) == PlanWire
				&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartSealProperty, null)
					== KingdomFoundingHeartRules.CompletionSeal(Plan), "completed-heart frozen plan or seal changed");
			for (int slot = 0; slot <= KingdomFoundingHeartRules.SlotCount; slot++)
			{
				string role = slot == KingdomFoundingHeartRules.SlotCount ? "final"
					: "slot-" + slot.ToString(CultureInfo.InvariantCulture);
				string id = KingdomFoundingHeartRules.StableId(Plan.TransactionId, Plan.ZoneId, role);
				Check(KingdomScenarioDurableState.ProvesExactText(KingdomPlots.FoundingHeartReservationPrefix + id,
					KingdomFoundingHeartReservationRules.Encode(Plan, id, role)), "completed-heart reservation changed");
				Absent((slot == KingdomFoundingHeartRules.SlotCount ? KingdomPlots.FoundingHeartFinalRootPrefix
					: KingdomPlots.FoundingHeartRootPrefix) + id);
			}
		}

		private void WalkWest()
		{
			Current();
			Check(Plan.RectX1 > 1 && Player.CurrentCell != null, "completed-heart westward route is unavailable");
			int moves = 0;
			while (Player.CurrentCell.X >= Plan.RectX1)
			{
				int x = Player.CurrentCell.X, y = Player.CurrentCell.Y;
				Check(++moves <= 32 && Player.Move("W", AllowDashing: false, DoConfirmations: false),
					"completed-heart founder could not walk clear");
				Current(); Authority();
				Check(Player.CurrentCell != null && Player.CurrentCell.X == x - 1 && Player.CurrentCell.Y == y,
					"completed-heart founder movement changed destination");
			}
			Check(Player.CurrentCell.X < Plan.RectX1, "completed-heart founder still occupies the footprint");
		}

		private GameObject Completed()
		{
			Current(); Authority();
			string wire = Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null);
			Check(KingdomFoundingHeartTerminalRules.TryDecode(wire, out KingdomFoundingHeartTerminalPlan terminal)
				&& terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled
				&& terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled
				&& terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled,
				"completed-heart terminal effects did not settle; failure="
					+ (Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalFailureProperty, null) ?? "none"));
			Check(terminal.TransactionId == Plan.TransactionId && terminal.ZoneId == Plan.ZoneId
				&& terminal.CompletionSeal == KingdomFoundingHeartRules.CompletionSeal(Plan)
				&& terminal.PredecessorId == KingdomFoundingHeartRules.SlotId(Plan, KingdomFoundingHeartRules.WorksSlot)
				&& terminal.FinalId == KingdomFoundingHeartRules.StableId(Plan.TransactionId, Plan.ZoneId, "final")
				&& terminal.Blueprint == "r_KingdomRiteGround" && terminal.BuildKey == "heartbasin"
				&& terminal.PlotId == Plan.PlotId && terminal.X == WorkCell.X && terminal.Y == WorkCell.Y,
				"completed-heart terminal binding changed");
			GameObject final = Live(terminal.FinalId);
			Check(final.Blueprint == terminal.Blueprint && ReferenceEquals(final.CurrentCell, WorkCell)
				&& ReferenceEquals(final.CurrentCell, Zone.GetCell(terminal.X, terminal.Y))
				&& Text(final, KingdomPlots.FoundingHeartTerminalProperty, wire)
				&& Text(final, KingdomUpgrade.BuildKeyProperty, terminal.BuildKey)
				&& Text(final, KingdomPlots.PlotIdProperty, Plan.PlotId)
				&& r_KingdomScaffold.HasRemovalProof(final, terminal.PredecessorId)
				&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalFailureProperty, null) == null,
				"completed-heart final mirror, identity or typed removal proof changed");
			Check(!GameObject.Validate(Predecessor) && Predecessor.IDIfAssigned == terminal.PredecessorId
				&& KingdomPlots.FindGlobalFoundingHeartId(terminal.PredecessorId, out _, out _)
					== KingdomPhysicalLookupState.Absent && ExactTombstone(terminal.PredecessorId),
				"completed-heart predecessor lacks its exact originating-zone tombstone");
			return final;
		}

		private bool ExactTombstone(string id)
		{
			var rows = Zone.Graveyard?.Objects;
			if (rows == null || rows.Count > 65536) return false;
			int matches = 0;
			foreach (GameObject body in rows)
				if (body != null && body.IDIfAssigned == id)
				{
					if (!ReferenceEquals(body, Predecessor) || GameObject.Validate(body)) return false;
					matches++;
				}
			return matches == 1;
		}

		private GameObject Live(string id)
		{
			Check(KingdomPlots.FindGlobalFoundingHeartId(id, out GameObject body, out bool graveyard)
				== KingdomPhysicalLookupState.Exact && !graveyard && GameObject.Validate(body)
				&& body.IDIfAssigned == id && ReferenceEquals(body.CurrentZone, Zone) && body.CurrentCell != null
				&& body.InInventory == null && body.Equipped == null && body.Count == 1,
				"completed-heart identity is not one exact live zone object");
			int count = 0;
			foreach (GameObject item in body.CurrentCell.GetObjects()) if (ReferenceEquals(item, body)) count++;
			Check(count == 1, "completed-heart live object has repeated or missing cell custody");
			return body;
		}

		private void RecoverWithoutReplay()
		{
			string wire = Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null);
			KingdomLedger ledger = System.Ledger;
			Check(ledger != null && ledger.Notes != null && Final.Property != null && Final.IntProperty != null,
				"completed-heart recovery snapshot unavailable");
			List<string> notes = ledger.Notes;
			string[] beforeNotes = notes.ToArray();
			GameObject[] bodies = new List<GameObject>(Zone.GetObjects()).ToArray();
			var strings = Final.Property; var ints = Final.IntProperty;
			var beforeStrings = new Dictionary<string, string>(strings);
			var beforeInts = new Dictionary<string, int>(ints);
			Current(); Authority();
			Check(KingdomPlots.RecoverFoundingHeart(System, Zone), "completed-heart settled recovery refused");
			Current(); Authority();
			Check(KingdomPlots.AuditFoundingHeartReservations(System, Zone), "completed-heart settled audit refused");
			Check(ReferenceEquals(Completed(), Final) && Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null) == wire
				&& ReferenceEquals(System.Ledger, ledger) && ReferenceEquals(ledger.Notes, notes)
				&& beforeNotes.Length == notes.Count, "completed-heart recovery changed authority or ledger");
			for (int i = 0; i < beforeNotes.Length; i++) Check(beforeNotes[i] == notes[i], "completed-heart recovery retold notes");
			GameObject[] after = new List<GameObject>(Zone.GetObjects()).ToArray();
			Check(after.Length == bodies.Length, "completed-heart recovery changed physical roster");
			for (int i = 0; i < bodies.Length; i++) Check(ReferenceEquals(after[i], bodies[i]), "completed-heart roster changed");
			Check(ReferenceEquals(Final.Property, strings) && ReferenceEquals(Final.IntProperty, ints),
				"completed-heart recovery replaced final property maps");
			Same(beforeStrings, strings); Same(beforeInts, ints); Current();
		}

		private static void Same<T>(Dictionary<string, T> before, Dictionary<string, T> after)
		{
			Check(before.Count == after.Count, "completed-heart final property count changed");
			foreach (KeyValuePair<string, T> row in before)
				Check(after.TryGetValue(row.Key, out T value) && EqualityComparer<T>.Default.Equals(row.Value, value),
					"completed-heart final property changed: " + row.Key);
		}
		private static bool Text(GameObject body, string key, string value)
		{
			return body.HasStringProperty(key) && !body.HasIntProperty(key) && body.GetStringProperty(key) == value;
		}
		private void Absent(string key)
		{
			Check(!KingdomNativeRegressionContext.HasAnyState(Game, key), "completed-heart root remains in a typed table: " + key);
		}
		private static void Check(bool condition, string failure)
		{
			if (!condition) throw new InvalidOperationException(failure);
		}
	}
}
