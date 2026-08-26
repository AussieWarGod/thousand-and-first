using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !TAF_TESTS
using XRL;
using XRL.World;
using XRL.World.Parts;
#endif

namespace ThousandAndFirst
{
	/// <summary>Read-only facts copied from a cell during preflight.</summary>
	internal struct KingdomInheritCellFacts
	{
		internal bool Exists;

		internal bool Occupied;

		internal bool Terrain;

		internal bool Stairs;

		internal bool Connection;

		internal bool Walkable;
	}

}
