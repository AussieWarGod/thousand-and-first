using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		public static KingdomLifecycleOptionDecision ObserveOption(
			KingdomLifecycleOptionState Prior, long PriorTick, bool Enabled,
			long Now, bool HasOpenOperation)
		{
			KingdomLifecycleOptionDecision result = new KingdomLifecycleOptionDecision
			{
				Valid = false,
				Action = KingdomLifecycleOptionAction.Quarantine,
				State = Prior,
				Tick = PriorTick,
				AllowNewWork = false,
				ReconcileOpenWork = HasOpenOperation
			};
			if (!KnownOption(Prior) || PriorTick < 0L || Now < PriorTick) return result;
			result.Valid = true;
			if (!Enabled)
			{
				result.Action = Prior == KingdomLifecycleOptionState.Disabled
					? KingdomLifecycleOptionAction.StayDisabled : KingdomLifecycleOptionAction.Disable;
				result.State = KingdomLifecycleOptionState.Disabled;
				result.Tick = Prior == KingdomLifecycleOptionState.Disabled ? PriorTick : Now;
				return result;
			}
			if (Prior != KingdomLifecycleOptionState.Enabled)
			{
				result.Action = KingdomLifecycleOptionAction.EnableAndRestamp;
				result.State = KingdomLifecycleOptionState.Enabled;
				result.Tick = Now;
				return result;
			}
			result.Action = KingdomLifecycleOptionAction.None;
			result.AllowNewWork = !HasOpenOperation;
			return result;
		}

		public static bool CanStartAfterOption(KingdomLifecycleOptionDecision Decision,
			long Now, long MinimumElapsed)
		{
			if (Decision == null || !Decision.Valid || !Decision.AllowNewWork
				|| Decision.State != KingdomLifecycleOptionState.Enabled
				|| MinimumElapsed < 0L || Now < Decision.Tick) return false;
			long due;
			return CheckedAdd(Decision.Tick, MinimumElapsed, out due) && Now >= due;
		}
	}
}
