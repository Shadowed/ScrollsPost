using System;
using System.IO;

using ScrollsModLoader.Interfaces;
using UnityEngine;
using Mono.Cecil;
using System.Collections.Generic;
using System.Text;

namespace ScrollsPost
{
	public class ScrollsPost : BaseMod
	{
		private String configFolder;

		public ScrollsPost()
		{
			configFolder = this.OwnFolder() + Path.DirectorySeparatorChar + "config";
			if (!Directory.Exists(configFolder + Path.DirectorySeparatorChar))
			{
				Directory.CreateDirectory(configFolder + Path.DirectorySeparatorChar);
			}
		}

		public static string GetName()
		{
			return "ScrollsPost";
		}

		public static int GetVersion()
		{
			return 2;
		}

		
		public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version)
		{
			try
			{
				return new MethodDefinition[] {
				};
			} 
			catch
			{
				return new MethodDefinition[] {};
			}
		}

		public override bool BeforeInvoke(InvocationInfo info, out object returnValue)
		{
			returnValue = null;
			return false;
		}

		public override void AfterInvoke(InvocationInfo info, ref object returnValue)
		{
			return;
		}
	}
}


