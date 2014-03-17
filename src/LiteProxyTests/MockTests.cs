namespace LiteProxyTests
{
    using System.Dynamic;
    using LiteProxy;
    using LiteProxy.Internal;
    using NUnit.Framework;

    [TestFixture]
    public class MockTests
    {
        SimpleClass _subject;

        [SetUp]
        public void a_mock_of_a_class()
        {
            _subject = Mock.Of<SimpleClass>();
        }

        [Test]
        public void should_be_able_to_cast_back_to_an_IMock()
        {
            var mock = _subject.AsMock();

            Assert.That(mock, Is.Not.Null);
        }

        [Test]
        public void calls_to_mock_are_routed_to_MockCore()
        {
            _subject.PublicVoidMethod();
	    var mock = _subject.AsMock();
            Assert.That(mock.CallsMade(), Is.Not.Empty);
        }
    }

    public class Exper : DynamicObject
    {
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            //var meta = new DynamicDictionary();

            result = null;// new SimpleClass();
            return true;
        }
    }

    /*public class SimpleClass
    {
        public void PublicVoidMethod() { }
        public void VoidWithArgs(int i, string s) { }
        public int ReturnsInt() { return 0; }
        public int ReturnsIntWithArg(int i) { return 0; }

        public string StringProperty { get; set; }
    }*/
    public interface SimpleClass
    {
        void PublicVoidMethod();
        void VoidWithArgs(int i, string s);
        int ReturnsInt();
        int ReturnsIntWithArg(int i);

        string StringProperty { get; set; }
    }
}