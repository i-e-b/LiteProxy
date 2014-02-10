namespace LiteProxy
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    /// <summary>
    /// Creates run-time backing types for abstract classes and interfaces
    /// </summary>
    public static class DynamicProxy
    {
        static readonly AssemblyBuilder DynamicAssembly;
        static readonly ModuleBuilder ModuleBuilder;

        /// <summary>
        /// Static constructor builds a dynamic container module to
        /// hold proxy objects
        /// </summary>
        static DynamicProxy()
        {
            var assemblyName = new AssemblyName("DynImpl");
            DynamicAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            ModuleBuilder = DynamicAssembly.DefineDynamicModule("DynImplModule");
        }

        /// <summary>
        /// Return a concrete wrapper type for the given input type `T` (creating if required)
        /// </summary>
        public static Type GetWrapperTypeFor<T>() 
        {
            return GetWrapperTypeFor(typeof(T));
        }

        /// <summary>
        /// Return a concrete wrapper type for the given input type (creating if required)
        /// </summary>
        public static Type GetWrapperTypeFor(Type targetType)
        {
            lock (DynamicAssembly)
            {
                return DynamicAssembly.GetType(ProxyName(targetType)) ?? GetConstructedType(targetType);
            }
        }

        /// <summary>
        /// Get an empty instance of a dynamic proxy for type T.
        /// All public fields are writable and all properties have both getters and setters.
        /// </summary>
        public static T GetInstanceFor<T>()
        {
            return (T)GetInstanceFor(typeof(T));
        }

        /// <summary>
        /// Get an empty instance of a dynamic proxy for the given type.
        /// All public fields are writable and all properties have both getters and setters.
        /// </summary>
        public static object GetInstanceFor(Type targetType)
        {
            return Activator.CreateInstance(GetWrapperTypeFor(targetType));
        }

        static string ProxyName(Type targetType)
        {
            return targetType.Name + "Proxy";
        }

        static Type GetConstructedType(Type targetType)
        {
            var typeBuilder = ModuleBuilder.DefineType(targetType.Name + "Proxy", TypeAttributes.Public);

            var ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new Type[] { });
            var ilGenerator = ctorBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ret);

            IncludeType(targetType, typeBuilder);

            foreach (var face in targetType.GetInterfaces())
                IncludeType(face, typeBuilder);

            return typeBuilder.CreateType();
        }

        static void IncludeType(Type typeOfT, TypeBuilder typeBuilder)
        {
            var methodInfos = typeOfT.GetMethods();
            foreach (var methodInfo in methodInfos)
            {
                if (methodInfo.Name.StartsWith("set_")) continue; // we always add a set for a get.

                if (methodInfo.Name.StartsWith("get_"))
                {
                    BindProperty(typeBuilder, methodInfo);
                }
                else
                {
                    if (methodInfo.IsAbstract) BindMethod(typeBuilder, methodInfo);
                }
            }

            if (typeOfT.IsInterface) typeBuilder.AddInterfaceImplementation(typeOfT);
            else if (typeOfT.IsAbstract) typeBuilder.SetParent(typeOfT);
        }

        static void BindMethod(TypeBuilder typeBuilder, MethodInfo methodInfo)
        {
            var args = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            var methodBuilder = typeBuilder.DefineMethod(
                methodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Virtual,
                CallingConventions.HasThis,
                methodInfo.ReturnType,
                args
                );

            var methodILGen = methodBuilder.GetILGenerator();

            // Unimplemented method -- throw an exception
            var notImplemented = typeof(NotImplementedException);
            var exCtorInfo = notImplemented.GetConstructor(new[] { typeof(string) });
            if (exCtorInfo == null) throw new Exception("Could not find exception constructor. This is a major bug in DynamicProxy");
            methodILGen.Emit(OpCodes.Ldstr, "The method \"" + methodInfo.Name + "\" is not implemented by DynamicProxy");
            methodILGen.Emit(OpCodes.Newobj, exCtorInfo);
            methodILGen.ThrowException(notImplemented);

            typeBuilder.DefineMethodOverride(methodBuilder, methodInfo);
        }

        /// <summary>
        /// Bind a new property into a type builder with getters and setters.
        /// </summary>
        public static void BindProperty(TypeBuilder typeBuilder, MethodInfo methodInfo)
        {
            // Backing Field
            var propertyName = methodInfo.Name.Replace("get_", "");
            var propertyType = methodInfo.ReturnType;
            var backingField = typeBuilder.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

            //Getter
            var backingGet = typeBuilder.DefineMethod("get_" + propertyName, MethodAttributes.Public |
                MethodAttributes.SpecialName | MethodAttributes.Virtual |
                MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
            var getIl = backingGet.GetILGenerator();

            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, backingField);
            getIl.Emit(OpCodes.Ret);


            //Setter
            var backingSet = typeBuilder.DefineMethod("set_" + propertyName, MethodAttributes.Public |
                MethodAttributes.SpecialName | MethodAttributes.Virtual |
                MethodAttributes.HideBySig, null, new[] { propertyType });

            var setIl = backingSet.GetILGenerator();

            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, backingField);
            setIl.Emit(OpCodes.Ret);

            // Property
            var propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.None, propertyType, null);
            propertyBuilder.SetGetMethod(backingGet);
            propertyBuilder.SetSetMethod(backingSet);
        }
    }
}