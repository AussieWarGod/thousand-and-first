#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source contracts for the RED 19 repairs whose subject is a runtime adapter.
	/// <para>
	/// The pure halves execute elsewhere. What cannot execute here is whether the XML adapter, the
	/// attended runner, the status surface, and the capture path actually route through them - and
	/// every defect in this docket was exactly that: a rule that existed and a caller that went
	/// around it.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioRed19SourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static string Section(string source, string start, string end)
		{
			int begin = source.IndexOf(start, StringComparison.Ordinal);
			Assert.Greater(begin, -1, start);
			int stop = source.IndexOf(end, begin + start.Length, StringComparison.Ordinal);
			if (stop < 0) stop = source.Length;
			return source.Substring(begin, stop - begin);
		}

		// ----- item 3: caps must bound traversal, not only the verdict ----------------------------

		/// <summary>
		/// Each cap returns before walking its children. Recording an over-cap finding and then
		/// traversing the whole hostile list is a bounded verdict over an unbounded scan.
		/// </summary>
		[Test]
		public void EveryDeclaredCapReturnsBeforeWalkingItsChildren()
		{
			// ADJACENCY, not co-presence. Asserting the message and the return separately let each
			// return be deleted while both Contains still passed - the fixture was blind to three
			// of its own four mutants.
			string rules = Read("Harness/KingdomScenarioRules.cs");
			StringAssert.Contains(
				"findings.Add(\"the scenario registry exceeds \" + MaxScenarios + \" rows\");\n"
				+ "\t\t\t\treturn findings;", rules);
			string validator = Read("Harness/KingdomScenarioRowValidator.cs");
			StringAssert.Contains(
				"Findings.Add(\"more than \" + MaxParameters + \" parameters\");\n"
				+ "\t\t\t\treturn;", validator);
			StringAssert.Contains(
				"Findings.Add(parameter.Name + \" has an oversize domain\");\n"
				+ "\t\t\t\t\tcontinue;", validator);
			StringAssert.Contains(
				"\t\t\t\t\t+ \" steps; the verb sequence must stay recordable\");\n"
				+ "\t\t\t\treturn;", validator);
		}

		/// <summary>The launcher's reserved request name may not be shadowed by an authored one.</summary>
		[Test]
		public void TheReservedRequestNameIsRefusedInAuthoredParameters()
		{
			string validator = Read("Harness/KingdomScenarioRowValidator.cs");
			StringAssert.Contains("KingdomScenarioRequest.SeedName", validator);
			StringAssert.Contains("uses the reserved request name", validator);
		}

		// ----- item 4: the XML adapter must not launder ------------------------------------------

		/// <summary>
		/// Bounding authored text before validation silently repaired it and made the length guard
		/// unreachable; dropping empty domain members turned a malformed "a||b" into a lawful "a|b".
		/// </summary>
		[Test]
		public void TheXmlAdapterPreservesRawAuthoredTextForTheValidator()
		{
			string registry = Read("Harness/KingdomScenarioRegistry.cs");
			StringAssert.Contains("DisplayName = Trim(displayName)", registry);
			StringAssert.Contains("definition.Description = Trim(child.GetAttribute(\"Text\"));",
				registry);
			StringAssert.DoesNotContain("KingdomScenarioRules.Bounded(displayName)", registry);
			StringAssert.DoesNotContain("StringSplitOptions.RemoveEmptyEntries", registry);
			StringAssert.Contains("domain.Split('|')", registry);
		}

		// ----- item 2: the runtime adapter routes through the pure parser -------------------------

		[Test]
		public void PlanningRoutesThroughTheBoundedParser()
		{
			string request = Read("Harness/KingdomScenarioRequest.cs");
			StringAssert.Contains("if (!TryParse(Request, out key, out selection, out seed,"
				+ " out Failure)) return false;", request);
			StringAssert.DoesNotContain("StringSplitOptions.RemoveEmptyEntries", request);
			StringAssert.Contains("MaxRequestChars", request);
			StringAssert.Contains("MaxSegments", request);
		}

		// ----- item 7: the measured key set reaches durable provenance ----------------------------

		/// <summary>
		/// Publication happens BEFORE signing and reporting, and a torn publication is visibly
		/// non-green. Signing an in-memory copy left the save saying nothing was compared.
		/// </summary>
		[Test]
		public void TheMeasuredKeySetIsPublishedBeforeAnyVerdict()
		{
			string run = Read("Harness/KingdomScenarioRun.cs");
			int publish = run.IndexOf("TryPublishMeasured", StringComparison.Ordinal);
			int sign = run.IndexOf("TrySignAcceptance", StringComparison.Ordinal);
			Assert.Greater(publish, -1, "the measured stamp must be published");
			Assert.Greater(sign, publish, "publication must precede signing");
			StringAssert.Contains("This run is NOT green", run);
			StringAssert.Contains("no replay", run);
		}

		/// <summary>
		/// ONE write path, proved by counting call sites across BOTH shards.
		/// <para>
		/// The previous version read a single file and asserted the strings it expected to find
		/// there, so two copies of encode/write/readback in two files passed as "one authority".
		/// Two copies that agree today are a divergence waiting for its first edit; the only thing
		/// that proves singularity is that the second site does not exist.
		/// </para>
		/// </summary>
		[Test]
		public void BothProvenanceWritesShareExactlyOneCodePath()
		{
			// CENSUS over every shard in Harness/: counting two files proved only that those two
			// agreed. The tree is flat by construction - the inventory helper refuses a
			// subdirectory - so this non-recursive listing IS the whole tree.
			int writes = 0;
			int readbacks = 0;
			string tree = System.IO.Path.Combine(TestMain.RepositoryRoot, "Harness");
			foreach (string shard in System.IO.Directory.GetFiles(tree, "*.cs"))
			{
				string source = System.IO.File.ReadAllText(shard);
				writes += Occurrences(source,
					"SetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState");
				// The QUALIFIED call form: the unqualified name also matches the declaration in
				// KingdomScenarioDurableState, which is the one place it is allowed to appear.
				readbacks += Occurrences(source,
					"KingdomScenarioDurableState.ProvesExactText(");
			}
			Assert.AreEqual(1, writes, "exactly one provenance write across the whole harness tree");
			Assert.AreEqual(1, readbacks, "exactly one exact readback across the whole harness tree");
			string authority = Read("Harness/KingdomScenarioStampAuthority.cs");
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			Assert.AreEqual(1, Occurrences(authority,
				"SetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState"),
				"the one write must live in the shared authority");
			// Both callers must actually route through it.
			StringAssert.Contains("TryWriteProvenance(Record, out Failure)", realizer);
			StringAssert.Contains("TryWriteProvenance(Measured, out Failure)", authority);
			StringAssert.Contains("internal static bool TryWriteProvenance(", authority);
			// The docstring must describe where the writes live, not where they used to.
			StringAssert.Contains("There is exactly ONE write path", authority);
			StringAssert.DoesNotContain("Keeping both writes here is the point", authority);
		}

		private static int Occurrences(string source, string needle)
		{
			int count = 0;
			int at = source.IndexOf(needle, StringComparison.Ordinal);
			while (at >= 0)
			{
				count++;
				at = source.IndexOf(needle, at + 1, StringComparison.Ordinal);
			}
			return count;
		}

		/// <summary>Status recomputes the verdict from the durable stamp, not the pre-run null.</summary>
		[Test]
		public void StatusRecomputesTheVerdictFromDurableProvenance()
		{
			string wishes = Read("Harness/KingdomScenarioWishes.cs");
			StringAssert.Contains("private static string Verdict(KingdomScenarioProvenance Record)",
				wishes);
			StringAssert.Contains("Record.KeySetDigest", wishes);
			StringAssert.Contains("KingdomScenarioAnchorRules.TrySignAcceptance(Record", wishes);
		}

		// ----- item 8: the anchor id is stable before the evidence exists -------------------------

		[Test]
		public void TheScenarioDeclaresItsAnchorIdBeforeCuration()
		{
			string roster = Read("Harness/KingdomScenarios.xml");
			StringAssert.Contains("AnchorId=\"anchor-arch-01\"", roster);
			StringAssert.DoesNotContain("AnchorId=\"\"", roster);
			string report = Read("Harness/KingdomScenarioCaptureReport.cs");
			StringAssert.Contains("plan.AnchorId", report);
			StringAssert.Contains("Capture the anchor the scenario actually leans on", report);
		}

		// ----- item 9: gallery authority may never found an ordinary anchor -----------------------

		/// <summary>
		/// An ALLOWLIST, pinned by ADJACENCY so each clause dies with its own guard.
		/// <para>
		/// Co-presence Contains was blind to all six clause mutants: deleting an entire
		/// <c>Refuse(...)</c> left the fixture green because the property name it named survived
		/// elsewhere in the file. This is the form RED 21 mandated for the caps - the condition and
		/// the refusal it raises pinned as ONE contiguous string - so removing the refusal removes
		/// the pin. Ruling condition 4, "existing refusals never loosen", is enforced here rather
		/// than trusted.
		/// </para>
		/// </summary>
		[Test]
		public void EveryAllowlistClauseIsPinnedToItsOwnRefusal()
		{
			string evidence = Read("Harness/KingdomScenarioOrdinaryEvidence.cs");
			foreach (string pin in AllowlistPins())
				StringAssert.Contains(pin, evidence, "an allowlist clause lost its adjacency pin");
			// The staked-plot clause and its two refusals, same form.
			StringAssert.Contains(
				"if (!TryProveStakedPlot(Zone, intent, lot, out Failure)) return false;", evidence);
			StringAssert.Contains(
				"\t\t\t\treturn Refuse(\"this building's lot is staked at a different rect",
				evidence);
			StringAssert.Contains(
				"\t\t\treturn Refuse(\"no staked plot in this zone carries this building's lot id",
				evidence);
		}

		/// <summary>Condition + refusal as one string: delete either half and the pin is gone.</summary>
		private static string[] AllowlistPins()
		{
			return new string[]
			{
				"if (KingdomScenarioGallerySlice.CarriesGalleryAuthority(Owner))"
					+ "\n" + "				return Refuse(\"this building carries debug-gallery authority; a gallery-staged \"",
				"if (KingdomScenarioGallerySlice.CarriesGalleryAuthority(item))"
					+ "\n" + "					return Refuse(\"a component of this building carries debug-gallery authority; \"",
				"|| Owner.HasStringProperty(KingdomArchitectureStamper.UpgradeSchemaProperty))"
					+ "\n" + "				return Refuse(\"this building carries upgrade authority; the commission that founded \"",
				"if (!Owner.HasStringProperty(KingdomConstruction.ReceiptProperty))"
					+ "\n" + "				return Refuse(\"this building carries no construction receipt, so ordinary \"",
			};
		}

		/// <summary>
		/// The ruled caveat must reach the curator before the row they paste, not in a ledger they
		/// are not reading. Proves ORDER, which is what AssertOrder establishes - not adjacency,
		/// which the name no longer claims.
		/// </summary>
		[Test]
		public void TheCurationCaveatPrintsBeforeThePastedRow()
		{
			string report = Read("Harness/KingdomScenarioCaptureReport.cs");
			AssertOrder(report,
				"{{R|CURATION CAVEAT}}",
				"inherited, relocated, socketed, or plot2 origin",
				"Curate only a building you commissioned in this run.",
				"{{C|Curated row}}");
			StringAssert.Contains("curate only a building you commissioned in this run",
				Read("TESTING.md"));
		}

		/// <summary>The allowlist is reached from the capture path, for every candidate.</summary>
		[Test]
		public void OrdinaryCaptureReprovesPositiveCommissionEvidence()
		{
			string evidence = Read("Harness/KingdomScenarioOrdinaryEvidence.cs");
			StringAssert.Contains("TryProveOrdinaryCommission", evidence);
			StringAssert.Contains("KingdomPlots.PlotX1Property", evidence);
			StringAssert.Contains("TryProveOrdinaryCommission(zone, candidate, out Failure)",
				Read("Harness/KingdomScenarioCaptureReport.cs"));
		}

		[Test]
		public void OrdinaryCaptureRefusesGalleryAuthority()
		{
			string slice = Read("Harness/KingdomScenarioGallerySlice.cs");
			StringAssert.Contains("internal static bool CarriesGalleryAuthority(", slice);
			StringAssert.Contains("HasStringProperty(ReceiptProperty)", slice);
			StringAssert.Contains("HasIntProperty(NumberProperty)", slice);
			string evidence = Read("Harness/KingdomScenarioOrdinaryEvidence.cs");
			StringAssert.Contains("KingdomScenarioGallerySlice.CarriesGalleryAuthority(Owner)",
				evidence);
			StringAssert.Contains("may never", evidence);
		}

		/// <summary>The evidence field is bound as the authorized recipe, not as observed provenance.</summary>
		[Test]
		public void TheAnchorVerbsFieldIsDescribedHonestly()
		{
			string models = Read("Harness/KingdomScenarioModels.cs");
			StringAssert.Contains("AUTHORIZES", models);
			StringAssert.DoesNotContain(
				"The exact production verb sequence the anchor state was reached by.", models);
		}
		private static void AssertOrder(string Source, params string[] Terms)
		{
			int offset = 0;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], offset, StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, 0, "missing ordered source term: " + Terms[i]);
				offset = found + Terms[i].Length;
			}
		}

	}
}
#endif
