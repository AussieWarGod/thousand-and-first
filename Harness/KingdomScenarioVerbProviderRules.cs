using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>One provider's claim, as the engine-side scan observed it.</summary>
	internal sealed class KingdomScenarioVerbClaim
	{
		/// <summary>Owning mod id, or the assembly name when the type belongs to no mod.</summary>
		internal string Owner;

		internal string TypeName;

		internal int ApiVersion;

		/// <summary>False when the type could not be constructed or is not a provider.</summary>
		internal bool Constructed;

		internal IList<string> Verbs = new List<string>();

		/// <summary>Owner and type together, so two providers in one mod are still told apart.</summary>
		internal string Label
		{
			get
			{
				return (string.IsNullOrEmpty(Owner) ? "(unowned)" : Owner) + "/"
					+ (string.IsNullOrEmpty(TypeName) ? "(untyped)" : TypeName);
			}
		}
	}

	/// <summary>The admitted verb table and every refusal that produced it.</summary>
	internal sealed class KingdomScenarioVerbAdmission
	{
		/// <summary>Verb name to the index of the claim that owns it.</summary>
		internal IDictionary<string, int> ByVerb =
			new Dictionary<string, int>(StringComparer.Ordinal);

		/// <summary>One bounded line per refusal, each carrying a stable code.</summary>
		internal IList<string> Refusals = new List<string>();
	}

	/// <summary>
	/// The pure admission law for third-party scenario verbs. No engine, no reflection, no journal:
	/// the engine-side registry observes, this decides, so every refusal shape executes in the test
	/// assembly without a licensed install.
	/// <para>
	/// FAIL-CLOSED AND ORDER-FREE. A claim is admitted whole or refused whole, and a name two
	/// providers both claim is held by NEITHER - first-registered wins nothing, because "first" is
	/// the player's mod load order and a verb that means different things on two machines with the
	/// same mods is not a test verb.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioVerbProviderRules
	{
		/// <summary>Providers admitted at once. A dev profile with more than this is not a test.</summary>
		internal const int MaxProviders = 64;

		/// <summary>Verbs one provider may claim.</summary>
		internal const int MaxVerbsPerProvider = 32;

		internal const string CodeThrew = "taf-scenario-verb-provider-threw";
		internal const string CodeVersion = "taf-scenario-verb-provider-version";
		internal const string CodeEmpty = "taf-scenario-verb-provider-empty";
		internal const string CodeMalformed = "taf-scenario-verb-malformed";
		internal const string CodeReserved = "taf-scenario-verb-reserved";
		internal const string CodeDuplicate = "taf-scenario-verb-duplicate";
		internal const string CodeCollision = "taf-scenario-verb-collision";
		internal const string CodeOverCap = "taf-scenario-verb-provider-cap";

		/// <summary>
		/// Judges every claim. <paramref name="Claims"/> must already be in a deterministic order -
		/// the registry sorts ordinally by owner then type - because the refusal lines are emitted
		/// in that order and a matrix host reads them as data.
		/// </summary>
		internal static KingdomScenarioVerbAdmission Admit(IList<KingdomScenarioVerbClaim> Claims)
		{
			KingdomScenarioVerbAdmission admission = new KingdomScenarioVerbAdmission();
			if (Claims == null) return admission;
			if (Claims.Count > MaxProviders)
			{
				admission.Refusals.Add(Line(CodeOverCap, "(all)", Claims.Count
					+ " verb providers are installed, over the " + MaxProviders + " cap; none are"
					+ " admitted"));
				return admission;
			}
			// Claim index by verb, collecting EVERY claimant rather than stopping at the first:
			// a collision has to name all of them or the operator fixes one half of it.
			Dictionary<string, List<int>> claimants =
				new Dictionary<string, List<int>>(StringComparer.Ordinal);
			for (int i = 0; i < Claims.Count; i++)
			{
				KingdomScenarioVerbClaim claim = Claims[i];
				if (claim == null) continue;
				IList<string> verbs;
				if (!TryAdmitClaim(claim, admission.Refusals, out verbs)) continue;
				for (int v = 0; v < verbs.Count; v++)
				{
					List<int> holders;
					if (!claimants.TryGetValue(verbs[v], out holders))
					{
						holders = new List<int>();
						claimants[verbs[v]] = holders;
					}
					holders.Add(i);
				}
			}
			List<string> names = new List<string>(claimants.Keys);
			names.Sort(StringComparer.Ordinal);
			for (int n = 0; n < names.Count; n++)
			{
				List<int> holders = claimants[names[n]];
				if (holders.Count == 1)
				{
					admission.ByVerb[names[n]] = holders[0];
					continue;
				}
				List<string> owners = new List<string>();
				for (int h = 0; h < holders.Count; h++) owners.Add(Claims[holders[h]].Label);
				admission.Refusals.Add(Line(CodeCollision, string.Join(", ", owners.ToArray()),
					"verb '" + names[n] + "' is claimed by " + holders.Count
					+ " providers; it is admitted for none of them"));
			}
			return admission;
		}

		/// <summary>
		/// One claim, admitted whole or refused whole. Partial admission would let a provider ship
		/// a reserved name beside a lawful one and still be half-live, which is exactly the silent
		/// half-state the harness refuses everywhere else.
		/// </summary>
		private static bool TryAdmitClaim(KingdomScenarioVerbClaim Claim, IList<string> Refusals,
			out IList<string> Verbs)
		{
			Verbs = null;
			if (!Claim.Constructed)
			{
				Refusals.Add(Line(CodeThrew, Claim.Label, "the provider could not be constructed "
					+ "or does not implement IKingdomScenarioVerbProvider"));
				return false;
			}
			if (Claim.ApiVersion != KingdomScenarioVerbApi.Version)
			{
				Refusals.Add(Line(CodeVersion, Claim.Label, "declares scenario verb API version "
					+ Claim.ApiVersion + "; this harness publishes "
					+ KingdomScenarioVerbApi.Version));
				return false;
			}
			if (Claim.Verbs == null || Claim.Verbs.Count == 0)
			{
				Refusals.Add(Line(CodeEmpty, Claim.Label, "claims no verb names"));
				return false;
			}
			if (Claim.Verbs.Count > MaxVerbsPerProvider)
			{
				Refusals.Add(Line(CodeOverCap, Claim.Label, "claims " + Claim.Verbs.Count
					+ " verbs, over the " + MaxVerbsPerProvider + " per-provider cap"));
				return false;
			}
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			List<string> admitted = new List<string>();
			for (int i = 0; i < Claim.Verbs.Count; i++)
			{
				string verb = Claim.Verbs[i];
				if (!KingdomScenarioRules.SafeToken(verb))
				{
					Refusals.Add(Line(CodeMalformed, Claim.Label, "claims malformed verb name '"
						+ KingdomScenarioRules.Bounded(verb ?? "") + "'"));
					return false;
				}
				if (KingdomScenarioVerbApi.IsReserved(verb))
				{
					Refusals.Add(Line(CodeReserved, Claim.Label, "claims '" + verb
						+ "', which the harness dispatches itself; the reserved set is "
						+ string.Join(", ", KingdomScenarioVerbApi.Reserved)));
					return false;
				}
				if (!seen.Add(verb))
				{
					Refusals.Add(Line(CodeDuplicate, Claim.Label, "claims '" + verb
						+ "' more than once"));
					return false;
				}
				admitted.Add(verb);
			}
			Verbs = admitted;
			return true;
		}

		/// <summary>One refusal line: code, owner, reason. The code is the assertable half.</summary>
		internal static string Line(string Code, string Owner, string Detail)
		{
			return "[" + Code + "] " + Owner + ": " + Detail;
		}
	}
}
