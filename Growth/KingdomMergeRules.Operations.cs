using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomMergeRules
	{
		// --- Merging one declaration into another -------------------------------------------

		/// <summary>
		/// Which of <see cref="RequiredAttributes"/> a draft still lacks. A file that names some of
		/// them and not others is a merge fragment, which is correct and ordinary when an earlier
		/// file declared the key, and is a typo when none did.
		/// </summary>
		/// <returns>True when something required is missing or blank.</returns>
		public static bool IsFragment(BuildingDraft Draft, out List<string> Missing)
		{
			Missing = new List<string>();
			for (int i = 0; i < RequiredAttributes.Length; i++)
			{
				string value = (Draft == null) ? null : Draft.Get(RequiredAttributes[i]);
				if (string.IsNullOrEmpty(value) || value.Trim().Length == 0)
				{
					Missing.Add(RequiredAttributes[i]);
				}
			}
			return Missing.Count > 0;
		}

		/// <summary>
		/// Folds a later declaration into the design an earlier file already declared.
		/// <para>
		/// Named attributes override, omitted attributes survive, skins carry across for the later
		/// file's <c>&lt;skin&gt;</c> children to append to or replace within. A merge that names a
		/// key nothing has declared simply becomes that key's first declaration &mdash; exactly
		/// what a re-used key has always done &mdash; and says so when it is too thin to stand on
		/// its own, because that shape is nearly always a mis-spelled key.
		/// </para>
		/// </summary>
		/// <param name="Standing">The design so far, or null for a key nobody has declared.</param>
		/// <param name="Later">This file's declaration. Null returns the standing design.</param>
		/// <param name="Findings">Appended to, for the load log. Null is accepted and skips the
		/// reporting; nothing about the merge itself changes.</param>
		/// <returns>A new draft. Neither argument is modified.</returns>
		public static BuildingDraft Merge(BuildingDraft Standing, BuildingDraft Later, List<CatalogueFinding> Findings)
		{
			if (Later == null)
			{
				return Standing;
			}
			if (Standing == null)
			{
				BuildingDraft first = Later.Copy();
				first.Declarations = 1;
				first.SkinKeysThisPass.Clear();
				List<string> missing;
				if (IsFragment(first, out missing) && !string.IsNullOrEmpty(first.Key))
				{
					Add(Findings, new CatalogueFinding(first.Key, "Key", CatalogueSeverity.Note,
						"building " + first.Key + " is merged into, but no earlier file declares it, so this is its first declaration and it is missing " + Join(missing)));
				}
				return first;
			}
			BuildingDraft merged = Standing.Copy();
			merged.SkinKeysThisPass.Clear();
			merged.Declarations = Standing.Declarations + 1;
			if (!string.IsNullOrEmpty(Later.Origin))
			{
				merged.Origin = Later.Origin;
			}
			List<string> overridden = new List<string>();
			for (int i = 0; i < Later.Attributes.Count; i++)
			{
				DraftAttribute attribute = Later.Attributes[i];
				if (!Same(merged.Get(attribute.Name), attribute.Value))
				{
					overridden.Add(attribute.Name);
				}
				merged.Set(attribute.Name, attribute.Value);
			}
			if (Later.Skins != null)
			{
				for (int i = 0; i < Later.Skins.Count; i++)
				{
					bool replaced;
					string error;
					TryMergeSkin(merged, Later.Skins[i], out replaced, out error);
				}
			}
			if (overridden.Count > 0)
			{
				Add(Findings, new CatalogueFinding(merged.Key, "Key", CatalogueSeverity.Note,
					"building " + merged.Key + " is merged into by a later file, which sets " + Join(overridden) + "; everything else the earlier design said still stands"));
			}
			// A later declaration that overrides no attribute is NOT reported, however tempting.
			// Skins are children, so in a single-pass load they have not been read yet when this
			// runs, and "this file changed nothing" would be a lie told to every file whose whole
			// purpose is to add one skin.
			return merged;
		}

		/// <summary>
		/// Appends one parsed skin to a draft, replacing an earlier file's skin of the same key in
		/// place rather than shadowing it, and still refusing the same key twice inside ONE
		/// element.
		/// <para>
		/// Replacing in place keeps the offer order the base catalogue chose, so a mod that
		/// re-colours the verdant skin does not also move it to the bottom of the founder's list.
		/// </para>
		/// </summary>
		/// <param name="Draft">The draft being built up. Its <see cref="BuildingDraft.Skins"/> is
		/// created on first use, so a design with no skins keeps a null list.</param>
		/// <param name="Skin">A skin from <c>KingdomDesignRules.TryParseSkinAttributes</c>.</param>
		/// <param name="Replaced">True when this skin took the place of one already in the list.
		/// </param>
		/// <param name="Error">Null on success, else a log-facing reason. The skin is not added.
		/// </param>
		public static bool TryMergeSkin(BuildingDraft Draft, KingdomDesignRules.SkinEntry Skin, out bool Replaced, out string Error)
		{
			Replaced = false;
			Error = null;
			if (Draft == null || Skin == null || string.IsNullOrEmpty(Skin.Key))
			{
				Error = "skin has nothing to attach to";
				return false;
			}
			if (Draft.SkinKeysThisPass.Contains(Skin.Key))
			{
				Error = "building " + Draft.Key + " declares the skin " + Skin.Key + " twice; the second was ignored";
				return false;
			}
			Draft.SkinKeysThisPass.Add(Skin.Key);
			if (Draft.Skins == null)
			{
				Draft.Skins = new List<KingdomDesignRules.SkinEntry>();
			}
			for (int i = 0; i < Draft.Skins.Count; i++)
			{
				if (Draft.Skins[i] != null && Draft.Skins[i].Key == Skin.Key)
				{
					Draft.Skins[i] = Skin;
					Replaced = true;
					return true;
				}
			}
			Draft.Skins.Add(Skin);
			return true;
		}

	}
}
