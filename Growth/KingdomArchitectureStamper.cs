using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine boundary that turns one frozen architecture receipt into exact durable scenery.
	/// Preparation may inspect current craft, stock claims, blueprints, and ground. Once an owner is
	/// frozen, every stage and proof reads only its architecture receipt and named per-slot receipts.
	/// </summary>
	public static class KingdomArchitectureStamper
	{
		public const int LayoutSchema = 1;
		public const int ComponentSchema = 1;
		public const int MaxFailureChars = 512;
		private const int MaxLotIdChars = 256;

		public const string SchemaProperty = "r_TAF_LayoutSchema";
		public const string LotIdProperty = "r_TAF_LayoutLotId";
		public const string HashProperty = "r_TAF_LayoutHash";
		public const string NextLayerProperty = "r_TAF_LayoutNextLayer";
		public const string FaultProperty = "r_TAF_LayoutFault";
		public const string OutputIdPrefix = "r_TAF_LayoutOutputId_";
		public const string OutputStatePrefix = "r_TAF_LayoutOutputState_";

		public const string ComponentSchemaProperty = "r_TAF_LayoutComponentSchema";
		public const string ComponentSlotProperty = "r_TAF_LayoutSlot";
		public const string ComponentLayerProperty = "r_TAF_LayoutLayer";
		public const string ComponentAnchorProperty = "r_TAF_LayoutAnchor";
		public const string ComponentHashProperty = "r_TAF_LayoutComponentHash";
		public const string ComponentTokenProperty = "r_TAF_LayoutComponentToken";
		public const string ComponentExistingProperty = "r_TAF_LayoutExisting";
		public const string ComponentCarriedProperty = "r_TAF_LayoutCarried";

		public const int UpgradeSchema = 1;
		public const string UpgradeSchemaProperty = "r_TAF_LayoutUpgradeSchema";
		public const string UpgradeTargetProperty = "r_TAF_LayoutUpgradeTarget";
		public const string UpgradeHashProperty = "r_TAF_LayoutUpgradeHash";
		public const string UpgradeLotProperty = "r_TAF_LayoutUpgradeLot";
		public const string UpgradePhaseProperty = "r_TAF_LayoutUpgradePhase";
		public const string UpgradeFaultProperty = "r_TAF_LayoutUpgradeFault";
		public const string UpgradeRemovePrefix = "r_TAF_LayoutUpgradeRemove_";
		public const string UpgradeRetainPrefix = "r_TAF_LayoutUpgradeRetain_";

		/// <summary>
		/// No-spend preflight for one new a2 intent. Natural placements and the immutable founding
		/// basin do not claim paid material; every other placement's material must occur in the exact
		/// future debit claim. All placements remain craft- and knowledge-gated.
		/// </summary>
		public static bool TryPreflight(KingdomSystem System, Zone Z,
			KingdomArchitectureIntent Intent, KingdomMaterialDebitCost PaidClaim,
			out string Failure)
		{
			Failure = null;
			ArchitectureLayoutSnapshot snapshot;
			if (System == null || !System.Founded || Z == null || PaidClaim == null)
				return Fail("authored layout preflight needs a founded settlement, zone, and exact paid claim",
					out Failure);
			if (!KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out Failure)) return false;
			if (!KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot))
				return Fail("legacy architecture snapshots are read-only and cannot stamp new scenery",
					out Failure);
			TechLevel liveTech = KingdomZoning.Tech(System);
			if (!KingdomZoningRules.IsKnownTechLevel(liveTech))
				return Fail("the settlement has an unknown craft rung", out Failure);
			List<string> roster = KingdomZoning.Roster(System);
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				if (!GameObjectFactory.Factory.HasBlueprint(placement.Blueprint))
					return Fail("authored slot " + placement.Slot + " names missing blueprint "
						+ placement.Blueprint, out Failure);
				int requiredTech;
				if (!KingdomArchitectureRules.TryParseTech(placement.MinTech, out requiredTech)
					|| requiredTech > (int)liveTech)
					return Fail("authored slot " + placement.Slot + " needs craft rung "
						+ (placement.MinTech ?? "<missing>"), out Failure);
				if (!string.IsNullOrEmpty(placement.Knowledge)
					&& KingdomZoningRules.MissingKnowledge(roster, placement.Knowledge).Count > 0)
					return Fail("authored slot " + placement.Slot + " needs knowledge "
						+ placement.Knowledge, out Failure);
				if (!string.IsNullOrEmpty(placement.Power))
					return Fail("authored slot " + placement.Slot + " needs power authority "
						+ placement.Power + ", but this frozen commission context proves none",
						out Failure);
				KingdomMaterial material;
				if (!KingdomMaterialRules.TryParseMaterial(placement.Material, out material))
					return Fail("authored slot " + placement.Slot + " has unknown material truth",
						out Failure);
				if (!placement.Natural && !placement.ExistingAuthority
					&& PaidClaim.Materials.Get(material) <= 0)
					return Fail("authored slot " + placement.Slot + " needs "
						+ KingdomMaterialRules.MaterialName(material)
						+ ", absent from the exact paid build claim", out Failure);
			}
			if (!TryBlueprintPassAudit(snapshot, out Failure)) return false;
			HashSet<int> connections = ConnectionCells(Z);
			HashSet<int> managed;
			if (!TryManagedCells(Intent, Z, out managed, out Failure)) return false;
			Dictionary<string, GameObject> existing;
			if (!TryExistingBindings(Z, snapshot, Intent.Rect, out existing, out Failure)) return false;
			foreach (int packed in managed)
			{
				int x = packed % Z.Width;
				int y = packed / Z.Width;
				Cell cell = Z.GetCell(x, y);
				if (cell == null || connections.Contains(packed)
					|| cell.HasObjectWithPart("StairsUp") || cell.HasObjectWithPart("StairsDown")
					|| cell.HasStairs())
					return Fail("authored layout would cover stairs or a zone connection at "
						+ Coordinate(x, y), out Failure);
				if (cell.HasOpenLiquidVolume())
					return Fail("authored layout would cover open liquid at " + Coordinate(x, y),
						out Failure);
				if (KingdomConstruction.HasActiveAt(System, Z, cell))
					return Fail("authored layout overlaps an active paid construction at "
						+ Coordinate(x, y), out Failure);
				List<GameObject> objects = cell.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (!GameObject.Validate(item) || IsExpectedExisting(item, existing)) continue;
					if (item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)
						return Fail("the immutable first basin does not align with its authored slot",
							out Failure);
					if (item.IsCreature || item.IsPlayer())
						return Fail("a living occupant stands on authored ground at "
							+ Coordinate(x, y), out Failure);
					if (item.Inventory != null && item.Inventory.Objects.Count != 0)
						return Fail("a non-empty container stands on authored ground at "
							+ Coordinate(x, y), out Failure);
					LiquidVolume liquid = item.GetPart<LiquidVolume>();
					if (liquid != null)
						return Fail("a liquid-bearing object stands on authored ground at "
							+ Coordinate(x, y), out Failure);
					string reason;
					if (KingdomMaterials.IsProtected(item, out reason))
						return Fail(reason ?? "protected state stands on authored ground", out Failure);
					KingdomPlotRules.GroundKind ground = KingdomPlots.ReadObject(item);
					if (KingdomPlotRules.Refuses(ground))
						return Fail("the " + (item.ShortDisplayNameStripped ?? item.Blueprint)
							+ " is protected on authored ground", out Failure);
				}
			}
			return true;
		}

		/// <summary>
		/// Pre-debit proof for an in-place authored tier. Only newly added placements claim current
		/// materials/craft/knowledge. Retained and removed outputs must still be exact, and any
		/// container, liquid, immutable relic, stateful anchor, or foreign occupant blocks removal.
		/// </summary>
		public static bool TryPreflightUpgrade(KingdomSystem System, Zone Z, GameObject Owner,
			KingdomArchitectureIntent Successor, KingdomMaterialDebitCost PaidClaim,
			out ArchitectureLayoutDelta Delta, out string Failure)
		{
			return TryPreflightUpgradeCore(System, Z, Owner, Successor, PaidClaim, false,
				out Delta, out Failure);
		}

		/// <summary>Pre-debit proof for one registry-declared same-set plan transition.</summary>
		public static bool TryPreflightPlanTransition(KingdomSystem System, Zone Z,
			GameObject Owner, KingdomArchitectureIntent Successor,
			KingdomSocketTransition Transition, KingdomMaterialDebitCost PaidClaim,
			out ArchitectureLayoutDelta Delta, out string Failure)
		{
			Delta = null;
			Failure = null;
			KingdomArchitectureIntent before;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (Transition == null || !TryReadOwner(Owner, out before, out snapshot, out lot,
				out Failure) || Transition.FromBuildKey != before.BuildKey
				|| Transition.ToBuildKey != Successor?.BuildKey
				|| Transition.LotType != before.LotType
				|| Transition.LotSize != before.LotSize)
				return Failure != null ? false : Fail(
					"same-set declaration does not match its frozen endpoints", out Failure);
			return TryPreflightUpgradeCore(System, Z, Owner, Successor, PaidClaim, true,
				out Delta, out Failure);
		}

		private static bool TryPreflightUpgradeCore(KingdomSystem System, Zone Z,
			GameObject Owner, KingdomArchitectureIntent Successor,
			KingdomMaterialDebitCost PaidClaim, bool AllowPlanChange,
			out ArchitectureLayoutDelta Delta, out string Failure)
		{
			Delta = null;
			Failure = null;
			KingdomArchitectureIntent beforeIntent;
			ArchitectureLayoutSnapshot before;
			string lot;
			if (System == null || !System.Founded || Z == null || PaidClaim == null
				|| !TryReadOwner(Owner, out beforeIntent, out before, out lot, out Failure)
				|| Owner.GetIntProperty(NextLayerProperty) != 3)
			{
				if (Failure == null) Failure = "authored upgrade needs one complete frozen lot";
				return false;
			}
			ArchitectureLayoutSnapshot after;
			if (!KingdomArchitectureRuntime.TryDecode(Successor, out after, out Failure)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Successor.EncodedSnapshot))
				return false;
			bool heartAccretion;
			if (Owner.CurrentZone != Z || Owner.CurrentCell != Z.GetCell(beforeIntent.MainWorldX,
				beforeIntent.MainWorldY)
				|| !TryAuthorizedTransition(Owner, Z, beforeIntent, before, Successor, after,
					AllowPlanChange, out heartAccretion, out Failure))
				return Failure != null ? false : Fail(
					"authored successor crosses, moves, or retypes its frozen lot", out Failure);
			ArchitectureLayoutDelta delta;
			if (!KingdomArchitectureRules.TryBuildDelta(before, after, out delta, out Failure)
				|| !TryBlueprintPassAudit(after, out Failure)) return false;

			TechLevel liveTech = KingdomZoning.Tech(System);
			if (!KingdomZoningRules.IsKnownTechLevel(liveTech))
				return Fail("the settlement has an unknown craft rung", out Failure);
			List<string> roster = KingdomZoning.Roster(System);
			for (int i = 0; i < delta.Added.Count; i++)
				if (!TryPlacementClaim(delta.Added[i], liveTech, roster, PaidClaim, out Failure))
					return false;

			HashSet<GameObject> owned = new HashSet<GameObject>();
			for (int i = 0; i < delta.Retained.Count; i++)
			{
				GameObject exact;
				if (!TryExactOutput(Owner, Z, beforeIntent, lot, delta.Retained[i], out exact,
					out Failure)) return false;
				owned.Add(exact);
			}
			if (heartAccretion && delta.Removed.Count != 0)
				return Fail("founding-heart accretion may not remove prior fabric", out Failure);
			for (int i = 0; i < delta.Removed.Count; i++)
			{
				ArchitecturePlacement placement = delta.Removed[i];
				GameObject exact;
				if (!TryExactOutput(Owner, Z, beforeIntent, lot, placement, out exact,
					out Failure) || !TryRemovableComponent(exact, placement, out Failure)) return false;
				owned.Add(exact);
			}
			HashSet<int> connections = ConnectionCells(Z);
			for (int i = 0; i < after.Cells.Count; i++)
			{
				ArchitectureCellState authored = after.Cells[i];
				if (!authored.Claim) continue;
				int x;
				int y;
				if (!KingdomArchitectureRuntime.TryWorldCell(after, Successor.Rect, authored,
					out x, out y, out Failure)) return false;
				int packed = y * Z.Width + x;
				Cell cell = Z.GetCell(x, y);
				if (cell == null || connections.Contains(packed) || cell.HasStairs()
					|| cell.HasObjectWithPart("StairsUp") || cell.HasObjectWithPart("StairsDown"))
					return Fail("authored tier would cover stairs or a zone connection at "
						+ Coordinate(x, y), out Failure);
				if (cell.HasOpenLiquidVolume())
					return Fail("authored tier would cover open liquid at " + Coordinate(x, y),
						out Failure);
				if (KingdomConstruction.HasActiveAt(System, Z, cell))
					return Fail("authored tier overlaps another active paid construction at "
						+ Coordinate(x, y), out Failure);
				List<GameObject> objects = cell.GetObjects();
				for (int o = 0; o < objects.Count; o++)
				{
					GameObject item = objects[o];
					if (!GameObject.Validate(item) || ReferenceEquals(item, Owner)
						|| owned.Contains(item)
						|| item.GetIntProperty(KingdomPlots.HeartStakeProperty) == 1
						|| KingdomPlots.ReadObject(item) == KingdomPlotRules.GroundKind.Bare) continue;
					return Fail("foreign or protected state occupies authored successor ground at "
						+ Coordinate(x, y), out Failure);
				}
			}
			Delta = delta;
			return true;
		}

		/// <summary>
		/// Proves that an explicit strike owns every removable current authored component exactly.
		/// The immutable founding basin is retained. Non-empty containers, liquid, protected
		/// settlement state, missing outputs, and injected PlotPart objects all refuse before work.
		/// </summary>
		public static bool TryPreflightStrike(GameObject Owner, Zone Z, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (Z == null || !TryReadOwner(Owner, out intent, out snapshot, out lot, out Failure)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(intent.EncodedSnapshot)
				|| Owner.CurrentZone != Z || Owner.GetIntProperty(NextLayerProperty) != 3)
			{
				if (Failure == null) Failure = "authored strike needs one complete exact layout owner";
				return false;
			}
			if (Owner.Inventory != null && Owner.Inventory.Objects.Count != 0)
				return Fail("the authored building must be emptied before it can be struck", out Failure);
			LiquidVolume ownerLiquid = Owner.GetPart<LiquidVolume>();
			if (ownerLiquid != null && ownerLiquid.Volume > 0)
				return Fail("the authored building still contains liquid and cannot be struck",
					out Failure);
			HashSet<string> removableIds = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				GameObject exact;
				if (!TryExactOutput(Owner, Z, intent, lot, placement, out exact, out Failure))
					return false;
				if (placement.ExistingAuthority)
				{
					if (exact.GetIntProperty(KingdomPlots.HeartRelicProperty) != 1)
						return Fail("existing-authority strike output is not the immutable basin",
							out Failure);
					continue;
				}
				if (!TryStrikeRemovable(exact, placement, out Failure)) return false;
				if (!removableIds.Add(exact.ID))
					return Fail("authored strike output identity is duplicated", out Failure);
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.PlotParts)
			{
				if (!GameObject.Validate(item) || item.GetIntProperty(
					KingdomPlots.PlotPartProperty) != 1
					|| item.GetStringProperty(KingdomPlots.PlotIdProperty) != lot) continue;
				if (!removableIds.Remove(item.ID))
					return Fail("foreign or unreceipted plot part shares the authored lot", out Failure);
			}
			if (removableIds.Count != 0)
				return Fail("authored strike receipt omits a standing owned component", out Failure);
			return true;
		}

		/// <summary>
		/// Pre-debit restake proof for a socket conversion. The current owner's exact removable
		/// pieces are treated as future absence; everything else is audited like a fresh authored lot.
		/// </summary>
		public static bool TryPreflightRestake(KingdomSystem System, Zone Z, GameObject Owner,
			KingdomArchitectureIntent Intent, KingdomMaterialDebitCost PaidClaim,
			out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || Z == null || PaidClaim == null
				|| !TryPreflightStrike(Owner, Z, out Failure)) return false;
			if (Owner.GetIntProperty(KingdomPlots.HeartPlotProperty) == 1)
				return Fail("the founding heart cannot be retyped or restaked", out Failure);
			KingdomArchitectureIntent oldIntent;
			ArchitectureLayoutSnapshot oldSnapshot;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (!TryReadOwner(Owner, out oldIntent, out oldSnapshot, out lot, out Failure)
				|| !KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out Failure)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot))
				return false;
			// A true retype is an ordinary fresh siting. Its behavior root is expected to move;
			// the old owner exists here only to prove the strike set and protected state.
			TechLevel liveTech = KingdomZoning.Tech(System);
			if (!KingdomZoningRules.IsKnownTechLevel(liveTech))
				return Fail("the settlement has an unknown craft rung", out Failure);
			List<string> roster = KingdomZoning.Roster(System);
			for (int i = 0; i < snapshot.Placements.Count; i++)
				if (!TryPlacementClaim(snapshot.Placements[i], liveTech, roster, PaidClaim,
					out Failure)) return false;
			if (!TryBlueprintPassAudit(snapshot, out Failure)) return false;
			HashSet<GameObject> oldOwned = new HashSet<GameObject>();
			for (int i = 0; i < oldSnapshot.Placements.Count; i++)
			{
				GameObject exact;
				if (!TryExactOutput(Owner, Z, oldIntent, lot, oldSnapshot.Placements[i],
					out exact, out Failure)) return false;
				oldOwned.Add(exact);
			}
			Dictionary<string, GameObject> existing;
			if (!TryExistingBindings(Z, snapshot, Intent.Rect, out existing, out Failure)) return false;
			HashSet<int> managed;
			if (!TryManagedCells(Intent, Z, out managed, out Failure)) return false;
			HashSet<int> connections = ConnectionCells(Z);
			foreach (int packed in managed)
			{
				int x = packed % Z.Width;
				int y = packed / Z.Width;
				Cell cell = Z.GetCell(x, y);
				if (cell == null || connections.Contains(packed) || cell.HasStairs()
					|| cell.HasObjectWithPart("StairsUp") || cell.HasObjectWithPart("StairsDown"))
					return Fail("socket restake would cover stairs or a zone connection at "
						+ Coordinate(x, y), out Failure);
				if (cell.HasOpenLiquidVolume())
					return Fail("socket restake would cover open liquid at " + Coordinate(x, y),
						out Failure);
				if (KingdomConstruction.HasActiveAt(System, Z, cell))
					return Fail("socket restake overlaps active paid construction at "
						+ Coordinate(x, y), out Failure);
				List<GameObject> objects = cell.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (!GameObject.Validate(item) || ReferenceEquals(item, Owner)
						|| oldOwned.Contains(item) || IsExpectedExisting(item, existing)
						|| item.GetIntProperty(KingdomPlots.HeartStakeProperty) == 1
						|| KingdomPlots.ReadObject(item) == KingdomPlotRules.GroundKind.Bare) continue;
					return Fail("foreign or protected state occupies socket restake ground at "
						+ Coordinate(x, y), out Failure);
				}
			}
			return true;
		}

		/// <summary>
		/// Reconstructs and proves a paid successor solely from the standing owner and frozen
		/// successor receipt. No current architecture catalogue, material table, or building entry is
		/// consulted. Used by projection/retry after the no-spend preflight has crossed debit.
		/// </summary>
		public static bool TryValidateFrozenUpgrade(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Successor, out ArchitectureLayoutDelta Delta,
			out string Failure)
		{
			Delta = null;
			Failure = null;
			KingdomArchitectureIntent beforeIntent;
			ArchitectureLayoutSnapshot before;
			ArchitectureLayoutSnapshot after;
			string lot;
			if (Z == null) return Fail("frozen authored upgrade has no exact zone", out Failure);
			if (!TryUpgradeBase(Owner, Z, Successor, out beforeIntent, out before,
				out after, out Delta, out lot, out Failure)) return false;
			if (Owner.CurrentZone != Z || Owner.CurrentCell != Z.GetCell(beforeIntent.MainWorldX,
				beforeIntent.MainWorldY) || Owner.GetIntProperty(NextLayerProperty) != 3)
				return Fail("frozen authored predecessor is not complete on its exact main cell",
					out Failure);
			return true;
		}

		/// <summary>
		/// Applies one frozen same-lot delta without consulting current catalogues. The predecessor
		/// remains the durable controller until every exact removal, retained retag, and added layer
		/// proves itself on the already-rooted successor behavior object.
		/// </summary>
		public static bool TryApplyUpgrade(GameObject Owner, GameObject Target, Zone Z,
			KingdomArchitectureIntent Successor, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Owner) || !GameObject.Validate(Target) || Z == null
				|| Owner.CurrentZone != Z || Target.CurrentZone != Z
				|| Target.CurrentCell != Z.GetCell(Successor == null ? -1 : Successor.MainWorldX,
					Successor == null ? -1 : Successor.MainWorldY))
				return Fail("authored upgrade endpoints do not stand on the frozen main cell", out Failure);
			KingdomArchitectureIntent beforeIntent;
			ArchitectureLayoutSnapshot before;
			ArchitectureLayoutSnapshot after;
			ArchitectureLayoutDelta delta;
			string lot;
			if (!TryUpgradeBase(Owner, Z, Successor, out beforeIntent, out before, out after,
				out delta, out lot, out Failure)) return false;

			bool marked = Owner.HasIntProperty(UpgradeSchemaProperty)
				|| Owner.HasStringProperty(UpgradeSchemaProperty);
			if (!marked)
			{
				if (!TryVerifyComplete(Owner, Z, out Failure)) return false;
				for (int i = 0; i < delta.Removed.Count; i++)
				{
					GameObject exact;
					if (!TryExactOutput(Owner, Z, beforeIntent, lot, delta.Removed[i],
						out exact, out Failure)
						|| !TryRemovableComponent(exact, delta.Removed[i], out Failure)) return false;
				}
				if (!TryBeginUpgradeReceipt(Owner, Target, Successor, lot, delta, out Failure))
					return false;
			}
			else if (!TryReadUpgradeReceipt(Owner, Target, Successor, lot, out Failure))
				return false;

			int phase = Owner.GetIntProperty(UpgradePhaseProperty);
			if (phase == 0)
			{
				Target.SetStringProperty(KingdomPlots.PlotIdProperty, lot);
				KingdomArchitectureIntent targetIntent;
				ArchitectureLayoutSnapshot targetSnapshot;
				string targetLot;
				if (!KingdomArchitectureStamper.TryReadOwner(Target, out targetIntent,
					out targetSnapshot, out targetLot, out _))
				{
					if (!KingdomArchitectureRuntime.TryFreeze(Target, Successor, out Failure)
						|| !TryInitializeOwner(Target, Successor, lot, out Failure))
						return UpgradeFail(Owner, Failure, out Failure);
				}
				else if (targetLot != lot || targetIntent.SnapshotHash != Successor.SnapshotHash)
					return UpgradeFail(Owner, "successor already carries another layout receipt",
						out Failure);
				Owner.SetIntProperty(UpgradePhaseProperty, 1);
				phase = 1;
			}
			if (!ExactSuccessorOwner(Target, Successor, lot, out Failure))
				return UpgradeFail(Owner, Failure, out Failure);

			if (phase == 1)
			{
				for (int i = 0; i < delta.Removed.Count; i++)
					if (!TryRemoveUpgradeSlot(Owner, Z, beforeIntent, lot, delta.Removed[i],
						out Failure)) return UpgradeFail(Owner, Failure, out Failure);
				Owner.SetIntProperty(UpgradePhaseProperty, 2);
				phase = 2;
			}
			if (phase == 2)
			{
				for (int i = 0; i < delta.Retained.Count; i++)
					if (!TryCarryUpgradeSlot(Owner, Target, Z, beforeIntent, Successor, lot,
						delta.Retained[i], delta.RetainedAfter[i], out Failure))
						return UpgradeFail(Owner, Failure, out Failure);
				Owner.SetIntProperty(UpgradePhaseProperty, 3);
				phase = 3;
			}
			if (phase == 3)
			{
				if (!TryStageLayer(Target, Z, ArchitectureLayer.Ground, out Failure)
					|| !TryStageLayer(Target, Z, ArchitectureLayer.Structure, out Failure)
					|| !TryStageLayer(Target, Z, ArchitectureLayer.Object, out Failure)
					|| !TryVerifyComplete(Target, Z, out Failure))
					return UpgradeFail(Owner, Failure, out Failure);
				Owner.SetIntProperty(UpgradePhaseProperty, 4);
				phase = 4;
			}
			if (phase != 4 || !TryVerifyComplete(Target, Z, out Failure))
				return UpgradeFail(Owner, Failure ?? "authored upgrade phase is malformed", out Failure);
			return true;
		}

		/// <summary>Freeze layout ownership on detached works. Schema is final commit marker.</summary>
		public static bool TryInitializeOwner(GameObject Owner, KingdomArchitectureIntent Intent,
			string LotId, out string Failure)
		{
			Failure = null;
			ArchitectureLayoutSnapshot snapshot;
			if (Owner == null || !KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out Failure)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot)
				|| !ValidLotId(LotId))
			{
				if (Failure == null) Failure = "layout owner, current snapshot, or lot identity is malformed";
				return false;
			}
			try
			{
				Owner.RemoveIntProperty(SchemaProperty);
				Owner.SetStringProperty(LotIdProperty, LotId);
				Owner.SetStringProperty(HashProperty, Intent.SnapshotHash);
				Owner.SetIntProperty(NextLayerProperty, 0);
				Owner.SetStringProperty(FaultProperty, null, RemoveIfNull: true);
				for (int i = 0; i < snapshot.Placements.Count; i++)
				{
					Owner.SetStringProperty(OutputId(snapshot.Placements[i]), null, RemoveIfNull: true);
					Owner.RemoveIntProperty(OutputState(snapshot.Placements[i]));
				}
				Owner.SetIntProperty(SchemaProperty, LayoutSchema);
			}
			catch (Exception exception)
			{
				try { Owner.RemoveIntProperty(SchemaProperty); } catch { }
				return Fail("layout owner receipt write failed: " + exception.Message, out Failure);
			}
			KingdomArchitectureIntent readIntent;
			ArchitectureLayoutSnapshot readSnapshot;
			string readLot;
			return TryReadOwner(Owner, out readIntent, out readSnapshot, out readLot, out Failure)
				&& readLot == LotId;
		}

		public static bool TryReadOwner(GameObject Owner, out KingdomArchitectureIntent Intent,
			out ArchitectureLayoutSnapshot Snapshot, out string LotId, out string Failure)
		{
			Intent = null;
			Snapshot = null;
			LotId = null;
			Failure = null;
			if (Owner == null || !Owner.HasIntProperty(SchemaProperty)
				|| Owner.HasStringProperty(SchemaProperty)
				|| Owner.GetIntProperty(SchemaProperty) != LayoutSchema)
				return Fail("layout owner receipt is absent, partial, or unknown", out Failure);
			string fault = Owner.GetStringProperty(FaultProperty);
			if (!string.IsNullOrEmpty(fault))
				return Fail("layout owner is quarantined: " + Bounded(fault), out Failure);
			string lot = Owner.GetStringProperty(LotIdProperty);
			string hash = Owner.GetStringProperty(HashProperty);
			if (!ValidLotId(lot) || hash == null || hash.Length != 64
				|| !KingdomArchitectureRuntime.TryRead(Owner, out Intent, out Failure)
				|| !KingdomArchitectureRuntime.TryDecode(Intent, out Snapshot, out Failure)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot)
				|| hash != Intent.SnapshotHash)
				return Failure != null ? false : Fail("layout owner scalars disagree with its snapshot",
					out Failure);
			int next = Owner.GetIntProperty(NextLayerProperty);
			if (!Owner.HasIntProperty(NextLayerProperty) || next < 0 || next > 3)
				return Fail("layout owner stage is absent or malformed", out Failure);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				int state = Owner.GetIntProperty(OutputState(placement));
				string id = Owner.GetStringProperty(OutputId(placement));
				if (state < 0 || state > 2 || (state == 0 && !string.IsNullOrEmpty(id))
					|| (state > 0 && (string.IsNullOrEmpty(id)
						|| id.Length > KingdomConstructionRules.MaxSubjectChars))
					|| ((int)placement.Layer < next && state != 2))
					return Fail("layout slot receipt " + placement.Slot + " is malformed", out Failure);
			}
			LotId = lot;
			return true;
		}

		/// <summary>Copy complete frozen authority from works to detached final root.</summary>
		public static bool TryCopyFrozenOwner(GameObject Source, GameObject Target, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (Target == null || !TryReadOwner(Source, out intent, out snapshot, out lot, out Failure)
				|| Source.GetIntProperty(NextLayerProperty) != 3)
			{
				if (Failure == null) Failure = "only a complete layout owner may become a final root";
				return false;
			}
			if (!KingdomArchitectureRuntime.TryCopyFrozen(Source, Target, out Failure)) return false;
			try
			{
				Target.RemoveIntProperty(SchemaProperty);
				Target.SetStringProperty(LotIdProperty, lot);
				Target.SetStringProperty(HashProperty, intent.SnapshotHash);
				Target.SetIntProperty(NextLayerProperty, 3);
				Target.SetStringProperty(FaultProperty, null, RemoveIfNull: true);
				for (int i = 0; i < snapshot.Placements.Count; i++)
				{
					ArchitecturePlacement placement = snapshot.Placements[i];
					Target.SetStringProperty(OutputId(placement),
						Source.GetStringProperty(OutputId(placement)));
					Target.SetIntProperty(OutputState(placement), 2);
				}
				Target.SetIntProperty(SchemaProperty, LayoutSchema);
			}
			catch (Exception exception)
			{
				try { Target.RemoveIntProperty(SchemaProperty); } catch { }
				return Fail("layout owner copy failed: " + exception.Message, out Failure);
			}
			KingdomArchitectureIntent ignoredIntent;
			ArchitectureLayoutSnapshot ignoredSnapshot;
			string checkedLot;
			return TryReadOwner(Target, out ignoredIntent, out ignoredSnapshot, out checkedLot,
				out Failure) && checkedLot == lot;
		}

		public static bool TryManagedCells(KingdomArchitectureIntent Intent, Zone Z,
			out HashSet<int> Cells, out string Failure)
		{
			Cells = null;
			Failure = null;
			ArchitectureLayoutSnapshot snapshot;
			if (Z == null || !KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out Failure))
				return false;
			HashSet<int> result = new HashSet<int>();
			for (int i = 0; i < snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = snapshot.Cells[i];
				if (!cell.Claim) continue;
				int x;
				int y;
				if (!KingdomArchitectureRuntime.TryWorldCell(snapshot, Intent.Rect, cell,
					out x, out y, out Failure)) return false;
				result.Add(y * Z.Width + x);
			}
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				int x;
				int y;
				if (!KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Intent.Rect,
					snapshot.Placements[i], out x, out y, out Failure)) return false;
				result.Add(y * Z.Width + x);
			}
			Cells = result;
			return true;
		}

		/// <summary>Stamp one exact layer. Interruption after output-ID publication fails closed.</summary>
		public static bool TryStageLayer(GameObject Owner, Zone Z, ArchitectureLayer Layer,
			out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (Z == null || !TryReadOwner(Owner, out intent, out snapshot, out lot, out Failure))
				return false;
			int target = (int)Layer;
			if (target < 0 || target > 2) return Fail("layout layer is unknown", out Failure);
			int next = Owner.GetIntProperty(NextLayerProperty);
			if (next > target) return TryVerifyLayer(Owner, Z, intent, snapshot, lot, Layer, out Failure);
			if (next < target) return Fail("layout layers must settle ground, structure, then object",
				out Failure);
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				if (placement.Layer != Layer) continue;
				if (!TrySettlePlacement(Owner, Z, intent, snapshot, lot, placement, out Failure))
					return false;
			}
			if (!TryVerifyPassabilityThrough(Z, intent, snapshot, lot, Layer, out Failure))
			{
				string rollback;
				bool clean = TryRollbackNewLayout(Owner, Z, intent, snapshot, lot, out rollback);
				return Quarantine(Owner, Failure + (clean ? "; exact new pieces rolled back"
					: "; exact rollback failed: " + rollback), out Failure);
			}
			Owner.SetIntProperty(NextLayerProperty, target + 1);
			return TryVerifyLayer(Owner, Z, intent, snapshot, lot, Layer, out Failure);
		}

		public static bool TryVerifyComplete(GameObject Owner, Zone Z, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (Z == null || !TryReadOwner(Owner, out intent, out snapshot, out lot, out Failure)
				|| Owner.GetIntProperty(NextLayerProperty) != 3)
			{
				if (Failure == null) Failure = "authored layout is not complete";
				return false;
			}
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				GameObject exact;
				if (!TryExactOutput(Owner, Z, intent, lot, placement, out exact, out Failure)) return false;
			}
			return TryVerifyPassability(Z, intent, snapshot, lot, out Failure);
		}

		private static bool TrySettlePlacement(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			string stateProperty = OutputState(Placement);
			string idProperty = OutputId(Placement);
			int state = Owner.GetIntProperty(stateProperty);
			if (state == 2)
			{
				GameObject settled;
				return TryExactOutput(Owner, Z, Intent, Lot, Placement, out settled, out Failure);
			}
			if (state == 1)
			{
				GameObject pending;
				KingdomPhysicalLookupState found = KingdomConstruction.FindExactId(Z,
					Owner.GetStringProperty(idProperty), out pending);
				if (found != KingdomPhysicalLookupState.Exact)
					return Quarantine(Owner, "layout slot " + Placement.Slot
						+ " lost its published output before settlement", out Failure);
				if (Placement.ExistingAuthority && IsExactExistingCore(pending, Placement, Intent))
				{
					StampComponent(pending, Lot, Intent.SnapshotHash, Placement);
					KingdomSurvey.ObserveChangedInActive(Z, pending);
				}
				if (!ExactComponent(pending, Z, Intent, Lot, Placement, Owner.GetStringProperty(idProperty)))
					return Quarantine(Owner, "layout slot " + Placement.Slot
						+ " changed after output publication", out Failure);
				Owner.SetIntProperty(stateProperty, 2);
				return true;
			}
			if (state != 0 || !string.IsNullOrEmpty(Owner.GetStringProperty(idProperty)))
				return Quarantine(Owner, "layout slot " + Placement.Slot
					+ " has a malformed creation receipt", out Failure);

			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Intent.Rect, Placement,
				out x, out y, out Failure)) return false;
			Cell cell = Z.GetCell(x, y);
			GameObject placed;
			if (Placement.ExistingAuthority)
			{
				if (!TryFindExistingAt(Z, Placement, cell, out placed, out Failure)) return false;
				Owner.SetStringProperty(idProperty, placed.ID);
				Owner.SetIntProperty(stateProperty, 1);
				StampComponent(placed, Lot, Intent.SnapshotHash, Placement);
			}
			else
			{
				if (!CanInsert(Owner, Z, cell, Lot, Intent.SnapshotHash, Placement, out Failure))
					return false;
				try { placed = GameObject.Create(Placement.Blueprint); }
				catch (Exception exception)
				{
					return Fail("layout slot " + Placement.Slot + " creation threw: "
						+ exception.Message, out Failure);
				}
				if (!GameObject.Validate(placed))
					return Fail("layout slot " + Placement.Slot + " created no exact object", out Failure);
				StampComponent(placed, Lot, Intent.SnapshotHash, Placement);
				Owner.SetStringProperty(idProperty, placed.ID);
				Owner.SetIntProperty(stateProperty, 1);
				try
				{
					GameObject accepted = cell.AddObject(placed, NoStack: true, Silent: true);
					KingdomSurvey.ObserveAddResultInActive(Z, placed, accepted);
					if (!ReferenceEquals(accepted, placed))
						return Quarantine(Owner, "layout slot " + Placement.Slot
							+ " AddObject replaced its exact output", out Failure);
				}
				catch (Exception exception)
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Z, placed);
					return Quarantine(Owner, "layout slot " + Placement.Slot
						+ " AddObject threw after output publication: " + exception.Message, out Failure);
				}
			}
			KingdomSurvey.ObserveChangedInActive(Z, placed);
			if (!ExactComponent(placed, Z, Intent, Lot, Placement,
				Owner.GetStringProperty(idProperty)))
				return Quarantine(Owner, "layout slot " + Placement.Slot
					+ " failed exact settlement proof", out Failure);
			Owner.SetIntProperty(stateProperty, 2);
			return true;
		}

		private static bool TryVerifyLayer(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitectureLayer Layer, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement.Layer != Layer) continue;
				GameObject exact;
				if (!TryExactOutput(Owner, Z, Intent, Lot, placement, out exact, out Failure)) return false;
			}
			return true;
		}

		private static bool TryExactOutput(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, string Lot, ArchitecturePlacement Placement,
			out GameObject Exact, out string Failure)
		{
			Exact = null;
			Failure = null;
			if (Owner.GetIntProperty(OutputState(Placement)) != 2)
				return Fail("layout slot " + Placement.Slot + " is not settled", out Failure);
			string id = Owner.GetStringProperty(OutputId(Placement));
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Z, id, out Exact);
			if (state != KingdomPhysicalLookupState.Exact
				|| !ExactComponent(Exact, Z, Intent, Lot, Placement, id))
				return Quarantine(Owner, "settled layout slot " + Placement.Slot
					+ " is absent, moved, duplicated, or changed", out Failure);
			return true;
		}

		private static bool ExactComponent(GameObject Item, Zone Z,
			KingdomArchitectureIntent Intent, string Lot, ArchitecturePlacement Placement,
			string ExpectedId)
		{
			if (!GameObject.Validate(Item) || Item.ID != ExpectedId || Item.CurrentZone != Z
				|| Item.Blueprint != Placement.Blueprint
				|| Item.GetIntProperty(ComponentSchemaProperty) != ComponentSchema
				|| Item.GetStringProperty(KingdomPlots.PlotIdProperty) != Lot
				|| Item.GetStringProperty(ComponentSlotProperty) != Placement.Slot
				|| Item.GetIntProperty(ComponentLayerProperty) != (int)Placement.Layer
				|| Item.GetStringProperty(ComponentHashProperty) != Intent.SnapshotHash
				|| Item.GetStringProperty(ComponentTokenProperty)
					!= ComponentToken(Lot, Intent.SnapshotHash, Placement)
				|| Item.GetIntProperty(ComponentExistingProperty)
					!= (Placement.ExistingAuthority ? 1 : 0)
				|| Item.GetIntProperty(KingdomPlots.PlotPartProperty)
					!= (Placement.ExistingAuthority ? 0 : 1)) return false;
			string anchor = Item.GetStringProperty(ComponentAnchorProperty);
			if ((Placement.StatefulAnchor ?? "") != (anchor ?? "")) return false;
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out _)) return false;
			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Intent.Rect, Placement,
				out x, out y, out _) || Item.CurrentCell != Z.GetCell(x, y)) return false;
			int count = 0;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject candidate in survey.ArchitectureComponents)
				if (GameObject.Validate(candidate)
					&& candidate.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& candidate.GetStringProperty(ComponentSlotProperty) == Placement.Slot) count++;
			return count == 1;
		}

		private static bool CanInsert(GameObject Owner, Zone Z, Cell Cell, string Lot,
			string Hash, ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			if (Cell == null) return Fail("layout slot lies outside its frozen zone", out Failure);
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item) || ReferenceEquals(item, Owner)
					|| item.GetIntProperty(KingdomPlots.HeartStakeProperty) == 1) continue;
				if (item.IsCreature || item.IsPlayer())
					return Fail("a living occupant moved onto layout slot " + Placement.Slot,
						out Failure);
				if (item.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& item.GetStringProperty(ComponentHashProperty) == Hash
					&& item.GetIntProperty(ComponentSchemaProperty) == ComponentSchema) continue;
				if (KingdomPlots.ReadObject(item) == KingdomPlotRules.GroundKind.Bare) continue;
				return Fail("protected or foreign state moved onto layout slot " + Placement.Slot,
					out Failure);
			}
			return true;
		}

		private static void StampComponent(GameObject Item, string Lot, string Hash,
			ArchitecturePlacement Placement)
		{
			Item.SetIntProperty(KingdomPlots.PlotPartProperty,
				Placement.ExistingAuthority ? 0 : 1);
			Item.SetStringProperty(KingdomPlots.PlotIdProperty, Lot);
			Item.SetStringProperty(ComponentSlotProperty, Placement.Slot);
			Item.SetIntProperty(ComponentLayerProperty, (int)Placement.Layer);
			Item.SetStringProperty(ComponentAnchorProperty, Placement.StatefulAnchor,
				RemoveIfNull: true);
			Item.SetStringProperty(ComponentHashProperty, Hash);
			Item.SetStringProperty(ComponentTokenProperty, ComponentToken(Lot, Hash, Placement));
			Item.SetIntProperty(ComponentExistingProperty, Placement.ExistingAuthority ? 1 : 0);
			Item.RemoveIntProperty(ComponentCarriedProperty);
			Item.SetIntProperty(ComponentSchemaProperty, ComponentSchema);
		}

		private static Dictionary<string, GameObject> EmptyExisting()
		{
			return new Dictionary<string, GameObject>(StringComparer.Ordinal);
		}

		private static bool TryExistingBindings(Zone Z, ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, out Dictionary<string, GameObject> Existing,
			out string Failure)
		{
			Existing = EmptyExisting();
			Failure = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (!placement.ExistingAuthority) continue;
				int x;
				int y;
				if (!KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Rect, placement,
					out x, out y, out Failure)) return false;
				GameObject exact;
				if (!TryFindExistingAt(Z, placement, Z.GetCell(x, y), out exact, out Failure))
					return false;
				Existing[placement.Slot] = exact;
			}
			return true;
		}

		private static bool TryFindExistingAt(Zone Z, ArchitecturePlacement Placement,
			Cell ExpectedCell, out GameObject Exact, out string Failure)
		{
			Exact = null;
			Failure = null;
			if (!Placement.ExistingAuthority || Placement.Blueprint != KingdomPlots.HeartRelicBlueprint
				|| ExpectedCell == null)
				return Fail("existing-authority slot is not the immutable first basin", out Failure);
			int count = 0;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.HeartRelics)
			{
				if (!GameObject.Validate(item)
					|| item.GetIntProperty(KingdomPlots.HeartRelicProperty) != 1) continue;
				count++;
				Exact = item;
			}
			if (count != 1 || Exact.Blueprint != Placement.Blueprint
				|| Exact.CurrentCell != ExpectedCell || Exact.CurrentZone != Z)
			{
				Exact = null;
				return Fail("the immutable first basin is absent, duplicated, moved, or misaligned",
					out Failure);
			}
			return true;
		}

		private static bool IsExpectedExisting(GameObject Item,
			Dictionary<string, GameObject> Existing)
		{
			foreach (KeyValuePair<string, GameObject> pair in Existing)
				if (ReferenceEquals(pair.Value, Item)) return true;
			return false;
		}

		private static bool IsExactExistingCore(GameObject Item,
			ArchitecturePlacement Placement, KingdomArchitectureIntent Intent)
		{
			if (!GameObject.Validate(Item) || !Placement.ExistingAuthority
				|| Item.Blueprint != Placement.Blueprint
				|| Item.GetIntProperty(KingdomPlots.HeartRelicProperty) != 1) return false;
			ArchitectureLayoutSnapshot snapshot;
			int x;
			int y;
			return KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out _)
				&& KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Intent.Rect, Placement,
					out x, out y, out _) && Item.CurrentCell != null
				&& Item.CurrentCell.X == x && Item.CurrentCell.Y == y;
		}

		private static bool TryPlacementClaim(ArchitecturePlacement Placement,
			TechLevel LiveTech, List<string> Roster, KingdomMaterialDebitCost PaidClaim,
			out string Failure)
		{
			if (Placement == null || !GameObjectFactory.Factory.HasBlueprint(Placement.Blueprint))
				return Fail("added authored slot names a missing blueprint", out Failure);
			int requiredTech;
			if (!KingdomArchitectureRules.TryParseTech(Placement.MinTech, out requiredTech)
				|| requiredTech > (int)LiveTech)
				return Fail("added authored slot " + Placement.Slot + " needs craft rung "
					+ (Placement.MinTech ?? "<missing>"), out Failure);
			if (!string.IsNullOrEmpty(Placement.Knowledge)
				&& KingdomZoningRules.MissingKnowledge(Roster, Placement.Knowledge).Count > 0)
				return Fail("added authored slot " + Placement.Slot + " needs knowledge "
					+ Placement.Knowledge, out Failure);
			if (!string.IsNullOrEmpty(Placement.Power))
				return Fail("added authored slot " + Placement.Slot + " needs power authority "
					+ Placement.Power + ", but this frozen improvement context proves none",
					out Failure);
			KingdomMaterial material;
			if (!KingdomMaterialRules.TryParseMaterial(Placement.Material, out material))
				return Fail("added authored slot " + Placement.Slot + " has unknown material truth",
					out Failure);
			if (!Placement.Natural && !Placement.ExistingAuthority
				&& PaidClaim.Materials.Get(material) <= 0)
				return Fail("added authored slot " + Placement.Slot + " needs "
					+ KingdomMaterialRules.MaterialName(material)
					+ ", absent from the exact paid improvement claim", out Failure);
			Failure = null;
			return true;
		}

		private static bool TryRemovableComponent(GameObject Item,
			ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item) || Placement == null || Placement.ExistingAuthority
				|| !string.IsNullOrEmpty(Placement.StatefulAnchor)
				|| Item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)
				return Fail("immutable or stateful authored slot "
					+ (Placement == null ? "<missing>" : Placement.Slot) + " cannot be removed",
					out Failure);
			if (Item.Inventory != null && Item.Inventory.Objects.Count != 0)
				return Fail("authored slot " + Placement.Slot
					+ " is a non-empty container and cannot be removed", out Failure);
			LiquidVolume liquid = Item.GetPart<LiquidVolume>();
			if (liquid != null && (liquid.Volume > 0 || liquid.MaxVolume < 0))
				return Fail("authored slot " + Placement.Slot
					+ " contains liquid and cannot be removed", out Failure);
			if (Item.GetIntProperty("KingdomBuilt") == 1
				|| Item.GetIntProperty("KingdomCitizen") == 1
				|| Item.GetIntProperty("KingdomStores") == 1
				|| Item.GetIntProperty("KingdomLarder") == 1
				|| Item.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
				return Fail("authored slot " + Placement.Slot
					+ " carries protected settlement state", out Failure);
			return true;
		}

		private static bool TryStrikeRemovable(GameObject Item,
			ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item) || Placement == null || Placement.ExistingAuthority
				|| Item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)
				return Fail("immutable authored slot cannot enter the strike target set", out Failure);
			if (Item.Inventory != null && Item.Inventory.Objects.Count != 0)
				return Fail("authored slot " + Placement.Slot
					+ " must be emptied before strike", out Failure);
			LiquidVolume liquid = Item.GetPart<LiquidVolume>();
			if (liquid != null && liquid.Volume > 0)
				return Fail("authored slot " + Placement.Slot
					+ " contains liquid and cannot be struck", out Failure);
			if (Item.GetIntProperty("KingdomBuilt") == 1
				|| Item.GetIntProperty("KingdomCitizen") == 1
				|| Item.GetIntProperty("KingdomStores") == 1
				|| Item.GetIntProperty("KingdomLarder") == 1
				|| Item.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
				return Fail("authored slot " + Placement.Slot
					+ " carries protected settlement state", out Failure);
			return true;
		}

		private static bool TryUpgradeBase(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Successor,
			out KingdomArchitectureIntent BeforeIntent, out ArchitectureLayoutSnapshot Before,
			out ArchitectureLayoutSnapshot After, out ArchitectureLayoutDelta Delta,
			out string Lot, out string Failure)
		{
			BeforeIntent = null;
			Before = null;
			After = null;
			Delta = null;
			Lot = null;
			Failure = null;
			if (Owner == null || !KingdomArchitectureRuntime.TryRead(Owner, out BeforeIntent,
				out Failure) || !KingdomArchitectureRuntime.TryDecode(BeforeIntent, out Before,
				out Failure) || !KingdomArchitectureRuntime.TryDecode(Successor, out After,
				out Failure)) return false;
			Lot = Owner.GetStringProperty(LotIdProperty);
			bool heartAccretion;
			if (!KingdomArchitectureRules.IsCurrentSnapshotEncoding(BeforeIntent.EncodedSnapshot)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Successor.EncodedSnapshot)
				|| !ValidLotId(Lot) || Owner.GetStringProperty(HashProperty) != BeforeIntent.SnapshotHash
				|| !TryAuthorizedTransition(Owner, Z, BeforeIntent, Before, Successor, After,
					false, out heartAccretion, out Failure))
				return Fail("authored upgrade receipt crosses its frozen layout set", out Failure);
			return KingdomArchitectureRules.TryBuildDelta(Before, After, out Delta, out Failure);
		}

		private static bool TryAuthorizedTransition(GameObject Owner, Zone Z,
			KingdomArchitectureIntent BeforeIntent, ArchitectureLayoutSnapshot Before,
			KingdomArchitectureIntent AfterIntent, ArchitectureLayoutSnapshot After,
			bool AllowPlanChange, out bool HeartAccretion, out string Failure)
		{
			HeartAccretion = false;
			Failure = null;
			if (Owner == null || Z == null || BeforeIntent == null || AfterIntent == null
				|| Before == null || After == null || Before.PlanKey != After.PlanKey
				|| Before.LotType != After.LotType || Before.Facing != After.Facing
				|| BeforeIntent.MainWorldX != AfterIntent.MainWorldX
				|| BeforeIntent.MainWorldY != AfterIntent.MainWorldY)
				return Fail("authored transition changes plan, lot type, pose, or main root", out Failure);
			int beforeRung = KingdomPlotRules.HeartRungOf(Before.BuildKey);
			int afterRung = KingdomPlotRules.HeartRungOf(After.BuildKey);
			if (beforeRung == 0 && afterRung == 0 && Before.BindingKey == After.BindingKey
				&& Before.LotSize == After.LotSize
				&& SameRect(BeforeIntent.Rect, AfterIntent.Rect)) return true;
			if (beforeRung == 0 && afterRung == 0 && Before.LotSize == After.LotSize
				&& SameRect(BeforeIntent.Rect, AfterIntent.Rect)
				&& (AllowPlanChange || KingdomSocketTransitions.Authorizes(Owner,
					BeforeIntent, AfterIntent))) return true;

			KingdomPlotRules.PlotRect expectedBefore;
			KingdomPlotRules.PlotRect expectedAfter;
			if (beforeRung < 1 || afterRung != beforeRung + 1
				|| Before.PlanKey != "civic-heart" || After.PlanKey != "civic-heart"
				|| Before.LotType != "civic" || After.LotType != "civic"
				|| (int)Before.LotSize != beforeRung || (int)After.LotSize != afterRung
				|| Owner.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1
				|| KingdomPlots.HeartRung(Z) != beforeRung
				|| !KingdomPlots.TryHeartRectFor(Z, beforeRung, out expectedBefore)
				|| !KingdomPlots.TryHeartRectFor(Z, afterRung, out expectedAfter)
				|| !SameRect(BeforeIntent.Rect, expectedBefore)
				|| !SameRect(AfterIntent.Rect, expectedAfter)
				|| Owner.GetStringProperty(KingdomPlots.PlotIdProperty)
					!= Owner.GetStringProperty(LotIdProperty)
				|| !TryExactHeartBasin(Owner, Z, BeforeIntent, Before, out Failure)
				|| !TryHeartSnapshotBasin(AfterIntent, After, Z, out Failure))
				return Failure != null ? false : Fail(
					"cross-size authored transition is not adjacent founding-heart accretion",
					out Failure);
			HeartAccretion = true;
			return true;
		}

		private static bool TryExactHeartBasin(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Failure = null;
			ArchitecturePlacement basin;
			if (!TryHeartBasinPlacement(Intent, Snapshot, Z, out basin, out Failure)) return false;
			string lot = Owner.GetStringProperty(LotIdProperty);
			GameObject exact;
			return TryExactOutput(Owner, Z, Intent, lot, basin, out exact, out Failure)
				&& exact.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1;
		}

		private static bool TryHeartSnapshotBasin(KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, Zone Z, out string Failure)
		{
			ArchitecturePlacement ignored;
			return TryHeartBasinPlacement(Intent, Snapshot, Z, out ignored, out Failure);
		}

		private static bool TryHeartBasinPlacement(KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, Zone Z, out ArchitecturePlacement Basin,
			out string Failure)
		{
			Basin = null;
			Failure = null;
			int riteX;
			int riteY;
			if (!KingdomPlots.TryRiteGround(Z, out riteX, out riteY))
				return Fail("founding-heart transition has no recorded rite", out Failure);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (!placement.ExistingAuthority) continue;
				if (Basin != null || placement.Blueprint != KingdomPlots.HeartRelicBlueprint
					|| placement.StatefulAnchor != "fixture:first-basin")
					return Fail("founding-heart snapshot has malformed existing authority", out Failure);
				Basin = placement;
			}
			int x;
			int y;
			if (Basin == null || !KingdomArchitectureRuntime.TryWorldPlacement(Snapshot,
				Intent.Rect, Basin, out x, out y, out Failure))
				return Failure != null ? false : Fail(
					"founding-heart snapshot has no immutable basin", out Failure);
			if (x != riteX || y != riteY)
				return Fail("founding-heart snapshot moves the immutable basin", out Failure);
			return true;
		}

		private static bool TryBeginUpgradeReceipt(GameObject Owner, GameObject Target,
			KingdomArchitectureIntent Successor, string Lot, ArchitectureLayoutDelta Delta,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Target) || string.IsNullOrEmpty(Target.ID)
				|| Target.ID.Length > KingdomConstructionRules.MaxSubjectChars)
				return Fail("authored successor has no bounded exact identity", out Failure);
			try
			{
				Owner.RemoveIntProperty(UpgradeSchemaProperty);
				Owner.SetStringProperty(UpgradeTargetProperty, Target.ID);
				Owner.SetStringProperty(UpgradeHashProperty, Successor.SnapshotHash);
				Owner.SetStringProperty(UpgradeLotProperty, Lot);
				Owner.SetIntProperty(UpgradePhaseProperty, 0);
				Owner.SetStringProperty(UpgradeFaultProperty, null, RemoveIfNull: true);
				for (int i = 0; i < Delta.Removed.Count; i++)
					Owner.RemoveIntProperty(UpgradeRemove(Delta.Removed[i]));
				for (int i = 0; i < Delta.Retained.Count; i++)
					Owner.RemoveIntProperty(UpgradeRetain(Delta.Retained[i]));
				Owner.SetIntProperty(UpgradeSchemaProperty, UpgradeSchema);
			}
			catch (Exception exception)
			{
				try { Owner.RemoveIntProperty(UpgradeSchemaProperty); } catch { }
				return Fail("authored upgrade receipt write threw: " + exception.Message, out Failure);
			}
			return TryReadUpgradeReceipt(Owner, Target, Successor, Lot, out Failure);
		}

		private static bool TryReadUpgradeReceipt(GameObject Owner, GameObject Target,
			KingdomArchitectureIntent Successor, string Lot, out string Failure)
		{
			Failure = null;
			if (Owner == null || Target == null || !Owner.HasIntProperty(UpgradeSchemaProperty)
				|| Owner.HasStringProperty(UpgradeSchemaProperty)
				|| Owner.GetIntProperty(UpgradeSchemaProperty) != UpgradeSchema
				|| Owner.GetStringProperty(UpgradeTargetProperty) != Target.ID
				|| Owner.GetStringProperty(UpgradeHashProperty) != Successor.SnapshotHash
				|| Owner.GetStringProperty(UpgradeLotProperty) != Lot)
				return Fail("authored upgrade receipt is absent, partial, unknown, or changed",
					out Failure);
			string fault = Owner.GetStringProperty(UpgradeFaultProperty);
			if (!string.IsNullOrEmpty(fault))
				return Fail("authored upgrade is quarantined: " + Bounded(fault), out Failure);
			int phase = Owner.GetIntProperty(UpgradePhaseProperty);
			if (!Owner.HasIntProperty(UpgradePhaseProperty) || phase < 0 || phase > 4)
				return Fail("authored upgrade phase is absent or malformed", out Failure);
			return true;
		}

		private static bool ExactSuccessorOwner(GameObject Target,
			KingdomArchitectureIntent Successor, string Lot, out string Failure)
		{
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string observedLot;
			return TryReadOwner(Target, out intent, out snapshot, out observedLot, out Failure)
				&& observedLot == Lot && intent.SnapshotHash == Successor.SnapshotHash
				&& SameRect(intent.Rect, Successor.Rect)
				&& intent.MainWorldX == Successor.MainWorldX
				&& intent.MainWorldY == Successor.MainWorldY;
		}

		private static bool TryRemoveUpgradeSlot(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Before, string Lot, ArchitecturePlacement Placement,
			out string Failure)
		{
			Failure = null;
			string stateProperty = UpgradeRemove(Placement);
			int state = Owner.GetIntProperty(stateProperty);
			if (Owner.HasStringProperty(stateProperty) || state < 0 || state > 2)
				return Fail("authored removal receipt for slot " + Placement.Slot + " is malformed",
					out Failure);
			string id = Owner.GetStringProperty(OutputId(Placement));
			if (state == 2)
				return KingdomConstruction.FindExactId(Z, id, out _)
					== KingdomPhysicalLookupState.Absent || Fail("removed authored slot "
						+ Placement.Slot + " reappeared", out Failure);
			GameObject exact;
			if (KingdomConstruction.FindExactId(Z, id, out exact)
				!= KingdomPhysicalLookupState.Exact
				|| !ExactComponent(exact, Z, Before, Lot, Placement, id)
				|| !TryRemovableComponent(exact, Placement, out Failure))
				return Failure != null ? false : Fail("authored removal source " + Placement.Slot
					+ " is absent, duplicated, moved, or changed", out Failure);
			if (state == 0) Owner.SetIntProperty(stateProperty, 1);
			bool removed;
			try { removed = exact.Destroy(null, Silent: true); }
			catch (Exception exception)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, exact);
				return Fail("authored removal " + Placement.Slot + " threw: "
					+ exception.Message, out Failure);
			}
			if (removed && !GameObject.Validate(exact))
				KingdomSurvey.ObserveRemovedFromActive(Z, exact);
			if (!removed || GameObject.Validate(exact)
				|| KingdomConstruction.FindExactId(Z, id, out _)
					!= KingdomPhysicalLookupState.Absent)
				return Fail("authored removal " + Placement.Slot
					+ " was vetoed or changed during callback", out Failure);
			Owner.SetIntProperty(stateProperty, 2);
			return true;
		}

		private static bool TryCarryUpgradeSlot(GameObject Owner, GameObject Target, Zone Z,
			KingdomArchitectureIntent Before, KingdomArchitectureIntent After, string Lot,
			ArchitecturePlacement BeforePlacement, ArchitecturePlacement AfterPlacement,
			out string Failure)
		{
			Failure = null;
			if (BeforePlacement == null || AfterPlacement == null)
				return Fail("authored retained placement pair is absent", out Failure);
			string stateProperty = UpgradeRetain(BeforePlacement);
			int state = Owner.GetIntProperty(stateProperty);
			if (Owner.HasStringProperty(stateProperty) || state < 0 || state > 2)
				return Fail("authored retained receipt for slot " + BeforePlacement.Slot + " is malformed",
					out Failure);
			string id = Owner.GetStringProperty(OutputId(BeforePlacement));
			if (state == 0)
			{
				GameObject old;
				if (KingdomConstruction.FindExactId(Z, id, out old)
					!= KingdomPhysicalLookupState.Exact
					|| !ExactComponent(old, Z, Before, Lot, BeforePlacement, id))
					return Fail("retained authored slot " + BeforePlacement.Slot
						+ " changed before successor publication", out Failure);
				Target.SetStringProperty(OutputId(AfterPlacement), id);
				Target.SetIntProperty(OutputState(AfterPlacement), 1);
				Owner.SetIntProperty(stateProperty, 1);
				state = 1;
			}
			if (state == 1)
			{
				GameObject exact;
				if (KingdomConstruction.FindExactId(Z, id, out exact)
					!= KingdomPhysicalLookupState.Exact)
					return Fail("retained authored slot " + BeforePlacement.Slot
						+ " vanished after identity publication", out Failure);
				if (ExactComponent(exact, Z, Before, Lot, BeforePlacement, id))
				{
					StampComponent(exact, Lot, After.SnapshotHash, AfterPlacement);
					exact.SetIntProperty(ComponentCarriedProperty, 1);
				}
				else if (!ExactComponent(exact, Z, After, Lot, AfterPlacement, id))
					return Fail("retained authored slot " + BeforePlacement.Slot
						+ " changed during successor retag", out Failure);
				else exact.SetIntProperty(ComponentCarriedProperty, 1);
				Target.SetIntProperty(OutputState(AfterPlacement), 2);
				Owner.SetIntProperty(stateProperty, 2);
				state = 2;
			}
			GameObject settled;
			return state == 2 && KingdomConstruction.FindExactId(Z, id, out settled)
				== KingdomPhysicalLookupState.Exact
				&& ExactComponent(settled, Z, After, Lot, AfterPlacement, id)
				&& settled.GetIntProperty(ComponentCarriedProperty) == 1
				|| Fail("retained authored slot " + BeforePlacement.Slot
					+ " did not settle on the successor", out Failure);
		}

		private static string UpgradeRemove(ArchitecturePlacement Placement)
		{
			return UpgradeRemovePrefix + PropertySlot(Placement.Slot);
		}

		private static string UpgradeRetain(ArchitecturePlacement Placement)
		{
			return UpgradeRetainPrefix + PropertySlot(Placement.Slot);
		}

		private static bool UpgradeFail(GameObject Owner, string Message, out string Failure)
		{
			Failure = Bounded(Message ?? "authored upgrade refused without a reason");
			try { Owner.SetStringProperty(UpgradeFaultProperty, Failure); } catch { }
			return false;
		}

		private static bool SameRect(KingdomPlotRules.PlotRect A,
			KingdomPlotRules.PlotRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}

		private static bool TryBlueprintPassAudit(ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Failure = null;
			for (int c = 0; c < Snapshot.Cells.Count; c++)
			{
				ArchitectureCellState cell = Snapshot.Cells[c];
				if (!cell.Claim) continue;
				bool solid = false;
				bool door = false;
				for (int p = 0; p < Snapshot.Placements.Count; p++)
				{
					ArchitecturePlacement placement = Snapshot.Placements[p];
					if (placement.X != cell.X || placement.Y != cell.Y) continue;
					GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(
						placement.Blueprint);
					if (blueprint == null)
						return Fail("pass audit names missing blueprint " + placement.Blueprint,
							out Failure);
					bool isDoor = blueprint.HasPart("Door");
					door |= isDoor;
					if (!isDoor && blueprint.HasPart("Physics")
						&& blueprint.GetPartParameter("Physics", "Solid", false)) solid = true;
					if (cell.Passability == ArchitecturePassability.Walkable && !isDoor
						&& blueprint.HasPart("Physics")
						&& blueprint.GetPartParameter("Physics", "Solid", false))
						return Fail("walkable authored cell " + Coordinate(cell.X, cell.Y)
							+ " contains solid blueprint " + placement.Blueprint, out Failure);
				}
				if (cell.Passability == ArchitecturePassability.Blocked && (!solid || door))
					return Fail("blocked authored cell " + Coordinate(cell.X, cell.Y)
						+ " lacks one solid non-door concrete blueprint", out Failure);
				if (cell.Passability == ArchitecturePassability.Adjacent
					&& !HasCardinalWalkCell(Snapshot, cell.X, cell.Y))
					return Fail("adjacent-use authored cell " + Coordinate(cell.X, cell.Y)
						+ " has no cardinal walk/door use cell", out Failure);
			}
			return true;
		}

		private static bool TryVerifyPassabilityThrough(Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitectureLayer Through, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (!cell.Claim || HighestLayerAt(Snapshot, cell.X, cell.Y) > (int)Through) continue;
				if (!TryVerifyPassabilityCell(Z, Intent, Snapshot, Lot, cell, out Failure))
					return false;
			}
			return true;
		}

		private static bool TryVerifyPassability(Zone Z, KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, string Lot, out string Failure)
		{
			return TryVerifyPassabilityThrough(Z, Intent, Snapshot, Lot,
				ArchitectureLayer.Object, out Failure);
		}

		private static bool TryVerifyPassabilityCell(Zone Z, KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, string Lot, ArchitectureCellState CellState,
			out string Failure)
		{
			Failure = null;
			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryWorldCell(Snapshot, Intent.Rect, CellState,
				out x, out y, out Failure)) return false;
			Cell cell = Z.GetCell(x, y);
			if (cell == null) return Fail("authored pass cell left its exact zone", out Failure);
			bool authoredDoor = HasAuthoredDoor(cell, Lot, Intent.SnapshotHash);
			bool walk = cell.IsPassable() || authoredDoor;
			if (CellState.Passability == ArchitecturePassability.Walkable && !walk)
				return Fail("concrete authored walk cell is blocked at " + Coordinate(x, y), out Failure);
			if (CellState.Passability == ArchitecturePassability.Blocked
				&& (cell.IsPassable() || authoredDoor))
				return Fail("concrete authored blocked cell is passable or a door at "
					+ Coordinate(x, y), out Failure);
			if (CellState.Passability == ArchitecturePassability.Adjacent)
			{
				int[] dx = new int[4] { 0, 1, 0, -1 };
				int[] dy = new int[4] { -1, 0, 1, 0 };
				bool reached = false;
				for (int d = 0; d < 4 && !reached; d++)
				{
					ArchitectureCellState neighbour = FindCell(Snapshot,
						CellState.X + dx[d], CellState.Y + dy[d]);
					if (neighbour == null
						|| neighbour.Passability != ArchitecturePassability.Walkable) continue;
					int nx;
					int ny;
					if (!KingdomArchitectureRuntime.TryWorldCell(Snapshot, Intent.Rect, neighbour,
						out nx, out ny, out Failure)) return false;
					Cell use = Z.GetCell(nx, ny);
					reached = use != null && (use.IsPassable()
						|| HasAuthoredDoor(use, Lot, Intent.SnapshotHash));
				}
				if (!reached)
					return Fail("adjacent-use authored cell has no concrete cardinal use cell at "
						+ Coordinate(x, y), out Failure);
			}
			return true;
		}

		private static int HighestLayerAt(ArchitectureLayoutSnapshot Snapshot, int X, int Y)
		{
			int layer = -1;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement.X == X && placement.Y == Y && (int)placement.Layer > layer)
					layer = (int)placement.Layer;
			}
			return layer;
		}

		private static ArchitectureCellState FindCell(ArchitectureLayoutSnapshot Snapshot,
			int X, int Y)
		{
			if (X < 0 || X >= Snapshot.Width || Y < 0 || Y >= Snapshot.Height) return null;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
				if (Snapshot.Cells[i].X == X && Snapshot.Cells[i].Y == Y) return Snapshot.Cells[i];
			return null;
		}

		private static bool HasCardinalWalkCell(ArchitectureLayoutSnapshot Snapshot, int X, int Y)
		{
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			for (int i = 0; i < 4; i++)
			{
				ArchitectureCellState cell = FindCell(Snapshot, X + dx[i], Y + dy[i]);
				if (cell != null && cell.Claim
					&& cell.Passability == ArchitecturePassability.Walkable) return true;
			}
			return false;
		}

		private static bool HasAuthoredDoor(Cell Cell, string Lot, string Hash)
		{
			if (Cell == null) return false;
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (GameObject.Validate(item) && item.IsDoor()
					&& item.GetIntProperty(ComponentSchemaProperty) == ComponentSchema
					&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& item.GetStringProperty(ComponentHashProperty) == Hash) return true;
			}
			return false;
		}

		private static bool TryRollbackNewLayout(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			out string Failure)
		{
			Failure = null;
			for (int i = Snapshot.Placements.Count - 1; i >= 0; i--)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement.ExistingAuthority
					|| Owner.GetIntProperty(OutputState(placement)) == 2
						&& FindCarriedComponent(Z, Owner.GetStringProperty(OutputId(placement)))
					|| Owner.GetIntProperty(OutputState(placement)) == 0) continue;
				string id = Owner.GetStringProperty(OutputId(placement));
				GameObject item;
				if (KingdomConstruction.FindExactId(Z, id, out item)
					!= KingdomPhysicalLookupState.Exact
					|| !ExactComponent(item, Z, Intent, Lot, placement, id))
					return Fail("rollback cannot prove exact slot " + placement.Slot, out Failure);
				bool removed;
				try { removed = item.Obliterate(null, Silent: true); }
				catch (Exception exception)
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Z, item);
					return Fail("rollback of slot " + placement.Slot + " threw: "
						+ exception.Message, out Failure);
				}
				if (removed && !GameObject.Validate(item))
					KingdomSurvey.ObserveRemovedFromActive(Z, item);
				if (!removed || GameObject.Validate(item)
					|| KingdomConstruction.FindExactId(Z, id, out _)
						!= KingdomPhysicalLookupState.Absent)
					return Fail("rollback could not remove exact slot " + placement.Slot, out Failure);
				Owner.SetStringProperty(OutputId(placement), null, RemoveIfNull: true);
				Owner.RemoveIntProperty(OutputState(placement));
			}
			return true;
		}

		private static bool FindCarriedComponent(Zone Z, string Id)
		{
			GameObject exact;
			return KingdomConstruction.FindExactId(Z, Id, out exact)
				== KingdomPhysicalLookupState.Exact
				&& exact.GetIntProperty(ComponentCarriedProperty) == 1;
		}

		private static HashSet<int> ConnectionCells(Zone Z)
		{
			HashSet<int> result = new HashSet<int>();
			foreach (ZoneConnection connection in Z.EnumerateConnections())
				AddConnection(result, Z, connection);
			if (Z.ZoneConnectionCache != null)
				for (int i = 0; i < Z.ZoneConnectionCache.Count; i++)
					AddConnection(result, Z, Z.ZoneConnectionCache[i]);
			return result;
		}

		private static void AddConnection(HashSet<int> Into, Zone Z, ZoneConnection Connection)
		{
			if (Connection != null && Connection.X >= 0 && Connection.X < Z.Width
				&& Connection.Y >= 0 && Connection.Y < Z.Height)
				Into.Add(Connection.Y * Z.Width + Connection.X);
		}

		private static string OutputId(ArchitecturePlacement Placement)
		{
			return OutputIdPrefix + PropertySlot(Placement.Slot);
		}

		private static string OutputState(ArchitecturePlacement Placement)
		{
			return OutputStatePrefix + PropertySlot(Placement.Slot);
		}

		private static string PropertySlot(string Slot)
		{
			return Slot == null ? "invalid" : Slot.Replace(':', '_');
		}

		private static string ComponentToken(string Lot, string Hash,
			ArchitecturePlacement Placement)
		{
			string preimage = Lot + "|" + Hash + "|" + Placement.Slot + "|"
				+ ((int)Placement.Layer).ToString(CultureInfo.InvariantCulture) + "|"
				+ Placement.X.ToString(CultureInfo.InvariantCulture) + "|"
				+ Placement.Y.ToString(CultureInfo.InvariantCulture) + "|"
				+ Placement.Blueprint + "|" + (Placement.StatefulAnchor ?? "") + "|"
				+ (Placement.ExistingAuthority ? "1" : "0");
			byte[] digest;
			using (SHA256 sha = SHA256.Create())
				digest = sha.ComputeHash(Encoding.UTF8.GetBytes(preimage));
			StringBuilder result = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++)
				result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return result.ToString();
		}

		private static bool ValidLotId(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxLotIdChars
				|| Value != Value.Trim()) return false;
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return false;
			return true;
		}

		private static string Coordinate(int X, int Y)
		{
			return X.ToString(CultureInfo.InvariantCulture) + ","
				+ Y.ToString(CultureInfo.InvariantCulture);
		}

		private static bool Quarantine(GameObject Owner, string Message, out string Failure)
		{
			Failure = Bounded(Message);
			try { Owner.SetStringProperty(FaultProperty, Failure); } catch { }
			return false;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Bounded(Message);
			return false;
		}

		private static string Bounded(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "authored layout refused without a reason";
			return Value.Length <= MaxFailureChars ? Value : Value.Substring(0, MaxFailureChars);
		}
	}
}
