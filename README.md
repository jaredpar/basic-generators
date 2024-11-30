# Basic Generators

Collection of roslyn source generators and analyzers.

## AutoEquality

This generator automatically adds equality to types annotated with `[AutoEquality]`

```csharp
[AutoEquality]
partial class Person
{
    string FirstName;
    string LastName;
}
```

This will generate:

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

This attribute is meant to make value equality easier and will generate equality code aimed towards that goal. This means it will compare collections by value, and not by reference. This table shows the defaults for various types:

| Type | Default Equality |
| --- | --- |
| Types implementing `IEnumerable<T>` | Sequence equality |
| `string` | Case sensitive |
| Primitive types | Value equality |
