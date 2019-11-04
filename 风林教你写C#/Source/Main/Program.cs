using System;
using System.Linq;
using Acme.Collections;

namespace source
{
  class Program
  {
    static void Main(string[] args)
    {
      Console.WriteLine("Hello World!");
      Console.WriteLine("Please input what you want to show!");
      /* var input = Console.ReadLine();
      Console.WriteLine($"Hello World!{input}"); */
      if (args != null && args.Any())
        Console.WriteLine($"Hello World!{args[0]}");
			TestC tc = new TestC {a="123",b="123"};
    }

    class TestC
    {
      public string a { get; set; }
      public string b { get; set; }
    }
  }
}
