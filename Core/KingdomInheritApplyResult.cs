using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !TAF_TESTS
using XRL;
using XRL.World;
using XRL.World.Parts;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// A coordinator-facing result. Applied and AlreadyApplied may commit the exact reservation;
	/// a site refusal may release it; a failed/partial transaction deliberately does neither.
	/// </summary>
	internal sealed class KingdomInheritApplyResult
	{
		internal readonly KingdomInheritApplyStatus Status;

		internal readonly KingdomInheritApplyFault Fault;

		internal readonly string Detail;

		internal readonly string ApplicationMarker;

		internal readonly int PlacedCount;

		internal readonly bool FreshEmptyVerified;

		internal bool ShouldCommit
		{
			get
			{
				return Status == KingdomInheritApplyStatus.Applied
					|| Status == KingdomInheritApplyStatus.AlreadyApplied;
			}
		}

		internal bool ShouldRelease
		{
			get { return Status == KingdomInheritApplyStatus.Refused; }
		}

		internal KingdomInheritApplyResult(KingdomInheritApplyStatus Status,
			KingdomInheritApplyFault Fault, string Detail, string ApplicationMarker,
			int PlacedCount, bool FreshEmptyVerified)
		{
			this.Status = Status;
			this.Fault = Fault;
			this.Detail = Detail ?? "";
			this.ApplicationMarker = ApplicationMarker ?? "";
			this.PlacedCount = PlacedCount;
			this.FreshEmptyVerified = FreshEmptyVerified;
		}
	}

}
