using System;
using UblSharp;

// Test what's available in UblSharp
namespace Test
{
    class Program
    {
        static void Main()
        {
            // This will help us understand the available types
            Console.WriteLine("UblSharp types available:");
            var assembly = typeof(UblDocument).Assembly;
            foreach (var type in assembly.GetTypes().Take(50))
            {
                Console.WriteLine($"{type.Namespace}.{type.Name}");
            }
        }
    }
}