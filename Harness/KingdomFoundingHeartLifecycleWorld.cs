using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>One retained production transaction with explicit recovery-capable audit calls.</summary>
	internal sealed class KingdomFoundingHeartLifecycleWorld
	{
		internal readonly XRLGame Game;
		internal readonly Zone Zone;
		internal readonly string Name;
		internal readonly long Tick;
		private string FrozenPlan;
		private string SiteAuthority;
		private KingdomSystem Owner;
		private Faction Realm;
		internal static KingdomFoundingHeartLifecycleWorld Retained { get; private set; }
		internal KingdomSystem System { get { return Game.GetSystem<KingdomSystem>(); } }

		internal KingdomFoundingHeartLifecycleWorld(XRLGame game, Zone zone)
		{
			Check(Retained == null, "prior lifecycle attempt retained");
			Game = game; Zone = zone; Tick = game.TimeTicks; Retained = this;
			Check(KingdomScenarioRealizer.TryBindStampedPlan(out KingdomScenarioPlan plan,
				out KingdomScenarioProvenance stamp, out string failure), failure);
			Check(plan.Key == "founding-first-city"
				&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority, "wrong founding plan");
			foreach (KingdomScenarioResolvedStep step in plan.Steps)
				if (step.Verb == KingdomScenarioVerb.FoundFirstCity)
				{
					Check(Name == null && step.Arguments.TryGetValue("CityName", out string name)
						&& !string.IsNullOrEmpty(name), "missing or repeated frozen city name");
					Name = step.Arguments["CityName"];
				}
			Check(Name != null && KingdomScenarioFoundingStep.TryProvePreconditions(zone, Name, out failure), failure);
			Check(KingdomScenarioTransactionMarker.TryBegin(out failure), failure);
		}

		internal void Current()
		{
			Check(ReferenceEquals(The.Game, Game) && ReferenceEquals(The.Player?.CurrentZone, Zone)
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && Game.TimeTicks == Tick,
				"lifecycle game, active ground or real clock changed");
			if (Owner != null) Check(ReferenceEquals(Owner, System), "founding system changed");
		}

		internal KingdomFoundingHeartPlan Plan()
		{
			Current();
			Check(KingdomFoundingHeartRules.TryDecode(Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null),
				out KingdomFoundingHeartPlan plan), "heart plan unreadable");
			KingdomFoundingHeartPlan identity = plan.Copy();
			identity.States = new int[KingdomFoundingHeartRules.SlotCount];
			string wire = KingdomFoundingHeartRules.Encode(identity);
			if (FrozenPlan == null) FrozenPlan = wire;
			Check(wire != null && FrozenPlan == wire, "frozen founding-heart authority changed");
			return plan;
		}

		internal void Pending(int slot)
		{
			KingdomFoundingHeartPlan plan = Plan();
			Check(KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.Attempted,
				"expected refusal did not retain its single attempt");
			Check(System != null && System.Founded, "partial founding did not retain published realm");
			if (Owner == null) Owner = System;
			string authority = Zone.GetZoneProperty(KingdomFoundingTransaction.SiteReservationProperty, null);
			if (SiteAuthority == null) SiteAuthority = authority;
			Check(!string.IsNullOrEmpty(authority) && SiteAuthority == authority, "pending site authority changed");
			Faction faction = Factions.GetIfExists(System.KingdomFactionName);
			if (Realm == null) Realm = faction;
			Check(faction != null && ReferenceEquals(Realm, faction) && faction.GetIntProperty("TAFFoundingPending") == 1
				&& faction.GetStringProperty(KingdomFoundingTransaction.PendingFactionAuthorityProperty) == authority
				&& faction.GetStringProperty(KingdomFoundingTransaction.RealmReservationProperty) == authority
				&& faction.GetStringProperty(KingdomFoundingTransaction.PendingFactionTransactionProperty) == plan.TransactionId,
				"pending realm identity changed");
			for (int i = 0; i < plan.States.Length; i++)
				Check(plan.States[i] == (i < slot ? 2 : 0), "refused slot checkpoint advanced");
			string id = KingdomFoundingHeartRules.SlotId(plan, slot);
			Absent(KingdomPlots.FoundingHeartRootPrefix + id);
			Check(Lookup(id, out _, out _) == KingdomPhysicalLookupState.Absent, "refused slot acquired global identity");
			Check(Zone.GetZoneProperty(KingdomPlots.FoundingHeartSealProperty, null) == null, "partial heart acquired seal");
		}

		internal r_KingdomPlotWorks Founded()
		{
			KingdomFoundingHeartPlan plan = Plan();
			Check(KingdomFoundingHeartRules.Complete(plan) && System.Founded
				&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartSealProperty, null) == KingdomFoundingHeartRules.CompletionSeal(plan),
				"production recovery did not finish and seal heart");
			Faction currentRealm = Factions.GetIfExists(System.KingdomFactionName);
			Check(ReferenceEquals(currentRealm, Realm) && currentRealm != null
				&& currentRealm.IntProperties != null
				&& currentRealm.IntProperties.TryGetValue("TAFFoundingPending", out int pending) && pending == 0
				&& (currentRealm.Properties == null || !currentRealm.Properties.ContainsKey("TAFFoundingPending"))
				&& Zone.GetZoneProperty(KingdomFoundingTransaction.SiteReservationProperty, null) == null,
				"completed founding left pending or superseded realm authority");
			GameObject work = null;
			for (int slot = 0; slot < plan.States.Length; slot++)
			{
				string id = KingdomFoundingHeartRules.SlotId(plan, slot);
				Check(Lookup(id, out GameObject body, out bool graveyard) == KingdomPhysicalLookupState.Exact
					&& !graveyard && GameObject.Validate(body) && ReferenceEquals(body.CurrentZone, Zone), "physical slot absent");
				Absent(KingdomPlots.FoundingHeartRootPrefix + id);
				if (slot == KingdomFoundingHeartRules.WorksSlot) work = body;
			}
			Check(work != null && !work.HasIntProperty(KingdomPlots.PlotWorkSchemaProperty)
				&& !work.HasStringProperty(KingdomPlots.PlotWorkSchemaProperty), "founding calendar contract changed");
			Check(KingdomPlots.AuditFoundingHeartReservations(System, Zone), "sealed heart audit refused");
			return work.GetPart<r_KingdomPlotWorks>();
		}

		internal void ClearPlayerFromHeart(KingdomFoundingHeartPlan plan)
		{
			Current();
			GameObject player = The.Player;
			Check(player != null && plan.RectX1 > 1 && player.CurrentCell != null,
				"safe westward departure from heart unavailable");
			int moves = 0;
			while (player.CurrentCell.X >= plan.RectX1)
			{
				int x = player.CurrentCell.X, y = player.CurrentCell.Y;
				Check(++moves <= 32 && player.Move("W", AllowDashing: false, DoConfirmations: false),
					"founder could not walk clear of the construction ground");
				Current();
				Check(ReferenceEquals(The.Player, player) && player.CurrentCell.X == x - 1
					&& player.CurrentCell.Y == y, "founder movement changed custody or destination");
			}
			Check(player.CurrentCell.X < plan.RectX1, "founder still occupies the heart plot");
		}

		internal void NoTerminal(r_KingdomPlotWorks works)
		{
			KingdomFoundingHeartPlan plan = Plan();
			string id = KingdomFoundingHeartRules.StableId(plan.TransactionId, plan.ZoneId, "final");
			Check(GameObject.Validate(works.ParentObject) && ReferenceEquals(works.ParentObject.CurrentZone, Zone)
				&& works.StageApplied == (int)KingdomPlotRules.PlotStage.Walls, "refused terminal consumed or changed predecessor");
			Check(Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null) == null
				&& !works.ParentObject.HasStringProperty("r_TAF_PlotFinalOutputId")
				&& !works.ParentObject.HasIntProperty("r_TAF_PlotFinalOutputId"), "refused final published intent");
			Absent(KingdomPlots.FoundingHeartFinalRootPrefix + id);
			Check(Lookup(id, out _, out _) == KingdomPhysicalLookupState.Absent, "refused final acquired global identity");
		}

		internal GameObject Completed(GameObject predecessor)
		{
			KingdomFoundingHeartPlan plan = Plan();
			Check(KingdomFoundingHeartTerminalRules.TryDecode(Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null),
				out KingdomFoundingHeartTerminalPlan terminal) && terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled
				&& terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled
				&& terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled,
				"terminal effects did not settle: phase=" + (terminal == null ? "absent" : terminal.Phase.ToString())
				+ " failure=" + (Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalFailureProperty, null) ?? "none"));
			Check(terminal.TransactionId == plan.TransactionId && terminal.Blueprint == "r_KingdomRiteGround"
				&& terminal.CompletionSeal == KingdomFoundingHeartRules.CompletionSeal(plan), "terminal authority changed");
			Check(Lookup(terminal.FinalId, out GameObject final, out bool graveyard) == KingdomPhysicalLookupState.Exact
				&& !graveyard && GameObject.Validate(final) && ReferenceEquals(final.CurrentZone, Zone)
				&& final.CurrentCell == Zone.GetCell(terminal.X, terminal.Y)
				&& final.GetStringProperty(KingdomPlots.FoundingHeartTerminalProperty) == KingdomFoundingHeartTerminalRules.Encode(terminal)
				&& r_KingdomScaffold.HasRemovalProof(final, terminal.PredecessorId), "exact final/removal proof absent");
			Check(!GameObject.Validate(predecessor)
				&& Lookup(terminal.PredecessorId, out _, out _) == KingdomPhysicalLookupState.Absent
				&& ExactTombstone(terminal.PredecessorId, predecessor), "predecessor lacks exact destruction tombstone");
			Absent(KingdomPlots.FoundingHeartFinalRootPrefix + terminal.FinalId);
			return final;
		}

		private static KingdomPhysicalLookupState Lookup(string id, out GameObject body, out bool graveyard)
		{
			return KingdomPlots.FindGlobalFoundingHeartId(id, out body, out graveyard);
		}

		private bool ExactTombstone(string id, GameObject predecessor)
		{
			// Independent native oracle: this predecessor was destroyed on this exact zone.
			var rows = Zone.Graveyard?.Objects;
			if (rows == null || rows.Count > 65536 || predecessor == null || predecessor.IDIfAssigned != id) return false;
			int matches = 0;
			foreach (GameObject body in rows)
				if (body != null && body.IDIfAssigned == id)
				{
					if (!ReferenceEquals(body, predecessor) || GameObject.Validate(body)) return false;
					matches++;
				}
			return matches == 1;
		}

		private void Absent(string key)
		{
			Check(!KingdomNativeRegressionContext.HasAnyState(Game, key), "root state unexpectedly present: " + key);
		}
		private static void Check(bool condition, string failure)
		{
			KingdomFoundingHeartAllocationNativeCases.Check(condition, failure);
		}
	}
}
