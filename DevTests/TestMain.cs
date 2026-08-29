#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace ThousandAndFirst.Tests
{
	public static class TestMain
	{
		public static string RepositoryRoot
		{
			get
			{
				string supplied = Environment.GetEnvironmentVariable("TAF_REPO_ROOT");
				if (!string.IsNullOrWhiteSpace(supplied))
				{
					string candidate = Path.GetFullPath(supplied);
					if (IsRepositoryRoot(candidate)) return candidate;
					throw new InvalidOperationException("TAF_REPO_ROOT is not a TAF checkout: "
						+ candidate + " (expected Core, DevTests, Tools, and manifest.json)");
				}

				string found = FindRepositoryRoot(AppContext.BaseDirectory)
					?? FindRepositoryRoot(Directory.GetCurrentDirectory());
				if (found != null) return found;
				throw new InvalidOperationException("Cannot locate TAF checkout. Set TAF_REPO_ROOT to its root.");
			}
		}

		public static string ReadRepositoryText(string relative)
		{
			if (string.IsNullOrWhiteSpace(relative))
				throw new ArgumentException("Repository-relative path is required.", "relative");
			string path = Path.GetFullPath(Path.Combine(RepositoryRoot,
				relative.Replace('/', Path.DirectorySeparatorChar)));
			if (!path.StartsWith(RepositoryRoot + Path.DirectorySeparatorChar,
				StringComparison.Ordinal) && !string.Equals(path, RepositoryRoot, StringComparison.Ordinal))
				throw new InvalidOperationException("Repository-relative path escapes checkout: " + relative);
			if (!File.Exists(path) && relative.IndexOf('/') < 0 &&
				relative.IndexOf(Path.DirectorySeparatorChar) < 0)
			{
				path = Path.Combine(RepositoryRoot, "RuntimeData", relative);
			}
			if (!File.Exists(path))
				throw new InvalidOperationException("Cannot locate repository source: " + relative);
			return File.ReadAllText(path);
		}

		private static string FindRepositoryRoot(string start)
		{
			if (string.IsNullOrWhiteSpace(start)) return null;
			for (DirectoryInfo cursor = new DirectoryInfo(Path.GetFullPath(start)); cursor != null;
				cursor = cursor.Parent)
			{
				if (IsRepositoryRoot(cursor.FullName)) return cursor.FullName;
			}
			return null;
		}

		private static bool IsRepositoryRoot(string path)
		{
			return Directory.Exists(Path.Combine(path, "Core"))
				&& Directory.Exists(Path.Combine(path, "DevTests"))
				&& Directory.Exists(Path.Combine(path, "Tools"))
				&& File.Exists(Path.Combine(path, "manifest.json"));
		}

		private static void InvokeIsolated(MethodInfo method, object instance, object[] arguments)
		{
			// This runner invokes tests directly instead of through NUnit's engine. Give each
			// case its own result context so Assert.Multiple failures cannot poison later cases.
			using (new TestExecutionContext.IsolatedContext())
			{
				object result = method.Invoke(method.IsStatic ? null : instance, arguments);
				Task task = result as Task;
				if (task != null) task.GetAwaiter().GetResult();
			}
		}

		public static int Main()
		{
			int passed = 0;
			int failed = 0;
			int skipped = 0;
			int discovered = 0;
			int selected = 0;
			string filter = Environment.GetEnvironmentVariable("TAF_TEST_FILTER");
			bool forbidSkips = string.Equals(Environment.GetEnvironmentVariable("TAF_FORBID_SKIPS"),
				"1", StringComparison.Ordinal);
			string allowedSkipText = Environment.GetEnvironmentVariable("TAF_ALLOWED_SKIPS");
			HashSet<string> allowedSkips = new HashSet<string>(StringComparer.Ordinal);
			if (!string.IsNullOrWhiteSpace(allowedSkipText))
			{
				foreach (string raw in allowedSkipText.Split(';'))
				{
					string label = raw.Trim();
					if (label.Length == 0 || !allowedSkips.Add(label))
					{
						Console.WriteLine("INVALID TAF_ALLOWED_SKIPS: empty or duplicate label");
						return 2;
					}
				}
			}
			if (forbidSkips && allowedSkips.Count > 0)
			{
				Console.WriteLine("INVALID SKIP POLICY: TAF_FORBID_SKIPS conflicts with TAF_ALLOWED_SKIPS");
				return 2;
			}
			HashSet<string> observedSkips = new HashSet<string>(StringComparer.Ordinal);
			const BindingFlags testFlags = BindingFlags.Public | BindingFlags.NonPublic
				| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes()
				.Where(t => t.GetMethods(testFlags).Any(m =>
					m.GetCustomAttribute<TestAttribute>() != null
					|| m.GetCustomAttributes<TestCaseAttribute>().Any()))
				.OrderBy(t => t.FullName, StringComparer.Ordinal))
			{
				object instance = null;
				foreach (MethodInfo method in type.GetMethods(testFlags)
					.OrderBy(m => m.Name, StringComparer.Ordinal)
					.ThenBy(m => m.ToString(), StringComparer.Ordinal))
				{
					TestAttribute test = method.GetCustomAttribute<TestAttribute>();
					TestCaseAttribute[] testCases = method.GetCustomAttributes<TestCaseAttribute>()
						.OrderBy(t => string.Join("\u001f", t.Arguments.Select(a => a?.ToString() ?? "null")), StringComparer.Ordinal)
						.ToArray();
					if (test != null && method.GetParameters().Length == 0)
					{
						discovered++;
						string label = type.Name + "." + method.Name;
						if (!string.IsNullOrEmpty(filter)
							&& label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
						selected++;
						try
						{
							InvokeIsolated(method, method.IsStatic ? null
								: (instance ?? (instance = Activator.CreateInstance(type, true))), null);
							passed++;
						}
						catch (Exception ex)
						{
							Exception failure = ex.InnerException ?? ex;
							if (failure is IgnoreException)
							{
								if (!forbidSkips && allowedSkips.Contains(label))
								{
									skipped++;
									observedSkips.Add(label);
									Console.WriteLine("SKIP " + label + "\n     " + failure.Message);
								}
								else
								{
									failed++;
									Console.WriteLine("FAIL " + label
										+ "\n     unauthorized skip: " + failure.Message);
								}
							}
							else
							{
								failed++;
								Console.WriteLine("FAIL " + label
									+ "\n     " + failure.Message + "\n     " + failure.StackTrace);
							}
						}
					}
					else if (test != null && testCases.Length == 0)
					{
						discovered++;
						string label = type.Name + "." + method.Name;
						if (string.IsNullOrEmpty(filter)
							|| label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							selected++;
							failed++;
							Console.WriteLine("FAIL " + label
								+ "\n     [Test] methods with parameters require explicit [TestCase] rows");
						}
					}
					foreach (TestCaseAttribute testCase in testCases)
					{
						discovered++;
						string label = type.Name + "." + method.Name + "(" + string.Join(", ", testCase.Arguments.Select(a => a?.ToString() ?? "null")) + ")";
						if (!string.IsNullOrEmpty(filter)
							&& label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
						selected++;
						try
						{
							InvokeIsolated(method, method.IsStatic ? null
								: (instance ?? (instance = Activator.CreateInstance(type, true))),
								testCase.Arguments);
							passed++;
						}
						catch (Exception ex)
						{
							Exception failure = ex.InnerException ?? ex;
							if (failure is IgnoreException)
							{
								if (!forbidSkips && allowedSkips.Contains(label))
								{
									skipped++;
									observedSkips.Add(label);
									Console.WriteLine("SKIP " + label + "\n     " + failure.Message);
								}
								else
								{
									failed++;
									Console.WriteLine("FAIL " + label
										+ "\n     unauthorized skip: " + failure.Message);
								}
							}
							else
							{
								failed++;
								Console.WriteLine("FAIL " + label + "\n     " + failure.Message
									+ "\n     " + failure.StackTrace);
							}
						}
					}
				}
			}
			if (selected == 0)
			{
				Console.WriteLine();
				Console.WriteLine(string.IsNullOrEmpty(filter)
					? "NO TESTS DISCOVERED"
					: "NO TESTS MATCHED TAF_TEST_FILTER=" + filter);
				return 2;
			}
			Console.WriteLine();
			string[] missingSkips = allowedSkips.Except(observedSkips)
				.OrderBy(label => label, StringComparer.Ordinal).ToArray();
			if (missingSkips.Length > 0)
			{
				foreach (string label in missingSkips)
				{
					failed++;
					Console.WriteLine("FAIL expected skip did not occur: " + label);
				}
			}
			Console.WriteLine(failed == 0
				? $"ALL GREEN: {passed} cases passed, {skipped} skipped ({discovered} discovered)"
				: $"{passed} passed, {failed} FAILED ({selected} selected; {discovered} discovered)");
			return failed == 0 ? 0 : 1;
		}
	}
}
#endif
