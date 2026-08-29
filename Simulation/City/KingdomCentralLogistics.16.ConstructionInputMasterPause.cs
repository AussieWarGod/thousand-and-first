using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Stages every authority-2 travel row across one observed master-off span.
		/// Source custody, cargo phase, route shape, and owner manifest remain byte-identical.</summary>
		internal static bool TryPrepareConstructionInputMasterResume(KingdomSystem system,
			IList<KingdomConstructionMasterPauseTarget> targets,
			out KingdomJobTable updated,
			out KingdomCityFault fault)
		{
			updated = null;
			fault = KingdomCityFault.NullArgument;
			if (system == null || system.Jobs == null || targets == null
				|| !system.Jobs.TryRead(out KingdomJobTable table, out fault)) return false;
			Dictionary<string, KingdomConstructionMasterPauseTarget> exactTargets =
				new Dictionary<string, KingdomConstructionMasterPauseTarget>(StringComparer.Ordinal);
			for (int i = 0; i < targets.Count; i++)
			{
				KingdomConstructionMasterPauseTarget target = targets[i];
				string key = target == null ? null
					: TargetKey(target.OperationId, target.TripId);
				if (target == null || string.IsNullOrEmpty(target.OperationId)
					|| target.TripId <= 0 || target.DesiredArrivalTick < 0L
					|| exactTargets.ContainsKey(key))
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				exactTargets.Add(key, target);
			}
			if (exactTargets.Count == 0)
			{ updated = table; fault = KingdomCityFault.None; return true; }

			List<KingdomJobRow> replacements = new List<KingdomJobRow>();
			HashSet<string> matched = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < table.Count; i++)
			{
				if (!table.TryAt(i, out KingdomJobRow row))
				{ fault = KingdomCityFault.InvalidIndex; return false; }
				if (row.DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.ConstructionInput
					|| !TravelPhase(row.DeliveryPhase)) continue;
				string key = TargetKey(row.DeliveryOwnerOperationId, row.DeliveryTripId);
				KingdomConstructionMasterPauseTarget target;
				KingdomLeg last;
				if (!exactTargets.TryGetValue(key, out target)) continue;
				if (!matched.Add(key) || !row.TryLeg(row.LegCount - 1, out last)
					|| last.ArriveTick > target.DesiredArrivalTick)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				long delta = target.DesiredArrivalTick - last.ArriveTick;
				if (delta == 0L) continue;
				if (!KingdomItineraryRules.TryShiftAll(row.Legs(), row.LegCount, delta,
					out KingdomLeg[] shifted, out fault)) return false;
				replacements.Add(row.WithLegs(shifted, shifted.Length));
			}
			if (matched.Count != exactTargets.Count)
			{ fault = KingdomCityFault.InvalidIndex; return false; }
			if (replacements.Count == 0)
			{ updated = table; fault = KingdomCityFault.None; return true; }
			return table.TryRewrite(replacements.ToArray(), replacements.Count,
				out updated, out fault);
		}

		private static string TargetKey(string operationId, int tripId)
		{ return (operationId ?? string.Empty) + "\0" + tripId.ToString(); }

		private static bool TravelPhase(KingdomDeliveryPhase phase)
		{
			return phase == KingdomDeliveryPhase.ReservationPrepared
				|| phase == KingdomDeliveryPhase.SourceDebitPrepared
				|| phase == KingdomDeliveryPhase.InFlight;
		}
	}
}
