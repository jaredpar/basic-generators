
using Demo;
using System.Text.RegularExpressions;


Console.WriteLine("Enter an email address");
var input = Console.ReadLine()!;
var regex = new Regex(@"[a-z0-9]+@[a-z0-9]+\.[a-z]{2,}$");
Console.WriteLine(regex.IsMatch(input));



#region demo2
var person1 = new Person("jared", "doe");
var person2 = new Person("penny", "doe");
Console.WriteLine(person1 == person2);
#endregion

