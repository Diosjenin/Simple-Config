# SimpleConfig

Configuration library for C#/.NET, designed to parse concrete types from an arbitrary number of configuration sources.  High-level architecture is as follows:

* ConfigWrapper: Takes a configuration object and exposes a deep copy as a dictionary of string key-value pairs
* ConfigRepository: Retrieves string values from a series of ConfigWrappers, with priority given by the order they were passed in. Supports iteration over all associated concrete parameters (e.g. for logging) and refreshing of changing config data
* ConfigParameter: Container for a single value, genericized to match the desired concrete value type. Supports default values, constraints (e.g. minimum integer values), custom parsing, temporary value overrides (e.g. for testing), and associated metadata for detailed and selective logging

All operations are as thread-safe as possible (within the confines of the .NET system libraries).

All custom classes are 100% covered by their own unit tests, and (where appropriate) have additional tests for thread safety and memory leaks.  All classes compile without any warnings from the Microsoft All Rules warning set, with a few select exceptions made (and suppressed) for stylistic and testing purposes only.

#### Great! Can I use it?

Knock yourself out (as per the license), but I won't be packaging or supporting it.  Here's why:

The one major capability that still I'd like to integrate is the ability to deal with hierarchical configurations, such as JSON/XML files.  This would require a massive and very much breaking refactor, and for obvious reasons, I'd like to make that refactor before publishing a package.  After it was suggested to me that [the new ASP.NET Core configuration system](https://github.com/aspnet/Configuration) might account for this and provide inspiration, I discovered that said official library is not only quite comprehensive, but in fact uses almost exactly the same high-level architectural design as SimpleConfig does.  Vindicating, yes... but still.

While there are enough feature differences from the official library to justify SimpleConfig's existence, the best path forward is probably to use the official ConfigurationProvider classes in place of my own ConfigWrappers for parsing and retrieval.  Which in turn means an even more massive and breaking refactor.

So the ConfigurationProvider integration will happen in a brand new codebase, whenever I get around to it.  This version of SimpleConfig will probably remain as-is, and it exists on GitHub purely for posterity.

## License

SimpleConfig is licensed under the [MIT license](LICENSE).
