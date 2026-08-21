using System;
using System.Collections.Generic;
using System.Reflection;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What may cross the executor boundary, decided by walking the type closure rather than by
	/// reading the diff.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;2.5 states the contract and then states the only thing that
	/// makes a contract hold: <i>"Enforcement is a reflection test, not a review habit."</i> The
	/// <c>*Rules.cs</c> engine-free discipline already guarantees purity for every rules module the
	/// design names; the seam is where it becomes checkable instead of merely conventional, and
	/// this class is the checkable part. The test that calls it fails the build.
	/// </para>
	/// <para>
	/// The predicate lives in production rather than in the test because <c>kingdom:selftest</c>
	/// and any third-party registration path need the same answer, and two implementations of "is
	/// this pure" would be two answers.
	/// </para>
	/// </summary>
	internal static class KingdomComputeSeam
	{
		/// <summary>How deep the walker follows a closure before refusing. A boundary type that
		/// nests deeper than this has not been shown clean, and an unproven boundary is refused
		/// rather than passed.</summary>
		internal const int MaxClosureDepth = 6;

		/// <summary>How many distinct types the walker will visit before refusing.</summary>
		internal const int MaxClosureTypes = 256;

		private static readonly string[] EngineNamespacePrefixes = new string[9]
		{
			"XRL",
			"Qud",
			"UnityEngine",
			"Unity",
			"ConsoleLib",
			"Genkit",
			"HistoryKit",
			"Occult",
			"HarmonyLib"
		};

		private static readonly string[] EngineAssemblyNames = new string[5]
		{
			"Assembly-CSharp",
			"Assembly-CSharp-firstpass",
			"UnityEngine",
			"UnityEngine.CoreModule",
			"0Harmony"
		};

		/// <summary>The namespace prefix every walkable type of ours begins with. A type outside it
		/// is checked and then treated as a leaf: the framework's own internals are not ours to
		/// rule on, and walking into <c>System.String</c>'s private fields would fail every
		/// boundary for a reason that has nothing to do with this design.</summary>
		private const string OwnNamespacePrefix = "ThousandAndFirst";

		/// <summary>Whether a namespace belongs to the host game or its runtime.</summary>
		internal static bool IsEngineNamespace(string candidate)
		{
			if (string.IsNullOrEmpty(candidate))
			{
				return false;
			}
			for (int i = 0; i < EngineNamespacePrefixes.Length; i++)
			{
				string prefix = EngineNamespacePrefixes[i];
				if (candidate.Length < prefix.Length)
				{
					continue;
				}
				if (!string.Equals(candidate.Substring(0, prefix.Length), prefix, StringComparison.Ordinal))
				{
					continue;
				}
				if (candidate.Length == prefix.Length || candidate[prefix.Length] == '.')
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Whether an assembly simple name is one of the host game's.</summary>
		internal static bool IsEngineAssembly(string candidate)
		{
			if (string.IsNullOrEmpty(candidate))
			{
				return false;
			}
			for (int i = 0; i < EngineAssemblyNames.Length; i++)
			{
				if (string.Equals(candidate, EngineAssemblyNames[i], StringComparison.Ordinal))
				{
					return true;
				}
			}
			return candidate.StartsWith("UnityEngine.", StringComparison.Ordinal);
		}

		/// <summary>
		/// Walks both boundary types of one computation. The whole contract of
		/// LIVING-CITY-ARCHITECTURE &sect;2.5, in one call: no engine type crosses, nothing mutable
		/// crosses, and nothing static and mutable hides behind what crosses.
		/// </summary>
		internal static bool TryValidateBoundary(Type input, Type output, out KingdomComputeRefusal refusal, out string offender)
		{
			if (input == null || output == null)
			{
				refusal = KingdomComputeRefusal.NullJob;
				offender = "null";
				return false;
			}
			List<Type> visited = new List<Type>();
			if (!TryWalk(input, 0, visited, out refusal, out offender))
			{
				return false;
			}
			return TryWalk(output, 0, visited, out refusal, out offender);
		}

		/// <summary>One type and everything reachable from it.</summary>
		internal static bool TryValidateType(Type type, out KingdomComputeRefusal refusal, out string offender)
		{
			if (type == null)
			{
				refusal = KingdomComputeRefusal.NullJob;
				offender = "null";
				return false;
			}
			return TryWalk(type, 0, new List<Type>(), out refusal, out offender);
		}

		private static bool TryWalk(Type type, int depth, List<Type> visited, out KingdomComputeRefusal refusal, out string offender)
		{
			refusal = KingdomComputeRefusal.None;
			offender = null;
			if (type == null)
			{
				return true;
			}
			if (type.IsByRef || type.IsPointer)
			{
				type = type.GetElementType();
				if (type == null)
				{
					return true;
				}
			}
			if (type.IsArray)
			{
				return TryWalk(type.GetElementType(), depth + 1, visited, out refusal, out offender);
			}
			if (visited.Contains(type))
			{
				return true;
			}
			if (depth > MaxClosureDepth || visited.Count >= MaxClosureTypes)
			{
				refusal = KingdomComputeRefusal.ClosureTooLarge;
				offender = type.FullName;
				return false;
			}
			visited.Add(type);

			if (IsEngineNamespace(type.Namespace) || IsEngineAssembly(AssemblyNameOf(type)))
			{
				refusal = KingdomComputeRefusal.EngineTypeAtBoundary;
				offender = type.FullName;
				return false;
			}
			if (type.IsGenericType)
			{
				Type[] arguments = type.GetGenericArguments();
				for (int i = 0; i < arguments.Length; i++)
				{
					if (!TryWalk(arguments[i], depth + 1, visited, out refusal, out offender))
					{
						return false;
					}
				}
			}
			if (!IsOurs(type))
			{
				// A framework type is a leaf: it is checked against the engine ban and then left
				// alone. Its internals are not ours to rule on, and mscorlib's own private mutable
				// fields would otherwise condemn every boundary in the design.
				return true;
			}
			if (type.IsEnum)
			{
				return true;
			}

			FieldInfo[] instanceFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < instanceFields.Length; i++)
			{
				FieldInfo field = instanceFields[i];
				if (!field.IsInitOnly)
				{
					refusal = KingdomComputeRefusal.MutableField;
					offender = type.FullName + "." + field.Name;
					return false;
				}
				if (!TryWalk(field.FieldType, depth + 1, visited, out refusal, out offender))
				{
					return false;
				}
			}
			FieldInfo[] staticFields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < staticFields.Length; i++)
			{
				FieldInfo field = staticFields[i];
				if (field.IsLiteral || field.IsInitOnly)
				{
					continue;
				}
				refusal = KingdomComputeRefusal.MutableStatic;
				offender = type.FullName + "." + field.Name;
				return false;
			}
			return true;
		}

		private static bool IsOurs(Type type)
		{
			string space = type.Namespace;
			if (string.IsNullOrEmpty(space))
			{
				return false;
			}
			if (space.Length < OwnNamespacePrefix.Length)
			{
				return false;
			}
			if (!string.Equals(space.Substring(0, OwnNamespacePrefix.Length), OwnNamespacePrefix, StringComparison.Ordinal))
			{
				return false;
			}
			return space.Length == OwnNamespacePrefix.Length || space[OwnNamespacePrefix.Length] == '.';
		}

		private static string AssemblyNameOf(Type type)
		{
			Assembly assembly = type.Assembly;
			if (assembly == null)
			{
				return null;
			}
			return assembly.GetName().Name;
		}
	}
}
