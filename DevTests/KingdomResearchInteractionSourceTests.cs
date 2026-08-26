#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomResearchInteractionSourceTests
	{
		[Test]
		public void SubjectSelectionLivesOnARealBenchAndNotTheCharterReading()
		{
			string inquiry = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomInquiry.cs"));
			string research = KingdomResearchLogicalSource.Read();
			string zoning = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomZoning.cs"));
			StringAssert.Contains("GetInventoryActionsEvent.ID", inquiry);
			StringAssert.Contains("set the city's research subject", inquiry);
			StringAssert.Contains("KingdomResearch.OpenBench(ParentObject, E.Actor)", inquiry);
			StringAssert.Contains("public static void OpenBench(GameObject Bench, GameObject Actor)", research);
			StringAssert.Contains("Actor.CurrentZone != zone", research);
			StringAssert.Contains("system.ClaimedZones.Contains(zone.ZoneID)", research);
			StringAssert.Contains("Bench.HasPart<XRL.World.Parts.r_KingdomInquiry>()", research);
			StringAssert.Contains("TakeUp(system, subjects[chosen].Key", research);
			StringAssert.DoesNotContain("Set the keepers a thing to work out", zoning);
			StringAssert.DoesNotContain("private static void SetSubject", zoning);
			Assert.AreEqual(0, Occurrences(zoning, "KingdomResearch.TakeUp("),
				"the Charter/keepers reading must have no hidden research mutation route");
		}

		[Test]
		public void RegistryReplacementPreservesOrderAndReloadInvalidatesEveryCache()
		{
			string source = KingdomResearchLogicalSource.Read();
			int reload = source.IndexOf("public static void Reload()", StringComparison.Ordinal);
			int ensure = source.IndexOf("private static void EnsureLoaded()", reload,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(reload, 0);
			Assert.Greater(ensure, reload);
			string reloadBody = source.Substring(reload, ensure - reload);
			AssertBefore(reloadBody, "_nodes = null;", "ByKey.Clear();");
			AssertBefore(reloadBody, "ByKey.Clear();", "QuestCache.Clear();");
			AssertBefore(reloadBody, "QuestCache.Clear();", "NotesFiled = false;");
			int forget = source.IndexOf("public static void ForgetQuests()", reload,
				StringComparison.Ordinal);
			Assert.Greater(forget, reload);
			Assert.Less(forget, ensure);
			string forgetBody = source.Substring(forget, ensure - forget);
			StringAssert.Contains("QuestCache.Clear();", forgetBody);
			StringAssert.DoesNotContain("ByKey.Clear();", forgetBody);
			StringAssert.DoesNotContain("NotesFiled = false;", forgetBody);

			int handler = source.IndexOf("private static void HandleNode(", ensure,
				StringComparison.Ordinal);
			int notes = source.IndexOf("public static string NoteId(", handler,
				StringComparison.Ordinal);
			Assert.Greater(handler, ensure);
			Assert.Greater(notes, handler);
			string body = source.Substring(handler, notes - handler);
			AssertBefore(body, "if (_nodes[i].Key == node.Key)", "_nodes[i] = node;");
			AssertBefore(body, "_nodes[i] = node;", "ByKey[node.Key] = node;");
			AssertBefore(body, "ByKey[node.Key] = node;", "xml.DoneWithElement();");
			AssertBefore(body, "xml.DoneWithElement();", "_nodes.Add(node);");
			AssertBefore(body, "_nodes.Add(node);", "ByKey[node.Key] = node;",
				body.IndexOf("_nodes.Add(node);", StringComparison.Ordinal));
		}

		private static void AssertBefore(string source, string first, string second,
			int start = 0)
		{
			int left = source.IndexOf(first, start, StringComparison.Ordinal);
			int right = source.IndexOf(second, left + first.Length, StringComparison.Ordinal);
			Assert.GreaterOrEqual(left, 0, first);
			Assert.Greater(right, left, second);
		}

		private static int Occurrences(string text, string value)
		{
			int count = 0;
			for (int at = 0; (at = text.IndexOf(value, at, StringComparison.Ordinal)) >= 0;
				at += value.Length) count++;
			return count;
		}
	}
}
#endif
