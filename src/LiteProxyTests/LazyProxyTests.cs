using System;
using LiteProxy;
using NUnit.Framework;

namespace LiteProxyTests
{
    [TestFixture]
    public class LazyProxyTests {

        [Test]
        public void accessing_a_virtual_property_will_load_base_object()
        {
            EagerBeaver.InitCalled = false;

            var lazy = LazyDelegate.For(SideStructor);
            //Assert.That(EagerBeaver.InitCalled, Is.False);
            
            lazy.ItsComplicated = 4; // this should not have a setter!
            var accessed = lazy.ItsComplicated;

            Assert.That(accessed, Is.EqualTo(7));
            Assert.That(EagerBeaver.InitCalled, Is.True);
        }

        [Test]
        public void key_properties_can_be_skipped ()
        {
            EagerBeaver.InitCalled = false;

            var lazy = LazyDelegate.ForKeyed<EagerBeaver>("Id", 123);
            Assert.That(EagerBeaver.InitCalled, Is.False);

            var key = lazy.Id;
            Assert.That(key, Is.EqualTo(123));
            Assert.That(EagerBeaver.InitCalled, Is.False);
            
            var accessed = lazy.ItsComplicated;
            Assert.That(accessed, Is.EqualTo(7));
            Assert.That(EagerBeaver.InitCalled, Is.True);
        }

        [Test]
        public void cant_proxy_an_interface ()
        {
            var msg = Assert.Throws<Exception>(() =>
            {
                var wrong = LazyDelegate.ForKeyed<IEagerBeaver>("Id", 123);
            }).Message;

            Assert.That(msg, Is.EqualTo("Interfaces can't be delegated to"));
        }

        public EagerBeaver SideStructor()
        {
            EagerBeaver.InitCalled = true;
            return new EagerBeaver
            {
                ItsComplicated = 7
            };
        }
    }

    public interface IEagerBeaver
    {
        int ItsComplicated { get; set; }
        int Id { get; set; }
    }

    public class EagerBeaver:IEagerBeaver {
        public static bool InitCalled = false;

        public virtual int ItsComplicated { get; set; }

        public virtual int Id { get; set; }

        public EagerBeaver()
        {
            //InitCalled = true;
            //ItsComplicated = 7; // pretend this is loaded from an expensive resource
        }
    }
}