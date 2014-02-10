namespace LiteProxy.Internal
{
    using System;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Threading;

    /// <summary> 
    /// Help wrap concrete types in interfaces that they don't explicitly implement.
    /// </summary> 
    public class WrapperGenerator : IDisposable
    {
        static readonly AppDomain WrappersAppDomain;
        static readonly ModuleBuilder ProxyModule;

        static WrapperGenerator()
        {
            WrappersAppDomain = Thread.GetDomain();
            var asmBuilder = WrappersAppDomain.DefineDynamicAssembly(
                new AssemblyName("Wrappers"),
                AssemblyBuilderAccess.Run);
            ProxyModule = asmBuilder.DefineDynamicModule("WrapperModule", false);
        }

        public static Type GenerateWrapperType(Type targetType, Type sourceType)
        {
            var wrapperTypeName = sourceType.Name + "To" + targetType.Name + "Wrapper";

            var pregenerated = ProxyModule.GetType(wrapperTypeName, false, false);
            if (pregenerated != null) return pregenerated;

            var proxyBuilder = GetProxyBuilder(targetType, wrapperTypeName);

            var srcField = typeof(WrapperBase).GetField("Src", BindingFlags.Instance | BindingFlags.NonPublic);
            if (srcField == null) throw new ApplicationException("Source binding failed!");

            foreach (var method in targetType.GetMethods())
            {
                BindProxyMethod(targetType, sourceType, srcField, method, proxyBuilder);
            }

            return proxyBuilder.CreateType();
        }

        private static TypeBuilder GetProxyBuilder(Type targetType, string wrapperTypeName)
        {
            return ProxyModule.DefineType(wrapperTypeName,
                                          TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
                                          typeof(WrapperBase), new[] { targetType });
        }

        /// <summary>
        /// Emit a new method in a target type that calls a method from the source type.
        /// </summary>
        private static void BindProxyMethod(Type targetType, Type sourceType, FieldInfo srcField, MethodInfo method, TypeBuilder proxyBuilder)
        {
            var parameters = method.GetParameters();
            var parameterTypes = new Type[parameters.Length];
            for (var i = 0; i < parameters.Length; i++) parameterTypes[i] = parameters[i].ParameterType;

            var srcMethod = sourceType.GetMethod(method.Name, parameterTypes);
            if (srcMethod == null)
                throw new MissingMethodException(method.Name + " is not implemented by " + sourceType.FullName + " as required by the " + targetType.FullName + " interface.");

            var methodBuilder = proxyBuilder
                .DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual, method.ReturnType, parameterTypes);

            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, srcField);
            for (var i = 1; i < parameters.Length + 1; i++) ilGenerator.Emit(OpCodes.Ldarg, i);

            ilGenerator.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, srcMethod);
            ilGenerator.Emit(OpCodes.Ret);
        }

        public static WrapperBase GenerateWrapperPrototype(Type targetType, Type sourceType)
        {
            var wrapperType = GenerateWrapperType(targetType, sourceType);
            return (WrapperBase)Activator.CreateInstance(wrapperType);
        }

        public void Dispose()
        {
            AppDomain.Unload(WrappersAppDomain);
        }
    }
}