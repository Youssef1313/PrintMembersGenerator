# PrintMembersGenerator

A source generator that helps re-defining C# record's PrintMembers method to force include/exclude certain members.

This project is related to [dotnet/csharplang#3925](https://github.com/dotnet/csharplang/issues/3925).

For more information about source generators, see [Introducing C# source generators blog](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/).

## Overview

C# 9.0 introduced a new language feature called [Records](https://docs.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-9.0/records). A record is simply a reference type with some compiler-generated (synthesized) methods.

The interest of this project is on `PrintMembers` method, which the compiler uses when you call `.ToString()` on a record.

The compiler only includes all public non-override instance fields/properties in `PrintMembers`, and allows you to re-define PrintMembers if the provided implementation doesn't suite your need.

This source generator makes it easier to force-include or force-exclude certain record members in `PrintMembers` implementation through a provided attribute.

## How to use

### Add reference to the generator

Currently, this is not packed into a NuGet package. So you'll have to clone the source code, and add a reference to the *PrintMembersSourceGenerators.csproj*. The added reference should look like the following:

```xml
  <ItemGroup>
    <ProjectReference Include="PATH TO PrintMembersGenerator.csproj" 
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
```

### Use the attribute

First of all, here is the internal definition of the attribute:

```csharp
namespace SourceGenerator
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PrintMembersAttribute : Attribute
    {
        public bool ShouldInclude { get; set; } = true;
    }
}
```

As you see, the attribute can only be applied to fields and properties. So, positional record parameters are currently not supported.

- To force-include a member in `PrintMembers`, you should either use `[PrintMembers]` or `[PrintMembers(ShouldInclude = true)]`.
- To force-exclude a member in `PrintMembers`, you should use `[PrintMembers(ShouldInclude = false)]`.

## Notes

1. If the semantics of the added attribute is the **same** as what the compiler does, the generator ignores the attribute.
2. The record **must** be partial to allow the source generator to re-define `PrintMembers`.
