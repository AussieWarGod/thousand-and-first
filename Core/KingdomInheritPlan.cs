using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal sealed class KingdomInheritPlan
	{
		private readonly KingdomInheritWork[] _works;

		internal readonly int Width;

		internal readonly int Height;

		internal int Count
		{
			get { return _works.Length; }
		}

		internal KingdomInheritPlan(KingdomInheritWork[] Works, int Width, int Height)
		{
			_works = Works ?? new KingdomInheritWork[0];
			this.Width = Width;
			this.Height = Height;
		}

		internal KingdomInheritWork WorkAt(int Index)
		{
			return (Index >= 0 && Index < _works.Length) ? _works[Index] : null;
		}
	}

}
