using Qud.API;
using XRL;
using XRL.Rules;
using XRL.World;

namespace ThousandAndFirst
{
	public static class KingdomChronicle
	{
		public const int MaxEntries = 200;

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
			if (Accomplishment)
			{
				JournalAPI.AddAccomplishment(Text.Capitalize() + ".", null, null, null, "general", MuralCategory.CreatesSomething, MuralWeight.Medium, null, -1L);
			}
		}
	}
}
