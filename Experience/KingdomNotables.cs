using XRL.Names;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement's office holder, named the way Qud names the people it means you to
	/// remember. BUILDING-CATALOGUE-BRIEF Addendum 13 lane 5: <i>"legendary notables &mdash;
	/// office holders minted through the engine's village-hero machinery: names, epithets,
	/// fame."</i>
	/// <para>
	/// <b>The survey, and the ruling it forced.</b> The engine's hero machinery is
	/// <c>XRL.World.HeroMaker</c> (<c>D/XRL/World/HeroMaker.cs:8</c>), and
	/// <c>MakeHero</c> (<c>:17</c>, <c>:347</c>) is genuinely cheap to call &mdash; no zone, no
	/// cell, no <c>Render</c>, no worldgen, no history object; vanilla itself calls it on detached
	/// objects (<c>D/XRL/Annals/PopulationInflux.cs:78</c>). <b>What it is not is free.</b>
	/// Unconditionally it adds +1 to all six stats, doubles hit points, multiplies level by 1.5
	/// and resets XP, rolls <b>zero to four random mutations</b>, and replaces the creature's
	/// <c>GivesRep</c> part &mdash; which then rolls <c>1d3</c> random loved and hated factions
	/// for it (<c>D/XRL/World/Parts/GivesRep.cs:271-299</c>). Every one of those is worldgen
	/// shaped: they exist to make a village's mayor a creature an adventurer might have to fight.
	/// <b>The founder's water-keeper is not that.</b> Turning a settler the player housed and fed
	/// into a doubled-hit-point mutant with faction grudges the realm never chose would be a
	/// mechanic, and Addendum 13 lane 5 asks for a name.
	/// </para>
	/// <para>
	/// <b>So this takes the narrowest viable slice, and it is still the engine's own machinery.</b>
	/// <c>HeroMaker</c>'s naming block (<c>D/XRL/World/HeroMaker.cs:182-230</c>) is exactly
	/// <c>NameMaker.MakeHonorific</c> / <c>MakeEpithet</c> followed by the <c>Honorifics</c> and
	/// <c>Epithets</c> parts, and that block is what is called here. <c>NameMaker</c>
	/// (<c>D/XRL/Names/NameMaker.cs:24, 29</c>) is a pure static over <c>B/Naming.xml</c>: it
	/// mutates nothing, needs no game, and is mod-extensible by the same <c>Special</c> key
	/// vanilla threads through every one of its own offices. The office holder therefore gets the
	/// same grammar a village mayor gets, out of the same file, and none of the combat statistics.
	/// </para>
	/// </summary>
	public static class KingdomNotables
	{
		/// <summary>
		/// The <c>Special</c> scope the office's names are drawn under.
		/// <para>
		/// Vanilla's own, deliberately: <c>Naming.xml</c> keys its office grammars on
		/// <c>Special="Mayor"</c>, <c>"Warden"</c>, <c>"King"</c> and the rest
		/// (<c>B/Naming.xml:4207-4420</c>), and a settlement's one office is the same kind of
		/// thing a village's mayor is. Borrowing the scope rather than declaring a new one means
		/// the names read as Qud's, and it means a mod that extends <c>Mayor</c> extends ours.
		/// </para>
		/// </summary>
		public const string OfficeNameScope = "Mayor";

		/// <summary>String property marking a settler already minted, so a later pass never
		/// re-rolls a name the founder has already read.</summary>
		public const string EpithetProperty = "KingdomEpithet";

		/// <summary>Where an epithet sorts among a creature's others. <c>HeroMaker</c>'s own
		/// number for a hero's primary epithet (<c>D/XRL/World/Parts/Epithets.cs:14</c>).</summary>
		private const int PrimaryOrder = -40;

		/// <summary>Where an honorific sorts. <c>Honorifics</c>' ordinary order.</summary>
		private const int HonorificOrder = 40;

		/// <summary>
		/// Gives the settlement's office holder a name of the kind Qud gives the people it wants
		/// remembered, once.
		/// <para>
		/// Idempotent by the property, and total over failure: <c>NameMaker</c> answers null or
		/// empty when <c>Naming.xml</c> has no style that fits, and an office holder with no
		/// epithet is simply an office holder, which is what every one of them was before this
		/// existed.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. The epithet is remembered on its city book so a word
		/// line can name the holder while the body is on disk.</param>
		/// <param name="Holder">The settler who now holds the office.</param>
		/// <returns>The epithet, or empty.</returns>
		public static string Mint(KingdomSystem System, GameObject Holder)
		{
			if (Holder == null)
			{
				return "";
			}
			string already = Holder.GetStringProperty(EpithetProperty);
			if (!string.IsNullOrEmpty(already))
			{
				Remember(System, already);
				return already;
			}
			string honorific = NameMaker.MakeHonorific(Holder, Special: OfficeNameScope, SpecialFaildown: true);
			string epithet = NameMaker.MakeEpithet(Holder, Special: OfficeNameScope, SpecialFaildown: true,
				HasHonorific: !string.IsNullOrEmpty(honorific));
			if (!string.IsNullOrEmpty(honorific))
			{
				Holder.RequirePart<Honorifics>().AddHonorific(honorific, HonorificOrder);
			}
			if (string.IsNullOrEmpty(epithet))
			{
				return "";
			}
			Holder.RequirePart<Epithets>().AddEpithet(epithet, PrimaryOrder);
			Holder.SetStringProperty(EpithetProperty, epithet);
			Remember(System, epithet);
			KingdomLog.Log("notable: " + Holder.GetStringProperty("KingdomName") + " epithet=\"" + epithet
				+ "\" honorific=\"" + (honorific ?? "") + "\"");
			return epithet;
		}

		/// <summary>
		/// The office holder as a happening or a word line should name them: their name, and the
		/// epithet the city knows them by when there is one.
		/// <para>
		/// Read off the city book rather than off the body, so a funeral told while the holder is
		/// two zones away and on disk still names them properly &mdash; which is the whole reason
		/// the epithet is remembered at all.
		/// </para>
		/// </summary>
		public static string HolderName(KingdomSystem System)
		{
			if (System == null || string.IsNullOrEmpty(System.OfficeHolderName))
			{
				return "";
			}
			string epithet = (System.City == null) ? null : System.City.OfficeEpithet;
			return string.IsNullOrEmpty(epithet) ? System.OfficeHolderName : (System.OfficeHolderName + " " + epithet);
		}

		private static void Remember(KingdomSystem System, string Epithet)
		{
			if (System != null && System.City != null)
			{
				System.City.OfficeEpithet = Epithet ?? "";
			}
		}
	}
}
