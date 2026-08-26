#if TAF_TESTS
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

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

		public static int Main()
		{
			int passed = 0;
			int failed = 0;
			int discovered = 0;
			int selected = 0;
			string filter = Environment.GetEnvironmentVariable("TAF_TEST_FILTER");
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
							object result = method.Invoke(method.IsStatic ? null
								: (instance ?? (instance = Activator.CreateInstance(type, true))), null);
							Task task = result as Task;
							if (task != null) task.GetAwaiter().GetResult();
							passed++;
						}
						catch (Exception ex)
						{
							failed++;
							Exception failure = ex.InnerException ?? ex;
							Console.WriteLine("FAIL " + label
								+ "\n     " + failure.Message + "\n     " + failure.StackTrace);
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
							object result = method.Invoke(method.IsStatic ? null
								: (instance ?? (instance = Activator.CreateInstance(type, true))),
								testCase.Arguments);
							Task task = result as Task;
							if (task != null) task.GetAwaiter().GetResult();
							passed++;
						}
						catch (Exception ex)
						{
							failed++;
							Exception failure = ex.InnerException ?? ex;
							Console.WriteLine("FAIL " + label + "\n     " + failure.Message
								+ "\n     " + failure.StackTrace);
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
			Console.WriteLine(failed == 0
				? $"ALL GREEN: {passed} cases passed ({discovered} discovered)"
				: $"{passed} passed, {failed} FAILED ({selected} selected; {discovered} discovered)");
			return failed == 0 ? 0 : 1;
		}
	}
}
#endif
