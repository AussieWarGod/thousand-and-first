using System;
using Qud.API;

namespace ThousandAndFirst
{
	/// <summary>
	/// Public civic memory is not the successor's personal secret. It cannot be sold or erased by
	/// the succession knowledge reset, but otherwise renders through Qud's native Sultan journal.
	/// </summary>
	[Serializable]
	public sealed class r_KingdomFounderHistoryNote : JournalSultanNote
	{
		public override bool Forgettable()
		{
			return false;
		}
	}
}
