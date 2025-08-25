using System.Reflection;

var ublAssembly = typeof(UblSharp.UblDocument).Assembly;

Console.WriteLine("UblSharp Assembly Types (first 100):");

var types = ublAssembly.GetTypes()
    .Where(t => t.Name.Contains("Invoice") || t.Name.Contains("Type"))
    .Take(100)
    .OrderBy(t => t.FullName)
    .ToList();

foreach (var type in types)
{
    Console.WriteLine($"{type.FullName}");
}

Console.WriteLine($"\nTotal types found: {types.Count}");