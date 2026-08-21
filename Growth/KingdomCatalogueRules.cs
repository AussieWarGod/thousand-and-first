using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// How loudly a <see cref="CatalogueFinding"/> should read in the log. Neither value ever
	/// unregisters anything: validation reports, and the catalogue is whatever the files said.
	/// </summary>
	public enum CatalogueSeverity
	{
		/// <summary>Worth saying out loud; the catalogue still works.</summary>
		Note = 0,

		/// <summary>Something in the file cannot do what it says it does. Still logged rather than
		/// thrown, because a third-party file must never be able to delete the base catalogue by
		/// being wrong about its own entry.</summary>
		Fault = 1
	}

	/// <summary>One thing wrong &mdash; or merely worth saying &mdash; about one catalogue entry,
	/// or about the catalogue as a whole when <see cref="Key"/> is null.</summary>
	public class CatalogueFinding
	{
		/// <summary>The entry this is about, or null for a finding about the whole file.</summary>
		public string Key;

		/// <summary>The attribute at fault, for an author reading the log. Null when the finding
		/// is about the entry rather than one of its attributes.</summary>
		public string Attribute;

		public CatalogueSeverity Severity;

		/// <summary>One sentence, log-facing. Nothing depends on the wording.</summary>
		public string Message;

		public CatalogueFinding(string Key, string Attribute, CatalogueSeverity Severity, string Message)
		{
			this.Key = Key;
			this.Attribute = Attribute;
			this.Severity = Severity;
			this.Message = Message;
		}
	}

	/// <summary>
	/// One <c>&lt;building&gt;</c> entry as the validator needs to see it: what
	/// <c>KingdomRules.BuildEntry</c> carries, plus the plot spec, the material cost, the
	/// equilibrium contribution, and the successor its chain names.
	/// <para>
	/// Deliberately a separate shape rather than a widened <c>BuildEntry</c>. The validator reads
	/// the file as a whole and needs four registries side by side &mdash; the catalogue, the plot
	/// specs, the material costs, and the upgrade chains &mdash; which are four different tables
	/// at load time. A caller fills one of these per entry and hands the list over; nothing here
	/// ever reads a registry, and nothing here needs the engine.
	/// </para>
	/// </summary>
	public class CatalogueEntry
	{
		public string Key;

		public string DisplayName;

		public string Category = "civic";

		/// <summary>The raw <c>Styles</c> attribute: a comma list, or <c>all</c>.</summary>
		public string Styles = "common";

		/// <summary>The authored <c>MinStage</c>. The stage a design is actually reachable at is
		/// <see cref="KingdomCatalogueRules.EffectiveMinStage"/>, which also accounts for the fact
		/// that a camp cannot lay a large plot however early the design says it may be raised.
		/// </summary>
		public GrowthStage MinStage;

		/// <summary><c>PlotSize.None</c> for a single-cell design &mdash; a wall segment, a
		/// tower, anything raised as one object on one cell.</summary>
		public KingdomPlotRules.PlotSize Plot;

		/// <summary>True for a plot with no roof over it: fields, yards, reservoirs, markets, and
		/// the salt-pan.</summary>
		public bool Open;

		/// <summary>Population table the finished interior is furnished from, or null.</summary>
		public string Contents;

		public int CostDrams;

		/// <summary>The raw <c>Materials</c> attribute, read by
		/// <c>KingdomMaterialRules.TryParseMaterialCost</c>.</summary>
		public string Materials;

		/// <summary>The raw <c>Carries</c> attribute: a comma list of <c>support:settlers</c>.
		/// </summary>
		public string Carries;

		public int Staff;

		public string Manning = "scaled";

		public int Defence;

		/// <summary>The <c>UpgradesTo</c> key, or null for a design that never changes.</summary>
		public string SuccessorKey;

		/// <summary>
		/// Width of the footprint this tier declares, or zero for a design that fills its plot.
		/// The footprint belongs to the building's tier; the plot is only the envelope it must fit
		/// inside, and the yard is whatever the tier does not cover.
		/// </summary>
		public int FootprintWidth;

		/// <summary>Height of the declared footprint. See <see cref="FootprintWidth"/>.</summary>
		public int FootprintHeight;

		/// <summary>What stands over the footprint. Meaningless unless
		/// <see cref="RoofDeclared"/>, which is the only state that can contradict anything.
		/// </summary>
		public KingdomPlotRules.RoofState Roof = KingdomPlotRules.RoofState.Walled;

		/// <summary>Whether the tier declared a roof of its own. Only a declared roof can
		/// contradict a design that needs weather; a design that claimed nothing is raised exactly
		/// as it always was.</summary>
		public bool RoofDeclared;

		/// <summary>The design's <c>Sky</c> flag: it needs sun, wind, or rain.</summary>
		public bool RequiresSky;

		/// <summary>
		/// How many <c>&lt;building&gt;</c> declarations this design is the merge of
		/// (<see cref="KingdomMergeRules"/>). One for a design only its own file declares; two or
		/// more means a fault reported here may belong to a file the base catalogue's author never
		/// saw, which the findings say out loud.
		/// </summary>
		public int Declarations = 1;

		/// <summary>A label for the file that most recently named this key, when the loader has one
		/// to give. Null is ordinary and simply leaves it out of the sentence.</summary>
		public string Origin;
	}

	/// <summary>One <c>kind:amount</c> pair out of a <c>Carries</c> list.</summary>
	public readonly struct KindAmount
	{
		public readonly string Kind;

		public readonly int Amount;

		public KindAmount(string Kind, int Amount)
		{
			this.Kind = Kind;
			this.Amount = Amount;
		}
	}

	/// <summary>
	/// Engine-free rules for how the building catalogue is denominated, and for what is wrong with
	/// a catalogue file read as a whole.
	/// <para>
	/// <b>The denomination.</b> A design's <c>Carries</c> number is not a flow. It is what the
	/// building adds to the settlement's <em>sustainable level</em> &mdash; the population the
	/// place holds when nobody is hauling anything in. Three supports bind
	/// (<see cref="BindingSupports"/>) and the level is the least of them; everything else a work
	/// gives lifts that level (<see cref="LiftingSupports"/>) and is capped, so no amount of
	/// shrines outruns the water. One point of <c>water</c> is one dram a day sustained, which is
	/// one settler's thirst at camp rates.
	/// </para>
	/// <para>
	/// <b>What this file does not own.</b> Plot geometry, stage-by-tier gating, contents rolls and
	/// wall material are <see cref="KingdomPlotRules"/>. The material vocabulary and the cost
	/// parser are <c>KingdomMaterialRules</c>. Gates are <see cref="KingdomZoningRules"/> and
	/// chains are <see cref="KingdomUpgradeRules"/>. This file adds the denomination and the
	/// whole-file cross-checks none of them can see alone, and defers everywhere else.
	/// </para>
	/// <para>
	/// <b>The catalogue it reads is the merged one.</b> Layering across files belongs to
	/// <see cref="KingdomMergeRules"/>: by the time a list reaches <see cref="Validate"/>, every
	/// file that named a key has been folded into one entry. That is what makes the whole-file
	/// checks worth having, because the contradictions that survive are the ones no single file
	/// contains &mdash; a footprint one mod declared standing on a plot a second mod shrank, an
	/// improvement ring whose last link is closed by a third. Every finding about a design more
	/// than one file declares says so, so the author reading the log knows to look past their own.
	/// </para>
	/// <para>
	/// <b>What this file never does.</b> Reject an entry. Every check returns a finding for the
	/// log, because a design that is wrong about itself should be visible and still buildable, and
	/// because a third-party file must never be able to delete the base catalogue by mis-spelling
	/// one attribute.
	/// </para>
	/// </summary>
	public static class KingdomCatalogueRules
	{
		/// <summary>Separator between entries in a <c>Carries</c> list.</summary>
		public const char ListSeparator = ',';

		/// <summary>Separator between a support and its amount: <c>water:8</c>.</summary>
		public const char AmountSeparator = ':';

		// --- Stage, against the plot the design stands on ----------------------------------------

		/// <summary>
		/// The stage a design is actually reachable at: the later of what its own <c>MinStage</c>
		/// asks for and what its plot tier requires (<c>KingdomPlotRules.StageForSize</c>). A
		/// design may gate itself above its tier &mdash; that is an author saying "not yet, even
		/// then" &mdash; but it can never gate itself below one, because there is no ground to put
		/// it on.
		/// </summary>
		public static GrowthStage EffectiveMinStage(GrowthStage Authored, KingdomPlotRules.PlotSize Plot)
		{
			GrowthStage plotStage = KingdomPlotRules.StageForSize(Plot);
			return (Authored > plotStage) ? Authored : plotStage;
		}

		// --- Supports and the equilibrium level -------------------------------------------------

		public const string SupportWater = "water";

		public const string SupportFood = "food";

		public const string SupportRoof = "roof";

		/// <summary>
		/// The three goods a settlement cannot go without, in the order a tie is broken. The
		/// equilibrium level is the least of the three, so a work that supplies none of them can
		/// never by itself let one more person live here &mdash; which is the whole reason the
		/// catalogue is denominated this way rather than in output per day.
		/// </summary>
		public static readonly string[] BindingSupports = new string[3] { SupportWater, SupportFood, SupportRoof };

		/// <summary>
		/// What a smithy, a shrine, a scriptorium, a barracks, and a bathhouse give: not a reason
		/// one more person can live here, but a reason one more person wants to. Lifting supports
		/// are summed and then capped against the binding level by <see cref="LiftCapPercent"/>.
		/// </summary>
		public static readonly string[] LiftingSupports = new string[5] { "craft", "spirit", "learning", "order", "luxury" };

		/// <summary>
		/// How far past its binding supports a settlement's comfort, faith, learning, order, and
		/// luxury can carry it, as a percentage of the binding level. Half: a well-loved town holds
		/// half again the people its water and fields alone would, and not one more.
		/// </summary>
		public const int LiftCapPercent = 50;

		/// <summary>
		/// The level below which nothing sinks. A camp carries itself &mdash; four people, a fire,
		/// and whatever they walked in with &mdash; so the floor is the smallest stage's own
		/// equilibrium rather than a special case bolted under the arithmetic.
		/// </summary>
		public const int FloorLevel = 4;

		/// <summary>Whether a support kind is one the level is the least of.</summary>
		public static bool IsBindingSupport(string Kind)
		{
			return Contains(BindingSupports, Fold(Kind));
		}

		/// <summary>Whether a support kind is one this file names at all. A kind it does not know
		/// is not an error &mdash; it lifts, because a third party inventing a new binding good
		/// would make every catalogue that predates it unbuildable.</summary>
		public static bool IsKnownSupport(string Kind)
		{
			string kind = Fold(Kind);
			return Contains(BindingSupports, kind) || Contains(LiftingSupports, kind);
		}

		/// <summary>
		/// The sustainable level, in settlers. The least of the three binding supports, lifted by
		/// the comfort of the place up to <see cref="LiftCapPercent"/> of that least figure, and
		/// never below <see cref="FloorLevel"/>.
		/// </summary>
		/// <param name="Water">Summed <c>water</c> contribution of every finished work.</param>
		/// <param name="Food">Summed <c>food</c> contribution.</param>
		/// <param name="Roof">Summed <c>roof</c> contribution.</param>
		/// <param name="Lift">Summed contribution of every lifting support together.</param>
		public static int Equilibrium(int Water, int Food, int Roof, int Lift)
		{
			int least = Least(Water, Food, Roof);
			if (least < 0)
			{
				least = 0;
			}
			int lift = (Lift < 0) ? 0 : Lift;
			int cap = least * LiftCapPercent / 100;
			if (lift > cap)
			{
				lift = cap;
			}
			int level = least + lift;
			return (level < FloorLevel) ? FloorLevel : level;
		}

		/// <summary>
		/// Which of the three is holding the settlement where it is. Ties go to water, then food,
		/// then roofs &mdash; water first because it is the spine of everything here, and because
		/// telling a founder to dig when they should be sowing is worse than the reverse.
		/// <para>
		/// This exists so the level can always say why (STANDARDS 7b). A settlement that has
		/// stopped growing and cannot name the reason is the single most common complaint made of
		/// building systems of this shape.
		/// </para>
		/// </summary>
		/// <returns>One of <see cref="BindingSupports"/>. Never null.</returns>
		public static string BindingSupport(int Water, int Food, int Roof)
		{
			int least = Least(Water, Food, Roof);
			if (Water == least)
			{
				return SupportWater;
			}
			return (Food == least) ? SupportFood : SupportRoof;
		}

		/// <summary>One line for the ledger, naming the level and what holds it there.</summary>
		public static string LimitLine(string Support, int Level)
		{
			string level = Level.ToString();
			switch (Fold(Support))
			{
			case SupportWater:
				return "The settlement carries " + level + ", and it is the water that holds it there.";
			case SupportFood:
				return "The settlement carries " + level + ", and it is the harvest that holds it there.";
			case SupportRoof:
				return "The settlement carries " + level + ". There are only so many roofs.";
			default:
				return "The settlement carries " + level + ".";
			}
		}

		// --- The Carries list -------------------------------------------------------------------

		/// <summary>
		/// Reads a <c>support:settlers</c> comma list. Whitespace anywhere is ignored, kinds are
		/// folded to lower case, and an empty attribute is an empty list rather than a fault.
		/// <para>
		/// Deliberately more forgiving than <c>KingdomMaterialRules.TryParseMaterialCost</c>, which
		/// refuses a kind it does not know. A material the settlement cannot hold is a cost nobody
		/// can ever pay; a support this file has not heard of is somebody else's good, and it
		/// lifts.
		/// </para>
		/// </summary>
		/// <param name="Source">The raw attribute, or null.</param>
		/// <param name="Tally">The pairs, in the order written. Never null; empty when
		/// <paramref name="Source"/> was.</param>
		/// <param name="Error">Null on success, else the first thing wrong. On failure the tally
		/// holds every pair that parsed before the bad one, so a caller that logs and carries on
		/// is not silently credited with nothing.</param>
		public static bool TryParseTally(string Source, out List<KindAmount> Tally, out string Error)
		{
			Tally = new List<KindAmount>();
			Error = null;
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return true;
			}
			string[] parts = Source.Split(ListSeparator);
			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i].Trim();
				if (part.Length == 0)
				{
					continue;
				}
				int split = part.IndexOf(AmountSeparator);
				if (split <= 0 || split >= part.Length - 1)
				{
					Error = "\"" + part + "\" is not a support and an amount";
					return false;
				}
				string kind = Fold(part.Substring(0, split));
				string amount = part.Substring(split + 1).Trim();
				if (kind == null)
				{
					Error = "\"" + part + "\" names no support";
					return false;
				}
				if (!int.TryParse(amount, out var value) || value < 0)
				{
					Error = "\"" + part + "\" has a bad amount";
					return false;
				}
				Tally.Add(new KindAmount(kind, value));
			}
			return true;
		}

		/// <summary>How much of one support a parsed tally holds. Repeats add, so
		/// <c>water:2,water:3</c> is five.</summary>
		public static int AmountOf(List<KindAmount> Tally, string Kind)
		{
			if (Tally == null)
			{
				return 0;
			}
			string kind = Fold(Kind);
			int total = 0;
			for (int i = 0; i < Tally.Count; i++)
			{
				if (Tally[i].Kind == kind)
				{
					total += Tally[i].Amount;
				}
			}
			return total;
		}

		/// <summary>Everything in a parsed tally that is not a binding support, summed &mdash; the
		/// <c>Lift</c> argument to <see cref="Equilibrium"/>. An unknown kind lifts.</summary>
		public static int LiftOf(List<KindAmount> Tally)
		{
			if (Tally == null)
			{
				return 0;
			}
			int total = 0;
			for (int i = 0; i < Tally.Count; i++)
			{
				if (!IsBindingSupport(Tally[i].Kind))
				{
					total += Tally[i].Amount;
				}
			}
			return total;
		}

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

		private static void ValidateEntry(CatalogueEntry Entry, Dictionary<string, CatalogueEntry> ByKey, List<CatalogueFinding> Findings)
		{
			// A Defence rating overrides the category at siting time: the layout puts anything that
			// carries one on the frontier line, whatever else it is filed under. A design that also
			// claims a plot is therefore asking for two mutually exclusive pieces of ground, and
			// the plot is the one that never gets laid.
			if (Entry.Defence > 0 && Entry.Plot != KingdomPlotRules.PlotSize.None)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Defence", CatalogueSeverity.Fault,
					"building " + Entry.Key + " claims " + PlotWord(Entry.Plot) + " and a defence rating; it will be sited on the wall line and its plot never laid"));
			}
			if (Entry.Open && !string.IsNullOrEmpty(Entry.Contents))
			{
				// An open plot has no interior, so the table would furnish the weather.
				Findings.Add(new CatalogueFinding(Entry.Key, "Contents", CatalogueSeverity.Note,
					"building " + Entry.Key + " is an open plot and names furnishings; there is no interior to put them in"));
			}
			if (Entry.MinStage < KingdomPlotRules.StageForSize(Entry.Plot))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "MinStage", CatalogueSeverity.Note,
					"building " + Entry.Key + " is offered from " + StageWord(Entry.MinStage) + " but wants " + PlotWord(Entry.Plot) + ", so it waits for " + StageWord(KingdomPlotRules.StageForSize(Entry.Plot)) + " anyway"));
			}

			List<KindAmount> carries;
			if (!TryParseTally(Entry.Carries, out carries, out var carriesError))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Carries", CatalogueSeverity.Fault,
					"building " + Entry.Key + " has a bad Carries: " + carriesError));
			}
			for (int i = 0; i < carries.Count; i++)
			{
				if (!IsKnownSupport(carries[i].Kind))
				{
					Findings.Add(new CatalogueFinding(Entry.Key, "Carries", CatalogueSeverity.Note,
						"building " + Entry.Key + " carries " + carries[i].Kind + ", which nothing binds on; it lifts the level instead"));
				}
			}
			// The material vocabulary and its parser belong to KingdomMaterialRules; this only
			// reports the verdict, so a seventh material never has to be added in two places.
			if (!KingdomMaterialRules.TryParseMaterialCost(Entry.Materials, out _, out var materialsError))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Materials", CatalogueSeverity.Fault,
					"building " + Entry.Key + " has a bad Materials: " + materialsError));
			}
			if (Entry.Staff > 0 && Entry.Defence == 0 && carries.Count == 0)
			{
				// Buildings are people: a work that takes a crew off the water detail and adds
				// nothing to what the settlement carries is a net loss the founder cannot see.
				Findings.Add(new CatalogueFinding(Entry.Key, "Carries", CatalogueSeverity.Note,
					"building " + Entry.Key + " takes a crew of " + Entry.Staff + " and adds nothing to what the settlement carries"));
			}
			if (Entry.Staff == 0 && Fold(Entry.Manning) == "threshold")
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Manning", CatalogueSeverity.Note,
					"building " + Entry.Key + " sets Manning but wants no staff, so the setting decides nothing"));
			}
			string manning = Fold(Entry.Manning);
			if (manning != null && manning != "scaled" && manning != "threshold")
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Manning", CatalogueSeverity.Note,
					"building " + Entry.Key + " has a Manning of " + manning + ", which is neither scaled nor threshold"));
			}
			if (KingdomZoningRules.NaturalDistricts(Entry.Category) == null)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Category", CatalogueSeverity.Note,
					"building " + Entry.Key + " is filed under " + (Fold(Entry.Category) ?? "nothing") + ", which no district claims; the plan will build it where the founder stands"));
			}
			ValidateFootprint(Entry, Findings);
			// A tier that DECLARED its roof has made a claim the design can contradict. A design
			// that declared nothing has claimed nothing, and is raised exactly as it always was.
			if (Entry.RequiresSky && Entry.RoofDeclared && !KingdomPlotRules.AdmitsSky(Entry.Roof))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Roof", CatalogueSeverity.Fault,
					"building " + Entry.Key + " needs weather and declares a tier that is " + KingdomPlotRules.RoofWord(Entry.Roof)
					+ "; it would be refused wherever it was raised" + Layered(Entry)));
			}
			if (Entry.Plot != KingdomPlotRules.PlotSize.None && Entry.RoofDeclared
				&& !KingdomPlotRules.HoldsBeds(Entry.Roof) && Fold(Entry.Category) == "housing")
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Roof", CatalogueSeverity.Note,
					"building " + Entry.Key + " is housing with nothing over it; nobody sleeps in the open" + Layered(Entry)));
			}
			ValidateChain(Entry, ByKey, Findings);
		}

		/// <summary>
		/// The sole footprint invariant: footprint &le; plot. The tier declares what it covers and
		/// the plot is only the envelope, so nothing here has an opinion about how big a tier
		/// should be &mdash; only about whether it fits on the ground the founder staked.
		/// <para>
		/// This is the check merge-by-key most needs. One file may declare the tier and its
		/// footprint; a second, wanting a smaller building, may override nothing but <c>Plot</c>.
		/// Neither file is wrong on its own and neither author can see the other's, so the only
		/// place the contradiction exists is the merged design &mdash; here.
		/// </para>
		/// </summary>
		private static void ValidateFootprint(CatalogueEntry Entry, List<CatalogueFinding> Findings)
		{
			if (Entry.FootprintWidth <= 0 && Entry.FootprintHeight <= 0)
			{
				return;
			}
			if (Entry.FootprintWidth <= 0 || Entry.FootprintHeight <= 0)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Footprint", CatalogueSeverity.Fault,
					"building " + Entry.Key + " declares a footprint of " + Entry.FootprintWidth + " by " + Entry.FootprintHeight + "; a footprint needs both a width and a height" + Layered(Entry)));
				return;
			}
			if (Entry.Plot == KingdomPlotRules.PlotSize.None)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Footprint", CatalogueSeverity.Fault,
					"building " + Entry.Key + " declares a footprint of " + Entry.FootprintWidth + " by " + Entry.FootprintHeight + " and no plot to stand it in" + Layered(Entry)));
				return;
			}
			int width;
			int height;
			if (!KingdomPlotRules.TryDimensions(Entry.Plot, out width, out height))
			{
				return;
			}
			if (Entry.FootprintWidth > width || Entry.FootprintHeight > height)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Footprint", CatalogueSeverity.Fault,
					"building " + Entry.Key + " covers " + Entry.FootprintWidth + " by " + Entry.FootprintHeight + " and stands on " + PlotWord(Entry.Plot) + ", which is " + width + " by " + height + "; a tier's footprint fits inside its plot or it is never raised" + Layered(Entry)));
			}
		}

		private static void ValidateChain(CatalogueEntry Entry, Dictionary<string, CatalogueEntry> ByKey, List<CatalogueFinding> Findings)
		{
			if (string.IsNullOrEmpty(Entry.SuccessorKey))
			{
				return;
			}
			if (!ByKey.TryGetValue(Entry.SuccessorKey, out var successor))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Fault,
					"building " + Entry.Key + " improves into " + Entry.SuccessorKey + ", which no building declares" + Layered(Entry)));
				return;
			}
			// Upgrades climb within a plot; sizes compete across plots. A design that improved into
			// a larger one would be an in-place metamorphosis onto ground the settlement never
			// cleared, and would quietly make the whole size-versus-stacking decision free.
			if (successor.Plot != Entry.Plot)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Fault,
					"building " + Entry.Key + " stands on " + PlotWord(Entry.Plot) + " and improves into " + successor.Key + ", which wants " + PlotWord(successor.Plot) + "; an improvement climbs within its own plot" + Layered(Entry) + Layered(successor)));
			}
			// Footprints climb within the plot. A successor that stands on LESS ground is not wrong,
			// but it hands back walled ground as yard, which is worth an author seeing.
			if (successor.FootprintWidth > 0 && Entry.FootprintWidth > 0
				&& successor.FootprintWidth * successor.FootprintHeight < Entry.FootprintWidth * Entry.FootprintHeight)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Footprint", CatalogueSeverity.Note,
					"building " + Entry.Key + " improves into " + successor.Key + ", which stands on less ground than it does" + Layered(Entry) + Layered(successor)));
			}
			GrowthStage from = EffectiveMinStage(Entry.MinStage, Entry.Plot);
			GrowthStage to = EffectiveMinStage(successor.MinStage, successor.Plot);
			if (to < from)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Fault,
					"building " + Entry.Key + " improves into " + successor.Key + ", which the settlement could have raised earlier"));
			}
			if (successor.CostDrams < Entry.CostDrams)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Note,
					"building " + Entry.Key + " improves into " + successor.Key + ", which costs less water to raise from nothing"));
			}
			if (Fold(successor.Category) != Fold(Entry.Category))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Note,
					"building " + Entry.Key + " improves into " + successor.Key + ", which is filed under a different purpose"));
			}
			// TryParseUpgradeAttributes already refuses a design that improves into itself; only a
			// pass over the whole catalogue can see a longer ring, and a ring spends the
			// settlement's entire surplus on going round it forever.
			List<string> walked = new List<string> { Entry.Key };
			string at = Entry.SuccessorKey;
			while (at != null && !walked.Contains(at) && ByKey.TryGetValue(at, out var next))
			{
				walked.Add(at);
				at = next.SuccessorKey;
			}
			if (at != null && walked.Contains(at))
			{
				// Post-merge, a ring is a thing no single file need contain: one mod may name the
				// first link and another the last, each correct alone. The finding therefore says
				// which of the links are themselves layered, which is the only clue an author has
				// that the file to fix may not be the one they wrote.
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Fault,
					"the improvement chain from " + Entry.Key + " comes back to " + at + RingLayers(walked, ByKey)));
			}
		}

		/// <summary>
		/// The clause that names a design as the merge of several files, or an empty string for a
		/// design only one file declares. Appended to a fault so an author reading the log knows to
		/// look past their own file (STANDARDS 7b: a thing that will not work says why, once, where
		/// somebody can act on it).
		/// </summary>
		private static string Layered(CatalogueEntry Entry)
		{
			if (Entry == null || Entry.Declarations <= 1)
			{
				return "";
			}
			string origin = string.IsNullOrEmpty(Entry.Origin) ? "" : (", last from " + Entry.Origin);
			return " (" + Entry.Key + " is the merge of " + Entry.Declarations + " declarations" + origin + ")";
		}

		/// <summary>Which links of a ring are themselves merged from more than one file.</summary>
		private static string RingLayers(List<string> Walked, Dictionary<string, CatalogueEntry> ByKey)
		{
			string list = "";
			for (int i = 0; i < Walked.Count; i++)
			{
				CatalogueEntry entry;
				if (ByKey.TryGetValue(Walked[i], out entry) && entry != null && entry.Declarations > 1)
				{
					list += ((list.Length == 0) ? "" : ", ") + Walked[i] + " from " + entry.Declarations + " files";
				}
			}
			return (list.Length == 0) ? "" : " (the ring closes across layered designs: " + list + ")";
		}

		/// <summary>Whether any finding in a list is a <see cref="CatalogueSeverity.Fault"/>.
		/// </summary>
		public static bool AnyFault(IEnumerable<CatalogueFinding> Findings)
		{
			if (Findings == null)
			{
				return false;
			}
			foreach (CatalogueFinding finding in Findings)
			{
				if (finding != null && finding.Severity == CatalogueSeverity.Fault)
				{
					return true;
				}
			}
			return false;
		}

		// The stage word is KingdomUpgradeRules' - one register for one idea, rather than a second
		// table here that could drift from the one the founder already reads in the ledger.
		private static string StageWord(GrowthStage Stage)
		{
			return "a " + KingdomUpgradeRules.StageWord(Stage);
		}

		// KingdomPlotRules.SizeName answers with an empty string for PlotSize.None, correctly: a
		// single-cell work has no tier to name. A finding still has to be a sentence, so it gets
		// one here rather than reading "stands on a  plot".
		private static string PlotWord(KingdomPlotRules.PlotSize Plot)
		{
			return (Plot == KingdomPlotRules.PlotSize.None) ? "no plot at all" : ("a " + KingdomPlotRules.SizeName(Plot) + " plot");
		}

		/// <returns>True when this entry is offered to every style there is.</returns>
		private static bool CollectStyles(CatalogueEntry Entry, List<string> Into)
		{
			string styles = Entry.Styles;
			if (string.IsNullOrEmpty(styles))
			{
				return false;
			}
			bool takesAll = false;
			string[] parts = styles.Split(ListSeparator);
			for (int i = 0; i < parts.Length; i++)
			{
				string part = Fold(parts[i]);
				if (part == null)
				{
					continue;
				}
				if (part == "all")
				{
					takesAll = true;
					continue;
				}
				if (!Into.Contains(part))
				{
					Into.Add(part);
				}
			}
			return takesAll;
		}

		private static int Least(int A, int B, int C)
		{
			int least = (A < B) ? A : B;
			return (least < C) ? least : C;
		}

		private static bool Contains(string[] Set, string Value)
		{
			if (Value == null)
			{
				return false;
			}
			for (int i = 0; i < Set.Length; i++)
			{
				if (Set[i] == Value)
				{
					return true;
				}
			}
			return false;
		}

		private static List<string> Fold(List<string> Values)
		{
			List<string> folded = new List<string>();
			for (int i = 0; i < Values.Count; i++)
			{
				string value = Fold(Values[i]);
				if (value != null && !folded.Contains(value))
				{
					folded.Add(value);
				}
			}
			return folded;
		}

		/// <summary>Trims and lower-cases one token. Null for anything that was only space, so
		/// every caller has one thing to test rather than two.</summary>
		private static string Fold(string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return null;
			}
			string trimmed = Value.Trim().ToLowerInvariant();
			return (trimmed.Length == 0) ? null : trimmed;
		}
	}
}
