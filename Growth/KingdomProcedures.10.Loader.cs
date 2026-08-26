using System;
using System.Collections.Generic;
using System.Reflection;

using Qud.API;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

namespace ThousandAndFirst
{
	/// <summary>
	/// Forgets the registry on every game load, for the reason the research registry states beside
	/// its own: the registry and the notes-filed flag are PROCESS statics, so a second game in the
	/// same session would otherwise believe its journal notes were already filed and quietly hide
	/// every named procedure from a founder who had found none of them.
	/// </summary>
	[HasCallAfterGameLoaded]
	public static class KingdomProcedureLoader
	{
		[CallAfterGameLoaded]
		public static void ForgetRegistry()
		{
			KingdomProcedures.Reload();
		}
	}
}
