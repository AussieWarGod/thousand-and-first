using System;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently.
namespace XRL.World.Parts
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

		/// <summary>
		/// The one blueprint the settlement dedicates to its own food stores on completion.
		/// Named here rather than inferred, so a future container-bearing building does not
		/// quietly become a pantry.
		/// </summary>
		public const string LarderBlueprint = "r_KingdomLarder";

		public void Complete()
		{
			Cell cell = ParentObject.CurrentCell;
			string blueprint = TargetBlueprint;
			string displayName = TargetDisplayName ?? "structure";
			int defence = ParentObject.GetIntProperty("KingdomDefencePending");
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
			else if (blueprint == LarderBlueprint)
			{
				// A civic larder the settlement paid for is the settlement's, the same way a
				// commissioned cask rack is. Keyed on the blueprint rather than "has an
				// Inventory and no LiquidVolume", because the charging post carries a
				// Container/Inventory pair too and is not a pantry.
				gameObject.SetIntProperty("KingdomLarder", 1);
			}
			gameObject.SetIntProperty("KingdomBuilt", 1);
			if (defence > 0)
			{
				gameObject.SetIntProperty("KingdomDefence", defence);
			}
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
				system.RecordDeed("the " + displayName + " raised at " + system.KingdomDisplayName);
				KingdomChronicle.Record(system, "the " + displayName + " was raised at " + system.KingdomDisplayName);
			}
			MessageQueue.AddPlayerMessage("{{G|The " + displayName + " is complete.}}");
		}
	}
}
