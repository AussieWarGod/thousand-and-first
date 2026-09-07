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
	public sealed class KingdomRaidDeathVetoNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "raid-death-veto-native-check";
		internal const string Receipt = "r_TAF_ScenarioRaidDeathVetoNative_v1";
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			if (verb != Verb || !string.IsNullOrEmpty(argument)) return Verb + " takes no arguments";
			XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
			try
			{
				if (!Eligible(game, zone, out string failure)) return "native raid death veto refused: " + failure;
				game.SetStringGameState(Receipt, "intent");
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "veto intent did not persist exactly");
				string report = KingdomRaidDeathVetoNativeChecks.Run(game, zone, out Ok);
				Require(ReferenceEquals(The.Game, game) && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"veto intent or game changed");
				game.SetStringGameState(Receipt, report);
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, report), "veto report did not persist exactly");
				return report;
			}
			catch (Exception error)
			{
				Ok = false;
				return "native raid death veto refused; evidence retained: "
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
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptA)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptB1)
				|| KingdomRaidLaunchNativeFixture.LastAttempt != null || !r_TAF_RaidMintProbe.Vacant
				|| r_TAF_RaidMintProbe.Armed || r_TAF_RaidMintProbe.Book != null) return false;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out failure)) return false;
			if (plan.Key != "founding-first-city" || plan.AuthorityClass != KingdomScenarioFoundingStep.FoundingAuthority
				|| !KingdomScenarioScript.TryRead(out var script, out failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{ failure = "requires exact sealed veto script and stamped founding plan"; return false; }
			failure = "requires unspent transaction and exact marsh zone";
			if (KingdomScenarioTransactionMarker.Observe(out _) != KingdomScenarioTransactionShape.None
				|| !KingdomQuickstartRules.TryProfile("marsh", out var profile) || zone.ZoneID != profile.ZoneId) return false;
			failure = null; return true;
		}
		internal static void Require(bool condition, string failure)
		{ if (!condition) throw new InvalidOperationException(failure ?? "native veto evidence refused"); }
	}

	[Serializable]
	public sealed class KingdomRaidDeathVetoNativePart : IPart
	{
		[NonSerialized] internal bool Armed;
		public override bool WantEvent(int ID, int cascade)
		{ return base.WantEvent(ID, cascade) || ID == BeforeDestroyObjectEvent.ID; }
		public override bool HandleEvent(BeforeDestroyObjectEvent E)
		{
			if (KingdomRaidDeathVetoNativeChecks.BeforeDestroy(this, E.Object)) return false;
			return base.HandleEvent(E);
		}
	}

	// These observers never suppress RaiderDying or alter its arguments/result.
	[HarmonyPatch(typeof(KingdomRaids), "RaiderDying", new Type[] { typeof(GameObject), typeof(r_KingdomRaiderObjective) })]
	internal static class KingdomRaidDeathVetoNativeObserver
	{
		[HarmonyPrefix]
		internal static void Prefix(GameObject actor, r_KingdomRaiderObjective part)
		{ KingdomRaidDeathVetoNativeChecks.Observe(true, actor, part); }
		[HarmonyPostfix]
		internal static void Postfix(GameObject actor, r_KingdomRaiderObjective part)
		{ KingdomRaidDeathVetoNativeChecks.Observe(false, actor, part); }
	}

	// Inventory/unequips/drops are deliberately not frozen: native death can change them before veto.
	internal sealed class KingdomRaidDeathVetoSubject
	{
		internal readonly GameObject Body;
		internal readonly Cell Cell;
		internal readonly r_KingdomRaiderObjective Objective;
		internal readonly string Id, Blueprint;
		internal readonly int BaseId;
		private readonly Physics Physics;
		private readonly PartRack Rack;
		private readonly IPart[] Parts;
		private readonly string Marker, OperationId, IncidentId, TargetId;
		private readonly int X, Y;
		internal KingdomRaidDeathVetoSubject(GameObject body)
		{
			Body = body; Physics = body.Physics; Cell = Physics?._CurrentCell;
			Id = body.IDIfAssigned; BaseId = body._BaseID; Blueprint = body.Blueprint;
			Objective = body.GetPart<r_KingdomRaiderObjective>();
			Require(Objective != null && Cell != null && body.PartsList.Count <= 512, "veto subject lacks bounded physical identity");
			OperationId = Objective.OperationId; IncidentId = Objective.IncidentId; TargetId = Objective.TargetObjectId;
			X = Objective.TargetX; Y = Objective.TargetY; Marker = body.GetStringProperty(KingdomRaids.ProjectionMarkerProperty);
			Rack = body.PartsList; Parts = Rack.ToArray();
			for (int i = 0; i < Parts.Length; i++) for (int j = 0; j < i; j++)
				Require(!ReferenceEquals(Parts[i], Parts[j]), "veto baseline repeats an original part");
			Exact(null);
		}
		internal void Identity()
		{
			Require(Body._BaseID == BaseId && Body.IDIfAssigned == Id && Body.Blueprint == Blueprint
				&& ReferenceEquals(Body.Physics, Physics) && ReferenceEquals(Body.GetPart<r_KingdomRaiderObjective>(), Objective)
				&& ReferenceEquals(Objective.ParentObject, Body) && Objective.OperationId == OperationId
				&& Objective.IncidentId == IncidentId && Objective.TargetObjectId == TargetId && Objective.TargetX == X && Objective.TargetY == Y
				&& Body.GetIntProperty("KingdomRaider") == 1 && Body.GetStringProperty(KingdomRaids.ProjectionMarkerProperty) == Marker,
				"veto subject identity, marker or objective changed");
		}
		internal void Exact(KingdomRaidDeathVetoNativePart added)
		{
			Identity(); int found = 0;
			foreach (GameObject row in Cell.Objects) if (ReferenceEquals(row, Body)) found++;
			Require(GameObject.Validate(Body) && !Body.IsInGraveyard() && ReferenceEquals(Physics._CurrentCell, Cell)
				&& Physics._InInventory == null && Physics._Equipped == null && Body.Implantee == null && found == 1,
				"veto subject lost original exclusive live custody");
			Require(ReferenceEquals(Body.PartsList, Rack) && Rack.Count == Parts.Length + (added == null ? 0 : 1), "veto subject part rack changed");
			int original = 0, additions = 0;
			foreach (IPart part in Rack)
			{
				Require(part != null && ReferenceEquals(part.ParentObject, Body), "veto subject part parent changed");
				if (ReferenceEquals(part, added)) { additions++; continue; }
				Require(original < Parts.Length && ReferenceEquals(part, Parts[original++]), "veto subject original part changed");
			}
			Require(original == Parts.Length && additions == (added == null ? 0 : 1), "veto attachment was not exact");
		}
		private static void Require(bool condition, string failure) { KingdomRaidDeathVetoNativeProvider.Require(condition, failure); }
	}
}
