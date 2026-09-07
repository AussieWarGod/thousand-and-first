using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomRaidRecoveryDeathNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "raid-recovery-death-native-check";
		internal const string Receipt = "r_TAF_ScenarioRaidRecoveryDeathNative_v1";
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			if (verb != Verb || !string.IsNullOrEmpty(argument)) return Verb + " takes no arguments";
			XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
			try
			{
				if (!Eligible(game, zone, out string failure)) return "native raid recovery death refused: " + failure;
				game.SetStringGameState(Receipt, "intent");
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "recovery intent did not persist exactly");
				string report = KingdomRaidRecoveryDeathNativeChecks.Run(game, zone, out Ok);
				Require(ReferenceEquals(The.Game, game) && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"recovery intent or game changed");
				game.SetStringGameState(Receipt, report);
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, report), "recovery report did not persist exactly");
				return report;
			}
			catch (Exception error)
			{
				Ok = false;
				return "native raid recovery death refused; evidence retained: "
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
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidDeathNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidContactNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, "r_TAF_ScenarioRaidDeathVetoNative_v1")
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptA)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptB1)
				|| KingdomRaidLaunchNativeFixture.LastAttempt != null || !r_TAF_RaidMintProbe.Vacant
				|| r_TAF_RaidMintProbe.Armed || r_TAF_RaidMintProbe.Book != null) return false;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out failure)) return false;
			if (plan.Key != "founding-first-city" || plan.AuthorityClass != KingdomScenarioFoundingStep.FoundingAuthority
				|| !KingdomScenarioScript.TryRead(out var script, out failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{ failure = "requires exact sealed recovery-death script and stamped founding plan"; return false; }
			failure = "requires unspent transaction and exact marsh zone";
			if (KingdomScenarioTransactionMarker.Observe(out _) != KingdomScenarioTransactionShape.None
				|| !KingdomQuickstartRules.TryProfile("marsh", out var profile) || zone.ZoneID != profile.ZoneId) return false;
			failure = null; return true;
		}
		internal static void Require(bool condition, string failure)
		{ if (!condition) throw new InvalidOperationException(failure ?? "native recovery death evidence refused"); }
	}

	[Serializable]
	public sealed class KingdomRaidRecoveryDeathNativePart : IPart
	{
		[NonSerialized] internal bool Armed;
		public override bool WantEvent(int ID, int cascade)
		{ return base.WantEvent(ID, cascade) || ID == BeforeDestroyObjectEvent.ID; }
		public override bool HandleEvent(BeforeDestroyObjectEvent E)
		{
			if (KingdomRaidRecoveryDeathNativeChecks.BeforeDestroy(this, E.Object)) return false;
			return base.HandleEvent(E);
		}
	}

	// Never suppresses or replaces the production pre-removal callback.
	[HarmonyPatch(typeof(KingdomRaids), "RaiderDying", new Type[] { typeof(GameObject), typeof(r_KingdomRaiderObjective) })]
	internal static class KingdomRaidRecoveryDeathNativeObserver
	{
		[HarmonyPrefix]
		internal static void Prefix(GameObject actor, r_KingdomRaiderObjective part)
		{ KingdomRaidRecoveryDeathNativeChecks.Observe(true, actor, part); }
		[HarmonyPostfix]
		internal static void Postfix(GameObject actor, r_KingdomRaiderObjective part)
		{ KingdomRaidRecoveryDeathNativeChecks.Observe(false, actor, part); }
	}

	// Native death may unequip/drop inventory before Destroy is vetoed; those outputs remain retained.
	internal sealed class KingdomRaidRecoveryDeathSubject
	{
		internal readonly GameObject Body;
		internal readonly Cell Cell;
		internal readonly r_KingdomRaiderObjective Objective;
		internal readonly string Id;
		internal readonly int BaseId;
		private readonly string Blueprint, Marker, OperationId, IncidentId, TargetId;
		private readonly int X, Y;
		private readonly Physics Physics;
		private readonly PartRack Rack;
		private readonly IPart[] Parts;
		internal KingdomRaidRecoveryDeathSubject(GameObject body)
		{
			Body = body; Physics = body.Physics; Cell = Physics?._CurrentCell;
			Id = body.IDIfAssigned; BaseId = body._BaseID; Blueprint = body.Blueprint;
			Objective = body.GetPart<r_KingdomRaiderObjective>();
			Require(Objective != null && Cell != null && body.PartsList.Count <= 512, "recovery subject lacks bounded identity");
			OperationId = Objective.OperationId; IncidentId = Objective.IncidentId; TargetId = Objective.TargetObjectId;
			X = Objective.TargetX; Y = Objective.TargetY; Marker = body.GetStringProperty(KingdomRaids.ProjectionMarkerProperty);
			Rack = body.PartsList; Parts = Rack.ToArray();
			for (int i = 0; i < Parts.Length; i++) for (int j = 0; j < i; j++)
				Require(!ReferenceEquals(Parts[i], Parts[j]), "recovery subject repeats an original part");
			Exact(null);
		}
		internal void Identity()
		{
			Require(Body._BaseID == BaseId && Body.IDIfAssigned == Id && Body.Blueprint == Blueprint
				&& ReferenceEquals(Body.Physics, Physics) && ReferenceEquals(Body.GetPart<r_KingdomRaiderObjective>(), Objective)
				&& ReferenceEquals(Objective.ParentObject, Body) && Objective.OperationId == OperationId
				&& Objective.IncidentId == IncidentId && Objective.TargetObjectId == TargetId && Objective.TargetX == X && Objective.TargetY == Y
				&& Body.GetIntProperty("KingdomRaider") == 1 && Body.GetStringProperty(KingdomRaids.ProjectionMarkerProperty) == Marker,
				"recovery subject identity/marker/objective changed");
		}
		internal void Exact(IPart added)
		{
			Identity(); int found = 0;
			foreach (GameObject row in Cell.Objects) if (ReferenceEquals(row, Body)) found++;
			Require(GameObject.Validate(Body) && !Body.IsInGraveyard() && ReferenceEquals(Physics._CurrentCell, Cell)
				&& Physics._InInventory == null && Physics._Equipped == null && Body.Implantee == null && found == 1,
				"recovery subject lost exclusive live custody");
			Require(ReferenceEquals(Body.PartsList, Rack) && Rack.Count == Parts.Length + (added == null ? 0 : 1), "recovery part rack changed");
			int original = 0, additions = 0;
			foreach (IPart part in Rack)
			{
				Require(part != null && ReferenceEquals(part.ParentObject, Body), "recovery part parent changed");
				if (ReferenceEquals(part, added)) { additions++; continue; }
				Require(original < Parts.Length && ReferenceEquals(part, Parts[original++]), "recovery original part changed");
			}
			Require(original == Parts.Length && additions == (added == null ? 0 : 1), "recovery veto attachment was not exact");
		}
		private static void Require(bool value, string failure) { KingdomRaidRecoveryDeathNativeProvider.Require(value, failure); }
	}

	// Reads raw quest fields; never invokes lazy Quest.System or changes completion/reward flags.
	internal sealed class KingdomRaidRecoveryQuestEvidence
	{
		internal readonly Quest Quest;
		private readonly XRLGame Game;
		private readonly KingdomSystem System;
		private readonly string Id, StepId, IncidentId;
		private readonly object ActiveMap, FinishedMap;
		private readonly Dictionary<string, QuestStep> Steps;
		private readonly Dictionary<string, object> Properties;
		private readonly Dictionary<string, int> IntProperties;
		private readonly QuestStep Step;
		private readonly object[] Fields;
		internal KingdomRaidRecoveryQuestEvidence(XRLGame game, KingdomSystem system, KingdomRaidIncident incident)
		{
			Game = game; System = system; Id = incident.RecoveryQuestId; StepId = incident.RecoveryStepId; IncidentId = incident.Id;
			Require(Id == KingdomRaidIncidentRules.RecoveryQuestId(IncidentId)
				&& StepId == KingdomRaidIncidentRules.RecoveryStepId(IncidentId), "noncanonical recovery quest identifiers");
			Require(game.Quests.TryGetValue(Id, out Quest quest) && quest != null && quest.StepsByID != null
				&& quest.StepsByID.TryGetValue(StepId, out _), "actual accept did not project its quest and step");
			Quest = quest; Steps = quest.StepsByID; Step = Steps[StepId]; Properties = quest.Properties; IntProperties = quest.IntProperties;
			ActiveMap = game.Quests; FinishedMap = game.FinishedQuests; Fields = Raw(); Exact(false);
		}
		internal void Exact(bool completed)
		{
			Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.Quests, ActiveMap) && ReferenceEquals(Game.FinishedQuests, FinishedMap)
				&& Game.Quests.TryGetValue(Id, out var actual) && ReferenceEquals(actual, Quest)
				&& ReferenceEquals(Quest.StepsByID, Steps) && Steps.Count == 1 && Steps.TryGetValue(StepId, out var step)
				&& ReferenceEquals(step, Step) && ReferenceEquals(Quest.Properties, Properties) && Properties.Count == 2
				&& Properties.TryGetValue("TAFKind", out var kind) && kind is string && (string)kind == "TAF:raid-recovery"
				&& Properties.TryGetValue("IncidentId", out var incident) && incident is string && (string)incident == IncidentId
				&& ReferenceEquals(Quest.IntProperties, IntProperties) && IntProperties.Count == 0, "recovery quest identity/rows changed");
			object[] now = Raw(); for (int i = 0; i < Fields.Length; i++) Require(Equals(Fields[i], now[i]), "recovery quest field changed: " + i);
			Require(Quest.Finished == completed && Step.Flags == (completed ? 42 : 8), "recovery quest completion flags differ");
			if (completed) Require(Game.FinishedQuests.TryGetValue(Id, out var finished) && ReferenceEquals(finished, Quest), "finished quest is not original");
			else
			{
				Require(!Game.FinishedQuests.ContainsKey(Id), "active recovery was already finished");
				Require(KingdomRaids.TryInspectRecoveryQuests(System, out var keys, out string failure)
					&& keys.Count == 1 && keys[0] == Id, "production recovery quest shape refused: " + failure);
			}
		}
		internal object[] Snapshot()
		{
			var fields = new List<object>(Raw());
			fields.Add(Quest.Finished); fields.Add(Step.Flags);
			fields.Add(Game.Quests.TryGetValue(Id, out var active) ? active : null);
			fields.Add(Game.FinishedQuests.TryGetValue(Id, out var finished) ? finished : null);
			return fields.ToArray();
		}
		private object[] Raw()
		{
			return new object[] { Quest.ID, Quest.Name, Quest.SystemType, Quest.Accomplishment, Quest.Achievement, Quest.BonusAtLevel,
				Quest.Level, Quest.Factions, Quest.Reputation, Quest.QuestGiverName, Quest.QuestGiverLocationName,
				Quest.QuestGiverLocationZoneID, Quest.Hagiograph, Quest.HagiographCategory, Quest.Gospel, Quest._Manager, Quest._dynamicReward,
				Step.ID, Step.Name, Step.Text, Step.Value, Step.XP, Step.Ordinal };
		}
		private static void Require(bool value, string failure) { KingdomRaidRecoveryDeathNativeProvider.Require(value, failure); }
	}
}
