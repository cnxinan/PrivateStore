using System;
using System.Linq;
using Acme.Clone;
using Acme.Collections;

namespace source
{
  class Program
  {
    static void Main(string[] args)
    {
      Console.WriteLine("Hello World!");
      Console.WriteLine("Please input what you want to show!");

      if (args != null && args.Any())
        Console.WriteLine($"Hello World:{args[0]}");

      //use $ to format string of gettting input
      /* var input = Console.ReadLine();
      Console.WriteLine($"Hello World:{input}"); */

      //use stack
      string firstStr = "it is the first string";
      string secondStr = "it is the first string";

      Stack stack = new Stack();
      stack.Push(firstStr);
      stack.Push(secondStr);

      Console.WriteLine($"The first element is {stack.Pop()}, second element is {stack.Pop()}");

      //serialize
      CloneTestClass ali = new CloneTestClass() { Name = "Ali", Age = 20 };

      var tencent = (CloneTestClass)Clone.DeepClone(ali);
      Console.WriteLine($"Before change Ali:Name is{ali.Name} and Age is {ali.Age}");
      Console.WriteLine($"Before change Tencent:Name is{tencent.Name} and Age is {tencent.Age}");
      tencent.Age = 30;
      tencent.Name = "Tencent";
      Console.WriteLine($"After change Ali:Name is{ali.Name} and Age is {ali.Age}");
      Console.WriteLine($"After change Tencent:Name is{tencent.Name} and Age is {tencent.Age}");

      try
      {
        var mayBeException = Clone.DeepClone(new SeriClassB() { Name = "Test" });
      }
      catch
      {
        Console.WriteLine($"SeriClassB can't be serialize cause SeriClassA don't have the attribute Serializeable!");
      }
    }
  }
}
