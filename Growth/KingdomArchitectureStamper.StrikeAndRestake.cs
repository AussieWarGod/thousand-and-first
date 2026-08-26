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
	}
}
