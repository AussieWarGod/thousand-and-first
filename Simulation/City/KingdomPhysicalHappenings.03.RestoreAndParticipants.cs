using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomPhysicalHappenings
	{
		private static bool Restore(KingdomSystem system, KingdomCityBook book,
			KingdomHappeningLifecycleBook lifecycle, long nowTick)
		{
			KingdomHappeningOperation operation = lifecycle.Active;
			if (operation == null) return false;
			if (!operation.Physical)
				return KingdomHappeningLifecycleRules.RestorationSettled(operation);
			Zone zone = ExactLoadedZone(operation.ZoneId);
			if (zone == null) return false;
			for (int i = 0; i < operation.Participants.Length; i++)
			{
				KingdomHappeningParticipant row = operation.Participants[i];
				if (row.Restored) continue;
				GameObject body;
				string bound;
				bool exact = KingdomResidents.TryResolveBoundBody(system, row.ResidentId, false,
					out body, out bound) && string.Equals(bound, operation.ZoneId,
						StringComparison.Ordinal) && string.Equals(body.IDIfAssigned, row.ObjectId,
						StringComparison.Ordinal);
				if (!exact && !ParticipantGone(system, book, row)) return false;
				if (exact)
				{
					if (body.Brain == null) return false;
					string token = body.GetStringProperty(TokenProperty);
					if (!string.IsNullOrEmpty(token) && token != operation.EventId) return false;
					RemoveOwnedGoal(body, operation.EventId);
					if (!StandFromWeddingFixture(body, operation)) return false;
					KingdomStations.Post(body, row.PostWorkId, (KingdomWorkKind)row.PostKind);
					body.Brain.Wanders = row.Wanders;
					body.Brain.WandersRandomly = row.WandersRandomly;
					body.Brain.Staying = row.Staying;
					if (string.IsNullOrEmpty(row.Anchor)) body.Brain.StartingCell = null;
					else body.Brain.StartingCell = new GlobalLocation(row.Anchor);
					Cell original = zone.GetCell(row.OriginalX, row.OriginalY);
					if (original == null) return false;
					if (body.CurrentCell != original)
					{
						if (!CanWalk(body, original)) return false;
						if (!HasOwnedGoal(body, operation.EventId + ":restore"))
							body.Brain.PushGoal(new KingdomHappeningMoveTo(
								operation.EventId + ":restore", original));
						return false;
					}
					RemoveOwnedGoal(body, operation.EventId + ":restore");
					ClearBodyProjection(body);
					if (!BodyScheduleRestored(body, row)) return false;
				}
				if (!MarkRestored(book, operation.EventId, i, false, nowTick)) return false;
				if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return false;
				operation = lifecycle.Active;
			}
			if (!operation.FixtureRestored)
			{
				if (!TryFindById(zone, operation.FixtureObjectId, out GameObject fixture,
					out bool fixtureAbsent)) return false;
				if (!fixtureAbsent)
				{
					string token = fixture.GetStringProperty(FixtureTokenProperty);
					string used = fixture.GetStringProperty(FixtureUseProperty);
					if ((!string.IsNullOrEmpty(token) && token != operation.EventId)
						|| (!string.IsNullOrEmpty(used)
							&& used != operation.EventId
							&& used != operation.EventId + ":attempt")) return false;
					fixture.RemoveStringProperty(FixtureTokenProperty);
					fixture.RemoveStringProperty(FixtureUseProperty);
					if (!string.IsNullOrEmpty(fixture.GetStringProperty(FixtureTokenProperty))
						|| !string.IsNullOrEmpty(fixture.GetStringProperty(FixtureUseProperty)))
						return false;
				}
				if (!MarkRestored(book, operation.EventId, -1, true, nowTick)) return false;
				if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return false;
				operation = lifecycle.Active;
			}
			return KingdomHappeningLifecycleRules.RestorationSettled(operation);
		}

		private static bool TryParticipants(KingdomSystem system, Zone zone,
			GameObject fixture, KingdomPhysicalHappeningKind kind, int[] requiredResidents,
			bool preferConstruction, out KingdomHappeningParticipant[] participants)
		{
			participants = null;
			List<GameObject> candidates = ExactResidents(system, zone);
			if (preferConstruction)
			{
				candidates.Sort(delegate(GameObject a, GameObject b)
				{
					bool ac = a.GetIntProperty(KingdomStations.PostKindProperty)
						== (int)KingdomWorkKind.Construction;
					bool bc = b.GetIntProperty(KingdomStations.PostKindProperty)
						== (int)KingdomWorkKind.Construction;
					if (ac != bc) return ac ? -1 : 1;
					return KingdomResidents.IdOf(a).CompareTo(KingdomResidents.IdOf(b));
				});
			}
			List<GameObject> selected = new List<GameObject>();
			for (int i = 0; requiredResidents != null && i < requiredResidents.Length; i++)
			{
				GameObject required = null;
				for (int j = 0; j < candidates.Count; j++)
					if (KingdomResidents.IdOf(candidates[j]) == requiredResidents[i])
						required = candidates[j];
				if (!GameObject.Validate(required) || selected.Contains(required)) return false;
				selected.Add(required);
			}
			for (int i = 0; i < candidates.Count
				&& selected.Count < KingdomHappeningLifecycleRules.MaxParticipants; i++)
				if (!selected.Contains(candidates[i])) selected.Add(candidates[i]);
			if (selected.Count == 0) return false;
			List<Cell> targets = OpenCells(zone, fixture, kind);
			List<KingdomHappeningParticipant> rows = new List<KingdomHappeningParticipant>();
			for (int i = 0; i < selected.Count; i++)
			{
				GameObject body = selected[i];
				Cell target = null;
				for (int j = 0; j < targets.Count; j++)
				{
					if (CanWalk(body, targets[j]))
					{
						target = targets[j];
						targets.RemoveAt(j);
						break;
					}
				}
				if (target == null)
				{
					if (requiredResidents != null && i < requiredResidents.Length) return false;
					continue;
				}
				Cell original = body.CurrentCell;
				string anchor = body.Brain.StartingCell == null ? ""
					: body.Brain.StartingCell.ToString();
				rows.Add(new KingdomHappeningParticipant(KingdomResidents.IdOf(body), body.ID,
					NameOf(body), body.GetStringProperty(KingdomLodging.HomePlotIdProperty) ?? "",
					anchor, original.X, original.Y, target.X, target.Y,
					KingdomStations.PostOf(body),
					body.GetIntProperty(KingdomStations.PostKindProperty), body.Brain.Wanders,
					body.Brain.WandersRandomly, body.Brain.Staying));
			}
			if (rows.Count == 0 || (requiredResidents != null
				&& rows.Count < requiredResidents.Length)) return false;
			participants = rows.ToArray();
			return true;
		}

		private static List<GameObject> ExactResidents(KingdomSystem system, Zone zone)
		{
			List<GameObject> result = new List<GameObject>();
			foreach (GameObject candidate in KingdomSurvey.ObjectsFor(zone))
			{
				if (!GameObject.Validate(candidate) || !candidate.IsAlive || candidate.Brain == null
					|| candidate.IsPlayer() || candidate.IsPlayerLed() || IsStaged(candidate)) continue;
				int residentId = KingdomResidents.IdOf(candidate);
				GameObject exact;
				string bound;
				if (residentId > 0 && KingdomResidents.TryResolveBoundBody(system, residentId,
					false, out exact, out bound) && ReferenceEquals(exact, candidate)
					&& bound == zone.ZoneID && !string.IsNullOrEmpty(NameOf(candidate)))
					result.Add(candidate);
			}
			result.Sort(delegate(GameObject a, GameObject b)
			{
				return KingdomResidents.IdOf(a).CompareTo(KingdomResidents.IdOf(b));
			});
			return result;
		}
	}
}
