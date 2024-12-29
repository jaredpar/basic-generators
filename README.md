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

This generator automates value equality for types and has defaults that support that goal. For example it will compare collections by value, not by reference. This table shows the defaults for various types:

| Type | Default Equality |
| --- | --- |
| Types implementing `IEnumerable<T>` | Sequence equality |
| `string` | Case sensitive |
| Primitive types | Value equality |

Individual members can customize their equality with the `[AutoEqualityMember]` attribute by specifying the `AutoEqualityKind` value:

| AutoEqualityKind | Description |
| --- | --- |
| None | This member is not considered for equality |
| Default | Use the default for this member |
| Ordinal | Uses `Ordinal` comparer. Only applicable on `string` types |
| OrdinalIgnoreCase | Uses `OrdinalIgnoreCase` comparer. Only applicable on `string` types |
| CurrentCulture | Uses `CurrentCulture` comparer. Only applicable on `string` types |
| CurrentCultureIgnoreCase | Uses `CurrentCultureIgnoreCase` comparer. Only applicable on `string` types |
| InvariantCulture | Uses `InvariantCulture` comparer. Only applicable on `string` types |
| InvariantCultureIgnoreCase | Uses `InvariantCultureIgnoreCase` comparer. Only applicable on `string` types |
| Sequence | Compare the sequence of elements. The type in question must have a `SequenceEquals` extension method |

Any field or property that has a `get` and a `set / init` member will be considered for equality.
