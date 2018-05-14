// This file contains the bulk of the low-level proxying.
// It's a mess of direct IL generation, with mixed concerns.
// Start at one of the non-internal classes and work your way back here.
namespace LiteProxy.Internal
{
    using System;
    using System.Linq;
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
        public static Type GenerateDirectDelegate(Type targetType, string keyPropertyName)
        {
            if (targetType.IsInterface)
            {
                throw new Exception("Interfaces can't be delegated to");
            }

            var wrapperTypeName = targetType.Name + "_LazyDelegate";
            if (!string.IsNullOrWhiteSpace(keyPropertyName)) wrapperTypeName += "Keyed_" + keyPropertyName;

            // cached version:
            var pregenerated = ProxyModule.GetType(wrapperTypeName, false, false);
            if (pregenerated != null) return pregenerated;


            var proxyBuilder = ProxyModule.DefineType(wrapperTypeName,
                TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
                targetType);

            var makerType = typeof(Func<>).MakeGenericType(targetType);

            // Make a constructor that DOES NOT call the base type
            BypassConstructorChain(proxyBuilder);

            // field to hold lazy delegate
            var baseFld = proxyBuilder.DefineField("__base", targetType, FieldAttributes.Public);
            var makerFld = proxyBuilder.DefineField("__baseMaker", makerType, FieldAttributes.Public); // constructor function

            // method to generate the lazy delegate
            var ensurer = proxyBuilder.DefineMethod("__EnsureBase", MethodAttributes.Family | MethodAttributes.Public, CallingConventions.HasThis);
            EmitEnsureBaseFunction(ensurer, baseFld, makerFld, makerType);


            foreach (var prop in targetType.GetProperties())
            {
                var parameterTypes = prop.GetIndexParameters().Select(p=>p.ParameterType).ToArray();
                var propertyBuilder = proxyBuilder.DefineProperty(prop.Name, PropertyAttributes.None, prop.PropertyType, parameterTypes);

                if (prop.Name == keyPropertyName) {
                    // field to hold key value
                    var keyFld = proxyBuilder.DefineField("__keyBacking", prop.PropertyType, FieldAttributes.Public);
                    // write a get/set property with a known-named backing field.
                    BindGetSetToBackingField(proxyBuilder, propertyBuilder, prop, keyFld);
                    continue;
                }

                if (prop.CanRead)
                {
                    DefineGetProxyProperty(proxyBuilder, prop, parameterTypes, ensurer, baseFld, propertyBuilder);
                }

                if (prop.CanWrite)
                {
                    DefineSetProxyProperty(proxyBuilder, prop, parameterTypes, ensurer, baseFld, propertyBuilder);
                }
            }

            return proxyBuilder.CreateType();
        }

        /// <summary>
        /// Replace the default constructor with one that call direct to object's one.
        /// This means any constructor actions are skipped
        /// </summary>
        private static void BypassConstructorChain(TypeBuilder proxyBuilder)
        {
            var constr = proxyBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName,
                CallingConventions.Standard, Type.EmptyTypes);
            var cg = constr.GetILGenerator();

            var octor = typeof(object).GetConstructor(Type.EmptyTypes);
            if (octor == null) throw new Exception("Can't find root constructor");

