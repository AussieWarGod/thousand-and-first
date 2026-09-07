using System;
using System.Collections.Generic;
using ThousandAndFirst.Harness;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		private static void NativeLarderChildAlias(KingdomNativeRegressionContext Context,
			GameObject Larder, KingdomQuickstartReceipt Receipt)
		{
			var snapshot = new NativeStockRows(Context, Larder);
			Context.Check(snapshot.Rows.Length == 12, "alias fixture requires twelve real creator meals");
			GameObject[] injected = (GameObject[])snapshot.Rows.Clone();
			injected[11] = injected[0];
			bool accepted;
			string failure;
			snapshot.List[11] = injected[11];
			try { accepted = VerifyLarderGrant(Context.Zone, Larder, Receipt, true, out failure); }
			finally
			{
				Context.Check(snapshot.Exact(injected), "larder verifier changed captured alias evidence");
				snapshot.List[11] = snapshot.Rows[11];
				Context.Check(snapshot.Exact(snapshot.Rows), "larder alias restoration was not exact");
			}
			Context.Check(VerifyLarderGrant(Context.Zone, Larder, Receipt, true, out string restored), restored);
			Context.Check(!accepted && !string.IsNullOrEmpty(failure),
				"one meal reference in two rows must not prove twelve physical meals");
		}

		private static void NativeMaterialChildAlias(KingdomNativeRegressionContext Context,
			GameObject Stockpile, KingdomQuickstartReceipt Receipt)
		{
			var snapshot = new NativeStockRows(Context, Stockpile);
			GameObject timber = null;
			foreach (GameObject item in snapshot.Rows)
				if (KingdomMaterials.TryOrdinaryMaterialOf(item, out var kind) && kind == KingdomMaterial.Timber)
				{
					Context.Check(timber == null, "material fixture requires one original timber stack");
					timber = item;
				}
			Context.Check(snapshot.Rows.Length == 3 && timber?.Stacker != null
				&& timber.Stacker._StackCount == 4, "alias fixture requires real creator timber count four");
			Stacker stacker = timber.Stacker;
			var injected = new List<GameObject>(snapshot.Rows) { timber };
			bool accepted;
			string failure;
			// Only fixture-owned raw rows/count change; no stack events or physical transfers.
			Context.Check(snapshot.Exact(snapshot.Rows),
				"material classification changed captured fixture authority before injection");
			stacker._StackCount = 2;
			snapshot.List.Add(timber);
			try { accepted = VerifyMaterialsGrant(Context.Zone, Stockpile, Receipt, true, out failure); }
			finally
			{
				Context.Check(snapshot.Exact(injected, timber, 2),
					"material verifier changed captured alias evidence");
				snapshot.List.RemoveAt(snapshot.Rows.Length);
				stacker._StackCount = 4;
				Context.Check(snapshot.Exact(snapshot.Rows), "material alias restoration was not exact");
			}
			Context.Check(VerifyMaterialsGrant(Context.Zone, Stockpile, Receipt, true, out string restored), restored);
			Context.Check(!accepted && !string.IsNullOrEmpty(failure),
				"one timber body with count two in two rows must not prove four physical timber");
		}

		private sealed class NativeStockRows
		{
			internal readonly List<GameObject> List;
			internal readonly GameObject[] Rows;
			private readonly KingdomNativeRegressionContext Context;
			private readonly GameObject Root;
			private readonly Inventory Inventory;
			private readonly NativeStockBody[] Bodies;
			private readonly Cell Cell;
			private readonly GameObject[] CellRows;
			private readonly string GameId;

			internal NativeStockRows(KingdomNativeRegressionContext Context, GameObject Root)
			{
				this.Context = Context; this.Root = Root; Inventory = Root.Inventory;
				List = Inventory.Objects; Rows = List.ToArray(); GameId = Context.Game.GameID;
				Cell = Root.Physics?._CurrentCell;
				Context.Check(Cell != null, "stock fixture requires original placed creator output");
				CellRows = Cell.Objects.ToArray();
				Bodies = new NativeStockBody[Rows.Length + 1];
				Bodies[0] = new NativeStockBody(Root);
				for (int i = 0; i < Rows.Length; i++) Bodies[i + 1] = new NativeStockBody(Rows[i]);
				Context.Check(Exact(Rows), "stock fixture initial captured ownership differs");
			}

			internal bool Exact(IList<GameObject> Expected, GameObject Adjusted = null, int Count = 0)
			{
				if (!ReferenceEquals(The.Game, Context.Game) || Context.Game.GameID != GameId
					|| !ReferenceEquals(The.ZoneManager?.ActiveZone, Context.Zone)
					|| !ReferenceEquals(Cell.ParentZone, Context.Zone)
					|| !ReferenceEquals(Root.Inventory, Inventory) || !ReferenceEquals(Inventory.ParentObject, Root)
					|| !ReferenceEquals(Inventory.Objects, List) || !SameRows(List, Expected)
					|| !SameRows(Cell.Objects, CellRows)) return false;
				foreach (NativeStockBody body in Bodies)
					if (!body.Exact(Adjusted, Count)) return false;
				return true;
			}

			private static bool SameRows(IList<GameObject> Actual, IList<GameObject> Expected)
			{
				if (Actual.Count != Expected.Count) return false;
				for (int i = 0; i < Actual.Count; i++)
					if (!ReferenceEquals(Actual[i], Expected[i])) return false;
				return true;
			}
		}

		private sealed class NativeStockBody
		{
			private readonly GameObject Body, Carrier;
			private readonly Physics Physics;
			private readonly Stacker Stacker;
			private readonly Cell Cell;
			private readonly int BaseId, Count;
			private readonly string Blueprint;
			private readonly Dictionary<string, string> Properties, PropertyValues;
			private readonly Dictionary<string, int> IntProperties, IntValues;

			internal NativeStockBody(GameObject Body)
			{
				this.Body = Body; Physics = Body.Physics; Stacker = Body.Stacker;
				Cell = Physics?._CurrentCell; Carrier = Physics?._InInventory;
				BaseId = Body._BaseID; Blueprint = Body.Blueprint; Count = Stacker?._StackCount ?? 1;
				Properties = Body.Property; IntProperties = Body.IntProperty;
				PropertyValues = Properties == null ? null : new Dictionary<string, string>(Properties);
				IntValues = IntProperties == null ? null : new Dictionary<string, int>(IntProperties);
			}

			internal bool Exact(GameObject Adjusted, int ExpectedCount)
			{
				return GameObject.Validate(Body) && Body._BaseID == BaseId && Body.Blueprint == Blueprint
					&& ReferenceEquals(Body.Physics, Physics) && ReferenceEquals(Body.Stacker, Stacker)
					&& (Physics == null || ReferenceEquals(Physics.ParentObject, Body))
					&& ReferenceEquals(Physics?._CurrentCell, Cell) && ReferenceEquals(Physics?._InInventory, Carrier)
					&& (Stacker == null || ReferenceEquals(Stacker.ParentObject, Body))
					&& (Stacker?._StackCount ?? 1) == (ReferenceEquals(Body, Adjusted) ? ExpectedCount : Count)
					&& ReferenceEquals(Body.Property, Properties) && SameDictionary(Properties, PropertyValues)
					&& ReferenceEquals(Body.IntProperty, IntProperties) && SameDictionary(IntProperties, IntValues);
			}

			private static bool SameDictionary<T>(Dictionary<string, T> Actual, Dictionary<string, T> Expected)
			{
				if (Actual == null || Expected == null) return Actual == null && Expected == null;
				if (Actual.Count != Expected.Count) return false;
				foreach (var item in Expected)
					if (!Actual.TryGetValue(item.Key, out T value)
						|| !EqualityComparer<T>.Default.Equals(value, item.Value)) return false;
				return true;
			}
		}
	}
}
