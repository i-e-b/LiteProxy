namespace LiteProxy.Internal
{
    public class StaticWrapper<TInterface, TConcrete>
    {
        public static TInterface Cast(TConcrete src)
        {
            var prototype = WrapperGenerator.GenerateWrapperPrototype(typeof(TInterface), typeof(TConcrete));
            return (TInterface)prototype.NewFromPrototype(src);
        }
    }
}