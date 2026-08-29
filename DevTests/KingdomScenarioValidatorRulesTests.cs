#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The three landed rules that had zero executing coverage.
	/// <para>
	/// Each is pure and directly callable, and each was asserted only as source text - the same
	/// defect class as the codec whose round-trip fixture never assigned the field it tested. The
	/// 300-character guard is the sharpest of the three: it is the exact guard RED 19 item 4 said
	/// "can never fire", so a test that only reads the source proves nothing about whether the
	/// repair works.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioValidatorRulesTests
	{
		private static KingdomScenarioDefinition Row()
		{
			KingdomScenarioDefinition row = new KingdomScenarioDefinition
			{
				Key = "arch-gallery-slice",
				Family = "architecture",
				AuthorityClass = "architecture-stamper",
				SyntheticRaw = "false"
			};
			KingdomScenarioStep step = new KingdomScenarioStep
			{
				Verb = KingdomScenarioVerb.ProveCatalogue
			};
			step.Arguments["Catalogue"] = "architecture";
			row.Steps.Add(step);
			return row;
		}

		private static bool Reports(KingdomScenarioDefinition row, string fragment)
		{
			IList<string> findings = KingdomScenarioRowValidator.Findings(row);
			for (int i = 0; i < findings.Count; i++)
				if (findings[i].IndexOf(fragment, StringComparison.Ordinal) >= 0) return true;
			return false;
		}

		[Test]
		public void ALawfulRowReportsNothing()
		{
			CollectionAssert.IsEmpty(KingdomScenarioRowValidator.Findings(Row()));
		}

		// ----- the 300-character guard item 4 said could never fire -------------------------------

		[Test]
		public void TextAtTheCapIsAccepted()
		{
			KingdomScenarioDefinition row = Row();
			row.DisplayName = new string('n', KingdomScenarioRowValidator.MaxTextChars);
			row.Description = new string('d', KingdomScenarioRowValidator.MaxTextChars);
			CollectionAssert.IsEmpty(KingdomScenarioRowValidator.Findings(row));
		}

		[Test]
		public void OversizeDisplayNameIsRefused()
		{
			KingdomScenarioDefinition row = Row();
			row.DisplayName = new string('n', KingdomScenarioRowValidator.MaxTextChars + 1);
			Assert.IsTrue(Reports(row, "oversize authored text"),
				"the guard the XML adapter used to make unreachable must fire");
		}

		[Test]
		public void OversizeDescriptionIsRefused()
		{
			KingdomScenarioDefinition row = Row();
			row.Description = new string('d', KingdomScenarioRowValidator.MaxTextChars + 1);
			Assert.IsTrue(Reports(row, "oversize authored text"));
		}

		// ----- the empty domain member a||b used to become a|b ------------------------------------

		[Test]
		public void AnEmptyDomainMemberIsRefused()
		{
			KingdomScenarioDefinition row = Row();
			KingdomScenarioParameter parameter = new KingdomScenarioParameter { Name = "facing" };
			// Exactly what "north||east" now yields once the adapter stopped discarding empties.
			parameter.Domain = new List<string> { "north", "", "east" };
			row.Parameters.Add(parameter);
			Assert.IsTrue(Reports(row, "malformed domain value"));
		}

		[Test]
		public void AWhollyEmptyDomainIsRefused()
		{
			KingdomScenarioDefinition row = Row();
			KingdomScenarioParameter parameter = new KingdomScenarioParameter { Name = "facing" };
			parameter.Domain = new List<string>();
			row.Parameters.Add(parameter);
			Assert.IsTrue(Reports(row, "empty domain"));
		}

		[Test]
		public void ARepeatedDomainValueIsRefused()
		{
			KingdomScenarioDefinition row = Row();
			KingdomScenarioParameter parameter = new KingdomScenarioParameter { Name = "facing" };
			parameter.Domain = new List<string> { "north", "north" };
			row.Parameters.Add(parameter);
			Assert.IsTrue(Reports(row, "repeats domain value"));
		}

		// ----- the reserved request name --------------------------------------------------------

		/// <summary>
		/// The launcher freezes the seed into the request; an authored parameter of that name would
		/// shadow it and make the request grammar ambiguous.
		/// </summary>
		[Test]
		public void TheReservedSeedParameterNameIsRefused()
		{
			KingdomScenarioDefinition row = Row();
			KingdomScenarioParameter parameter = new KingdomScenarioParameter
			{
				Name = KingdomScenarioRequest.SeedName
			};
			parameter.Domain = new List<string> { "north" };
			row.Parameters.Add(parameter);
			Assert.IsTrue(Reports(row, "reserved request name"));
		}

		[Test]
		public void AnOrdinaryParameterNameIsNotReserved()
		{
			KingdomScenarioDefinition row = Row();
			KingdomScenarioParameter parameter = new KingdomScenarioParameter { Name = "facing" };
			parameter.Domain = new List<string> { "north" };
			row.Parameters.Add(parameter);
			Assert.IsFalse(Reports(row, "reserved request name"));
		}

		[Test]
		public void ADuplicateParameterNameIsRefused()
		{
			KingdomScenarioDefinition row = Row();
			for (int i = 0; i < 2; i++)
			{
				KingdomScenarioParameter parameter =
					new KingdomScenarioParameter { Name = "facing" };
				parameter.Domain = new List<string> { "north" };
				row.Parameters.Add(parameter);
			}
			Assert.IsTrue(Reports(row, "declared twice"));
		}
	}
}
#endif
