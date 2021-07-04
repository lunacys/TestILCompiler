using System;

namespace TestILCompiler
{
    class Program
    {
        static void Main(string[] args)
        {
            var mono = new MonoCecil();
            mono.ExecuteHelloWorld();

            var refl = new ReflectionEmit();
            refl.ExecuteHelloWorld();
        }
    }
}