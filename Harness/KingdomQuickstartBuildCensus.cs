using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Read-only exact-reference proof for the quickstart-build native phase. Every
	/// check compares actual GameObject/part CLR references, never IDIfAssigned alone (an empty
	/// id is legal on an old-save starter child and must never itself defeat identity proof, but
	/// two rows both reporting an empty id must never be read as the same object either).
	/// IDIfAssigned is read for reporting only, never minted. Nothing here binds a survey, mints
	/// stock, or reads an eventful Count.</summary>
	internal static class KingdomQuickstartBuildCensus
	{
		internal sealed class Row
		{
			internal readonly GameObject Object;
			internal readonly string Id, Blueprint;
			internal readonly int Count;
			internal readonly KingdomMaterial? Material;
			internal Row(GameObject o, KingdomMaterial? material)
			{
				Object = o; Id = o.IDIfAssigned; Blueprint = o.Blueprint;
				Count = o.Stacker?._StackCount ?? 1; Material = material;
			}
		}

		internal sealed class StockSnapshot
		{
			internal readonly GameObject Stockpile;
			internal readonly List<GameObject> InventoryList;
			internal readonly Row[] Rows;
			internal StockSnapshot(GameObject stockpile, List<GameObject> list, Row[] rows)
			{ Stockpile = stockpile; InventoryList = list; Rows = rows; }
		}

		internal sealed class WaterSnapshot
		{
			internal readonly GameObject Cask;
			internal readonly LiquidVolume Volume;
			internal readonly Cell GroundCell;
			internal readonly int Drams;
			internal readonly bool Fresh;
			internal WaterSnapshot(GameObject cask, LiquidVolume volume, Cell cell, int drams, bool fresh)
			{ Cask = cask; Volume = volume; GroundCell = cell; Drams = drams; Fresh = fresh; }
		}

		/// <summary>Re-proves the stockpile by its OWN placed ground cell, its own reference, and
		/// exactly one receipt-ID match -- KingdomConstruction.FindExactId's bounded loaded-tree
		/// walk, which itself rejects duplicate refs/IDs with no BindPass, never a bare
		/// first-match Zone.FindObjectByID trust.</summary>
		internal static bool TryStockpile(Zone Zone, string StockpileObjectId, out GameObject Stockpile, out string Failure)
		{
			Stockpile = null;
			Failure = "starter materials stockpile could not be found by its exact granted id";
			if (string.IsNullOrEmpty(StockpileObjectId)
				|| KingdomConstruction.FindExactId(Zone, StockpileObjectId, out GameObject found) != KingdomPhysicalLookupState.Exact)
				return false;
			if (!GameObject.Validate(found) || found.IDIfAssigned != StockpileObjectId
				|| found.Physics == null || found.Physics._CurrentCell == null
				|| !ReferenceEquals(found.Physics._CurrentCell.ParentZone, Zone)
				|| found.Physics._InInventory != null || found.Physics._Equipped != null)
			{
				Failure = "the resolved stockpile object did not carry its own exact receipt id and ground placement";
				return false;
			}
			Stockpile = found;
			Failure = null;
			return true;
		}

		internal static bool TakeStock(Zone Zone, GameObject Stockpile, bool RequireExactStarter,
			out StockSnapshot Result, out string Failure)
		{
			Result = null;
			Failure = "materials stockpile is not a real placed inventory holder";
			if (!GameObject.Validate(Stockpile) || Stockpile.Inventory == null || Zone == null) return false;
			List<GameObject> children = Stockpile.Inventory.Objects;
			if (children == null) { Failure = "materials stockpile inventory could not be read"; return false; }
			var rows = new Row[children.Count];
			var seenRefs = new HashSet<GameObject>();
			var seenIds = new HashSet<string>();
			int mud = 0, brush = 0, timber = 0;
			for (int i = 0; i < children.Count; i++)
			{
				GameObject child = children[i];
				if (!GameObject.Validate(child)) { Failure = "a stockpile child failed live validation"; return false; }
				if (!seenRefs.Add(child)) { Failure = "the same object appears twice in the stockpile's inventory"; return false; }
				if (child.Physics == null || !ReferenceEquals(child.Physics._InInventory, Stockpile)
					|| child.Physics._CurrentCell != null || child.Physics._Equipped != null)
				{ Failure = "a stockpile child is not exclusively held by field-only custody"; return false; }
				string id = child.IDIfAssigned;
				if (!string.IsNullOrEmpty(id) && !seenIds.Add(id))
				{ Failure = "two stockpile children report the same non-empty id"; return false; }
				bool ordinary = KingdomMaterials.TryOrdinaryMaterialOf(child, out KingdomMaterial kind);
				KingdomMaterial? material = ordinary ? (KingdomMaterial?)kind : null;
				int count = child.Stacker?._StackCount ?? 1;
				if (count <= 0) { Failure = "a stockpile child reports a non-positive raw count"; return false; }
				if (material == KingdomMaterial.Mud) mud++;
				else if (material == KingdomMaterial.Brush) brush++;
				else if (material == KingdomMaterial.Timber) timber++;
				rows[i] = new Row(child, material);
			}
			if (RequireExactStarter)
			{
				// By material classification, not row position: the starter creator does not
				// promise row order, only exact classification and exact raw counts per kind.
				int mudCount = 0, brushCount = 0, timberCount = 0;
				foreach (Row row in rows)
				{
					if (row.Material == KingdomMaterial.Mud) mudCount = row.Count;
					else if (row.Material == KingdomMaterial.Brush) brushCount = row.Count;
					else if (row.Material == KingdomMaterial.Timber) timberCount = row.Count;
				}
				if (mud != 1 || brush != 1 || timber != 1 || mudCount != 1 || brushCount != 3 || timberCount != 4)
				{
					Failure = "starter stockpile is not the exact one-mud/three-brush/four-timber creator grant";
					return false;
				}
			}
			Result = new StockSnapshot(Stockpile, children, rows);
			Failure = null;
			return true;
		}

		/// <summary>Same ground cell, same reference, same raw row set across the call -- proves
		/// the chest itself was never replaced or relocated by the production commissioning.</summary>
		internal static bool SameStockpile(StockSnapshot Before, StockSnapshot After, out string Failure)
		{
			Failure = "stockpile identity or its own row list reference changed across commissioning";
			if (!ReferenceEquals(Before.Stockpile, After.Stockpile)
				|| !ReferenceEquals(Before.InventoryList, After.InventoryList)) return false;
			if (!ReferenceEquals(Before.Stockpile.Physics?._CurrentCell, After.Stockpile.Physics?._CurrentCell))
			{ Failure = "the stockpile's own ground cell changed across commissioning"; return false; }
			Failure = null;
			return true;
		}

		/// <summary>Exact single-material debit by object reference: the SAME row object drops by
		/// ExpectedDrop, every other row's reference and raw count are unchanged, and no row is
		/// added, removed or reordered.</summary>
		internal static bool ExactSingleDebit(StockSnapshot Before, StockSnapshot After, KingdomMaterial Material,
			int ExpectedDrop, out string Failure)
		{
			Failure = "target material row could not be matched exactly by reference before and after";
			if (Before.Rows.Length != After.Rows.Length) { Failure = "stockpile row count changed"; return false; }
			bool found = false;
			for (int i = 0; i < Before.Rows.Length; i++)
			{
				Row before = Before.Rows[i], after = After.Rows[i];
				if (!ReferenceEquals(before.Object, after.Object) || before.Material != after.Material)
				{ Failure = "a stockpile row's object reference or material classification changed"; return false; }
				if (before.Material != Material)
				{
					if (before.Count != after.Count) { Failure = "a non-target stockpile row's raw count moved"; return false; }
					continue;
				}
				if (found) { Failure = "more than one starter row classifies as the target material"; return false; }
				found = true;
				if (after.Count != before.Count - ExpectedDrop)
				{ Failure = "target material raw count did not drop by the exact expected amount"; return false; }
			}
			if (!found) { Failure = "no starter row classifies as the target material"; return false; }
			Failure = null;
			return true;
		}

		/// <summary>Re-proves the receipted cask by KingdomConstruction.FindExactId (never a bare
		/// first-match Zone.FindObjectByID), its live LiquidVolume.ParentObject reference, its
		/// ground cell/Physics, and fresh-water classification.</summary>
		internal static bool TryWater(Zone Zone, string WaterObjectId, out WaterSnapshot Result, out string Failure)
		{
			Result = null;
			Failure = "starter water cask could not be found by its exact granted id";
			if (string.IsNullOrEmpty(WaterObjectId)
				|| KingdomConstruction.FindExactId(Zone, WaterObjectId, out GameObject cask) != KingdomPhysicalLookupState.Exact)
				return false;
			if (!GameObject.Validate(cask) || cask.IDIfAssigned != WaterObjectId
				|| cask.Physics == null || cask.Physics._CurrentCell == null
				|| !ReferenceEquals(cask.Physics._CurrentCell.ParentZone, Zone))
			{ Failure = "the resolved cask did not carry its own exact receipt id and ground placement"; return false; }
			LiquidVolume volume = cask.GetPart<LiquidVolume>();
			if (volume == null || !ReferenceEquals(volume.ParentObject, cask))
			{ Failure = "the receipted cask carries no exact LiquidVolume part"; return false; }
			bool fresh = KingdomLiquids.HasFreshWater(volume);
			if (!fresh) { Failure = "the receipted cask no longer carries fresh water"; return false; }
			Result = new WaterSnapshot(cask, volume, cask.Physics._CurrentCell, volume.Volume, fresh);
			Failure = null;
			return true;
		}

		internal static bool ExactWaterDebit(WaterSnapshot Before, WaterSnapshot After, int ExpectedDrop, out string Failure)
		{
			Failure = "the receipted cask, its LiquidVolume part or its ground cell changed identity across commissioning";
			if (!ReferenceEquals(Before.Cask, After.Cask) || !ReferenceEquals(Before.Volume, After.Volume)
				|| !ReferenceEquals(Before.GroundCell, After.GroundCell)) return false;
			if (!Before.Fresh || !After.Fresh) { Failure = "the cask's fresh-water classification did not hold"; return false; }
			if (After.Drams != Before.Drams - ExpectedDrop)
			{ Failure = "stored water did not drop by the exact commissioned cost"; return false; }
			Failure = null;
			return true;
		}

		/// <summary>The one job absent before and present after, matched by full identity, its
		/// exactly-funded paid claims against the quote actually committed, and its own linked
		/// plot build output -- never a bare key search alone.</summary>
		internal static bool TryNewPaidJob(List<KingdomConstructionJob> Before, List<KingdomConstructionJob> After,
			Zone Zone, KingdomSystem System, string BuildKey, int CostDrams, KingdomPlotQuote Quote,
			out KingdomConstructionJob Job, out string Failure)
		{
			Job = null;
			Failure = "no exactly-one new job appeared after commissioning";
			var beforeIds = new HashSet<string>();
			foreach (KingdomConstructionJob job in Before) if (job?.Id != null) beforeIds.Add(job.Id);
			foreach (KingdomConstructionJob candidate in After)
			{
				if (candidate?.Id == null || beforeIds.Contains(candidate.Id)) continue;
				if (Job != null) { Failure = "more than one new job appeared after commissioning"; Job = null; return false; }
				Job = candidate;
			}
			if (Job == null) return false;
			string expectedOwner = KingdomConstruction.OwnerOf(System);
			if (Job.OwnerKey != expectedOwner || Job.ZoneId != Zone.ZoneID
				|| Job.Route != KingdomConstructionRoute.PlotCommission || Job.TargetKey != BuildKey)
			{ Failure = "the new job's settlement, zone, route or target key is not exact"; return false; }
			string expectedMaterial = Quote.MaterialClaim.ToClaimString();
			if (Job.Claims == null || !Job.Claims.Exact
				|| Job.Claims.WaterRequested != CostDrams || Job.Claims.WaterSpent != CostDrams || Job.Claims.WaterOutstanding != 0
				|| Job.Claims.WaterLost != 0 || Job.Claims.MaterialRequested != expectedMaterial
				|| Job.Claims.MaterialRequested != Job.Claims.MaterialSpent)
			{ Failure = "the new job's paid water/material claims are not exactly funded against the committed quote"; return false; }
			if (Job.Phase != KingdomConstructionPhase.Projected || Job.Projection != KingdomConstructionProjection.PlotWorks)
			{ Failure = "the new job is not in the exact projected plot-works state"; return false; }
			if (string.IsNullOrEmpty(Job.OutputId)
				|| KingdomConstruction.FindExactId(Zone, Job.OutputId, out GameObject works) != KingdomPhysicalLookupState.Exact)
			{ Failure = "the new job's linked build output could not be resolved by its exact id"; return false; }
			if (!GameObject.Validate(works) || works.IDIfAssigned != Job.OutputId || !KingdomConstruction.HasReceipt(works, Job)
				|| !works.HasPart("r_KingdomPlot") || works.Physics?._CurrentCell?.X != Job.X || works.Physics?._CurrentCell?.Y != Job.Y)
			{ Failure = "the new job's linked output does not carry its exact receipt, plot part and ground"; return false; }
			Failure = null;
			return true;
		}

		/// <summary>No survey scope may leak out of one Commission call, win or refuse.</summary>
		internal static bool SurveyScopeClear()
		{
			return !KingdomSurvey.HasBoundPass;
		}
	}
}
