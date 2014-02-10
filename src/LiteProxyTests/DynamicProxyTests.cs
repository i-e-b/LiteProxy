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
        public void simple_methods_exposed_by_proxy_return_default_values()
        {
            var fromInterface = DynamicProxy.GetInstanceFor<IInterfaceWithNoConcrete>();

            try
            {
                fromInterface.FuncWithRefType(10, null);
                fromInterface.FuncWithValType_1();
                fromInterface.FuncWithValType_2();
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }  
         
    }

    public abstract class AbstractWithNoConcrete
    {
        public string Name { get; set; }
        public abstract string Game { get; set; }
    }

    public interface IInterfaceWithNoConcrete
    {
        string Name { get; set; }
        object FuncWithRefType(int a, object b);
        int FuncWithValType_1();
        bool FuncWithValType_2();
    }
}