using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private sealed class PendingPlotReservation
		{
			internal GameObject Owner;
			internal GameObject Target;
			internal string Lot;
			internal KingdomPlotRules.PlotRect Before;
			internal KingdomPlotRules.PlotRect After;
		}

		/// <summary>Builds one geometric reservation per durable lot. A committed improvement
		/// reserves its frozen successor envelope even while the target's mirrored PlotX/Y scalars
		/// are only a crash prefix; duplicate or malformed ownership fails closed.</summary>
		private static bool TryReadReservedPlots(Zone Z, KingdomSurvey Survey,
			out List<KingdomPlotRules.PlotRect> Plots)
		{
			Plots = new List<KingdomPlotRules.PlotRect>();
			if (Z == null || Survey == null) return false;
			Dictionary<string, PendingPlotReservation> pending =
				new Dictionary<string, PendingPlotReservation>(StringComparer.Ordinal);
			HashSet<GameObject> pendingTargets = new HashSet<GameObject>();
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject item = Survey.Objects[i];
				if (!item.HasIntProperty(KingdomArchitectureStamper.UpgradeSchemaProperty)
					&& !item.HasStringProperty(
						KingdomArchitectureStamper.UpgradeSchemaProperty)) continue;
				if (!TryReadPendingReservation(Z, item, out PendingPlotReservation row)
					|| pending.ContainsKey(row.Lot) || !pendingTargets.Add(row.Target)) return false;
				pending.Add(row.Lot, row);
			}

			Dictionary<string, KingdomPlotRules.PlotRect> byLot =
				new Dictionary<string, KingdomPlotRules.PlotRect>(StringComparer.Ordinal);
			Dictionary<string, GameObject> rootByLot =
				new Dictionary<string, GameObject>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, PendingPlotReservation> pair in pending)
				byLot.Add(pair.Key, pair.Value.After);
			for (int i = 0; i < Survey.PlotRoots.Count; i++)
			{
				GameObject root = Survey.PlotRoots[i];
				if (!TryReadRect(root, out KingdomPlotRules.PlotRect rect)) return false;
				string lot = root.GetStringProperty(PlotIdProperty);
				if (string.IsNullOrEmpty(lot))
				{
					Plots.Add(rect);
					continue;
				}
				if (pending.TryGetValue(lot, out PendingPlotReservation reservation))
				{
					if (!ReferenceEquals(root, reservation.Owner)
						&& !ReferenceEquals(root, reservation.Target)) return false;
					if (ReferenceEquals(root, reservation.Target)
						&& !SameRect(rect, reservation.After)) return false;
					if (ReferenceEquals(root, reservation.Owner)
						&& !SameRect(rect, reservation.Before)) return false;
					continue;
				}
				if (rootByLot.ContainsKey(lot)) return false;
				rootByLot.Add(lot, root);
				byLot.Add(lot, rect);
			}
			foreach (KeyValuePair<string, KingdomPlotRules.PlotRect> pair in byLot)
				Plots.Add(pair.Value);
			return true;
		}

		private static bool TryReadPendingReservation(Zone Z, GameObject Owner,
			out PendingPlotReservation Reservation)
		{
			Reservation = null;
			if (!GameObject.Validate(Owner) || Owner.CurrentZone != Z
				|| !Owner.HasIntProperty(KingdomArchitectureStamper.UpgradeSchemaProperty)
				|| Owner.HasStringProperty(KingdomArchitectureStamper.UpgradeSchemaProperty)
				|| Owner.GetIntProperty(KingdomArchitectureStamper.UpgradeSchemaProperty)
					!= KingdomArchitectureStamper.UpgradeSchema
				|| !ExactUpgradeString(Owner,
					KingdomArchitectureStamper.UpgradeTargetProperty, out string targetId)
				|| !ExactUpgradeString(Owner,
					KingdomArchitectureStamper.UpgradeHashProperty, out string hash)
				|| !ExactUpgradeString(Owner,
					KingdomArchitectureStamper.UpgradeLotProperty, out string lot)
				|| !Owner.HasIntProperty(KingdomArchitectureStamper.UpgradePhaseProperty)
				|| Owner.HasStringProperty(KingdomArchitectureStamper.UpgradePhaseProperty)) return false;
			int phase = Owner.GetIntProperty(KingdomArchitectureStamper.UpgradePhaseProperty);
			if (phase < 0 || phase > 5) return false;
			if (!KingdomArchitectureStamper.TryReadOwner(Owner,
				out KingdomArchitectureIntent beforeIntent, out _, out string beforeLot, out _)
				|| beforeLot != lot || !TryReadRect(Owner, out KingdomPlotRules.PlotRect before)
				|| !SameRect(before, beforeIntent.Rect)) return false;
			if (KingdomConstruction.FindGlobalLiveId(targetId, out GameObject target)
				!= KingdomPhysicalLookupState.Exact || ReferenceEquals(target, Owner)
				|| target.CurrentZone != Z
				|| !KingdomArchitectureStamper.TryReadOwner(target,
					out KingdomArchitectureIntent afterIntent, out _, out string afterLot, out _)
				|| afterLot != lot || afterIntent.SnapshotHash != hash
				|| !ContainsRect(afterIntent.Rect, beforeIntent.Rect)
				|| !ExactOrAbsentString(target, PlotIdProperty, lot)
				|| !ExactOrAbsentInt(target, PlotX1Property, afterIntent.Rect.X1)
				|| !ExactOrAbsentInt(target, PlotY1Property, afterIntent.Rect.Y1)
				|| !ExactOrAbsentInt(target, PlotX2Property, afterIntent.Rect.X2)
				|| !ExactOrAbsentInt(target, PlotY2Property, afterIntent.Rect.Y2)) return false;
			Reservation = new PendingPlotReservation
			{
				Owner = Owner,
				Target = target,
				Lot = lot,
				Before = beforeIntent.Rect,
				After = afterIntent.Rect,
			};
			return true;
		}

		private static bool ExactUpgradeString(GameObject Object, string Property,
			out string Value)
		{
			Value = Object.GetStringProperty(Property);
			return Object.HasStringProperty(Property) && !Object.HasIntProperty(Property)
				&& !string.IsNullOrEmpty(Value)
				&& Value.Length <= KingdomConstructionRules.MaxSubjectChars;
		}

		private static bool ContainsRect(KingdomPlotRules.PlotRect Outer,
			KingdomPlotRules.PlotRect Inner)
		{
			return Outer.X1 <= Inner.X1 && Outer.Y1 <= Inner.Y1
				&& Outer.X2 >= Inner.X2 && Outer.Y2 >= Inner.Y2;
		}
	}
}
