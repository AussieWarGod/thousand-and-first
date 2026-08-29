#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Consent and status disclosure for the reciprocal purpose portfolio. The source
	/// food debit, the carried amount and the carriage loss must all be legible before anything
	/// is committed, and every number must be derived from the catalogue row rather than typed
	/// as a literal on a prompt.</summary>
	[TestFixture]
	public class KingdomPurposePortfolioDisclosureTests
	{
		private const string OpenPath = "Growth/KingdomPurposePortfolio.Open.cs";
		private const string InteractionPath = "Growth/KingdomPurposePortfolio.Interaction.cs";
		private const string PairingPath = "Growth/KingdomPurposePortfolio.Pairing.cs";
		private const string LandingPath = "Growth/KingdomPurposePortfolio.LandingFood.cs";
		private const string CargoRootPath = "Growth/KingdomPurposePortfolio.CargoRoot.cs";

		private static string Read(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		/// <summary>Whitespace-insensitive view of source text, so a reflow of a prompt
		/// expression cannot silently retire a disclosure assertion.</summary>
		private static string Squash(string text)
		{
			StringBuilder squashed = new StringBuilder(text.Length);
			for (int i = 0; i < text.Length; i++)
				if (!char.IsWhiteSpace(text[i])) squashed.Append(text[i]);
			return squashed.ToString();
		}

		private static int Count(string haystack, string needle)
		{
			int found = 0;
			for (int at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
				at = haystack.IndexOf(needle, at + 1, StringComparison.Ordinal)) found++;
			return found;
		}

		private static int At(string source, string term)
		{
			int found = source.IndexOf(term, StringComparison.Ordinal);
			Assert.Greater(found, -1, term);
			return found;
		}

		/// <summary>Exact replica of the one production carriage phrase
		/// (<c>KingdomPurposePortfolio.Open.cs</c> <c>CarriageLine</c>). The replica is pinned to
		/// production by <see cref="CarriageArithmeticIsDerivedFromTheRowNotTyped"/>, so a change
		/// to either one fails a test.</summary>
		private static string CarriageLine(string Lead, KingdomPurposePortfolioRecipe Recipe,
			KingdomPurposeKind Destination)
		{
			if (Recipe == null || Recipe.CarriedFood <= 0) return "";
			return "\n" + Lead + ": {{C|" + Recipe.CarriedFood + " of "
				+ Recipe.FoodServings + " food}} to {{C|"
				+ KingdomPurposePortfolioRules.PurposeName(Destination) + "}}; {{C|"
				+ (Recipe.FoodServings - Recipe.CarriedFood) + "}} lost in carriage.";
		}

		/// <summary>Exact replica of the food half of the operation consent line
		/// (<c>KingdomPurposePortfolio.Interaction.cs</c> <c>OperationPrompt</c>): the local food
		/// debit, closing the debit clause, then whatever carriage the row declares. Pinned to
		/// production by <see cref="ConsentComposesTheDebitThenTheCarriage"/>.</summary>
		private static string ConsentFood(KingdomPurposePortfolioRecipe Recipe,
			KingdomPurposeKind Destination)
		{
			return (Recipe.FoodServings > 0 ? ", " + Recipe.FoodServings + " food" : "")
				+ "}}." + CarriageLine("Provision carried", Recipe, Destination);
		}

		private static KingdomPurposePortfolioRecipe Row(KingdomPurposeKind Source,
			KingdomPurposeKind Destination)
		{
			KingdomPurposePortfolioRecipe recipe;
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRecipe(Source, Destination, out recipe),
				Source + ">" + Destination);
			return recipe;
		}

		[Test]
		public void CatalogueCarriesEightServingsAsSixWithTwoLostInCarriage()
		{
			int carrying = 0;
			foreach (KingdomPurposePortfolioRecipe row in KingdomPurposePortfolioRules.AllRecipes())
			{
				Assert.GreaterOrEqual(row.CarriedFood, 0, row.CargoKey);
				Assert.LessOrEqual(row.CarriedFood, row.FoodServings, row.CargoKey);
				if (row.CarriedFood <= 0) continue;
				carrying++;
				Assert.AreEqual(KingdomPurposeKind.Harvest, row.Source, row.CargoKey);
				Assert.AreEqual(8, row.FoodServings, row.CargoKey);
				Assert.AreEqual(6, row.CarriedFood, row.CargoKey);
				Assert.AreEqual(2, row.FoodServings - row.CarriedFood, row.CargoKey);
			}
			Assert.AreEqual(2, carrying,
				"exactly the two Harvest-sourced rows carry provision; the disclosure numbers are read from them");
		}

		[Test]
		public void ConsentRendersSourceDebitCarriedAmountAndCarriageLoss()
		{
			string rendered = CarriageLine("Provision carried",
				Row(KingdomPurposeKind.Harvest, KingdomPurposeKind.Forge),
				KingdomPurposeKind.Forge);
			StringAssert.Contains("Provision carried:", rendered);
			StringAssert.Contains("{{C|6 of 8 food}}", rendered);
			StringAssert.Contains(KingdomPurposePortfolioRules.PurposeName(
				KingdomPurposeKind.Forge), rendered);
			StringAssert.Contains("{{C|2}} lost in carriage.", rendered);
		}

		[Test]
		public void CarriageArithmeticIsDerivedFromTheRowNotTyped()
		{
			string open = Squash(Read(OpenPath));
			StringAssert.Contains(
				"+Lead+\":{{C|\"+Recipe.CarriedFood+\"of\"+Recipe.FoodServings+\"food}}to{{C|\"",
				open, "the carried and debited servings must be read from the recipe row");
			StringAssert.Contains("+(Recipe.FoodServings-Recipe.CarriedFood)+\"}}lostincarriage.\"",
				open, "the carriage loss must be subtracted, never typed");
			StringAssert.DoesNotContain("6of8food", open);
			string surfaces = Squash(Read(OpenPath)) + Squash(Read(InteractionPath))
				+ Squash(Read(PairingPath));
			Assert.AreEqual(1, Count(surfaces, "lostincarriage."),
				"one carriage phrase serves every consent and status surface; no prompt writes its own");
		}

		[Test]
		public void ChangingTheRowChangesTheRenderedNumbers()
		{
			KingdomPurposePortfolioRecipe row = Row(KingdomPurposeKind.Harvest,
				KingdomPurposeKind.Forge);
			KingdomPurposePortfolioRecipe altered = row.Copy();
			altered.FoodServings = 9;
			altered.CarriedFood = 3;
			string rendered = CarriageLine("Provision carried", altered, KingdomPurposeKind.Forge);
			StringAssert.Contains("{{C|3 of 9 food}}", rendered);
			StringAssert.Contains("{{C|6}} lost in carriage.", rendered);
			StringAssert.DoesNotContain("6 of 8", rendered);
			StringAssert.DoesNotContain("{{C|2}} lost", rendered);

			KingdomPurposePortfolioRecipe carriesAll = row.Copy();
			carriesAll.CarriedFood = carriesAll.FoodServings;
			StringAssert.Contains("{{C|0}} lost in carriage.",
				CarriageLine("Provision carried", carriesAll, KingdomPurposeKind.Forge));

			KingdomPurposePortfolioRecipe carriesNothing = row.Copy();
			carriesNothing.CarriedFood = 0;
			Assert.AreEqual("", CarriageLine("Provision carried", carriesNothing,
				KingdomPurposeKind.Forge),
				"retiring a row's carry must retire its carriage prose, not print an eight-serving loss");
			StringAssert.Contains(", 8 food",
				ConsentFood(carriesNothing, KingdomPurposeKind.Forge),
				"the local debit survives the carry being retired");
		}

		[Test]
		public void ProcessOnlyFoodIsDisclosedAsADebitAndNeverAsCarriageLoss()
		{
			KingdomPurposePortfolioRecipe consumesOnly = Row(KingdomPurposeKind.Flesh,
				KingdomPurposeKind.Harvest);
			Assert.AreEqual(4, consumesOnly.FoodServings);
			Assert.AreEqual(0, consumesOnly.CarriedFood,
				"the Flesh rows declare no carry; their food is consumed by the operation");
			Assert.AreEqual("", CarriageLine("Provision carried", consumesOnly,
				KingdomPurposeKind.Harvest),
				"a row that carries nothing must not claim transport loss");

			string consent = ConsentFood(consumesOnly, KingdomPurposeKind.Harvest);
			StringAssert.Contains(", 4 food", consent,
				"the four-serving local debit stays disclosed as a debit");
			StringAssert.DoesNotContain("lost in carriage", consent);
			StringAssert.DoesNotContain("Provision carried", consent);
		}

		[Test]
		public void RowsWithoutAnyFoodDiscloseNeitherDebitNorCarriage()
		{
			KingdomPurposePortfolioRecipe dry = Row(KingdomPurposeKind.Deep,
				KingdomPurposeKind.Forge);
			Assert.AreEqual(0, dry.FoodServings);
			Assert.AreEqual("", CarriageLine("Provision carried", dry, KingdomPurposeKind.Forge));
			Assert.AreEqual("}}.", ConsentFood(dry, KingdomPurposeKind.Forge));
		}

		[Test]
		public void ConsentComposesTheDebitThenTheCarriage()
		{
			StringAssert.Contains(
				"+(Recipe.FoodServings>0?\",\"+Recipe.FoodServings+\"food\":\"\")+\"}}.\"+CarriageLine(\"Provisioncarried\",Recipe,Destination)",
				Squash(Read(InteractionPath)),
				"operation consent states the local food debit, then any carriage that row declares");
			StringAssert.Contains("Recipe.CarriedFood<=0)return\"\"", Squash(Read(OpenPath)),
				"the carriage phrase is gated on a declared carry, not merely on a food debit");

			KingdomPurposePortfolioRecipe carries = Row(KingdomPurposeKind.Harvest,
				KingdomPurposeKind.Forge);
			string consent = ConsentFood(carries, KingdomPurposeKind.Forge);
			StringAssert.Contains(", 8 food", consent, "the source debit stays disclosed");
			StringAssert.Contains("{{C|6 of 8 food}}", consent);
			StringAssert.Contains("{{C|2}} lost in carriage.", consent);
		}

		[Test]
		public void EveryConsentAndStatusSurfaceRendersTheCarriage()
		{
			string interaction = Squash(Read(InteractionPath));
			StringAssert.Contains("CarriageLine(\"Provisioncarried\",Recipe,Destination)",
				interaction, "operation consent must disclose the carriage before it is accepted");

			string pairing = Squash(Read(PairingPath));
			StringAssert.Contains("CarriageLine(\"Provisioncarried\",outgoing,secondKind)",
				pairing, "pair consent must disclose the bootstrap carriage");
			StringAssert.Contains("CarriageLine(\"Provisioncarried\",incoming,FirstKind)",
				pairing, "pair consent must disclose the return carriage");
			StringAssert.Contains("(incoming.FoodServings>0?\",\"+incoming.FoodServings+\"food\":\"\")",
				pairing, "the return row's own food debit must be named beside its carriage");

			string open = Squash(Read(OpenPath));
			StringAssert.Contains("\"Provisioncommitted\"", open);
			StringAssert.Contains(
				"+ProvisionState(Pair)+PurposeEffectState(Pair)+DeclaredEffect(acting)", open);
		}

		[Test]
		public void StatusNeverInfersPhysicalArrivalFromAPhase()
		{
			string open = Read(OpenPath);
			int delivered = At(open, "!= KingdomPurposeOperationPhase.Delivered");
			int landed = At(open, "\"Provision landed\"");
			int accessor = At(open, "TryPurposeProvisionLanded(Pair.Operation");
			Assert.Less(accessor, landed,
				"a landing claim is only reachable through the discriminator, never from the phase alone");
			Assert.Greater(accessor, delivered,
				"the discriminator is consulted only once the operation is delivered");
			StringAssert.Contains(
				"Whether that provision reached the destination larders is proved by those stores, not by this receipt.",
				open, "an unproved delivery must say so plainly");
			StringAssert.Contains("bool proved = TryPurposeProvisionLanded(", open);
			StringAssert.Contains("return proved ? CarriageLine(\"Provision landed\"", open,
				"proved renders proved text; anything else falls through to the unverified text");
			StringAssert.Contains("if (!applicable) return carriage;", open,
				"a row carrying no provision claims neither arrival nor ignorance of one");
		}

		[Test]
		public void TheLandingDiscriminatorIsReadOnlyAndTakesNoMutationFlag()
		{
			// Contract depended on, owned by the landing lane.
			string landing = Read(LandingPath);
			int api = At(landing, "internal static bool TryPurposeProvisionLanded(");
			int open = landing.IndexOf('(', api);
			string[] parameters = landing.Substring(open + 1,
				landing.IndexOf(')', open) - open - 1).Split(',');
			// Every parameter is either an out-result or the receipt being asked about: there is
			// no input the caller could set to make the read repair, migrate, or write anything.
			for (int i = 0; i < parameters.Length; i++)
			{
				string parameter = parameters[i].Trim();
				Assert.IsTrue(parameter.StartsWith("out ", StringComparison.Ordinal)
					|| parameter.StartsWith("KingdomPurposeOperationReceipt ",
						StringComparison.Ordinal),
					"status must not be able to ask the discriminator to change anything: " + parameter);
			}
			string body = landing.Substring(api,
				landing.IndexOf("\n\t\t}", api, StringComparison.Ordinal) - api);
			StringAssert.Contains("TryRootedPurposeCargoExact(", body,
				"the read path reads the canonical key alone");
			foreach (string mutation in new[] { "ObjectGameState.Remove", "ObjectGameState[",
				"SetIntProperty", "SetStringProperty", "RemoveIntProperty", "RemoveStringProperty" })
				StringAssert.DoesNotContain(mutation, body,
					"drawing a status popup must migrate and write nothing");

			string cargoRoot = Read(CargoRootPath);
			int exact = At(cargoRoot, "private static bool TryRootedPurposeCargoExact(");
			string exactBody = cargoRoot.Substring(exact,
				cargoRoot.IndexOf("\n\t\t}", exact, StringComparison.Ordinal) - exact);
			foreach (string mutation in new[] { "Remove", "ObjectGameState[" })
				StringAssert.DoesNotContain(mutation, exactBody,
					"the exact read helper is the non-migrating half of the root lookup");
		}

		/// <summary>Replica of <c>KingdomPurposePortfolio.Pairing.cs</c> <c>ParseClaim</c>.</summary>
		private static string Materials(string Claim)
		{
			KingdomMaterialDebitCost parsed;
			KingdomMaterialDebitCost.TryParseClaim(Claim, out parsed);
			return (parsed ?? new KingdomMaterialDebitCost()).Materials.Describe();
		}

		[Test]
		public void PairConsentNamesTheReturnRowsMaterialDebitToo()
		{
			StringAssert.Contains(
				"+\"\\nReturn:\"+incoming.WaterDrams+\"drams,\"+ParseClaim(incoming.MaterialClaim).Materials.Describe()",
				Squash(Read(PairingPath)),
				"the return row must name its material debit in the same voice as the bootstrap row");
			StringAssert.Contains(
				"+ParseClaim(outgoing.MaterialClaim).Materials.Describe()", Squash(Read(PairingPath)),
				"the bootstrap row's material debit is the voice being matched");

			foreach (KingdomPurposePortfolioRecipe row in KingdomPurposePortfolioRules.AllRecipes())
				Assert.IsFalse(string.IsNullOrEmpty(Materials(row.MaterialClaim)),
					row.CargoKey + " declares materials that consent must name");

			KingdomPurposePortfolioRecipe returnRow = Row(KingdomPurposeKind.Forge,
				KingdomPurposeKind.Deep);
			string named = Materials(returnRow.MaterialClaim);
			StringAssert.Contains("2", named);
			StringAssert.Contains("4", named);
			Assert.AreNotEqual(Materials(Row(KingdomPurposeKind.Deep,
				KingdomPurposeKind.Forge).MaterialClaim), named,
				"a Deep-first pair's two rows debit different materials, so hiding one hides a real cost");
		}

		[Test]
		public void ConsentCarriageAgreesWithTheLandingAuthority()
		{
			foreach (KingdomPurposePortfolioRecipe row in KingdomPurposePortfolioRules.AllRecipes())
			{
				int debited;
				int landed;
				int lost;
				Assert.IsTrue(KingdomPurposePortfolioRules.TryCarriedFood(row.Source,
					row.Destination, out debited, out landed, out lost), row.CargoKey);
				string rendered = CarriageLine("Provision carried", row, row.Destination);
				if (landed <= 0)
				{
					Assert.AreEqual("", rendered, row.CargoKey);
					continue;
				}
				StringAssert.Contains("{{C|" + landed + " of " + debited + " food}}", rendered,
					row.CargoKey + ": consent must state the figures the landing moves");
				StringAssert.Contains("{{C|" + lost + "}} lost in carriage.", rendered,
					row.CargoKey);
			}
		}
	}
}
#endif
