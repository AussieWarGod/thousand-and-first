using System;
using System.Collections.Generic;
using ThousandAndFirst.Harness;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		private static void NativeQuickstartFaults(KingdomNativeRegressionContext Context)
		{
			Context.Case("adapter-water-verification-refusal", () => NativeWaterFault(Context, "verify"));
			Context.Case("adapter-throw-before-placement", () => NativeWaterFault(Context, "before"));
			Context.Case("adapter-throw-after-placement", () => NativeWaterFault(Context, "after"));
			Context.Case("adapter-child-refusal-before-transfer", () => NativeChildFault(Context, "before"));
			Context.Case("adapter-child-throw-after-transfer", () => NativeChildFault(Context, "after"));
			Context.Case("adapter-moved-child", () => NativeChildFault(Context, "moved"));
			Context.Case("adapter-moved-root", () => NativeWaterFault(Context, "moved"));
			Context.Case("adapter-foreign-return", () => NativeChildFault(Context, "foreign"));
			Context.Case("adapter-foreign-contents-fence", () => NativeForeignContents(Context));
			Context.Case("adapter-interrupted-factory-fence", () => NativeInterruptedFactory(Context));
			Context.Case("adapter-quarantine-five-tables", () => NativeQuarantineTables(Context));
		}

		private static void NativeWaterFault(KingdomNativeRegressionContext Context, string Cut)
		{
			KingdomQuickstartReceipt receipt = NativeReceipt(Context, KingdomQuickstartPhase.WaterStocked);
			GameObject water = null;
			Action attempt = () =>
			{
				bool accepted = TryCreateFreshGrant(Context.Game, scope =>
				{
					water = scope.Create(() => Context.Track(GameObject.Create("r_KingdomCaskRack")));
					water.SetIntProperty("KingdomStores", 1);
					Context.Check(TryPrepareGrant(water, receipt, KingdomQuickstartPhase.WaterStocked,
						out string failure), failure);
					LiquidVolume volume = water.GetPart<LiquidVolume>();
					Context.Check(KingdomLiquids.Fill(volume, "water", 24) == 24,
						"native water preparation must fill 24 physical drams");
					if (Cut == "before") throw new InvalidOperationException(NativeFaultMessage);
					Context.Check(TryPlaceGrant(Context.Zone, water, KingdomQuickstartRules.WaterCellX,
						KingdomQuickstartRules.WaterCellY, out failure), failure);
					if (Cut == "after") throw new InvalidOperationException(NativeFaultMessage);
					if (Cut == "moved")
					{
						water.RemoveFromContext();
						Cell destination = Context.Zone.GetCell(29, 10);
						destination.AddObject(water, NoStack: true, Silent: true);
						Context.Check(water.CurrentCell == destination, "injected relocation must actually move water");
					}
					else volume.MaxVolume = 32;
					return water;
				}, item => VerifyWaterGrant(Context.Zone, item, receipt, true, out _), out GameObject grant);
				Context.Check(!accepted && grant == null, "the production adapter must refuse the injected state");
			};
			if (Cut == "before" || Cut == "after") NativeExpectedThrow(Context, attempt);
			else attempt();
			NativeAbsent(Context, water);
			Context.Check(!GrantQuarantined(Context.Game), "proved water cleanup must release its fence");
			if (Cut == "verify")
			{
				GameObject retry = NativeTrackFresh(Context,
					CreateWater(Context.Game, Context.Zone, receipt, out string failure));
				Context.Check(!ReferenceEquals(retry, water)
					&& VerifyWaterGrant(Context.Zone, retry, receipt, true, out failure)
					&& ExactGrantMarker(retry, receipt, KingdomQuickstartPhase.WaterStocked),
					"clean rollback must permit exactly one fresh production grant on retry");
			}
		}

		private static GameObject NativePair(KingdomNativeRegressionContext Context,
			KingdomQuickstartGrantScope<GameObject> Scope, out GameObject Child)
		{
			GameObject root = Scope.Create(() => Context.Track(GameObject.Create("Chest")));
			Context.Check(GameObject.Validate(root) && root.Inventory != null && root.Inventory.Objects.Count == 0,
				"native fixture chest must be empty");
			Child = Scope.Create(() => Context.Track(GameObject.Create("Vinewafer")));
			Context.Check(KingdomOrdinaryFoodAuthority.IsEdible(Child) && Child.Count == 1,
				"native fixture meal must be one ordinary food item");
			return root;
		}

		private static void NativeInsert(KingdomNativeRegressionContext Context,
			GameObject Holder, GameObject Child)
		{
			GameObject result = Holder.Inventory.AddObject(Child, null, Silent: true, NoStack: true);
			Context.Check(ReferenceEquals(result, Child) && Child.InInventory == Holder
				&& ContainsExact(Holder.Inventory.Objects, Child), "native inventory transfer must retain exact custody");
		}

		private static void NativeChildFault(KingdomNativeRegressionContext Context, string Cut)
		{
			GameObject root = null, child = null, foreign = null;
			Cell scratch = Context.Zone.GetCell(29, 10);
			if (Cut == "foreign")
			{
				foreign = Context.Track(GameObject.Create("Vinewafer"));
				scratch.AddObject(foreign, NoStack: true, Silent: true);
			}
			Action attempt = () =>
			{
				bool accepted = TryCreateFreshGrant(Context.Game, scope =>
				{
					root = NativePair(Context, scope, out child);
					if (Cut == "before") return null;
					if (Cut == "foreign") return foreign;
					NativeInsert(Context, root, child);
					if (Cut == "after") throw new InvalidOperationException(NativeFaultMessage);
					child.RemoveFromContext();
					scratch.AddObject(child, NoStack: true, Silent: true);
					Context.Check(child.CurrentCell == scratch && child.InInventory == null,
						"injected child movement must actually leave the inventory");
					return null;
				}, item => true, out GameObject grant);
				Context.Check(!accepted && grant == null, "injected child refusal must abort the real adapter");
			};
			if (Cut == "after") NativeExpectedThrow(Context, attempt);
			else attempt();
			NativeAbsent(Context, root);
			NativeAbsent(Context, child);
			if (foreign != null)
				Context.Check(GameObject.Validate(foreign) && foreign.CurrentCell == scratch && foreign.Count == 1,
					"a returned foreign reference must remain untouched");
			Context.Check(!GrantQuarantined(Context.Game), "proved child cleanup must release its fence");
		}

		private static void NativeForeignContents(KingdomNativeRegressionContext Context)
		{
			GameObject root = null, child = null, foreign = null;
			Context.Check(!TryCreateFreshGrant(Context.Game, scope =>
			{
				root = NativePair(Context, scope, out child);
				NativeInsert(Context, root, child);
				foreign = Context.Track(GameObject.Create("Vinewafer"));
				NativeInsert(Context, root, foreign);
				return root;
			}, item => false, out _), "injected verification must refuse the mixed-custody container");
			try
			{
				NativeAbsent(Context, child);
				Context.Check(GameObject.Validate(root) && GameObject.Validate(foreign)
					&& foreign.InInventory == root && root.Inventory.Objects.Count == 1,
					"cleanup must preserve the foreign child and refuse parent destruction");
			}
			finally { NativeAssertFence(Context); }
		}

		private static void NativeInterruptedFactory(KingdomNativeRegressionContext Context)
		{
			GameObject root = null, unreturned = null;
			NativeExpectedThrow(Context, () => TryCreateFreshGrant(Context.Game, scope =>
			{
				root = scope.Create(() => Context.Track(GameObject.Create("Chest")));
				return scope.Create(() =>
				{
					unreturned = Context.Track(GameObject.Create("Vinewafer"));
					throw new InvalidOperationException(NativeFaultMessage);
				});
			}, item => true, out _));
			try
			{
				NativeAbsent(Context, root);
				Context.Check(GameObject.Validate(unreturned),
					"the interrupted factory must leave an allocation whose reference the scope never received");
			}
			finally { NativeAssertFence(Context); }
		}

		private static void NativeQuarantineTables(KingdomNativeRegressionContext Context)
		{
			NativeQuarantineTable(Context, Context.Game.StringGameState, "", "string");
			NativeQuarantineTable(Context, Context.Game.IntGameState, 0, "int");
			NativeQuarantineTable(Context, Context.Game.Int64GameState, 0L, "int64");
			NativeQuarantineTable(Context, Context.Game.BooleanGameState, false, "boolean");
			NativeQuarantineTable(Context, Context.Game.ObjectGameState, (object)null, "object");
		}

		private static void NativeQuarantineTable<T>(KingdomNativeRegressionContext Context,
			IDictionary<string, T> States, T Value, string Kind)
		{
			string key = KingdomQuickstartRules.QuarantineState;
			Context.Check(!GrantQuarantined(Context.Game) && States != null,
				"quarantine table fixture must start absent");
			States.Add(key, Value);
			try
			{
				int calls = 0;
				Context.Check(!TryCreateFreshGrant(Context.Game, scope =>
				{
					calls++;
					return scope.Create(() => Context.Track(GameObject.Create("Chest")));
				}, item => true, out _) && calls == 0, Kind + " key presence must forbid allocation");
				Context.Check(States.TryGetValue(key, out T observed)
					&& EqualityComparer<T>.Default.Equals(observed, Value),
					Kind + " quarantine value must remain exactly authored");
			}
			finally
			{
				if (States.TryGetValue(key, out T observed)
					&& EqualityComparer<T>.Default.Equals(observed, Value)) States.Remove(key);
			}
		}
	}
}
