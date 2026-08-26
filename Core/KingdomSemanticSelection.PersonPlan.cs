using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Frozen person payload produced before any dependent object is created.</summary>
	internal sealed class KingdomSemanticPersonPlan
	{
		internal int RulesVersion;
		internal long Sequence;
		internal string StreamId;
		internal uint EventKind;
		internal string Blueprint;
		internal string Origin;
		internal string Creed;
		internal string Name;
		internal string Title;
		internal string Arrived;
		internal int X = -1;
		internal int Y = -1;
	}

}
