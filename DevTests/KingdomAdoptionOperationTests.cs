using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomAdoptionOperationTests
	{
		[Test]
		public void StaffedWorkReceiptRoundTripsAndBindsEveryOperatingFact()
		{
			Assert.That(KingdomAdoptionOperationRules.TryCreate("root-1", "smithy",
				"craft", 3, true, out KingdomAdoptionOperationReceipt receipt,
				out string failure), Is.True, failure);
			string encoded = KingdomAdoptionOperationRules.Encode(receipt);
			Assert.That(KingdomAdoptionOperationRules.TryDecode(encoded,
				out KingdomAdoptionOperationReceipt read, out failure), Is.True, failure);
			Assert.Multiple(() => {
				Assert.That(read.RootId, Is.EqualTo("root-1"));
				Assert.That(read.BuildingKey, Is.EqualTo("smithy"));
				Assert.That(read.Category, Is.EqualTo("craft"));
				Assert.That(read.StaffNeeded, Is.EqualTo(3));
				Assert.That(read.ThresholdManning, Is.True);
			});
			Assert.That(KingdomAdoptionOperationRules.TryCreate("root-1", "smithy",
				"craft", 2, true, out KingdomAdoptionOperationReceipt changed, out failure),
				Is.True, failure);
			Assert.That(changed.Revision, Is.Not.EqualTo(receipt.Revision));
			Assert.That(KingdomAdoptionOperationRules.TryDecode(encoded + "x", out _, out _),
				Is.False);
		}

		[TestCase("housing", 2)]
		[TestCase("storage", 2)]
		[TestCase("craft", 0)]
		public void HousingStorageAndStafflessRolesCannotMintOperationAuthority(
			string category, int staff)
		{
			Assert.That(KingdomAdoptionOperationRules.RequiresContract(category, staff),
				Is.False);
			Assert.That(KingdomAdoptionOperationRules.TryCreate("root-1", "design",
				category, staff, false, out _, out _), Is.False);
		}

		[Test]
		public void AdoptionPublishesOnlySignedStaffingMetadataAndSurveyReprovesIt()
		{
			string designation = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomAdoptionDesignation.cs"));
			string operation = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomAdoptionOperation.cs"));
			string survey = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomSurvey.01.Capture.cs"));
			string crews = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomCrews.Assignments.cs"));
			StringAssert.Contains("KingdomAdoptionOperation.TryStamp", designation);
			StringAssert.Contains("TryReproveLocal", designation);
			StringAssert.Contains("KingdomUpgrade.BuildKeyProperty", operation);
			StringAssert.Contains("KingdomAdopt.StaffNeededProperty", operation);
			StringAssert.Contains("CategoryProperty", operation);
			StringAssert.DoesNotContain("Carries", operation);
			StringAssert.DoesNotContain("Provides", operation);
			StringAssert.Contains("KingdomAdoptionOperation.TryRead(Item", survey);
			Assert.That(crews.IndexOf("KingdomAdoptionOperation.TryRead(work"),
				Is.LessThan(crews.IndexOf("new KingdomCrewRules.CrewDemand(")),
				"reservation demand must re-prove signed adopted operation first");
			StringAssert.Contains("work.Blueprint == KingdomAdopt.WorkMarkerBlueprint", crews,
				"removing the positive adopted bit cannot turn a marker into an ordinary work");
		}

		[Test]
		public void ReleasePreflightsAuthorityAndClearsBothPropertyTypes()
		{
			string release = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomAdopt.Release.cs"));
			string designation = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomAdoptionDesignation.cs"));
			string operation = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomAdoptionOperation.cs"));
			string transaction = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomAdopt.Transaction.cs"));
			Assert.That(release.IndexOf("KingdomDesignationReleaseAuthority.TryCanRelease"),
				Is.LessThan(release.IndexOf("KingdomAdoptionDesignation.Clear(Adopted)")));
			Assert.That(release.IndexOf("KingdomAdoptionDesignation.Clear(Adopted)"),
				Is.LessThan(release.IndexOf("ClearTyped(Adopted, AdoptedProperty)")));
			Assert.That(transaction.IndexOf("KingdomAdoptionDesignation.Clear(Target)"),
				Is.LessThan(transaction.IndexOf("ClearTyped(Target, AdoptedProperty)")));
			StringAssert.Contains("RemoveIntProperty(Property)", designation);
			StringAssert.Contains("RemoveStringProperty(Property)", designation);
			foreach (string property in new[] { "KingdomUpgrade.BuildKeyProperty",
				"KingdomAdopt.StaffNeededProperty", "ThresholdProperty", "KingdomStaffed",
				"KingdomEffectiveness", "KingdomCrews.IdentityAffinityProperty" })
				StringAssert.Contains(property, operation,
					"operation cleanup omits " + property);
			StringAssert.Contains("this object already has a complete adoption", transaction,
				"recovery must not reopen a fully published adoption transaction");
		}
	}
}
