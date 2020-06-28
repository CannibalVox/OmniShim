using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace omnishim
{
    public sealed class OmniShim
    {
        private static OmniShim instance;

        internal OmniShim()
        {
            LoadTypeCache();
            CreateDynamicModule();
            instance = this;
        }

        private Dictionary<String, Type> publicTypeCache = new Dictionary<string, Type>();
        private ModuleBuilder moduleBuilder;

        private void LoadTypeCache()
        {
            var allTypes = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).SelectMany(a => a.GetExportedTypes());
            foreach(Type t in allTypes)
            {
                publicTypeCache[t.FullName] = t;
            }
        }

        private void CreateDynamicModule()
        {
            AssemblyName assemblyName = new AssemblyName("OmniShimDynamicClasses");
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule("OmniShimDynamicClasses");
        }

        private static void checkInstance()
        {
            if (instance == null)
            {
                throw new Exception("OmniShim may not be interacted with prior to the OmniShimSystem executing 'PreStart'");
            }
        }

        public static Type RegisterForInterface<T>(params string[] interfaceTypeNames) where T : new()
        {
            checkInstance();
            return instance._RegisterForInterface<T>(interfaceTypeNames);
        }
        private Type _RegisterForInterface<T>(params string[] interfaceTypeNames) where T : new()
        {
            var interfaceTypes = new List<Type>();
            foreach (string interfaceName in interfaceTypeNames)
            {
                Type iType;
                if (publicTypeCache.TryGetValue(interfaceName, out iType))
                {
                    interfaceTypes.Add(iType);
                }
            }

            return CreateAdapterType<T>(interfaceTypes);
        }

        private Type CreateAdapterType<T>(List<Type> interfaces) where T : new()
        {
            Type inputType = typeof(T);
            string name = string.Format("OmniShim$${0}", inputType.FullName.Replace('.', '_'));
            var tBuilder = moduleBuilder.DefineType(name, TypeAttributes.Public);
            tBuilder.SetParent(inputType);
            foreach (Type iFace in interfaces)
            {
                tBuilder.AddInterfaceImplementation(iFace);
            }

            foreach (ConstructorInfo construct in inputType.GetConstructors())
            {
                var typeArray = construct.GetParameters().Select(p => p.ParameterType).ToArray();
                var newConstructor = tBuilder.DefineConstructor(construct.Attributes, construct.CallingConvention, typeArray);
                var code = newConstructor.GetILGenerator();

                code.Emit(OpCodes.Ldarg_0);
                for (int i = 0; i < typeArray.Length; i++)
                {
                    code.Emit(OpCodes.Ldarg_S, i + 1);
                }
                code.Emit(OpCodes.Call, construct);
                code.Emit(OpCodes.Ret);
            }

            return tBuilder.CreateType();
        }
    }
}
