
// See https://aka.ms/new-console-template for more information
using Demo;

Console.WriteLine("Hello, World!");
var person1 = new Person("jared", "doe");
var person2 = new Person("penny", "doe");
if (person1 == person2)
{

}

namespace Demo
{
    partial class Person
    {
        string FirstName;
        string LastName;

        public Person(string firstName, string lastName)
        {
            FirstName = firstName;
            LastName = lastName;
        })
    }
}
