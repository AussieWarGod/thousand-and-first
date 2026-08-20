using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for a design's optional look and a founder's optional name for what they
	/// raised.
	/// <para>
	/// Skin resolution answers "what should this building look like" from data alone: a design's
	/// authored <see cref="SkinEntry"/> list, the city's own <c>Style</c>, and whichever key (if
	/// any) the founder picked when they commissioned it. Nothing here ever guesses a look for a
	/// style nobody authored one for &mdash; <see cref="ResolveDefaultSkin"/> returns null rather
	/// than the first entry in the list, because "whatever the catalogue offers first" is
	/// precisely the choice this file exists to take away from the settlement and hand to the
	/// founder.
	/// </para>
	/// <para>
	/// Name rules answer "what does the mod call this thing" the same way: a founder-given name
	/// if there is a clean one, the design's own generic name otherwise.
	/// </para>
	/// <para>
	/// Nothing here touches <c>XRL</c>, and nothing here can check whether a <see
	/// cref="SkinEntry.Tile"/> actually exists &mdash; that is a static-asset-reachability
	/// question (STANDARDS.md §4's "asset is not shipped until something proves it is reachable"),
	/// out of reach for engine-free code. The engine-coupled half (Popup prompts, the <c>Render</c>
	/// part, the property that carries a name) lives in <see cref="KingdomDesign"/>, in the same
	/// folder.
	/// </para>
	/// </summary>
	public static class KingdomDesignRules
	{
		/// <summary>
		/// One authored appearance for a design: a subset of what the <c>Render</c> part exposes
		/// (<c>Tile</c>, <c>ColorString</c>, <c>DetailColor</c>, <c>RenderString</c>), keyed and
		/// optionally tied to one city style. A blank field means "leave the design's own value
		/// alone" &mdash; a skin only overrides what it actually names, the same restraint the
		/// protection law (STANDARDS.md §7) asks of everything else this mod touches.
		/// </summary>
		public sealed class SkinEntry
		{
			/// <summary>Unique within one design's skin list. Required.</summary>
			public string Key;

			/// <summary>
			/// A city style this skin is suggested for (see <see cref="ResolveDefaultSkin"/>), or
			/// null for a skin offered under every style with no suggestion either way. Compared
			/// the same way <c>KingdomRules.StyleAllows</c> compares a design's own <c>Styles</c>
			/// list &mdash; an exact, case-sensitive match against <c>KingdomSystem.Style</c>.
			/// </summary>
			public string Style;

			/// <summary>Overrides <c>Render.ColorString</c> when non-null. See remarks on <see
			/// cref="Tile"/> for why a skin never introduces new art, only recolors it.</summary>
			public string ColorString;

			/// <summary>Overrides <c>Render.DetailColor</c> when non-null.</summary>
			public string DetailColor;

			/// <summary>Overrides <c>Render.RenderString</c> (the ASCII glyph) when non-null.</summary>
			public string RenderString;

			/// <summary>
			/// A tile path to reuse. Never a new asset: STANDARDS.md's own account of four
			/// shipped, unreachable tiles is why a skin only ever NAMES art, never introduces it
			/// &mdash; whoever authors a skin is responsible for pointing at a tile that already
			/// exists (a vanilla one, or one the design's own blueprint already ships). Leave
			/// this null to keep the design's own tile.
			/// </summary>
			public string Tile;
		}

		/// <summary>
		/// Matches <c>Founding/FounderBasin.cs</c>'s own settlement-naming prompt
		/// (<c>Popup.AskString(..., MaxLength: 30, ...)</c>), so a building's name and a city's
		/// name are held to the same bar.
		/// </summary>
		public const int MaxBuildingNameLength = 30;

		/// <summary>
		/// Validates one <c>&lt;skin&gt;</c> node's raw attributes into a <see cref="SkinEntry"/>.
		/// A skin that overrides nothing is rejected rather than silently accepted as a no-op
		/// &mdash; the same "an unreadable extension disables itself with a logged reason" bar
		/// STANDARDS.md §9 sets for every third-party XML boundary.
		/// </summary>
		/// <param name="Key">Required, unique within the owning design.</param>
		/// <param name="Style">Optional city style this skin is suggested for.</param>
		/// <param name="ColorString">Optional <c>Render.ColorString</c> override.</param>
		/// <param name="DetailColor">Optional <c>Render.DetailColor</c> override.</param>
		/// <param name="RenderString">Optional <c>Render.RenderString</c> override.</param>
		/// <param name="Tile">Optional <c>Render.Tile</c> override; must already exist.</param>
		/// <param name="Entry">The parsed skin, or null on failure.</param>
		/// <param name="Error">A logged-ready reason, or null on success.</param>
		public static bool TryParseSkinAttributes(string Key, string Style, string ColorString, string DetailColor, string RenderString, string Tile, out SkinEntry Entry, out string Error)
		{
			Entry = null;
			Error = null;
			if (string.IsNullOrWhiteSpace(Key))
			{
				Error = "skin needs a Key";
				return false;
			}
			bool hasOverride = !string.IsNullOrEmpty(ColorString) || !string.IsNullOrEmpty(DetailColor)
				|| !string.IsNullOrEmpty(RenderString) || !string.IsNullOrEmpty(Tile);
			if (!hasOverride)
			{
				Error = "skin " + Key + " changes nothing -- give it a ColorString, DetailColor, RenderString, or Tile";
				return false;
			}
			Entry = new SkinEntry
			{
				Key = Key.Trim(),
				Style = string.IsNullOrWhiteSpace(Style) ? null : Style.Trim(),
				ColorString = string.IsNullOrEmpty(ColorString) ? null : ColorString,
				DetailColor = string.IsNullOrEmpty(DetailColor) ? null : DetailColor,
				RenderString = string.IsNullOrEmpty(RenderString) ? null : RenderString,
				Tile = string.IsNullOrEmpty(Tile) ? null : Tile
			};
			return true;
		}

		/// <summary>Looks up one skin by key. Null if <paramref name="Skins"/> is empty, null, or
		/// has no matching key &mdash; never throws on a stale or third-party-withdrawn key.</summary>
		public static SkinEntry FindSkin(IReadOnlyList<SkinEntry> Skins, string Key)
		{
			if (Skins == null || string.IsNullOrEmpty(Key))
			{
				return null;
			}
			for (int i = 0; i < Skins.Count; i++)
			{
				if (Skins[i] != null && Skins[i].Key == Key)
				{
					return Skins[i];
				}
			}
			return null;
		}

		/// <summary>
		/// The skin a city's own style suggests, so "a verdant city looks verdant without the
		/// founder touching anything" (VISION.md). Returns null &mdash; never the first skin in
		/// the list &mdash; when no skin names this exact style; a design with no style-matched
		/// skin keeps its own vanilla look by default, which is always a correct answer.
		/// </summary>
		public static SkinEntry ResolveDefaultSkin(IReadOnlyList<SkinEntry> Skins, string CityStyle)
		{
			if (Skins == null || string.IsNullOrEmpty(CityStyle))
			{
				return null;
			}
			for (int i = 0; i < Skins.Count; i++)
			{
				if (Skins[i] != null && Skins[i].Style == CityStyle)
				{
					return Skins[i];
				}
			}
			return null;
		}

		/// <summary>One menu line for a skin choice. Pure presentation; <paramref
		/// name="Suggested"/> marks the one <see cref="ResolveDefaultSkin"/> would pick.</summary>
		public static string DescribeSkinOption(SkinEntry Skin, bool Suggested)
		{
			if (Skin == null || string.IsNullOrEmpty(Skin.Key))
			{
				return "";
			}
			return Suggested ? (Skin.Key + " {{G|[suggested]}}") : Skin.Key;
		}

		/// <summary>True when <paramref name="Raw"/> is null, empty, or whitespace-only &mdash;
		/// the founder declining to name (or renaming to nothing, which clears a name).</summary>
		public static bool IsBlank(string Raw)
		{
			return string.IsNullOrWhiteSpace(Raw);
		}

		/// <summary>
		/// Validates and trims a founder-typed building name. Rejects a blank name (call <see
		/// cref="IsBlank"/> first to tell "declined" apart from "invalid"), a name over <see
		/// cref="MaxBuildingNameLength"/> characters after trimming, and any control character or
		/// curly brace &mdash; a name is spliced verbatim into <c>{{C|...}}</c>-style
		/// chronicle and ledger markup elsewhere in this mod, and a brace could close that markup
		/// early and inject arbitrary trailing text into the founder's own reports.
		/// </summary>
		/// <param name="Raw">The founder's typed text.</param>
		/// <param name="Cleaned">The trimmed, accepted name, or null on failure.</param>
		/// <param name="Error">A founder-facing reason, or null on success.</param>
		public static bool TryValidateBuildingName(string Raw, out string Cleaned, out string Error)
		{
			Cleaned = null;
			Error = null;
			if (IsBlank(Raw))
			{
				Error = "a name needs at least one character";
				return false;
			}
			string trimmed = Raw.Trim();
			if (trimmed.Length > MaxBuildingNameLength)
			{
				Error = "a name can be at most " + MaxBuildingNameLength + " characters";
				return false;
			}
			for (int i = 0; i < trimmed.Length; i++)
			{
				char c = trimmed[i];
				if (char.IsControl(c) || c == '{' || c == '}')
				{
					Error = "a name cannot use control characters or { }";
					return false;
				}
			}
			Cleaned = trimmed;
			return true;
		}

		/// <summary>
		/// How the mod refers to a raised building in generated prose: the founder's own name if
		/// there is a clean one, the design's generic label otherwise. An unnamed building reads
		/// exactly as it always has &mdash; naming is a bonus for engaging, never a requirement.
		/// </summary>
		/// <param name="GivenName">The stored given name, possibly null, empty, or stale (untrimmed,
		/// pre-validation data from an older save). Blank after trimming falls back to <paramref
		/// name="GenericLabel"/> rather than erroring.</param>
		/// <param name="GenericLabel">The design's own name (<c>BuildEntry.Name</c>).</param>
		public static string NamedReference(string GivenName, string GenericLabel)
		{
			return IsBlank(GivenName) ? GenericLabel : GivenName.Trim();
		}
	}
}
