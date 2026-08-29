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
	internal enum KingdomPhysicalQueueResult : byte
	{
		Refused = 0,
		Pending = 1,
		Unattended = 2,
		AttendedReady = 3,
		Busy = 4,
		AlreadyCompleted = 5
	}

	[Serializable]
	internal sealed class KingdomHappeningMoveTo : MoveTo
	{
		public string HappeningEventId;

		public KingdomHappeningMoveTo()
		{
		}

		internal KingdomHappeningMoveTo(string eventId, Cell target)
			: base(target, careful: true)
		{
			HappeningEventId = eventId ?? "";
		}
	}

	/// <summary>
	/// Projects one city-owned happening onto exact resident bodies and an authored functional
	/// fixture. The city-book sidecar is authority; body and fixture properties are recoverable
	/// projections. Movement is exclusively vanilla <c>MoveTo</c>/anchor pathing.
	/// </summary>
	internal static partial class KingdomPhysicalHappenings
	{
		internal const string TokenProperty = "r_TAF_HappeningToken";
		internal const string PostReceiptProperty = "r_TAF_HappeningPostBefore";
		internal const string AnchorReceiptProperty = "r_TAF_HappeningAnchorBefore";
		internal const string HomeReceiptProperty = "r_TAF_HappeningHomeBefore";
		internal const string TargetReceiptProperty = "r_TAF_HappeningTarget";
		internal const string FixtureReceiptProperty = "r_TAF_HappeningFixture";
		internal const string OriginalReceiptProperty = "r_TAF_HappeningOriginal";
		internal const string WandersReceiptProperty = "r_TAF_HappeningWandersBefore";
		internal const string RandomReceiptProperty = "r_TAF_HappeningRandomBefore";
		internal const string StayingReceiptProperty = "r_TAF_HappeningStayingBefore";
		internal const string FixtureTokenProperty = "r_TAF_HappeningLocusToken";
		internal const string FixtureUseProperty = "r_TAF_HappeningUseToken";

		private const int MaxPathSteps = 256;

		internal static bool IsStaged(GameObject body)
		{
			return GameObject.Validate(body)
				&& !string.IsNullOrEmpty(body.GetStringProperty(TokenProperty));
		}

		internal static string EventId(string settlementId, KingdomPhysicalHappeningKind kind,
			long eventTick, int subjectA, int subjectB, int outcome)
		{
			return "taf:happening:" + (settlementId ?? "") + ":"
				+ ((int)kind).ToString(CultureInfo.InvariantCulture) + ":"
				+ eventTick.ToString(CultureInfo.InvariantCulture) + ":"
				+ subjectA.ToString(CultureInfo.InvariantCulture) + ":"
				+ subjectB.ToString(CultureInfo.InvariantCulture) + ":"
				+ outcome.ToString(CultureInfo.InvariantCulture);
		}

		internal static bool AlreadyCompleted(KingdomCityBook book,
			KingdomPhysicalHappeningKind kind, int subjectA, int subjectB, long nowTick)
		{
			return TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)
				&& KingdomHappeningLifecycleRules.AlreadyCompleted(lifecycle, kind, subjectA,
					subjectB);
		}

		internal static KingdomPhysicalQueueResult QueueGeneric(KingdomSystem system,
			KingdomCityBook book, KingdomPhysicalHappeningKind kind, long eventTick,
			int subjectA, int subjectB, int outcome, Zone zone, int[] requiredResidents,
			string chronicleAttended, string chronicleUnattended, string ledgerAttended,
			string ledgerUnattended, string messageAttended, string messageUnattended,
			string effect, string displayName, long nowTick)
		{
			return Queue(system, book, kind, eventTick, subjectA, subjectB, outcome, zone,
				requiredResidents, false, false, chronicleAttended, chronicleUnattended,
				ledgerAttended, ledgerUnattended, messageAttended, messageUnattended, effect,
				displayName, "", nowTick, out string[] ignoredNames);
		}

		internal static KingdomPhysicalQueueResult QueueRaising(KingdomSystem system,
			KingdomCityBook book, string constructionId, long eventTick, Zone zone,
			string displayName, string planQuote, long nowTick, out string eventId,
			out string[] names)
		{
			eventId = EventId(book == null ? "" : book.SettlementId,
				KingdomPhysicalHappeningKind.Raising, eventTick,
				KingdomCityRules.StableId(constructionId ?? ""), 0, 0);
			return Queue(system, book, KingdomPhysicalHappeningKind.Raising, eventTick,
				KingdomCityRules.StableId(constructionId ?? ""), 0, 0, zone, null, true, true,
				"", "", "", "", "", "", "", displayName, planQuote, nowTick,
				out names, eventId);
		}

		internal static int Drive(KingdomSystem system, KingdomCityBook book, string label,
			bool here, long nowTick, int pushBudget)
		{
			// D8 has a second, authenticated semantic owner. Only that owner may decide
			// whether its option epoch is current before staging or advancing residents.
			if (CommunalRiteActive(book)) return 0;
			KingdomPhysicalQueueResult result = DriveCore(system, book, label, here, nowTick,
				pushBudget < 0 ? 0 : pushBudget, out int pushed);
			return result == KingdomPhysicalQueueResult.Refused ? 0 : pushed;
		}

		internal static bool AcknowledgeRaising(KingdomSystem system, KingdomCityBook book,
			string eventId, long nowTick)
		{
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)) return false;
			if (lifecycle.Active == null) return true;
			if (!string.Equals(lifecycle.Active.EventId, eventId, StringComparison.Ordinal)
				|| !lifecycle.Active.ExternalSemantic) return false;
			if (lifecycle.Active.Phase == KingdomHappeningLifecyclePhase.Restoring)
			{
				DriveCore(system, book, system.SeatName, StandsIn(lifecycle.Active.ZoneId), nowTick,
					0, out int ignoredRestore);
				return TryRead(book, nowTick, out lifecycle) && lifecycle.Active == null;
			}
			if (lifecycle.Active.Phase != KingdomHappeningLifecyclePhase.Ready
				|| !lifecycle.Active.Attended
				|| nowTick - lifecycle.Active.UpdatedTick
					>= KingdomHappeningLifecycleRules.ExternalReadyTimeoutTicks) return false;
			if (!SetPhase(book, lifecycle, KingdomHappeningLifecyclePhase.Ready,
				KingdomHappeningLifecyclePhase.Restoring, true, 0L, nowTick)) return false;
			DriveCore(system, book, system.SeatName, StandsIn(lifecycle.Active.ZoneId), nowTick,
				0, out int ignored);
			if (!TryRead(book, nowTick, out lifecycle)) return false;
			return lifecycle.Active == null;
		}

		internal static bool ReconcileSettledRaising(KingdomSystem system, KingdomCityBook book,
			string constructionId, long nowTick)
		{
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)) return false;
			if (lifecycle.Active == null) return true;
			KingdomHappeningOperation operation = lifecycle.Active;
			if (operation.Kind != KingdomPhysicalHappeningKind.Raising
				|| operation.SubjectA != KingdomCityRules.StableId(constructionId ?? "")
				|| !operation.ExternalSemantic) return true;
			return AcknowledgeRaising(system, book, operation.EventId, nowTick);
		}

		internal static bool TryReadyRaising(KingdomCityBook book, string constructionId,
			long nowTick, out string eventId, out string[] names)
		{
			eventId = null;
			names = new string[0];
			if (string.IsNullOrEmpty(constructionId)
				|| !TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)
				|| lifecycle.Active == null) return false;
			KingdomHappeningOperation operation = lifecycle.Active;
			if (operation.Kind != KingdomPhysicalHappeningKind.Raising
				|| operation.SubjectA != KingdomCityRules.StableId(constructionId)
				|| !operation.Physical || !operation.ExternalSemantic || !operation.Attended
				|| operation.Phase != KingdomHappeningLifecyclePhase.Ready
				|| nowTick - operation.UpdatedTick
					>= KingdomHappeningLifecycleRules.ExternalReadyTimeoutTicks) return false;
			eventId = operation.EventId;
			names = Names(operation);
			return true;
		}

		internal static bool ExactTold(KingdomCityState state,
			KingdomPhysicalHappeningKind kind, long tick, int subjectA, int subjectB,
			string zoneId, int outcome)
		{
			KingdomToldKind toldKind = ToldKind(kind);
			if (state == null || toldKind == KingdomToldKind.None) return false;
			for (int i = 0; i < state.ToldCount; i++)
			{
				if (state.TryTold(i, out KingdomToldRow row) && row.Kind == toldKind
					&& row.Tick == tick && row.SubjectA == subjectA && row.SubjectB == subjectB
					&& row.Outcome == outcome && string.Equals(row.PlaceZoneId ?? "",
						zoneId ?? "", StringComparison.Ordinal)) return true;
			}
			return false;
		}
	}
}
