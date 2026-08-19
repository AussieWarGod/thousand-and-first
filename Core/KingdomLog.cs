using XRL.UI;

namespace ThousandAndFirst
{
	public static class KingdomLog
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionDevLog") != "No";

		public static void Log(string Text)
		{
			if (Enabled)
			{
				UnityEngine.Debug.Log("[TAF] " + Text);
			}
		}
	}
}
