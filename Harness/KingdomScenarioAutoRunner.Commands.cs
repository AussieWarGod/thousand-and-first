using System;
using System.Diagnostics;
using XRL;
using XRL.World;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst
{
	public sealed partial class KingdomScenarioAutoRunner
	{
		public override bool HandleEvent(CommandEvent E)
		{
			if (E == null || !KingdomCampHeartChainScript.UnexpectedCampCommand(Verbs,
				KingdomScenarioAdvance.Pending, ReferenceEquals(E.Actor, The.Player), E.Command))
				return base.HandleEvent(E);
			try
			{
				string caller = new StackTrace(1, false).ToString().Replace("\r", " ").Replace("\n", " ");
				KingdomLog.Log("scenario camp command origin: " + caller);
				string context = "taf-chain-unexpected-camp-command; actor=" + E.Actor.IDIfAssigned
					+ "; tick=" + The.Game.TimeTicks + "; command=" + E.Command
					+ "; direction-picker-not-entered=true; caller=" + caller;
				Finish(StoppedRow, false, KingdomScenarioRules.Bounded(context));
			}
			catch (Exception error)
			{
				KingdomScenarioAdvance.Cancel();
				KingdomLog.Log("scenario unexpected camp command guard: " + error.Message);
			}
			return false;
		}
	}
}
