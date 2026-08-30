using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Closed set of production entry points a scenario may drive. A scenario cannot name anything
	/// outside this enum, so the harness can never reach a production authority the reviewer has
	/// not admitted. Each verb also carries a closed argument schema; a closed enum over an open
	/// dictionary would still be an open authority surface.
	/// </summary>
	internal enum KingdomScenarioVerb : byte
	{
		None = 0,

		/// <summary>Read-only: proves the authored architecture catalogue is fit to review.</summary>
		ProveCatalogue = 1,

		/// <summary>
		/// Mutating: drives the production architecture gallery staging path for one exact case
		/// and pose. The single production transaction a phase-1 scenario is allowed.
		/// </summary>
		StageGalleryCase = 2
	}

	/// <summary>One declared parameter and the exact closed domain its value must come from.</summary>
	internal sealed class KingdomScenarioParameter
	{
		internal string Name;
		internal IList<string> Domain = new List<string>();
	}

	/// <summary>
	/// One ordered authored step. Argument values may reference a declared parameter as
	/// <c>{name}</c>; preflight resolves those, and only resolved arguments reach a plan.
	/// </summary>
	internal sealed class KingdomScenarioStep
	{
		internal KingdomScenarioVerb Verb;
		internal IDictionary<string, string> Arguments =
			new Dictionary<string, string>(StringComparer.Ordinal);
	}

	/// <summary>A step whose arguments are fully resolved and schema-checked. Realization reads
	/// only these; it never rereads the authored definition or the caller's selection.</summary>
	internal sealed class KingdomScenarioResolvedStep
	{
		internal KingdomScenarioVerb Verb;
		internal IDictionary<string, string> Arguments =
			new Dictionary<string, string>(StringComparer.Ordinal);
	}

	/// <summary>
	/// One authored scenario. Load-time data only: never persisted, so it carries no wire version
	/// beyond the registry Schema attribute the shared guard already reads.
	/// </summary>
	internal sealed class KingdomScenarioDefinition
	{
		internal string Key;
		internal string Family;

		/// <summary>
		/// Production authority this scenario exercises. Each distinct class needs its own
		/// ordinary-play differential anchor before any sibling verdict counts.
		/// </summary>
		internal string AuthorityClass;

		/// <summary>
		/// Raw authored Synthetic text. Kept verbatim so a malformed value is a registry finding
		/// rather than a silent downgrade to "not synthetic".
		/// </summary>
		internal string SyntheticRaw;

		/// <summary>Ordinary-play anchor this scenario leans on; null until one is recorded.</summary>
		internal string AnchorId;

		internal string DisplayName;
		internal string Description;

		/// <summary>
		/// Which mod authored this row. The roster loads through
		/// <c>DataManager.YieldXMLStreamsWithRoot</c>, so a third-party mod extends it just by
		/// shipping a file with the same root element - and a merged roster whose rows are
		/// anonymous cannot tell an operator whose scenario just refused. Taken from the stream's
		/// own <c>XmlDataHelper.modInfo.ID</c>, never asserted by the row, so a row cannot claim
		/// another mod's name. Empty for a base-game stream, which is not a case that occurs today.
		/// </summary>
		internal string Owner;

		internal IList<KingdomScenarioParameter> Parameters = new List<KingdomScenarioParameter>();
		internal IList<KingdomScenarioStep> Steps = new List<KingdomScenarioStep>();
	}

	/// <summary>
	/// A definition bound to one exact parameter selection, fully resolved before any mutation.
	/// Holding a plan is the proof that preflight passed; realization takes nothing else.
	/// </summary>
	internal sealed class KingdomScenarioPlan
	{
		internal string Key;
		internal string AuthorityClass;
		internal string Seed;
		internal bool Synthetic;
		internal string AnchorId;

		internal IDictionary<string, string> Bindings =
			new Dictionary<string, string>(StringComparer.Ordinal);

		/// <summary>Exact ordered verb sequence, joined by '+', recorded in provenance verbatim.</summary>
		internal string Verbs;

		/// <summary>Digest over the whole authored registry, not just this row.</summary>
		internal string DefinitionDigest;

		/// <summary>
		/// Canonical digest over this exact resolved plan: key, authority, verb sequence, bound
		/// parameter selection, and every resolved step argument. Recorded in the stamp so an
		/// attended run can prove it is executing the plan that was stamped, not a later request.
		/// </summary>
		internal string PlanDigest;

		internal IList<KingdomScenarioResolvedStep> Steps =
			new List<KingdomScenarioResolvedStep>();
	}

	/// <summary>
	/// Frozen evidence that one ordinary-play differential anchor was reached and measured.
	/// <para>
	/// Authority constraint: this record is supplied independently by a reviewer from a curated,
	/// checked-in source. It is never read from a save, never produced by the scenario registry,
	/// and never derived from a scenario-built state; those paths would let the harness sign its
	/// own evidence, which is exactly what the governing ruling forbids.
	/// </para>
	/// </summary>
	internal sealed class KingdomScenarioAnchorEvidence
	{
		internal string AnchorId;
		internal string AuthorityClass;

		/// <summary>
		/// The scenario recipe this anchor AUTHORIZES - not a record of how the ordinary state was
		/// reached.
		/// <para>
		/// The curated row copies the scenario's verb sequence, which ordinary play did not execute:
		/// a reviewer commissions a building through the game, they do not run
		/// <c>provecatalogue+stagegallerycase</c>. Binding it as the authorized recipe is the honest
		/// reading, and it is what the signing law compares. Calling it observed causal provenance
		/// would be describing evidence nobody collected.
		/// </para>
		/// </summary>
		internal string Verbs;

		internal string KeySetDigest;
		internal string DefinitionDigest;
		internal string PlanDigest;
		internal string ModVersion;
		internal string QudCoreVersion;

		/// <summary>How the anchor state was reached. Only ordinary play may found an anchor.</summary>
		internal KingdomScenarioAnchorRules.Provenance Reached =
			KingdomScenarioAnchorRules.Provenance.Unknown;
	}
}
