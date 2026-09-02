namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>
		/// The founding heart's refusals, named in the log. The founder is told the outcome ("first
		/// founding remains recoverable"); this line tells the log which step refused, so a refusal
		/// on strange ground is read rather than guessed at. Reads as false so a refusing branch
		/// returns it directly.
		/// </summary>
		private static bool HeartRefused(string Step)
		{
			KingdomLog.Log("founding heart refused: " + Step);
			return false;
		}

		/// <summary>Inside a refusal chain: notes the cause and reads as true, so
		/// <c>|| (!TryX(out failure) &amp;&amp; HeartNoted(failure))</c> refuses with its cause kept.</summary>
		/// <summary>The same refusal for a path that answers with an object: reads as null.</summary>
		private static XRL.World.GameObject HeartRefusedNull(string Step)
		{
			KingdomLog.Log("founding heart refused: " + Step);
			return null;
		}

		private static bool HeartNoted(string Reason)
		{
			KingdomLog.Log("founding heart refused: " + Reason);
			return true;
		}
	}
}
