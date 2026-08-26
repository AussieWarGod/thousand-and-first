using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCatalogueRules
	{
		// --- Whole-file validation --------------------------------------------------------------

		/// <summary>
		/// Reads a whole catalogue and says what is wrong with it. Nothing is refused and nothing
		/// is changed: every problem comes back as a <see cref="CatalogueFinding"/> for the log.
		/// <para>
		/// Findings come out in a stable order &mdash; the duplicate-key faults first, then every
		/// per-entry finding in the order the entries were given, then the findings about the file
		/// as a whole &mdash; so a log diffs cleanly between runs.
		/// </para>
		/// </summary>
		/// <param name="Entries">Every <c>&lt;building&gt;</c> in the merged catalogue. Null reads
		/// as none, which is a catalogue with nothing wrong with it and nothing in it.</param>
		/// <param name="DeclaredStyles">Every <c>&lt;style&gt;</c> name the merged files declare.
		/// Null skips both style checks rather than calling every style unknown.</param>
		public static List<CatalogueFinding> Validate(IEnumerable<CatalogueEntry> Entries, IEnumerable<string> DeclaredStyles)
		{
			List<CatalogueFinding> findings = new List<CatalogueFinding>();
			List<CatalogueEntry> entries = new List<CatalogueEntry>();
			Dictionary<string, CatalogueEntry> byKey = new Dictionary<string, CatalogueEntry>();
			if (Entries != null)
			{
				foreach (CatalogueEntry entry in Entries)
				{
					if (entry == null || string.IsNullOrEmpty(entry.Key))
					{
						continue;
					}
					entries.Add(entry);
					if (byKey.ContainsKey(entry.Key))
					{
						// Not the same thing as a third-party file re-using a key: that happens
						// across files, is a supported way to retheme the catalogue, and is folded
						// into ONE entry before validation (KingdomMergeRules.Absorb). Two entries
						// under one key reaching this far means the caller did not merge, and the
						// design the settlement builds is only half of what the files said.
						findings.Add(new CatalogueFinding(entry.Key, "Key", CatalogueSeverity.Fault,
							"building " + entry.Key + " reaches the catalogue twice unmerged; a later declaration of a key merges into the earlier one rather than replacing it"));
					}
					byKey[entry.Key] = entry;
				}
			}
			List<string> styles = (DeclaredStyles == null) ? null : Fold(new List<string>(DeclaredStyles));
			List<string> stylesUsed = new List<string>();
			bool anyStyleTakesAll = false;
			List<string> categoriesAtCamp = new List<string>();
			List<string> categoriesSeen = new List<string>();

			for (int i = 0; i < entries.Count; i++)
			{
				CatalogueEntry entry = entries[i];
				ValidateEntry(entry, byKey, findings);
				anyStyleTakesAll |= CollectStyles(entry, stylesUsed);
				string category = Fold(entry.Category) ?? "civic";
				if (!categoriesSeen.Contains(category))
				{
					categoriesSeen.Add(category);
				}
				if (EffectiveMinStage(entry.MinStage, entry.Plot) == GrowthStage.Camp && !categoriesAtCamp.Contains(category))
				{
					categoriesAtCamp.Add(category);
				}
			}

			if (styles != null)
			{
				for (int i = 0; i < stylesUsed.Count; i++)
				{
					if (!styles.Contains(stylesUsed[i]))
					{
						findings.Add(new CatalogueFinding(null, "Styles", CatalogueSeverity.Note,
							"the style " + stylesUsed[i] + " is built for but declared by no <style>"));
					}
				}
				// A single design written Styles="all" is offered to every style there is, so it
				// answers the unreferenced-style half of the check for all of them at once. Only
				// a catalogue where every design names its styles can leave one with nothing.
				if (!anyStyleTakesAll)
				{
					for (int i = 0; i < styles.Count; i++)
					{
						if (!stylesUsed.Contains(styles[i]))
						{
							findings.Add(new CatalogueFinding(null, "style", CatalogueSeverity.Note,
								"the style " + styles[i] + " is declared but no design is offered to it"));
						}
					}
				}
			}

			for (int i = 0; i < categoriesSeen.Count; i++)
			{
				if (!categoriesAtCamp.Contains(categoriesSeen[i]))
				{
					// Not automatically wrong - a settlement has no business raising a scriptorium
					// on its first night - but a family that opens above a camp is a family the
					// early game cannot touch at all, and that should be a decision somebody made
					// rather than an accident of stage gates.
					findings.Add(new CatalogueFinding(null, "MinStage", CatalogueSeverity.Note,
						"nothing filed under " + categoriesSeen[i] + " is within a camp's reach"));
				}
			}
			return findings;
		}

	}
}
