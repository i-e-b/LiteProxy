namespace LiteProxy
{
    using System;
    using LiteProxy.Internal;

    /// <summary>
    /// Extract an interface wrapper from a concrete that could implement it.
    /// </summary>
    /// <typeparam name="TInterface">Interface type</typeparam>
    public class Extract<TInterface>
    {
        /// <summary>
        /// Extract an interface wrapper from a concrete that could implement it.
        /// </summary>
        /// <param name="src">source concrete instance</param>
        /// <returns>A wrapper to the source instance which is of type `TInterface`</returns>
        public static TInterface From<TConcrete>(TConcrete src)
        {
            if (!typeof(TInterface).IsInterface) throw new ArgumentException("Target type must be an interface");
            return StaticWrapper<TInterface, TConcrete>.Cast(src);
        }
    }
}