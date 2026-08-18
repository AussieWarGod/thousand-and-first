#if TAF_TESTS
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public static class TestMain
	{
		public static int Main()
		{
			int passed = 0;
			int failed = 0;
			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Name.EndsWith("Tests")))
			{
				object instance = Activator.CreateInstance(type);
				foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
				{
					foreach (TestCaseAttribute testCase in method.GetCustomAttributes<TestCaseAttribute>())
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
