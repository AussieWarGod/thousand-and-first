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
		/// <param name="Accomplishment">True to also file a journal accomplishment. Reserve this
		/// for milestones; ordinary events would spam the journal.</param>
		/// <param name="MuralText">
		/// Authored third-person line for the player's end-of-game murals, or null &mdash; which
		/// is what almost everything should pass.
		/// <para>
		/// Mural space is scarce and shared with the player's own life.
		/// <c>PlayerMuralController.initializeMurals</c> keeps at most sixteen accomplishments
		/// with non-empty mural text, and vanilla's Coda then draws its gospel from the first ten
		/// of those. A settlement that files a mural for every trade charter and repelled raid
		/// would push the player's actual history out of their own murals.
		/// </para>
		/// <para>
		/// Passing null is <b>not</b> enough on its own to stay out: `AddAccomplishment` derives
		/// mural text from the accomplishment text whenever mural text is null and the weight is
		/// not <c>Nil</c>, and silently overrides the category to <c>DoesSomethingRad</c> while
		/// doing it. Staying out requires the weight, which is why the two travel together here.
		/// </para>
		/// </param>
		public static void Record(KingdomSystem System, string Text, bool Accomplishment = false, string MuralText = null)
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
				bool wantsMural = !string.IsNullOrEmpty(MuralText);
				JournalAPI.AddAccomplishment(Text.Capitalize() + ".", wantsMural ? MuralText : null, null, null, "general", MuralCategory.CreatesSomething, wantsMural ? MuralWeight.Medium : MuralWeight.Nil, null, -1L);
			}
		}
	}
}
