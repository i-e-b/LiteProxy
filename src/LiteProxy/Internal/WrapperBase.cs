namespace LiteProxy.Internal
{
    /// <summary>
    /// Helper class for runtime interface extraction.
    /// Do not use directly, call Extract&lt;interface&gt;.From(object) instead.
    /// </summary>
    public class WrapperBase
    {
        /// <summary>
        ///  WARNING: this field is directly referenced in the wrapper generator. Don't change it!
        /// </summary>
        internal protected object Src;
        internal object NewFromPrototype(object src)
        {
            var newWrapper = (WrapperBase)MemberwiseClone();
            newWrapper.Src = src;
            return newWrapper;
        }
    }
}