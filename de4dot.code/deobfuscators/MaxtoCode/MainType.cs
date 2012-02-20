﻿/*
    Copyright (C) 2011-2012 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using Mono.Cecil;
using de4dot.blocks;

namespace de4dot.code.deobfuscators.MaxtoCode {
	class MainType {
		ModuleDefinition module;
		TypeDefinition mcType;
		ModuleReference mcModule1, mcModule2;

		public bool Detected {
			get { return mcType != null; }
		}

		public MainType(ModuleDefinition module) {
			this.module = module;
		}

		public void find() {
			var cctor = getCctor();
			if (cctor == null)
				return;

			foreach (var info in DotNetUtils.getCalledMethods(module, cctor)) {
				var method = info.Item2;
				if (method.Name != "Startup")
					continue;
				if (!DotNetUtils.isMethod(method, "System.Void", "()"))
					continue;

				ModuleReference module1, module2;
				if (!checkType(method.DeclaringType, out module1, out module2))
					return;

				mcType = method.DeclaringType;
				mcModule1 = module1;
				mcModule2 = module2;
				break;
			}
		}

		MethodDefinition getCctor() {
			int checksLeft = 3;
			foreach (var type in module.GetTypes()) {
				if (type.IsEnum)
					continue;
				var cctor = DotNetUtils.getMethod(type, ".cctor");
				if (cctor != null)
					return cctor;
				if (--checksLeft <= 0)
					return null;
			}
			return null;
		}

		static bool checkType(TypeDefinition type, out ModuleReference module1, out ModuleReference module2) {
			module1 = module2 = null;

			if (DotNetUtils.getMethod(type, "Startup") == null)
				return false;

			var pinvokes = getPinvokes(type);
			var pinvokeList = getPinvokeList(pinvokes, "CheckRuntime");
			if (pinvokeList == null)
				return false;
			if (getPinvokeList(pinvokes, "MainDLL") == null)
				return false;
			if (getPinvokeList(pinvokes, "GetModuleBase") == null)
				return false;

			module1 = pinvokeList[0].PInvokeInfo.Module;
			module2 = pinvokeList[1].PInvokeInfo.Module;
			return true;
		}

		static Dictionary<string, List<MethodDefinition>> getPinvokes(TypeDefinition type) {
			var pinvokes = new Dictionary<string, List<MethodDefinition>>(StringComparer.Ordinal);
			foreach (var method in type.Methods) {
				var info = method.PInvokeInfo;
				if (info == null || info.EntryPoint == null)
					continue;
				List<MethodDefinition> list;
				if (!pinvokes.TryGetValue(info.EntryPoint, out list))
					pinvokes[info.EntryPoint] = list = new List<MethodDefinition>();
				list.Add(method);
			}
			return pinvokes;
		}

		static List<MethodDefinition> getPinvokeList(Dictionary<string, List<MethodDefinition>> pinvokes, string methodName) {
			List<MethodDefinition> list;
			if (!pinvokes.TryGetValue(methodName, out list))
				return null;
			if (list.Count != 2)
				return null;
			return list;
		}
	}
}
