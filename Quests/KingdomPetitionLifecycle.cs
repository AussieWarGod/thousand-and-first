using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Only production authority for petition publication, recovery, and transitions.</summary>
	internal static partial class KingdomPetitionLifecycle
	{
		internal static bool OnSettlementPass(KingdomSystem system, Zone zone,
			KingdomSurvey survey, bool enabled, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !SeatGround(system, zone, survey) || now < 0L) return false;
			if (!AdoptLegacy(system, zone, survey, book, now)) return false;
			if (!Drive(system, book, now)) return false;
			KingdomLifecycleOptionState prior = book.PetitionOption;
			if (!ObserveOption(book, enabled, now)) return false;
			if (!enabled)
			{
				if (!ReconcileDisabled(system, book, now)) return false;
				Project(system, book.Petition);
				return true;
			}
			if (prior == KingdomLifecycleOptionState.Disabled
				&& !ResumeAccepted(system, book, now)) return false;
			if (!Drive(system, book, now)) return false;
			Check(system, zone, survey, now);
			if (KingdomPetitionRules.IsActive(Status(system))) return true;
			return Issue(system, zone, survey, enabled, now) || Status(system) != PetitionLifecycle.None;
		}

		internal static bool Issue(KingdomSystem system, Zone zone, KingdomSurvey survey,
			bool enabled, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !enabled || !SeatGround(system, zone, survey) || now < 0L)
				return false;
			if (!AdoptLegacy(system, zone, survey, book, now) || !Drive(system, book, now)
				|| !ObserveOption(book, true, now) || !CanStart(system, book, now)) return false;
			string faction = null;
			int worstStanding = 0;
			if (system.Standings != null)
				foreach (KeyValuePair<string, int> row in system.Standings)
					if (row.Value < worstStanding)
					{
						worstStanding = row.Value;
						faction = row.Key;
					}
			if (!TryPhysicalRoof(survey, out int roof, out string roofFailure))
			{
				KingdomLog.Log("petition: " + roofFailure); return false;
			}
			KingdomRules.PetitionKind kind = KingdomRules.ChoosePetition(survey.StoredWater,
				system.Population, roof, system.IdleWorks, worstStanding,
				HasShrine(survey), system.Dead);
			return kind != KingdomRules.PetitionKind.None
				&& PublishOffer(system, zone, survey, book, kind, faction, null, now, false);
		}

		internal static bool Raise(KingdomSystem system, Zone zone, KingdomSurvey survey,
			KingdomRules.PetitionKind kind, string faction, string eventId, bool enabled, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !enabled
				|| !Enum.IsDefined(typeof(KingdomRules.PetitionKind), kind)
				|| kind == KingdomRules.PetitionKind.None
				|| !SeatGround(system, zone, survey) || now < 0L
				|| (eventId != null && !KingdomPetitionRules.EventIdValid(eventId))
				|| !KingdomPetitionRules.SnapshotTextValid(faction,
					KingdomLifecycleRules.MaxNameChars, true)) return false;
			if (!AdoptLegacy(system, zone, survey, book, now) || !Drive(system, book, now)
				|| !ObserveOption(book, true, now)) return false;
			KingdomLifecycleOperation current = book.Petition;
			if (eventId != null && KingdomPetitionRules.FrozenSnapshotValid(current)
				&& string.Equals(current.ObjectMarker, eventId, StringComparison.Ordinal))
				return (KingdomRules.PetitionKind)current.Kind == kind
					&& string.Equals(current.Faction, faction, StringComparison.Ordinal)
					&& KingdomPetitionRules.IsActive(KingdomPetitionRules.LifecycleOf(current));
			return CanStart(system, book, now)
				&& PublishOffer(system, zone, survey, book, kind, faction, eventId, now, false);
		}

		internal static bool Accept(KingdomSystem system, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !Drive(system, book, now)) return false;
			KingdomLifecycleOperation source = book.Petition;
			if (!KingdomPetitionRules.FrozenSnapshotValid(source)
				|| KingdomPetitionRules.LifecycleOf(source) != PetitionLifecycle.Offered) return false;
			bool result = PublishTransition(system, book, source,
				KingdomLifecycleAction.PetitionAccept, KingdomPetitionRules.ActiveClock,
				source.DepartTick, now, "accepted");
			if (result) KingdomGovernanceScope.Commit("accept petition");
			return result;
		}

		internal static bool Decline(KingdomSystem system, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !Drive(system, book, now)) return false;
			KingdomLifecycleOperation source = book.Petition;
			return KingdomPetitionRules.LifecycleOf(source) == PetitionLifecycle.Offered
				&& PublishTransition(system, book, source,
					KingdomLifecycleAction.PetitionDecline, KingdomPetitionRules.ActiveClock,
					source.DepartTick, now, "declined");
		}

		internal static void Check(KingdomSystem system, Zone zone, KingdomSurvey survey, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !SeatGround(system, zone, survey) || !Drive(system, book, now)) return;
			KingdomLifecycleOperation op = book.Petition;
			if (!KingdomPetitionRules.FrozenSnapshotValid(op)
				|| !KingdomPetitionRules.OriginMatches(op.Origin, book.SettlementId)
				|| string.Equals(op.Creed, KingdomPetitionRules.PausedClock,
					StringComparison.Ordinal)) return;
			PetitionLifecycle state = KingdomPetitionRules.LifecycleOf(op);
			if (state == PetitionLifecycle.Accepted)
			{
				int standing = string.IsNullOrEmpty(op.Faction) ? 0 :
					system.GetRegardForRealm(op.Faction);
				KingdomRules.PetitionKind kind = (KingdomRules.PetitionKind)op.Kind;
				int roof = 0;
				string roofFailure = null;
				bool roofProved = kind != KingdomRules.PetitionKind.Shelter
					|| TryPhysicalRoof(survey, out roof, out roofFailure);
				if (!roofProved)
					KingdomLog.Log("petition: shelter evidence paused: " + roofFailure);
				if (roofProved && KingdomPetitionRules.CanResolve(state, kind,
					op.Target, survey.StoredWater, roof, system.IdleWorks, standing,
					HasShrine(survey)))
				{
					PublishTransition(system, book, op, KingdomLifecycleAction.PetitionResolve,
						KingdomPetitionRules.ActiveClock, op.DepartTick, now, "resolved");
					return;
				}
			}
			if ((state == PetitionLifecycle.Offered || state == PetitionLifecycle.Accepted)
				&& KingdomPetitionRules.IsExpired(now, op.DepartTick))
				PublishTransition(system, book, op, KingdomLifecycleAction.PetitionExpire,
					KingdomPetitionRules.ActiveClock, op.DepartTick, now, "expired");
		}

		private static bool TryPhysicalRoof(KingdomSurvey Survey, out int Roof,
			out string Failure)
		{
			Roof = 0;
			Failure = null;
			if (Survey == null)
			{
				Failure = "The settlement survey is unavailable.";
				return false;
			}
			if (!Survey.TryBenefits(out KingdomBenefitIndex benefits, out Failure)) return false;
			Roof = benefits.Total("roof");
			return true;
		}

		internal static PetitionLifecycle Status(KingdomSystem system)
		{
			KingdomLifecycleOperation op = Open(system);
			if (!KingdomPetitionRules.FrozenSnapshotValid(op)) return PetitionLifecycle.None;
			Project(system, op);
			return KingdomPetitionRules.LifecycleOf(op);
		}

		internal static KingdomLifecycleOperation Open(KingdomSystem system)
		{
			KingdomLifecycleBook book = Authority(system);
			return book?.Petition;
		}

	}
}