            cg.Emit(OpCodes.Ldarg_0); // load 'this'
            cg.Emit(OpCodes.Call, octor); // call [mscorlib]System.Object::.ctor()
            cg.Emit(OpCodes.Ret); // return
        }

        /// <summary>
        /// Make a get and set method to back a property, and bind it
        /// </summary>
        private static void BindGetSetToBackingField(TypeBuilder proxyBuilder, PropertyBuilder propertyBuilder, PropertyInfo targetProperty, FieldBuilder backingField)
        {
            var setter = proxyBuilder.DefineMethod("set_" + targetProperty.Name,
                MethodAttributes.Virtual | MethodAttributes.ReuseSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Public,
                typeof(void), new[] { targetProperty.PropertyType });
            
            var getter = proxyBuilder.DefineMethod("get_" + targetProperty.Name,
                MethodAttributes.Virtual | MethodAttributes.ReuseSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Public,
                targetProperty.PropertyType, Type.EmptyTypes);
            {
                var gg = getter.GetILGenerator();
                gg.Emit(OpCodes.Ldarg_0); // load 'this'
                gg.Emit(OpCodes.Ldfld, backingField); // load the value
                gg.Emit(OpCodes.Ret); // return the result.
            }
            {
                var gs = setter.GetILGenerator();
                gs.Emit(OpCodes.Ldarg_0); // load 'this'
                gs.Emit(OpCodes.Ldarg_1); // load 'value'
                gs.Emit(OpCodes.Stfld, backingField); // store value to the field
                gs.Emit(OpCodes.Ret); // return the result.
            }
            propertyBuilder.SetGetMethod(getter);
            propertyBuilder.SetSetMethod(setter);
        }

        /// <summary>
        /// Bind a new `set_` method into a type, for a property. This will call an 'ensureProxyMethod' method, then pass the 'set' down to a proxy object
        /// </summary>
        private static void DefineSetProxyProperty(TypeBuilder proxyBuilder, PropertyInfo targetProperty, Type[] parameterTypes,
            MethodInfo ensureProxyMethod, FieldInfo proxyInstanceField, PropertyBuilder propertyBuilder)
        {
            var setParams = parameterTypes.Concat(new[] { targetProperty.PropertyType }).ToArray(); // add 'value' param
            var setter = proxyBuilder.DefineMethod("set_" + targetProperty.Name,
                MethodAttributes.Virtual | MethodAttributes.ReuseSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Public,
                typeof(void), setParams);
            var g = setter.GetILGenerator();

            g.Emit(OpCodes.Ldarg_0); // load 'this'
            g.Emit(OpCodes.Call, ensureProxyMethod); // call the ensurer function

            g.Emit(OpCodes.Ldarg_0); // load 'this'
            g.Emit(OpCodes.Ldfld, proxyInstanceField); // load the ensured delegate
            
            g.Emit(OpCodes.Ldarg_1); // load 'value'
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                g.Emit(OpCodes.Ldarg, i + 2); // load parameters
            }

            g.Emit(OpCodes.Callvirt, targetProperty.SetMethod); // call the underlying setter of the delegate
            g.Emit(OpCodes.Ret); // return control (no result).
            
            propertyBuilder.SetSetMethod(setter);
        }
        
        /// <summary>
        /// Bind a new `get_` method into a type, for a property. This will call an 'ensureProxyMethod' method, then pass the 'get' down to a proxy object
        /// </summary>
        private static void DefineGetProxyProperty(TypeBuilder proxyBuilder, PropertyInfo targetProperty, Type[] parameterTypes,
            MethodInfo ensureProxyMethod, FieldInfo proxyInstanceField, PropertyBuilder propertyBuilder)
        {
// write a getter method, that calls down to the ensure function, then calls delegate.
            var getter = proxyBuilder.DefineMethod("get_" + targetProperty.Name, MethodAttributes.Virtual | MethodAttributes.ReuseSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Public,
                targetProperty.PropertyType, parameterTypes);
            var g = getter.GetILGenerator();

            g.DeclareLocal(typeof(object));
            g.Emit(OpCodes.Ldarg_0); // load 'this'
            g.Emit(OpCodes.Call, ensureProxyMethod); // call the ensurer function

            g.Emit(OpCodes.Ldarg_0); // load 'this'
            g.Emit(OpCodes.Ldfld, proxyInstanceField); // load the ensured delegate

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                g.Emit(OpCodes.Ldarg, i + 1);
            }

            g.Emit(OpCodes.Callvirt, targetProperty.GetMethod); // call the underlying getter of the delegate

            // The C# compiler does this, but it's not needed, we can leave the value on the stack:
            //g.Emit(OpCodes.Stloc_0); // evaluation stack to local
            //g.Emit(OpCodes.Ldloc_0); // local back to evaluation stack

            g.Emit(OpCodes.Ret); // return the result.

            propertyBuilder.SetGetMethod(getter);
        }

        private static void EmitEnsureBaseFunction(MethodBuilder methodBuilder, FieldBuilder fieldToPopulate, FieldBuilder generatorFunctionField, Type generatorType)
        {
            var makerInvokeCall = generatorType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            if (makerInvokeCall == null) throw new Exception("Could not find invoker for constructor function");

            var g = methodBuilder.GetILGenerator();
            var retEnd = g.DefineLabel(); // label for return
            g.DeclareLocal(typeof(bool));
            g.Emit(OpCodes.Ldarg_0); // load 'this'
            g.Emit(OpCodes.Ldfld, fieldToPopulate); // load the '__base' ref
            g.Emit(OpCodes.Ldnull); // load a null
            g.Emit(OpCodes.Ceq); // compare: __base == null
            g.Emit(OpCodes.Brfalse_S, retEnd); // if not null, jump to return


            g.Emit(OpCodes.Ldarg_0); // load 'this'
            g.Emit(OpCodes.Ldarg_0); // load 'this'
            g.Emit(OpCodes.Ldfld, generatorFunctionField); // load constructor function of this instance
            g.Emit(OpCodes.Callvirt, makerInvokeCall); // call constructor function against that instance
            g.Emit(OpCodes.Stfld, fieldToPopulate); // store result in the holding field

            g.MarkLabel(retEnd); // set label to this position
            g.Emit(OpCodes.Ret);
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