#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class TestMainDiscoveryRulesTests
	{
		[Test]
		public void NamedCaseSourceIsRejectedWithoutInvokingItsProvider()
		{
			Attribute metadata = new TestCaseSourceAttribute(typeof(PoisonSource), "Cases");
			Assert.AreEqual("TestCaseSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(metadata));
		}

		[Test]
		public void NamedFixtureSourceIsRejectedWithoutInvokingItsProvider()
		{
			Attribute metadata = new TestFixtureSourceAttribute(typeof(PoisonSource), "Cases");
			Assert.AreEqual("TestFixtureSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(metadata));
		}

		[Test]
		public void TypeOnlySourcesAreRejectedWithoutConstructingOrEnumeratingTheirProvider()
		{
			Assert.AreEqual("TestCaseSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(
				new TestCaseSourceAttribute(typeof(PoisonSource))));
			Assert.AreEqual("TestFixtureSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(
				new TestFixtureSourceAttribute(typeof(PoisonSource))));
		}

		[Test]
		public void ExplicitTestCaseAndFixtureMetadataKeepTheirExistingMeaning()
		{
			foreach (Attribute metadata in new Attribute[] { new TestAttribute(),
				new TestCaseAttribute(7), new TestFixtureAttribute() })
				Assert.IsNull(TestMain.UnsupportedDiscoveryAttribute(metadata));
		}

		[Test]
		public void UnrelatedMetadataIsNotMistakenForADynamicSource()
		{
			foreach (Attribute metadata in new Attribute[] { new CategoryAttribute("source"),
				new DescriptionAttribute("TestCaseSourceAttribute"), new ObsoleteAttribute() })
				Assert.IsNull(TestMain.UnsupportedDiscoveryAttribute(metadata));
		}

		[Test]
		public void AbsentMetadataIsNotAnUnsupportedAttribute()
		{
			Assert.IsNull(TestMain.UnsupportedDiscoveryAttribute(null));
		}

		[Test]
		public void SourceOnlyMetadataIsRejectedAndNotRewritten()
		{
			TestCaseSourceAttribute cases = new TestCaseSourceAttribute("Cases");
			TestFixtureSourceAttribute fixtures = new TestFixtureSourceAttribute("Fixtures");
			Assert.AreEqual("TestCaseSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(cases));
			Assert.AreEqual("TestFixtureSourceAttribute", TestMain.UnsupportedDiscoveryAttribute(fixtures));
			Assert.AreEqual("Cases", cases.SourceName);
			Assert.AreEqual("Fixtures", fixtures.SourceName);
			Assert.IsNull(cases.SourceType);
			Assert.IsNull(fixtures.SourceType);
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
			Assert.AreEqual(2, CaptureRun(new[] { type }, Filter, out output));
			Assert.AreEqual(Refusal(type, FixtureSource), output);
			AssertNoProbeExecution();
		}

		[Test]
		public void ActualRunnerReportsBothSourceFamiliesDespiteAnUnmatchedFilter()
		{
			Type cases = EmitSourceFixture(false), fixtures = EmitSourceFixture(true);
			TestMainDiscoveryProbe.Reset();
			string output;
			Assert.AreEqual(2, CaptureRun(new[] { fixtures, cases }, "unmatched-filter", out output));
			Assert.AreEqual(Refusal(cases, false) + Refusal(fixtures, true), output);
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
			Assert.AreEqual(Match ? 0 : 2, CaptureRun(new[] { type }, filter, out output));
			Assert.AreEqual(Environment.NewLine + (Match
				? "ALL GREEN: 2 cases passed, 0 skipped (2 discovered)"
				: "NO TESTS MATCHED TAF_TEST_FILTER=" + filter) + Environment.NewLine, output);
			Assert.AreEqual(Match ? 1 : 0, TestMainDiscoveryProbe.PlainCalls);
			Assert.AreEqual(Match ? 1 : 0, TestMainDiscoveryProbe.CaseCalls);
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
			Assert.AreEqual(0, TestMainDiscoveryProbe.SourceReads);
			Assert.AreEqual(0, TestMainDiscoveryProbe.FixtureConstructions);
			Assert.AreEqual(0, TestMainDiscoveryProbe.PoisonCalls);
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
