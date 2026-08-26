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

}
