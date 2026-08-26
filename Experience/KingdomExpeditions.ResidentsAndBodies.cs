using System;
using System.Collections.Generic;

using Qud.API;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomExpeditions
	{
		private static bool TrySetResident(KingdomSystem System, int ResidentId,
			KingdomResidentStanding Standing, KingdomStandingCause Cause, string ZoneId)
		{
			KingdomCityBook[] books = new KingdomCityBook[2]
			{
				(System == null) ? null : System.City,
				(System == null || System.Away == null) ? null : System.Away.City
			};
			for (int b = 0; b < books.Length; b++)
			{
				KingdomCityBook book = books[b];
				KingdomCityState state;
				KingdomCityFault fault;
				int index;
				if (book == null || !book.TryRead(out state, out fault)
					|| !state.TryResidentIndex(ResidentId, out index)) continue;
				KingdomResidentRow[] rows = new KingdomResidentRow[state.ResidentCount];
				for (int i = 0; i < rows.Length; i++)
					if (!state.TryResident(i, out rows[i])) return false;
				KingdomResidentRow before = rows[index];
				KingdomResidentRow after = before.WithStanding(Standing, Cause).WithBoundZone(ZoneId);
				if (before.Standing == after.Standing && before.Cause == after.Cause
					&& string.Equals(before.BoundZoneId, after.BoundZoneId, StringComparison.Ordinal)) return true;
				rows[index] = after;
				KingdomCityState written;
				return state.TryWithResidents(rows, out written, out fault)
					&& book.TryPublish(written, out fault);
			}
			return false;
		}

		private static bool TryReadResident(KingdomSystem System, int ResidentId,
			out KingdomResidentRow Resident)
		{
			Resident = default(KingdomResidentRow);
			KingdomCityBook[] books = new KingdomCityBook[2]
			{
				(System == null) ? null : System.City,
				(System == null || System.Away == null) ? null : System.Away.City
			};
			for (int b = 0; b < books.Length; b++)
			{
				KingdomCityState state;
				KingdomCityFault fault;
				int index;
				if (books[b] != null && books[b].TryRead(out state, out fault)
					&& state.TryResidentIndex(ResidentId, out index)
					&& state.TryResident(index, out Resident)) return true;
			}
			return false;
		}

		private static bool EnsureResidentUnbound(KingdomSystem System, int ResidentId,
			KingdomUnbindCause Cause)
		{
			if (System == null || System.Bindings == null || ResidentId <= 0) return false;
			KingdomBindingTable table;
			KingdomBinding binding;
			KingdomCityFault fault;
			if (!System.Bindings.TryRead(out table, out fault)) return false;
			if (!table.TryGet(ResidentId, KingdomBindingKind.Resident, out binding)) return true;
			GameObject exact = KingdomResidents.FindExactBindingObject(binding);
			if (GameObject.Validate(exact))
			{
				try
				{
					exact.RemoveIntProperty(ResidentJobProperty);
					exact.SetStringProperty(DebitReceiptProperty, null, RemoveIfNull: true);
				}
				catch { return false; }
			}
			KingdomResidents.Unbind(System, ResidentId, KingdomBindingKind.Resident, Cause);
			if (!System.Bindings.TryRead(out table, out fault)) return false;
			return !table.TryGet(ResidentId, KingdomBindingKind.Resident, out binding);
		}

		private static BoundBodyState FindBoundBody(KingdomSystem System, KingdomJobRow Row,
			bool LoadZone, out GameObject Body, out string ZoneId)
		{
			Body = null;
			ZoneId = null;
			if (System == null || System.Bindings == null || The.ZoneManager == null
				|| Row.Kind != KingdomJobKind.Expedition || Row.JobId <= 0
				|| Row.SubjectId <= 0) return BoundBodyState.Unreachable;
			KingdomBindingTable table;
			KingdomBinding binding;
			KingdomCityFault fault;
			if (!System.Bindings.TryRead(out table, out fault)
				|| !table.TryGet(Row.SubjectId, KingdomBindingKind.Resident, out binding)
				|| binding.BindingKey != Row.SubjectId
				|| binding.Kind != KingdomBindingKind.Resident
				|| string.IsNullOrEmpty(binding.ZoneId) || string.IsNullOrEmpty(binding.ObjectId))
				return BoundBodyState.Unreachable;

			// Exact engine id is the physical authority. Explicit Charter actions may thaw only the
			// three transaction grounds; settlement passes pass LoadZone=false and therefore defer.
			GameObject exact = KingdomResidents.FindExactBindingObject(binding);
			if (!GameObject.Validate(exact) && LoadZone)
			{
				TryExactZone(binding.ZoneId, true, out Zone ignoredBindingZone);
				exact = KingdomResidents.FindExactBindingObject(binding);
				if (!GameObject.Validate(exact))
				{
					TryExactZone(Row.SourceZoneId, true, out Zone ignoredSourceZone);
					exact = KingdomResidents.FindExactBindingObject(binding);
				}
				if (!GameObject.Validate(exact))
				{
					TryExactZone(Row.DestZoneId, true, out Zone ignoredDestZone);
					exact = KingdomResidents.FindExactBindingObject(binding);
				}
			}
			if (!GameObject.Validate(exact)
				&& GameObject.Validate(GameObject.FindByID(binding.ObjectId)))
				return BoundBodyState.Ambiguous;
			if (!GameObject.Validate(exact))
			{
				ZoneId = binding.ZoneId;
				return CandidateZonesAvailable(binding, Row)
					? BoundBodyState.Missing : BoundBodyState.Unreachable;
			}
			if (!string.Equals(exact.IDIfAssigned, binding.ObjectId, StringComparison.Ordinal)
				|| KingdomResidents.IdOf(exact) != Row.SubjectId || exact.CurrentCell == null
				|| exact.CurrentZone == null || !ReferenceEquals(exact.CurrentCell.ParentZone,
					exact.CurrentZone))
				return BoundBodyState.Ambiguous;

			string actualZone = exact.CurrentZone.ZoneID;
			bool transactionZone = string.Equals(actualZone, binding.ZoneId, StringComparison.Ordinal)
				|| string.Equals(actualZone, Row.SourceZoneId, StringComparison.Ordinal)
				|| string.Equals(actualZone, Row.DestZoneId, StringComparison.Ordinal);
			bool ledHere = (exact.IsPlayer() || exact.IsPlayerLed())
				&& ReferenceEquals(exact.CurrentZone, The.Player?.CurrentZone);
			if (!transactionZone && !ledHere) return BoundBodyState.Ambiguous;

			KingdomCityBook book;
			int locatedId;
			KingdomCityState state;
			KingdomResidentRow resident;
			int residentIndex;
			if (!KingdomResidents.TryLocate(System, exact, out book, out locatedId)
				|| locatedId != Row.SubjectId || book == null || !book.TryRead(out state, out fault)
				|| !state.TryResidentIndex(Row.SubjectId, out residentIndex)
				|| !state.TryResident(residentIndex, out resident)
				|| (resident.Standing != KingdomResidentStanding.Resident
					&& resident.Standing != KingdomResidentStanding.Expedition)
				|| (!string.IsNullOrEmpty(resident.BoundZoneId)
					&& !string.Equals(resident.BoundZoneId, binding.ZoneId, StringComparison.Ordinal)
					&& !string.Equals(resident.BoundZoneId, Row.SourceZoneId, StringComparison.Ordinal)
					&& !string.Equals(resident.BoundZoneId, Row.DestZoneId, StringComparison.Ordinal)))
				return BoundBodyState.Ambiguous;

			int jobMarker = exact.GetIntProperty(ResidentJobProperty);
			if (jobMarker != 0 && jobMarker != Row.JobId) return BoundBodyState.Ambiguous;
			if (KingdomExpeditionRules.IsDispatched(Row.OriginCode) && jobMarker != Row.JobId)
			{
				// Return recovery may have moved/rebound the exact body home and cleared its marker
				// before the resident-row/close publishes. That monotone state is the sole zero-marker
				// exception for a dispatched row.
				bool returningHome = string.Equals(actualZone, Row.SourceZoneId,
					StringComparison.Ordinal)
					&& (string.Equals(binding.ZoneId, Row.SourceZoneId, StringComparison.Ordinal)
						|| string.Equals(resident.BoundZoneId, Row.SourceZoneId,
							StringComparison.Ordinal));
				if (!returningHome) return BoundBodyState.Ambiguous;
			}
			Body = exact;
			ZoneId = actualZone;
			if (!exact.IsAlive) return BoundBodyState.Dead;
			if (exact.IsPlayer() || exact.IsPlayerLed()) return BoundBodyState.Led;
			return BoundBodyState.Alive;
		}

		private static bool CandidateZonesAvailable(KingdomBinding Binding, KingdomJobRow Row)
		{
			return ExactZoneAvailable(Binding.ZoneId)
				&& ExactZoneAvailable(Row.SourceZoneId)
				&& ExactZoneAvailable(Row.DestZoneId);
		}

		private static bool ExactZoneAvailable(string ZoneId)
		{
			if (string.IsNullOrEmpty(ZoneId) || The.ZoneManager?.CachedZones == null) return false;
			Zone zone;
			return The.ZoneManager.CachedZones.TryGetValue(ZoneId, out zone) && zone != null;
		}

		private static bool TryExactZone(string ZoneId, bool LoadZone, out Zone Zone)
		{
			Zone = null;
			if (string.IsNullOrEmpty(ZoneId) || The.ZoneManager == null) return false;
			if (The.ZoneManager.CachedZones != null
				&& The.ZoneManager.CachedZones.TryGetValue(ZoneId, out Zone) && Zone != null)
				return true;
			if (!LoadZone) return false;
			try { Zone = The.ZoneManager.GetZone(ZoneId); }
			catch { Zone = null; }
			return Zone != null;
		}

		private static Cell SafeCell(Zone Zone)
		{
			if (Zone == null) return null;
			Cell best = null;
			int bestScore = int.MaxValue;
			int cx = Zone.Width / 2;
			int cy = Zone.Height / 2;
			for (int y = 0; y < Zone.Height; y++)
			{
				for (int x = 0; x < Zone.Width; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || !cell.IsPassable() || !cell.IsEmptyOfSolid()
						|| cell.HasOpenLiquidVolume()) continue;
					int score = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
					if (score < bestScore)
					{
						best = cell;
						bestScore = score;
					}
				}
			}
			return best;
		}

		private static bool MoveExact(GameObject Body, Cell Target)
		{
			if (!GameObject.Validate(Body) || !Body.IsAlive || Target == null) return false;
			if (ReferenceEquals(Body.CurrentCell, Target)) return true;
			Zone before = Body.CurrentZone;
			try
			{
				bool moved = Body.SystemLongDistanceMoveTo(Target, 0, forced: true, ignoreCombat: true)
					&& ReferenceEquals(Body.CurrentCell, Target);
				if (moved && !ReferenceEquals(before, Body.CurrentZone))
				{
					KingdomSurvey.ObserveRemovedFromActive(before, Body);
					KingdomSurvey.ObserveAddedToActive(Body.CurrentZone, Body);
				}
				return moved;
			}
			catch
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(before, Body);
				return false;
			}
		}

	}
}
