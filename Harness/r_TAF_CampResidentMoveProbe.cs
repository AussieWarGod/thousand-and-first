using System;
using System.Diagnostics;
using System.Text;
using XRL.World;

namespace XRL.World.Parts
{
	[Serializable]
	public sealed class r_TAF_CampResidentMoveProbe : IPart
	{
		[NonSerialized] internal Action<string> Record;
		[NonSerialized] internal string OriginZone;
		[NonSerialized] internal int FixtureSlot;
		[NonSerialized] private bool LeftZone;
		[NonSerialized] private bool DeathObserved;
		[NonSerialized] private bool DestructionObserved;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == EnteredCellEvent.ID
				|| ID == BeforeDeathRemovalEvent.ID || ID == OnDestroyObjectEvent.ID;
		}

		public override bool HandleEvent(EnteredCellEvent E)
		{
			if (!LeftZone && Record != null && E.Cell?.ParentZone != null
				&& E.Cell.ParentZone.ZoneID != OriginZone)
			{
				LeftZone = true;
				Observe("first-zone-exit");
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			if (!DeathObserved && Record != null)
			{
				DeathObserved = true;
				Observe("before-death-removal");
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(OnDestroyObjectEvent E)
		{
			if (!DestructionObserved && Record != null)
			{
				DestructionObserved = true;
				Observe("object-destroyed");
			}
			return base.HandleEvent(E);
		}

		private void Observe(string Kind)
		{
			try
			{
				GameObject body = ParentObject;
				Brain brain = body?.Brain;
				var text = new StringBuilder("\nresident-movement kind=").Append(Kind)
					.Append("; slot=").Append(FixtureSlot).Append("; object=").Append(body?.IDIfAssigned)
					.Append("; zone=").Append(body?.CurrentZone?.ZoneID)
					.Append("; home=").Append(brain?.StartingCell?.ToString() ?? "absent")
					.Append("; hp=").Append(body?.GetStat("Hitpoints")?.Value ?? -1);
				for (int i = 0; brain != null && i < Math.Min(brain.Goals.Count, 12); i++)
					text.Append("; goal=").Append(brain.Goals.Items[i]?.GetType().FullName);
				int retained = 0;
				foreach (StackFrame frame in new StackTrace(false).GetFrames() ?? new StackFrame[0])
				{
					var method = frame.GetMethod();
					string type = method?.DeclaringType?.FullName ?? "";
					if (!type.StartsWith("XRL.World.", StringComparison.Ordinal)
						&& !type.StartsWith("ThousandAndFirst.", StringComparison.Ordinal)) continue;
					text.Append("; caller=").Append(type).Append('.').Append(method.Name);
					if (++retained == 24) break;
				}
				Record(text.ToString());
			}
			catch (Exception error)
			{
				Record("\nresident-movement observation-error=" + error.GetType().Name);
			}
		}
	}
}
