using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
internal static class InspectGambleManaged
{
    static void Main(string[] args)
    {
        Console.OutputEncoding=Encoding.UTF8;
        using(var module=ModuleDefinition.ReadModule(args[0],new ReaderParameters{ReadingMode=ReadingMode.Deferred,ReadSymbols=false}))
        foreach(var type in module.Types.Where(t=>Regex.IsMatch(t.FullName,args[1],RegexOptions.IgnoreCase)))
        {
            Console.WriteLine("TYPE "+type.FullName);
            foreach(var f in type.Fields)Console.WriteLine("FIELD "+f.FieldType+" "+f.Name);
            foreach(var m in type.Methods)
            {
                Console.WriteLine("METHOD "+m.FullName);
                if(args.Length>2 && m.HasBody)foreach(var i in m.Body.Instructions)Console.WriteLine(i.ToString());
            }
        }
    }
}
