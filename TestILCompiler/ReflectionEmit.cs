using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Threading;
using Lokad.ILPack;

namespace TestILCompiler
{
    enum Associativities
    {
        Right,
        Left
    };

    /// <summary>
    /// An exceptional state that happens during parsing.
    /// </summary>
    class ParseException : Exception
    {

        public ParseException(string msg) : base(msg)
        {

        }
    }

    /// <summary>
    /// An exceptional state during compilation
    /// </summary>
    class CompileException : Exception
    {

        public CompileException(string msg) : base(msg)
        {

        }
    }

    /// <summary>
    /// Any of the arithmetic operators, e.g. +-*/^%
    /// </summary>
    class Operator
    {

        public string Representation;
        public int Precedence;
        public Associativities Associativity = Associativities.Left;

        public Operator(string repr, int prec, Associativities assoc)
        {
            Representation = repr;
            Precedence = prec;
            Associativity = assoc;
        }
    }

    public class CalculatorProgram
    {
        // the internal representation is postfix
        private List<string> Postfix = new List<string>();
        private List<string> Tokens = new List<string>();
        private Stack<string> OperatorStack = new Stack<string>();
        private readonly List<Operator> Operators = new List<Operator>();

        private bool IsOperator(string s)
        {
            foreach (var o in Operators)
            {
                if (s == o.Representation)
                {
                    return true;
                }
            }

            return false;
        }

        private Operator OperatorFromString(string s)
        {
            foreach (Operator o in Operators)
            {
                if (o.Representation == s)
                {
                    return o;
                }
            }

            throw new ParseException("Unknown operator '" + s + "'.");
        }

        private static bool GreaterPrecedence(Operator a, Operator b)
        {
            return (a.Precedence > b.Precedence) ||
                   ((a.Precedence == b.Precedence) &&
                    (b.Associativity == Associativities.Left));
        }

        private void PopOperators(string token)
        {
            Operator cur = OperatorFromString(token);

            if (OperatorStack.Count == 0)
            {
                return;
            }

            try
            {
                for (Operator top = OperatorFromString(OperatorStack.Peek());
                    (OperatorStack.Count > 0 && GreaterPrecedence(top, cur));
                    top = OperatorFromString(OperatorStack.Peek()))
                {
                    Postfix.Add(OperatorStack.Pop());
                }
            }
            catch (ParseException)
            {
                // it's a parenthesis
                return;
            }

        }

        public void Execute()
        {
            // create everything needed to start writing CIL
            AssemblyName name = new AssemblyName();
            name.Name = "CalculatorExpression";
            // make a run-only assembly (can't save as .exe)
            AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect);
            ModuleBuilder module = asm.DefineDynamicModule("CalculatorExpressionModule");
            TypeBuilder tpb = module.DefineType("ExpressionExecutor", TypeAttributes.Public);
            // the method that will hold our expression code
            MethodBuilder meth = tpb.DefineMethod("WriteValue", MethodAttributes.Public | MethodAttributes.Static);

            // thing that writes CIL to the method
            ILGenerator il = meth.GetILGenerator();

            int intToken; // a var to parse int tokens into
            
            // loop through the postfix AST:
            foreach (string token in Postfix)
            {

                // push ints onto the stack
                if (int.TryParse(token, out intToken))
                {
                    il.Emit(OpCodes.Ldc_I4, intToken);
                }
                // compile operators
                else if (IsOperator(token))
                {
                    switch (token)
                    {
                        case "+":
                            il.Emit(OpCodes.Add);
                            break;
                        case "-":
                            il.Emit(OpCodes.Sub);
                            break;
                        case "*":
                            il.Emit(OpCodes.Mul);
                            break;
                        case "/": // division can use either / or ÷
                        case "÷":
                            il.Emit(OpCodes.Div);
                            break;
                        case "%":
                            il.Emit(OpCodes.Rem);
                            break;
                        case "^":

                            // convert top two values to float64, since that's what Math.Pow expects
                            LocalBuilder tmp = il.DeclareLocal(typeof(int));
                            il.Emit(OpCodes.Stloc, tmp);
                            il.Emit(OpCodes.Conv_R8);
                            il.Emit(OpCodes.Ldloc, tmp);
                            il.Emit(OpCodes.Conv_R8);

                            // call Math.Pow
                            il.EmitCall(OpCodes.Call, typeof(Math).GetMethod("Pow"), null);

                            // convert the float64 return value to int32
                            il.Emit(OpCodes.Conv_I4);

                            break;
                        default:
                            throw new CompileException("Unknown operator: '" + token + "'.");
                    }

                }
                else
                {
                    // if the token's not an integer or an operator, 
                    // barf appropriately
                    if ("()".Contains(token))
                    {
                        throw new CompileException("Unbalanced parentheses.");
                    }
                    else
                    {
                        throw new CompileException(
                            "Unable to compile expression; unknown token '" +
                            token + "'.");
                    }
                }
            }

