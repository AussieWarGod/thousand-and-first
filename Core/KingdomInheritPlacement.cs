using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal sealed class KingdomInheritPlacement
	{
		private readonly KingdomInheritWork[] _works;

		internal readonly int EntryX;

		internal readonly int EntryY;

		internal readonly int CairnX;

		internal readonly int CairnY;

		internal readonly int HeartX;

		internal readonly int HeartY;

		internal readonly KingdomInheritEngineCheck RemainingEngineChecks;

		private readonly KingdomInheritWork[] _streets;

		internal readonly int SpatialVersion;

		internal int Count
		{
			get { return _works.Length; }
		}

		internal int StreetCount { get { return _streets.Length; } }

		internal KingdomInheritPlacement(KingdomInheritWork[] Works, int EntryX, int EntryY,
			int CairnX, int CairnY, int HeartX, int HeartY, KingdomInheritEngineCheck RemainingEngineChecks)
			: this(Works, EntryX, EntryY, CairnX, CairnY, HeartX, HeartY,
				RemainingEngineChecks, 0, null, null)
		{
		}

		internal KingdomInheritPlacement(KingdomInheritWork[] Works, int EntryX, int EntryY,
			int CairnX, int CairnY, int HeartX, int HeartY,
			KingdomInheritEngineCheck RemainingEngineChecks, int SpatialVersion,
			IList<int> StreetX, IList<int> StreetY)
		{
			_works = Works ?? new KingdomInheritWork[0];
			this.EntryX = EntryX;
			this.EntryY = EntryY;
			this.CairnX = CairnX;
			this.CairnY = CairnY;
			this.HeartX = HeartX;
			this.HeartY = HeartY;
			this.RemainingEngineChecks = RemainingEngineChecks;
			this.SpatialVersion = SpatialVersion;
			int count = StreetX == null || StreetY == null ? 0
				: Math.Min(StreetX.Count, StreetY.Count);
			_streets = new KingdomInheritWork[count];
			for (int i = 0; i < count; i++)
				_streets[i] = new KingdomInheritWork("inherit.street", StreetX[i], StreetY[i],
					0, KingdomInheritWorkState.Memory);
		}

		internal KingdomInheritWork WorkAt(int Index)
		{
			return (Index >= 0 && Index < _works.Length) ? _works[Index] : null;
		}

		internal int StreetXAt(int Index) { return _streets[Index].X; }

		internal int StreetYAt(int Index) { return _streets[Index].Y; }
	}

}
