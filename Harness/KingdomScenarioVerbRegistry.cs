using System;
using System.Collections.Generic;

using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Third-party scenario verbs: discovery, admission, and the one place they are run.
	/// <para>
	/// THE SCAN IS THE ENGINE'S, THE CONSTRUCTION IS OURS.
	/// <c>ModManager.GetTypesWithAttribute</c> (<c>D/XRL/ModManager.cs:1185-1196</c>) is the same
	/// cached attribute scan the engine runs for wishes and world builders, so a third-party mod is
	/// discovered without registering anything with us. Its sibling
	/// <c>GetInstancesWithAttribute&lt;T&gt;</c> would construct as well, but it does so UNGUARDED
	/// over every marked type, so one provider with no parameterless constructor would throw out of
	/// the middle of the loop and take every other mod's verbs with it. Only the per-type
	/// construction moved inside a guard; the scan is untouched.
	/// </para>
	/// <para>
	/// ONE DISPATCH PATH. <see cref="KingdomScenarioVerbs.Invoke"/> is still the only entry the wish
	/// and the auto-runner call, and it still writes exactly one journal row per invocation. This
	/// shard supplies the second SOURCE of verbs behind that one path - the harness's own closed
	/// set first, extensions after - so an extension verb produces the same row grammar, the same
	/// <c>OK|REFUSED</c> law, and the same script-stops-on-refusal behaviour as a built-in.
	/// </para>
	/// <para>
	/// DETERMINISTIC. Claims are ordinal-sorted by owner then type before admission, because
	/// <c>ActiveTypes</c> order is the player's mod list and a verb table that depended on it would
	/// differ between two installs carrying the same mods.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioVerbRegistry
	{
		/// <summary>Journal verb column for a provider the admission law refused.</summary>
		internal const string RefusedRow = "VERB-REFUSED";

		/// <summary>Refusal code for a provider verb that threw while running.</summary>
		internal const string CodeThrew = "taf-scenario-verb-threw";

		private static List<IKingdomScenarioVerbProvider> Providers;

		private static List<KingdomScenarioVerbClaim> Claims;

		private static KingdomScenarioVerbAdmission Admission;

		/// <summary>Refusals are journalled once per build, never once per verb lookup.</summary>
		private static bool Announced;

		/// <summary>Rebuilds on next ask. The mod list is fixed for a sealed profile, so this
		/// exists for the test path and for a mod-list change inside one process.</summary>
		internal static void Invalidate()
		{
			Providers = null;
			Claims = null;
			Admission = null;
			Announced = false;
		}

		/// <summary>Admitted verb names, ordinal-sorted. Empty when no provider is installed.</summary>
		internal static IList<string> Verbs
		{
			get
			{
				EnsureBuilt();
				List<string> names = new List<string>(Admission.ByVerb.Keys);
				names.Sort(StringComparer.Ordinal);
				return names;
			}
		}

		/// <summary>Refusal lines from the last admission, each carrying a stable code.</summary>
		internal static IList<string> Refusals
		{
			get { EnsureBuilt(); return Admission.Refusals; }
		}

		/// <summary>The owner label behind one admitted verb, or null.</summary>
		internal static string Owner(string Verb)
		{
			EnsureBuilt();
			int index;
			return Admission.ByVerb.TryGetValue(Verb ?? "", out index) ? Claims[index].Label : null;
		}

		/// <summary>
		/// Runs a verb an extension owns. Returns false when NOBODY owns it, so the caller's own
		/// unknown-verb refusal still stands and a script that names a typo still stops.
		/// <para>
		/// A provider that throws is a REFUSED with a stable code, never an escaped exception: the
		/// auto-runner treats a refusal as a stop, which is what an operator wants, and the fault
		/// is named in the row rather than in a log nobody reads.
		/// </para>
		/// </summary>
		internal static bool TryRun(string Verb, string Argument, out string Message, out bool Ok)
		{
			Message = null;
			Ok = false;
			EnsureBuilt();
			int index;
			if (Verb == null || !Admission.ByVerb.TryGetValue(Verb, out index)) return false;
			IKingdomScenarioVerbProvider provider = Providers[index];
			try
			{
				bool ok;
				string message = provider.RunScenarioVerb(Verb, Argument ?? "", out ok);
				Ok = ok;
				Message = message ?? "";
			}
			catch (Exception exception)
			{
				Ok = false;
				Message = KingdomScenarioVerbProviderRules.Line(CodeThrew, Claims[index].Label,
					"the verb threw: " + KingdomScenarioRules.Bounded(exception.Message));
			}
			return true;
		}

		/// <summary>One operator-readable block naming every admitted verb and every refusal.</summary>
		internal static string Describe()
		{
			EnsureBuilt();
			IList<string> names = Verbs;
			if (names.Count == 0 && Admission.Refusals.Count == 0) return "";
			System.Text.StringBuilder sb =
				new System.Text.StringBuilder("\n\n{{C|Extension verbs}}");
			if (names.Count == 0) sb.Append("\n  none admitted");
			for (int i = 0; i < names.Count; i++)
				sb.Append("\n  {{W|").Append(names[i]).Append("}}  from ")
					.Append(Owner(names[i]));
			for (int i = 0; i < Admission.Refusals.Count; i++)
				sb.Append("\n{{R|verb provider refused}} ").Append(Admission.Refusals[i]);
			return sb.ToString();
		}

		private static void EnsureBuilt()
		{
			if (Admission != null) return;
			List<KingdomScenarioVerbClaim> claims = new List<KingdomScenarioVerbClaim>();
			List<IKingdomScenarioVerbProvider> instances =
				new List<IKingdomScenarioVerbProvider>();
			// The whole scan is guarded: a build with no ModManager surface at all - a unit host,
			// a stripped profile - must leave the harness with zero extension verbs, not broken.
			KingdomSystem.Guard("scenario verb provider scan",
				delegate { Collect(claims, instances); });
			Sort(claims, instances);
			Claims = claims;
			Providers = instances;
			Admission = KingdomScenarioVerbProviderRules.Admit(claims);
			Announce();
		}

		private static void Collect(List<KingdomScenarioVerbClaim> Claims,
			List<IKingdomScenarioVerbProvider> Instances)
		{
			foreach (Type type in ModManager.GetTypesWithAttribute(
				typeof(KingdomScenarioVerbProviderAttribute)))
			{
				if (type == null) continue;
				KingdomScenarioVerbClaim claim = new KingdomScenarioVerbClaim
				{
					Owner = OwnerOf(type),
					TypeName = type.FullName ?? type.Name
				};
				IKingdomScenarioVerbProvider instance = null;
				// Third-party code running before it has been admitted. The constructor, the
				// version getter, and the name enumerator are all asked inside the guard, so any
				// one of them throwing refuses THAT provider rather than the registry.
				KingdomSystem.Guard("scenario verb provider " + claim.TypeName, delegate
				{
					instance = Activator.CreateInstance(type) as IKingdomScenarioVerbProvider;
					if (instance == null) return;
					claim.ApiVersion = instance.ScenarioVerbApiVersion;
					IEnumerable<string> names = instance.ScenarioVerbs;
					if (names != null)
						foreach (string name in names) claim.Verbs.Add(name);
					claim.Constructed = true;
				});
				Claims.Add(claim);
				Instances.Add(instance);
			}
		}

		/// <summary>Ordinal sort by owner then type, carrying the instances with their claims.</summary>
		private static void Sort(List<KingdomScenarioVerbClaim> Claims,
			List<IKingdomScenarioVerbProvider> Instances)
		{
			for (int i = 1; i < Claims.Count; i++)
				for (int j = i; j > 0
					&& string.CompareOrdinal(Claims[j - 1].Label, Claims[j].Label) > 0; j--)
				{
					KingdomScenarioVerbClaim claim = Claims[j - 1];
					Claims[j - 1] = Claims[j];
					Claims[j] = claim;
					IKingdomScenarioVerbProvider instance = Instances[j - 1];
					Instances[j - 1] = Instances[j];
					Instances[j] = instance;
				}
		}

		private static string OwnerOf(Type type)
		{
			ModInfo mod = type.Assembly == null ? null : ModManager.GetMod(type.Assembly);
			if (mod != null && !string.IsNullOrEmpty(mod.ID)) return mod.ID;
			return type.Assembly == null ? "" : (type.Assembly.GetName().Name ?? "");
		}

		/// <summary>
		/// One journal row per refusal, once. Bookkeeping rather than a verb row: it describes the
		/// PROFILE a run was launched into, not a step the script asked for, and a persona must not
		/// go red because somebody else's mod shipped a broken provider. The matrix runner surfaces
		/// these rows in its report either way.
		/// </summary>
		private static void Announce()
		{
			if (Announced) return;
			Announced = true;
			for (int i = 0; i < Admission.Refusals.Count; i++)
			{
				KingdomLog.Log("[TAF scenario] verb provider refused: " + Admission.Refusals[i]);
				KingdomScenarioJournal.Append(RefusedRow, false, Admission.Refusals[i]);
			}
		}
	}
}
