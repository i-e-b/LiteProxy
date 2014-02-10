namespace LiteProxyTests
{
    using System;
    using LiteProxy;
    using Microsoft.CSharp.RuntimeBinder;
    using NUnit.Framework;

    [TestFixture]
    public class ExtractTests
    {
        [Test]
        public void calling_interface_methods_invokes_wrapper()
        {
            var inner = new ToBeWrapped();
            var subject = Extract<IAmWrapper>.From(inner);

            Assert.That(subject.Methodical(1, "two", 1, 2, 3, 4), Is.EqualTo(8));
        }

        [Test]
        public void methods_not_exposed_by_the_wrapper_are_not_invocable()
        {
            var inner = new ToBeWrapped();
            dynamic subject = Extract<IAmWrapper>.From(inner);

            var ex = Assert.Throws<RuntimeBinderException>(() => subject.Other());
            Assert.That(ex.Message, Contains.Substring("does not contain a definition for 'Other'"));
        }

        [Test]
        public void trying_to_wrap_a_concrete_with_an_incompatible_interface_throws_an_exception()
        {
            var inner = new ToBeWrapped();
            
            var ex = Assert.Throws<MissingMethodException>(() => Extract<IIncompatibleWrapper>.From(inner));
            Assert.That(ex.Message, Contains.Substring(
                "NoSuchMethod is not implemented by LiteProxyTests.ToBeWrapped as required by the LiteProxyTests.IIncompatibleWrapper interface"));
        }

    }

    public class ToBeWrapped
    {
        public int Methodical(int a, string b, params object[] other)
        {
            return a + b.Length + other.Length;
        }

        /// <summary>
        /// This is not visible or callable from the wrapper
        /// </summary>
        public void Other()
        {

        }
    }

    public interface IAmWrapper
    {
        int Methodical(int a, string b, params object[] other);
    }

    public interface IIncompatibleWrapper
    {
        void NoSuchMethod();
    }
}