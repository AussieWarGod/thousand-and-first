#if TAF_TESTS
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes()
				.Where(t => t.Name.EndsWith("Tests"))
				.OrderBy(t => t.FullName, StringComparer.Ordinal))
			{
				object instance = Activator.CreateInstance(type);
				foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.OrderBy(m => m.Name, StringComparer.Ordinal)
					.ThenBy(m => m.ToString(), StringComparer.Ordinal))
				{
					if (method.GetCustomAttribute<TestAttribute>() != null && method.GetParameters().Length == 0)
					{
						try
						{
							method.Invoke(instance, null);
							passed++;
						}
						catch (Exception ex)
						{
							failed++;
							Console.WriteLine("FAIL " + type.Name + "." + method.Name + "\n     " + (ex.InnerException ?? ex).Message);
						}
					}
					foreach (TestCaseAttribute testCase in method.GetCustomAttributes<TestCaseAttribute>()
						.OrderBy(t => string.Join("\u001f", t.Arguments.Select(a => a?.ToString() ?? "null")), StringComparer.Ordinal))
					{
						string label = type.Name + "." + method.Name + "(" + string.Join(", ", testCase.Arguments.Select(a => a?.ToString() ?? "null")) + ")";
						try
						{
							method.Invoke(instance, testCase.Arguments);
							passed++;
						}
						catch (Exception ex)
						{
							failed++;
							Console.WriteLine("FAIL " + label + "\n     " + (ex.InnerException ?? ex).Message);
						}
					}
				}
			}
			Console.WriteLine();
			Console.WriteLine(failed == 0 ? $"ALL GREEN: {passed} cases passed" : $"{passed} passed, {failed} FAILED");
			return failed == 0 ? 0 : 1;
		}
	}
}
#endif
