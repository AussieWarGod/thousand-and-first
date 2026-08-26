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
	/// <summary>One allowlisted object the transaction will construct after preflight.</summary>
	internal sealed class KingdomInheritBuildSpec
	{
		internal readonly int Index;

		internal readonly string Key;

		internal readonly string Blueprint;

		internal readonly int X;

		internal readonly int Y;

		internal readonly int Condition;

		internal readonly KingdomInheritWorkState State;

		internal readonly int FootprintWidth;

		internal readonly int FootprintHeight;

		internal readonly int FootprintX;

		internal readonly int FootprintY;

		internal readonly bool IsArchitecture;

		internal readonly bool IsStreet;

		internal readonly string ArchitectureSnapshot;

		internal readonly string ArchitectureHash;

		internal KingdomInheritBuildSpec(int Index, KingdomInheritWork Work, string Blueprint,
			int FootprintX, int FootprintY, int FootprintWidth, int FootprintHeight)
		{
			this.Index = Index;
			Key = Work.Key;
			this.Blueprint = Blueprint;
			X = Work.X;
			Y = Work.Y;
			Condition = Work.Condition;
			State = Work.State;
			this.FootprintWidth = FootprintWidth;
			this.FootprintHeight = FootprintHeight;
			this.FootprintX = FootprintX;
			this.FootprintY = FootprintY;
			IsArchitecture = Work.ArchitectureSnapshot.Length > 0;
			IsStreet = false;
			ArchitectureSnapshot = Work.ArchitectureSnapshot;
			ArchitectureHash = Work.ArchitectureHash;
		}

		internal KingdomInheritBuildSpec(int Index, int X, int Y)
		{
			this.Index = Index;
			Key = "inherit.street";
			Blueprint = "DirtPath";
			this.X = X;
			this.Y = Y;
			Condition = 0;
			State = KingdomInheritWorkState.Memory;
			FootprintWidth = 1;
			FootprintHeight = 1;
			FootprintX = X;
			FootprintY = Y;
			IsArchitecture = false;
			IsStreet = true;
			ArchitectureSnapshot = "";
			ArchitectureHash = "";
		}
	}

}
