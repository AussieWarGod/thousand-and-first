using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		private sealed class CleanupFrame
		{
			internal GameObject Notice;
			internal string NoticeId;
			internal r_KingdomNotice Data;
			internal Zone Zone;
			internal Cell Cell;
		}

		private static bool TryCaptureCleanup(GameObject Notice, r_KingdomNotice Data,
			out CleanupFrame Frame)
		{
			Frame = null;
			if (!GameObject.Validate(Notice) || string.IsNullOrEmpty(Notice.IDIfAssigned) || Data == null
				|| Data.ParentObject != Notice
				|| !ReferenceEquals(Notice.GetPart<r_KingdomNotice>(), Data)) return false;
			Zone zone = Notice.CurrentZone;
			Cell cell = Notice.CurrentCell;
			if ((zone == null) != (cell == null)
				|| (cell != null && cell.ParentZone != zone)) return false;
			Frame = new CleanupFrame
			{
				Notice = Notice,
				NoticeId = Notice.IDIfAssigned,
				Data = Data,
				Zone = zone,
				Cell = cell
			};
			return true;
		}

		private static bool CleanupFinalized(CleanupFrame Frame)
		{
			if (Frame == null || GameObject.Validate(Frame.Notice)) return false;
			GameObject sameId = GameObject.FindByID(Frame.NoticeId);
			return !GameObject.Validate(sameId);
		}

		/// <summary>The only destructive bounty call site. Attempting recovery never enters it.</summary>
		private static bool InvokeCleanupOnce(GameObject Target, bool Silent)
		{
			if (Target == null || !GameObject.Validate(Target)) return true;
			Zone zone = Target.CurrentZone;
			try { return Target.Obliterate(null, Silent); }
			finally { KingdomSurvey.ObserveCurrentTopologyInActive(zone, Target); }
		}
	}
}
