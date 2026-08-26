using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>One bounded, named problem in the merged authored-architecture catalogue.</summary>
	public sealed class KingdomArchitectureFault
	{
		public string Name { get; private set; }
		public string Message { get; private set; }

		internal KingdomArchitectureFault(string Name, string Message)
		{
			this.Name = Name;
			this.Message = Message;
		}

		public override string ToString()
		{
			return Name + ": " + Message;
		}
	}
}
