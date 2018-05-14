LiteProxy
=========

Tiny object proxying tools for C#

Features:
---------

`DynamicProxy.GetInstanceFor<...>();` -- Pass the type of an interface or abstract, and you will get an object instance that implements that type and backs the properties and fields.

`Extract<Interface>.From(Instance)` -- Wraps an object instance in a proxy. Use this to coerce objects into interfaces they *could* implement.

`LazyDelegate.For(...)` -- Wraps a constructor function in a proxy that overrides all virtual properties. Any access to the properties will cause the function to be invoked and calls passed to the result.

`LazyDelegate.ForKeyed("Id", MyValue, ConstructorFunc)` -- Same as LazyDelegate.For, but this causes one named property with an initial value to skip the lazy invocation.

TODO
------
 1. Add the delegating behaviour of `Extract<>.From()` to `DynamicProxy`.
 2. Expose the delegating object (as dynamic?) through dynamic proxy generation
    * methods are always mapped to delegate object
	* fields are mapped to delegate if they exist, otherwise getters, setters and backing field are added as currently