using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ProtoBuf;

namespace omnishim
{
    public sealed class OmniShim
    {
        private static OmniShim instance;
        private static MethodInfo changeTypeMethod = typeof(Serializer).GetMethod(nameof(Serializer.ChangeType), BindingFlags.Static | BindingFlags.Public); 

        internal OmniShim()
        {
            LoadTypeCache();
            CreateDynamicModule();
            instance = this;
        }

        private Dictionary<String, Type> publicTypeCache = new Dictionary<string, Type>();
        private Dictionary<Type, HashSet<Type>> protoMirrors = new Dictionary<Type, HashSet<Type>>();
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

        public static void RegisterMirrorProto<T>(string protoTypeName) where T : new()
        {
            checkInstance();
            instance._RegisterMirrorProto<T>(protoTypeName);
        }

        private void _RegisterMirrorProto<T>(string protoTypeName) where T : new() {

            if (typeof(T).GetCustomAttribute<ProtoContractAttribute>() == null)
            {
                throw new InvalidOperationException(string.Format("Generic parameter {0} must be a Proto contract", typeof(T).FullName));
            }

            Type targetType;
            if (!publicTypeCache.TryGetValue(protoTypeName, out targetType))
            {
                return;
            }

            if (typeof(T).GetCustomAttribute<ProtoContractAttribute>() == null)
            {
                throw new ArgumentException(string.Format("Type '{0}' must be a Proto contract", typeof(T).FullName, "protoTypeName"));
            }

            HashSet<Type> mirrors;
            if (!protoMirrors.TryGetValue(targetType, out mirrors))
            {
                mirrors = new HashSet<Type>();
                protoMirrors[targetType] = mirrors;
            }

            if (!mirrors.Contains(typeof(T)))
            {
                mirrors.Add(typeof(T));
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
                if (publicTypeCache.TryGetValue(interfaceName, out Type iType))
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
                Type[] typeArray = construct.GetParameters().Select(p => p.ParameterType).ToArray();
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

            var requiredMethods = interfaces
                .SelectMany(i => i.GetMethods())
                .GroupBy(m => new Tuple<string, int>(m.Name, m.GetParameters().Length))
                .ToDictionary(m => m.Key, m => m.ToList());
            var availableMethods = inputType.GetMethods()
                .Where(m => m.IsVirtual && !m.IsStatic)
                .GroupBy(m => new Tuple<string, int>(m.Name, m.GetParameters().Length))
                .ToDictionary(m => m.Key, m => m.ToList());

            foreach(Tuple<string, int> methodType in requiredMethods.Keys)
            {
                List<MethodInfo> interfaceMethods = requiredMethods[methodType];

                List<MethodInfo> possibleMatches;
                if (availableMethods.TryGetValue(methodType, out possibleMatches))
                {
                    attemptMatches(tBuilder, interfaceMethods, possibleMatches);
                }
            }

            return tBuilder.CreateType();
        }

        private void attemptMatches(TypeBuilder tBuilder, List<MethodInfo> requiredMethods, List<MethodInfo> possibleMatches)
        {
            foreach (MethodInfo requiredMethod in requiredMethods)
            {
                foreach(MethodInfo attempt in possibleMatches)
                {
                    bool exactMatch = true;
                    TypeMatch returnMatch = canMatchType(attempt.ReturnType, requiredMethod.ReturnType);
                    if (returnMatch == TypeMatch.NoMatch)
                    {
                        continue;
                    } else if (exactMatch && returnMatch != TypeMatch.ExactMatch)
                    {
                        exactMatch = false;
                    }

                    var requireParams = requiredMethod.GetParameters();
                    var attemptParams = attempt.GetParameters();
                    var paramMatches = new TypeMatch[requireParams.Length];

                    bool matchFailed = false;
                    for (int i=0; i < requireParams.Length; i++)
                    {
                        TypeMatch paramMatch = canMatchType(attemptParams[i].ParameterType, requireParams[i].ParameterType);
                        if (returnMatch == TypeMatch.NoMatch)
                        {
                            matchFailed = true;
                            break;
                        } else if (exactMatch && paramMatch != TypeMatch.ExactMatch)
                        {
                            exactMatch = false;
                        }

                        paramMatches[i] = paramMatch;
                    }

                    if (!matchFailed)
                    {
                        if (!exactMatch) writeMappingMethod(tBuilder, requiredMethod, attempt, returnMatch, paramMatches);
                        break;
                    }
                }
            }
        }

        private enum TypeMatch
        {
            NoMatch,
            ExactMatch,
            ProtoMirror
        }

        private TypeMatch canMatchType(Type possibleType, Type requiredType)
        {
            if (possibleType == requiredType)
            {
                return TypeMatch.ExactMatch;
            }

            if (protoMirrors.TryGetValue(requiredType, out HashSet<Type> mirrors))
            {
                if (mirrors.Contains(possibleType)) return TypeMatch.ProtoMirror;
            }

            return TypeMatch.NoMatch;
        }

        private void writeMappingMethod(TypeBuilder tBuilder, MethodInfo iFaceMethod, MethodInfo classMethod, TypeMatch returnMatch, TypeMatch[] paramMatches)
        {
            var iFaceParams = iFaceMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            var classParams = classMethod.GetParameters().Select(p => p.ParameterType).ToArray();

            var methodBuilder = tBuilder.DefineMethod(iFaceMethod.Name, classMethod.Attributes, iFaceMethod.CallingConvention, iFaceMethod.ReturnType, iFaceParams);
            var codeGen = methodBuilder.GetILGenerator();

            var convertedArgs = new LocalBuilder[iFaceParams.Length];

            for (int i = 0; i < paramMatches.Length; i++)
            {
                if (paramMatches[i] == TypeMatch.ProtoMirror)
                {
                    //Convert to input
                    var convertMethod = changeTypeMethod.MakeGenericMethod(iFaceParams[i], classParams[i]);

                    codeGen.Emit(OpCodes.Ldarg_S, i + 1);
                    codeGen.Emit(OpCodes.Call, convertMethod);
                    convertedArgs[i] = codeGen.DeclareLocal(classParams[i]);
                    codeGen.Emit(OpCodes.Stloc, convertedArgs[i]);
                }
            }

            //Call inner class
            codeGen.Emit(OpCodes.Ldarg_0);
            for (int i = 0; i < convertedArgs.Length; i++)
            {
                if (convertedArgs[i] == null)
                {
                    //Use param
                    codeGen.Emit(OpCodes.Ldarg_S, i + 1);
                } else
                {
                    //Use local
                    codeGen.Emit(OpCodes.Ldloc, convertedArgs[i]);
                }
            }
            codeGen.Emit(OpCodes.Call, classMethod);

            //Convert to output
            if (returnMatch == TypeMatch.ProtoMirror)
            {
                var convertMethod = changeTypeMethod.MakeGenericMethod(classMethod.ReturnType, iFaceMethod.ReturnType);
                codeGen.Emit(OpCodes.Call, convertMethod);
            }

            codeGen.Emit(OpCodes.Ret);
        }
    }
}
