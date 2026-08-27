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
		private static bool ExactBodyReceipt(GameObject body,
			KingdomHappeningOperation operation, KingdomHappeningParticipant row)
		{
			return body.GetStringProperty(TokenProperty) == operation.EventId
				&& body.GetStringProperty(PostReceiptProperty) == PostReceipt(row)
				&& body.GetStringProperty(AnchorReceiptProperty) == row.Anchor
				&& body.GetStringProperty(HomeReceiptProperty) == row.Home
				&& body.GetStringProperty(TargetReceiptProperty) == row.TargetX.ToString(
					CultureInfo.InvariantCulture) + "," + row.TargetY.ToString(
					CultureInfo.InvariantCulture)
				&& body.GetStringProperty(FixtureReceiptProperty) == operation.FixtureObjectId
				&& body.GetStringProperty(OriginalReceiptProperty) == row.OriginalX.ToString(
					CultureInfo.InvariantCulture) + "," + row.OriginalY.ToString(
					CultureInfo.InvariantCulture)
				&& body.GetIntProperty(WandersReceiptProperty) == (row.Wanders ? 1 : 0)
				&& body.GetIntProperty(RandomReceiptProperty) == (row.WandersRandomly ? 1 : 0)
				&& body.GetIntProperty(StayingReceiptProperty) == (row.Staying ? 1 : 0);
		}

		private static bool MarkRestored(KingdomCityBook book, string eventId,
			int participantIndex, bool fixture, long nowTick)
		{
			return TryReadRaw(book, out KingdomHappeningLifecycleBook lifecycle)
				&& KingdomHappeningLifecycleRules.TryMarkRestored(lifecycle, eventId,
					participantIndex, fixture, nowTick,
					out KingdomHappeningLifecycleBook changed,
					out KingdomHappeningLifecycleFault fault) && Write(book, changed);
		}

		private static bool BodyScheduleRestored(GameObject body,
			KingdomHappeningParticipant row)
		{
			if (!GameObject.Validate(body) || body.Brain == null || IsStaged(body)
				|| PostReceipt(body) != PostReceipt(row)
				|| body.Brain.Wanders != row.Wanders
				|| body.Brain.WandersRandomly != row.WandersRandomly
				|| body.Brain.Staying != row.Staying) return false;
			string anchor = body.Brain.StartingCell == null ? ""
				: body.Brain.StartingCell.ToString();
			return anchor == row.Anchor;
		}

		private static void ClearBodyProjection(GameObject body)
		{
			body.RemoveStringProperty(TokenProperty);
			body.RemoveStringProperty(PostReceiptProperty);
			body.RemoveStringProperty(AnchorReceiptProperty);
			body.RemoveStringProperty(HomeReceiptProperty);
			body.RemoveStringProperty(TargetReceiptProperty);
			body.RemoveStringProperty(FixtureReceiptProperty);
			body.RemoveStringProperty(OriginalReceiptProperty);
			body.RemoveIntProperty(WandersReceiptProperty);
			body.RemoveIntProperty(RandomReceiptProperty);
			body.RemoveIntProperty(StayingReceiptProperty);
		}

		private static bool StandFromWeddingFixture(GameObject body,
			KingdomHappeningOperation operation)
		{
			Sitting sitting = body.GetEffect<Sitting>();
			if (sitting == null) return true;
			GameObject fixture = sitting.SittingOn;
			if (!GameObject.Validate(fixture)
				|| fixture.IDIfAssigned != operation.FixtureObjectId)
				return body.RemoveEffect(sitting);
			Chair chair = fixture.GetPart<Chair>();
			return chair != null && chair.StandUp(body, S: sitting);
		}

		private static bool HasOwnedGoal(GameObject body, string eventId)
		{
			if (body?.Brain?.Goals?.Items == null) return false;
			for (int i = 0; i < body.Brain.Goals.Items.Count; i++)
				if (body.Brain.Goals.Items[i] is KingdomHappeningMoveTo move
					&& move.HappeningEventId == eventId) return true;
			return false;
		}

		private static bool ParticipantGone(KingdomSystem system, KingdomCityBook book,
			KingdomHappeningParticipant row)
		{
			if (system?.Bindings == null || !system.Bindings.TryRead(
				out KingdomBindingTable bindings, out KingdomCityFault bindingFault)
				|| bindings.TryGet(row.ResidentId, KingdomBindingKind.Resident,
					out KingdomBinding ignoredBinding)
				|| book == null || !book.TryRead(out KingdomCityState state,
					out KingdomCityFault cityFault)) return false;
			if (!state.TryResidentIndex(row.ResidentId, out int index)) return true;
			return state.TryResident(index, out KingdomResidentRow resident)
				&& resident.Standing == KingdomResidentStanding.Dead;
		}

		private static void ReconcileZoneProjections(Zone zone, string settlementId,
			string activeEventId)
		{
			if (zone == null || string.IsNullOrEmpty(settlementId)) return;
			string prefix = "taf:happening:" + settlementId + ":";
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
			{
				string token = item.GetStringProperty(TokenProperty);
				if (!string.IsNullOrEmpty(token) && token != activeEventId
					&& token.StartsWith(prefix, StringComparison.Ordinal))
					RestoreStaleBodyProjection(item, token);
				string fixture = item.GetStringProperty(FixtureTokenProperty);
				if (!string.IsNullOrEmpty(fixture) && fixture != activeEventId
					&& fixture.StartsWith(prefix, StringComparison.Ordinal))
				{
					item.RemoveStringProperty(FixtureTokenProperty);
					string use = item.GetStringProperty(FixtureUseProperty);
					if (use == fixture || use == fixture + ":attempt")
						item.RemoveStringProperty(FixtureUseProperty);
				}
			}
		}

		private static void RestoreStaleBodyProjection(GameObject body, string token)
		{
			if (!GameObject.Validate(body) || body.Brain == null) return;
			RemoveOwnedGoal(body, token);
			RemoveOwnedGoal(body, token + ":restore");
			Sitting sitting = body.GetEffect<Sitting>();
			if (sitting != null && GameObject.Validate(sitting.SittingOn)
				&& sitting.SittingOn.GetStringProperty(FixtureTokenProperty) == token)
			{
				Chair chair = sitting.SittingOn.GetPart<Chair>();
				if (chair != null) chair.StandUp(body, S: sitting);
			}
			string post = body.GetStringProperty(PostReceiptProperty);
			int slash = string.IsNullOrEmpty(post) ? -1 : post.IndexOf('/');
			if (slash > 0
				&& int.TryParse(post.Substring(0, slash), NumberStyles.Integer,
					CultureInfo.InvariantCulture, out int workId)
				&& int.TryParse(post.Substring(slash + 1), NumberStyles.Integer,
					CultureInfo.InvariantCulture, out int kind)
				&& workId >= 0 && kind >= byte.MinValue && kind <= byte.MaxValue
				&& Enum.IsDefined(typeof(KingdomWorkKind), (KingdomWorkKind)kind))
				KingdomStations.Post(body, workId, (KingdomWorkKind)kind);
			body.Brain.Wanders = body.GetIntProperty(WandersReceiptProperty) == 1;
			body.Brain.WandersRandomly = body.GetIntProperty(RandomReceiptProperty) == 1;
			body.Brain.Staying = body.GetIntProperty(StayingReceiptProperty) == 1;
			string anchor = body.GetStringProperty(AnchorReceiptProperty) ?? "";
			try
			{
				body.Brain.StartingCell = string.IsNullOrEmpty(anchor)
					? null : new GlobalLocation(anchor);
			}
			catch { }
			ClearBodyProjection(body);
		}

		private static void RemoveOwnedGoal(GameObject body, string eventId)
		{
			if (body?.Brain?.Goals?.Items == null) return;
			for (int i = body.Brain.Goals.Items.Count - 1; i >= 0; i--)
			{
				if (!(body.Brain.Goals.Items[i] is KingdomHappeningMoveTo move)
					|| move.HappeningEventId != eventId) continue;
				for (int j = body.Brain.Goals.Items.Count - 1; j >= i; j--)
					body.Brain.Goals.Items.RemoveAt(j);
				return;
			}
		}
	}
}
