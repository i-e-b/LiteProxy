LiteProxy
=========

Tiny object proxying and mocking tools for C#

TODO
------
 1. Add the delegating behaviour of `Extract<>.From()` to `DynamicProxy`.
 2. Expose the delegating object (as dynamic?) through dynamic proxy generation
    * methods are always mapped to delegate object
	* fields are mapped to delegate if they exist, otherwise getters, setters and backing field are added as currently