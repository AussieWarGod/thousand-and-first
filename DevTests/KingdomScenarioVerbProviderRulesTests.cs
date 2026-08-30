#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The admission law for third-party scenario verbs, executed.
	/// <para>
	/// The engine-side registry only OBSERVES - a cached attribute scan, a guarded construction -
	/// and hands what it saw to the pure law under test here, so every refusal shape runs without a
	/// licensed install. The cases that matter are the ones a hostile or careless third-party mod
	/// produces: a constructor that throws, a version that drifted, a reserved name, and two mods
	/// claiming one verb.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioVerbProviderRulesTests
	{
		private static KingdomScenarioVerbClaim Claim(string Owner, string Type,
			params string[] Verbs)
		{
			KingdomScenarioVerbClaim claim = new KingdomScenarioVerbClaim
			{
				Owner = Owner,
				TypeName = Type,
				ApiVersion = KingdomScenarioVerbApi.Version,
				Constructed = true
			};
			for (int i = 0; i < Verbs.Length; i++) claim.Verbs.Add(Verbs[i]);
			return claim;
		}

		private static IList<KingdomScenarioVerbClaim> List(
			params KingdomScenarioVerbClaim[] Claims)
		{
			return new List<KingdomScenarioVerbClaim>(Claims);
		}

		private static bool Names(IList<string> Refusals, string Code)
		{
			for (int i = 0; i < Refusals.Count; i++)
				if (Refusals[i].IndexOf(Code, StringComparison.Ordinal) >= 0) return true;
			return false;
		}

		[Test]
		public void AWellFormedProviderIsAdmitted()
		{
			KingdomScenarioVerbAdmission admission =
				KingdomScenarioVerbProviderRules.Admit(List(Claim("mod.a", "A", "probe", "sweep")));
			Assert.AreEqual(2, admission.ByVerb.Count);
			Assert.AreEqual(0, admission.ByVerb["probe"]);
			Assert.AreEqual(0, admission.ByVerb["sweep"]);
			Assert.AreEqual(0, admission.Refusals.Count);
		}

		[Test]
		public void NothingIsAdmittedFromAnEmptyOrNullRoster()
		{
			Assert.AreEqual(0, KingdomScenarioVerbProviderRules.Admit(null).ByVerb.Count);
			Assert.AreEqual(0,
				KingdomScenarioVerbProviderRules.Admit(List()).ByVerb.Count);
		}

		/// <summary>A provider that could not be constructed is refused, never treated as empty.</summary>
		[Test]
		public void AProviderThatThrewIsRefusedByName()
		{
			KingdomScenarioVerbClaim claim = Claim("mod.a", "A", "probe");
			claim.Constructed = false;
			KingdomScenarioVerbAdmission admission =
				KingdomScenarioVerbProviderRules.Admit(List(claim));
			Assert.AreEqual(0, admission.ByVerb.Count);
			Assert.IsTrue(Names(admission.Refusals, KingdomScenarioVerbProviderRules.CodeThrew));
			Assert.IsTrue(Names(admission.Refusals, "mod.a"));
		}

		/// <summary>Version drift is a loud refusal, because a silently inactive verb reads as ours.</summary>
		[Test]
		public void VersionDriftIsRefused()
		{
			KingdomScenarioVerbClaim claim = Claim("mod.a", "A", "probe");
			claim.ApiVersion = KingdomScenarioVerbApi.Version + 1;
			KingdomScenarioVerbAdmission admission =
				KingdomScenarioVerbProviderRules.Admit(List(claim));
			Assert.AreEqual(0, admission.ByVerb.Count);
			Assert.IsTrue(Names(admission.Refusals, KingdomScenarioVerbProviderRules.CodeVersion));
		}

		[Test]
		public void AProviderClaimingNoVerbsIsRefused()
		{
			KingdomScenarioVerbAdmission admission =
				KingdomScenarioVerbProviderRules.Admit(List(Claim("mod.a", "A")));
			Assert.IsTrue(Names(admission.Refusals, KingdomScenarioVerbProviderRules.CodeEmpty));
		}

		[TestCase("Probe")]
		[TestCase("my verb")]
		[TestCase("my_verb")]
		[TestCase("")]
		[TestCase(null)]
		public void AMalformedVerbNameRefusesTheWholeProvider(string Name)
		{
			KingdomScenarioVerbAdmission admission = KingdomScenarioVerbProviderRules.Admit(
				List(Claim("mod.a", "A", "probe", Name)));
			// Partial admission would leave a provider half-live, which is the silent half-state
			// the harness refuses everywhere else.
			Assert.AreEqual(0, admission.ByVerb.Count);
			Assert.IsTrue(Names(admission.Refusals,
				KingdomScenarioVerbProviderRules.CodeMalformed));
		}

		/// <summary>
		/// A reserved name refuses the provider ENTIRELY and leaves the built-in standing. Letting
		/// both drop out would let any mod revoke <c>realize</c> - the single mutating production
		/// transaction - and make every verdict on the machine unfalsifiable.
		/// </summary>
		[Test]
		public void AReservedNameRefusesTheProviderAndNeverTheBuiltIn()
		{
			for (int i = 0; i < KingdomScenarioVerbApi.Reserved.Length; i++)
			{
				string reserved = KingdomScenarioVerbApi.Reserved[i];
				KingdomScenarioVerbAdmission admission = KingdomScenarioVerbProviderRules.Admit(
					List(Claim("mod.a", "A", "probe", reserved)));
				Assert.AreEqual(0, admission.ByVerb.Count, reserved);
				Assert.IsFalse(admission.ByVerb.ContainsKey(reserved), reserved);
				Assert.IsTrue(Names(admission.Refusals,
					KingdomScenarioVerbProviderRules.CodeReserved), reserved);
			}
		}

		[Test]
		public void ADuplicateInsideOneProviderIsRefused()
		{
			KingdomScenarioVerbAdmission admission = KingdomScenarioVerbProviderRules.Admit(
				List(Claim("mod.a", "A", "probe", "probe")));
			Assert.AreEqual(0, admission.ByVerb.Count);
			Assert.IsTrue(Names(admission.Refusals,
				KingdomScenarioVerbProviderRules.CodeDuplicate));
		}

		/// <summary>
		/// Two providers claiming one name: NEITHER holds it. First-registered winning would make a
		/// verb mean different things on two machines carrying the same mods in a different order.
		/// </summary>
		[Test]
		public void ACollisionLeavesNobodyHoldingTheName()
		{
			KingdomScenarioVerbAdmission admission = KingdomScenarioVerbProviderRules.Admit(
				List(Claim("mod.a", "A", "probe", "alpha"), Claim("mod.b", "B", "probe", "beta")));
			Assert.IsFalse(admission.ByVerb.ContainsKey("probe"));
			// The uncontested names on both sides survive: a collision is about one name.
			Assert.AreEqual(0, admission.ByVerb["alpha"]);
			Assert.AreEqual(1, admission.ByVerb["beta"]);
			Assert.IsTrue(Names(admission.Refusals,
				KingdomScenarioVerbProviderRules.CodeCollision));
			Assert.IsTrue(Names(admission.Refusals, "mod.a"));
			Assert.IsTrue(Names(admission.Refusals, "mod.b"));
		}

		[Test]
		public void AThreeWayCollisionNamesEveryClaimant()
		{
			KingdomScenarioVerbAdmission admission = KingdomScenarioVerbProviderRules.Admit(
				List(Claim("mod.a", "A", "probe"), Claim("mod.b", "B", "probe"),
					Claim("mod.c", "C", "probe")));
			Assert.AreEqual(0, admission.ByVerb.Count);
			Assert.AreEqual(1, admission.Refusals.Count);
			Assert.IsTrue(Names(admission.Refusals, "mod.a"));
			Assert.IsTrue(Names(admission.Refusals, "mod.b"));
			Assert.IsTrue(Names(admission.Refusals, "mod.c"));
		}

		/// <summary>Two providers inside ONE mod are still told apart, so a collision names both.</summary>
		[Test]
		public void TwoProvidersInOneModAreDistinguishedByType()
		{
			KingdomScenarioVerbAdmission admission = KingdomScenarioVerbProviderRules.Admit(
				List(Claim("mod.a", "First", "probe"), Claim("mod.a", "Second", "probe")));
			Assert.AreEqual(0, admission.ByVerb.Count);
			Assert.IsTrue(Names(admission.Refusals, "mod.a/First"));
			Assert.IsTrue(Names(admission.Refusals, "mod.a/Second"));
		}

		[Test]
		public void OverCapRostersAndProvidersAreRefused()
		{
			List<KingdomScenarioVerbClaim> many = new List<KingdomScenarioVerbClaim>();
			for (int i = 0; i <= KingdomScenarioVerbProviderRules.MaxProviders; i++)
				many.Add(Claim("mod." + i, "T", "verb" + i));
			KingdomScenarioVerbAdmission admission =
				KingdomScenarioVerbProviderRules.Admit(many);
			Assert.AreEqual(0, admission.ByVerb.Count);
			Assert.IsTrue(Names(admission.Refusals,
				KingdomScenarioVerbProviderRules.CodeOverCap));

			KingdomScenarioVerbClaim greedy = Claim("mod.a", "A");
			for (int i = 0; i <= KingdomScenarioVerbProviderRules.MaxVerbsPerProvider; i++)
				greedy.Verbs.Add("verb" + i);
			admission = KingdomScenarioVerbProviderRules.Admit(List(greedy));
			Assert.AreEqual(0, admission.ByVerb.Count);
			Assert.IsTrue(Names(admission.Refusals,
				KingdomScenarioVerbProviderRules.CodeOverCap));
		}

		/// <summary>A null row in the scan is skipped without taking the admission with it.</summary>
		[Test]
		public void ANullClaimDoesNotBreakTheRoster()
		{
			List<KingdomScenarioVerbClaim> rows = new List<KingdomScenarioVerbClaim>();
			rows.Add(null);
			rows.Add(Claim("mod.a", "A", "probe"));
			KingdomScenarioVerbAdmission admission =
				KingdomScenarioVerbProviderRules.Admit(rows);
			Assert.AreEqual(1, admission.ByVerb["probe"]);
		}

		/// <summary>Every refusal carries its stable code first, so a host binds to the code.</summary>
		[Test]
		public void EveryRefusalLineLeadsWithItsCode()
		{
			Assert.AreEqual("[taf-scenario-verb-collision] mod.a: because",
				KingdomScenarioVerbProviderRules.Line(
					KingdomScenarioVerbProviderRules.CodeCollision, "mod.a", "because"));
		}
	}
}
#endif
