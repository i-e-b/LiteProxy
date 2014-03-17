namespace LiteProxy
{
    using LiteProxy.Internal;

    /// <summary>
    /// A very lightweight mocking object
    /// </summary>
    public class Mock
    {
        /// <summary>
        /// Create a mock of type `T`
        /// </summary>
        public static T Of<T>()
        {
            return (T)WrapperGenerator.GenerateMockPrototype(typeof(T));
        }
    }
}