using System;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Retained, synthetic native raid-launch setup: founds a real profile, seeds one
	/// dedicated store and one committed incident, then drives the REAL activation entry. Nothing
	/// is torn down; every raider, substitute and abandoned original is evidence.</summary>
	internal sealed class KingdomRaidLaunchNativeFixture
	{
		internal const string StoreBlueprint = "r_KingdomReservoir";
		internal const string AttackerFaction = "Snapjaws";
		internal const int PartySize = 3;
		internal const int StoredDrams = 240;
		/// <summary>The frozen roster tier, pinned rather than read off the settlement's current
		/// stage: Members (Raids/KingdomRaidProfiles.cs:28-34) returns the Steading roster below
		/// Village, and Snapjaws Steading (RuntimeData/KingdomRaidProfiles.xml:12) is exactly the
		/// two probed spawnable variants, not the refused BaseObject archetypes.</summary>
		internal const GrowthStage FrozenStage = GrowthStage.Steading;
		internal static readonly string[] ProbedBlueprints =
			new[] { "Snapjaw Scavenger 0", "Snapjaw Hunter 0" };
		internal static KingdomRaidLaunchNativeFixture LastAttempt { get; private set; }
		internal XRLGame Game { get; private set; }
		internal Zone Zone { get; private set; }
		internal KingdomSystem System { get; private set; }
		internal KingdomSurvey Survey { get; private set; }
		internal GameObject Store { get; private set; }
		internal KingdomRaidIncident Incident { get; private set; }

		private KingdomRaidLaunchNativeFixture(XRLGame Game, Zone Zone)
		{
			this.Game = Game;
			this.Zone = Zone;
		}

		/// <summary>True only for a blueprint the overlay merges the probe into.</summary>
		internal static bool Probed(string Blueprint)
		{
			return string.Equals(ProbedBlueprints[0], Blueprint, StringComparison.Ordinal)
				|| string.Equals(ProbedBlueprints[1], Blueprint, StringComparison.Ordinal);
		}

		/// <summary>Founding mirrors Harness/KingdomSubsidenceNativeFixture.cs:36-82.</summary>
		internal static bool TryCreate(Zone Zone, out KingdomRaidLaunchNativeFixture Fixture,
			out string Failure)
		{
			Fixture = LastAttempt;
			Failure = null;
			try
			{
				Require(Fixture == null, "a prior native raid-launch fixture remains retained");
				XRLGame game = The.Game;
				Require(game != null && Zone != null && ReferenceEquals(The.Player?.CurrentZone, Zone)
					&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone), "exact active game/zone absent");
				KingdomScenarioPlan plan;
				KingdomScenarioProvenance stamp;
				string failure;
				Require(KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out failure),
					"scenario provenance refused: " + failure);
				Require(plan.Key == "founding-first-city"
					&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority,
					"fixture requires the stamped first-founding plan");
				string name = null;
				foreach (KingdomScenarioResolvedStep step in plan.Steps)
					if (step.Verb == KingdomScenarioVerb.FoundFirstCity)
					{
						Require(name == null && step.Arguments.TryGetValue("CityName", out name)
							&& !string.IsNullOrEmpty(name), "exact founding name missing or repeated");
					}
				Require(name != null && KingdomScenarioFoundingStep.TryProvePreconditions(Zone,
					name, out failure), "founding preconditions refused: " + failure);
				Require(KingdomMaster.ConfiguredEnabled && KingdomRaids.Enabled,
					"master and raid options must be enabled");
				Fixture = new KingdomRaidLaunchNativeFixture(game, Zone);
				LastAttempt = Fixture;
				Require(KingdomScenarioTransactionMarker.TryBegin(out failure), failure);
				string line;
				Require(KingdomScenarioFoundingStep.TryFound(Zone, name, out line, out failure), failure);
				Require(KingdomScenarioTransactionMarker.TryCommit(out failure), failure);
				Fixture.System = game.GetSystem<KingdomSystem>();
				Fixture.Build();
				return true;
			}
			catch (Exception error)
			{
				Failure = "native raid-launch fixture retained after " + error.GetType().Name;
				try { Failure += ": " + error.Message; } catch (Exception) { }
				return false;
			}
		}

		/// <summary>Runs the REAL activation entry the zone-activation path uses
		/// (Raids/KingdomRaids.01.ActivationAndRecovery.cs:16-40). No test-side LaunchRaid.</summary>
		internal void Activate()
		{
			RequireWorld();
			KingdomRaids.OnZoneActivated(System, Zone);
		}

		private void Build()
		{
			RequireWorld();
			Require(System.LifecycleBook != null && System.LifecycleBook.Raid == null
				&& KingdomLifecycleRules.CanOwnAuthority(System.LifecycleBook)
				&& System.LifecycleBook.RaidLedger != null
				&& System.LifecycleBook.RaidLedger.Incidents.Count == 0,
				"founded fixture must own a free, empty raid lane");
			Cell cell = FirstEmptyCell();
			Store = Create(StoreBlueprint);
			LiquidVolume liquid = Store.GetPart<LiquidVolume>();
			Require(liquid != null && ReferenceEquals(liquid.ParentObject, Store)
				&& liquid.MaxVolume == 1920 && liquid.Volume == 0
				&& KingdomLiquids.CanReceiveFreshWater(liquid), "empty finite reservoir shape changed");
			Store.SetIntProperty("KingdomStores", 1);
			Require(EmptyCell(cell), "fixture destination changed before placement");
			GameObject placed = cell.AddObject(Store, NoStack: true);
			Require(ReferenceEquals(placed, Store) && GameObject.Validate(Store)
				&& ReferenceEquals(Store.CurrentCell, cell) && ReferenceEquals(Store.CurrentZone, Zone),
				"native placement did not preserve exact custody");
			Require(KingdomLiquids.Fill(liquid, "water", StoredDrams) == StoredDrams
				&& KingdomLiquids.HasFreshWater(liquid) && liquid.Volume == StoredDrams,
				"the fixture store did not receive its exact fresh water");
			// ExactStore sorts on GameObject.ID, which assigns one (07.cs:241-255).
			Require(!string.IsNullOrEmpty(Store.ID) && !string.IsNullOrEmpty(Store.IDIfAssigned),
				"the fixture store has no assigned physical identity");
			Survey = KingdomSurvey.Take(Zone, System);
			Require(ReferenceEquals(Survey.Ground, Zone) && Survey.Stores.Count == 1
				&& ReferenceEquals(Survey.Stores[0].ParentObject, Store)
				&& Survey.StoredWater == StoredDrams, "the founded scene must expose one store");
			SeedIncident();
			r_TAF_RaidMintProbe.Book = System.LifecycleBook;
		}

		/// <summary>Warning -&gt; delivery -&gt; acknowledgement through the real compiled ledger
		/// rules, then the documented test-only FightCommitted stamp (precedent:
		/// DevTests/KingdomRaidLaunchOrderTests.cs:128-167). Nothing reaches the lifecycle book,
		/// so the raid lane stays free for the actual LaunchRaid.</summary>
		private void SeedIncident()
		{
			KingdomLifecycleBook book = System.LifecycleBook;
			KingdomRaidProfile profile = null;
			long now = Game.TimeTicks;
			Require(now >= 8L && KingdomRaidProfiles.TryGet(AttackerFaction, out profile)
				&& profile != null, "no started world clock or shipped Snapjaws raid profile");
			KingdomLifecycleOperation warning = LedgerOp(KingdomLifecycleAction.RaidWarning, now - 4L);
			warning.Origin = KingdomLifecycleRules.ChildId(book.SettlementId,
				"native-raid-launch-provocation", 0);
			warning.ObjectId = KingdomRaidIncidentRules.GrievanceId(warning.Origin);
			warning.ObjectMarker = KingdomRaidIncidentRules.IncidentId(warning.ObjectId);
			warning.ObjectName = "authored act";
			warning.DisplayFaction = profile.Reach;
			warning.Creed = "debug-test-provocation";
			warning.Detail = "a native raid-launch fixture act was explicitly recorded";
			warning.ArrivalText = Zone.ZoneID;
			warning.Target = 1;
			warning.Count = PartySize;
			warning.DepartTick = now - 3L;
			warning.PlunderRequested = KingdomRules.RaidTributeDrams;
			warning.Kind = KingdomRules.RaidPlunderDrams;
			long seed = KingdomRaidIncidentRules.SeedFor(warning.ObjectMarker);
			warning.Blueprint = KingdomRaidProfiles.FreezePlan(profile, FrozenStage, seed, PartySize);
			Require(!string.IsNullOrEmpty(warning.Blueprint),
				"the pinned stage could not freeze an exact Snapjaws roster plan");
			book.RaidLedger = Apply(book.RaidLedger, warning);
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(book.RaidLedger);
			Require(incident != null && incident.Seed == seed, "the fixture warning opened no incident");
			KingdomLifecycleOperation deliver = LedgerOp(
				KingdomLifecycleAction.RaidDeliverDemand, now - 3L);
			deliver.Id = KingdomLifecycleRules.ChildId(incident.Id, "native-raid-launch-deliver", 0);
			deliver.ObjectId = incident.Id;
			deliver.Origin = incident.DemandChannelId;
			deliver.Target = incident.ChannelRevision + 1;
			deliver.ObjectMarker = KingdomRaidIncidentRules.DemandObjectId(
				incident.DemandChannelId, deliver.Target);
			deliver.Count = 1;
			deliver.Blueprint = profile.ChannelBlueprint;
			book.RaidLedger = Apply(book.RaidLedger, deliver);
			incident = KingdomRaidIncidentRules.Active(book.RaidLedger);
			KingdomLifecycleOperation acknowledge = LedgerOp(
				KingdomLifecycleAction.RaidAcknowledgeDemand, now - 2L);
			acknowledge.Id = KingdomLifecycleRules.ChildId(incident.Id, "native-raid-launch-ack", 0);
			acknowledge.ObjectId = incident.Id;
			acknowledge.Origin = incident.DemandObjectId;
			acknowledge.DepartTick = now - 1L;
			book.RaidLedger = Apply(book.RaidLedger, acknowledge);
			incident = KingdomRaidIncidentRules.Active(book.RaidLedger);
			incident.State = KingdomRaidIncidentState.FightCommitted;
			incident.Response = KingdomRaidResponse.Fight;
			Require(KingdomRaidIncidentRules.ValidLedger(book.RaidLedger),
				"the seeded committed incident is not a valid ledger");
			Require(incident.DueTick > 0L && now >= incident.DueTick
				&& incident.PlannedPartySize == PartySize
				&& string.Equals(incident.TargetZoneId, Zone.ZoneID, StringComparison.Ordinal),
				"the committed incident is not due at this seat now");
			Incident = incident;
			FreezeRoster(profile);
		}

		/// <summary>Proves pre-activation that the frozen plan resolves back to the pinned stage and
		/// that EVERY roster slot the launcher could request is a probed blueprint.</summary>
		private void FreezeRoster(KingdomRaidProfile Profile)
		{
			KingdomRaidProfile resolved;
			GrowthStage stage;
			Require(KingdomRaidProfiles.TryResolveFrozen(Incident.AttackerFactionId,
				Incident.ForceProfileId, Incident.Seed, Incident.PlannedPartySize,
				out resolved, out stage) && stage == FrozenStage,
				"the frozen plan does not resolve back to the pinned roster stage");
			for (int i = 0; i < KingdomRaidIncidentRules.MaxParty; i++)
			{
				string blueprint = KingdomRaidProfiles.Blueprint(Profile, FrozenStage, Incident.Seed, i);
				Require(Probed(blueprint), "roster slot " + i
					+ " is not a probed blueprint: " + (blueprint ?? "(null)"));
			}
		}

		private KingdomLifecycleOperation LedgerOp(KingdomLifecycleAction Action, long Tick)
		{
			return new KingdomLifecycleOperation
			{
				Lane = KingdomLifecycleLane.Raid, Action = Action,
				SettlementId = System.LifecycleBook.SettlementId, ZoneId = Zone.ZoneID,
				Faction = AttackerFaction, CreatedTick = Tick
			};
		}

		private static KingdomRaidLedger Apply(KingdomRaidLedger Before,
			KingdomLifecycleOperation Operation)
		{
			KingdomRaidLedger after;
			Require(KingdomRaidIncidentRules.TryApply(Before, Operation, out after) && after != null,
				"the fixture ledger refused " + Operation.Action);
			return after;
		}

		/// <summary>Captures the allocation, then asserts AFTER the factory returns: the subsidence
		/// fixture asserts inside this callback (KingdomSubsidenceNativeFixture.cs:141-148), this
		/// one does not, because a throw in the factory's callback region is swallowed there
		/// (design rev2 sec 4, decompiled GameObjectFactory.cs:1163-1169).</summary>
		private GameObject Create(string Blueprint)
		{
			GameObject captured = null;
			int roots = 0;
			GameObject created = GameObject.Create(Blueprint, BeforeObjectCreated: body =>
			{
				roots++;
				captured = body;
				if (body != null) body.SetIntProperty("NoLoot", 1);
			});
			Require(roots == 1 && ReferenceEquals(created, captured) && GameObject.Validate(created)
				&& created.Blueprint == Blueprint && created.Count == 1
				&& created.CurrentCell == null && created.InInventory == null
				&& created.Equipped == null && created.GetIntProperty("NoLoot") == 1,
				"factory returned foreign or occupied custody");
			RequireWorld();
			return created;
		}

		private Cell FirstEmptyCell()
		{
			for (int y = 1; y < Zone.Height - 1; y++)
				for (int x = 1; x < Zone.Width - 1; x++)
					if (EmptyCell(Zone.GetCell(x, y))) return Zone.GetCell(x, y);
			Require(false, "no clear interior fixture cell is available");
			return null;
		}

		private static bool EmptyCell(Cell Cell)
		{
			if (Cell == null || !Cell.IsPassable() || !Cell.IsEmpty() || Cell.HasOpenLiquidVolume())
				return false;
			foreach (GameObject body in Cell.GetObjects())
				if (!GameObject.Validate(body) || body.IsCreature
					|| KingdomPlots.ReadObject(body) != KingdomPlotRules.GroundKind.Bare) return false;
			return true;
		}

		private void RequireWorld()
		{
			Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)
				&& ReferenceEquals(The.Player?.CurrentZone, Zone)
				&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && System != null
				&& System.Founded && System.OwnedZone(Zone.ZoneID)
				&& !string.IsNullOrEmpty(System.CurrentRealmId)
				&& !string.IsNullOrEmpty(System.CurrentSettlementId)
				&& Factions.GetIfExists(System.KingdomFactionName) != null
				&& KingdomMaster.NewWorkAllowed(System), "founded native fixture authority changed");
		}

		private static void Require(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure ?? "native fixture refused");
		}
	}
}
