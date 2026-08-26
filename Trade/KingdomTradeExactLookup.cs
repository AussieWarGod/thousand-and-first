using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public enum KingdomTradeExactLookup : byte
	{
		Incomplete = 0,
		Missing = 1,
		ExactUnique = 2,
		Ambiguous = 3
	}
}