            // result needs a variable so we can WriteLine it
            LocalBuilder result = il.DeclareLocal(typeof(int));
            il.Emit(OpCodes.Stloc, result);

            // print and return
            il.EmitWriteLine(result);
            il.Emit(OpCodes.Ret); // this will err if values left on stack

            // bake the type definition
            tpb.CreateType();

            // empty argument array
            object[] noArgs = new object[0];

            // call the method we wrote (on a null object because we don't
            // need one for static method)
            asm.GetType("ExpressionExecutor").GetMethod(
                "WriteValue").Invoke(null, noArgs);

            var generator = new AssemblyGenerator();
            generator.GenerateAssembly(asm, "ReflectionEmitCalculator.exe");
        }


        private void Parse()
        {

            foreach (string token in Tokens)
            {
                if (IsOperator(token))
                {
                    PopOperators(token);
                    OperatorStack.Push(token);
                }
                else if (token == "(")
                {
                    // parens only go on stack temporarily, 
                    // as they're not ops
                    OperatorStack.Push(token);
                }
                else if (token == ")")
                {
                    while (OperatorStack.Peek() != "(")
                    {
                        Postfix.Add(OperatorStack.Pop());
                    }

                    OperatorStack.Pop();
                }
                else if (int.TryParse(token, out int throwaway))
                {
                    Postfix.Add(token);
                }
                else
                {
                    throw new ParseException(
                        "Unrecognized token: " + token + ".");
                }
            }

            while (OperatorStack.Count > 0)
            {
                Postfix.Add(OperatorStack.Pop());
            }
        }

        private void Tokenize(string expression)
        {
            // place to accumulate digits
            string number = "";

            // loop by char through code
            foreach (char c in expression)
            {

                // operators can end tokens as well as being one
                if (IsOperator(c.ToString()) ||
                    "()".Contains(c.ToString()))
                {

                    if (number != "")
                    {
                        Tokens.Add(number);
                        number = "";
                    }

                    Tokens.Add(c.ToString());
                }
                // spaces end numbers
                else if (c == ' ')
                {
                    if (number != "")
                    {
                        Tokens.Add(number);
                        number = "";
                    }
                }
                // digits can combine, so store each one
                else if ("0123456789".Contains(c.ToString()))
                {
                    number += c;
                }
                else
                {
                    // barf if you encounter something not a digit,
                    // space, or operator
                    throw new ParseException(
                        "Unexpected character '" + c.ToString() + "'.");
                }
            }

            // add the last token if there is one
            if (number != "")
            {
                Tokens.Add(number);
            }

            return;
        }

        public CalculatorProgram(string code)
        {
            Operators.Add(new Operator("+", 2, Associativities.Left));
            Operators.Add(new Operator("-", 2, Associativities.Left));
            Operators.Add(new Operator("*", 3, Associativities.Left));
            Operators.Add(new Operator("/", 3, Associativities.Left));
            Operators.Add(new Operator("÷", 3, Associativities.Left));
            Operators.Add(new Operator("^", 4, Associativities.Right));
            Operators.Add(new Operator("%", 4, Associativities.Left));

            // Parse an AST from the code
            Tokenize(code);
            Parse();
        }
    }

    public class ReflectionEmit
    {
        public ReflectionEmit()
        {

        }

        private string PromptExpression()
        {
            Console.WriteLine("Enter an integer arithmetic expression: ");
            return Console.ReadLine();
        }

        public void ExecuteHelloWorld()
        {
            /*var myApp = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("HelloWorldEmit"),
                AssemblyBuilderAccess.Run);

            var module = myApp.DefineDynamicModule("HelloWorldEmitModule");
            var typeBuilder = module.DefineType("ExpressionExecutor", TypeAttributes.Public);

            var methodBuilder =
                typeBuilder.DefineMethod("WriteValue", MethodAttributes.Public | MethodAttributes.Static);

            var il = methodBuilder.GetILGenerator();
            */

            try
            {

                string expr;

                while ((expr = PromptExpression()) != "")
                {
                    CalculatorProgram program = new CalculatorProgram(expr);
                    program.Execute();
                }

            }
            catch (NullReferenceException)
            {
                // stdin EOF — this is a fine way to exit
                return;
            }
            catch (ParseException e)
            {
                Console.WriteLine(
                    "Unparseable expression: " + e.Message);
            }
            catch (CompileException e)
            {
                Console.WriteLine(
                    "Unable to compile expression: " + e.Message);
            }
        }
    }
}