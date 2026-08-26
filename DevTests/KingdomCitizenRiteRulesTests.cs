#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Lane 1: whether one of the founder's own settlers may host Qud's water ritual.
	/// <para>
	/// BUILDING-CATALOGUE-BRIEF Addendum 13. The two refusals at the bottom of the order are the
	/// two documented ways the engine's ritual FAILS HARD rather than declining — an unregistered
	/// base faction throws out of <c>Factions.Get</c>, and a named-but-unknown ritual liquid nulls
	/// out of <c>WaterRitual.LiquidName</c> — so both must be refused before a settler is ever made
	/// a host, and both must be reported.
	/// </para>
	/// </summary>
	internal class KingdomCitizenRiteRulesTests
	{
		private const string RealmA =
			"taf:realm:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string RealmB =
			"taf:realm:v1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		[Test]
		public void LiveRiteLogicalSourceKeepsTallyAndMutationOrder()
		{
			string source = KingdomCitizenRiteLogicalSource.Read();
			Ordered(source,
				"public sealed class RiteTally",
				"internal int Hosts;",
				"internal int Citizens;",
				"internal CitizenRiteVerdict Worst;",
				"internal string Liquid;",
				"public static RiteTally Begin(",
				"public static void Observe(",
				"public static void Close(",
				"private static void Chronicle(",
				"JournalAPI.GetObservation(id)",
				"JournalAPI.AddObservation(",
				"public static CitizenRiteVerdict Host(",
				"Citizen.AddPart<GivesRep>()",
				"rep.FillInRelatedFactions(Initial: true);",
				"Speak(System, Citizen);",
				"Citizen.SetIntProperty(HostProperty, 1);",
				"private static void Speak(",
				"ConversationsAPI.addSimpleConversationToObject",
				"citizen.SetIntProperty(ConversationProperty, 1);",
				"citizen.SetIntProperty(GreetingBandProperty, band + 1);");
			StringAssert.DoesNotContain("internal int Hosts =", source);
			StringAssert.DoesNotContain("internal int Citizens =", source);
			StringAssert.DoesNotContain("internal CitizenRiteVerdict Worst =", source);
			StringAssert.DoesNotContain("internal string Liquid =", source);
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		/// <summary>Everything true is a host.</summary>
		[Test]
		public void Judge_EverythingInPlaceIsAHost()
		{
			Assert.AreEqual(CitizenRiteVerdict.Host,
				KingdomCitizenRiteRules.Judge(true, true, true, true, true));
		}

		/// <summary>The order is frozen, so combined-invalid input cannot vary: unfounded, then not
		/// a citizen, then no body, then the faction, then the liquid.</summary>
		[TestCase(false, false, false, false, false, CitizenRiteVerdict.Unfounded)]
		[TestCase(true, false, false, false, false, CitizenRiteVerdict.NotCitizen)]
		[TestCase(true, true, false, false, false, CitizenRiteVerdict.NoBody)]
		[TestCase(true, true, true, false, false, CitizenRiteVerdict.UnknownFaction)]
		[TestCase(true, true, true, true, false, CitizenRiteVerdict.UnknownLiquid)]
		public void Judge_OrderIsFrozen(bool founded, bool citizen, bool body, bool faction, bool liquid, CitizenRiteVerdict expected)
		{
			Assert.AreEqual(expected, KingdomCitizenRiteRules.Judge(founded, citizen, body, faction, liquid));
		}

		/// <summary>The two fatal verdicts announce, and every other verdict is silent. STANDARDS
		/// §7b's split: "not applicable" says nothing, "applicable but blocked" must say why.</summary>
		[TestCase(CitizenRiteVerdict.Host, false)]
		[TestCase(CitizenRiteVerdict.Unfounded, false)]
		[TestCase(CitizenRiteVerdict.NotCitizen, false)]
		[TestCase(CitizenRiteVerdict.NoBody, false)]
		[TestCase(CitizenRiteVerdict.UnknownFaction, true)]
		[TestCase(CitizenRiteVerdict.UnknownLiquid, true)]
		public void BlockedLine_SpeaksOnlyForTheBlockingVerdicts(CitizenRiteVerdict verdict, bool speaks)
		{
			Assert.AreEqual(speaks, !string.IsNullOrEmpty(KingdomCitizenRiteRules.BlockedLine(verdict, "Kavvat", "brainbrine")));
		}

		/// <summary>A blocked line names the city and, for a bad liquid, the liquid — the two facts
		/// somebody would need to go and fix it.</summary>
		[Test]
		public void BlockedLine_NamesTheCityAndTheLiquid()
		{
			string line = KingdomCitizenRiteRules.BlockedLine(CitizenRiteVerdict.UnknownLiquid, "Kavvat", "brainbrine");
			StringAssert.Contains("Kavvat", line);
			StringAssert.Contains("brainbrine", line);
			StringAssert.Contains("Kavvat", KingdomCitizenRiteRules.BlockedLine(CitizenRiteVerdict.UnknownFaction, "Kavvat", null));
		}

		/// <summary>A blocked line with nothing to name still reads as a sentence rather than as a
		/// hole.</summary>
		[Test]
		public void BlockedLine_DegradesRatherThanBlanking()
		{
			string line = KingdomCitizenRiteRules.BlockedLine(CitizenRiteVerdict.UnknownLiquid, "", "");
			StringAssert.Contains("your city", line);
			StringAssert.Contains("something", line);
		}

		/// <summary>The greeting reads off the shared-living counter the inward rite already keeps,
		/// so a settler who has lived here a season does not greet the founder like a
		/// newcomer.</summary>
		[Test]
		public void Greeting_ChangesWithHowLongTheyHaveLivedHere()
		{
			string stranger = KingdomCitizenRiteRules.Greeting("Kavvat", 0);
			string settling = KingdomCitizenRiteRules.Greeting("Kavvat", 1);
			string settled = KingdomCitizenRiteRules.Greeting("Kavvat", KingdomCitizenRiteRules.SettledDays);
			Assert.AreNotEqual(stranger, settling);
			Assert.AreNotEqual(settling, settled);
			StringAssert.Contains("Kavvat", stranger);
			StringAssert.Contains("Kavvat", settled);
		}

		/// <summary>A city with no name still greets. The line degrades to "here" rather than to a
		/// gap where a name should be.</summary>
		[Test]
		public void Greeting_DegradesWithoutACityName()
		{
			StringAssert.Contains("here", KingdomCitizenRiteRules.Greeting("", 0));
		}

		/// <summary>
		/// The band is what a caller re-reads to notice a settler has earned a different greeting.
		/// A conversation is a fixed string on the object, so without this every settler would keep
		/// the newcomer's line forever and two of the three greetings would be unreachable.
		/// </summary>
		[TestCase(0, 0)]
		[TestCase(1, 1)]
		[TestCase(KingdomCitizenRiteRules.SettledDays - 1, 1)]
		[TestCase(KingdomCitizenRiteRules.SettledDays, 2)]
		[TestCase(KingdomCitizenRiteRules.SettledDays + 500, 2)]
		[TestCase(-3, 0)]
		public void Band_HasARungPerGreeting(int sharedDays, int expected)
		{
			Assert.AreEqual(expected, KingdomCitizenRiteRules.Band(sharedDays));
		}

		/// <summary>Every band has its own line, and no two share one. A band that mapped two rungs
		/// onto one greeting would make the rung pointless.</summary>
		[Test]
		public void Greeting_IsOnePerBand()
		{
			Assert.AreEqual(KingdomCitizenRiteRules.Greeting("Kavvat", 0), KingdomCitizenRiteRules.Greeting("Kavvat", -1));
			Assert.AreNotEqual(KingdomCitizenRiteRules.Greeting("Kavvat", 0), KingdomCitizenRiteRules.Greeting("Kavvat", 1));
			Assert.AreEqual(KingdomCitizenRiteRules.Greeting("Kavvat", 1),
				KingdomCitizenRiteRules.Greeting("Kavvat", KingdomCitizenRiteRules.SettledDays - 1));
		}

		/// <summary>The parting is Qud's own, because the whole act is borrowed from it.</summary>
		[Test]
		public void Farewell_IsQudsOwn()
		{
			Assert.AreEqual("Live and drink.", KingdomCitizenRiteRules.Farewell());
		}

		// ---- The chronicle as a tradable secret (W5's remainder, W6) --------------------------

		/// <summary>
		/// The id is derived from the realm and the words, which is what makes filing the same
		/// telling twice a no-op without a cursor into a register that gets trimmed at two hundred
		/// entries.
		/// </summary>
		[Test]
		public void TheSameTellingAlwaysYieldsTheSameSecretId()
		{
			string id;
			string text;
			Assert.IsTrue(KingdomCitizenRiteRules.TryTradableSecret(RealmA, "Travelers claim that the well ran dry.", out id, out text));
			string again;
			string sameText;
			Assert.IsTrue(KingdomCitizenRiteRules.TryTradableSecret(RealmA, "Travelers claim that the well ran dry.", out again, out sameText));
			Assert.AreEqual(id, again);
			Assert.AreEqual(text, sameText);
		}

		/// <summary>Two realms telling the same thing are two secrets, because they are about two
		/// different cities.</summary>
		[Test]
		public void TwoRealmsTellingTheSameThingAreTwoSecrets()
		{
			string a;
			string b;
			string text;
			Assert.IsTrue(KingdomCitizenRiteRules.TryTradableSecret(RealmA, "Travelers claim that the well ran dry.", out a, out text));
			Assert.IsTrue(KingdomCitizenRiteRules.TryTradableSecret(RealmB, "Travelers claim that the well ran dry.", out b, out text));
			Assert.AreNotEqual(a, b);
		}

		/// <summary>The text that travels is the OUTSIDER register's line, handed through unaltered:
		/// this file composes no prose of its own for the roads to carry.</summary>
		[Test]
		public void TheSecretCarriesTheOutsiderLineWordForWord()
		{
			string id;
			string text;
			Assert.IsTrue(KingdomCitizenRiteRules.TryTradableSecret(RealmA, "Some deny that Kavvat took in a hundred settlers.", out id, out text));
			Assert.AreEqual("Some deny that Kavvat took in a hundred settlers.", text);
			StringAssert.StartsWith("taf:chronicle:" + RealmA + ":", id);
		}

		[Test]
		public void ARealmWithNoNameOrATellingWithNoWordsFilesNothing()
		{
			string id;
			string text;
			Assert.IsFalse(KingdomCitizenRiteRules.TryTradableSecret("", "a line", out id, out text));
			Assert.AreEqual("", id);
			Assert.IsFalse(KingdomCitizenRiteRules.TryTradableSecret(RealmA, "", out id, out text));
			Assert.IsFalse(KingdomCitizenRiteRules.TryTradableSecret(null, null, out id, out text));
		}

		[Test]
		public void MutableRealmNamesNeverBecomeSecretIdentityKeys()
		{
			string id;
			string text;
			Assert.IsFalse(KingdomCitizenRiteRules.TryTradableSecret(
				"taf_kingdom_kavvat", "a line", out id, out text));
			Assert.AreEqual("", id);
			Assert.AreEqual("", text);
		}

		/// <summary>
		/// The tags are vanilla's, and they are the two a city's history honestly is. The category
		/// decides which of the ritual's two elements offers it: "Gossip" is the gossip element's
		/// own filter in WaterRitualSellSecret.GetWeight.
		/// </summary>
		[Test]
		public void TheSecretIsTaggedInVanillasOwnInterestVocabulary()
		{
			string[] tags = KingdomCitizenRiteRules.SecretTags();
			Assert.AreEqual(2, tags.Length);
			CollectionAssert.Contains(tags, "gossip");
			CollectionAssert.Contains(tags, "settlement");
			Assert.AreEqual("Gossip", KingdomCitizenRiteRules.SecretCategory);
		}
	}
}
#endif
