using XRL;
using XRL.UI;
using XRL.Wish;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Attended operator surface. Reachable only when the excluded Harness tree is loaded, because
	/// wish discovery is a reflection scan over compiled types.
	/// <para>
	/// PRESENTATION ONLY. Every verb, and the journal row it writes, live in
	/// <see cref="KingdomScenarioVerbs"/>, which is also what the unattended
	/// <see cref="ThousandAndFirst.KingdomScenarioAutoRunner"/> calls. This file adds exactly one
	/// thing: the popup. Keeping the popup here is what makes the harness scriptable - a verb that
	/// blocked on a keypress could never run on a turn nobody is watching.
	/// </para>
	/// </summary>
	[HasWishCommand]
	public static class KingdomScenarioWishes
	{
		[WishCommand("kingdom:scenario", null)]
		public static void Scenario(string Parameter)
		{
			KingdomSystem.Guard("scenario harness", delegate
			{
				bool ok;
				Popup.Show(KingdomScenarioVerbs.Invoke(Parameter, out ok));
			});
		}
	}
}
