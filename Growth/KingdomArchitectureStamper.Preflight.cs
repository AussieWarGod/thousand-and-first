using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
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
	}
}
