﻿using Mono.Cecil;
using System.IO;
using System.Linq;

namespace CollectDependencies
{
    class VirtualizedModule
    {
        private readonly FileInfo file;
        private ModuleDefinition module;

        public static VirtualizedModule Load(string engineFile)
        {
            return new VirtualizedModule(engineFile);
        }

        private VirtualizedModule(string assemblyFile)
        {
            file = new FileInfo(assemblyFile);

            LoadModules();
        }

        private void LoadModules()
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(file.Directory?.FullName);

            var parameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadWrite = false,
                ReadingMode = ReadingMode.Immediate,
                InMemory = true
            };

            module = ModuleDefinition.ReadModule(file.FullName, parameters);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetFile"></param>
        public void Virtualize(string targetFile)
        {

            foreach (var type in module.Types)
            {
                VirtualizeType(type);
            }

            module.Write(targetFile);
        }

        private void VirtualizeType(TypeDefinition type)
        {
            if(type.IsSealed)
            {
                // Unseal
                type.IsSealed = false;
            }

            if (type.IsInterface) return;
            if (type.IsAbstract) return;

            // These two don't seem to work.
            if (type.Name == "SceneControl" || type.Name == "ConfigUI") return;
            
            // Take care of sub types
            foreach (var subType in type.NestedTypes)
            {
                VirtualizeType(subType);
            }

            foreach (var method in type.Methods)
            {
                if (method.IsManaged
                    && method.IsIL
                    && !method.IsStatic
                    && !method.IsVirtual
                    && !method.IsAbstract
                    && !method.IsAddOn
                    && !method.IsConstructor
                    && !method.IsSpecialName
                    && !method.IsGenericInstance
                    && !method.HasOverrides)
                {
                    method.IsVirtual = true;
                    method.IsPublic = true;
                    method.IsPrivate = false;
                    method.IsNewSlot = true;
                    method.IsHideBySig = true;
                }
            }

            foreach (var field in type.Fields)
            {
                if (field.IsPrivate) field.IsFamily = true;
            }
        }

        public bool IsVirtualized
        {
            get
            {
                var awakeMethods = module.GetTypes().SelectMany(t => t.Methods.Where(m => m.Name == "Awake"));
                var methodDefinitions = awakeMethods as MethodDefinition[] ?? awakeMethods.ToArray();
                if (!methodDefinitions.Any()) return false;

                return ((float)methodDefinitions.Count(m => m.IsVirtual) / methodDefinitions.Count()) > 0.5f;
            }
        }
    }
}
