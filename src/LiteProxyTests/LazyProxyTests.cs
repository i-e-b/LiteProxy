using System;
using System.Collections.Generic;
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
            Assert.That(EagerBeaver.InitCalled, Is.False);
            
            var accessed = lazy.ItsComplicated;

            Assert.That(accessed, Is.EqualTo(7));
            Assert.That(EagerBeaver.InitCalled, Is.True);
        }

        [Test]
        public void writing_to_a_virtual_indexer_will_load_base_object()
        {
            EagerBeaver.InitCalled = false;

            var lazy = LazyDelegate.For(SideStructor);
            Assert.That(EagerBeaver.InitCalled, Is.False, "Init called before");
            
            lazy[1] = 8;

            Assert.That(EagerBeaver.InitCalled, Is.True, "Init called after");
            Assert.That(lazy[1], Is.EqualTo(8), "lazy instance's value");
        }
        [Test]
        public void writing_to_a_virtual_property_will_load_base_object()
        {
            EagerBeaver.InitCalled = false;

            var lazy = LazyDelegate.For(SideStructor);
            Assert.That(EagerBeaver.InitCalled, Is.False, "Init called before");
            
            lazy.ItsComplicated = 4;

            Assert.That(EagerBeaver.InitCalled, Is.True, "Init called after");
            Assert.That(lazy.ItsComplicated, Is.EqualTo(4), "lazy instance's value");
        }

        [Test]
        public void accessing_a_virtual_indexer_will_load_base_object()
        {
            EagerBeaver.InitCalled = false;

            var lazy = LazyDelegate.For(SideStructor);
            Assert.That(EagerBeaver.InitCalled, Is.False);
            
            var accessed = lazy[1];

            Assert.That(accessed, Is.EqualTo(2));
            Assert.That(EagerBeaver.InitCalled, Is.True);
        }

        [Test]
        public void key_properties_can_be_skipped ()
        {
            EagerBeaver.InitCalled = false;

            var lazy = LazyDelegate.ForKeyed("Id", 123, SideStructor);
            Assert.That(EagerBeaver.InitCalled, Is.False);

            var key = lazy.Id;
            Assert.That(key, Is.EqualTo(123));
            Assert.That(EagerBeaver.InitCalled, Is.False);
            
            var accessed = lazy.ItsComplicated;
            Assert.That(accessed, Is.EqualTo(7));
            Assert.That(EagerBeaver.InitCalled, Is.True);
        }

        [Test]
        public void key_properties_can_be_updated ()
        {
            EagerBeaver.InitCalled = false;

            var lazy = LazyDelegate.ForKeyed("Id", 123, SideStructor);

            var key = lazy.Id;
            Assert.That(key, Is.EqualTo(123), "Original key not set");

            lazy.Id = 456;
            Assert.That(lazy.Id, Is.EqualTo(456), "Failed to update key");
        }

        [Test]
        public void cant_proxy_an_interface ()
        {
            var msg = Assert.Throws<Exception>(() =>
            {
                var _ = LazyDelegate.For<IEagerBeaver>(SideStructor);
            }).Message;

            Assert.That(msg, Is.EqualTo("Interfaces can't be delegated to"));
        }

        [Test]
        public void can_handle_constructor_chains_that_reference_properties()
        {
            var lazy = LazyDelegate.For(ChainConstructor);

            Assert.That(lazy.ThingA, Is.EqualTo("PhilsFace"));
        }

        public EagerBeaver SideStructor()
        {
            EagerBeaver.InitCalled = true;
            return new EagerBeaver
            {
                ItsComplicated = 7
            };
        }
        public TopOfChain ChainConstructor()
        {
            return new TopOfChain();
        }
    }

    public interface IEagerBeaver
    {
        int ItsComplicated { get; set; }
        int Id { get; set; }
    }

    public class EagerBeaver : IEagerBeaver
    {
        public static bool InitCalled;

        public virtual int ItsComplicated { get; set; }

        public virtual int Id { get; set; }

        private readonly List<int> _backing = new List<int>(new[] { 1, 2, 3 });
        public virtual int this[int idx]
        {
            get { return _backing[idx]; }
            set { _backing[idx] = value; }
        }
    }

    public class TopOfChain : MiddleOfChain
    {
        public virtual object Whatever { get; set; }

        public TopOfChain()
        {
            Whatever = new object();
        }
    }

    public class MiddleOfChain
    {
        public virtual string ThingA { get; set; }

        public MiddleOfChain()
        {
            ThingA = "PhilsFace";
        }
    }
}