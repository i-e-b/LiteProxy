namespace LiteProxy.Internal
{
    /// <summary>
    /// <para>Internal</para>
    /// Wrapper generator for `Extract`
    /// </summary>
    public class StaticWrapper<TInterface, TConcrete>
    {
        /// <summary>
        /// Use a wrapper to expose a concrete as an interface
        /// </summary>
        public static TInterface Cast(TConcrete src)
        {
            var prototype = WrapperGenerator.GenerateWrapperPrototype(typeof(TInterface), typeof(TConcrete));
            return (TInterface)prototype.NewFromPrototype(src);
        }
    }
}