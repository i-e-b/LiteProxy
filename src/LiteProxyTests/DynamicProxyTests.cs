#pragma warning disable 183
namespace LiteProxyTests
{
    using System;
    using LiteProxy;
    using NUnit.Framework;

    [TestFixture]
    public class DynamicProxyTests
    {
        [Test]
        public void proxy_for_interface_using_generic_type_has_getters_and_setters ()
        {
            var fromInterface = DynamicProxy.GetInstanceFor<IInterfaceWithNoConcrete>();
            fromInterface.Name = "Name!";

            Assert.That(fromInterface.Name, Is.EqualTo("Name!"));
        }

        [Test]
        public void proxy_for_abstract_using_generic_type_has_getters_and_setters()
        {
            var fromAbstract = DynamicProxy.GetInstanceFor<AbstractWithNoConcrete>();
            fromAbstract.Name = "Name!";
            fromAbstract.Game = "fame, shame";

            Assert.That(fromAbstract.Name, Is.EqualTo("Name!"));
            Assert.That(fromAbstract.Game, Is.EqualTo("fame, shame"));
        }

        [Test]
        public void proxy_for_interface_using_type_struct_has_getters_and_setters()
        {
            dynamic fromInterface = DynamicProxy.GetInstanceFor(typeof(IInterfaceWithNoConcrete));
            fromInterface.Name = "Name!";

            Assert.That(fromInterface.Name, Is.EqualTo("Name!"));
        }

        [Test]
        public void proxy_for_abstract_using_type_struct_has_getters_and_setters()
        {
            dynamic fromAbstract = DynamicProxy.GetInstanceFor(typeof(AbstractWithNoConcrete));
            fromAbstract.Name = "Name!";
            fromAbstract.Game = "fame, shame";

            Assert.That(fromAbstract.Name, Is.EqualTo("Name!"));
            Assert.That(fromAbstract.Game, Is.EqualTo("fame, shame"));
        }

        [Test]
        public void proxy_objects_can_be_cast_to_target_types()
        {
            dynamic fromInterface = DynamicProxy.GetInstanceFor(typeof(IInterfaceWithNoConcrete));
            dynamic fromAbstract = DynamicProxy.GetInstanceFor(typeof(AbstractWithNoConcrete));

            Assert.That(fromInterface as IInterfaceWithNoConcrete, Is.Not.Null);
            Assert.That(fromAbstract as AbstractWithNoConcrete, Is.Not.Null);
        }


        [Test]
        public void proxy_for_interface_can_be_compared_to_target_type_as_a_subclass()
        {
            var fromInterface = DynamicProxy.GetInstanceFor<IInterfaceWithNoConcrete>();

            if (fromInterface == null) throw new Exception();

            Assert.That(fromInterface is IInterfaceWithNoConcrete, Is.True);
            Assert.That(fromInterface.GetType() == typeof(IInterfaceWithNoConcrete), Is.False);
        }

        [Test]
        public void proxy_for_abstract_can_be_compared_to_target_type_as_a_subclass()
        {
            var fromAbstract = DynamicProxy.GetInstanceFor<AbstractWithNoConcrete>();

            if (fromAbstract == null) throw new Exception();

            Assert.That(fromAbstract is AbstractWithNoConcrete, Is.True);
            Assert.That(fromAbstract.GetType() == typeof(AbstractWithNoConcrete), Is.False);
        }

        [Test]
        public void methods_on_interface_throw_NotImplementedException_on_call ()
        {
            var fromInterface = DynamicProxy.GetInstanceFor<IInterfaceWithNoConcrete>();

            Assert.Throws<NotImplementedException>(() => fromInterface.FuncWithRefType(10, null));
            Assert.Throws<NotImplementedException>(() => fromInterface.FuncWithValType_1());
            Assert.Throws<NotImplementedException>(() => fromInterface.FuncWithValType_2());
        }

        [Test]
        public void unimplemented_methods_on_abstract_throw_NotImplementedException_on_call()
        {
            var fromAbstract = DynamicProxy.GetInstanceFor<AbstractWithNoConcrete>();

            Assert.Throws<NotImplementedException>(() => fromAbstract.Unimplemented());
        }

        [Test]
        public void implemented_methods_on_abstract_invoke_on_call()
        {
            var fromAbstract = DynamicProxy.GetInstanceFor<AbstractWithNoConcrete>();

            Assert.That(fromAbstract.RealMethod(), Is.EqualTo("Hi"));
        }  
         
    }

    public abstract class AbstractWithNoConcrete
    {
        public string Name { get; set; }
        public abstract string Game { get; set; }
        public abstract string Unimplemented();
        public string RealMethod() { return "Hi"; }
    }

    public interface IInterfaceWithNoConcrete
    {
        string Name { get; set; }
        object FuncWithRefType(int a, object b);
        int FuncWithValType_1();
        bool FuncWithValType_2();
    }
}