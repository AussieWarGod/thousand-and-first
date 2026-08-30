using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Marks a class as a scenario verb provider. Discovery is the engine's own idiom &mdash;
	/// <c>ModManager.GetTypesWithAttribute(typeof(KingdomScenarioVerbProviderAttribute))</c>, the
	/// same cached attribute scan <c>WishManager</c> uses for wishes
	/// (<c>D/XRL/Wish/WishManager.cs:43</c>) and <c>WorldFactory</c> for world builders
	/// (<c>D/XRL/World/WorldFactory.cs:108</c>) &mdash; so a third-party mod needs no line of ours
	/// beyond this namespace.
	/// <para>
	/// The marked class needs a public parameterless constructor. Construction is guarded per type,
	/// so a provider whose constructor throws refuses itself and never takes another mod's provider
	/// down with it.
	/// </para>
	/// <para>
	/// DEV-ONLY, FOR EVERYONE. This contract lives in <c>Harness/</c>, which is absent from
	/// <c>manifest.json</c> <c>Directories</c> and excluded from <c>Tools/stage.sh</c>, so it is
	/// compiled only inside a throwaway scenario profile. A third-party provider is test code and
	/// belongs in a directory that mod's own dev manifest selects, under the same containment: a
	/// test verb has no business in a Workshop package, theirs or ours.
	/// </para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class KingdomScenarioVerbProviderAttribute : Attribute
	{
	}

	/// <summary>
	/// What a third-party scenario verb provider declares.
	/// <para>
	/// One verb is one report plus one boolean. <c>Ok</c> false means the verb DECLINED TO ACT, and
	/// it is the only thing that stops a sealed script; an observation whose answer is unwelcome -
	/// an ineligible verdict, an empty store - is still an answer and returns true. That is the same
	/// law the harness's own verbs keep, and the journal takes the boolean rather than reading the
	/// prose, so a provider cannot describe its way to a different outcome.
	/// </para>
	/// <para>
	/// Nothing here may show a popup. <c>Popup.Show</c> blocks on a keypress, so a verb that owned
	/// one could never run on a turn nobody is watching - which is the entire point of the
	/// unattended runner.
	/// </para>
	/// </summary>
	public interface IKingdomScenarioVerbProvider
	{
		/// <summary>
		/// The value of <see cref="KingdomScenarioVerbApi.Version"/> this provider was compiled
		/// against. Return the constant, never a literal: recompiling against a newer harness is
		/// what re-admits the provider, and a drifted version is refused by mod name rather than
		/// silently skipped.
		/// </summary>
		int ScenarioVerbApiVersion { get; }

		/// <summary>
		/// The verb names this provider claims, lowercase, matching
		/// <c>KingdomScenarioRules.SafeToken</c>. A name the harness reserves, a malformed name, or
		/// a name repeated inside one provider refuses the WHOLE provider by name.
		/// </summary>
		IEnumerable<string> ScenarioVerbs { get; }

		/// <summary>
		/// Runs one claimed verb. <paramref name="Verb"/> is the lowercased first word and
		/// <paramref name="Argument"/> is the rest of the line, trimmed and possibly empty.
		/// </summary>
		/// <returns>The operator-readable report. It becomes the journal's message column verbatim,
		/// bounded and escaped, so one verb is still one row.</returns>
		string RunScenarioVerb(string Verb, string Argument, out bool Ok);
	}

	/// <summary>
	/// The published contract version and the names a provider may not claim.
	/// </summary>
	internal static class KingdomScenarioVerbApi
	{
		/// <summary>
		/// Bumped when the interface above changes shape. A provider declaring anything else is
		/// refused loudly: a silently inactive provider is worse than a refused one, because the
		/// operator attributes the missing verb to the harness.
		/// </summary>
		internal const int Version = 1;

		/// <summary>
		/// Names the harness itself dispatches. A provider claiming one of these is refused
		/// ENTIRELY rather than shadowing it.
		/// <para>
		/// This is the one place the "a collision leaves nobody holding the name" law bends, and
		/// deliberately: <c>realize</c> is the single mutating production transaction, and a third
		/// party that could revoke it - by claiming it and having both sides drop out - could make
		/// every scenario verdict on the machine unfalsifiable while the journal still read green.
		/// Provider-versus-provider collisions below take the unbent form: nobody wins the name.
		/// </para>
		/// </summary>
		internal static readonly string[] Reserved =
		{
			"advance", "anchor", "capture", "flatten", "ground", "help", "list", "realize",
			"status"
		};

		/// <summary>Whether a claimed name is one of the reserved built-ins.</summary>
		internal static bool IsReserved(string Verb)
		{
			for (int i = 0; i < Reserved.Length; i++)
				if (string.Equals(Reserved[i], Verb, StringComparison.Ordinal)) return true;
			return false;
		}
	}
}
