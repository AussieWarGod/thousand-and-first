using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ThousandAndFirst
{
	internal static class KingdomQuickstartStockRules
	{
		internal static bool HasDistinctChildren<T>(IList<T> Children) where T : class
		{
			if (Children == null) return false;
			var seen = new HashSet<T>(ReferenceIdentity<T>.Instance);
			for (int i = 0; i < Children.Count; i++)
				if (ReferenceEquals(Children[i], null) || !seen.Add(Children[i])) return false;
			return true;
		}

		private sealed class ReferenceIdentity<T> : IEqualityComparer<T> where T : class
		{
			internal static readonly ReferenceIdentity<T> Instance = new ReferenceIdentity<T>();
			public bool Equals(T Left, T Right) { return ReferenceEquals(Left, Right); }
			public int GetHashCode(T Value) { return RuntimeHelpers.GetHashCode(Value); }
		}
	}
}
