#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Api;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The published contract's pure half: who is admitted, what a refusal says, and the clamps
	/// every extension-supplied string passes through.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE §6.6. The version table is the load-bearing one — a mod built
	/// against the wrong API must be REFUSED BY NAME, never silently skipped, because a player
	/// attributes missing behaviour to us.
	/// </para>
	/// </summary>
	internal class KingdomApiRulesTests
	{
		private const string Mod = "Someone Else's Mod";

		/// <summary>The supported v1-v2 window admits. Later versions refuse; absent versions do
		/// not silently enter.</summary>
		[TestCase(1, KingdomExtensionVerdict.Accepted)]
		[TestCase(2, KingdomExtensionVerdict.Accepted)]
		[TestCase(3, KingdomExtensionVerdict.Accepted)]
		[TestCase(4, KingdomExtensionVerdict.RefusedAhead)]
		[TestCase(99, KingdomExtensionVerdict.RefusedAhead)]
		[TestCase(0, KingdomExtensionVerdict.RefusedNoVersion)]
		[TestCase(-4, KingdomExtensionVerdict.RefusedNoVersion)]
		public void Judge_AdmitsThePublishedCompatibilityWindow(int declared, KingdomExtensionVerdict expected)
		{
			ClassicAssert.AreEqual(expected, KingdomApiRules.Judge(Mod, declared, true));
		}

		/// <summary>The published version is 2 and this test says so out loud: a bump that forgets
		/// to think about every consumer fails here first.</summary>
		[Test]
		public void Version_IsThreeAndOlderSourcesRemainSupported()
		{
			ClassicAssert.AreEqual(3, KingdomApiRules.Version);
			ClassicAssert.AreEqual(1, KingdomApiRules.MinSupportedVersion);
			ClassicAssert.AreEqual(KingdomExtensionVerdict.Accepted, KingdomApiRules.Judge(Mod, 1, true));
			ClassicAssert.AreEqual(KingdomExtensionVerdict.Accepted, KingdomApiRules.Judge(Mod, KingdomApiRules.Version, true));
			ClassicAssert.AreEqual(KingdomExtensionVerdict.RefusedAhead, KingdomApiRules.Judge(Mod, KingdomApiRules.Version + 1, true));
		}

		[Test]
		public void RulesAndVerdictKeepExactPublicAbi()
		{
			System.Type rules = typeof(KingdomApiRules);
			ClassicAssert.AreEqual("ThousandAndFirst.Api.KingdomApiRules", rules.FullName);
			ClassicAssert.IsTrue(rules.IsPublic && rules.IsAbstract && rules.IsSealed);

			System.Type verdict = typeof(KingdomExtensionVerdict);
			ClassicAssert.AreEqual("ThousandAndFirst.Api.KingdomExtensionVerdict", verdict.FullName);
			ClassicAssert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(verdict));
			CollectionAssert.AreEqual(new string[]
			{
				"Accepted", "RefusedNoVersion", "RefusedAhead", "RefusedBehind",
				"RefusedNoContract", "RefusedUnnamed", "RefusedThrew",
				"RefusedNamespaceCollision"
			}, System.Enum.GetNames(verdict));
			System.Array values = System.Enum.GetValues(verdict);
			for (int i = 0; i < values.Length; i++)
			{
				ClassicAssert.AreEqual(i, (int)(KingdomExtensionVerdict)values.GetValue(i));
			}

			System.Reflection.MethodInfo[] methods = rules.GetMethods(
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static |
				System.Reflection.BindingFlags.DeclaredOnly);
			string[] actual = new string[methods.Length];
			for (int i = 0; i < methods.Length; i++)
			{
				System.Reflection.ParameterInfo[] parameters = methods[i].GetParameters();
				string[] parameterTypes = new string[parameters.Length];
				for (int j = 0; j < parameters.Length; j++)
				{
					parameterTypes[j] = parameters[j].ParameterType.FullName;
				}
				actual[i] = methods[i].Name + "(" + string.Join(",", parameterTypes) + ")->"
					+ methods[i].ReturnType.FullName;
			}
			string[] expected = new string[]
			{
				"BehaviourIdentifier(System.String,System.Boolean)->System.String",
				"ExtensionKey(System.String,System.String)->System.String",
				"IdentityAffinity(System.Int32)->System.Int32",
				"IdentityKey(System.String,System.String)->System.String",
				"IdentityName(System.String)->System.String",
				"IdentityWorkKind(System.String)->System.String",
				"Judge(System.String,System.Int32,System.Boolean)->ThousandAndFirst.Api.KingdomExtensionVerdict",
				"Judge(System.String,System.Int32,System.Boolean,System.Int32)->ThousandAndFirst.Api.KingdomExtensionVerdict",
				"Kind(System.String)->System.String",
				"RefusalLine(ThousandAndFirst.Api.KingdomExtensionVerdict,System.String,System.Int32)->System.String",
				"RefusalLine(ThousandAndFirst.Api.KingdomExtensionVerdict,System.String,System.Int32,System.Int32)->System.String",
				"Slug(System.String)->System.String",
				"Trim(System.String)->System.String",
				"Trim(System.String,System.Int32)->System.String",
				"TryStream(System.String,System.String,System.String&)->System.Boolean"
			};
			System.Array.Sort(actual, System.StringComparer.Ordinal);
			System.Array.Sort(expected, System.StringComparer.Ordinal);
			CollectionAssert.AreEqual(expected, actual);
		}

		/// <summary>
		/// Every version in the supported window is admitted, not just the newest. STANDARDS §9
		/// promises a supported contract keeps working for at least one minor cycle after it
		/// changes, and a check that admitted only the current version would break that promise on
		/// the day of the bump — refusing every extension in the world at once.
		/// </summary>
		[Test]
		public void Judge_AdmitsTheWholeSupportedWindow()
		{
			ClassicAssert.LessOrEqual(KingdomApiRules.MinSupportedVersion, KingdomApiRules.Version);
			ClassicAssert.GreaterOrEqual(KingdomApiRules.MinSupportedVersion, 1);
			for (int version = KingdomApiRules.MinSupportedVersion; version <= KingdomApiRules.Version; version++)
			{
				ClassicAssert.AreEqual(KingdomExtensionVerdict.Accepted, KingdomApiRules.Judge(Mod, version, true),
					"version " + version + " is inside the supported window");
			}
			// Below the window is always a refusal. WHICH refusal depends on where the window
			// starts: while the floor is 1, one below it is zero, and zero is "declared none"
			// rather than "too old" — a distinction the modder needs, because the fixes differ.
			KingdomExtensionVerdict below = KingdomApiRules.Judge(Mod, KingdomApiRules.MinSupportedVersion - 1, true);
			ClassicAssert.AreNotEqual(KingdomExtensionVerdict.Accepted, below);
			ClassicAssert.AreEqual((KingdomApiRules.MinSupportedVersion > 1)
				? KingdomExtensionVerdict.RefusedBehind
				: KingdomExtensionVerdict.RefusedNoVersion, below);
		}

		/// <summary>A class that threw is told it threw, not that it forgot to declare a version.
		/// Sending a modder to the wrong line is the whole reason this verdict exists.</summary>
		[Test]
		public void RefusalLine_ThrowingIsItsOwnVerdict()
		{
			string line = KingdomApiRules.RefusalLine(KingdomExtensionVerdict.RefusedThrew, Mod, 0);
			StringAssert.Contains(Mod, line);
			StringAssert.Contains("threw", line);
			StringAssert.DoesNotContain("declares no API version", line);
			string collision = KingdomApiRules.RefusalLine(
				KingdomExtensionVerdict.RefusedNamespaceCollision, Mod, 0);
			StringAssert.Contains(Mod, collision);
			StringAssert.Contains("Both owners are refused", collision);
		}

		/// <summary>Nameless first, then contract, then version. A refusal that cannot name its
		/// owner is the one failure the whole contract exists to prevent, so it outranks
		/// everything — including a version that is also wrong.</summary>
		[Test]
		public void Judge_OrderIsFrozen_NamelessOutranksEverything()
		{
			ClassicAssert.AreEqual(KingdomExtensionVerdict.RefusedUnnamed, KingdomApiRules.Judge("", 99, false));
			ClassicAssert.AreEqual(KingdomExtensionVerdict.RefusedUnnamed, KingdomApiRules.Judge("   ", 1, true));
			ClassicAssert.AreEqual(KingdomExtensionVerdict.RefusedNoContract, KingdomApiRules.Judge(Mod, 99, false));
		}

		/// <summary>Every refusal line names the mod, and the version refusals name both versions.
		/// Those are the three facts a player pastes into a bug report.</summary>
		[TestCase(KingdomExtensionVerdict.RefusedAhead, 7)]
		[TestCase(KingdomExtensionVerdict.RefusedBehind, 0)]
		public void RefusalLine_NamesTheModAndBothVersions(KingdomExtensionVerdict verdict, int declared)
		{
			string line = KingdomApiRules.RefusalLine(verdict, Mod, declared);
			StringAssert.Contains(Mod, line);
			StringAssert.Contains(declared.ToString(), line);
			StringAssert.Contains(KingdomApiRules.Version.ToString(), line);
		}

		/// <summary>An accepted extension is not announced. A registry that narrated its successes
		/// would bury the one line that matters.</summary>
		[Test]
		public void RefusalLine_IsEmptyForAnAcceptedExtension()
		{
			ClassicAssert.AreEqual("", KingdomApiRules.RefusalLine(KingdomExtensionVerdict.Accepted, Mod, 1));
		}

		/// <summary>Slugging folds to the kernel identifier alphabet, collapses separator runs, and
		/// never leaves a leading or trailing hyphen.</summary>
		[TestCase("Someone Else's Mod", "someone-else-s-mod")]
		[TestCase("  spaces  ", "spaces")]
		[TestCase("A..B", "a..b")]
		[TestCase("!!!", "")]
		[TestCase(null, "")]
		[TestCase("Mod_9.1", "mod_9.1")]
		public void Slug_FoldsToTheKernelAlphabet(string source, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomApiRules.Slug(source));
		}

		/// <summary>Every extension stream begins with the shared prefix and carries both the mod
		/// and its own lane, so two mods at ordinal zero cannot collide.</summary>
		[Test]
		public void TryStream_CarriesBothTheModAndTheLane()
		{
			string stream;
			ClassicAssert.IsTrue(KingdomApiRules.TryStream("Their Mod", "weather", out stream));
			ClassicAssert.AreEqual("taf:ext:their-mod:weather", stream);

			string firstKey, secondKey;
			ClassicAssert.IsTrue(KingdomHappeningCursorRules.TrySourceKey(
				"their-manifest", "Their.Assembly", "Their.FirstSource", out firstKey));
			ClassicAssert.IsTrue(KingdomHappeningCursorRules.TrySourceKey(
				"their-manifest", "Their.Assembly", "Their.SecondSource", out secondKey));
			ClassicAssert.AreNotEqual(firstKey, secondKey);
			string otherAssemblyKey;
			ClassicAssert.IsTrue(KingdomHappeningCursorRules.TrySourceKey(
				"their-manifest", "Other.Assembly", "Their.FirstSource", out otherAssemblyKey));
			ClassicAssert.AreNotEqual(firstKey, otherAssemblyKey,
				"same full type name in two assemblies must own distinct receipts");
			string cursors;
			ClassicAssert.IsTrue(KingdomHappeningCursorRules.TryRetain("",
				new[] { firstKey, secondKey }, out cursors));
			long since;
			ClassicAssert.IsTrue(KingdomHappeningCursorRules.TryAdvance(cursors, firstKey, 100L,
				out since, out cursors));
			ClassicAssert.AreEqual(0L, since, "the first exact source call must receive zero");
			ClassicAssert.IsTrue(KingdomHappeningCursorRules.TryAdvance(cursors, secondKey, 100L,
				out since, out cursors));
			ClassicAssert.AreEqual(0L, since, "another source must not inherit the first source's cursor");
			ClassicAssert.IsTrue(KingdomHappeningCursorRules.TryAdvance(cursors, firstKey, 200L,
				out since, out cursors));
			ClassicAssert.AreEqual(100L, since, "cold wire must retain the exact source window");
			ClassicAssert.IsFalse(KingdomHappeningCursorRules.TryAdvance("malformed", firstKey, 300L,
				out since, out cursors));

			ClassicAssert.IsTrue(KingdomHappeningCursorRules.TrySeedLegacy(
				new[] { firstKey, secondKey }, 250L, out cursors));
			ClassicAssert.IsTrue(KingdomHappeningCursorRules.TryAdvance(cursors, firstKey, 300L,
				out since, out cursors));
			ClassicAssert.AreEqual(250L, since,
				"v11 global receipt must prevent replay from zero after cursor migration");
			ClassicAssert.IsFalse(KingdomHappeningCursorRules.TrySeedLegacy(
				new[] { firstKey, firstKey }, 250L, out cursors));
			ClassicAssert.IsFalse(KingdomHappeningCursorRules.TrySeedLegacy(
				new[] { firstKey }, 0L, out cursors));
		}

		/// <summary>A stream that will not fit the kernel's identifier is refused outright, never
		/// truncated: a truncated stream is a silently different random sequence, and two mods
		/// could truncate to the same one.</summary>
		[Test]
		public void TryStream_RefusesRatherThanTruncating()
		{
			string stream;
			ClassicAssert.IsFalse(KingdomApiRules.TryStream("mod", new string('a', 200), out stream));
			ClassicAssert.AreEqual("", stream);
			ClassicAssert.IsFalse(KingdomApiRules.TryStream("!!!", "lane", out stream));
			ClassicAssert.IsFalse(KingdomApiRules.TryStream("mod", "", out stream));
		}

		/// <summary>Colour is taken away whole: an extension line that opened a span the report
		/// never closed would recolour everything after it, and leaving the code behind would print
		/// a literal "R|" in the founder's report.</summary>
		[Test]
		public void Trim_StripsMarkupWholeAndCollapsesWhitespace()
		{
			ClassicAssert.AreEqual("a red line", KingdomApiRules.Trim("{{R|a  red\n\tline}}"));
			ClassicAssert.AreEqual("plain", KingdomApiRules.Trim("{{rr|plain}}"));
		}

		/// <summary>Braces with no colour code behind them are just braces: only they are dropped,
		/// and the words survive.</summary>
		[Test]
		public void Trim_BracesWithoutACodeLoseOnlyTheBraces()
		{
			ClassicAssert.AreEqual("a longcode|line", KingdomApiRules.Trim("{{a longcode|line}}"));
		}

		/// <summary>Over-long lines are cut at a word boundary with an ellipsis, never refused and
		/// never cut mid-word.</summary>
		[Test]
		public void Trim_CutsAtAWordBoundary()
		{
			string source = new string('x', 40) + " " + new string('y', 40);
			string trimmed = KingdomApiRules.Trim(source, 50);
			ClassicAssert.AreEqual(new string('x', 40) + "…", trimmed);
		}

		/// <summary>The ellipsis counts against the limit. A method whose whole contract is a
		/// ceiling must not return one character over it.</summary>
		[TestCase(10)]
		[TestCase(25)]
		[TestCase(200)]
		public void Trim_NeverExceedsItsLimit(int limit)
		{
			ClassicAssert.LessOrEqual(KingdomApiRules.Trim(new string('x', 500), limit).Length, limit);
			ClassicAssert.LessOrEqual(KingdomApiRules.Trim(new string('y', 300) + " " + new string('z', 300), limit).Length, limit);
		}

		/// <summary>A filing key is a slug, and a slug of nothing is nothing — which the surfaces
		/// read as "drop this ask" rather than as a blank row.</summary>
		[TestCase("Weather!", "weather")]
		[TestCase("", "")]
		public void Kind_IsASlug(string source, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomApiRules.Kind(source));
		}

		/// <summary>A kind longer than the cap is cut rather than refused, because the ask behind
		/// it is still real.</summary>
		[Test]
		public void Kind_ClampsRatherThanRefusing()
		{
			ClassicAssert.AreEqual(KingdomApiRules.MaxKindLength, KingdomApiRules.Kind(new string('k', 90)).Length);
		}

		[TestCase("trade", "someone-else-s-mod:trade")]
		[TestCase("someone-else-s-mod:trade", "someone-else-s-mod:trade")]
		[TestCase("culture:mopango", null)]
		[TestCase("another-mod:trade", null)]
		[TestCase("", null)]
		[TestCase("bad|key", null)]
		public void IdentityKey_FilesOnlyOwnedBoundedKeys(string source, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomApiRules.IdentityKey(Mod, source));
		}

		[Test]
		public void IdentityKey_DropsInsteadOfTruncatingAtTheCollisionBoundary()
		{
			ClassicAssert.IsNull(KingdomApiRules.IdentityKey(Mod,
				new string('x', KingdomApiRules.MaxIdentityKeyLength + 1)));
		}

		[TestCase(20, 70)]
		[TestCase(70, 70)]
		[TestCase(100, 100)]
		[TestCase(130, 130)]
		[TestCase(900, 130)]
		public void IdentityAffinity_ClampsToTheDoctrineBand(int offered, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomApiRules.IdentityAffinity(offered));
		}

		[TestCase(0L, 100)]
		[TestCase(10L, 110)]
		[TestCase(40L, 130)]
		[TestCase(-40L, 70)]
		[TestCase(30L, 130)]
		[TestCase(-30L, 70)]
		public void IdentityAffinityFromDelta_ClampsOnlyTheFinalSum(long delta, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomApiRules.IdentityAffinityFromDelta(delta));
		}

		[Test]
		public void IdentityAffinityComposition_IsOrderIndependentAcrossEarlySaturation()
		{
			long forward = (KingdomApiRules.IdentityAffinity(130) - 100L)
				+ (KingdomApiRules.IdentityAffinity(130) - 100L)
				+ (KingdomApiRules.IdentityAffinity(70) - 100L);
			long reverse = (KingdomApiRules.IdentityAffinity(70) - 100L)
				+ (KingdomApiRules.IdentityAffinity(130) - 100L)
				+ (KingdomApiRules.IdentityAffinity(130) - 100L);
			ClassicAssert.AreEqual(130, KingdomApiRules.IdentityAffinityFromDelta(forward));
			ClassicAssert.AreEqual(KingdomApiRules.IdentityAffinityFromDelta(forward),
				KingdomApiRules.IdentityAffinityFromDelta(reverse));
		}

		[Test]
		public void IdentityReading_IsFrozenBoundedAndControlFree()
		{
			KingdomIdentityReading reading = new KingdomIdentityReading(
				"  Mopango\nfolk  ", new string('s', 200), null, "Mutated Human");
			ClassicAssert.AreEqual("Mopango folk", reading.Culture);
			ClassicAssert.AreEqual(KingdomApiRules.MaxIdentityNameLength, reading.Species.Length);
			ClassicAssert.AreEqual("", reading.Creed);
			ClassicAssert.AreEqual("Mutated Human", reading.Genotype);
		}

		[Test]
		public void IdentityBoundariesAreFrozenAndContainNoEngineType()
		{
			KingdomComputeRefusal refusal;
			string offender;
			ClassicAssert.IsTrue(KingdomComputeSeam.TryValidateType(
				typeof(KingdomIdentityReading), out refusal, out offender), offender);
			ClassicAssert.IsTrue(KingdomComputeSeam.TryValidateType(
				typeof(KingdomIdentityWorkReading), out refusal, out offender), offender);
		}

		[Test]
		public void IdentityContractPublishesOnlyKeysAndAffinity()
		{
			System.Reflection.MethodInfo[] methods = typeof(IKingdomIdentitySource).GetMethods();
			ClassicAssert.AreEqual(2, methods.Length);
			CollectionAssert.AreEquivalent(new string[2] { "Keys", "Affinity" },
				new string[2] { methods[0].Name, methods[1].Name });
		}
	}
}
#endif
