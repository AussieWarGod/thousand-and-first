using System;
using System.Diagnostics;
using System.Text;
using ThousandAndFirst.Harness;
using XRL;
using XRL.World;

namespace XRL.World.Parts
{
	[Serializable]
	public sealed class r_TAF_CampVortexOriginProbe : IPart
	{
		[NonSerialized] private string Creation;
		[NonSerialized] private string GameId;
		[NonSerialized] private string ObservedZone;
		[NonSerialized] private bool Recorded;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeObjectCreatedEvent.ID
				|| ID == EnteredCellEvent.ID;
		}

		public override bool HandleEvent(BeforeObjectCreatedEvent E)
		{
			try
			{
				ObservedZone = KingdomCampHeartNativeChecks.ObservedHazardZone;
				if (ObservedZone != null)
				{
					if (!ReferenceEquals(E.Object, ParentObject))
						throw new InvalidOperationException("vortex creation object differs");
					GameId = The.Game.GameID;
					var text = new StringBuilder("created-tick=").Append(The.Game.TimeTicks);
					GameObject actor = The.ActionManager?.Actor;
					Cell actorCell = actor?.CurrentCell;
					text.Append("; acting-blueprint=").Append(actor?.Blueprint)
						.Append("; acting-object=").Append(actor?.IDIfAssigned)
						.Append("; acting-zone=").Append(actorCell?.ParentZone?.ZoneID)
						.Append("; acting-cell=").Append(actorCell?.X).Append(',').Append(actorCell?.Y)
						.Append("; acting-has-vortex=").Append(actor?.HasPart("SpacetimeVortex") ?? false)
						.Append("; acting-target=").Append(actor?.Brain?.Target?.Blueprint);
					int count = 0;
					foreach (StackFrame frame in new StackTrace(false).GetFrames() ?? new StackFrame[0])
					{
						var method = frame.GetMethod();
						string type = method?.DeclaringType?.FullName ?? "";
						if (!type.StartsWith("XRL.", StringComparison.Ordinal)
							&& !type.StartsWith("ThousandAndFirst.", StringComparison.Ordinal)) continue;
						text.Append("; creator=").Append(type).Append('.').Append(method.Name);
						if (++count >= 32 || text.Length >= 6000) break;
					}
					Creation = text.ToString();
				}
			}
			catch (Exception error) { Fail(error); }
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EnteredCellEvent E)
		{
			try
			{
				if (!Recorded && Creation != null && The.Game?.GameID == GameId
					&& ObservedZone == KingdomCampHeartNativeChecks.ObservedHazardZone
					&& E.Cell?.ParentZone?.ZoneID == ObservedZone)
				{
					if (!ReferenceEquals(E.Object, ParentObject))
						throw new InvalidOperationException("vortex placement object differs");
					Recorded = true;
					KingdomCampHeartNativeChecks.RecordVortexOrigin(true,
						Creation + "; placed-tick=" + The.Game.TimeTicks + "; blueprint="
						+ ParentObject.Blueprint + "; object=" + ParentObject.IDIfAssigned
						+ "; zone=" + ObservedZone + "; cell=" + E.Cell.X + "," + E.Cell.Y);
				}
			}
			catch (Exception error) { Fail(error); }
			return base.HandleEvent(E);
		}

		private void Fail(Exception Error)
		{
			Recorded = true;
			KingdomCampHeartNativeChecks.RecordVortexOrigin(false,
				"observation-error=" + Error.GetType().Name);
		}
	}
}
