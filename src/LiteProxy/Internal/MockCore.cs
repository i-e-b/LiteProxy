namespace LiteProxy.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Delegate core of mocking
    /// </summary>
    public class MockCore : IMock
    {
        /// <summary>
        /// Delegate call for mock objects
        /// </summary>
        public object DelegateCall(string invocationName, object[] parameters)
        {
            return null;
        }

        public IEnumerable<Invocation> CallsMade()
        {
            throw new NotImplementedException();
        }

        public void ClearCalls()
        {
            throw new NotImplementedException();
        }

        public void AddSetup(string methodName, Predicate<Invocation> filter, DelegateCallback callback)
        {
            throw new NotImplementedException();
        }

        public void CleanSetups()
        {
            throw new NotImplementedException();
        }
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