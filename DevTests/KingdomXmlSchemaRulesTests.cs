#if TAF_TESTS
using System;
using System.IO;
using System.Xml;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomXmlSchemaRulesTests
	{
		[TestCase(null, 0, KingdomXmlSchemaVerdict.LegacyUnversioned)]
		[TestCase("1", 1, KingdomXmlSchemaVerdict.Compatible)]
		[TestCase("2", 2, KingdomXmlSchemaVerdict.Unsupported)]
		[TestCase("0", 0, KingdomXmlSchemaVerdict.Unsupported)]
		[TestCase("", 0, KingdomXmlSchemaVerdict.Malformed)]
		[TestCase("01", 0, KingdomXmlSchemaVerdict.Malformed)]
		[TestCase(" 1", 0, KingdomXmlSchemaVerdict.Malformed)]
		[TestCase("1 ", 0, KingdomXmlSchemaVerdict.Malformed)]
		[TestCase("-1", 0, KingdomXmlSchemaVerdict.Malformed)]
		[TestCase("future", 0, KingdomXmlSchemaVerdict.Malformed)]
		public void JudgePinsLegacyCurrentFutureAndMalformedRoots(string declared, int version,
			KingdomXmlSchemaVerdict expected)
		{
			int parsed;
			Assert.AreEqual(expected, KingdomXmlSchemaRules.Judge(declared, out parsed));
			Assert.AreEqual(version, parsed);
		}

		[TestCase(KingdomXmlSchemaVerdict.Compatible, true)]
		[TestCase(KingdomXmlSchemaVerdict.LegacyUnversioned, true)]
		[TestCase(KingdomXmlSchemaVerdict.Unsupported, false)]
		[TestCase(KingdomXmlSchemaVerdict.Malformed, false)]
		public void OnlyCurrentAndUnversionedLegacyStreamsLoad(
			KingdomXmlSchemaVerdict verdict, bool readable)
		{
			Assert.AreEqual(readable, KingdomXmlSchemaRules.IsReadable(verdict));
		}

		[TestCase("KingdomBuildings.xml", "kingdombuildings")]
		[TestCase("KingdomDeals.xml", "kingdomdeals")]
		[TestCase("KingdomYardWorks.xml", "kingdomyardworks")]
		[TestCase("KingdomResearch.xml", "kingdomresearch")]
		[TestCase("KingdomProcedures.xml", "kingdomprocedures")]
		[TestCase("KingdomRaidProfiles.xml", "kingdomraidprofiles")]
		public void EveryShippedPublicRegistryDeclaresSchemaOne(string file, string root)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(TestMain.ReadRepositoryText(file));
			Assert.AreEqual(root, document.DocumentElement.Name);
			Assert.AreEqual(KingdomXmlSchemaRules.CurrentVersion.ToString(),
				document.DocumentElement.GetAttribute("Schema"));
		}

		[TestCase("Core/KingdomData.cs", "KingdomBuildings")]
		[TestCase("Core/KingdomData.cs", "KingdomDeals")]
		[TestCase("Core/KingdomData.cs", "KingdomYardWorks")]
		[TestCase("Growth/KingdomResearch.cs", "KingdomResearch")]
		[TestCase("Growth/KingdomProcedures.cs", "KingdomProcedures")]
		[TestCase("Raids/KingdomRaidProfiles.cs", "KingdomRaidProfiles")]
		public void EveryPublicRegistryLoaderUsesOneFailClosedSchemaBoundary(
			string file, string registry)
		{
			string source = string.Equals(file, "Core/KingdomData.cs",
				StringComparison.Ordinal) ? KingdomDataLogicalSource.Read()
				: string.Equals(file, "Growth/KingdomResearch.cs",
					StringComparison.Ordinal) ? KingdomResearchLogicalSource.Read()
					: string.Equals(file, "Growth/KingdomProcedures.cs",
						StringComparison.Ordinal) ? KingdomProceduresLogicalSource.Read()
					: TestMain.ReadRepositoryText(file);
			StringAssert.Contains(
				"KingdomXmlSchema.HandleRoot(xml, ", source.Replace("item, ", "xml, "));
			StringAssert.Contains("\"" + registry + "\"", source);
			string boundary = TestMain.ReadRepositoryText("Core/KingdomXmlSchema.cs");
			StringAssert.Contains("KingdomXmlSchemaRules.Judge", boundary);
			StringAssert.Contains("this stream was ignored", boundary);
		}
	}
}
#endif
