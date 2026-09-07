#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class TestMainDiscoveryRulesTests
	{
		[Test]
		public void NamedCaseSourceIsRejectedWithoutInvokingItsProvider()
		{
			Attribute metadata = new TestCaseSourceAttribute(typeof(PoisonSource), "Cases");
			ClassicAssert.AreEqual("TestCaseSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(metadata));
		}

		[Test]
		public void NamedFixtureSourceIsRejectedWithoutInvokingItsProvider()
		{
			Attribute metadata = new TestFixtureSourceAttribute(typeof(PoisonSource), "Cases");
			ClassicAssert.AreEqual("TestFixtureSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(metadata));
		}

		[Test]
		public void TypeOnlySourcesAreRejectedWithoutConstructingOrEnumeratingTheirProvider()
		{
			ClassicAssert.AreEqual("TestCaseSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(
				new TestCaseSourceAttribute(typeof(PoisonSource))));
			ClassicAssert.AreEqual("TestFixtureSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(
				new TestFixtureSourceAttribute(typeof(PoisonSource))));
		}

		[Test]
		public void ExplicitTestCaseAndFixtureMetadataKeepTheirExistingMeaning()
		{
			foreach (Attribute metadata in new Attribute[] { new TestAttribute(),
				new TestCaseAttribute(7), new TestFixtureAttribute() })
				ClassicAssert.IsNull(TestMain.UnsupportedDiscoveryAttribute(metadata));
		}

		[Test]
		public void UnrelatedMetadataIsNotMistakenForADynamicSource()
		{
			foreach (Attribute metadata in new Attribute[] { new CategoryAttribute("source"),
				new DescriptionAttribute("TestCaseSourceAttribute"), new ObsoleteAttribute() })
				ClassicAssert.IsNull(TestMain.UnsupportedDiscoveryAttribute(metadata));
		}

		[Test]
		public void AbsentMetadataIsNotAnUnsupportedAttribute()
		{
			ClassicAssert.IsNull(TestMain.UnsupportedDiscoveryAttribute(null));
		}

		[Test]
		public void SourceOnlyMetadataIsRejectedAndNotRewritten()
		{
			TestCaseSourceAttribute cases = new TestCaseSourceAttribute("Cases");
			TestFixtureSourceAttribute fixtures = new TestFixtureSourceAttribute("Fixtures");
			ClassicAssert.AreEqual("TestCaseSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(cases));
			ClassicAssert.AreEqual("TestFixtureSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(fixtures));
			ClassicAssert.AreEqual("Cases", cases.SourceName);
			ClassicAssert.AreEqual("Fixtures", fixtures.SourceName);
			ClassicAssert.IsNull(cases.SourceType);
			ClassicAssert.IsNull(fixtures.SourceType);
		}

		[TestCase(false, null)]
		[TestCase(false, "unmatched-filter")]
		[TestCase(true, null)]
		[TestCase(true, "unmatched-filter")]
		public void ActualRunnerRejectsSourceOnlyFixturesBeforeEligibilityAndFiltering(bool FixtureSource, string Filter)
		{
			Type type = EmitSourceFixture(FixtureSource);
			TestMainDiscoveryProbe.Reset();
			string output;
			ClassicAssert.AreEqual(2, CaptureRun(new[] { type }, Filter, out output));
			ClassicAssert.AreEqual(Refusal(type, FixtureSource), output);
			AssertNoProbeExecution();
		}

		[Test]
		public void ActualRunnerReportsBothSourceFamiliesDespiteAnUnmatchedFilter()
		{
			Type cases = EmitSourceFixture(false), fixtures = EmitSourceFixture(true);
			TestMainDiscoveryProbe.Reset();
			string output;
			ClassicAssert.AreEqual(2, CaptureRun(new[] { fixtures, cases }, "unmatched-filter", out output));
			ClassicAssert.AreEqual(Refusal(cases, false) + Refusal(fixtures, true), output);
			AssertNoProbeExecution();
		}

		[TestCase(true)]
		[TestCase(false)]
		public void ActualRunnerPreservesMatchedAndUnmatchedExplicitTestBehavior(bool Match)
		{
			Type type = EmitControlFixture();
			TestMainDiscoveryProbe.Reset();
			string output;
			string filter = Match ? type.Name : "unmatched-filter";
			ClassicAssert.AreEqual(Match ? 0 : 2, CaptureRun(new[] { type }, filter, out output));
			ClassicAssert.AreEqual(Environment.NewLine + (Match
				? "ALL GREEN: 2 cases passed, 0 skipped (2 discovered)"
				: "NO TESTS MATCHED TAF_TEST_FILTER=" + filter) + Environment.NewLine, output);
			ClassicAssert.AreEqual(Match ? 1 : 0, TestMainDiscoveryProbe.PlainCalls);
			ClassicAssert.AreEqual(Match ? 1 : 0, TestMainDiscoveryProbe.CaseCalls);
			AssertNoProbeExecution();
		}

		private static int CaptureRun(Type[] Types, string Filter, out string Output)
		{
			TextWriter previous = Console.Out;
			StringWriter captured = new StringWriter();
			try
			{
				Console.SetOut(captured);
				int result = TestMain.Run(Types, Filter, true, null);
				Output = captured.ToString();
				return result;
			}
			finally
			{
				Console.SetOut(previous);
				captured.Dispose();
			}
		}

		private static string Refusal(Type Type, bool FixtureSource)
		{
			return "UNSUPPORTED TEST DISCOVERY " + Type.FullName
				+ (FixtureSource ? ": [TestFixtureSourceAttribute]" : ".Probe: [TestCaseSourceAttribute]")
				+ "; this runner does not execute dynamic NUnit sources" + Environment.NewLine;
		}

		private static void AssertNoProbeExecution()
		{
			ClassicAssert.AreEqual(0, TestMainDiscoveryProbe.SourceReads);
			ClassicAssert.AreEqual(0, TestMainDiscoveryProbe.FixtureConstructions);
			ClassicAssert.AreEqual(0, TestMainDiscoveryProbe.PoisonCalls);
		}

		private static TypeBuilder NewFixture(string Name)
		{
			AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
				new AssemblyName("TAFDiscoveryRegression_" + Guid.NewGuid().ToString("N")),
				AssemblyBuilderAccess.RunAndCollect);
			return assembly.DefineDynamicModule("Fixtures").DefineType(
				"ThousandAndFirst.Tests.Emitted." + Name, TypeAttributes.Public | TypeAttributes.Sealed);
		}

		private static Type EmitSourceFixture(bool FixtureSource)
		{
			TypeBuilder type = NewFixture(FixtureSource ? "FixtureSourceOnly" : "CaseSourceOnly");
			ConstructorBuilder constructor = type.DefineConstructor(MethodAttributes.Public,
				CallingConventions.Standard, Type.EmptyTypes);
			ILGenerator construction = constructor.GetILGenerator();
			construction.Emit(OpCodes.Ldarg_0);
			construction.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
			construction.Emit(OpCodes.Call, typeof(TestMainDiscoveryProbe).GetMethod("ConstructPoison"));
			construction.Emit(OpCodes.Ret);
			MethodBuilder method = type.DefineMethod("Probe", MethodAttributes.Public, typeof(void),
				FixtureSource ? Type.EmptyTypes : new[] { typeof(int) });
			method.GetILGenerator().Emit(OpCodes.Call, typeof(TestMainDiscoveryProbe).GetMethod("InvokePoison"));
			method.GetILGenerator().Emit(OpCodes.Ret);
			Type family = FixtureSource ? typeof(TestFixtureSourceAttribute) : typeof(TestCaseSourceAttribute);
			CustomAttributeBuilder source = new CustomAttributeBuilder(
				family.GetConstructor(new[] { typeof(Type), typeof(string) }),
				new object[] { typeof(TestMainDiscoveryProbe), "Cases" });
			if (FixtureSource) type.SetCustomAttribute(source);
			else method.SetCustomAttribute(source);
			return type.CreateTypeInfo().AsType();
		}

		private static Type EmitControlFixture()
		{
			TypeBuilder type = NewFixture("ExplicitControl");
			MethodBuilder plain = type.DefineMethod("Plain", MethodAttributes.Public | MethodAttributes.Static,
				typeof(void), Type.EmptyTypes);
			plain.SetCustomAttribute(new CustomAttributeBuilder(typeof(TestAttribute).GetConstructor(Type.EmptyTypes),
				new object[0]));
			plain.GetILGenerator().Emit(OpCodes.Call, typeof(TestMainDiscoveryProbe).GetMethod("RunPlain"));
			plain.GetILGenerator().Emit(OpCodes.Ret);
			MethodBuilder row = type.DefineMethod("WithCase", MethodAttributes.Public | MethodAttributes.Static,
				typeof(void), new[] { typeof(int) });
			row.SetCustomAttribute(new CustomAttributeBuilder(
				typeof(TestCaseAttribute).GetConstructor(new[] { typeof(object[]) }),
				new object[] { new object[] { 7 } }));
			row.GetILGenerator().Emit(OpCodes.Ldarg_0);
			row.GetILGenerator().Emit(OpCodes.Call, typeof(TestMainDiscoveryProbe).GetMethod("RunCase"));
			row.GetILGenerator().Emit(OpCodes.Ret);
			return type.CreateTypeInfo().AsType();
		}

		private sealed class PoisonSource
		{
			public PoisonSource() { throw new InvalidOperationException("source constructor executed"); }
			public static object[] Cases
			{
				get { throw new InvalidOperationException("source getter executed"); }
			}
		}
	}

	public static class TestMainDiscoveryProbe
	{
		public static int SourceReads, FixtureConstructions, PoisonCalls, PlainCalls, CaseCalls;
		public static object[] Cases
		{
			get { SourceReads++; throw new InvalidOperationException("source getter executed"); }
		}
		public static void ConstructPoison()
		{
			FixtureConstructions++;
			throw new InvalidOperationException("source fixture constructed");
		}
		public static void InvokePoison()
		{
			PoisonCalls++;
			throw new InvalidOperationException("source method executed");
		}
		public static void RunPlain() { PlainCalls++; }
		public static void RunCase(int Value)
		{
			if (Value != 7) throw new InvalidOperationException("explicit case argument changed");
			CaseCalls++;
		}
		public static void Reset()
		{
			SourceReads = FixtureConstructions = PoisonCalls = PlainCalls = CaseCalls = 0;
		}
	}
}
#endif
