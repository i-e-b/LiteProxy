namespace LiteProxy.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Delegate core of mocking
    /// </summary>
    public class MockCore : IMock
    {
        readonly List<Invocation> _callsMade;
        readonly Dictionary<string, List<FilteredCallback>> _setups;
        static readonly object Lock = new object();

        /// <summary> Create a new mock core base </summary>
        public MockCore()
        {
            _callsMade = new List<Invocation>();
            _setups = new Dictionary<string, List<FilteredCallback>>();
        }

        /// <summary>
        /// Delegate call for mock objects
        /// </summary>
        public object DelegateCall(string invocationName, object[] parameters)
        {
            FilteredCallback callback = null;
            lock (Lock)
            {
                var inv = new Invocation { MethodName = invocationName, Parameters = parameters };
                _callsMade.Add(inv);

                if (_setups.ContainsKey(invocationName))
                {
                    callback = _setups[invocationName].FirstOrDefault(i=>i.Predicate(inv));
                }
            }

            return (callback == null) ? null : callback.Callback(new Type[0], parameters);
        }

        /// <summary>
        /// Returns an in-order list of calls made to the mock
        /// </summary>
        public IEnumerable<Invocation> CallsMade()
        {
            return _callsMade.ToList();
        }

        /// <summary>
        /// Clear list of recorded calls
        /// </summary>
        public void ClearCalls()
        {
            lock (Lock)
            {
                _callsMade.Clear();
            }
        }

        /// <summary>
        /// Setup an invocation callback.
        /// <para>Filter used to deterimine if callback should be used.</para>
        /// <para>Each setup is used in order, and only once</para>
        /// </summary>
        public void AddSetup(string methodName, Predicate<Invocation> filter, DelegateCallback callback)
        {
            lock (Lock)
            {
                if (!_setups.ContainsKey(methodName)) _setups.Add(methodName, new List<FilteredCallback>());
                _setups[methodName].Add(new FilteredCallback { Predicate = filter, Callback = callback});
            }
        }

        /// <summary>
        /// Remove all setup callbacks
        /// </summary>
        public void CleanSetups()
        {
            lock (Lock)
            {
                _setups.Clear();
            }
        }
    }

    /// <summary>
    /// Callback with filter terms
    /// </summary>
    public class FilteredCallback
    {
        /// <summary>
        /// Filter. If the predicate returns true, the callback will be called and consumed.
        /// </summary>
        public Predicate<Invocation> Predicate { get; set; }

        /// <summary>
        /// Callback actions
        /// </summary>
        public DelegateCallback Callback { get; set; }
    }

    /// <summary>
    /// Invocation of a call to a mock
    /// </summary>
    public class Invocation
    {
        /// <summary>
        /// Name of method called
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// Parameters used in call
        /// </summary>
        public object[] Parameters { get; set; }

        /// <summary>
        /// Generic types, if any
        /// </summary>
        public Type[] GenericTypes{get;set;}
    }

    /// <summary>
    /// Mock setup and verification interface
    /// </summary>
    public interface IMock
    {
        /// <summary>
        /// Returns an in-order list of calls made to the mock
        /// </summary>
        IEnumerable<Invocation> CallsMade();

        /// <summary>
        /// Clear list of recorded calls
        /// </summary>
        void ClearCalls();

        /// <summary>
        /// Setup an invocation callback.
        /// <para>Filter used to deterimine if callback should be used.</para>
        /// <para>Each setup is used in order, and only once</para>
        /// </summary>
        void AddSetup(string methodName, Predicate<Invocation> filter, DelegateCallback callback);

        /// <summary>
        /// Remove all setup callbacks
        /// </summary>
        void CleanSetups();
    }

    /// <summary>
    /// Callback used to when a mock is called to take actions and provide results
    /// </summary>
    public delegate object DelegateCallback(Type[] genericTypes, object[] incomingParameters);

    /// <summary>
    /// Extension methods to access mock properties
    /// </summary>
    public static class MockExtensions
    {
        /// <summary>
        /// Expose mock interface of mocked object.
        /// </summary>
        public static IMock AsMock(this object target)
        {
            var core = target as MockCore;
            if (core == null) throw new ArgumentException("Target was not a mock object", "target");

            return core;
        }
    }
}