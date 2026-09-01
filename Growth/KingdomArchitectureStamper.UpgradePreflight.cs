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
		/// Pre-debit proof for an in-place authored tier. Only newly added placements claim current
		/// materials/craft/knowledge. Retained and removed outputs must still be exact, and any
		/// container, liquid, immutable relic, stateful anchor, or foreign occupant blocks removal.
		/// A heart tier uses the same protection law: its static fabric may be renovated while its
		/// immutable basin and state-bearing fixtures remain exact.
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
			ArchitectureLayoutSnapshot successorSnapshot;
			string lot;
			if (!TryReadOwner(Owner, out before, out snapshot, out lot, out Failure) ||
				!KingdomSocketTransitions.TryResolveCurrent(Transition, before.BuildKey,
					Successor?.BuildKey, before.LotType, before.LotSize,
					out KingdomSocketTransition declared)
				|| !KingdomArchitectureRuntime.TryDecode(Successor, out successorSnapshot,
					out Failure)
				|| successorSnapshot.IncomingTransitionMode != declared.Mode
				|| !ExactTransitionClaim(PaidClaim, declared.Materials))
				return Failure != null ? false : Fail(
					"same-set declaration or paid claim is not exactly current", out Failure);
			return TryPreflightUpgradeCore(System, Z, Owner, Successor, PaidClaim, true,
				out Delta, out Failure);
		}

		private static bool ExactTransitionClaim(KingdomMaterialDebitCost Claim,
			KingdomMaterialTally Materials)
		{
			if (Claim == null || Materials == null) return false;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				if (Claim.Materials.Get((KingdomMaterial)i)
					!= Materials.Get((KingdomMaterial)i)) return false;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
				if (Claim.Bits.Get(i) != 0) return false;
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
				if (Claim.Exotics.Get((KingdomExotic)i) != 0) return false;
			return true;
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
				|| !KingdomArchitectureRules.IsLatestSnapshotEncoding(
					beforeIntent.EncodedSnapshot)
				|| Owner.GetIntProperty(NextLayerProperty) != 3)
			{
				if (Failure == null) Failure = "authored upgrade needs one complete frozen lot";
				return false;
			}
			ArchitectureLayoutSnapshot after;
			if (!KingdomArchitectureRuntime.TryDecode(Successor, out after, out Failure)
				|| !KingdomArchitectureRules.IsLatestSnapshotEncoding(Successor.EncodedSnapshot)
				|| !KingdomArchitectureRuntime.TryVerifyPhysicalIngressRoutes(
					Z, Successor.Rect, after, out Failure))
				return false;
			bool heartAccretion;
			if (Owner.CurrentZone != Z || Owner.CurrentCell != Z.GetCell(beforeIntent.MainWorldX,
				beforeIntent.MainWorldY)
				|| !TryAuthorizedTransition(Owner, Z, beforeIntent, before, Successor, after,
					AllowPlanChange, out heartAccretion, out Failure))
				return Failure != null ? false : Fail(
					"authored successor crosses, moves, or retypes its frozen lot", out Failure);
			if (!SameRect(beforeIntent.Rect, Successor.Rect)
				&& !TryProveEnvelopeGrowth(System, Z, Owner, null, Successor, false,
					out Failure)) return false;
			KingdomPlotRules.PlotRect expectedBeforeFootprint;
			KingdomPlotRules.PlotRect standingFootprint;
			KingdomPlotRules.RoofState expectedBeforeRoof;
			if (!KingdomArchitectureRuntime.TryWorldFootprint(beforeIntent,
					out expectedBeforeFootprint, out Failure)
				|| !KingdomArchitectureRuntime.TryRoofOnGround(beforeIntent,
					KingdomPlotRules.IsUnderground(Z.Z), out expectedBeforeRoof, out Failure)
				|| !KingdomPlots.TryReadFootprint(Owner, out standingFootprint)
				|| !SameRect(expectedBeforeFootprint, standingFootprint)
				|| KingdomPlots.RoofOf(Owner) != expectedBeforeRoof)
				return Failure != null ? false : Fail(
					"standing building no longer matches its frozen footprint or roof", out Failure);
			ArchitectureLayoutDelta delta;
			if (!KingdomArchitectureRules.TryBuildDelta(before, after, out delta, out Failure)
				|| !TryBlueprintPassAudit(after, out Failure)) return false;
			HashSet<int> impacted;
			if (!TryUpgradeImpact(beforeIntent, Successor, delta, Z, out impacted,
				out Failure)) return false;

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
			for (int i = 0; i < delta.Removed.Count; i++)
			{
				ArchitecturePlacement placement = delta.Removed[i];
				GameObject exact;
				if (!TryExactOutput(Owner, Z, beforeIntent, lot, placement, out exact,
					out Failure) || !TryRemovableComponent(exact, placement, out Failure)) return false;
				owned.Add(exact);
			}
			HashSet<int> connections = ConnectionCells(Z);
			foreach (int packed in impacted)
			{
				int x = packed % Z.Width;
				int y = packed / Z.Width;
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
	}
}
