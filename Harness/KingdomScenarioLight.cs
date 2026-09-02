using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Dev-only evidence lighting. A native capture shows what the tester can see, so a staged lot
	/// larger than a field of view photographs as one lit room in a dark rectangle. This verb lights,
	/// reveals, and marks explored the whole loaded zone so the capture shows the lot as authored.
	/// It edits no architecture, receipt, option, or production state: light and visibility are
	/// engine presentation the next turn recomputes. It is a persona verb like <c>frame</c>, and the
	/// file lives in <c>Harness/</c>, which an ordinary build never compiles.
	/// </summary>
	internal static class KingdomScenarioLight
	{
		internal const string Verb = "light";

		internal static string Run(out bool Ok)
		{
			Ok = false;
			GameObject player = The.Player;
			Zone zone = player?.CurrentZone;
			if (!GameObject.Validate(player) || zone == null)
				return "{{R|Light refused}}: light runs only with a live player in a loaded zone.";
			zone.LightAll();
			zone.VisAll();
			zone.ExploreAll();
			Ok = true;
			return "Lit, revealed, and explored " + zone.ZoneID + " for native evidence capture; "
				+ "the engine recomputes light and sight on the next turn.";
		}
	}
}
