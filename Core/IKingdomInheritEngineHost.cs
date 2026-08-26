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
	/// <summary>
	/// Narrow engine seam. All inspection methods are called before TryCreateFresh; tests enforce
	/// that a refused site crosses no mutating method.
	/// </summary>
	internal interface IKingdomInheritEngineHost
	{
		int Width { get; }

		int Height { get; }

		string ZoneId { get; }

		string TargetGameId { get; }

		string ReadApplicationMarker();

		int CountApplicationObjects(string Marker);

		bool HasAnyApplicationObjects();

		bool HasExactApplicationObject(string Marker, KingdomInheritBuildSpec Spec, string CairnText);

		bool HasBlueprint(string Blueprint);

		bool TryReadCell(int X, int Y, out KingdomInheritCellFacts Facts);

		bool TryCreateFresh(KingdomInheritBuildSpec Spec, string Marker, string CairnText,
			out object Handle, out string Failure);

		bool IsFreshEmpty(object Handle);

		bool TryPlace(object Handle, int X, int Y, out string Failure);

		bool Discard(object Handle);

		bool TryWriteApplicationMarker(string Marker, out string Failure);

		bool TryRemoveApplicationMarker(string Marker);
	}

}
