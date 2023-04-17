# basic-generators
Roslyn Source Generators and Analyzers

## AutoEquality
This generator automatically adds equality to types annotated with `[AutoEquality]`. 

```csharp
[AutoEquality]
partial class Person
{
    string FirstName;
    string LastName;
}
```

This will generate 

```csharp
partial class Person : IEquatable<Person?> 
{
    public static bool operator==(Person?, Person?);
    public static bool operator!=(Person?, Person?);
    public bool Equals(Person?);
    public override bool Equals(object?);
    public override int GetHashCode();
}
```
