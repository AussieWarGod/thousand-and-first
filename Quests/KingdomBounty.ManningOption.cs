using System;
using XRL;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		private const string ManningGlobalOptionPrefix = "r_TAF_BountyManningOption_v1:";

		/// <summary>Cheap realm-wide epoch observation. EndTurn calls this before the master
		/// guard, so a complete Bounty off/on span cannot disappear while every city is unloaded.</summary>
		internal static void ObserveManningGlobalOption(KingdomSystem System, long Now)
		{
			ObserveManningOption(System, Now);
		}

		private static KingdomElapsedOptionDecision ObserveManningOption(KingdomSystem System,
			long Now)
		{
			if (System == null || The.Game == null || Now < 0L
				|| !KingdomIdentityRules.IsRealmId(System.CurrentRealmId))
				return new KingdomElapsedOptionDecision(false,
					KingdomElapsedOptionRecord.Unobserved,
					KingdomElapsedOptionTransition.None, KingdomElapsedOptionAction.Invalid);
			string key = ManningGlobalOptionPrefix + System.CurrentRealmId;
			KingdomElapsedOptionRecord prior;
			if (!KingdomElapsedOptionRules.TryDecode(The.Game.GetStringGameState(key, ""),
				out prior))
				return new KingdomElapsedOptionDecision(false, prior,
					KingdomElapsedOptionTransition.None, KingdomElapsedOptionAction.Invalid);
			KingdomElapsedOptionDecision decision = KingdomElapsedOptionRules.Observe(prior,
				Enabled, System.MasterAppliedResumeToken, Now);
			if (decision.Valid && decision.Transition != KingdomElapsedOptionTransition.None)
			{
				string encoded = KingdomElapsedOptionRules.Encode(decision.Record);
				if (encoded == null) return new KingdomElapsedOptionDecision(false, prior,
					KingdomElapsedOptionTransition.None, KingdomElapsedOptionAction.Invalid);
				The.Game.SetStringGameState(key, encoded);
			}
			return decision;
		}

		private static bool ApplyManningOption(r_KingdomNotice Data, long Now,
			KingdomElapsedOptionDecision Global)
		{
			if (!Global.Valid)
			{
				Quarantine(Data, "The realm-wide manning option epoch is malformed or regressed.");
				return false;
			}
			KingdomElapsedOptionRecord prior;
			bool decoded = KingdomElapsedOptionRules.TryDecode(Data.ManningOptionRecord, out prior);
			if (!decoded)
			{
				Quarantine(Data, "The manning notice's option epoch is malformed.");
				return false;
			}
			KingdomElapsedOptionRecord current = Global.Record;
			bool exact = prior.State == current.State && prior.ObservedTick == current.ObservedTick
				&& prior.MasterResumeToken == current.MasterResumeToken;
			if (!exact || current.State != KingdomElapsedOptionState.Enabled
				|| current.ObservedTick == Now)
			{
				string encoded = KingdomElapsedOptionRules.Encode(current);
				if (encoded == null)
				{
					Quarantine(Data, "The realm-wide manning option epoch could not be encoded.");
					return false;
				}
				Data.ManningAssigned = false;
				Data.ManningCheckpointTick = Now;
				Data.DueTick = 0L;
				Data.ManningOptionRecord = encoded;
				return false;
			}
			return true;
		}

		private static bool BindManningOption(KingdomSystem System, r_KingdomNotice Data,
			long Now)
		{
			KingdomElapsedOptionDecision option = ObserveManningOption(System, Now);
			if (!option.Valid || option.Record.State != KingdomElapsedOptionState.Enabled)
				return false;
			Data.ManningOptionRecord = KingdomElapsedOptionRules.Encode(option.Record);
			return Data.ManningOptionRecord != null;
		}
	}
}
