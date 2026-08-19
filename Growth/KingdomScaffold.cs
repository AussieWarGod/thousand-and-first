using System;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	[Serializable]
	public class r_KingdomScaffold : IPart
	{
		public string TargetBlueprint;

		public string TargetDisplayName;

		public long CompleteTick;

		public int StaffNeeded;

		public bool ThresholdManning;

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (TargetBlueprint != null && TimeTick >= CompleteTick)
			{
				Complete();
			}
			base.TurnTick(TimeTick, Amount);
		}

		public void Complete()
		{
			Cell cell = ParentObject.CurrentCell;
			string blueprint = TargetBlueprint;
			string displayName = TargetDisplayName ?? "structure";
			TargetBlueprint = null;
			if (cell == null)
			{
				return;
			}
			GameObject gameObject = GameObject.Create(blueprint);
			if (gameObject == null)
			{
				return;
			}
			ParentObject.Destroy(null, Silent: true);
			cell.AddObject(gameObject);
			if (gameObject.GetPart<XRL.World.Parts.LiquidVolume>() != null)
			{
				gameObject.SetIntProperty("KingdomStores", 1);
			}
			gameObject.SetIntProperty("KingdomBuilt", 1);
			if (StaffNeeded > 0)
			{
				gameObject.SetIntProperty("KingdomStaffNeeded", StaffNeeded);
				if (ThresholdManning)
				{
					gameObject.SetIntProperty("KingdomThresholdManning", 1);
				}
				if (gameObject.GetPart<XRL.World.Parts.Capacitor>() != null)
				{
					gameObject.SetIntProperty("KingdomHandCranked", 1);
				}
			}
			gameObject.MakeActive();
			KingdomLog.Log("scaffold complete: " + displayName + " (" + blueprint + ") at " + cell.X + "," + cell.Y);
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				KingdomChronicle.Record(system, "the " + displayName + " was raised at " + system.KingdomDisplayName);
			}
			MessageQueue.AddPlayerMessage("{{G|The " + displayName + " is complete.}}");
		}
	}
}
