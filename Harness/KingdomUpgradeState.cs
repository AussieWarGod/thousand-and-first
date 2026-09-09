using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomUpgradeState
	{
		internal readonly XRLGame Game;
		internal readonly KingdomSystem System;
		internal readonly KingdomInheritanceState Inheritance;
		internal readonly KingdomPolityRealmTransition Transition;
		internal readonly KingdomUpgradeSnapshot Snapshot;
		private readonly object Player, Zone, ObjectTable;
		private readonly List<object> References = new List<object>();

		internal KingdomUpgradeState(XRLGame game, string scenario)
		{
			Engine();
			Check(game != null && ReferenceEquals(The.Game, game), "upgrade game owner differs");
			Game = game; Player = game.Player?.Body; Zone = game.ZoneManager?.ActiveZone; ObjectTable = game.ObjectGameState;
			Check(Player != null && Zone != null && ReferenceEquals(The.Player, Player), "upgrade player or active zone is missing");
			System = game.GetSystem<KingdomSystem>();
			Check(System == null || !System.LoadFailed, "upgrade kingdom load is quarantined");
			Inheritance = ReadInheritance(game);
			Transition = System?.PolityTransition;
			string inheritance = KingdomUpgradeGraph.Capture(Inheritance, References);
			string transition = KingdomUpgradeGraph.Capture(Transition, References);
			string legacy = KingdomUpgradeGraph.Capture(Transition?.Legacy, References);
			Snapshot = new KingdomUpgradeSnapshot(game.GameID, scenario, KingdomUpgradeSnapshotCodec.OldPin,
				game.Turns, game.TimeTicks, game.ActionTicks, game.PlayerActionTicks, inheritance, transition, legacy);
			Check(KingdomUpgradeSnapshotCodec.TryEncode(Snapshot, out _), "upgrade observation is outside its wire bounds");
			Validate(scenario);
			Stage();
			Exact(); // Pure validators must not repair their source graph.
		}

		internal void Exact()
		{
			Check(ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.Player?.Body, Player)
				&& ReferenceEquals(Game.ZoneManager?.ActiveZone, Zone) && ReferenceEquals(Game.ObjectGameState, ObjectTable)
				&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && (System == null || !System.LoadFailed)
				&& ReferenceEquals(ReadInheritance(Game), Inheritance) && ReferenceEquals(System?.PolityTransition, Transition),
				"upgrade observed owner graph changed");
			Check(Game.GameID == Snapshot.GameId && Game.Turns == Snapshot.Turns && Game.TimeTicks == Snapshot.TimeTicks
				&& Game.ActionTicks == Snapshot.ActionTicks && Game.PlayerActionTicks == Snapshot.PlayerActionTicks,
				"upgrade observed clock changed");
			List<object> current = new List<object>();
			Check(KingdomUpgradeGraph.Capture(Inheritance, current) == Snapshot.Inheritance
				&& KingdomUpgradeGraph.Capture(Transition, current) == Snapshot.Transition
				&& KingdomUpgradeGraph.Capture(Transition?.Legacy, current) == Snapshot.Legacy
				&& current.Count == References.Count, "upgrade raw fields changed");
			for (int i = 0; i < current.Count; i++) Check(ReferenceEquals(current[i], References[i]), "upgrade retained child reference changed");
		}

		internal void Matches(KingdomUpgradeSnapshot expected)
		{
			Exact();
			Check(KingdomUpgradeSnapshotCodec.TryEncode(Snapshot, out string actual)
				&& KingdomUpgradeSnapshotCodec.TryEncode(expected, out string original) && actual == original,
				"loaded upgrade state differs from actual source-save observation");
		}

		private void Validate(string scenario)
		{
			if (Inheritance != null) ValidateInheritance(Inheritance, Game.GameID);
			if (Transition != null) Check(KingdomPolityRules.TryValidateRealmTransition(Transition, out string failure), failure);
			if (scenario == "inheritance")
			{
				Check(Inheritance != null && (Inheritance.Phase == KingdomInheritancePhase.Reserved
					|| Inheritance.Phase == KingdomInheritancePhase.SiteSelected || Inheritance.Phase == KingdomInheritancePhase.WorldValidated
					|| Inheritance.Phase == KingdomInheritancePhase.Installed || Inheritance.Phase == KingdomInheritancePhase.Committed)
					&& (Transition == null || Transition.Phase == KingdomPolityRealmTransitionPhase.None),
					"inheritance case requires genuine stable nonempty inheritance without detached transition");
			}
			else
			{
				Check(scenario == "detached-transition" && System != null && Transition != null
					&& Transition.Phase == KingdomPolityRealmTransitionPhase.Detached && Transition.Legacy != null,
					"detached-transition case has no genuine detached legacy");
			}
		}

		internal string Stage()
		{
			string origin, legacyId, lineage; int generation;
			if (Snapshot.Case == "inheritance")
			{
				Check(KingdomSealRecord.TryParse(Field<string>(Inheritance, "LegacyText"), out KingdomSealRecord legacy,
					out _, out _), "stage observation has no canonical inherited origin");
				origin = legacy.OriginGameId; legacyId = legacy.LegacyId; lineage = legacy.LineageId; generation = legacy.Generation;
			}
			else
			{
				KingdomSeal seal = Game.GetSystem<KingdomSeal>();
				Check(seal != null, "detached stage has no original seal coordinator");
				origin = Game.GameID; legacyId = seal.CurrentLegacyId; lineage = seal.CurrentLineageId; generation = seal.CurrentGeneration;
			}
			Check(KingdomSealReceipt.ValidId(origin) && KingdomSealReceipt.ValidId(legacyId)
				&& KingdomSealReceipt.ValidId(lineage), "stage observation has no exact source identity");
			string path = DataManager.SyncedPath("ThousandAndFirst");
			Check(KingdomUpgradeFiles.Same(path, Path.Combine(KingdomUpgradeFiles.Root(), "Synced", "ThousandAndFirst")),
				"actual seal store left its owned profile");
			KingdomSealRecord stage = new KingdomSealStore(path).ReadStage(origin);
			Check(stage != null && stage.OriginGameId == origin && stage.LegacyId == legacyId
				&& stage.LineageId == lineage && stage.Generation == generation
				&& (Snapshot.Case == "inheritance" ? stage.Status == KingdomSealStatus.Terminal || stage.Status == KingdomSealStatus.Retired
					: stage.Status != KingdomSealStatus.Promoted && stage.WrittenTick <= Game.TimeTicks),
				"actual source seal stage is unreadable or belongs to another generation");
			string wire = stage.Compose(); bool exact = false;
			foreach (string slot in new[] { "a", "b" })
			{
				string file = Path.Combine(path, "Stages", origin + "." + slot + ".seal");
				if (File.Exists(file) && KingdomUpgradeFiles.Read(file, 262144) == wire) exact = true;
			}
			Check(exact, "readable stage lacks its exact canonical stored bytes");
			return "; seal-stage-readable=true; stage-origin=" + origin + "; stage-sha256=" + KingdomUpgradeFiles.HashText(wire);
		}

		internal static KingdomInheritanceState ReadInheritance(XRLGame game)
		{
			const string key = "r_TAF_Inheritance";
			Check(game.ObjectGameState != null && game.StringGameState != null && game.IntGameState != null
				&& game.Int64GameState != null && game.BooleanGameState != null
				&& !game.StringGameState.ContainsKey(key) && !game.IntGameState.ContainsKey(key)
				&& !game.Int64GameState.ContainsKey(key) && !game.BooleanGameState.ContainsKey(key),
				"inheritance key is missing its tables or duplicated in a foreign table");
			if (!game.ObjectGameState.TryGetValue(key, out object value)) return null;
			Check(value != null && value.GetType() == typeof(KingdomInheritanceState), "inheritance object has wrong type or stored null");
			return (KingdomInheritanceState)value;
		}

		internal static T Field<T>(KingdomInheritanceState state, string name)
		{
			FieldInfo field = typeof(KingdomInheritanceState).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
			Check(field != null && field.FieldType == typeof(T), "inheritance field schema differs: " + name);
			return (T)field.GetValue(state);
		}

		internal static void Engine()
		{
			Check(typeof(XRLGame).Assembly.GetName().Version.ToString() == "2.0.211.51",
				"upgrade observation requires pinned engine 2.0.211.51");
		}

		internal static void ValidateInheritance(KingdomInheritanceState state, string gameId)
		{
			Check(Field<int>(state, "SerializationVersion") == 4 && !Field<bool>(state, "RecoveryDisabled")
				&& state.Phase != KingdomInheritancePhase.RepairRequired && !Field<bool>(state, "ReleasePending")
				&& !Field<bool>(state, "RetryAuthorized"), "inheritance is not a stable supported no-repair state");
			KingdomInheritanceSavedShape shape = new KingdomInheritanceSavedShape
			{
				PhaseValue = Field<int>(state, "PhaseValue"), LegacyText = Field<string>(state, "LegacyText"),
				ReceiptText = Field<string>(state, "ReceiptText"), CommittedReceiptText = Field<string>(state, "CommittedReceiptText"),
				TargetZoneId = Field<string>(state, "TargetZoneId"), TargetTerrainBlueprint = Field<string>(state, "TargetTerrainBlueprint"),
				TargetTerrainRank = Field<int>(state, "TargetTerrainRank"), SecretId = Field<string>(state, "SecretId"),
				SiteName = Field<string>(state, "SiteName"), ApplyStatus = Field<int>(state, "ApplyStatusValue"),
				ApplyFault = Field<int>(state, "ApplyFaultValue"), ApplicationMarker = Field<string>(state, "ApplicationMarker"),
				OwnsSkipTerrainBuilders = Field<bool>(state, "OwnsSkipTerrainBuilders"), OwnsNoBiomes = Field<bool>(state, "OwnsNoBiomes"),
				OwnsZoneName = Field<bool>(state, "OwnsZoneName")
			};
			Check(shape.LegacyText != null && shape.ReceiptText != null && shape.CommittedReceiptText != null,
				"inheritance retained null wire");
			Check(KingdomInheritanceStateRules.TryValidateSavedShape(shape, gameId,
				KingdomInheritEngine.ReconstructionVersionForText(shape.LegacyText), out string failure), failure);
			if (state.Phase != KingdomInheritancePhase.Empty)
				Check(KingdomSealRecord.TryParse(shape.LegacyText, out KingdomSealRecord legacy, out _, out _)
					&& legacy.Compose() == shape.LegacyText && legacy.Status == KingdomSealStatus.Promoted && legacy.IsResolved,
					"inheritance legacy is not exact canonical promoted text");
		}

		private static void Check(bool condition, string failure) { KingdomUpgradeFiles.Check(condition, failure); }
	}
}
