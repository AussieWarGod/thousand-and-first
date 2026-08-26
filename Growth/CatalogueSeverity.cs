using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// How loudly a <see cref="CatalogueFinding"/> should read in the log. Neither value ever
	/// unregisters anything: validation reports, and the catalogue is whatever the files said.
	/// </summary>
	public enum CatalogueSeverity
	{
		/// <summary>Worth saying out loud; the catalogue still works.</summary>
		Note = 0,

		/// <summary>Something in the file cannot do what it says it does. Still logged rather than
		/// thrown, because a third-party file must never be able to delete the base catalogue by
		/// being wrong about its own entry.</summary>
		Fault = 1
	}
}
