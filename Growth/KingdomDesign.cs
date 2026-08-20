using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of a building's look and name: the founder-facing prompts, the
	/// <c>Render</c> part mutation a chosen skin actually performs, and the property a given name
	/// lives in. Every decision (which skin a style suggests, whether a typed name is clean) is
	/// delegated to the engine-free <see cref="KingdomDesignRules"/>; this file only asks Popup
	/// for input and applies what that file already decided.
	/// <para>
	/// Skin choice happens once, at commission time, because the object it applies to does not
	/// exist yet &mdash; the choice is staged onto the <c>r_KingdomScaffold</c> that will build it
	/// (<see cref="StageSkin"/>) and realised in <see cref="ApplyRenderOverrides"/> when that
	/// scaffold completes. Naming is the opposite shape: it only makes sense once there is a
	/// building standing to name (<see cref="RenameBuilding"/>), so it is offered as its own
	/// action against whatever the founder is standing beside, any time after it is raised, and as
	/// many times as the founder likes.
	/// </para>
	/// </summary>
	public static class KingdomDesign
	{
		/// <summary>
		/// Property a founder-given name is stored under. Read by <see cref="ReferenceFor"/> and
		/// nothing else in this mod reads it directly &mdash; every other file that wants a
		/// building's name goes through that method rather than the raw property, so a future
		/// change to how names fall back never has to be re-applied at each call site.
		/// </summary>
		public const string GivenNameProperty = "KingdomGivenName";

		/// <summary>
		/// Offers a skin choice for a design, if it has any. Shows no popup at all &mdash; not even
		/// an empty one &mdash; when the design carries no skins, matching the rest of this mod's
		/// "a bonus for engaging, never friction for abstaining" rule. The city's own style is
		/// offered as the suggested choice (<see cref="KingdomDesignRules.ResolveDefaultSkin"/>)
		/// but never forced; escaping or picking the design's own look both return null, which
		/// callers treat as "no override".
		/// </summary>
		/// <param name="Entry">The design being commissioned.</param>
		/// <param name="CityStyle">The kingdom's own <c>Style</c>.</param>
		/// <returns>The chosen skin, or null for the design's unmodified look.</returns>
		public static KingdomDesignRules.SkinEntry ChooseSkin(KingdomRules.BuildEntry Entry, string CityStyle)
		{
			if (Entry == null || Entry.Skins == null || Entry.Skins.Count == 0)
			{
				return null;
			}
			KingdomDesignRules.SkinEntry suggested = KingdomDesignRules.ResolveDefaultSkin(Entry.Skins, CityStyle);
			string[] options = new string[Entry.Skins.Count + 1];
			options[0] = "{{K|The design's own look}}" + ((suggested == null) ? " {{G|[suggested]}}" : "");
			for (int i = 0; i < Entry.Skins.Count; i++)
			{
				options[i + 1] = KingdomDesignRules.DescribeSkinOption(Entry.Skins[i], Entry.Skins[i] == suggested);
			}
			int num = Popup.PickOption(Title: "How should the " + Entry.Name + " look?", Options: options, AllowEscape: true);
			if (num <= 0)
			{
				return null;
			}
			return Entry.Skins[num - 1];
		}

		/// <summary>Property a staged skin's <c>Render.ColorString</c> override waits in.</summary>
		public const string StagedColorStringProperty = "KingdomSkinColorString";

		/// <summary>Property a staged skin's <c>Render.DetailColor</c> override waits in.</summary>
		public const string StagedDetailColorProperty = "KingdomSkinDetailColor";

		/// <summary>Property a staged skin's <c>Render.RenderString</c> override waits in.</summary>
		public const string StagedRenderStringProperty = "KingdomSkinRenderString";

		/// <summary>Property a staged skin's <c>Render.Tile</c> override waits in.</summary>
		public const string StagedTileProperty = "KingdomSkinTile";

		/// <summary>
		/// Property a founder's chosen skin key waits in on a staked plan, from the moment the plan
		/// is staked to the moment the settlement turns it into a scaffold.
		/// </summary>
		public const string PlannedSkinProperty = "KingdomSkinKey";

		/// <summary>
		/// Copies a chosen skin's overrides onto the scaffold that will build it, so they survive
		/// the wait until the scaffold completes. A no-op on a null scaffold, entry, or key, or when
		/// the key names no skin the design currently carries (a design a third-party mod re-shipped
		/// without that skin between commission and completion loses the override quietly rather
		/// than throwing &mdash; the building still completes, just without the look asked for).
		/// <para>
		/// The overrides ride as string properties on the scaffold OBJECT rather than as fields on
		/// its part, for the same reason <c>KingdomDefencePending</c> does: a part's fields are
		/// written positionally, so appending four of them would make every save with a scaffold
		/// mid-build unreadable (STANDARDS.md &sect;1). Properties are a self-describing dictionary,
		/// and a save written before skins existed simply has none.
		/// </para>
		/// </summary>
		/// <param name="Scaffold">The freshly created scaffold object.</param>
		/// <param name="Entry">The design being built.</param>
		/// <param name="SkinKey">The key chosen at commission time, or null for no override.</param>
		public static void StageSkin(GameObject Scaffold, KingdomRules.BuildEntry Entry, string SkinKey)
		{
			if (Scaffold == null || Entry == null || string.IsNullOrEmpty(SkinKey))
			{
				return;
			}
			KingdomDesignRules.SkinEntry skin = KingdomDesignRules.FindSkin(Entry.Skins, SkinKey);
			if (skin == null)
			{
				return;
			}
			Stage(Scaffold, StagedColorStringProperty, skin.ColorString);
			Stage(Scaffold, StagedDetailColorProperty, skin.DetailColor);
			Stage(Scaffold, StagedRenderStringProperty, skin.RenderString);
			Stage(Scaffold, StagedTileProperty, skin.Tile);
		}

		private static void Stage(GameObject Scaffold, string Property, string Value)
		{
			if (!string.IsNullOrEmpty(Value))
			{
				Scaffold.SetStringProperty(Property, Value);
			}
		}

		/// <summary>
		/// Applies a staged skin's overrides to the <c>Render</c> part of a just-completed
		/// building. Called from <c>r_KingdomScaffold.Complete()</c>, which runs on a turn tick, so
		/// this wraps its own work in <see cref="KingdomSystem.Guard"/> rather than trust the tick
		/// loop to survive a bad override on its own (STANDARDS.md §9: "every entry point invoked
		/// by the engine... wraps its work"). Only fields that were actually overridden change;
		/// every blank field leaves the design's own vanilla look untouched.
		/// </summary>
		public static void ApplyRenderOverrides(GameObject Target, string ColorString, string DetailColor, string RenderString, string Tile)
		{
			if (Target == null)
			{
				return;
			}
			if (string.IsNullOrEmpty(ColorString) && string.IsNullOrEmpty(DetailColor)
				&& string.IsNullOrEmpty(RenderString) && string.IsNullOrEmpty(Tile))
			{
				return;
			}
			KingdomSystem.Guard("building skin", delegate
			{
				Render render = Target.GetPart<Render>();
				if (render == null)
				{
					return;
				}
				if (!string.IsNullOrEmpty(ColorString))
				{
					render.ColorString = ColorString;
				}
				if (!string.IsNullOrEmpty(DetailColor))
				{
					render.DetailColor = DetailColor;
				}
				if (!string.IsNullOrEmpty(RenderString))
				{
					render.RenderString = RenderString;
				}
				if (!string.IsNullOrEmpty(Tile))
				{
					render.Tile = Tile;
				}
			});
		}

		/// <summary>What the mod calls <paramref name="Target"/> in generated prose: its given
		/// name if the founder set one, <paramref name="GenericLabel"/> otherwise.</summary>
		public static string ReferenceFor(GameObject Target, string GenericLabel)
		{
			string given = Target?.GetStringProperty(GivenNameProperty);
			return KingdomDesignRules.NamedReference(given, GenericLabel);
		}

		/// <summary>
		/// The standalone naming action: lets the founder give (or let go of) a name for whatever
		/// they raised near where they are standing. Naming is never required and touches nothing
		/// the founder did not pick from the list &mdash; an unnamed building is refused nothing,
		/// and this action only ever offers structures the settlement itself already marks
		/// <c>KingdomBuilt</c> (commissioned, staked-and-realised, or adopted alike).
		/// </summary>
		/// <param name="System">The realm, for its claim and its chronicle.</param>
		/// <param name="Founder">Whoever is standing at the naming &mdash; the Charter's own
		/// <c>ParentObject</c>.</param>
		public static void RenameBuilding(KingdomSystem System, GameObject Founder)
		{
			if (System == null || Founder == null)
			{
				return;
			}
			Zone zone = Founder.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Names are given on the kingdom's own ground.");
				return;
			}
			Cell cell = Founder.CurrentCell;
			if (cell == null)
			{
				return;
			}
			List<GameObject> candidates = new List<GameObject>();
			CollectBuilt(cell, candidates);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				CollectBuilt(adjacent, candidates);
			}
			if (candidates.Count == 0)
			{
				Popup.Show("Stand beside something " + System.SeatName + " built to give it a name.");
				return;
			}
			string[] options = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				string given = candidates[i].GetStringProperty(GivenNameProperty);
				options[i] = candidates[i].ShortDisplayName + (string.IsNullOrEmpty(given) ? "" : (" {{G|[named " + given + "]}}"));
			}
			int num = Popup.PickOption(Title: "Name a building at " + System.SeatName, Options: options, AllowEscape: true);
			if (num < 0)
			{
				return;
			}
			GameObject target = candidates[num];
			string existing = target.GetStringProperty(GivenNameProperty);
			string raw = Popup.AskString("Name this " + target.ShortDisplayNameStripped + ". Leave blank to let go of its name.",
				existing ?? "", MaxLength: KingdomDesignRules.MaxBuildingNameLength, ReturnNullForEscape: true);
			if (raw == null)
			{
				return;
			}
			if (KingdomDesignRules.IsBlank(raw))
			{
				if (string.IsNullOrEmpty(existing))
				{
					return;
				}
				target.SetStringProperty(GivenNameProperty, null, RemoveIfNull: true);
				KingdomChronicle.Record(System, existing + " is called that no longer");
				Popup.Show("The name is let go.");
				return;
			}
			if (!KingdomDesignRules.TryValidateBuildingName(raw, out string cleaned, out string error))
			{
				Popup.Show(error);
				return;
			}
			target.SetStringProperty(GivenNameProperty, cleaned);
			System.RecordDeed(cleaned + " was named, at " + System.KingdomDisplayName);
			KingdomChronicle.Record(System, target.ShortDisplayNameStripped + " at " + System.KingdomDisplayName + " was named " + cleaned);
			System.Ledger.Note("{{G|" + cleaned + " (" + target.ShortDisplayNameStripped + ") now stands named at " + System.KingdomDisplayName + ".}}");
			Popup.Show("{{G|" + target.ShortDisplayNameStripped + " is named " + cleaned + ".}}");
		}

		private static void CollectBuilt(Cell C, List<GameObject> Into)
		{
			if (C == null)
			{
				return;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 && !Into.Contains(item))
				{
					Into.Add(item);
				}
			}
		}
	}
}
