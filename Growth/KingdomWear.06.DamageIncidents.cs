using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomWear
	{
		/// <summary>
		/// Raiders who got past the wall may leave one or two works worse for it. Called from
		/// <c>KingdomRaids.ExecuteRaid</c> once for a raid that actually put raiders on the
		/// ground; does nothing for one the wall turned back outright, because nothing got past
		/// it to damage anything.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the raid landed in.</param>
		/// <param name="Survey">This pass's survey, so the candidate list is exactly the works
		/// already known to be crewed here. A fresh survey is taken when null.</param>
		/// <param name="RaidersThrough">Raiders who made it past the wall this raid.</param>
		/// <param name="RaidTick">The raid's own due tick, so a reload asks each candidate work
		/// this exact question exactly once.</param>
		public static void OnRaidDamage(KingdomSystem System, Zone Z, KingdomSurvey Survey, int RaidersThrough, long RaidTick)
		{
			if (!Enabled || System == null || Z == null || RaidersThrough <= 0 || RaidTick < 0L)
			{
				return;
			}
			KingdomSurvey survey = Survey ?? KingdomSurvey.Take(Z, System);
			if (survey.Works.Count == 0)
			{
				return;
			}
			int want = KingdomWearRules.WorksToDamage(RaidersThrough);
			if (want <= 0)
			{
				return;
			}
			string settlementId = KingdomChronicle.SettlementId(System);
			int hit = 0;
			for (int i = 0; i < survey.Works.Count && hit < want; i++)
			{
				GameObject work = survey.Works[i];
				if (!GameObject.Validate(work) || !KingdomWearRules.RollRaidDamage(settlementId, work.ID, RaidTick))
				{
					continue;
				}
				long lastRaid;
				if (!TryReadStrictTick(work, LastRaidIncidentTickProperty, out lastRaid)
					|| RaidTick < lastRaid)
				{
					QuarantineWear(System, work, "Its raid-damage receipt regressed or is malformed.");
					hit++;
					continue;
				}
				if (RaidTick > lastRaid || work.GetIntProperty("KingdomWearRaidTickSet") != 1)
				{
					if (!ApplyDamageIncident(System, work, KingdomWearRules.WearCause.Raid,
						WearEventId(work, "raid", RaidTick))) return;
					KingdomMaterials.WriteTick(work, LastRaidIncidentTickProperty, RaidTick);
					work.SetIntProperty("KingdomWearRaidTickSet", 1);
				}
				hit++;
			}
		}

		private static bool ApplyDamageIncident(KingdomSystem System, GameObject Work,
			KingdomWearRules.WearCause Cause, string IncidentId)
		{
			r_KingdomWear wear = Work.RequirePart<r_KingdomWear>();
			if ((KingdomWearIncidentPhase)wear.IncidentPhase == KingdomWearIncidentPhase.None
				&& HasActiveRepair(Work, out _)) return true;
			if (wear.LifecycleQuarantined)
			{
				TellWearQuarantine(System, Work, wear);
				return false;
			}
			KingdomWearIncidentPhase phase = (KingdomWearIncidentPhase)wear.IncidentPhase;
			if (phase == KingdomWearIncidentPhase.None)
			{
				if (string.Equals(wear.LastCompletedIncidentId, IncidentId,
					StringComparison.Ordinal)) return true;
				wear.IncidentId = IncidentId;
				wear.IncidentCause = (int)Cause;
				wear.IncidentBeforeWear = wear.Wear;
				wear.IncidentAfterWear = KingdomMaterialRules.AddWear(wear.Wear,
					KingdomWearRules.IncrementFor(Cause));
				wear.IncidentLine = KingdomWearRules.DamagedLine(DisplayName(Work), Cause,
					wear.IncidentAfterWear);
				wear.IncidentMessageState = (int)KingdomWearSinkDisposition.None;
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;
				phase = KingdomWearIncidentPhase.Bound;
			}
			else if (!string.Equals(wear.IncidentId, IncidentId, StringComparison.Ordinal)
				|| wear.IncidentCause != (int)Cause)
			{
				QuarantineWear(System, Work, "Two damage incidents claim the same work.");
				return false;
			}
			if (phase == KingdomWearIncidentPhase.Bound)
			{
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.MutationIntent;
				phase = KingdomWearIncidentPhase.MutationIntent;
			}
			if (phase == KingdomWearIncidentPhase.MutationIntent)
			{
				KingdomWearMutationAction action = KingdomWearRules.DamageMutationAction(phase,
					wear.IncidentBeforeWear, wear.Wear, wear.IncidentAfterWear);
				if (action == KingdomWearMutationAction.Apply)
				{
					wear.Wear = wear.IncidentAfterWear;
					wear.LastCause = (int)Cause;
				}
				else if (action == KingdomWearMutationAction.Confirm)
				{
					wear.LastCause = (int)Cause;
				}
				else
				{
					QuarantineWear(System, Work, "A damage incident no longer matches its exact before/after state.");
					return false;
				}
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Mutated;
				phase = KingdomWearIncidentPhase.Mutated;
			}
			if (wear.IncidentBeforeWear == wear.IncidentAfterWear)
			{
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Complete;
				phase = KingdomWearIncidentPhase.Complete;
			}
			if (phase == KingdomWearIncidentPhase.Mutated)
			{
				if (!KingdomChronicle.RecordOnce(System, wear.IncidentId + ":chronicle",
					wear.IncidentLine)) return false;
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.ChronicleDone;
				phase = KingdomWearIncidentPhase.ChronicleDone;
			}
			if (phase == KingdomWearIncidentPhase.ChronicleDone)
			{
				if (wear.IncidentMessageState == (int)KingdomWearSinkDisposition.None)
					wear.IncidentMessageState = (int)KingdomWearSinkDisposition.Pending;
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.MessageIntent;
				DeliverWearMessage(ref wear.IncidentMessageState,
					"{{r|" + wear.IncidentLine + "}}");
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.MessageDone;
				phase = KingdomWearIncidentPhase.MessageDone;
			}
			else if (phase == KingdomWearIncidentPhase.MessageIntent)
			{
				if (wear.IncidentMessageState == (int)KingdomWearSinkDisposition.None)
					wear.IncidentMessageState = (int)KingdomWearSinkDisposition.Attempting;
				wear.IncidentMessageState = (int)KingdomWearRules.RecoverUninspectable(
					(KingdomWearSinkDisposition)wear.IncidentMessageState);
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.MessageDone;
				phase = KingdomWearIncidentPhase.MessageDone;
			}
			if (phase == KingdomWearIncidentPhase.MessageDone)
			{
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Complete;
				phase = KingdomWearIncidentPhase.Complete;
				KingdomLog.Log("wear: damaged " + Work.Blueprint + " cause=" + Cause
					+ " wear=" + wear.Wear + " incident=" + IncidentId);
			}
			if (phase != KingdomWearIncidentPhase.Complete) return false;
			wear.LastCompletedIncidentId = IncidentId;
			wear.IncidentPhase = (int)KingdomWearIncidentPhase.None;
			wear.IncidentId = null;
			wear.IncidentLine = null;
			return true;
		}

	}
}
