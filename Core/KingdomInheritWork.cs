using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal sealed class KingdomInheritWork
	{
		internal readonly string Key;

		internal readonly int X;

		internal readonly int Y;

		internal readonly int Condition;

		internal readonly KingdomInheritWorkState State;

		internal readonly string ArchitectureSnapshot;

		internal readonly string ArchitectureHash;

		internal KingdomInheritWork(string Key, int X, int Y, int Condition, KingdomInheritWorkState State)
			: this(Key, X, Y, Condition, State, "", "")
		{
		}

		internal KingdomInheritWork(string Key, int X, int Y, int Condition,
			KingdomInheritWorkState State, string ArchitectureSnapshot, string ArchitectureHash)
		{
			this.Key = Key ?? "";
			this.X = X;
			this.Y = Y;
			this.Condition = Condition;
			this.State = State;
			this.ArchitectureSnapshot = ArchitectureSnapshot ?? "";
			this.ArchitectureHash = ArchitectureHash ?? "";
		}
	}

}
