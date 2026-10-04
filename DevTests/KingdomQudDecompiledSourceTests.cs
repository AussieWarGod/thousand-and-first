#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The decompile-backed "InstalledQud" checks answer only for the pinned engine: a decompile
	/// of a replaced engine is refused whether it is supplied or found by default, the only
	/// default is the version-keyed archive, and a named install must be the pinned core too.
	/// </summary>
	[TestFixture]
	public class KingdomQudDecompiledSourceTests
	{
		private static readonly string[] Locators =
			{ "KingdomFounderHistoryRulesTests", "KingdomShopStockSourceTests" };
		private static readonly string Relic = Path.Combine("XRL", "World", "RelicGenerator.cs");

		[TestCase("2.0.211.51")]
		[TestCase("2.0.210.24")]
		public void SuppliedDecompileOfAReplacedEngineIsRefused(string replaced)
		{
			string pinned = KingdomQudDecompiledSource.PinnedCoreBuild();
			ClassicAssert.AreNotEqual(pinned, replaced, "fixture must name a replaced engine");
			string root = Tree(Declaring(replaced));
			try
			{
				foreach (string locator in Locators)
				{
					string message = Refusal(locator, root, false, null);
					StringAssert.Contains("declares AssemblyVersion " + replaced, message);
					StringAssert.Contains("not the pinned core " + pinned, message);
				}
			}
			finally { Directory.Delete(root, true); }
		}

		[TestCase("absent")]
		[TestCase("unversioned")]
		[TestCase("twice")]
		public void SuppliedDecompileWithoutOneExactVersionIsRefused(string shape)
		{
			string pinned = KingdomQudDecompiledSource.PinnedCoreBuild();
			string info = shape == "absent" ? null : shape == "unversioned"
				? "[assembly: CLSCompliant(false)]\n"
				: Declaring(pinned) + Declaring(pinned);
			string root = Tree(info);
			try
			{
				foreach (string locator in Locators)
					StringAssert.Contains(shape == "absent" ? "lacks Properties/AssemblyInfo.cs"
						: "exactly one AssemblyVersion", Refusal(locator, root, false, null));
			}
			finally { Directory.Delete(root, true); }
		}

		[Test]
		public void PinnedDecompileAnswersWhenNoInstallIsNamed()
		{
			string root = Tree(Declaring(KingdomQudDecompiledSource.PinnedCoreBuild()));
			try
			{
				foreach (string locator in Locators)
					WithEnvironment(root, true, null, () => ClassicAssert.AreEqual(root,
						Method(locator).Invoke(null, null)));
			}
			finally { Directory.Delete(root, true); }
		}

		[Test]
		public void NamedInstallOfAnotherEngineIsRefused()
		{
			string pinned = KingdomQudDecompiledSource.PinnedCoreBuild();
			string root = Tree(Declaring(pinned));
			string install = Path.Combine(Path.GetTempPath(), "taf-qud-install-"
				+ Guid.NewGuid().ToString("N"));
			string qudBase = Path.Combine(install, "CoQ_Data", "StreamingAssets", "Base");
			try
			{
				Directory.CreateDirectory(qudBase);
				foreach (string locator in Locators)
					StringAssert.Contains("does not resolve Assembly-CSharp.dll",
						Refusal(locator, root, true, qudBase));
				Directory.CreateDirectory(Path.Combine(install, "CoQ_Data", "Managed"));
				Assembly foreign = typeof(KingdomQudDecompiledSourceTests).Assembly;
				string version = foreign.GetName().Version.ToString();
				ClassicAssert.AreNotEqual(pinned, version, "fixture engine must not be the pin");
				File.Copy(foreign.Location, Path.Combine(install, "CoQ_Data", "Managed",
					"Assembly-CSharp.dll"));
				foreach (string locator in Locators)
					StringAssert.Contains("Installed Qud core " + version + " is not the pinned core "
						+ pinned, Refusal(locator, root, true, qudBase));
			}
			finally { Directory.Delete(root, true); Directory.Delete(install, true); }
		}

		[Test]
		public void DefaultDecompileIsOnlyTheVersionKeyedArchive()
		{
			string pinned = KingdomQudDecompiledSource.PinnedCoreBuild();
			string home = Path.Combine(Path.GetTempPath(), "taf-qud-home-" + Guid.NewGuid().ToString("N"));
			string archive = Path.Combine(home, "coq", "qud_helper", "game_base", "decompiled");
			try
			{
				ClassicAssert.IsNull(KingdomQudDecompiledSource.Select(null, " ", pinned, Relic));
				ClassicAssert.IsNull(KingdomQudDecompiledSource.Select(null, home, pinned, Relic));
				Tree(Declaring("2.0.211.51"), Path.Combine(archive, "2.0.211.51-ilspy9.1"));
				Tree(Declaring("2.0.210.24"), Path.Combine(archive, "6000.0.41.4645959"));
				ClassicAssert.IsNull(KingdomQudDecompiledSource.Select(null, home, pinned, Relic),
					"a replaced engine's decompile must never become the default");
				string keyed = Path.Combine(archive, pinned + "-ilspy9.1");
				ClassicAssert.AreEqual(keyed, KingdomQudDecompiledSource.DefaultRoot(home, pinned));
				Tree(Declaring(pinned), keyed);
				ClassicAssert.AreEqual(keyed, KingdomQudDecompiledSource.Select(null, home, pinned,
					Relic));
				File.WriteAllText(Path.Combine(keyed, "Properties", "AssemblyInfo.cs"),
					Declaring("2.0.211.51"));
				InvalidOperationException mislabeled = Assert.Throws<InvalidOperationException>(() =>
					KingdomQudDecompiledSource.Select(null, home, pinned, Relic));
				StringAssert.Contains("declares AssemblyVersion 2.0.211.51", mislabeled.Message);
			}
			finally { if (Directory.Exists(home)) Directory.Delete(home, true); }
		}

		private static string Declaring(string version)
		{
			return "[assembly: AssemblyVersion(\"" + version + "\")]\n";
		}

		private static string Tree(string assemblyInfo, string root = null)
		{
			root = root ?? Path.Combine(Path.GetTempPath(), "taf-qud-decompile-"
				+ Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(root, "XRL", "World"));
			File.WriteAllText(Path.Combine(root, Relic), "owned decompile fixture");
			if (assemblyInfo == null) return root;
			Directory.CreateDirectory(Path.Combine(root, "Properties"));
			File.WriteAllText(Path.Combine(root, "Properties", "AssemblyInfo.cs"), assemblyInfo);
			return root;
		}

		private static string Refusal(string locator, string decompiled, bool replaceBase,
			string qudBase)
		{
			string message = null;
			WithEnvironment(decompiled, replaceBase, qudBase, () =>
			{
				TargetInvocationException failure = Assert.Throws<TargetInvocationException>(
					() => Method(locator).Invoke(null, null));
				ClassicAssert.IsInstanceOf<InvalidOperationException>(failure.InnerException);
				message = failure.InnerException.Message;
			});
			return message;
		}

		private static MethodInfo Method(string locator)
		{
			Type type = Array.Find(typeof(KingdomQudDecompiledSourceTests).Assembly.GetTypes(),
				candidate => candidate.Name == locator);
			ClassicAssert.IsNotNull(type, locator);
			MethodInfo method = type.GetMethod("LocateDecompiledQud",
				BindingFlags.Static | BindingFlags.NonPublic);
			ClassicAssert.IsNotNull(method, locator);
			return method;
		}

		private static void WithEnvironment(string decompiled, bool replaceBase, string qudBase,
			Action action)
		{
			string previousDecompiled = Environment.GetEnvironmentVariable(
				KingdomQudDecompiledSource.Variable);
			string previousBase = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			try
			{
				Environment.SetEnvironmentVariable(KingdomQudDecompiledSource.Variable, decompiled);
				if (replaceBase) Environment.SetEnvironmentVariable("TAF_QUD_BASE", qudBase);
				action();
			}
			finally
			{
				Environment.SetEnvironmentVariable(KingdomQudDecompiledSource.Variable,
					previousDecompiled);
				Environment.SetEnvironmentVariable("TAF_QUD_BASE", previousBase);
			}
		}
	}
}
#endif
