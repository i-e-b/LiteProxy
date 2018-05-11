using System.Linq;

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

        /// <summary>
        /// Create a new type wrapper binding calls of the source type to matching declarations
        /// of the target type
        /// </summary>
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
        
        /// <summary>
        /// Create a new type wrapper that delegates call to the source
        /// type through to a single delegate method in a mock body.
        /// </summary>
        public static Type GenerateDirectDelegate(Type targetType)
        {
            if (targetType.IsInterface)
            {
                throw new Exception("Interfaces can't be delegated to");
            }

            var wrapperTypeName = targetType.Name + "_LazyDelegate";

            // cached version:
            var pregenerated = ProxyModule.GetType(wrapperTypeName, false, false);
            if (pregenerated != null) return pregenerated;


            var proxyBuilder = ProxyModule.DefineType(wrapperTypeName,
                TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
                targetType);

            var makerType = typeof(Func<>).MakeGenericType(targetType);
            var makerInvokeCall = makerType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            if (makerInvokeCall == null) throw new Exception("Could not find invoker for constructor function");

            // field to hold lazy delegate
            var baseFld = proxyBuilder.DefineField("__base", targetType, FieldAttributes.Public);
            var makerFld = proxyBuilder.DefineField("__baseMaker", makerType, FieldAttributes.Public); // constructor funtion

            // method to generate the lazy delegate
            var ensurer = proxyBuilder.DefineMethod("__EnsureBase", MethodAttributes.Family | MethodAttributes.Public, CallingConventions.HasThis);
            var ensGen = ensurer.GetILGenerator();
            var retEnd = ensGen.DefineLabel(); // label for return
            ensGen.DeclareLocal(typeof(bool));
            ensGen.Emit(OpCodes.Ldarg_0); // load 'this'
            ensGen.Emit(OpCodes.Ldfld, baseFld); // load the '__base' ref
            ensGen.Emit(OpCodes.Ldnull); // load a null
            ensGen.Emit(OpCodes.Ceq); // compare: __base == null
            ensGen.Emit(OpCodes.Brfalse_S, retEnd); // if not null, jump to return


            ensGen.Emit(OpCodes.Ldarg_0); // load 'this'
            ensGen.Emit(OpCodes.Ldarg_0); // load 'this'
            ensGen.Emit(OpCodes.Ldfld, makerFld); // load constructor function of this instance
            ensGen.Emit(OpCodes.Callvirt, makerInvokeCall); // call constructor function against that instance
            ensGen.Emit(OpCodes.Stfld, baseFld); // store result in the holding field

            ensGen.MarkLabel(retEnd); // set label to this position
            ensGen.Emit(OpCodes.Ret);

            //var srcField = AddMockCore(proxyBuilder); // TODO: need an 'ensure created' and re-pipe call
            /*var rebindMethod = AddRebindingMethod(proxyBuilder);

            foreach (var method in targetType.GetMethods())
            {
                BindMethod(rebindMethod, method, proxyBuilder);
                //BindProxyMethod(targetType, proxyBuilder, srcField, method, proxyBuilder);
            }*/

            foreach (var prop in targetType.GetProperties())
            {
                var parameterTypes = prop.GetIndexParameters().Select(p=>p.GetType()).ToArray();
                var buil = proxyBuilder.DefineProperty(prop.Name, PropertyAttributes.None, prop.PropertyType, parameterTypes);

                // write a getter method, that calls down to the ensure function, then calls delegate.
                var getter = proxyBuilder.DefineMethod("get_" + prop.Name, MethodAttributes.Virtual | MethodAttributes.ReuseSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Public,
                    prop.PropertyType, parameterTypes);
                var gb = getter.GetILGenerator();

                gb.DeclareLocal(typeof(object));
                gb.Emit(OpCodes.Ldarg_0);// load 'this'
                gb.Emit(OpCodes.Call, ensurer); // call the ensurer function

                gb.Emit(OpCodes.Ldarg_0);// load 'this'
                gb.Emit(OpCodes.Ldfld, baseFld); // load the ensured delegate
                gb.Emit(OpCodes.Callvirt, prop.GetMethod); // call the underlying getter of the delegate
                gb.Emit(OpCodes.Stloc_0); // evaluation stack to local
                gb.Emit(OpCodes.Ldloc_0);// local back to evaluation stack

                gb.Emit(OpCodes.Ret); // return the result.

                buil.SetGetMethod(getter);


                // TODO: write setter logic
                //buil.SetSetMethod(getter);
                /*if (prop.CanRead)
                {
                    //BindMethod(rebindMethod, prop.GetMethod, proxyBuilder);
                    //BindProxyMethod(targetType, proxyBuilder, srcField, prop.GetMethod, proxyBuilder);
                }
                if (prop.CanWrite)
                {
                    //BindMethod(rebindMethod, prop.SetMethod, proxyBuilder);
                    //BindProxyMethod(targetType, proxyBuilder, srcField, prop.SetMethod, proxyBuilder);
                }*/
            }

            return proxyBuilder.CreateType();
        }

        /// <summary>
        /// Create a new type wrapper that delegates call to the source
        /// type through to a single delegate method in a mock body.
        /// </summary>
        public static Type GenerateMockType(Type targetType)
        {
            var wrapperTypeName = targetType.Name + "_Mock";

            var pregenerated = ProxyModule.GetType(wrapperTypeName, false, false);
            if (pregenerated != null) return pregenerated;

            TypeBuilder proxyBuilder;
            if (targetType.IsInterface)
            {
                proxyBuilder = ProxyModule.DefineType(wrapperTypeName,
                    TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
                    typeof(MockBase), new[] { targetType });
            }
            else
            {
                proxyBuilder = ProxyModule.DefineType(wrapperTypeName,
                    TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
                    targetType);
            }


            var srcField = AddMockCore(proxyBuilder);

            foreach (var method in targetType.GetMethods())
            {
                BindMockMethod(srcField, method, proxyBuilder);
            }

            return proxyBuilder.CreateType();
        }
        
        /// <summary>
        /// Add a field for the mock, and a constructor that populates it
        /// </summary>
        static MethodInfo AddRebindingMethod(TypeBuilder builder)
        {
            var method = builder.DefineMethod("__mockcore", MethodAttributes.Public);
            //var ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[0]).GetILGenerator();
            //var coreCtor = typeof(MockCore).GetConstructor(new Type[] { });
            //var objCtor = typeof(object).GetConstructor(new Type[] {});

            var gen = method.GetILGenerator();

            /*gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, objCtor);

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Newobj, coreCtor);
            gen.Emit(OpCodes.Stfld, field);
            */
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ret);

            return method;
        }

        /// <summary>
        /// Add a field for the mock, and a constructor that populates it
        /// </summary>
        static FieldInfo AddMockCore(TypeBuilder builder)
        {
            var field = builder.DefineField("__mockcore", typeof(IMock), FieldAttributes.Public);
            var ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[0]).GetILGenerator();
            var coreCtor = typeof(MockCore).GetConstructor(new Type[] { });
            var objCtor = typeof(object).GetConstructor(new Type[] {});

            if (objCtor == null) throw new Exception("Compiler is malformed");
            if (coreCtor == null) throw new Exception("MockCore object is malformed");

            ctor.Emit(OpCodes.Ldarg_0);
            ctor.Emit(OpCodes.Call, objCtor);

            ctor.Emit(OpCodes.Ldarg_0);
            ctor.Emit(OpCodes.Newobj, coreCtor);
            ctor.Emit(OpCodes.Stfld, field);

            ctor.Emit(OpCodes.Ret);
            return field;
        }

        /// <summary>
        /// Re-bind method to `DelegateCall` in `__mockcore` field
        /// </summary>
        private static void BindMockMethod(FieldInfo srcField, MethodInfo method, TypeBuilder proxyBuilder)
        {
            var parameters = method.GetParameters();
            var parameterTypes = new Type[parameters.Length];
            for (var i = 0; i < parameters.Length; i++) parameterTypes[i] = parameters[i].ParameterType;

            var srcMethod = typeof(MockCore).GetMethod("DelegateCall");
            if (srcMethod == null)
                throw new Exception("MockCore object is malformed");

            var methodBuilder = proxyBuilder
                .DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual, method.ReturnType, parameterTypes);

            /*var methodBuilder = proxyBuilder
                .DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.NewSlot, method.ReturnType, parameterTypes);*/

            var ilGenerator = methodBuilder.GetILGenerator();
            /*ilGenerator.Emit(OpCodes.Ldarg_0);

            for (var i = 1; i < parameters.Length + 1; i++) ilGenerator.Emit(OpCodes.Ldarg, i);

            ilGenerator.Emit(OpCodes.Ldfld, srcField);
            ilGenerator.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, srcMethod);
            ilGenerator.Emit(OpCodes.Call, srcMethod);*/
            ilGenerator.Emit(OpCodes.Ret);

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
        private static void BindMethod(MethodInfo methodToCall, MethodInfo methodOnDelegate, TypeBuilder proxyBuilder)
        {
            var parameters = methodOnDelegate.GetParameters();
            var parameterTypes = new Type[parameters.Length];
            for (var i = 0; i < parameters.Length; i++) parameterTypes[i] = parameters[i].ParameterType;

            var methodBuilder = proxyBuilder
                .DefineMethod(methodOnDelegate.Name, MethodAttributes.Public | MethodAttributes.Virtual, methodOnDelegate.ReturnType, parameterTypes);

            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            /*ilGenerator.Emit(OpCodes.Ldfld, srcField);
            for (var i = 1; i < parameters.Length + 1; i++) ilGenerator.Emit(OpCodes.Ldarg, i);
            */
            ilGenerator.Emit(methodOnDelegate.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, methodToCall);
            ilGenerator.Emit(OpCodes.Ret);
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

        /// <summary>
        /// Create and instantiate a wrapper from the source type to the target type
        /// </summary>
        public static WrapperBase GenerateWrapperPrototype(Type targetType, Type sourceType)
        {
            var wrapperType = GenerateWrapperType(targetType, sourceType);
            return (WrapperBase)Activator.CreateInstance(wrapperType);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            AppDomain.Unload(WrappersAppDomain);
        }

        /// <summary>
        /// Create and instantiate a mock from the source type to the target type
        /// </summary>
        public static object GenerateMockPrototype(Type type)
        {
            var wrapperType = GenerateMockType(type);
            return Activator.CreateInstance(wrapperType);
        }
    }
}