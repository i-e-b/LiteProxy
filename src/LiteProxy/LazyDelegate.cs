using System;
using System.Reflection;
using LiteProxy.Internal;

namespace LiteProxy
{
    /// <summary>
    /// Produce a superclass of the target that constructs a new instance when its properties are accessed.
    /// <para/>
    /// Properties must be marked as `virtual`, and sealed classes can't be delegated.
    /// </summary>
    public class LazyDelegate {
        /// <summary>
        /// Wrap a constructor call in a lazy wrapper
        /// </summary>
        public static T For<T>(Func<T> constructor){
            // get or create the dynamic type
            var derivedType = WrapperGenerator.GenerateDirectDelegate(typeof(T), null);
            
            // Make an instance
            var actual = (T)Activator.CreateInstance(derivedType);

            // inject the constructor function:
            derivedType
                .GetField("__baseMaker", BindingFlags.Public | BindingFlags.Instance)
               ?.SetValue(actual, constructor);

            return actual;
        }

        /// <summary>
        /// Wrap a constructor call in a lazy wrapper. The wrapper will hold the 'key' property's value,
        /// and accessing that value will not cause the constructor trigger
        /// </summary>
        public static T ForKeyed<T>(string keyProperty, object keyValue, Func<T> constructor) {
            // get or create the dynamic type
            var derivedType = WrapperGenerator.GenerateDirectDelegate(typeof(T), keyProperty);
            
            // Make an instance
            var actual = (T)Activator.CreateInstance(derivedType);

            // inject the constructor function:
            derivedType
                .GetField("__baseMaker", BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(actual, constructor);
            
            // inject the key value
            var back = derivedType .GetField("__keyBacking", BindingFlags.Public | BindingFlags.Instance);
            if (back == null) throw new Exception("Backing field was never written. Check the property name is correct.");
            back.SetValue(actual, keyValue);

            return actual;
        }
    }
}