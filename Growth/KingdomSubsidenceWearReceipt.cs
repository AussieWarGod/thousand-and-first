namespace ThousandAndFirst
{
	internal enum KingdomSubsidenceReleasePhase : byte { Pending, Intent, Released }

	internal sealed class KingdomSubsidenceWearReceipt
	{
		internal readonly int Phase, Cause, BeforeWear, AfterWear, Wear, LastCause, MessageState;
		internal readonly string Id, LastCompletedId, Line;

		internal KingdomSubsidenceWearReceipt(int phase, string id, int cause, int beforeWear,
			int afterWear, int wear, int lastCause, string lastCompletedId, string line, int messageState)
		{
			Phase = phase; Id = id; Cause = cause; BeforeWear = beforeWear; AfterWear = afterWear;
			Wear = wear; LastCause = lastCause; LastCompletedId = lastCompletedId;
			Line = line; MessageState = messageState;
		}
	}
}
