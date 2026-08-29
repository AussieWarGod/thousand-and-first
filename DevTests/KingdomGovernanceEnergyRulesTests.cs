#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// C17 foundation repair, Fix 3: the stasis vault and the mirror gate charge their one action
	/// through <see cref="KingdomGovernanceRules"/> instead of a bare literal
	/// <c>UseEnergy(1000, "...")</c>.
	/// <para>
	/// Neither <c>Growth/KingdomStasisVault.cs</c> nor <c>Growth/KingdomMirrorGate.cs</c> is compiled
	/// into this test assembly &mdash; both reach into <c>XRL.World.GameObject</c> and <c>Popup</c>,
	/// which want the live Qud engine this harness does not have. What is pure is proven
	/// behaviourally below: the constant and the exact reason strings the two sites depend on. What
	/// is not pure &mdash; that a cancelled, read-only, or declined interaction spends nothing, and a
	/// completed one spends exactly once &mdash; is proven as a source contract against the
	/// checked-in text instead, the same idiom
	/// <c>KingdomStasisVaultRulesTests.RuntimeUsesOneNativePhaseIsolatedFieldAndWholeBodyCustody</c>
	/// and <c>KingdomMirrorGateDestinationTests</c> already use to reach into these two files.
	/// </para>
	/// </summary>
	public class KingdomGovernanceEnergyRulesTests
	{
		// ---- Behavioural (pure): the exact reason strings the two fixed call sites depend on. ----

		[Test]
		public void StasisVaultReasonIsGovernedAndStable()
		{
			Assert.AreEqual(1000, KingdomGovernanceRules.NominalEnergyCost);
			Assert.AreEqual("TAF Governance stasis vault",
				KingdomGovernanceRules.EnergyReason("stasis vault"));
		}

		[Test]
		public void MirrorGateReasonIsGovernedAndStable()
		{
			Assert.AreEqual(1000, KingdomGovernanceRules.NominalEnergyCost);
			Assert.AreEqual("TAF Governance cross mirror gate",
				KingdomGovernanceRules.EnergyReason("cross mirror gate"));
		}

		// ---- Source contract (impure): the call sites cannot be exercised here, so the checked-in
		// text is asserted directly. ----

		[Test]
		public void StasisVaultChargesOnlyOnceAndOnlyAfterEveryEscapeHasReturned()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomStasisVault.cs");
			StringAssert.DoesNotContain("UseEnergy(1000", source,
				"the literal charge must not come back");
			string openMethod = MethodBody(source, "internal static void Open",
				"private static string Status");
			// One charge site in the whole method: no loop, no duplicate call, so a successful
			// commit is charged exactly once and there is nothing left to double-charge.
			Assert.AreEqual(1, CountOccurrences(openMethod, "UseEnergy("));
			StringAssert.Contains("KingdomGovernanceRules.NominalEnergyCost", openMethod);
			StringAssert.Contains("KingdomGovernanceRules.EnergyReason(\"stasis vault\")", openMethod);
			int chargeIndex = openMethod.IndexOf("Actor.UseEnergy(", StringComparison.Ordinal);
			Assert.GreaterOrEqual(chargeIndex, 0);
			// The charge itself is gated on the boolean TryEnter/TryRelease returned, not merely on
			// reaching the line: an unsuccessful attempt still falls through `if (changed)` false.
			StringAssert.Contains("if (changed) Actor.UseEnergy(", openMethod);
			StringAssert.Contains("changed = TryEnter(Vault, Actor, out failure);", openMethod);
			StringAssert.Contains("changed = TryRelease(Vault, actions[picked],", openMethod);
			// Every way out of Open that is not a completed TryEnter/TryRelease is textually a
			// `return;` above the charge line: the escape pick, the read-only listing, and both
			// confirmation declines. None of them can fall through to the charge.
			AssertAllOccurrencesBefore(openMethod, chargeIndex,
				"if (picked < 0 || picked >= actions.Count) return;", 1);
			AssertAllOccurrencesBefore(openMethod, chargeIndex,
				"if (actions[picked] == -2) { Popup.Show(Status(Vault)); return; }", 1);
			AssertAllOccurrencesBefore(openMethod, chargeIndex, "!= DialogResult.Yes) return;", 2);
		}

		[Test]
		public void MirrorGateChargesOnlyOnceAndOnlyOnASuccessfulCross()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomMirrorGate.cs");
			StringAssert.DoesNotContain("UseEnergy(1000", source,
				"the literal charge must not come back");
			string handler = MethodBody(source,
				"public override bool HandleEvent(InventoryActionEvent E)",
				"public override bool FireEvent(Event E)");
			// One charge site total: Dedicate, Re-key and Dispatch never reach UseEnergy, and Cross
			// only reaches it once, inside its own success branch.
			Assert.AreEqual(1, CountOccurrences(handler, "UseEnergy("));
			StringAssert.Contains("KingdomGovernanceRules.NominalEnergyCost", handler);
			StringAssert.Contains("KingdomGovernanceRules.EnergyReason(\"cross mirror gate\")", handler);
			int crossBranchIndex = handler.IndexOf(
				"if (KingdomMirrorGate.Cross(this, E.Actor, E))", StringComparison.Ordinal);
			int chargeIndex = handler.IndexOf("UseEnergy(", StringComparison.Ordinal);
			Assert.GreaterOrEqual(crossBranchIndex, 0);
			Assert.Greater(chargeIndex, crossBranchIndex,
				"the charge must live inside the Cross success branch, not before it");
			int dedicateIndex = handler.IndexOf("r_DedicateMirrorGate", StringComparison.Ordinal);
			int rekeyIndex = handler.IndexOf("r_RekeyMirrorGate", StringComparison.Ordinal);
			int dispatchIndex = handler.IndexOf("r_DispatchPurposeCargo", StringComparison.Ordinal);
			// Every other command's whole branch is textually clear of the one charge line: Dedicate
			// resolves and returns before Cross is even reached; Re-key and Dispatch resolve and
			// return after it, in their own untouched branches.
			Assert.Less(dedicateIndex, crossBranchIndex);
			Assert.Greater(rekeyIndex, chargeIndex);
			Assert.Greater(dispatchIndex, chargeIndex);
		}

		private static string MethodBody(string source, string startMarker, string endMarker)
		{
			int start = source.IndexOf(startMarker, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, "missing marker: " + startMarker);
			int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
			Assert.Greater(end, start, "missing marker: " + endMarker);
			return source.Substring(start, end - start);
		}

		private static void AssertAllOccurrencesBefore(string body, int boundary, string needle,
			int expectedCount)
		{
			int count = 0;
			int index = 0;
			while ((index = body.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
			{
				count++;
				Assert.Less(index, boundary, "escape must precede the charge: " + needle);
				index += needle.Length;
			}
			Assert.AreEqual(expectedCount, count, "unexpected occurrence count: " + needle);
		}

		private static int CountOccurrences(string haystack, string needle)
		{
			int count = 0;
			int index = 0;
			while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
			{
				count++;
				index += needle.Length;
			}
			return count;
		}
	}
}
#endif
