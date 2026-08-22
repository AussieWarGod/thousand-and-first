using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// How far a change to one catalogue attribute reaches once a settlement has already raised
	/// something from the design that attribute belongs to.
	/// <para>
	/// The addendum's guardrail in three words: <em>merges shape future commissions only</em>. This
	/// enum is that guardrail made enumerable, so it can be checked rather than believed.
	/// </para>
	/// </summary>
	public enum MergeReach
	{
		/// <summary>Already paid, the day the work went up. Water poured, days worked, material
		/// consumed. A later file may say the design was always cheaper; the settlement is not
		/// refunded and is not charged again.</summary>
		Spent = 0,

		/// <summary>Already cut into the ground: the blueprint standing there, the plot tier, the
		/// footprint inside it, the roof state, the table the interior was furnished from. Applying
		/// a merged value here would move or delete objects the settlement raised and the founder
		/// has been living beside, which the protection law forbids outright.</summary>
		Stamped = 1,

		/// <summary>Read again every time the question is asked, so a merge lands on standing works
		/// as well as future ones: what the design is called, what it carries, what crew it wants,
		/// what it may grow into, and what it may be re-dressed in. This is the half a rebalance is
		/// supposed to reach, and it destroys nothing when it does.</summary>
		Read = 2
	}

	/// <summary>One attribute as a file wrote it. <see cref="Value"/> is the raw string; an
	/// attribute the file did not write is absent from the draft entirely rather than present and
	/// null, because that distinction is the whole of merge-by-key.</summary>
	public sealed class DraftAttribute
	{
		public string Name;

		public string Value;

		public DraftAttribute(string Name, string Value)
		{
			this.Name = Name;
			this.Value = Value;
		}
	}

	/// <summary>
	/// One <c>&lt;building&gt;</c> element as the merge layer sees it: its key, the attributes this
	/// file actually named, and the skins declared under it.
	/// <para>
	/// Deliberately a bag of raw strings rather than a parsed entry. Merge happens <em>before</em>
	/// any parser runs, so that <c>KingdomRules.TryParseBuildAttributes</c>,
	/// <c>KingdomZoning.RegisterGate</c>, <c>KingdomUpgrade.RegisterChain</c>,
	/// <c>KingdomMaterials.RegisterCost</c> and <c>KingdomPlots.RegisterSpec</c> each go on reading
	/// exactly what they always read &mdash; a string per attribute &mdash; and none of them has to
	/// learn that catalogues layer. It also means an attribute a later wave invents (a tier's
	/// footprint, a roof state, a third party's own) merges correctly the day it is added, with no
	/// change here.
	/// </para>
	/// </summary>
	public sealed class BuildingDraft
	{
		public string Key;

		/// <summary>A label for the file that most recently named this key, when the caller has one
		/// to give. Null everywhere the loader cannot say, which is not a defect: the merge counts
		/// declarations regardless, and a count is enough to tell a modder that the design they are
		/// reading is not the one their file wrote.</summary>
		public string Origin;

		/// <summary>How many <c>&lt;building&gt;</c> elements have been folded into this draft. One
		/// for a design only its own file declares.</summary>
		public int Declarations = 1;

		/// <summary>Every attribute this draft names, in the order first named. Attribute names are
		/// matched case-insensitively; the loader passes our own constants, so the case never
		/// varies in practice.</summary>
		public readonly List<DraftAttribute> Attributes = new List<DraftAttribute>();

		/// <summary>Appearances, in offer order. Null &mdash; never an empty list &mdash; for a
		/// design that declares none, matching <c>KingdomRules.BuildEntry.Skins</c>.</summary>
		public List<KingdomDesignRules.SkinEntry> Skins;

		/// <summary>Skin keys the element currently being read has declared, so declaring one skin
		/// key twice in ONE element is still refused while the same key in a LATER file replaces.
		/// Cleared by <see cref="KingdomMergeRules.Merge"/> at the top of every merge.</summary>
		public readonly List<string> SkinKeysThisPass = new List<string>();

		public BuildingDraft()
		{
		}

		public BuildingDraft(string Key, string Origin = null)
		{
			this.Key = Key;
			this.Origin = Origin;
		}

		/// <summary>
		/// Records one attribute as this file named it. A null <paramref name="Value"/> is an
		/// attribute the file did not write, and is not recorded &mdash; that is what lets an
		/// omitted attribute survive a merge. An empty string IS a value: a file that writes
		/// <c>Contents=""</c> has said "no table", and every downstream parser reads blank as the
		/// default, so clearing an inherited attribute is spelled exactly that way.
		/// <para>
		/// That distinction is load-bearing and it holds against the engine:
		/// <c>XmlDataHelper.GetAttribute</c> is <c>XmlReader.GetAttribute</c> under a CP437
		/// conversion that returns null unchanged, and the engine's own <c>HasAttribute</c> is
		/// written as <c>GetAttribute(name) != null</c>. An absent attribute reads null; a blank
		/// one reads empty; nothing collapses the two.
		/// </para>
		/// </summary>
		public void Set(string Name, string Value)
		{
			if (string.IsNullOrEmpty(Name) || Value == null)
			{
				return;
			}
			DraftAttribute attribute = Find(Name);
			if (attribute == null)
			{
				Attributes.Add(new DraftAttribute(Name, Value));
				return;
			}
			attribute.Value = Value;
		}

		/// <summary>What this draft says one attribute is, or null when no file has named it.
		/// </summary>
		public string Get(string Name)
		{
			DraftAttribute attribute = Find(Name);
			return (attribute == null) ? null : attribute.Value;
		}

		/// <summary>Whether any file folded into this draft named the attribute at all.</summary>
		public bool Names(string Name)
		{
			return Find(Name) != null;
		}

		/// <summary>
		/// An independent draft with the same attributes and the same skin list. The skin entries
		/// themselves are shared rather than cloned, on purpose: replacing a skin swaps the
		/// reference in the list and never edits the entry another draft is holding.
		/// </summary>
		public BuildingDraft Copy()
		{
			BuildingDraft copy = new BuildingDraft(Key, Origin);
			copy.Declarations = Declarations;
			for (int i = 0; i < Attributes.Count; i++)
			{
				copy.Attributes.Add(new DraftAttribute(Attributes[i].Name, Attributes[i].Value));
			}
			if (Skins != null)
			{
				copy.Skins = new List<KingdomDesignRules.SkinEntry>(Skins);
			}
			for (int i = 0; i < SkinKeysThisPass.Count; i++)
			{
				copy.SkinKeysThisPass.Add(SkinKeysThisPass[i]);
			}
			return copy;
		}

		private DraftAttribute Find(string Name)
		{
			if (string.IsNullOrEmpty(Name))
			{
				return null;
			}
			for (int i = 0; i < Attributes.Count; i++)
			{
				if (string.Equals(Attributes[i].Name, Name, System.StringComparison.OrdinalIgnoreCase))
				{
					return Attributes[i];
				}
			}
			return null;
		}
	}

	/// <summary>
	/// A work the settlement has already raised, as the merge layer needs to see it: the design key
	/// it was raised under, the dress it is wearing, and the draft it was raised FROM.
	/// <para>
	/// Holding the whole raised draft rather than a handful of numbers is what makes the guardrail
	/// checkable for attributes nobody has invented yet: whatever a later wave adds to the schema,
	/// the work still carries what that attribute said on the day it went up.
	/// </para>
	/// </summary>
	public sealed class StandingWork
	{
		public string Key;

		/// <summary>The skin the founder chose when they commissioned it, or null for the design's
		/// own look.</summary>
		public string SkinKey;

		/// <summary>The catalogue draft as it read the day this work was raised. Null for a work
		/// raised before the loader kept one, which reconciles to "everything the merge says is
		/// new" and still rewrites nothing.</summary>
		public BuildingDraft Raised;

		public StandingWork()
		{
		}

		public StandingWork(string Key, BuildingDraft Raised, string SkinKey = null)
		{
			this.Key = Key;
			this.Raised = Raised;
			this.SkinKey = SkinKey;
		}
	}

	/// <summary>
	/// What a standing work sees after somebody's mod update changed the catalogue under it: the
	/// materialised record it keeps, and the offers that follow the merge.
	/// </summary>
	public sealed class MergeOffer
	{
		public string Key;

		/// <summary>The draft this work was raised from, copied. Never the merged draft, and never
		/// the caller's own instance &mdash; a caller that edits this cannot reach the work.
		/// </summary>
		public BuildingDraft Raised;

		/// <summary>What the founder reads now. A rename lands: it moves nothing.</summary>
		public string DisplayName;

		/// <summary>What an improvement offers now, which is how "chains extendable by later files"
		/// reaches a hut that is already standing.</summary>
		public string SuccessorKey;

		/// <summary>Every skin a re-dress may now apply, including ones a later file added.
		/// </summary>
		public List<string> SkinKeys = new List<string>();

		/// <summary>The dress this work is wearing.</summary>
		public string WearingSkinKey;

		/// <summary>True when the skin this work is wearing is no longer declared anywhere. The
		/// work goes on wearing it &mdash; its render was stamped when it was raised &mdash; but a
		/// re-dress can no longer put it back, which is worth one line rather than a surprise.
		/// </summary>
		public bool WearingSkinWithdrawn;

		/// <summary>Every <see cref="MergeReach.Spent"/> or <see cref="MergeReach.Stamped"/>
		/// attribute the merge changed and this work is keeping anyway. Empty is the ordinary case.
		/// </summary>
		public List<string> Diverged = new List<string>();
	}

	/// <summary>
	/// Engine-free rules for layering catalogue files: what a later <c>&lt;building Key="X"&gt;</c>
	/// does to the design an earlier file already declared, and what it is forbidden to do to
	/// anything the settlement has already built.
	/// <para>
	/// <b>The contract.</b> A later declaration of a key <em>merges</em>. Attributes it names
	/// override; attributes it omits survive; <c>&lt;skin&gt;</c> children append, with a repeated
	/// skin key replacing the earlier skin in place rather than shadowing it; an upgrade chain is
	/// extended by whichever file names the next link. Load order is file order, which is the mod
	/// system's own priority &mdash; this file never sorts anything and never decides who wins by
	/// any rule but "later".
	/// </para>
	/// <para>
	/// <b>The guardrail.</b> Merges shape future commissions only. A standing work keeps the ground
	/// it was cut, the object standing on it, and the water it was raised with, whatever a mod
	/// update later says the design costs (<see cref="Reconcile"/>, <see cref="MergeReach"/>). What
	/// a settlement re-reads every pass &mdash; names, contributions, crews, chains, skins &mdash;
	/// does follow the merge, because that is what a rebalance is for and it moves nothing.
	/// </para>
	/// <para>
	/// <b>What this file never does.</b> Parse an attribute, reject an entry, or touch a registry.
	/// Merging is string-level on purpose, so every parser downstream reads what it always read and
	/// an attribute invented in a later wave layers correctly with no change here.
	/// </para>
	/// </summary>
	public static class KingdomMergeRules
	{
		// --- The schema, named once ---------------------------------------------------------

		public const string AttrDisplayName = "DisplayName";

		public const string AttrBlueprint = "Blueprint";

		public const string AttrCost = "Cost";

		public const string AttrTicks = "Ticks";

		public const string AttrStyles = "Styles";

		public const string AttrCategory = "Category";

		public const string AttrMinStage = "MinStage";

		public const string AttrStaff = "Staff";

		public const string AttrManning = "Manning";

		public const string AttrDefence = "Defence";

		public const string AttrCarries = "Carries";

		public const string AttrMaterials = "Materials";

		public const string AttrDistricts = "Districts";

		public const string AttrMinZones = "MinZones";

		public const string AttrKnowledge = "Knowledge";

		public const string AttrMinTech = "MinTech";

		/// <summary>Who must be living here for the design to be raised at all (Addendum 16
		/// clause 1): a comma list in the <c>kind:name</c> language <see cref="AttrKnowledge"/>
		/// already uses, optionally with a count. Deliberately absent from
		/// <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/> for exactly
		/// <see cref="AttrKnowledge"/>'s reason &mdash; a gate is asked again every time somebody
		/// tries to raise the design, and re-writing it moves nothing already standing.</summary>
		public const string AttrBuilders = "Builders";

		/// <summary>The creed a design belongs to (Addendum 16 clause 4): raised only by builders
		/// who hold it, or have previously held it. One faction name. A gate, so it reads again
		/// like the rest of them.</summary>
		public const string AttrCreed = "Creed";

		/// <summary>How much of the city must hold <see cref="AttrCreed"/> (Addendum 16 clause 2),
		/// in whole percent. A gate, so it reads again.</summary>
		public const string AttrCreedShare = "CreedShare";

		public const string AttrUpgradesTo = "UpgradesTo";

		public const string AttrUpgradeCost = "UpgradeCost";

		public const string AttrUpgradeTicks = "UpgradeTicks";

		public const string AttrUpgradeCrew = "UpgradeCrew";

		public const string AttrUpgradeMinStage = "UpgradeMinStage";

		public const string AttrUpgradeMaterials = "UpgradeMaterials";

		public const string AttrPlot = "Plot";

		public const string AttrOpen = "Open";

		public const string AttrSky = "Sky";

		public const string AttrContents = "Contents";

		/// <summary>A tier's own footprint, which the plot is merely the envelope for. Named here
		/// so the merge and the validator agree on the spelling even while the parser for it lands
		/// separately.</summary>
		public const string AttrFootprint = "Footprint";

		/// <summary>A tier's roof state: <c>Open</c>, <c>Soft</c>, <c>Walled</c>, <c>Carved</c>.
		/// </summary>
		public const string AttrRoof = "Roof";

		/// <summary>The quality-of-life tags a design offers whoever lives or works in it
		/// (Addendum 4). Named here so the merge and the loader agree on the spelling. Deliberately
		/// absent from <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/>: what a
		/// building provides is read again every time somebody asks whether they will live there,
		/// which is what <see cref="MergeReach.Read"/> already means for everything this file does
		/// not name, and it moves nothing when a mod changes it.</summary>
		public const string AttrProvides = "Provides";

		/// <summary>How close the quarters a design's residents keep are (Addendum 4c):
		/// <c>Packed</c>, <c>Close</c>, <c>Roomed</c>, <c>Private</c>. An override for the
		/// beds-per-footprint derivation, and absent from every design content to be measured.
		/// Named here so the merge and the loader agree on the spelling. Deliberately absent from
		/// <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/> for exactly
		/// <see cref="AttrProvides"/>'s reason: how close a roof holds people is re-read every time
		/// somebody asks whether they will live under it, and changing it moves nothing.</summary>
		public const string AttrCloseness = "Closeness";

		/// <summary>How far what a design gives actually carries (Addendum 6): <c>plot</c>,
		/// <c>quarter</c>, <c>zone</c>, <c>city</c>, <c>realm</c>. An override for the derivation
		/// from plot size and chain position, and absent from every design content to be derived.
		/// Named here so the merge and the loader agree on the spelling. Deliberately absent from
		/// <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/> for exactly
		/// <see cref="AttrProvides"/>'s reason: how far a work carries is re-read every time
		/// somebody asks, and changing it moves nothing.</summary>
		public const string AttrReach = "Reach";

		/// <summary>What a design's crew needs to be capable of to raise it at full pace
		/// (Addendum 7): a <c>kind:amount</c> list in the same language as
		/// <see cref="AttrCarries"/>, e.g. <c>strength:16</c>. Named here so the merge and the
		/// loader agree on the spelling. Deliberately absent from <see cref="SpentAttributes"/> and
		/// <see cref="StampedAttributes"/> for exactly <see cref="AttrProvides"/>'s reason: what a
		/// work demands of its crew is re-read every time it is crewed, and a rebalance reaches a
		/// work already standing rather than the crew it happened to draw the day it was raised.
		/// </summary>
		public const string AttrCrewNeeds = "CrewNeeds";

		/// <summary>What a high-craft design costs in vanilla's own tinkering bits (Addendum 7),
		/// written in the game's own bit tiers: <c>Bits="0034"</c>. Named here so the merge and the
		/// loader agree on the spelling. Belongs to <see cref="SpentAttributes"/> for exactly
		/// <see cref="AttrCost"/>'s reason: bits are paid out the day the work goes up, so a later
		/// file that re-prices the design neither charges nor refunds a work that already stands.
		/// </summary>
		public const string AttrBits = "Bits";

		/// <summary>The rare finds a great work is finished in (Addendum 7):
		/// <c>Exotics="gold:2,gem:1"</c>. Spent at commission, so it joins
		/// <see cref="SpentAttributes"/> beside <see cref="AttrBits"/> and
		/// <see cref="AttrMaterials"/>.</summary>
		public const string AttrExotics = "Exotics";

		/// <summary>What a processing work turns raw stock into: <c>Refines="shapedstone"</c>, or
		/// the yard's own key. Deliberately absent from <see cref="SpentAttributes"/> and
		/// <see cref="StampedAttributes"/>: what a standing yard makes is asked again every pass, so
		/// a mod that re-purposes a yard changes what comes off it tomorrow and moves nothing today.
		/// </summary>
		public const string AttrRefines = "Refines";

		/// <summary>What <c>KingdomRules.TryParseBuildAttributes</c> refuses an entry for the want
		/// of. A later file may omit every one of them &mdash; that is a merge &mdash; but the
		/// design as a whole must end up with all four.</summary>
		public static readonly string[] RequiredAttributes = new string[4] { AttrDisplayName, AttrBlueprint, AttrCost, AttrTicks };

		/// <summary>Attributes whose value was paid out when the work went up.</summary>
		public static readonly string[] SpentAttributes = new string[5] { AttrCost, AttrTicks, AttrMaterials, AttrBits, AttrExotics };

		/// <summary>Attributes whose value is cut into the ground the work stands on.</summary>
		public static readonly string[] StampedAttributes = new string[6] { AttrBlueprint, AttrPlot, AttrFootprint, AttrRoof, AttrOpen, AttrContents };

		/// <summary>
		/// How far a change to this attribute reaches. Anything this file does not name reads
		/// again: an attribute belonging to a third party's own system cannot have been spent by
		/// our economy or stamped on our ground, so treating it as read-again is both the safe
		/// answer and the true one.
		/// </summary>
		public static MergeReach Classify(string Attribute)
		{
			if (Contains(SpentAttributes, Attribute))
			{
				return MergeReach.Spent;
			}
			return Contains(StampedAttributes, Attribute) ? MergeReach.Stamped : MergeReach.Read;
		}

		/// <summary>Whether a merge changing this attribute may reach a work that already stands.
		/// </summary>
		public static bool ReachesStandingWork(string Attribute)
		{
			return Classify(Attribute) == MergeReach.Read;
		}

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

		// --- The load-order table -----------------------------------------------------------

		private static readonly Dictionary<string, BuildingDraft> _drafts = new Dictionary<string, BuildingDraft>();

		private static readonly List<CatalogueFinding> _findings = new List<CatalogueFinding>();

		/// <summary>Everything the merges had to say, in the order the elements were read. Reported
		/// with the rest of the catalogue's findings so one load produces one log.</summary>
		public static List<CatalogueFinding> Findings => _findings;

		/// <summary>Forgets every draft and every finding. Called by the loader before it re-reads
		/// the XML streams, beside <c>KingdomZoning.ClearGates</c>.</summary>
		public static void ClearDrafts()
		{
			_drafts.Clear();
			_findings.Clear();
		}

		/// <summary>
		/// Folds one element's draft into whatever earlier files declared under the same key, keeps
		/// the result as the design of record, and hands it back for parsing and registration.
		/// <para>
		/// One pass, one read: everything the registries need comes out of the returned draft, so
		/// no stream is ever read twice and no attribute goes unasked-for.
		/// </para>
		/// </summary>
		public static BuildingDraft Absorb(BuildingDraft Later)
		{
			if (Later == null || string.IsNullOrEmpty(Later.Key))
			{
				return Later;
			}
			BuildingDraft standing;
			_drafts.TryGetValue(Later.Key, out standing);
			BuildingDraft merged = Merge(standing, Later, _findings);
			_drafts[Later.Key] = merged;
			return merged;
		}

		/// <summary>The design of record for a key: every file that named it, folded.</summary>
		public static bool TryGetDraft(string Key, out BuildingDraft Draft)
		{
			Draft = null;
			return !string.IsNullOrEmpty(Key) && _drafts.TryGetValue(Key, out Draft) && Draft != null;
		}

		/// <summary>How many declarations a key's design is the merge of. Zero for a key no file
		/// named.</summary>
		public static int DeclarationsOf(string Key)
		{
			BuildingDraft draft;
			return TryGetDraft(Key, out draft) ? draft.Declarations : 0;
		}

		// --- The guardrail ------------------------------------------------------------------

		/// <summary>
		/// What a work that is already standing sees once the catalogue under it has been merged
		/// into.
		/// <para>
		/// The materialised half &mdash; every <see cref="MergeReach.Spent"/> and
		/// <see cref="MergeReach.Stamped"/> attribute &mdash; comes back exactly as the work was
		/// raised, whatever the merged draft now says, and comes back as a copy so no caller can
		/// reach the work through it. The read half follows the merge, which is how a later file's
		/// skin becomes available to re-dress a standing house and how a later file's chain link
		/// becomes something a standing hut can climb.
		/// </para>
		/// </summary>
		/// <param name="Work">The standing work. Null returns null.</param>
		/// <param name="Merged">The design of record after every file has been folded in. Null
		/// reads as "nothing changed".</param>
		public static MergeOffer Reconcile(StandingWork Work, BuildingDraft Merged)
		{
			if (Work == null)
			{
				return null;
			}
			BuildingDraft raised = Work.Raised;
			MergeOffer offer = new MergeOffer
			{
				Key = Work.Key,
				Raised = (raised == null) ? null : raised.Copy(),
				WearingSkinKey = Work.SkinKey
			};
			BuildingDraft reading = Merged ?? raised;
			if (reading != null)
			{
				offer.DisplayName = reading.Get(AttrDisplayName);
				offer.SuccessorKey = reading.Get(AttrUpgradesTo);
				if (reading.Skins != null)
				{
					for (int i = 0; i < reading.Skins.Count; i++)
					{
						if (reading.Skins[i] != null && !string.IsNullOrEmpty(reading.Skins[i].Key))
						{
							offer.SkinKeys.Add(reading.Skins[i].Key);
						}
					}
				}
			}
			if (string.IsNullOrEmpty(offer.DisplayName))
			{
				offer.DisplayName = Work.Key;
			}
			offer.WearingSkinWithdrawn = !string.IsNullOrEmpty(Work.SkinKey) && !offer.SkinKeys.Contains(Work.SkinKey);
			if (Merged != null && raised != null)
			{
				Diverge(raised, Merged, SpentAttributes, offer.Diverged);
				Diverge(raised, Merged, StampedAttributes, offer.Diverged);
			}
			return offer;
		}

		/// <summary>
		/// One line for the founder when a mod update changed a design under something they already
		/// built, or null when nothing that matters changed (STANDARDS 7b: say it once, and only
		/// when there is something to say).
		/// </summary>
		/// <param name="Name">What the founder calls the standing work.</param>
		public static string StandingLine(string Name, MergeOffer Offer)
		{
			if (Offer == null || Offer.Diverged.Count == 0)
			{
				return null;
			}
			string name = string.IsNullOrEmpty(Name) ? "the work" : Name;
			return "The plans for " + name + " have been redrawn, but the one that stands keeps the ground it was cut and the water it was raised with.";
		}

		private static void Diverge(BuildingDraft Raised, BuildingDraft Merged, string[] Attributes, List<string> Into)
		{
			for (int i = 0; i < Attributes.Length; i++)
			{
				string attribute = Attributes[i];
				if (!Same(Raised.Get(attribute), Merged.Get(attribute)) && !Into.Contains(attribute))
				{
					Into.Add(attribute);
				}
			}
		}

		// --- Small shared helpers -----------------------------------------------------------

		/// <summary>Whether two raw attribute values say the same thing. Absent and blank are the
		/// same thing, because every parser downstream reads blank as the default.</summary>
		private static bool Same(string A, string B)
		{
			string a = (A == null) ? "" : A.Trim();
			string b = (B == null) ? "" : B.Trim();
			return a == b;
		}

		private static bool Contains(string[] Set, string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return false;
			}
			for (int i = 0; i < Set.Length; i++)
			{
				if (string.Equals(Set[i], Value, System.StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		private static void Add(List<CatalogueFinding> Findings, CatalogueFinding Finding)
		{
			if (Findings != null && Finding != null)
			{
				Findings.Add(Finding);
			}
		}

		private static string Join(List<string> Names)
		{
			if (Names == null || Names.Count == 0)
			{
				return "nothing";
			}
			if (Names.Count == 1)
			{
				return Names[0];
			}
			string joined = "";
			for (int i = 0; i < Names.Count; i++)
			{
				if (i > 0)
				{
					joined += (i == Names.Count - 1) ? " and " : ", ";
				}
				joined += Names[i];
			}
			return joined;
		}
	}
}
