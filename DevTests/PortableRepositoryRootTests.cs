#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class PortableRepositoryRootTests
	{
		private static string IndependentCheckoutRoot()
		{
			string[] starts = new string[2] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
			for (int i = 0; i < starts.Length; i++)
			{
				for (DirectoryInfo cursor = new DirectoryInfo(starts[i]); cursor != null;
					cursor = cursor.Parent)
				{
					if (Directory.Exists(Path.Combine(cursor.FullName, "Core"))
						&& Directory.Exists(Path.Combine(cursor.FullName, "DevTests"))
						&& Directory.Exists(Path.Combine(cursor.FullName, "Tools"))
						&& File.Exists(Path.Combine(cursor.FullName, "manifest.json")))
						return cursor.FullName;
				}
			}
			throw new InvalidOperationException("Neither test working directory nor output is below a TAF checkout.");
		}

		private static void WithRepositoryRoot(string value, Action action)
		{
			string previous = Environment.GetEnvironmentVariable("TAF_REPO_ROOT");
			try
			{
				Environment.SetEnvironmentVariable("TAF_REPO_ROOT", value);
				action();
			}
			finally
			{
				Environment.SetEnvironmentVariable("TAF_REPO_ROOT", previous);
			}
		}

		[Test]
		public void RepositoryRoot_FallsBackFromPortableOutput()
		{
			WithRepositoryRoot(null, () =>
			{
				Assert.AreEqual(IndependentCheckoutRoot(), TestMain.RepositoryRoot);
			});
		}

		[Test]
		public void RepositoryRoot_PrefersValidatedEnvironmentRoot()
		{
			string expected = IndependentCheckoutRoot();
			WithRepositoryRoot(expected, () =>
			{
				Assert.AreEqual(expected, TestMain.RepositoryRoot);
			});
		}

		[Test]
		public void RepositoryRoot_RejectsInvalidEnvironmentRootClearly()
		{
			string invalid = Path.Combine(Path.GetTempPath(), "taf-not-a-checkout-" + Guid.NewGuid());
			WithRepositoryRoot(invalid, () =>
			{
				InvalidOperationException error = Assert.Throws<InvalidOperationException>(
					() => { string ignored = TestMain.RepositoryRoot; });
				StringAssert.Contains("TAF_REPO_ROOT is not a TAF checkout", error.Message);
			});
		}

		[Test]
		public void ReadRepositoryText_ReadsInsideCheckoutAndRejectsEscape()
		{
			string root = IndependentCheckoutRoot();
			WithRepositoryRoot(root, () =>
			{
				StringAssert.Contains("\"id\"", TestMain.ReadRepositoryText("manifest.json"));
				InvalidOperationException error = Assert.Throws<InvalidOperationException>(
					() => TestMain.ReadRepositoryText("../manifest.json"));
				StringAssert.Contains("escapes checkout", error.Message);
			});
		}
	}
}
#endif
