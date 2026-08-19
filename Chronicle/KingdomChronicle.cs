using Qud.API;
using XRL;
using XRL.Rules;
using XRL.World;

namespace ThousandAndFirst
{
	public static class KingdomChronicle
	{
		public const int MaxEntries = 200;

		/// <summary>
		/// Writes an event into the kingdom's chronicle in both registers: the official
		/// entry (dated in Qud's calendar) and an outsider retelling (third person, wrapped
		/// in rumor grammar). Both lists are capped at <see cref="MaxEntries"/>.
		/// </summary>
		/// <param name="System">The kingdom system; must be founded for names to read correctly.</param>
		/// <param name="Text">Lower-case clause with no trailing period, written from the
		/// founder's perspective, e.g. "the well ran dry" or "you poured the first water".
		/// Second-person phrasing is converted automatically for the outsider register.</param>
		/// <param name="Accomplishment">True to also file a journal accomplishment, which
		/// makes the event eligible for the player's chronicle and sultan murals. Reserve
		/// this for milestones; ordinary events would spam the journal.</param>
		public static void Record(KingdomSystem System, string Text, bool Accomplishment = false)
		{
			System.ChronicleEntries.Add("On the " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", " + Calendar.GetYear() + " AR, " + Text + ".");
			if (System.ChronicleEntries.Count > MaxEntries)
			{
				System.ChronicleEntries.RemoveAt(0);
			}
			string founder = The.Player?.BaseDisplayNameStripped ?? "the founder";
			System.OutsiderEntries.Add(KingdomRules.ComposeOutsider(KingdomRules.ToThirdPerson(Text, founder), Stat.Random(0, 35)));
			if (System.OutsiderEntries.Count > MaxEntries)
			{
				System.OutsiderEntries.RemoveAt(0);
			}
			if (Accomplishment && XRL.UI.Options.GetOption("r_TAF_OptionChronicle") != "No")
			{
				JournalAPI.AddAccomplishment(Text.Capitalize() + ".", null, null, null, "general", MuralCategory.CreatesSomething, MuralWeight.Medium, null, -1L);
			}
		}
	}
}
