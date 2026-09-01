namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		public class BuildEntry
		{
			public string Key;

			public string DisplayName;

			public string Blueprint;

			public int CostDrams;

			public long BuildTicks;

			public string Styles = "common";

			public string Category = "civic";

			public GrowthStage MinStage;

			public int Staff;

			public string Manning = "scaled";

			public int Defence;

			/// <summary>Explicit permission for a player-built exact room or vessel to take this
			/// catalogue role. Absence is false; authored/stateful works must never be inferred.</summary>
			public bool Adoptable;

			/// <summary>Faction key of the optional covenant that opens this design. Null means no
			/// covenant gate. The key is validated against Qud's faction registry while the merged
			/// catalogue is loaded.</summary>
			public string CovenantFaction;

			/// <summary>Kingdom standing required with <see cref="CovenantFaction"/>. Meaningful
			/// only when that field is non-null.</summary>
			public int CovenantMinStanding;

			/// <summary>
			/// Raw <c>Carries</c> attribute: what this design adds to the settlement's SUSTAINABLE
			/// LEVEL, as a comma list of <c>support:settlers</c>. Read through
			/// <see cref="KingdomCatalogueRules.TryParseTally"/>; <c>water</c>, <c>food</c> and
			/// <c>roof</c> bind and the level is the least of them, everything else lifts. Null for a
			/// design that adds nothing to what the place carries, which is correct for a wall.
			/// </summary>
			public string Carries;

			/// <summary>Raw <c>Materials</c> attribute, kept for whole-file validation. The cost
			/// itself is parsed and held by <c>KingdomMaterials</c>.</summary>
			public string Materials;

			public string ShortName;

			/// <summary>
			/// Appearances this design was authored with, from its <c>&lt;skin&gt;</c> child
			/// elements, in the order the file declares them. Null &mdash; never an empty list
			/// &mdash; for a design that declares none, which is every design written before skins
			/// existed and every one that simply does not want them.
			/// <para>
			/// Read through <see cref="KingdomDesignRules"/>; nothing here validates a skin, because
			/// the same entry may be re-declared by a third-party file and the last declaration owns
			/// its whole skin list.
			/// </para>
			/// </summary>
			public System.Collections.Generic.List<KingdomDesignRules.SkinEntry> Skins;

			public string Name => ShortName ?? DisplayName;
		}

		/// <summary>
		/// Appends one parsed <c>&lt;skin&gt;</c> to a design, refusing a key the design already
		/// carries rather than letting the later one shadow the earlier at pick time.
		/// </summary>
		/// <param name="Entry">The design being built up. Null is refused.</param>
		/// <param name="Skin">A skin from <c>KingdomDesignRules.TryParseSkinAttributes</c>.</param>
		/// <param name="Error">Null on success, else a log-facing reason. The skin is not added.
		/// </param>
		/// <returns>False when the skin was refused.</returns>
		[System.Obsolete("Retired before public release; use KingdomMergeRules.TryMergeSkin on the keyed draft.", true)]
		public static bool TryAddSkin(BuildEntry Entry, KingdomDesignRules.SkinEntry Skin, out string Error)
		{
			Error = null;
			if (Entry == null || Skin == null || string.IsNullOrEmpty(Skin.Key))
			{
				Error = "skin has nothing to attach to";
				return false;
			}
			if (KingdomDesignRules.FindSkin(Entry.Skins, Skin.Key) != null)
			{
				Error = "building " + Entry.Key + " declares the skin " + Skin.Key + " twice; the second was ignored";
				return false;
			}
			if (Entry.Skins == null)
			{
				Entry.Skins = new System.Collections.Generic.List<KingdomDesignRules.SkinEntry>();
			}
			Entry.Skins.Add(Skin);
			return true;
		}

		/// <summary>
		/// Whether a value is one of the growth stages this file defines.
		/// <para>
		/// <c>Enum.TryParse</c> accepts any number the underlying type can hold, so
		/// <c>MinStage="7"</c> parses happily into a stage no settlement can ever reach, and the
		/// design it gates is out of the founder's reach forever with nothing anywhere saying so.
		/// This is the guard that keeps such a value out of a registry entry.
		/// </para>
		/// </summary>
		public static bool IsKnownStage(GrowthStage Stage)
		{
			return System.Enum.IsDefined(typeof(GrowthStage), Stage);
		}

		public static string StripParenthetical(string Text)
		{
			if (string.IsNullOrEmpty(Text))
			{
				return Text;
			}
			int num = Text.IndexOf(" (");
			if (num <= 0)
			{
				return Text;
			}
			return Text.Substring(0, num);
		}

		public static bool TryParseBuildAttributes(string Key, string DisplayName, string Blueprint, string Cost, string Ticks, string Styles, string Category, string MinStage, string Staff, string Manning, string Defence, out BuildEntry Entry, out string Error)
		{
			Entry = null;
			Error = null;
			if (string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(DisplayName) || string.IsNullOrEmpty(Blueprint))
			{
				Error = "building needs Key, DisplayName, and Blueprint";
				return false;
			}
			if (!int.TryParse(Cost, out var costDrams) || costDrams < 0)
			{
				Error = "building " + Key + " has a bad Cost";
				return false;
			}
			if (!long.TryParse(Ticks, out var buildTicks) || buildTicks <= 0)
			{
				Error = "building " + Key + " has a bad Ticks";
				return false;
			}
			int defence = 0;
			if (!string.IsNullOrEmpty(Defence) && (!int.TryParse(Defence, out defence) || defence < 0))
			{
				Error = "building " + Key + " has a bad Defence";
				return false;
			}
			int staff = 0;
			if (!string.IsNullOrEmpty(Staff) && (!int.TryParse(Staff, out staff) || staff < 0))
			{
				Error = "building " + Key + " has a bad Staff";
				return false;
			}
			GrowthStage minStage = GrowthStage.Camp;
			if (!string.IsNullOrEmpty(MinStage) && (!System.Enum.TryParse<GrowthStage>(MinStage, ignoreCase: true, out minStage) || !IsKnownStage(minStage)))
			{
				Error = "building " + Key + " has a bad MinStage";
				return false;
			}
			Entry = new BuildEntry
			{
				Key = Key,
				DisplayName = DisplayName,
				Blueprint = Blueprint,
				CostDrams = costDrams,
				BuildTicks = buildTicks,
				Styles = (string.IsNullOrEmpty(Styles) ? "common" : Styles),
				Category = (string.IsNullOrEmpty(Category) ? "civic" : Category),
				MinStage = minStage,
				Staff = staff,
				Defence = defence,
				Manning = (string.IsNullOrEmpty(Manning) ? "scaled" : Manning),
				ShortName = StripParenthetical(DisplayName)
			};
			return true;
		}

		/// <summary>
		/// Whether a design's <c>Styles</c> list offers it to a city of this style.
		/// <para>
		/// A style is a TAG (Addendum 16), and this is the one tag idiom every open-ended set in
		/// the catalogue is matched by: <c>KingdomZoningRules.TagAccepts</c>. The whole of the
		/// rule lives there &mdash; a comma list, <c>all</c> for every style there is, and a
		/// leading <c>!</c> for "every style but this one" &mdash; and this method is kept where
		/// it has always been because a third party is calling it.
		/// </para>
		/// <para>
		/// What the migration changed, and it only ever widens: the comparison is case-folded
		/// now. <c>Styles="Verdant"</c> used to match nothing at all and say nothing about it,
		/// which is the exact silent failure a tag idiom exists to make impossible.
		/// </para>
		/// </summary>
		public static bool StyleAllows(string EntryStyles, string CityStyle)
		{
			return KingdomStyleRules.TagAccepts(EntryStyles, CityStyle);
		}

		/// <summary>Registry-aware form for engine callers and third-party style aliases.</summary>
		public static bool StyleAllows(string EntryStyles,
			System.Collections.Generic.IList<string> CityStyleKeys)
		{
			return KingdomStyleRules.TagAccepts(EntryStyles, CityStyleKeys);
		}

	}
}
