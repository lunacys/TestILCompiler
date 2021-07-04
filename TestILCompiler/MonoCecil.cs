using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TestILCompiler
{
    public class MonoCecil
    {
        public MonoCecil()
        {
            
        }

        public void ExecuteHelloWorld()
        {
            var myApp = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("HelloWorld", new Version(1, 0, 0, 0)), "HelloWorld", ModuleKind.Console);

            var module = myApp.MainModule;

            // create the program type and add it to the module
            var programType = new TypeDefinition("HelloWorld", "Program", TypeAttributes.Class | TypeAttributes.Public,
                module.TypeSystem.Object);
            
            module.Types.Add(programType);

            // add an empty constructor
            var ctor = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName, module.TypeSystem.Void);

            var il = ctor.Body.GetILProcessor();
            
            il.Append(il.Create(OpCodes.Ldarg_0));
            
            il.Append(il.Create(OpCodes.Call, module.ImportReference(typeof(object).GetConstructor(Array.Empty<Type>()))));
            
            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ret));
            
            programType.Methods.Add(ctor);
            
            // define the 'Main' method and add it to 'Program'
            var mainMethod = new MethodDefinition("Main",
                MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
            
            programType.Methods.Add(mainMethod);
            
            // add the 'args' parameter
            var argsParameter = new ParameterDefinition("args",
                ParameterAttributes.None, module.ImportReference(typeof(string[])));

            mainMethod.Parameters.Add(argsParameter);

            // create the method body
            il = mainMethod.Body.GetILProcessor();

            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ldstr, "Hello World"));

            var writeLineMethod = il.Create(OpCodes.Call,
                module.ImportReference(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) })));

            // call the method
            il.Append(writeLineMethod);

            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ret));

            // set the entry point and save the module
            myApp.EntryPoint = mainMethod;
            myApp.Write("HelloWorld.exe");
        }
    }
}