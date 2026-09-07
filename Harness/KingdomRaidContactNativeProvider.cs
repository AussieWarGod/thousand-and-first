using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomRaidContactNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "raid-contact-native-check";
		internal const string Receipt = "r_TAF_ScenarioRaidContactNative_v1";
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			if (verb != Verb || !string.IsNullOrEmpty(argument)) return Verb + " takes no arguments";
			XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
			try
			{
				if (!Eligible(game, zone, out string failure)) return "native raid contact refused: " + failure;
				game.SetStringGameState(Receipt, "intent");
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "contact intent did not persist exactly");
				string report = KingdomRaidContactNativeChecks.Run(game, zone, out Ok);
				Require(ReferenceEquals(The.Game, game)
					&& KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "contact intent or game changed");
				game.SetStringGameState(Receipt, report);
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, report), "contact report did not persist exactly");
				return report;
			}
			catch (Exception error)
			{
				Ok = false;
				return "native raid contact refused; evidence retained: "
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
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptA)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptB1)
				|| KingdomRaidLaunchNativeFixture.LastAttempt != null || !r_TAF_RaidMintProbe.Vacant
				|| r_TAF_RaidMintProbe.Armed || r_TAF_RaidMintProbe.Book != null) return false;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out failure)) return false;
			if (plan.Key != "founding-first-city" || plan.AuthorityClass != KingdomScenarioFoundingStep.FoundingAuthority
				|| !KingdomScenarioScript.TryRead(out var script, out failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{ failure = "requires exact sealed contact script and stamped founding plan"; return false; }
			failure = "requires unspent transaction and exact marsh zone";
			if (KingdomScenarioTransactionMarker.Observe(out _) != KingdomScenarioTransactionShape.None
				|| !KingdomQuickstartRules.TryProfile("marsh", out var profile) || zone.ZoneID != profile.ZoneId) return false;
			failure = null; return true;
		}
		internal static void Require(bool condition, string failure)
		{
			if (!condition) throw new InvalidOperationException(failure ?? "native contact evidence refused");
		}
	}

	// Captures physical identity/parts/custody; not an arbitrary-world-state snapshot.
	internal sealed class KingdomRaidContactBody
	{
		internal readonly GameObject Body;
		internal readonly Cell OriginalCell;
		internal readonly LiquidVolume Liquid;
		internal readonly r_KingdomRaiderObjective Objective;
		private readonly Physics Physics;
		private readonly IPart[] Parts;
		private readonly Inventory Inventory;
		private readonly List<GameObject> InventoryRows;
		private readonly GameObject[] Contents;
		private readonly string Id, Blueprint, OperationId, IncidentId, TargetId;
		private readonly int BaseId, TargetX, TargetY, MaxVolume, Volume;
		private readonly Dictionary<string, string> Properties, PropertyValues;
		private readonly Dictionary<string, int> IntProperties, IntValues;
		internal KingdomRaidContactBody(GameObject body)
		{
			Require(GameObject.Validate(body) && body.PartsList != null && body.PartsList.Count <= 512,
				"captured contact body is invalid or oversized");
			Body = body; Physics = body.Physics; OriginalCell = Physics?._CurrentCell;
			Id = body.IDIfAssigned; BaseId = body._BaseID; Blueprint = body.Blueprint;
			Liquid = body.GetPart<LiquidVolume>(); MaxVolume = Liquid?.MaxVolume ?? 0; Volume = Liquid?.Volume ?? 0;
			Objective = body.GetPart<r_KingdomRaiderObjective>();
			OperationId = Objective?.OperationId; IncidentId = Objective?.IncidentId; TargetId = Objective?.TargetObjectId;
			TargetX = Objective?.TargetX ?? 0; TargetY = Objective?.TargetY ?? 0;
			Properties = body.Property; IntProperties = body.IntProperty;
			PropertyValues = Properties == null ? null : new Dictionary<string, string>(Properties);
			IntValues = IntProperties == null ? null : new Dictionary<string, int>(IntProperties);
			Inventory = body.Inventory; InventoryRows = Inventory?.Objects; Contents = InventoryRows?.ToArray();
			Require(Contents == null || Contents.Length <= 512, "contact body inventory exceeds bound");
			Parts = new IPart[body.PartsList.Count];
			for (int i = 0; i < Parts.Length; i++)
			{
				Parts[i] = body.PartsList[i];
				for (int j = 0; j < i; j++) Require(!ReferenceEquals(Parts[i], Parts[j]), "contact body repeats a part");
			}
			Exact(OriginalCell);
		}
		internal void Exact(Cell cell, int? drams = null)
		{
			Require(GameObject.Validate(Body) && Body._BaseID == BaseId && Body.IDIfAssigned == Id && Body.Blueprint == Blueprint
				&& ReferenceEquals(Body.Physics, Physics) && ReferenceEquals(Physics?._CurrentCell, cell)
				&& Physics?._InInventory == null && Physics?._Equipped == null && Body.Implantee == null
				&& Body.PartsList.Count == Parts.Length, "contact body identity or exclusive custody changed");
			for (int i = 0; i < Parts.Length; i++)
				Require(ReferenceEquals(Parts[i], Body.PartsList[i]) && ReferenceEquals(Parts[i]?.ParentObject, Body),
					"contact body part or parent changed");
			Require(ReferenceEquals(Body.GetPart<LiquidVolume>(), Liquid) && (Liquid == null
				|| (Liquid.MaxVolume == MaxVolume && Liquid.Volume == (drams ?? Volume))), "contact liquid changed unexpectedly");
			Require(ReferenceEquals(Body.GetPart<r_KingdomRaiderObjective>(), Objective) && (Objective == null
				|| (Objective.OperationId == OperationId && Objective.IncidentId == IncidentId
					&& Objective.TargetObjectId == TargetId && Objective.TargetX == TargetX && Objective.TargetY == TargetY)),
				"contact objective was retargeted or replaced");
			Require(ReferenceEquals(Body.Inventory, Inventory) && ReferenceEquals(Inventory?.Objects, InventoryRows)
				&& (Inventory == null || ReferenceEquals(Inventory.ParentObject, Body)), "contact inventory owner changed");
			if (Contents != null)
			{
				Require(InventoryRows.Count == Contents.Length, "contact inventory row count changed");
				for (int i = 0; i < Contents.Length; i++) Require(ReferenceEquals(InventoryRows[i], Contents[i])
					&& GameObject.Validate(Contents[i]) && ReferenceEquals(Contents[i].Physics?._InInventory, Body),
					"contact inventory lost exact child custody");
			}
			Require(ReferenceEquals(Body.Property, Properties) && Same(Properties, PropertyValues)
				&& ReferenceEquals(Body.IntProperty, IntProperties) && Same(IntProperties, IntValues), "contact markers changed");
		}
		private static bool Same<T>(Dictionary<string, T> actual, Dictionary<string, T> expected)
		{
			if (actual == null || expected == null) return actual == null && expected == null;
			if (actual.Count != expected.Count) return false;
			foreach (var row in expected)
				if (!actual.TryGetValue(row.Key, out T value) || !EqualityComparer<T>.Default.Equals(value, row.Value)) return false;
			return true;
		}
		private static void Require(bool condition, string failure) { KingdomRaidContactNativeProvider.Require(condition, failure); }
	}
}
